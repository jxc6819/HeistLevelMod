using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IEYTD_Mod2Code
{
    public sealed class LevelLoader
    {

        readonly string _bundleFileName;
        readonly string _fallbackSceneAssetPath;
        readonly string _mergedRootName;
        readonly float _sceneLoadTimeout;

        readonly string[] _keepNameContains;

        public bool _roomProbeEnabled = true;
        public Vector3 _roomProbeSize = new Vector3(28f, 10f, 18f);
        public Vector3 _roomProbeCenterLocal = new Vector3(0f, 4f, 0f);
        public float _roomProbeIntensity = 0.25f;
        public bool _roomProbeBoxProjection = true;

        public bool _mattePassEnabled = true;
        public float _matteSmoothness = 0.02f;
        public string[] _matteRendererNameContains = new[]
        {
            "wall", "floor", "ceiling", "room", "hull", "interior", "corridor", "panel", "bulkhead"
        };

        public float _renderSettingsReflectionIntensity = 0.75f;

        bool _roomProbeRenderPending;
        ReflectionProbe _roomProbe;
        GameObject _roomProbeGO;

        readonly string _redLightName;
        readonly Vector3 _redLightWorldPos;
        readonly float _redLightIntensity;
        readonly float _coloredLightBoostFactor;

        AssetBundle _bundle;
        string _sceneName, _scenePath;
        string _pendingAdditive;
        bool _loading;

        GameObject _mergedRoot;
        Camera _hmd;

        void SetBlind(bool blind)
        {
            try { LevelUtil.HellenKeller(blind); } catch { }
        }

        ReflectionProbe _globalProbe;
        Cubemap _brightFallbackCube;

        readonly Dictionary<Light, float> _origLightIntensity = new Dictionary<Light, float>();

        public bool IsLoading => _loading;
        public GameObject MergedRoot => _mergedRoot;

        public Action<GameObject> OnMergeFinished;

        Shader _phoenixPackedOpaque;
        Shader _phoenixDefaultOpaque;
        Shader _phoenixCutout;
        Shader _phoenixTransparent;

        readonly List<Material> _mergedMats = new List<Material>();

        public LevelLoader(
            string bundleFileName,
            string fallbackSceneAssetPath,
            string mergedRootName,
            float sceneLoadTimeout,
            string[] keepNameContains,

            string redLightName = "Red Light",
            Vector3? redLightWorldPos = null,
            float redLightIntensity = 15f,
            float coloredLightBoostFactor = 2.0f
        )
        {
            _bundleFileName = bundleFileName;
            _fallbackSceneAssetPath = fallbackSceneAssetPath;
            _mergedRootName = mergedRootName;
            _sceneLoadTimeout = sceneLoadTimeout;
            _keepNameContains = keepNameContains ?? Array.Empty<string>();

            _redLightName = redLightName;
            _redLightWorldPos = redLightWorldPos ?? new Vector3(0f, 5f, -3f);
            _redLightIntensity = redLightIntensity;
            _coloredLightBoostFactor = coloredLightBoostFactor;
        }

        public void BeginMerge()
        {
            if (_loading)
            {
                MelonLogger.Warning("[LevelLoader] Merge already in progress.");
                return;
            }
            SetBlind(true);
            MelonCoroutines.Start(Co_BeginMerge());
        }

        bool EnsureBundle() => _bundle != null && !string.IsNullOrEmpty(_sceneName) ? true : TryReadBundle();

        bool TryReadBundle()
        {
            try
            {
                var path = Path.Combine(MelonUtils.UserDataDirectory, _bundleFileName);

                if (!File.Exists(path))
                {
                    MelonLogger.Error("[Bundle] Not found at: " + path);
                    _scenePath = _fallbackSceneAssetPath;
                    _sceneName = Path.GetFileNameWithoutExtension(_scenePath);
                    return false;
                }

                _bundle = AssetBundle.LoadFromFile(path);
                if (_bundle == null)
                {
                    MelonLogger.Error("[Bundle] LoadFromFile returned null.");
                    return false;
                }

                _scenePath = _fallbackSceneAssetPath;
                try
                {
                    var ps = _bundle.GetAllScenePaths();
                    if (ps != null && ps.Length > 0) _scenePath = ps[0];
                    if (ps != null && ps.Length > 0) MelonLogger.Msg("[Bundle] Scenes: " + string.Join(", ", ps));
                }
                catch { }

                _sceneName = Path.GetFileNameWithoutExtension(_scenePath);
                MelonLogger.Msg("[Bundle] Scene ready: name='" + (_sceneName ?? "null") + "'  path='" + _scenePath + "'");
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[Bundle] " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        [UnhollowerBaseLib.Attributes.HideFromIl2Cpp]
        IEnumerator Co_BeginMerge()
        {
            if (_loading) yield break;
            _loading = true;

            try
            {
                SetBlind(true);
                if (!EnsureBundle())
                {
                    MelonLogger.Error("[Merge] Bundle not ready.");
                    yield break;
                }

                string toLoadName = _sceneName;
                string toLoadPath = _scenePath;

                AsyncOperation op = null;
                _pendingAdditive = null;

                try
                {

                    op = SceneManager.LoadSceneAsync(toLoadName, LoadSceneMode.Additive);
                    _pendingAdditive = toLoadName;
                }
                catch
                {
                    try
                    {
                        op = SceneManager.LoadSceneAsync(toLoadPath, LoadSceneMode.Additive);
                        _pendingAdditive = Path.GetFileNameWithoutExtension(toLoadPath);
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error("[Merge] Could not start additive load: " + ex.Message);
                        yield break;
                    }
                }

                if (op == null)
                {
                    MelonLogger.Error("[Merge] LoadSceneAsync returned null.");
                    yield break;
                }

                float t = 0f;
                while (true)
                {
                    var sc = SceneManager.GetSceneByName(_pendingAdditive);
                    if (sc.IsValid() && sc.isLoaded) break;

                    t += Time.deltaTime;
                    if (t > _sceneLoadTimeout)
                    {
                        MelonLogger.Error("[Merge] Timeout waiting for additive scene '" + _pendingAdditive + "'.");
                        yield break;
                    }
                    SetBlind(true);
                    yield return null;
                }

                yield return MelonCoroutines.Start(Co_MergeCore(_pendingAdditive));
            }
            finally
            {
                _loading = false;
                _pendingAdditive = null;
            }
        }

        [UnhollowerBaseLib.Attributes.HideFromIl2Cpp]
        IEnumerator Co_MergeCore(string loadedSceneName)
        {
            SetBlind(true);

            var src = SceneManager.GetSceneByName(loadedSceneName);
            if (!src.IsValid() || !src.isLoaded)
            {
                MelonLogger.Error("[Merge] Internal error: source scene not loaded.");
                yield break;
            }

            var dst = SceneManager.GetActiveScene();

            _mergedRoot = GameObject.Find(_mergedRootName) ?? new GameObject(_mergedRootName);
            SceneManager.MoveGameObjectToScene(_mergedRoot, dst);

            int moved = 0, enabled = 0;

            var allGos = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < allGos.Length; i++)
            {
                var go = allGos[i];
                if (!go || !go.scene.IsValid() || go.scene != src) continue;
                if (go.transform.parent != null) continue;

                if (go.name == "VRRig" || go.name == "Player" || go.name == "HMD") continue;

                SceneManager.MoveGameObjectToScene(go, dst);
                go.transform.SetParent(_mergedRoot.transform, true);

                enabled += HardEnableDraw(go);
                moved++;
            }

            _hmd = FindHMDCamera();
            if (_hmd) _hmd.depthTextureMode |= DepthTextureMode.Depth;

            if (_hmd)
            {
                _hmd.nearClipPlane = 0.01f;
                _hmd.farClipPlane = 1000f;
                _hmd.useOcclusionCulling = false;
                try { _hmd.allowHDR = true; } catch { }

                EnsureLayerVisibleToCamera(_mergedRoot, _hmd);
            }
            if (_hmd) ForceAllToVisibleLayer(_mergedRoot, _hmd);

            EnableDepthTexturesOnAllCameras();

            if (QualitySettings.pixelLightCount < 4) QualitySettings.pixelLightCount = 4;
            try { QualitySettings.realtimeReflectionProbes = true; } catch { }

            MelonLogger.Msg($"[Merge] Merged {moved} roots; renderers enabled: {enabled}. ColorSpace={QualitySettings.activeColorSpace}");

            HarvestPhoenixShaders();
            ConvertMergedMaterialsToPhoenix();
            SyncAllPhoenixTiling();

            try { RenderSettings.reflectionIntensity = Mathf.Clamp(_renderSettingsReflectionIntensity, 0f, 2f); } catch { }

            EnsureBrightFallbackReflectionCubemap();

            DisableOtherReflectionProbes();

            ApplyProbeUsage_NoSkyFallback(_mergedRoot);

            BuildOrUpdateGlobalProbe(true);

            _roomProbeRenderPending = _roomProbeEnabled;
            if (_roomProbeRenderPending)
            {

                SetBlind(true);
                yield return null;
                SetBlind(true);
                yield return null;

                ApplyRoomReflectionProbe(_mergedRoot.transform);
            }

            if (_mattePassEnabled)
                ApplyMattePassToEnvironment(_mergedRoot);

            TuneRedLight();
            ApplyColoredLightAssist(_coloredLightBoostFactor, true);

            SceneManager.UnloadSceneAsync(src);
            CleanHostRoots();

            DumpEnvDebug();
            RenderSettings.reflectionIntensity = 0f;

            try { OnMergeFinished?.Invoke(_mergedRoot); } catch { }

            SetBlind(true);
            MelonLogger.Msg("[Merge] Done.");
            yield break;
        }

        static int FirstVisibleLayerFromMask(int mask)
        {
            for (int i = 0; i < 32; i++)
                if (((mask >> i) & 1) != 0) return i;
            return 0;
        }

        void ForceAllToVisibleLayer(GameObject root, Camera cam)
        {
            if (!root || !cam) return;

            int layer = FirstVisibleLayerFromMask(cam.cullingMask);
            var trs = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < trs.Length; i++)
                trs[i].gameObject.layer = layer;

            MelonLogger.Msg("[Layers] Forced ModLevel_ROOT hierarchy to layer " + layer);
        }

        int HardEnableDraw(GameObject root)
        {
            if (root == null) return 0;
            root.SetActive(true);

            int count = 0;
            var rends = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (r == null) continue;
                count++;
                try
                {
                    r.enabled = true;
#if UNITY_2019_4_OR_NEWER
                    try { r.forceRenderingOff = false; } catch { }
#endif
                    r.shadowCastingMode = ShadowCastingMode.On;
                    r.receiveShadows = true;
                    r.allowOcclusionWhenDynamic = false;

                    var mats = r.sharedMaterials;
                    bool changed = false;
                    for (int m = 0; m < mats.Length; m++)
                    {
                        var mat = mats[m];
                        if (mat == null) continue;
                        if (mat.HasProperty("_Color"))
                        {
                            var col = mat.color;
                            if (col.a < 0.99f) { col.a = 1f; mat.color = col; changed = true; }
                        }
                    }
                    if (changed) r.sharedMaterials = mats;
                }
                catch { }
            }
            return count;
        }

        static Camera FindHMDCamera()
        {
            var cams = Resources.FindObjectsOfTypeAll<Camera>();
            for (int i = 0; i < cams.Length; i++)
            {
                var c = cams[i];
                if (c == null) continue;
                if (!c.gameObject.scene.IsValid()) continue;
                if (!c.gameObject.activeInHierarchy) continue;
                if (c.name == "HMD") return c;
            }
            return null;
        }

        static void EnsureLayerVisibleToCamera(GameObject go, Camera cam)
        {
            if (go == null || cam == null) return;

            int layer = go.layer;
            int mask = cam.cullingMask;

            if (((mask >> layer) & 1) == 0)
            {
                for (int i = 0; i < 32; i++)
                {
                    if (((mask >> i) & 1) != 0)
                    {
                        SetLayerRecursive(go, i);
                        break;
                    }
                }
            }
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            var t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i).gameObject, layer);
        }

        void EnableDepthTexturesOnAllCameras()
        {
            var cams = Resources.FindObjectsOfTypeAll<Camera>();
            int n = 0;
            for (int i = 0; i < cams.Length; i++)
            {
                var c = cams[i];
                if (!c || !c.gameObject.scene.IsValid()) continue;
                c.depthTextureMode |= DepthTextureMode.Depth;
                n++;
            }
            MelonLogger.Msg($"[Depth] DepthTextureMode.Depth enabled on {n} camera(s).");
        }

        [UnhollowerBaseLib.Attributes.HideFromIl2Cpp]
        void ApplyProbeUsage_NoSkyFallback(GameObject root)
        {
            if (!root) return;

            var rends = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (!r) continue;

                try { r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes; } catch { }
                try { r.lightProbeUsage = LightProbeUsage.BlendProbes; } catch { }
            }
        }

        [UnhollowerBaseLib.Attributes.HideFromIl2Cpp]
        private void ApplyRoomReflectionProbe(Transform anchor)
        {
            if (!_roomProbeEnabled) return;
            if (anchor == null) anchor = _mergedRoot != null ? _mergedRoot.transform : null;
            if (anchor == null) return;

            if (_roomProbeGO == null)
            {
                _roomProbeGO = new GameObject("LL_RoomReflectionProbe");
                _roomProbeGO.transform.SetParent(anchor, false);

                _roomProbe = _roomProbeGO.AddComponent<ReflectionProbe>();
                _roomProbe.hdr = true;
                _roomProbe.cullingMask = ~0;
                _roomProbe.mode = ReflectionProbeMode.Realtime;
                _roomProbe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
                _roomProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                _roomProbe.boxProjection = true;

                try { _roomProbe.clearFlags = ReflectionProbeClearFlags.SolidColor; } catch { }
                try { _roomProbe.backgroundColor = Color.black; } catch { }
            }

            _roomProbe.size = _roomProbeSize;
            _roomProbe.center = _roomProbeCenterLocal;
            _roomProbe.intensity = _roomProbeIntensity;
            _roomProbe.boxProjection = _roomProbeBoxProjection;
            _roomProbe.enabled = true;

            if (_roomProbeRenderPending)
            {
                _roomProbeRenderPending = false;
                try { _roomProbe.RenderProbe(); } catch { }
            }
        }

        bool RendererMatchesMatteFilter(Renderer r)
        {
            if (r == null) return false;
            var n = (r.name ?? "").ToLowerInvariant();
            for (int i = 0; i < _matteRendererNameContains.Length; i++)
            {
                var token = (_matteRendererNameContains[i] ?? "").ToLowerInvariant();
                if (token.Length == 0) continue;
                if (n.Contains(token)) return true;
            }
            return false;
        }

        void MakeMaterialMatte(Material m)
        {
            if (m == null) return;

            var shName = (m.shader != null) ? (m.shader.name ?? "") : "";
            if (shName.IndexOf("Phoenix/", StringComparison.OrdinalIgnoreCase) < 0) return;

            float sm = Mathf.Clamp01(_matteSmoothness);

            try { if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", sm); } catch { }
            try { if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", sm); } catch { }
            try { if (m.HasProperty("_Gloss")) m.SetFloat("_Gloss", sm); } catch { }

            try { if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f); } catch { }
            try { if (m.HasProperty("_Metalness")) m.SetFloat("_Metalness", 0f); } catch { }

            try
            {
                if (m.HasProperty("_MaskMap"))
                {

                    SupplyNeutralMaskMap(m, 0f, sm);
                }
            }
            catch { }
        }

        [UnhollowerBaseLib.Attributes.HideFromIl2Cpp]
        void ApplyMattePassToEnvironment(GameObject root)
        {
            if (!root) return;

            int touched = 0;

            var rends = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (!r) continue;

                var mats = r.sharedMaterials;
                if (mats == null) continue;

                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (mat == null) continue;

                    var sh = (mat.shader != null) ? (mat.shader.name ?? "") : "";
                    if (sh.IndexOf("Phoenix/", StringComparison.OrdinalIgnoreCase) < 0) continue;

                    MakeMaterialMatte(mat);
                    touched++;
                }
            }

            MelonLogger.Msg("[Matte] Applied matte pass to " + touched + " material slot(s). Smoothness=" + _matteSmoothness.ToString("0.###"));
        }

        void EnsureBrightFallbackReflectionCubemap()
        {
            try
            {
                if (_brightFallbackCube == null)
                {
                    int s = 16;
                    _brightFallbackCube = new Cubemap(s, TextureFormat.RGBAHalf, false);
                    _brightFallbackCube.wrapMode = TextureWrapMode.Clamp;
                    _brightFallbackCube.hideFlags = HideFlags.DontUnloadUnusedAsset;

                    Color c = new Color(0.75f, 0.78f, 0.82f, 1f);
                    Color[] px = new Color[s * s];
                    for (int i = 0; i < px.Length; i++) px[i] = c;

                    _brightFallbackCube.SetPixels(px, CubemapFace.PositiveX);
                    _brightFallbackCube.SetPixels(px, CubemapFace.NegativeX);
                    _brightFallbackCube.SetPixels(px, CubemapFace.PositiveY);
                    _brightFallbackCube.SetPixels(px, CubemapFace.NegativeY);
                    _brightFallbackCube.SetPixels(px, CubemapFace.PositiveZ);
                    _brightFallbackCube.SetPixels(px, CubemapFace.NegativeZ);
                    _brightFallbackCube.Apply(false, false);
                }

                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflection = _brightFallbackCube;

                RenderSettings.reflectionIntensity = Mathf.Clamp(_renderSettingsReflectionIntensity, 0f, 2f);
                RenderSettings.reflectionBounces = 1;

                if (RenderSettings.ambientMode != AmbientMode.Skybox &&
                    RenderSettings.ambientLight.maxColorComponent < 0.1f)
                {
                    RenderSettings.ambientMode = AmbientMode.Flat;
                    RenderSettings.ambientLight = new Color(0.14f, 0.14f, 0.14f);
                }

                MelonLogger.Msg("[BrightEnv] Injected bright fallback reflection cubemap.");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[BrightEnv] " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        void BuildOrUpdateGlobalProbe(bool forceRender)
        {
            if (_mergedRoot == null) return;

            if (_globalProbe == null)
            {
                var go = new GameObject("ModLevel_GlobalReflectionProbe");
                go.transform.SetParent(_mergedRoot.transform, false);

                _globalProbe = go.AddComponent<ReflectionProbe>();
                _globalProbe.mode = ReflectionProbeMode.Realtime;
                _globalProbe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
                _globalProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                _globalProbe.boxProjection = true;
                _globalProbe.importance = 100;
                _globalProbe.hdr = true;

                try { _globalProbe.clearFlags = ReflectionProbeClearFlags.SolidColor; } catch { }
                try { _globalProbe.backgroundColor = Color.black; } catch { }
            }

            Bounds world;
            if (!TryComputeWorldBounds(_mergedRoot, out world))
                world = new Bounds(_mergedRoot.transform.position, Vector3.one * 20f);

            _globalProbe.transform.position = world.center;
            _globalProbe.center = Vector3.zero;

            Vector3 size = world.size;
            if (size.sqrMagnitude < 0.01f) size = Vector3.one * 10f;

            _globalProbe.size = size * 1.25f;
            _globalProbe.cullingMask = ~0;
            _globalProbe.intensity = 1.0f;
            _globalProbe.enabled = true;

            if (forceRender)
            {
                try { _globalProbe.RenderProbe(); } catch { }
            }

            MelonLogger.Msg("[Probe] Global probe ready. Size=" + _globalProbe.size + " Center=" + world.center);
        }

        bool TryComputeWorldBounds(GameObject root, out Bounds b)
        {
            b = new Bounds(root.transform.position, Vector3.zero);
            bool inited = false;

            var rends = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (r == null) continue;

                if (!inited) { b = r.bounds; inited = true; }
                else b.Encapsulate(r.bounds);
            }

            return inited;
        }

        void TuneRedLight()
        {
            if (_mergedRoot == null) return;

            var lights = _mergedRoot.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                var L = lights[i];
                if (L == null) continue;

                if (string.Equals(L.name, _redLightName, StringComparison.OrdinalIgnoreCase))
                {
                    _origLightIntensity[L] = _redLightIntensity;

                    try { L.transform.position = _redLightWorldPos; } catch { }
                    L.intensity = _redLightIntensity;
                    L.renderMode = LightRenderMode.ForcePixel;
                    L.shadows = LightShadows.Soft;
                    L.cullingMask = ~0;

                    MelonLogger.Msg("[RedLight] Positioned to " + _redLightWorldPos + " and set intensity " + _redLightIntensity);
                    return;
                }
            }

            MelonLogger.Warning("[RedLight] Light named '" + _redLightName + "' not found.");
        }

        void ApplyColoredLightAssist(float factor, bool log)
        {
            if (_mergedRoot == null) return;

            int n = 0, affected = 0;
            var lights = _mergedRoot.GetComponentsInChildren<Light>(true);

            for (int i = 0; i < lights.Length; i++)
            {
                var L = lights[i];
                if (L == null) continue;
                n++;

                if (string.Equals(L.name, _redLightName, StringComparison.OrdinalIgnoreCase))
                    continue;

                Color c = L.color;
                float max = Math.Max(c.r, Math.Max(c.g, c.b));
                float min = Math.Min(c.r, Math.Min(c.g, c.b));
                float sat = max > 0f ? (max - min) / max : 0f;
                if (sat < 0.25f) continue;

                if (!_origLightIntensity.ContainsKey(L)) _origLightIntensity[L] = L.intensity;

                L.intensity = _origLightIntensity[L] * factor;
                L.renderMode = LightRenderMode.ForcePixel;
                L.shadows = LightShadows.Soft;
                L.cullingMask = ~0;

                affected++;
            }

            if (log)
                MelonLogger.Msg("[Lights] Colored light assist applied ×" + factor.ToString("0.##") +
                                " on " + affected + "/" + n + " lights (skipped '" + _redLightName + "').");
        }

        void DumpEnvDebug()
        {
            string sky = "(none)";
            try
            {
                if (RenderSettings.skybox != null)
                    sky = (RenderSettings.skybox.shader != null) ? RenderSettings.skybox.shader.name : "(no shader)";
            }
            catch { }

            MelonLogger.Msg("[ENV] AmbientMode=" + RenderSettings.ambientMode
                + " AmbientInt=" + RenderSettings.ambientIntensity.ToString("0.##")
                + " RefInt=" + RenderSettings.reflectionIntensity.ToString("0.##")
                + " DefaultRefl=" + RenderSettings.defaultReflectionMode
                + " Skybox=" + sky);

            if (_hmd != null)
            {
                try { MelonLogger.Msg("[ENV] HMD allowHDR=" + _hmd.allowHDR + " cullingMask=0x" + _hmd.cullingMask.ToString("X8")); } catch { }
            }
            if (_globalProbe != null)
            {
                try { MelonLogger.Msg("[ENV] Probe pos=" + _globalProbe.transform.position + " size=" + _globalProbe.size + " mask=0x" + _globalProbe.cullingMask.ToString("X8")); } catch { }
            }
            if (_roomProbe != null)
            {
                try { MelonLogger.Msg("[ENV] RoomProbe size=" + _roomProbe.size + " center=" + _roomProbe.center + " intensity=" + _roomProbe.intensity.ToString("0.##")); } catch { }
            }
            MelonLogger.Msg("[ENV] PixelLights=" + QualitySettings.pixelLightCount);
        }

        [UnhollowerBaseLib.Attributes.HideFromIl2Cpp]
        void DisableOtherReflectionProbes()
        {
            var probes = Resources.FindObjectsOfTypeAll<ReflectionProbe>();
            int off = 0, kept = 0;

            for (int i = 0; i < probes.Length; i++)
            {
                var p = probes[i];
                if (!p) continue;

                bool isOurs = false;
                try
                {
                    if (_mergedRoot != null && p.transform.IsChildOf(_mergedRoot.transform)) isOurs = true;
                    if (!isOurs && (p.name ?? "").IndexOf("ModLevel_", StringComparison.OrdinalIgnoreCase) >= 0) isOurs = true;
                    if (!isOurs && (p.name ?? "").IndexOf("LL_RoomReflectionProbe", StringComparison.OrdinalIgnoreCase) >= 0) isOurs = true;
                }
                catch { }

                if (isOurs) { kept++; continue; }

                try { p.enabled = false; } catch { }
                try { p.intensity = 0f; } catch { }
                off++;
            }

            MelonLogger.Msg("[Probe] Disabled " + off + " non-mod ReflectionProbe(s), kept " + kept + ".");
        }

        void CleanHostRoots()
        {
            var active = SceneManager.GetActiveScene();
            var roots = Resources.FindObjectsOfTypeAll<Transform>();
            int deleted = 0, kept = 0;

            for (int i = 0; i < roots.Length; i++)
            {
                var t = roots[i];
                if (t == null) continue;

                var go = t.gameObject;
                if (!go.scene.IsValid() || go.scene != active) continue;
                if (t.parent != null) continue;

                if (go == _mergedRoot) { kept++; continue; }
                if (go.name == "IEYTD2_Tools_ROOT") { kept++; continue; }

                string n = (go.name ?? "").ToLowerInvariant();
                bool keepByName = false;
                for (int k = 0; k < _keepNameContains.Length; k++)
                {
                    if (n.Contains(_keepNameContains[k]))
                    {
                        keepByName = true;
                        break;
                    }
                }

                if (keepByName || go.GetComponentInChildren<ReflectionProbe>(true) != null) { kept++; continue; }
                if (go.GetComponentInChildren<Camera>(true) != null) { kept++; continue; }

                try
                {
                    UnityEngine.Object.Destroy(go);
                    deleted++;
                }
                catch { }
            }

            MelonLogger.Msg("[Clean] Deleted " + deleted + " host roots (kept rig/probes/merged/tools).");
        }

        void HarvestPhoenixShaders()
        {
            _phoenixPackedOpaque = Shader.Find("Phoenix/Packed/SH_Shared_PackedPBR_Metallic_Opaque_01");
            _phoenixDefaultOpaque = Shader.Find("Phoenix/SH_Shared_DefaultPBR_Metallic_Opaque_01");
            _phoenixCutout = Shader.Find("Phoenix/SH_Shared_DefaultPBR_Metallic_Cutout_01");
            if (_phoenixCutout == null) _phoenixCutout = _phoenixPackedOpaque;
            _phoenixTransparent = Shader.Find("Phoenix/SH_Shared_DefaultPBR_Metallic_TransparentColorAlpha_01");

            MelonLogger.Msg("[Phoenix] Packed='" + (_phoenixPackedOpaque ? _phoenixPackedOpaque.name : "null")
                + "' Default='" + (_phoenixDefaultOpaque ? _phoenixDefaultOpaque.name : "null")
                + "' Transparent='" + (_phoenixTransparent ? _phoenixTransparent.name : "null") + "'");
        }

        static bool HasKw(Material m, string kw) { try { return m.IsKeywordEnabled(kw); } catch { return false; } }

        static void CopyTexST(Material src, Material dst, string srcProp, string dstProp)
        {
            try
            {
                if (src.HasProperty(srcProp) && dst.HasProperty(dstProp))
                {
                    var t = src.GetTexture(srcProp);
                    if (t != null) dst.SetTexture(dstProp, t);
                    var st = src.GetTextureScale(srcProp);
                    var of = src.GetTextureOffset(srcProp);
                    dst.SetTextureScale(dstProp, st);
                    dst.SetTextureOffset(dstProp, of);
                }
            }
            catch { }
        }

        static void TryCopyEmission(Material src, Material dst)
        {
            try
            {
                if (src.IsKeywordEnabled("_EMISSION"))
                {
                    dst.EnableKeyword("_EMISSION");
                    if (src.HasProperty("_EmissionColor") && dst.HasProperty("_EmissionColor"))
                        dst.SetColor("_EmissionColor", src.GetColor("_EmissionColor"));

                    if (src.HasProperty("_EmissionMap") && dst.HasProperty("_EmissionMap"))
                    {
                        var t = src.GetTexture("_EmissionMap");
                        if (t != null) dst.SetTexture("_EmissionMap", t);
                        var st = src.GetTextureScale("_EmissionMap");
                        var of = src.GetTextureOffset("_EmissionMap");
                        dst.SetTextureScale("_EmissionMap", st);
                        dst.SetTextureOffset("_EmissionMap", of);
                    }
                }
            }
            catch { }
        }

        void SupplyNeutralMaskMap(Material dst, float metallic, float smoothness)
        {
            if (!dst.HasProperty("_MaskMap")) return;

            var t2 = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            t2.wrapMode = TextureWrapMode.Repeat;
            t2.filterMode = FilterMode.Bilinear;

            var c = new Color(Mathf.Clamp01(metallic), 1f, 1f, Mathf.Clamp01(smoothness));
            t2.SetPixels(new Color[] { c, c, c, c }); t2.Apply();
            t2.hideFlags = HideFlags.DontUnloadUnusedAsset;

            dst.SetTexture("_MaskMap", t2);
            try
            {
                var st = dst.GetTextureScale("_MainTex");
                var of = dst.GetTextureOffset("_MainTex");
                dst.SetTextureScale("_MaskMap", st);
                dst.SetTextureOffset("_MaskMap", of);
            }
            catch { }
        }

        static readonly string[] kPreserveShaderHints =
        {
            "Water/",
            "Stylized Ocean",
            "StylizedWater",
            "Custom/Puddle_Procedural",
            "Custom/UnlitAdditiveFire",
            "Legacy Shaders/Particles",
            "Particles/"
        };

        static bool ShouldPreserveSpecial(Material m)
        {
            if (m == null) return false;
            var sh = m.shader;
            if (sh == null) return false;
            string name = sh.name ?? "";

            for (int i = 0; i < kPreserveShaderHints.Length; i++)
                if (name.IndexOf(kPreserveShaderHints[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

            if (m.renderQueue >= 3000) return true;

            var mn = m.name ?? "";
            if (mn.IndexOf("[KEEP]", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            return false;
        }

        void ConvertMergedMaterialsToPhoenix()
        {
            if (_mergedRoot == null) return;

            var rends = _mergedRoot.GetComponentsInChildren<Renderer>(true);
            int converted = 0;

            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i]; if (r == null) continue;
                var srcMats = r.sharedMaterials; if (srcMats == null) continue;

                bool any = false;

                for (int m = 0; m < srcMats.Length; m++)
                {
                    var src = srcMats[m]; if (src == null) continue;

                    var srcShaderName = src.shader ? src.shader.name : "";
                    if (!string.IsNullOrEmpty(srcShaderName) &&
                        srcShaderName.IndexOf("Phoenix/", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    if (ShouldPreserveSpecial(src)) continue;

                    bool cutout = HasKw(src, "_ALPHATEST_ON") || src.HasProperty("_Cutoff");
                    bool blend = HasKw(src, "_ALPHABLEND_ON") || HasKw(src, "_ALPHAPREMULTIPLY_ON") || src.renderQueue >= 3000;

                    Shader target =
                        (!blend && !cutout)
                            ? (_phoenixPackedOpaque != null ? _phoenixPackedOpaque : _phoenixDefaultOpaque)
                            : (cutout ? _phoenixCutout : _phoenixTransparent);

                    if (target == null) continue;

                    var dst = new Material(target);
                    dst.name = src.name + "_Phoenix";

                    CopyTexST(src, dst, "_BaseMap", "_MainTex");
                    CopyTexST(src, dst, "_MainTex", "_MainTex");
                    CopyTexST(src, dst, "_BumpMap", "_BumpMap");
                    CopyTexST(src, dst, "_NormalMap", "_BumpMap");

                    float srcMetal = src.HasProperty("_Metallic") ? src.GetFloat("_Metallic") : 0f;
                    float srcSmooth = src.HasProperty("_Smoothness") ? src.GetFloat("_Smoothness")
                                   : (src.HasProperty("_Glossiness") ? src.GetFloat("_Glossiness") : 0.5f);

                    bool hadMetalTex =
                        (src.HasProperty("_MetallicGlossMap") && src.GetTexture("_MetallicGlossMap") != null) ||
                        (src.HasProperty("_SpecGlossMap") && src.GetTexture("_SpecGlossMap") != null);

                    float fallbackMetal = hadMetalTex ? Math.Max(0.6f, srcMetal) : srcMetal;
                    float fallbackSmooth = Mathf.Min(srcSmooth, 0.08f);

                    if (dst.HasProperty("_Metallic")) dst.SetFloat("_Metallic", Mathf.Clamp01(fallbackMetal));
                    if (dst.HasProperty("_Smoothness")) dst.SetFloat("_Smoothness", Mathf.Clamp01(fallbackSmooth));
                    if (dst.HasProperty("_Glossiness")) dst.SetFloat("_Glossiness", Mathf.Clamp01(fallbackSmooth));

                    SupplyNeutralMaskMap(dst, Mathf.Clamp01(fallbackMetal), Mathf.Clamp01(fallbackSmooth));
                    TryCopyEmission(src, dst);

                    srcMats[m] = dst;
                    _mergedMats.Add(dst);
                    any = true;
                    converted++;
                }

                if (any) r.sharedMaterials = srcMats;
            }

            MelonLogger.Msg("[Phoenix] Converted " + converted + " material(s) to Phoenix shaders.");
        }

        class STInfo { public Vector2 scale; public Vector2 offset; public string prop; }

        STInfo GetBaseST(Material m)
        {
            for (int i = 0; i < 2; i++)
            {
                string p = (i == 0) ? "_MainTex" : "_BaseMap";
                try
                {
                    if (m.HasProperty(p) && m.GetTexture(p) != null)
                    {
                        return new STInfo
                        {
                            scale = m.GetTextureScale(p),
                            offset = m.GetTextureOffset(p),
                            prop = p
                        };
                    }
                }
                catch { }
            }
            return new STInfo { scale = new Vector2(1f, 1f), offset = Vector2.zero, prop = null };
        }

        static readonly string[] kFollowerTexProps =
        {
            "_BumpMap", "_MaskMap", "_MetallicGlossMap", "_SpecGlossMap", "_OcclusionMap", "_EmissionMap"
        };

        void SyncMaterialTiling(Material m, out int changedProps)
        {
            changedProps = 0; if (m == null) return;
            var baseST = GetBaseST(m);
            if (baseST.prop == null) return;

            for (int i = 0; i < kFollowerTexProps.Length; i++)
            {
                string p = kFollowerTexProps[i];
                try
                {
                    if (!m.HasProperty(p)) continue;
                    var tex = m.GetTexture(p);
                    if (tex == null) continue;

                    var st = m.GetTextureScale(p);
                    var of = m.GetTextureOffset(p);

                    if (st != baseST.scale || of != baseST.offset)
                    {
                        m.SetTextureScale(p, baseST.scale);
                        m.SetTextureOffset(p, baseST.offset);
                        changedProps++;
                    }
                }
                catch { }
            }
        }

        int SyncAllPhoenixTiling()
        {
            if (_mergedMats == null || _mergedMats.Count == 0) return 0;

            int matsTouched = 0, props = 0;
            for (int i = 0; i < _mergedMats.Count; i++)
            {
                var m = _mergedMats[i];
                if (m == null) continue;

                string sh = (m.shader != null) ? m.shader.name : "";
                if (string.IsNullOrEmpty(sh) || sh.IndexOf("Phoenix/", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                int c; SyncMaterialTiling(m, out c);
                if (c > 0) { matsTouched++; props += c; }
            }

            MelonLogger.Msg("[UV] Synced tiling on " + matsTouched + " materials (" + props + " map STs updated).");
            return matsTouched;
        }
    }
}
