using System;
using System.Collections;
using System.Reflection;
using MelonLoader;
using SG.Phoenix.Assets.Code.Interactables;
using TMPro;
using UnhollowerBaseLib.Attributes;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public static class LevelUtil
    {

        static GameObject _mergedRoot;
        static Camera _hmd;

        static Action _restartAction;
        static Func<IEnumerator> _restartCoroutine;

        static Type _tPickUpManaged, _tShakeManaged;
        static Il2CppSystem.Type _tPickUpIl2, _tShakeIl2;

        static bool _donorSearched;
        static Component _donorPickUp;

        public static string[] PreferredDonorNameContains = { "fastfoodcup", "cup" };

        [HideFromIl2Cpp]
        public static void OnLevelLoaded(GameObject mergedRoot = null, Camera hmd = null)
        {
            _mergedRoot = mergedRoot;
            _hmd = hmd != null ? hmd : FindHMDCamera();

            _donorSearched = false;
            if (_donorPickUp != null)
            {
                try { if (!_donorPickUp) _donorPickUp = null; }
                catch { _donorPickUp = null; }
            }

            _rotDonorSearched = false;
            if (_donorRotMotion != null)
            {
                try { if (!_donorRotMotion) _donorRotMotion = null; }
                catch { _donorRotMotion = null; }
            }

        }

        public static void stageItem(GameObject obj, Vector3 pos)
        {
            obj.transform.position = pos;
            obj.SetActive(true);
        }

        public static void stageItem(GameObject obj, Vector3 pos, Vector3 rot)
        {
            obj.transform.position = pos;
            obj.transform.rotation = Quaternion.Euler(rot);
            obj.SetActive(true);
        }

        [HideFromIl2Cpp]
        public static T AttachScript<T>(GameObject go) where T : Component
        {
            if (!go) return null;
            var c = go.GetComponent<T>();
            if (c) return c;
            return go.AddComponent<T>();
        }

        [HideFromIl2Cpp]
        public static Component AttachScript(GameObject go, Type t)
        {
            if (!go || t == null) return null;

            var il2 = ToIl2(t);
            if (il2 == null) return null;

            var existing = go.GetComponent(il2);
            if (existing != null) return existing;

            return go.AddComponent(il2);
        }

        [HideFromIl2Cpp]
        public static bool MakeGrabbable(GameObject target, bool preferDonorHost = true)
        {
            if (!target)
            {
                MelonLogger.Warning("[LevelUtil] MakeGrabbable called with null target.");
                return false;
            }

            ResolvePhoenixTypes();
            if (_tPickUpIl2 == null)
            {
                MelonLogger.Error("[LevelUtil] Phoenix PickUp type not found. Can't make grabbable.");
                return false;
            }

            if (_hmd == null) _hmd = FindHMDCamera();

            if (preferDonorHost)
            {
                var donor = EnsureDonorPickUp();
                if (donor != null && TryCloneDonorAsHost(donor, target))
                    return true;
            }

            return TryFallbackAddComponents(target);
        }

        public static void MakeGrabbable(string objName)
        {
            GameObject obj = GameObject.Find(objName);
            if (obj == null) { MelonLogger.Error("[MakeGrabbable] - Object not found"); return; }
            MakeGrabbable(obj);
        }

        static bool _hellenKellerActive = false;
        static object _hardBlackoutRoutine = null;
        static GameObject _hardBlackoutQuad = null;
        static MeshRenderer _hardBlackoutRenderer = null;
        static Material _hardBlackoutMaterial = null;

        public static void blindPlayer(bool blind)
        {
            GameObject playerBlindness = GameObject.Find("PlayeBlindnessVisual");
            if (playerBlindness == null) return;

            PlayerBlindnessVisual pbv = playerBlindness.GetComponent<PlayerBlindnessVisual>();
            if (pbv == null) return;

            pbv.Blind(blind);
        }

        public static void HellenKeller(bool hellen)
        {
            _hellenKellerActive = hellen;

            if (hellen)
            {

                try { blindPlayer(true); } catch { }
                try { AudioListener.pause = true; } catch { }

                SetHardBlackoutVisible(true);

                if (_hardBlackoutRoutine == null)
                    _hardBlackoutRoutine = MelonCoroutines.Start(Co_HardBlackoutKeeper());
            }
            else
            {
                SetHardBlackoutVisible(false);

                try { blindPlayer(false); } catch { }
                try { AudioListener.pause = false; } catch { }
            }
        }

        static void SetHardBlackoutVisible(bool visible)
        {
            if (visible)
            {
                EnsureHardBlackoutQuad();
                UpdateHardBlackoutTransform();
            }

            if (_hardBlackoutQuad != null)
                _hardBlackoutQuad.SetActive(visible);
        }

        static void EnsureHardBlackoutQuad()
        {
            if (_hardBlackoutQuad != null && _hardBlackoutRenderer != null)
                return;

            _hardBlackoutQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _hardBlackoutQuad.name = "IEYTD_Mod_HardBlackout";
            UnityEngine.Object.DontDestroyOnLoad(_hardBlackoutQuad);

            Collider col = _hardBlackoutQuad.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);

            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");

            _hardBlackoutMaterial = new Material(shader);
            _hardBlackoutMaterial.name = "IEYTD_Mod_HardBlackout_Mat";
            if (_hardBlackoutMaterial.HasProperty("_Color")) _hardBlackoutMaterial.SetColor("_Color", Color.black);
            if (_hardBlackoutMaterial.HasProperty("_MainTex")) _hardBlackoutMaterial.SetTexture("_MainTex", Texture2D.blackTexture);
            _hardBlackoutMaterial.color = Color.black;
            _hardBlackoutMaterial.renderQueue = 5000;

            _hardBlackoutRenderer = _hardBlackoutQuad.GetComponent<MeshRenderer>();
            if (_hardBlackoutRenderer != null)
            {
                _hardBlackoutRenderer.sharedMaterial = _hardBlackoutMaterial;
                _hardBlackoutRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _hardBlackoutRenderer.receiveShadows = false;
            }
        }

        static Camera FindActiveBlackoutCamera()
        {
            Camera hmd = FindHMDCamera();
            if (hmd != null) return hmd;
            if (Camera.main != null) return Camera.main;

            Camera[] cams = Resources.FindObjectsOfTypeAll<Camera>();
            for (int i = 0; i < cams.Length; i++)
            {
                Camera c = cams[i];
                if (c == null) continue;
                if (!c.gameObject.scene.IsValid()) continue;
                if (!c.gameObject.activeInHierarchy) continue;
                if (!c.enabled) continue;
                return c;
            }

            return null;
        }

        static void UpdateHardBlackoutTransform()
        {
            if (_hardBlackoutQuad == null) return;

            Camera cam = FindActiveBlackoutCamera();
            if (cam == null) return;

            float z = Mathf.Max(0.05f, cam.nearClipPlane + 0.05f);
            _hardBlackoutQuad.transform.position = cam.transform.position + cam.transform.forward * z;
            _hardBlackoutQuad.transform.rotation = cam.transform.rotation;

            float height;
            if (cam.orthographic)
                height = cam.orthographicSize * 2f;
            else
                height = 2f * z * Mathf.Tan(cam.fieldOfView * 0.5f * 0.0174532924f);

            float width = height * Mathf.Max(1f, cam.aspect);
            _hardBlackoutQuad.transform.localScale = new Vector3(width * 2.5f, height * 2.5f, 1f);

            int layer = cam.gameObject.layer;
            if (((cam.cullingMask >> layer) & 1) == 0)
            {
                for (int i = 0; i < 32; i++)
                {
                    if (((cam.cullingMask >> i) & 1) != 0)
                    {
                        layer = i;
                        break;
                    }
                }
            }
            _hardBlackoutQuad.layer = layer;
        }

        [HideFromIl2Cpp]
        static IEnumerator Co_HardBlackoutKeeper()
        {
            while (_hellenKellerActive)
            {
                SetHardBlackoutVisible(true);
                try { AudioListener.pause = true; } catch { }
                yield return null;
            }

            SetHardBlackoutVisible(false);
            _hardBlackoutRoutine = null;
        }

        [HideFromIl2Cpp]
        public static void SetLayerRecursive(GameObject go, int layer)
        {
            if (!go) return;
            go.layer = layer;
            var t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i).gameObject, layer);
        }

        public static void SetHiddenVolumeRenderers(PickUp pu, GameObject targetRoot)
        {
            if (pu == null || targetRoot == null) return;

            var rends = targetRoot.GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0)
            {
                MelonLoader.MelonLogger.Warning("[HiddenVolume] No renderers found under: " + targetRoot.name);
                return;
            }

            var arr = new Il2CppReferenceArray<Renderer>(rends.Length);
            for (int i = 0; i < rends.Length; i++)
                arr[i] = rends[i];

            pu._HiddenVolumeRenderers = arr;

            MelonLoader.MelonLogger.Msg("[HiddenVolume] Registered " + rends.Length + " renderers for " + targetRoot.name);
        }

        static void ResolvePhoenixTypes()
        {
            if (_tPickUpManaged == null) _tPickUpManaged = FindTypeBySuffix(".Interactables.PickUp");
            if (_tShakeManaged == null) _tShakeManaged = FindTypeBySuffix(".Gestures.PickUpShakeGesture");

            if (_tPickUpManaged != null && _tPickUpIl2 == null) _tPickUpIl2 = Il2CppType.From(_tPickUpManaged);
            if (_tShakeManaged != null && _tShakeIl2 == null) _tShakeIl2 = Il2CppType.From(_tShakeManaged);
        }

        static Component EnsureDonorPickUp()
        {
            if (_donorPickUp != null) return _donorPickUp;
            if (_donorSearched) return null;
            _donorSearched = true;

            if (_tPickUpIl2 == null) return null;

            Component best = null;

            var allGos = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < allGos.Length; i++)
            {
                var g = allGos[i];
                if (!g || !g.scene.IsValid()) continue;

                var c = g.GetComponent(_tPickUpIl2) as Component;
                if (c == null) continue;

                var nm = (g.name ?? "").ToLowerInvariant();

                if (PreferredDonorNameContains != null)
                {
                    for (int k = 0; k < PreferredDonorNameContains.Length; k++)
                    {
                        var tok = PreferredDonorNameContains[k];
                        if (!string.IsNullOrEmpty(tok) && nm.Contains(tok))
                        {
                            _donorPickUp = c;
                            MelonLogger.Msg("[LevelUtil] Donor PickUp found (preferred): " + g.name);
                            return _donorPickUp;
                        }
                    }
                }

                if (best == null || g.GetComponent<Rigidbody>() != null) best = c;
            }

            _donorPickUp = best;
            if (_donorPickUp != null) MelonLogger.Msg("[LevelUtil] Donor PickUp found: " + _donorPickUp.gameObject.name);
            else MelonLogger.Warning("[LevelUtil] No donor PickUp found in loaded scenes.");
            return _donorPickUp;
        }

        static bool SameIl2CppObject(UnityEngine.Object a, IntPtr bPtr)
        {
            if (a == null || bPtr == IntPtr.Zero) return false;
            try
            {
                var ib = a as Il2CppObjectBase;
                if (ib == null) return false;
                return ib.Pointer == bPtr;
            }
            catch { return false; }
        }

        static IntPtr PtrOf(UnityEngine.Object o)
        {
            if (o == null) return IntPtr.Zero;
            try
            {
                var ib = o as Il2CppObjectBase;
                return ib != null ? ib.Pointer : IntPtr.Zero;
            }
            catch { return IntPtr.Zero; }
        }

        static void StripHost(GameObject host, GameObject target, Il2CppSystem.Type pickUpIl2, Il2CppSystem.Type shakeIl2)
        {
            if (!host) return;

            var rb = host.GetComponent<Rigidbody>();
            var pick = (pickUpIl2 != null) ? (host.GetComponent(pickUpIl2) as Component) : null;
            var shake = (shakeIl2 != null) ? (host.GetComponent(shakeIl2) as Component) : null;

            IntPtr rbPtr = PtrOf(rb);
            IntPtr pickPtr = PtrOf(pick);
            IntPtr shakePtr = PtrOf(shake);

            var tr = host.transform;
            var killChildren = new List<GameObject>();
            for (int i = 0; i < tr.childCount; i++)
            {
                var child = tr.GetChild(i);
                if (!child) continue;

                if (target != null && (child == target.transform || child.IsChildOf(target.transform)))
                    continue;

                killChildren.Add(child.gameObject);
            }

            for (int i = 0; i < killChildren.Count; i++)
            {
                try { UnityEngine.Object.DestroyImmediate(killChildren[i]); } catch { }
            }

            var comps = host.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i];
                if (!c) continue;

                if (c is Transform) continue;

                if (SameIl2CppObject(c, rbPtr)) continue;
                if (SameIl2CppObject(c, pickPtr)) continue;
                if (SameIl2CppObject(c, shakePtr)) continue;

                try { UnityEngine.Object.DestroyImmediate(c); } catch { }
            }

            var cols = host.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (!c) continue;

                if (target != null && (c.transform == target.transform || c.transform.IsChildOf(target.transform)))
                    continue;

                try { UnityEngine.Object.DestroyImmediate(c); } catch { }
            }

            var rends = host.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (!r) continue;

                if (target != null && (r.transform == target.transform || r.transform.IsChildOf(target.transform)))
                    continue;

                try { UnityEngine.Object.DestroyImmediate(r); } catch { }
            }

            var aud = host.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < aud.Length; i++)
            {
                var a = aud[i];
                if (!a) continue;

                if (target != null && (a.transform == target.transform || a.transform.IsChildOf(target.transform)))
                    continue;

                try { UnityEngine.Object.DestroyImmediate(a); } catch { }
            }

            if (host.GetComponent<Rigidbody>() == null)
                host.AddComponent<Rigidbody>();

            if (pickUpIl2 != null && host.GetComponent(pickUpIl2) == null)
                host.AddComponent(pickUpIl2);

            if (shakeIl2 != null && host.GetComponent(shakeIl2) == null)
                host.AddComponent(shakeIl2);
        }

        static bool TryCloneDonorAsHost(Component donorPickUp, GameObject target)
        {
            try
            {
                var donorGO = donorPickUp.gameObject;
                if (!donorGO || !target) return false;

                var host = UnityEngine.Object.Instantiate(donorGO);
                host.SetActive(true);
                host.name = "PickUp_HOST_" + target.name;

                if (_mergedRoot != null) host.transform.SetParent(_mergedRoot.transform, true);

                host.transform.position = target.transform.position;
                host.transform.rotation = target.transform.rotation;
                host.transform.localScale = target.transform.lossyScale;

                var rb = host.GetComponent<Rigidbody>() ?? host.AddComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.useGravity = true;
                if (rb.mass <= 0f) rb.mass = 1f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                if (_tPickUpIl2 != null && host.GetComponent(_tPickUpIl2) == null)
                    host.AddComponent(_tPickUpIl2);

                if (_tShakeIl2 != null && host.GetComponent(_tShakeIl2) == null)
                    host.AddComponent(_tShakeIl2);

                var donorRends = host.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < donorRends.Length; i++)
                {
                    var r = donorRends[i];
                    if (r) r.enabled = false;
                }

                target.transform.SetParent(host.transform, true);

                var targetRbs = target.GetComponentsInChildren<Rigidbody>(true);
                for (int i = 0; i < targetRbs.Length; i++)
                    if (targetRbs[i]) UnityEngine.Object.Destroy(targetRbs[i]);

                StripHost(host, target, _tPickUpIl2, _tShakeIl2);

                var il2Cols = target.GetComponentsInChildren<Collider>(true);
                var list = new List<Collider>();
                if (il2Cols != null)
                {
                    for (int i = 0; i < il2Cols.Length; i++)
                    {
                        var c = il2Cols[i];
                        if (!c) continue;

                        var mc = c as MeshCollider;
                        if (mc != null) { try { mc.convex = true; } catch { } }

                        list.Add(c);
                    }
                }

                if (list.Count == 0)
                {
                    var mf = target.GetComponentInChildren<MeshFilter>(true);
                    if (mf && mf.sharedMesh)
                    {
                        var mc = target.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                        mc.convex = true;
                        list.Add(mc);
                    }
                    else
                    {
                        list.Add(target.AddComponent<BoxCollider>());
                    }
                }

                var pickUpComp = (_tPickUpIl2 != null) ? host.GetComponent(_tPickUpIl2) as Component : null;
                if (pickUpComp != null)
                {
                    TryEnableBehaviour(pickUpComp);
                    KickPickUpEnableGuardian(pickUpComp, 4f);
                    BindCollidersToInteractable(pickUpComp, list.ToArray());
                }

                EnsureTargetVisualsOn(target);

                int interact = LayerMask.NameToLayer("Interactable");
                if (interact < 0) interact = 8;
                SetLayerRecursive(target, interact);
                EnsureLayerVisibleToCamera(target, _hmd);

                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[LevelUtil] Grab host failed: " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        static bool TryFallbackAddComponents(GameObject target)
        {
            try
            {
                if (_tPickUpIl2 == null) return false;

                BreakAllJoints(target);

                var cols = target.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < cols.Length; i++)
                {
                    var c = cols[i];
                    if (!c) continue;

                    try { c.enabled = true; } catch { }
                    var mc = c as MeshCollider;
                    if (mc != null) { try { mc.convex = true; } catch { } }
                }

                if (target.GetComponent(_tPickUpIl2) == null) target.AddComponent(_tPickUpIl2);
                if (_tShakeIl2 != null && target.GetComponent(_tShakeIl2) == null) target.AddComponent(_tShakeIl2);

                var rb = target.GetComponent<Rigidbody>() ?? target.AddComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.mass = (rb.mass <= 0f) ? 1f : rb.mass;
                rb.drag = Mathf.Max(rb.drag, 0.05f);
                rb.angularDrag = Mathf.Max(rb.angularDrag, 0.05f);
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.constraints = RigidbodyConstraints.None;
                rb.detectCollisions = true;

                bool hadCollider = false;
                for (int i = 0; i < cols.Length; i++) { if (cols[i]) { hadCollider = true; break; } }
                if (!hadCollider)
                {
                    var mf = target.GetComponentInChildren<MeshFilter>(true);
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var mc = target.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                        mc.convex = true;
                    }
                    else
                    {
                        target.AddComponent<BoxCollider>();
                    }
                }

                int interact = LayerMask.NameToLayer("Interactable");
                if (interact < 0) interact = 8;
                SetLayerRecursive(target, interact);
                EnsureLayerVisibleToCamera(target, _hmd);

                var pu = target.GetComponent(_tPickUpIl2) as Component;
                if (pu != null)
                {
                    TryEnableBehaviour(pu);
                    KickPickUpEnableGuardian(pu, 3.5f);

                    var bindCols = target.GetComponentsInChildren<Collider>(true);
                    if (bindCols != null && bindCols.Length > 0)
                        BindCollidersToInteractable(pu, bindCols);
                }

                EnsureTargetVisualsOn(target);
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[LevelUtil] Fallback attach failed: " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        static void KickPickUpEnableGuardian(Component pickUp, float seconds)
        {
            if (pickUp == null) return;
            MelonCoroutines.Start(Co_PickUpEnableGuardian(pickUp, seconds));
        }

        static IEnumerator Co_PickUpEnableGuardian(Component pickUp, float seconds)
        {
            float end = Time.time + Mathf.Max(0.25f, seconds);
            while (pickUp != null && Time.time < end)
            {
                try
                {
                    TryEnableBehaviour(pickUp);

                    var go = pickUp.gameObject;
                    if (go != null && !go.activeSelf) go.SetActive(true);

                    TrySetBoolProperty(pickUp, "IsEnabled", true);
                    TrySetBoolProperty(pickUp, "Enabled", true);
                    TrySetBoolField(pickUp, "isEnabled", true);

                    var rb = (go != null) ? go.GetComponent<Rigidbody>() : null;
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        rb.detectCollisions = true;
                    }
                }
                catch { }
                yield return null;
            }
        }

        static void TryEnableBehaviour(object comp)
        {
            if (comp == null) return;
            try
            {
                var b = comp as Behaviour;
                if (b != null) b.enabled = true;
            }
            catch { }
        }

        static bool TrySetBoolProperty(object o, string name, bool val)
        {
            try
            {
                var p = o.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.CanWrite && p.PropertyType == typeof(bool)) { p.SetValue(o, val, null); return true; }
            }
            catch { }
            return false;
        }

        static bool TrySetBoolField(object o, string name, bool val)
        {
            try
            {
                var f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null && f.FieldType == typeof(bool)) { f.SetValue(o, val); return true; }
            }
            catch { }
            return false;
        }

        static void BindCollidersToInteractable(Component interactable, Collider[] cols)
        {
            if (!interactable || cols == null) return;

            try
            {
                var arr = new Il2CppReferenceArray<Collider>(cols.Length);
                for (int i = 0; i < cols.Length; i++) arr[i] = cols[i];
                if (TrySetFirstColliderArrayField(interactable, arr)) return;
            }
            catch { }

            TrySetFirstColliderArrayField(interactable, cols);
        }

        static bool TrySetFirstColliderArrayField(object obj, object value)
        {
            var t = obj.GetType();
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

            var fields = t.GetFields(F);
            for (int i = 0; i < fields.Length; i++)
            {
                var f = fields[i];
                try
                {
                    var ft = f.FieldType;

                    if (ft.FullName != null && ft.FullName.Contains("Collider") && ft.IsArray == value.GetType().IsArray)
                    { f.SetValue(obj, value); return true; }

                    if (ft.FullName != null && ft.FullName.Contains("Il2CppReferenceArray") &&
                        value.GetType().FullName != null && value.GetType().FullName.Contains("Il2CppReferenceArray"))
                    { f.SetValue(obj, value); return true; }
                }
                catch { }
            }
            return false;
        }

        static void EnsureTargetVisualsOn(GameObject target)
        {
            if (!target) return;
            target.SetActive(true);

            var rends = target.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (!r) continue;
                try
                {
                    r.enabled = true;
#if UNITY_2019_4_OR_NEWER
                    try { r.forceRenderingOff = false; } catch { }
#endif
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows = true;
                }
                catch { }
            }
        }

        public static void TriggerDeath(string deathMessage)
        {
            MelonCoroutines.Start(Co_TriggerDeath(deathMessage));
        }

        static System.Collections.IEnumerator Co_TriggerDeath(string deathMessage)
        {
            HellenKeller(true);
            yield return new WaitForSeconds(1);
            yield return SceneManager.LoadSceneAsync("DeathRoom", LoadSceneMode.Single);
            HellenKeller(false);
            GameObject button = GameObject.Find("P_WinRoom_INT_DebriefCaseButton_01");
            GameObject deathTextObj = GameObject.Find("CauseOfDeath Text");
            GameObject sceneLoader = GameObject.Find("Scene Loader");
            UnityEngine.Object.Destroy(sceneLoader);
            TMPro.TextMeshPro deathText = deathTextObj.GetComponent<TextMeshPro>();
            deathText.text = deathMessage;
            PhoenixButtonHook hook = button.AddComponent<PhoenixButtonHook>();
            while (!PhoenixButtonHook.restarting)
            {
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
            HellenKeller(true);
            yield return SceneManager.LoadSceneAsync("Van", LoadSceneMode.Single);
            HellenKeller(true);
            yield return new WaitForSeconds(0.5f);
            HellenKeller(true);
            MyMod._RequestLoad();
        }

        static void BreakAllJoints(GameObject go)
        {
            if (!go) return;

            var j0 = go.GetComponents<Joint>();
            for (int i = 0; i < j0.Length; i++) { try { UnityEngine.Object.Destroy(j0[i]); } catch { } }

            var j1 = go.GetComponentsInChildren<Joint>(true);
            for (int i = 0; i < j1.Length; i++) { try { UnityEngine.Object.Destroy(j1[i]); } catch { } }
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

        static Type FindTypeBySuffix(string suffix)
        {
            if (string.IsNullOrEmpty(suffix)) return null;

            string[] guesses =
            {
                "SG.Phoenix.Assets.Code" + suffix,
                "Phoenix.Assets.Code" + suffix,
                suffix.TrimStart('.')
            };

            for (int i = 0; i < guesses.Length; i++)
            {
                try
                {
                    var exact = Type.GetType(guesses[i], false);
                    if (exact != null) return exact;
                }
                catch { }
            }

            try
            {
                var asms = AppDomain.CurrentDomain.GetAssemblies();
                for (int a = 0; a < asms.Length; a++)
                {
                    var asm = asms[a];
                    Type[] types;

                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                    catch { continue; }

                    if (types == null) continue;

                    for (int t = 0; t < types.Length; t++)
                    {
                        var ty = types[t];
                        if (ty == null) continue;

                        var fn = ty.FullName ?? ty.Name;
                        if (fn != null && fn.EndsWith(suffix, StringComparison.Ordinal))
                            return ty;
                    }
                }
            }
            catch { }

            return null;
        }

        static Il2CppSystem.Type ToIl2(Type t)
        {
            if (t == null) return null;
            try { return Il2CppType.From(t); }
            catch { return null; }
        }

        [HideFromIl2Cpp]
        public static void ConvertToPhoenix(GameObject root)
        {
            var shOpaque = Shader.Find("Phoenix/SH_Shared_DefaultPBR_Metallic_Opaque_01");
            if (!shOpaque) shOpaque = Shader.Find("Phoenix/Packed/SH_Shared_PackedPBR_Metallic_Opaque_01");
            var shTrans = Shader.Find("Phoenix/SH_Shared_DefaultPBR_Metallic_TransparentColorAlpha_01");

            var rends = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                var mats = r.sharedMaterials;

                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (mat == null) continue;

                    var shName = mat.shader ? mat.shader.name : "";
                    if (shName.StartsWith("Phoenix/")) continue;

                    bool transparent = (mat.renderQueue >= 3000) || mat.IsKeywordEnabled("_ALPHABLEND_ON");

                    var target = transparent ? shTrans : shOpaque;
                    if (!target) continue;

                    mat.shader = target;
                }

                r.sharedMaterials = mats;
            }
        }

        static bool _rotDonorSearched;
        static RotationalMotion _donorRotMotion;

        public static string[] PreferredRotationalDonorNameContains = { "bannerpulleylever", "pulley", "lever" };

        [HideFromIl2Cpp]
        public static RotationalMotion MakeRotationalMotion(
            GameObject target,
            Vector3 localRotationAxis,
            float startRotationDeg,
            float endRotationDeg,
            bool preferDonorHost = true,

            string donorHandleCenterChildName = "Pulley Handle Center",
            Vector3? handleCenterLocalPos = null,
            Vector3? handleCenterLocalEuler = null,

            bool locked = false,
            bool utilizeLocking = false,
            float lockedMinValue = 0f,
            float lockedMaxValue = 0.02f,
            float lockedGiveRange = 0.01f
        )
        {
            if (!target)
            {
                MelonLogger.Warning("[LevelUtil] MakeRotationalMotion called with null target.");
                return null;
            }

            if (_hmd == null)
                _hmd = FindHMDCamera();

            if (preferDonorHost)
            {
                var donor = EnsureDonorRotationalMotion();
                if (donor != null)
                {
                    var rm = TryCloneRotationalDonorAsHost(
                        donor,
                        target,
                        localRotationAxis,
                        startRotationDeg,
                        endRotationDeg,
                        donorHandleCenterChildName,
                        handleCenterLocalPos,
                        handleCenterLocalEuler,
                        locked,
                        utilizeLocking,
                        lockedMinValue,
                        lockedMaxValue,
                        lockedGiveRange
                    );

                    if (rm != null)
                        return rm;
                }
            }

            try
            {
                var rm = target.GetComponent<RotationalMotion>();
                if (!rm)
                    rm = target.AddComponent<RotationalMotion>();

                rm._RotationAxis = localRotationAxis;
                rm.StartRotation = startRotationDeg;
                rm.EndRotation = endRotationDeg;
                rm.Locked = locked;
                rm._UtilizeLocking = utilizeLocking;
                rm._lockedMinValue = lockedMinValue;
                rm._lockedMaxValue = lockedMaxValue;
                rm._LockedGiveRange = lockedGiveRange;

                var cols = target.GetComponentsInChildren<Collider>(true);
                if (cols != null && cols.Length > 0)
                    BindCollidersToInteractable(rm, cols);

                return rm;
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[LevelUtil] MakeRotationalMotion fallback failed: " + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }

        static RotationalMotion EnsureDonorRotationalMotion()
        {
            if (_donorRotMotion != null)
                return _donorRotMotion;

            if (_rotDonorSearched)
                return null;

            _rotDonorSearched = true;

            RotationalMotion best = null;
            var allGos = Resources.FindObjectsOfTypeAll<GameObject>();

            for (int i = 0; i < allGos.Length; i++)
            {
                var g = allGos[i];
                if (!g || !g.scene.IsValid())
                    continue;

                var rm = g.GetComponent<RotationalMotion>();
                if (!rm)
                    continue;

                var nm = (g.name ?? "").ToLowerInvariant();

                if (PreferredRotationalDonorNameContains != null)
                {
                    for (int k = 0; k < PreferredRotationalDonorNameContains.Length; k++)
                    {
                        var tok = PreferredRotationalDonorNameContains[k];
                        if (!string.IsNullOrEmpty(tok) && nm.Contains(tok))
                        {
                            _donorRotMotion = rm;
                            MelonLogger.Msg("[LevelUtil] Donor RotationalMotion found (preferred): " + g.name);
                            return _donorRotMotion;
                        }
                    }
                }

                if (best == null || g.GetComponent<Rigidbody>() != null)
                    best = rm;
            }

            _donorRotMotion = best;

            if (_donorRotMotion != null)
                MelonLogger.Msg("[LevelUtil] Donor RotationalMotion found: " + _donorRotMotion.gameObject.name);
            else
                MelonLogger.Warning("[LevelUtil] No donor RotationalMotion found in loaded scenes.");

            return _donorRotMotion;
        }

        static RotationalMotion TryCloneRotationalDonorAsHost(
            RotationalMotion donor,
            GameObject target,
            Vector3 localRotationAxis,
            float startRotationDeg,
            float endRotationDeg,
            string donorHandleCenterChildName,
            Vector3? handleCenterLocalPos,
            Vector3? handleCenterLocalEuler,
            bool locked,
            bool utilizeLocking,
            float lockedMinValue,
            float lockedMaxValue,
            float lockedGiveRange
        )
        {
            try
            {
                var donorGO = donor.gameObject;
                if (!donorGO || !target)
                    return null;

                var host = UnityEngine.Object.Instantiate(donorGO);
                host.SetActive(true);
                host.name = "Rot_HOST_" + target.name;

                if (_mergedRoot != null)
                    host.transform.SetParent(_mergedRoot.transform, true);

                host.transform.position = target.transform.position;
                host.transform.rotation = target.transform.rotation;
                host.transform.localScale = target.transform.lossyScale;

                var donorRends = host.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < donorRends.Length; i++)
                    if (donorRends[i])
                        donorRends[i].enabled = false;

                target.transform.SetParent(host.transform, true);

                var targetRbs = target.GetComponentsInChildren<Rigidbody>(true);
                for (int i = 0; i < targetRbs.Length; i++)
                    if (targetRbs[i])
                        UnityEngine.Object.Destroy(targetRbs[i]);

                StripRotHostButKeepMotion(host, target, donorHandleCenterChildName);

                var rm = host.GetComponent<RotationalMotion>();
                if (!rm)
                {
                    MelonLogger.Warning("[LevelUtil] Rot host missing RotationalMotion after clone?");
                    return null;
                }

                rm._RotationAxis = localRotationAxis;
                rm.StartRotation = startRotationDeg;
                rm.EndRotation = endRotationDeg;
                rm.Locked = locked;
                rm._UtilizeLocking = utilizeLocking;
                rm._lockedMinValue = lockedMinValue;
                rm._lockedMaxValue = lockedMaxValue;
                rm._LockedGiveRange = lockedGiveRange;

                Transform handle = null;

                if (!string.IsNullOrEmpty(donorHandleCenterChildName))
                    handle = host.transform.Find(donorHandleCenterChildName);

                if (handle != null)
                {
                    if (handleCenterLocalPos.HasValue)
                        handle.localPosition = handleCenterLocalPos.Value;

                    if (handleCenterLocalEuler.HasValue)
                        handle.localRotation = Quaternion.Euler(handleCenterLocalEuler.Value);

                    try
                    {
                        rm.TelekinesisBeamAttachTransform = handle;
                    }
                    catch { }
                }

                var cols = target.GetComponentsInChildren<Collider>(true);
                if (cols != null && cols.Length > 0)
                    BindCollidersToInteractable(rm, cols);

                EnsureTargetVisualsOn(target);

                int interact = LayerMask.NameToLayer("Interactable");
                if (interact < 0)
                    interact = 8;

                SetLayerRecursive(target, interact);
                EnsureLayerVisibleToCamera(target, _hmd);

                TryEnableBehaviour(rm);

                return rm;
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[LevelUtil] Rot host failed: " + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }

        static void StripRotHostButKeepMotion(GameObject host, GameObject target, string keepChildName)
        {
            if (!host)
                return;

            var rb = host.GetComponent<Rigidbody>();
            var rm = host.GetComponent<RotationalMotion>();

            var cols = host.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (!c) continue;

                if (target != null && (c.transform == target.transform || c.transform.IsChildOf(target.transform)))
                    continue;

                try { UnityEngine.Object.DestroyImmediate(c); } catch { }
            }

            var rends = host.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (!r) continue;

                if (target != null && (r.transform == target.transform || r.transform.IsChildOf(target.transform)))
                    continue;

                try { UnityEngine.Object.DestroyImmediate(r); } catch { }
            }

            var aud = host.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < aud.Length; i++)
            {
                var a = aud[i];
                if (!a) continue;

                if (target != null && (a.transform == target.transform || a.transform.IsChildOf(target.transform)))
                    continue;

                try { UnityEngine.Object.DestroyImmediate(a); } catch { }
            }

            var comps = host.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i];
                if (!c) continue;

                if (c is Transform) continue;
                if (rb != null && c == rb) continue;
                if (rm != null && c == rm) continue;

                var tn = c.GetType().Name ?? "";
                if (tn.Contains("RotationalVelocityTracker")) continue;

                try { UnityEngine.Object.DestroyImmediate(c); } catch { }
            }

            var tr = host.transform;
            var kill = new List<GameObject>();

            for (int i = 0; i < tr.childCount; i++)
            {
                var child = tr.GetChild(i);
                if (!child) continue;

                if (target != null && (child == target.transform || child.IsChildOf(target.transform)))
                    continue;

                if (!string.IsNullOrEmpty(keepChildName) && child.name == keepChildName)
                    continue;

                kill.Add(child.gameObject);
            }

            for (int i = 0; i < kill.Count; i++)
                try { UnityEngine.Object.DestroyImmediate(kill[i]); } catch { }

            if (host.GetComponent<Rigidbody>() == null)
                host.AddComponent<Rigidbody>();

            if (!host.GetComponent<RotationalMotion>())
                host.AddComponent<RotationalMotion>();
        }

        [HideFromIl2Cpp]
        public static void IgnoreCollisionRecursive(GameObject a, GameObject b)
        {
            if (!a || !b) return;

            var colsA = a.GetComponentsInChildren<Collider>(true);
            var colsB = b.GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < colsA.Length; i++)
            {
                var ca = colsA[i];
                if (!ca) continue;

                for (int j = 0; j < colsB.Length; j++)
                {
                    var cb = colsB[j];
                    if (!cb) continue;

                    Physics.IgnoreCollision(ca, cb, true);
                }
            }
        }

        [HideFromIl2Cpp]
        public static void IgnoreCollisionRecursiveFirstOnly(GameObject a, GameObject b)
        {
            if (!a || !b) return;

            Collider[] aCols = a.GetComponentsInChildren<Collider>(true);

            Collider[] bCols = b.GetComponents<Collider>();

            for (int i = 0; i < aCols.Length; i++)
            {
                var ca = aCols[i];
                if (!ca) continue;

                for (int j = 0; j < bCols.Length; j++)
                {
                    var cb = bCols[j];
                    if (!cb) continue;

                    Physics.IgnoreCollision(ca, cb, true);
                }
            }
        }

        [HideFromIl2Cpp]
        public static void MakeMetallic(GameObject root, float metallic)
        {
            if (!root)
            {
                MelonLogger.Warning("[LevelUtil] MakeMetallic called with null root.");
                return;
            }

            metallic = Mathf.Clamp01(metallic);

            Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0)
            {
                MelonLogger.Warning("[LevelUtil] MakeMetallic found no renderers under: " + root.name);
                return;
            }

            int changed = 0;

            for (int i = 0; i < rends.Length; i++)
            {
                Renderer r = rends[i];
                if (!r) continue;

                Material[] mats = r.sharedMaterials;
                if (mats == null) continue;

                bool rendererChanged = false;

                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    if (!mat) continue;

                    if (SetMaterialMetallic(mat, metallic))
                    {
                        changed++;
                        rendererChanged = true;
                    }
                }

                if (rendererChanged)
                    r.sharedMaterials = mats;
            }

            MelonLogger.Msg("[LevelUtil] MakeMetallic SHARED set metallic=" + metallic.ToString("0.###") +
                            " on " + changed + " shared material slot(s) under " + root.name + ".");
        }

        [HideFromIl2Cpp]
        static bool SetMaterialMetallic(Material mat, float metallic)
        {
            if (!mat) return false;

            bool changed = false;

            string[] metallicFloatProps =
            {
        "_Metallic",
        "_MetallicAmount",
        "_MetallicStrength",
        "_Metalness",
        "_MetallicLevel",
        "_MetallicValue"
    };

            for (int i = 0; i < metallicFloatProps.Length; i++)
            {
                string prop = metallicFloatProps[i];

                try
                {
                    if (mat.HasProperty(prop))
                    {
                        mat.SetFloat(prop, metallic);
                        changed = true;
                        MelonLogger.Msg("[LevelUtil] MakeMetallic set float " + prop + "=" + metallic.ToString("0.###") +
                                        " on material " + mat.name);
                    }
                }
                catch { }
            }

            float smooth = metallic > 0.001f ? 0.85f : 0.25f;

            string[] smoothFloatProps =
            {
        "_Smoothness",
        "_Glossiness",
        "_Gloss",
        "_SmoothnessAmount",
        "_GlossinessAmount"
    };

            for (int i = 0; i < smoothFloatProps.Length; i++)
            {
                string prop = smoothFloatProps[i];

                try
                {
                    if (mat.HasProperty(prop))
                    {
                        mat.SetFloat(prop, smooth);
                        changed = true;
                        MelonLogger.Msg("[LevelUtil] MakeMetallic set smooth/gloss " + prop + "=" + smooth.ToString("0.###") +
                                        " on material " + mat.name);
                    }
                }
                catch { }
            }

            if (metallic <= 0.001f)
            {
                string[] packedTextureProps =
                {
            "_MSE",
            "_MSEMap",
            "_MSETex",
            "_MSETexture",
            "_MetallicSmoothnessEmissionMap",
            "_MetallicSmoothnessEmissionTex",
            "_PackedMSEMap",
            "_PackedMSETex",
            "_PackedMap",
            "_PackedTex",
            "_PackedPBRMap",
            "_PackedPBRTex",
            "_MaskMap",
            "_MaskTex",
            "_MetallicGlossMap",
            "_MetallicMap",
            "_MetallicTex",
            "_MetalnessMap"
        };

                for (int i = 0; i < packedTextureProps.Length; i++)
                {
                    string prop = packedTextureProps[i];

                    try
                    {
                        if (mat.HasProperty(prop))
                        {
                            Texture oldTex = mat.GetTexture(prop);
                            if (oldTex != null)
                            {
                                mat.SetTexture(prop, null);
                                changed = true;
                                MelonLogger.Msg("[LevelUtil] MakeMetallic cleared packed/MSE texture " + prop +
                                                " on material " + mat.name);
                            }
                        }
                    }
                    catch { }
                }
            }

            try
            {
                if (metallic > 0.001f)
                {
                    mat.EnableKeyword("_METALLICGLOSSMAP");
                    mat.EnableKeyword("_SPECGLOSSMAP");
                }
                else
                {
                    mat.DisableKeyword("_METALLICGLOSSMAP");
                    mat.DisableKeyword("_SPECGLOSSMAP");
                }
            }
            catch { }

            if (!changed)
            {
                string shaderName = mat.shader ? mat.shader.name : "null";
                MelonLogger.Warning("[LevelUtil] MakeMetallic skipped material '" + mat.name +
                                    "' because no known metallic/smoothness/MSE property matched. Shader=" + shaderName);
            }

            return changed;
        }

        [HideFromIl2Cpp]
        public static void ReplaceMSE(GameObject root, Texture2D mseTexture)
        {
            if (!root)
            {
                MelonLogger.Warning("[LevelUtil] ReplaceMSE called with null root.");
                return;
            }

            if (!mseTexture)
            {
                MelonLogger.Warning("[LevelUtil] ReplaceMSE called with null mseTexture.");
                return;
            }

            Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0)
            {
                MelonLogger.Warning("[LevelUtil] ReplaceMSE found no renderers under: " + root.name);
                return;
            }

            string[] mseProps =
            {
        "_MSE",
        "_MSEMap",
        "_MSETex",
        "_MSETexture",
        "_MetallicSmoothnessEmissionMap",
        "_MetallicSmoothnessEmissionTex",
        "_PackedMSEMap",
        "_PackedMSETex",
        "_PackedMap",
        "_PackedTex",
        "_PackedPBRMap",
        "_PackedPBRTex",
        "_MaskMap",
        "_MaskTex",
        "_MetallicGlossMap"
    };

            int changed = 0;

            for (int i = 0; i < rends.Length; i++)
            {
                Renderer r = rends[i];
                if (!r) continue;

                Material[] mats = r.sharedMaterials;
                if (mats == null) continue;

                bool rendererChanged = false;

                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    if (!mat) continue;

                    for (int p = 0; p < mseProps.Length; p++)
                    {
                        string prop = mseProps[p];

                        try
                        {
                            if (!mat.HasProperty(prop)) continue;

                            mat.SetTexture(prop, mseTexture);
                            changed++;
                            rendererChanged = true;

                            MelonLogger.Msg("[LevelUtil] ReplaceMSE set " + prop +
                                            " on material " + mat.name +
                                            " under " + root.name);

                            break;
                        }
                        catch { }
                    }

                    try
                    {
                        mat.EnableKeyword("_METALLICGLOSSMAP");
                        mat.EnableKeyword("_SPECGLOSSMAP");
                    }
                    catch { }
                }

                if (rendererChanged)
                    r.sharedMaterials = mats;
            }

            if (changed == 0)
            {
                MelonLogger.Warning("[LevelUtil] ReplaceMSE found renderers, but no known MSE texture property matched under: " + root.name);
            }
            else
            {
                MelonLogger.Msg("[LevelUtil] ReplaceMSE replaced MSE texture on " +
                                changed + " shared material slot(s) under " + root.name + ".");
            }
        }

        [HideFromIl2Cpp]
        public static void ReplaceNormal(GameObject root, Texture2D normalTexture)
        {
            if (!root)
            {
                MelonLogger.Warning("[LevelUtil] ReplaceNormal called with null root.");
                return;
            }

            if (!normalTexture)
            {
                MelonLogger.Warning("[LevelUtil] ReplaceNormal called with null normalTexture.");
                return;
            }

            Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0)
            {
                MelonLogger.Warning("[LevelUtil] ReplaceNormal found no renderers under: " + root.name);
                return;
            }

            string[] normalProps =
            {
        "_BumpMap",
        "_NormalMap",
        "_NormalTex",
        "_NormalTexture",
        "_BumpTex",
        "_BumpTexture",
        "_Normal",
        "_PackedNormalMap",
        "_PackedNormalTex"
    };

            int changed = 0;

            for (int i = 0; i < rends.Length; i++)
            {
                Renderer r = rends[i];
                if (!r) continue;

                Material[] mats = r.sharedMaterials;
                if (mats == null) continue;

                bool rendererChanged = false;

                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    if (!mat) continue;

                    for (int p = 0; p < normalProps.Length; p++)
                    {
                        string prop = normalProps[p];

                        try
                        {
                            if (!mat.HasProperty(prop)) continue;

                            mat.SetTexture(prop, normalTexture);
                            changed++;
                            rendererChanged = true;

                            MelonLogger.Msg("[LevelUtil] ReplaceNormal set " + prop +
                                            " on material " + mat.name +
                                            " under " + root.name);

                            break;
                        }
                        catch { }
                    }

                    try
                    {
                        mat.EnableKeyword("_NORMALMAP");
                        mat.EnableKeyword("_BUMP_MAP");
                    }
                    catch { }

                    try
                    {
                        if (mat.HasProperty("_BumpScale"))
                            mat.SetFloat("_BumpScale", 1f);

                        if (mat.HasProperty("_NormalScale"))
                            mat.SetFloat("_NormalScale", 1f);
                    }
                    catch { }
                }

                if (rendererChanged)
                    r.sharedMaterials = mats;
            }

            if (changed == 0)
            {
                MelonLogger.Warning("[LevelUtil] ReplaceNormal found renderers, but no known normal texture property matched under: " + root.name);
            }
            else
            {
                MelonLogger.Msg("[LevelUtil] ReplaceNormal replaced normal texture on " +
                                changed + " shared material slot(s) under " + root.name + ".");
            }
        }

        public static List<Transform> GetAllChildrenRecursive(Transform root)
        {
            List<Transform> result = new List<Transform>();
            if (root == null) return result;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                result.Add(child);
                AddChildrenRecursive(child, result);
            }

            return result;
        }

        private static void AddChildrenRecursive(Transform parent, List<Transform> result)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                result.Add(child);
                AddChildrenRecursive(child, result);
            }
        }
    }
}
