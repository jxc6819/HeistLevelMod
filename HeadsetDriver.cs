
using System;
using System.Collections;
using MelonLoader;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{





    public class HeadsetDriver : MonoBehaviour
    {
        public HeadsetDriver(IntPtr p) : base(p) { }
        public HeadsetDriver() : base(ClassInjector.DerivedConstructorPointer<HeadsetDriver>())
            => ClassInjector.DerivedConstructorBody(this);



        public bool StartEnabled = false;

        public float GrainStrength = 0.12f;
        public float LineStrength = 0.10f;

        public float NoiseUpdateHz = 12f;
        public float GrainScrollSpeed = 0.05f;
        public float LineScrollSpeed = 0.015f;

        public float JitterAmount = 0.0010f;
        public float Overscan = 1.35f;
        public float Darken = 0.05f;



        public float ShotDuration = 1.30f;
        public float ImpactFxAlpha = 1.25f;
        public float GlitchBarAlpha = 0.90f;
        public float TemporaryJitterBoost = 0.0080f;
        public float TemporaryExtraGrain = 0.52f;
        public float TemporaryExtraLines = 0.26f;

        public float PersistentCrackAlpha = 0.95f;
        public float PersistentBurnAlpha = 0.88f;
        public float PersistentPixelDamageAlpha = 0.80f;

        public int ImpactTextureSize = 384;
        public int MainCrackLengthPx = 120;
        public int EdgeCrackLengthPx = 180;
        public int StressCrackCount = 8;
        public int SparkCount = 34;
        public int ArcCount = 10;
        public int PersistentMiniSparkCount = 22;
        public float PersistentMiniSparkAlpha = 0.85f;

        public bool LogRenderSetup = false;



        Camera _cam;

        GameObject _root;

        MeshRenderer _grainR;
        MeshRenderer _linesR;
        MeshRenderer _burnR;
        MeshRenderer _pixelR;
        MeshRenderer _crackR;
        MeshRenderer _fxR;
        MeshRenderer _miniSparkR;

        Material _grainMat;
        Material _linesMat;
        Material _burnMat;
        Material _pixelMat;
        Material _crackMat;
        Material _fxMat;
        Material _miniSparkMat;

        Texture2D _noiseTex;
        Texture2D _linesTex;
        Texture2D _crackTex;
        Texture2D _burnTex;
        Texture2D _pixelTex;
        Texture2D _fxTex;
        Texture2D _miniSparkTex;

        Color32[] _crackPx;
        Color32[] _burnPx;
        Color32[] _pixelPx;
        Color32[] _fxPx, _miniSparkPx;

        float _noiseTimer;
        Vector2 _grainUV;
        Vector2 _linesUV;

        bool _overlayOn;

        float _baseGrainStrength;
        float _baseLineStrength;
        float _baseJitterAmount;

        float _crackAlpha;
        float _burnAlpha;
        float _pixelAlpha;

        int _shotIndex;
        object _shotRoutine;



        public void Enable() => SetEnabled(true);
        public void Disable() => SetEnabled(false);

        public void SetEnabled(bool on)
        {
            _overlayOn = on;
            EnsureBuilt();

            if (_root) _root.SetActive(on);

            if (on)
            {
                _noiseTimer = 0f;
                _grainUV = Vector2.zero;
                _linesUV = Vector2.zero;

                _baseGrainStrength = GrainStrength;
                _baseLineStrength = LineStrength;
                _baseJitterAmount = JitterAmount;

                ApplyStrengths();
                ApplyPersistentDamageAlpha();
                ClearTransientFx();
                RebuildNoise(true);
                FitToCamera();
            }
        }

        public void Punch(float seconds = 0.10f, float extraStrength = 0.25f)
        {
            if (!_overlayOn)
                SetEnabled(true);

            MelonCoroutines.Start(Co_Punch(seconds, extraStrength));
        }

        public void DroneShot()
        {
            if (!_overlayOn)
                SetEnabled(true);

            EnsureBuilt();
            if (!_root || !_fxTex) return;

            Vector2 impactUv = PickImpactUV();
            int w = _fxTex.width;
            int h = _fxTex.height;
            int cx = Mathf.RoundToInt(impactUv.x * (w - 1));
            int cy = Mathf.RoundToInt(impactUv.y * (h - 1));

            StampMajorImpact(_crackPx, _burnPx, _pixelPx, w, h, cx, cy);
            StampPeripheralDamage(_crackPx, _burnPx, _pixelPx, w, h, cx, cy);
            ApplyPersistentTextures();

            _crackAlpha = PersistentCrackAlpha;
            _burnAlpha = PersistentBurnAlpha;
            _pixelAlpha = PersistentPixelDamageAlpha;
            ApplyPersistentDamageAlpha();

            BuildTransientShotFx(_fxPx, w, h, cx, cy);
            ApplyFxTexture();

            BuildPersistentMiniSparks(_miniSparkPx, w, h, cx, cy);
            ApplyMiniSparkTexture();
            ApplyMiniSparkAlpha(PersistentMiniSparkAlpha);

            if (_shotRoutine != null)
                MelonCoroutines.Stop(_shotRoutine);
            _shotRoutine = MelonCoroutines.Start(Co_DroneShot());

            _shotIndex++;
        }

        public void ClearShotDamage()
        {
            EnsureBuilt();
            if (_crackPx == null) return;

            ClearArray(_crackPx);
            ClearArray(_burnPx);
            ClearArray(_pixelPx);
            ClearArray(_fxPx);
            ClearArray(_miniSparkPx);

            _crackAlpha = 0f;
            _burnAlpha = 0f;
            _pixelAlpha = 0f;

            ApplyPersistentTextures();
            ApplyFxTexture();
            ApplyMiniSparkTexture();
            ApplyPersistentDamageAlpha();
            ApplyMiniSparkAlpha(0f);
        }

        IEnumerator Co_Punch(float seconds, float extraStrength)
        {
            float g0 = _baseGrainStrength;
            float l0 = _baseLineStrength;

            float end = Time.time + Mathf.Max(0.01f, seconds);
            while (Time.time < end)
            {
                float k = 1f - Mathf.InverseLerp(end - seconds, end, Time.time);
                float g = Mathf.Clamp01(g0 + extraStrength * k);
                float l = Mathf.Clamp01(l0 + (extraStrength * 0.6f) * k);

                SetStrengths(g, l);
                yield return null;
            }

            SetStrengths(g0, l0);
        }

        IEnumerator Co_DroneShot()
        {
            float g0 = _baseGrainStrength;
            float l0 = _baseLineStrength;
            float j0 = _baseJitterAmount;

            float duration = Mathf.Max(0.05f, ShotDuration);
            float end = Time.time + duration;

            while (Time.time < end)
            {
                float k = 1f - Mathf.InverseLerp(end - duration, end, Time.time);
                k = Mathf.Clamp01(k);

                SetStrengths(
                    Mathf.Clamp01(g0 + TemporaryExtraGrain * k),
                    Mathf.Clamp01(l0 + TemporaryExtraLines * k)
                );

                JitterAmount = j0 + (TemporaryJitterBoost * k);

                FadeTransientFx(k);
                RebuildNoise(false);

                yield return null;
            }

            SetStrengths(g0, l0);
            JitterAmount = j0;
            ClearTransientFx();
            _shotRoutine = null;
        }



        void Awake()
        {
            _cam = FindHMDCamera();
            if (!_cam) _cam = Camera.main;

            _baseGrainStrength = GrainStrength;
            _baseLineStrength = LineStrength;
            _baseJitterAmount = JitterAmount;

            if (_cam)
            {
                BuildOverlay();
                SetEnabled(StartEnabled);
            }
        }

        void Start()
        {
            if (!_cam) _cam = FindHMDCamera();
            if (!_cam) _cam = Camera.main;

            if (_cam && !_root)
            {
                BuildOverlay();
                SetEnabled(StartEnabled);
            }
        }

        void OnDestroy()
        {
            try { if (_root) Destroy(_root); } catch { }

            try { if (_grainMat) Destroy(_grainMat); } catch { }
            try { if (_linesMat) Destroy(_linesMat); } catch { }
            try { if (_burnMat) Destroy(_burnMat); } catch { }
            try { if (_pixelMat) Destroy(_pixelMat); } catch { }
            try { if (_crackMat) Destroy(_crackMat); } catch { }
            try { if (_fxMat) Destroy(_fxMat); } catch { }
            try { if (_miniSparkMat) Destroy(_miniSparkMat); } catch { }

            try { if (_noiseTex) Destroy(_noiseTex); } catch { }
            try { if (_linesTex) Destroy(_linesTex); } catch { }
            try { if (_crackTex) Destroy(_crackTex); } catch { }
            try { if (_burnTex) Destroy(_burnTex); } catch { }
            try { if (_pixelTex) Destroy(_pixelTex); } catch { }
            try { if (_fxTex) Destroy(_fxTex); } catch { }
            try { if (_miniSparkTex) Destroy(_miniSparkTex); } catch { }
        }

        void Update()
        {
            if (!_overlayOn || !_cam || !_root) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            FitToCamera();

            if (JitterAmount > 0f)
            {
                float jx = (Mathf.PerlinNoise(Time.time * 17.3f, 0.1f) - 0.5f) * 2f;
                float jy = (Mathf.PerlinNoise(0.2f, Time.time * 19.1f) - 0.5f) * 2f;

                Vector3 lp = _root.transform.localPosition;
                _root.transform.localPosition = new Vector3(jx * JitterAmount, jy * JitterAmount, lp.z);
            }
            else
            {
                Vector3 lp = _root.transform.localPosition;
                _root.transform.localPosition = new Vector3(0f, 0f, lp.z);
            }

            _grainUV += new Vector2(GrainScrollSpeed * dt, (GrainScrollSpeed * 0.65f) * dt);
            _linesUV += new Vector2(0f, LineScrollSpeed * dt);

            if (_grainMat && _grainMat.HasProperty("_MainTex"))
                _grainMat.mainTextureOffset = _grainUV;

            if (_linesMat && _linesMat.HasProperty("_MainTex"))
                _linesMat.mainTextureOffset = _linesUV;

            if (_miniSparkMat && _miniSparkPx != null && _miniSparkTex != null)
            {
                AnimateMiniSparks();
            }

            float hz = Mathf.Max(1f, NoiseUpdateHz);
            _noiseTimer += dt;
            if (_noiseTimer >= (1f / hz))
            {
                _noiseTimer = 0f;
                RebuildNoise(false);
            }
        }



        void EnsureBuilt()
        {
            if (_root) return;

            if (!_cam) _cam = FindHMDCamera();
            if (!_cam) _cam = Camera.main;
            if (!_cam) return;

            BuildOverlay();
        }

        void BuildOverlay()
        {
            _root = new GameObject("HeadsetVhsOverlay");
            _root.transform.SetParent(_cam.transform, false);
            _root.transform.localRotation = Quaternion.identity;
            _root.transform.localScale = Vector3.one;

            float z = Mathf.Max(0.03f, _cam.nearClipPlane + 0.03f);
            _root.transform.localPosition = new Vector3(0f, 0f, z);

            Shader alphaSh = Shader.Find("FX/FX_Alpha_UVPan_Shader");
            Shader unlitSh = Shader.Find("Unlit/Transparent");
            if (!alphaSh) alphaSh = unlitSh;
            if (!unlitSh) unlitSh = alphaSh;

            _noiseTex = BuildNoiseTex(128, 128);
            _linesTex = BuildScanlineTex(256, 256);

            int texSize = Mathf.Clamp(ImpactTextureSize, 256, 512);

            _crackTex = BuildBlankTex(texSize, texSize, FilterMode.Bilinear);
            _burnTex = BuildBlankTex(texSize, texSize, FilterMode.Bilinear);
            _pixelTex = BuildBlankTex(texSize, texSize, FilterMode.Point);
            _fxTex = BuildBlankTex(texSize, texSize, FilterMode.Bilinear);
            _miniSparkTex = BuildBlankTex(texSize, texSize, FilterMode.Bilinear);

            _crackPx = new Color32[texSize * texSize];
            _burnPx = new Color32[texSize * texSize];
            _pixelPx = new Color32[texSize * texSize];
            _fxPx = new Color32[texSize * texSize];
            _miniSparkPx = new Color32[texSize * texSize];

            _grainMat = new Material(alphaSh);
            _grainMat.name = "HeadsetVhs_GrainMat";
            if (_grainMat.HasProperty("_MainTex")) _grainMat.SetTexture("_MainTex", _noiseTex);
            _grainMat.renderQueue = 4990;
            ForceOverlayRenderState(_grainMat);

            _linesMat = new Material(alphaSh);
            _linesMat.name = "HeadsetVhs_LinesMat";
            if (_linesMat.HasProperty("_MainTex")) _linesMat.SetTexture("_MainTex", _linesTex);
            _linesMat.renderQueue = 4991;
            ForceOverlayRenderState(_linesMat);

            _burnMat = new Material(unlitSh);
            _burnMat.name = "HeadsetVhs_BurnMat";
            if (_burnMat.HasProperty("_MainTex")) _burnMat.SetTexture("_MainTex", _burnTex);
            if (_burnMat.HasProperty("_Color")) _burnMat.SetColor("_Color", new Color(0.16f, 0.12f, 0.08f, 0f));
            _burnMat.renderQueue = 4992;
            ForceOverlayRenderState(_burnMat);

            _pixelMat = new Material(unlitSh);
            _pixelMat.name = "HeadsetVhs_PixelMat";
            if (_pixelMat.HasProperty("_MainTex")) _pixelMat.SetTexture("_MainTex", _pixelTex);
            if (_pixelMat.HasProperty("_Color")) _pixelMat.SetColor("_Color", new Color(0.03f, 0.03f, 0.03f, 0f));
            _pixelMat.renderQueue = 4993;
            ForceOverlayRenderState(_pixelMat);

            _crackMat = new Material(unlitSh);
            _crackMat.name = "HeadsetVhs_CrackMat";
            if (_crackMat.HasProperty("_MainTex")) _crackMat.SetTexture("_MainTex", _crackTex);
            if (_crackMat.HasProperty("_Color")) _crackMat.SetColor("_Color", new Color(1f, 1f, 1f, 0f));
            _crackMat.renderQueue = 4994;
            ForceOverlayRenderState(_crackMat);

            _fxMat = new Material(unlitSh);
            _fxMat.name = "HeadsetVhs_FxMat";
            if (_fxMat.HasProperty("_MainTex")) _fxMat.SetTexture("_MainTex", _fxTex);
            if (_fxMat.HasProperty("_Color")) _fxMat.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            _fxMat.renderQueue = 4995;
            ForceOverlayRenderState(_fxMat);

            _miniSparkMat = new Material(unlitSh);
            _miniSparkMat.name = "HeadsetVhs_MiniSparkMat";
            if (_miniSparkMat.HasProperty("_MainTex")) _miniSparkMat.SetTexture("_MainTex", _miniSparkTex);
            if (_miniSparkMat.HasProperty("_Color")) _miniSparkMat.SetColor("_Color", new Color(1f, 1f, 1f, 0f));
            _miniSparkMat.renderQueue = 4996;
            ForceOverlayRenderState(_miniSparkMat);

            _grainR = MakeQuad("Grain", _grainMat, -0.00030f);
            _linesR = MakeQuad("Lines", _linesMat, -0.00024f);
            _burnR = MakeQuad("Burn", _burnMat, -0.00018f);
            _pixelR = MakeQuad("Pixel", _pixelMat, -0.00012f);
            _crackR = MakeQuad("Crack", _crackMat, -0.00008f);
            _fxR = MakeQuad("Fx", _fxMat, -0.00005f);
            _miniSparkR = MakeQuad("MiniSpark", _miniSparkMat, -0.00004f);

            ApplyPersistentTextures();
            ApplyFxTexture();
            ApplyStrengths();
            ApplyPersistentDamageAlpha();
            FitToCamera();

            if (LogRenderSetup)
                MelonLogger.Msg("[HeadsetDriver] Built overlay on camera '" + _cam.name + "'");
        }

        MeshRenderer MakeQuad(string name, Material mat, float localZ)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Vhs_" + name;
            go.transform.SetParent(_root.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, localZ);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.layer = _cam ? _cam.gameObject.layer : 0;

            Collider col = go.GetComponent<Collider>();
            if (col) Destroy(col);

            MeshRenderer r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return r;
        }

        void ForceOverlayRenderState(Material m)
        {
            if (!m) return;

            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            m.SetInt("_ZWrite", 0);
            m.SetInt("_ZTest", 8);
            m.renderQueue = Mathf.Max(m.renderQueue, 4990);
        }

        void FitToCamera()
        {
            if (!_cam || !_root) return;

            float zWorld = Mathf.Max(0.03f, _cam.nearClipPlane + 0.03f);

            Vector3 s = _cam.transform.lossyScale;
            float invX = (Mathf.Abs(s.x) < 0.0001f) ? 1f : 1f / s.x;
            float invY = (Mathf.Abs(s.y) < 0.0001f) ? 1f : 1f / s.y;
            float invZ = (Mathf.Abs(s.z) < 0.0001f) ? 1f : 1f / s.z;

            _root.transform.localPosition = new Vector3(
                _root.transform.localPosition.x,
                _root.transform.localPosition.y,
                zWorld * invZ
            );

            float deg2rad = 0.0174532924f;
            float hWorld = 2f * zWorld * Mathf.Tan(_cam.fieldOfView * 0.5f * deg2rad);
            float wWorld = hWorld * _cam.aspect;

            float overscan = Mathf.Max(1.0f, Overscan);

            _root.transform.localScale = new Vector3(
                wWorld * overscan * invX,
                hWorld * overscan * invY,
                1f
            );
        }

        void ApplyStrengths()
        {
            SetStrengths(GrainStrength, LineStrength);
        }

        void SetStrengths(float grain, float lines)
        {
            GrainStrength = Mathf.Clamp01(grain);
            LineStrength = Mathf.Clamp01(lines);

            float baseRGB = Mathf.Clamp01(1f - Darken);

            if (_grainMat && _grainMat.HasProperty("_Color"))
                _grainMat.SetColor("_Color", new Color(baseRGB, baseRGB, baseRGB, GrainStrength));

            if (_linesMat && _linesMat.HasProperty("_Color"))
                _linesMat.SetColor("_Color", new Color(baseRGB, baseRGB, baseRGB, LineStrength));
        }

        void ApplyPersistentDamageAlpha()
        {
            if (_crackMat && _crackMat.HasProperty("_Color"))
                _crackMat.SetColor("_Color", new Color(0.92f, 0.94f, 0.98f, _crackAlpha));

            if (_burnMat && _burnMat.HasProperty("_Color"))
                _burnMat.SetColor("_Color", new Color(0.10f, 0.06f, 0.03f, _burnAlpha));

            if (_pixelMat && _pixelMat.HasProperty("_Color"))
                _pixelMat.SetColor("_Color", new Color(0.02f, 0.02f, 0.02f, _pixelAlpha));
        }

        void ApplyMiniSparkAlpha(float alpha)
        {
            if (_miniSparkMat && _miniSparkMat.HasProperty("_Color"))
                _miniSparkMat.SetColor("_Color", new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
        }

        void RebuildNoise(bool force)
        {
            if (!_noiseTex) return;

            int w = _noiseTex.width;
            int h = _noiseTex.height;

            float seed = force ? UnityEngine.Random.value * 9999f : Time.time * 0.65f;

            Color32[] px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float fy = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float fx = x / (float)(w - 1);

                    float p = Mathf.PerlinNoise(fx * 14f + seed, fy * 14f + seed * 0.37f);
                    float r = UnityEngine.Random.value;

                    float g = Mathf.Clamp01(0.80f * p + 0.20f * r);
                    g = Mathf.Lerp(0.35f, 0.85f, g);

                    byte b = (byte)(g * 255f);
                    px[y * w + x] = new Color32(b, b, b, 255);
                }
            }

            _noiseTex.SetPixels32(px);
            _noiseTex.Apply(false, false);
        }



        void StampMajorImpact(Color32[] crackPx, Color32[] burnPx, Color32[] pixelPx, int w, int h, int cx, int cy)
        {

            StampImpactShatter(crackPx, burnPx, w, h, cx, cy, 18);

            for (int i = 0; i < 7; i++)
            {
                float a = (360f / 7f) * i + UnityEngine.Random.Range(-18f, 18f);
                float rad = a * 0.0174532924f;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                int len = MainCrackLengthPx + UnityEngine.Random.Range(-24, 42);
                DrawBranchedCrack(crackPx, w, h, cx, cy, dir, len, 2, 255, 5f, 11f);
            }

            for (int i = 0; i < StressCrackCount + 3; i++)
            {
                float a = UnityEngine.Random.Range(0f, 360f);
                float rad = a * 0.0174532924f;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                int len = MainCrackLengthPx + UnityEngine.Random.Range(50, 130);
                DrawSegmentedFracture(crackPx, w, h, cx, cy, dir, len, 2);
            }

            for (int i = 0; i < 4; i++)
            {
                Vector2 edge = RandomEdgePoint01(i);
                int ex = Mathf.RoundToInt(edge.x * (w - 1));
                int ey = Mathf.RoundToInt(edge.y * (h - 1));
                DrawJaggedLine(crackPx, w, h, ex, ey, cx, cy, 2, 240, 7f, 18f);
            }

            StampSoftBlotch(burnPx, w, h, cx, cy, 40, new Color32(255, 255, 255, 225));
            StampSoftBlotch(burnPx, w, h, cx + UnityEngine.Random.Range(-10, 10), cy + UnityEngine.Random.Range(-10, 10), 64, new Color32(255, 255, 255, 100));
            StampSoftBlotch(burnPx, w, h, cx + UnityEngine.Random.Range(-24, 24), cy + UnityEngine.Random.Range(-24, 24), 20, new Color32(255, 255, 255, 255));

            StampBlackPatch(pixelPx, w, h, cx + UnityEngine.Random.Range(-34, 34), cy + UnityEngine.Random.Range(-34, 34), 34, 20, 240);
            StampBlackPatch(pixelPx, w, h, cx + UnityEngine.Random.Range(-52, 52), cy + UnityEngine.Random.Range(-52, 52), 18, 10, 175);
            StampDeadSegments(pixelPx, w, h, cy + UnityEngine.Random.Range(-60, 60), 4, 150);
        }

        void StampPeripheralDamage(Color32[] crackPx, Color32[] burnPx, Color32[] pixelPx, int w, int h, int cx, int cy)
        {

            for (int i = 0; i < 3; i++)
            {
                Vector2 p = PickPeripheralUv(i);
                int px = Mathf.RoundToInt(p.x * (w - 1));
                int py = Mathf.RoundToInt(p.y * (h - 1));

                float a = UnityEngine.Random.Range(0f, 360f);
                float rad = a * 0.0174532924f;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                DrawSegmentedFracture(crackPx, w, h, px, py, dir, EdgeCrackLengthPx + UnityEngine.Random.Range(-40, 70), 2);
                DrawBranchedCrack(crackPx, w, h, px, py, dir, EdgeCrackLengthPx / 2 + UnityEngine.Random.Range(-30, 30), 1, 210, 4f, 9f);

                StampSoftBlotch(burnPx, w, h, px, py, UnityEngine.Random.Range(22, 36), new Color32(255, 255, 255, 125));
                StampBlackPatch(pixelPx, w, h, px, py, UnityEngine.Random.Range(20, 36), UnityEngine.Random.Range(12, 22), 145);
            }

            for (int i = 0; i < 3; i++)
            {
                Vector2 edge = RandomEdgePoint01(i + 5);
                Vector2 toward = new Vector2((cx / (float)Mathf.Max(1, w - 1)), (cy / (float)Mathf.Max(1, h - 1)));
                int ex = Mathf.RoundToInt(edge.x * (w - 1));
                int ey = Mathf.RoundToInt(edge.y * (h - 1));
                int tx = Mathf.RoundToInt(Mathf.Lerp(edge.x, toward.x, 0.58f) * (w - 1));
                int ty = Mathf.RoundToInt(Mathf.Lerp(edge.y, toward.y, 0.58f) * (h - 1));
                DrawJaggedLine(crackPx, w, h, ex, ey, tx, ty, 2, 220, 8f, 22f);
            }
        }

        void BuildTransientShotFx(Color32[] fxPx, int w, int h, int cx, int cy)
        {
            ClearArray(fxPx);

            StampRadial(fxPx, w, h, cx, cy, 54, new Color32(255, 140, 50, 230));
            StampRadial(fxPx, w, h, cx, cy, 24, new Color32(255, 230, 180, 255));
            StampRadial(fxPx, w, h, cx + UnityEngine.Random.Range(-10, 10), cy + UnityEngine.Random.Range(-10, 10), 34, new Color32(110, 220, 255, 165));

            for (int i = 0; i < SparkCount; i++)
            {
                float a = UnityEngine.Random.Range(0f, 360f);
                float rad = a * 0.0174532924f;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

                int sx = cx + Mathf.RoundToInt(dir.x * UnityEngine.Random.Range(8f, 28f));
                int sy = cy + Mathf.RoundToInt(dir.y * UnityEngine.Random.Range(8f, 28f));
                int len = UnityEngine.Random.Range(18, 42);

                DrawJaggedCrack(fxPx, w, h, sx, sy, dir, len, 3, 255, 2f, 8f, new Color32(255, 155, 40, 255));
                StampRadial(fxPx, w, h, sx, sy, 6, new Color32(255, 220, 120, 220));
            }

            for (int i = 0; i < ArcCount; i++)
            {
                float a = UnityEngine.Random.Range(0f, 360f);
                float rad = a * 0.0174532924f;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

                int x0 = cx + Mathf.RoundToInt(dir.x * UnityEngine.Random.Range(18f, 54f));
                int y0 = cy + Mathf.RoundToInt(dir.y * UnityEngine.Random.Range(18f, 54f));
                int x1 = x0 + UnityEngine.Random.Range(-70, 70);
                int y1 = y0 + UnityEngine.Random.Range(-36, 36);

                DrawJaggedLine(fxPx, w, h, x0, y0, x1, y1, 3, 255, 5f, 13f, new Color32(70, 210, 255, 255));
                StampRadial(fxPx, w, h, x0, y0, 10, new Color32(180, 245, 255, 230));
                StampRadial(fxPx, w, h, x1, y1, 8, new Color32(120, 230, 255, 210));
            }

            for (int i = 0; i < 7; i++)
            {
                int x = cx + UnityEngine.Random.Range(-120, 120);
                int y = cy + UnityEngine.Random.Range(-100, 100);
                int hSeg = UnityEngine.Random.Range(18, 48);
                int wSeg = UnityEngine.Random.Range(4, 10);
                byte a = (byte)UnityEngine.Random.Range(110, 210);
                Color32 c = (i % 2 == 0)
                    ? new Color32(255, 120, 50, a)
                    : new Color32(85, 220, 255, a);

                DrawVerticalBand(fxPx, w, h, x, wSeg, y - hSeg, y + hSeg, c);
            }
        }

        void BuildPersistentMiniSparks(Color32[] px, int w, int h, int cx, int cy)
        {
            if (px == null) return;
            ClearArray(px);

            for (int i = 0; i < PersistentMiniSparkCount; i++)
            {
                float a = UnityEngine.Random.Range(0f, 360f);
                float rad = a * 0.0174532924f;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

                int sx = cx + Mathf.RoundToInt(dir.x * UnityEngine.Random.Range(14f, 82f));
                int sy = cy + Mathf.RoundToInt(dir.y * UnityEngine.Random.Range(14f, 82f));

                if (UnityEngine.Random.value > 0.45f)
                {
                    StampRadial(px, w, h, sx, sy, UnityEngine.Random.Range(3, 5), new Color32(255, 165, 60, 235));
                    int x1 = sx + UnityEngine.Random.Range(-8, 8);
                    int y1 = sy + UnityEngine.Random.Range(-6, 6);
                    DrawJaggedLine(px, w, h, sx, sy, x1, y1, 1, 210, 1f, 3f, new Color32(255, 210, 120, 210));
                }
                else
                {
                    StampRadial(px, w, h, sx, sy, UnityEngine.Random.Range(3, 5), new Color32(110, 220, 255, 220));
                    int x1 = sx + UnityEngine.Random.Range(-10, 10);
                    int y1 = sy + UnityEngine.Random.Range(-8, 8);
                    DrawJaggedLine(px, w, h, sx, sy, x1, y1, 1, 190, 1f, 4f, new Color32(180, 245, 255, 180));
                }
            }
        }

        void AnimateMiniSparks()
        {
            int texSize = _miniSparkTex.width;
            if (_miniSparkPx == null || _miniSparkTex == null) return;

            int litCount = 0;
            for (int i = 0; i < _miniSparkPx.Length; i++)
            {
                Color32 c = _miniSparkPx[i];
                if (c.a == 0) continue;

                float noise = Mathf.PerlinNoise((i % texSize) * 0.11f + Time.time * 12f, (i / texSize) * 0.07f + 1.7f);
                float blink = Mathf.PerlinNoise((i % texSize) * 0.23f + Time.time * 21f, 5.3f);

                if (noise > 0.73f || blink > 0.82f)
                {
                    c.a = (byte)Mathf.Clamp((int)(220 + (35 * Mathf.Sin(Time.time * 24f + (i * 0.01f)))), 160, 255);
                    if (c.r > c.b)
                    {
                        c.r = 255;
                        c.g = 185;
                        c.b = 85;
                    }
                    else
                    {
                        c.r = 135;
                        c.g = 235;
                        c.b = 255;
                    }
                    litCount++;
                }
                else
                {
                    c.a = (byte)Mathf.Clamp((int)(c.a * 0.84f), 18, 145);
                }

                _miniSparkPx[i] = c;
            }

            if (Time.frameCount % 2 == 0)
            {
                int tries = 10;
                for (int t = 0; t < tries; t++)
                {
                    int idx = UnityEngine.Random.Range(0, _miniSparkPx.Length);
                    if (_miniSparkPx[idx].a == 0) continue;
                    int x = idx % texSize;
                    int y = idx / texSize;
                    Color32 col = (UnityEngine.Random.value > 0.5f)
                        ? new Color32(255, 175, 70, 240)
                        : new Color32(120, 225, 255, 225);
                    StampRadial(_miniSparkPx, texSize, texSize, x, y, UnityEngine.Random.Range(2, 4), col);
                    break;
                }
            }

            ApplyMiniSparkTexture();
            ApplyMiniSparkAlpha(PersistentMiniSparkAlpha);
        }

        void FadeTransientFx(float k)
        {
            if (_fxPx == null || _fxTex == null) return;

            float linger = Mathf.Clamp01((k * 0.92f) + 0.22f);
            byte maxAlpha = (byte)Mathf.Clamp(Mathf.RoundToInt(255f * ImpactFxAlpha * linger), 0, 255);
            for (int i = 0; i < _fxPx.Length; i++)
            {
                Color32 c = _fxPx[i];
                if (c.a == 0) continue;
                c.a = (byte)((c.a * maxAlpha) / 255);
                _fxPx[i] = c;
            }

            ApplyFxTexture();
        }

        void ClearTransientFx()
        {
            if (_fxPx == null) return;
            ClearArray(_fxPx);
            ApplyFxTexture();
        }

        void ApplyPersistentTextures()
        {
            ApplyPixelsToTex(_crackTex, _crackPx);
            ApplyPixelsToTex(_burnTex, _burnPx);
            ApplyPixelsToTex(_pixelTex, _pixelPx);
        }

        void ApplyFxTexture()
        {
            ApplyPixelsToTex(_fxTex, _fxPx);
        }

        void ApplyMiniSparkTexture()
        {
            ApplyPixelsToTex(_miniSparkTex, _miniSparkPx);
        }

        void ApplyPixelsToTex(Texture2D tex, Color32[] px)
        {
            if (!tex || px == null) return;
            tex.SetPixels32(px);
            tex.Apply(false, false);
        }

        static void ClearArray(Color32[] px)
        {
            if (px == null) return;
            Color32 clear = new Color32(255, 255, 255, 0);
            for (int i = 0; i < px.Length; i++) px[i] = clear;
        }



        static Texture2D BuildNoiseTex(int w, int h)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 0;

            Color32[] px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                byte v = (byte)UnityEngine.Random.Range(0, 256);
                px[i] = new Color32(v, v, v, 255);
            }

            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        static Texture2D BuildScanlineTex(int w, int h)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Point;
            tex.anisoLevel = 0;

            Color32[] px = new Color32[w * h];

            for (int y = 0; y < h; y++)
            {
                bool darkRow = (y % 3) == 0;
                float baseA = darkRow ? 0.22f : 0.07f;
                if ((y % 37) == 0) baseA = 0.28f;

                for (int x = 0; x < w; x++)
                {
                    float jitter = (Mathf.PerlinNoise(x * 0.05f, y * 0.11f) - 0.5f) * 0.06f;
                    float a = Mathf.Clamp01(baseA + jitter);

                    byte A = (byte)(a * 255f);
                    px[y * w + x] = new Color32(255, 255, 255, A);
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        static Texture2D BuildBlankTex(int w, int h, FilterMode filter)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = filter;
            tex.anisoLevel = 0;

            Color32[] px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++)
                px[i] = new Color32(255, 255, 255, 0);

            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }



        void StampImpactShatter(Color32[] crackPx, Color32[] burnPx, int w, int h, int cx, int cy, int radius)
        {
            int shards = 9;
            for (int i = 0; i < shards; i++)
            {
                float a0 = (360f / shards) * i + UnityEngine.Random.Range(-18f, 18f);
                float a1 = a0 + UnityEngine.Random.Range(18f, 42f);

                Vector2 p0 = new Vector2(cx, cy) + DirFromDeg(a0) * UnityEngine.Random.Range(radius * 0.25f, radius * 0.60f);
                Vector2 p1 = new Vector2(cx, cy) + DirFromDeg(a1) * UnityEngine.Random.Range(radius * 0.35f, radius * 0.90f);
                Vector2 p2 = new Vector2(cx, cy) + DirFromDeg((a0 + a1) * 0.5f) * UnityEngine.Random.Range(radius * 0.55f, radius * 1.20f);

                DrawJaggedLine(crackPx, w, h, Mathf.RoundToInt(p0.x), Mathf.RoundToInt(p0.y), Mathf.RoundToInt(p1.x), Mathf.RoundToInt(p1.y), 1, 220, 1f, 3f);
                DrawJaggedLine(crackPx, w, h, Mathf.RoundToInt(p1.x), Mathf.RoundToInt(p1.y), Mathf.RoundToInt(p2.x), Mathf.RoundToInt(p2.y), 1, 220, 1f, 3f);
            }

            StampSoftBlotch(burnPx, w, h, cx, cy, radius + 6, new Color32(255, 255, 255, 90));
        }

        void DrawSegmentedFracture(Color32[] px, int w, int h, int cx, int cy, Vector2 dir, int totalLen, int thickness)
        {
            int segments = UnityEngine.Random.Range(4, 7);
            Vector2 pos = new Vector2(cx, cy);
            Vector2 tangent = dir.normalized;

            for (int i = 0; i < segments; i++)
            {
                float bend = UnityEngine.Random.Range(-34f, 34f) * 0.0174532924f;
                float cs = Mathf.Cos(bend);
                float sn = Mathf.Sin(bend);
                tangent = new Vector2(tangent.x * cs - tangent.y * sn, tangent.x * sn + tangent.y * cs).normalized;

                int segLen = Mathf.RoundToInt(totalLen / (float)segments) + UnityEngine.Random.Range(-8, 9);
                Vector2 next = pos + tangent * segLen;
                DrawJaggedLine(px, w, h, Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y), Mathf.RoundToInt(next.x), Mathf.RoundToInt(next.y), thickness, 240, 5f, 12f);
                pos = next;
            }
        }

        void DrawBranchedCrack(Color32[] px, int w, int h, int sx, int sy, Vector2 dir, int length, int thickness, byte alpha, float jitterMin, float jitterMax)
        {
            Vector2 mainEnd = new Vector2(sx, sy) + dir.normalized * length;
            DrawJaggedLine(px, w, h, sx, sy, Mathf.RoundToInt(mainEnd.x), Mathf.RoundToInt(mainEnd.y), thickness, alpha, jitterMin, jitterMax);

            int branchCount = UnityEngine.Random.Range(2, 5);
            Vector2 start = new Vector2(sx, sy);
            Vector2 delta = (mainEnd - start).normalized;
            for (int i = 0; i < branchCount; i++)
            {
                float t = UnityEngine.Random.Range(0.22f, 0.88f);
                Vector2 branchStart = Vector2.Lerp(start, mainEnd, t);
                float bendDeg = UnityEngine.Random.Range(18f, 54f) * (UnityEngine.Random.value > 0.5f ? 1f : -1f);
                float bend = bendDeg * 0.0174532924f;
                float cs = Mathf.Cos(bend);
                float sn = Mathf.Sin(bend);
                Vector2 bdir = new Vector2(delta.x * cs - delta.y * sn, delta.x * sn + delta.y * cs).normalized;
                Vector2 branchEnd = branchStart + bdir * UnityEngine.Random.Range(length * 0.16f, length * 0.32f);
                DrawJaggedLine(px, w, h,
                    Mathf.RoundToInt(branchStart.x), Mathf.RoundToInt(branchStart.y),
                    Mathf.RoundToInt(branchEnd.x), Mathf.RoundToInt(branchEnd.y),
                    1, (byte)Mathf.Max(140, alpha - 60), 3f, 8f);
            }
        }

        Vector2 DirFromDeg(float deg)
        {
            float rad = deg * 0.0174532924f;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        void DrawJaggedCrack(Color32[] px, int w, int h, int sx, int sy, Vector2 dir, int length, int thickness, byte alpha, float jitterMin, float jitterMax)
        {
            Vector2 end = new Vector2(sx, sy) + dir.normalized * length;
            DrawJaggedLine(px, w, h, sx, sy, Mathf.RoundToInt(end.x), Mathf.RoundToInt(end.y), thickness, alpha, jitterMin, jitterMax);
        }

        void DrawJaggedCrack(Color32[] px, int w, int h, int sx, int sy, Vector2 dir, int length, int thickness, byte alpha, float jitterMin, float jitterMax, Color32 color)
        {
            Vector2 end = new Vector2(sx, sy) + dir.normalized * length;
            DrawJaggedLine(px, w, h, sx, sy, Mathf.RoundToInt(end.x), Mathf.RoundToInt(end.y), thickness, alpha, jitterMin, jitterMax, color);
        }

        void DrawJaggedLine(Color32[] px, int w, int h, int x0, int y0, int x1, int y1, int thickness, byte alpha, float jitterMin, float jitterMax)
        {
            DrawJaggedLine(px, w, h, x0, y0, x1, y1, thickness, alpha, jitterMin, jitterMax, new Color32(255, 255, 255, 255));
        }

        void DrawJaggedLine(Color32[] px, int w, int h, int x0, int y0, int x1, int y1, int thickness, byte alpha, float jitterMin, float jitterMax, Color32 color)
        {
            int steps = Mathf.Max(12, Mathf.RoundToInt(Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)) / 8f));
            Vector2 prev = new Vector2(x0, y0);
            Vector2 start = new Vector2(x0, y0);
            Vector2 end = new Vector2(x1, y1);
            Vector2 delta = (end - start).normalized;
            Vector2 normal = new Vector2(-delta.y, delta.x);

            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 p = Vector2.Lerp(start, end, t);

                float jitter = UnityEngine.Random.Range(jitterMin, jitterMax);
                if (i < steps)
                    p += normal * UnityEngine.Random.Range(-jitter, jitter);

                DrawLine(px, w, h,
                    Mathf.RoundToInt(prev.x), Mathf.RoundToInt(prev.y),
                    Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y),
                    thickness, alpha, color);

                prev = p;
            }
        }

        void DrawLine(Color32[] px, int w, int h, int x0, int y0, int x1, int y1, int thickness, byte alpha, Color32 color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                StampPoint(px, w, h, x0, y0, thickness, alpha, color);
                if (x0 == x1 && y0 == y1) break;

                int e2 = err * 2;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
        }

        void StampPoint(Color32[] px, int w, int h, int cx, int cy, int radius, byte alpha, Color32 color)
        {
            int r2 = radius * radius;
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if ((x * x) + (y * y) > r2) continue;

                    int pxX = cx + x;
                    int pxY = cy + y;
                    if (pxX < 0 || pxX >= w || pxY < 0 || pxY >= h) continue;

                    int idx = pxY * w + pxX;
                    byte a = (byte)Mathf.Max(px[idx].a, alpha);
                    px[idx] = new Color32(color.r, color.g, color.b, a);
                }
            }
        }

        void StampSoftBlotch(Color32[] px, int w, int h, int cx, int cy, int radius, Color32 color)
        {
            int r2 = radius * radius;
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    int d2 = (x * x) + (y * y);
                    if (d2 > r2) continue;

                    int pxX = cx + x;
                    int pxY = cy + y;
                    if (pxX < 0 || pxX >= w || pxY < 0 || pxY >= h) continue;

                    float d = Mathf.Sqrt(d2) / Mathf.Max(1f, radius);
                    float edge = 1f - d;
                    float noise = 0.75f + (Mathf.PerlinNoise((pxX * 0.09f) + 3.4f, (pxY * 0.11f) + 1.9f) * 0.5f);
                    byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(color.a * edge * noise), 0, 255);

                    int idx = pxY * w + pxX;
                    if (a > px[idx].a)
                        px[idx] = new Color32(color.r, color.g, color.b, a);
                }
            }
        }

        void StampRing(Color32[] px, int w, int h, int cx, int cy, int radius, int thickness, byte alpha)
        {
            for (int i = 0; i < 120; i++)
            {
                float a0 = (360f / 120f) * i;
                float rad = a0 * 0.0174532924f;
                int x = cx + Mathf.RoundToInt(Mathf.Cos(rad) * radius);
                int y = cy + Mathf.RoundToInt(Mathf.Sin(rad) * radius);
                StampPoint(px, w, h, x, y, Mathf.Max(1, thickness / 2), alpha, new Color32(255, 255, 255, 255));
            }
        }

        void StampBlackPatch(Color32[] px, int w, int h, int cx, int cy, int radiusX, int radiusY, byte alpha)
        {
            for (int y = -radiusY; y <= radiusY; y++)
            {
                for (int x = -radiusX; x <= radiusX; x++)
                {
                    float nx = x / Mathf.Max(1f, radiusX);
                    float ny = y / Mathf.Max(1f, radiusY);
                    float e = (nx * nx) + (ny * ny);
                    if (e > 1f) continue;

                    int pxX = cx + x;
                    int pxY = cy + y;
                    if (pxX < 0 || pxX >= w || pxY < 0 || pxY >= h) continue;

                    float noise = 0.7f + (Mathf.PerlinNoise(pxX * 0.12f, pxY * 0.12f) * 0.6f);
                    byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * (1f - e) * noise), 0, 255);

                    int idx = pxY * w + pxX;
                    if (a > px[idx].a)
                        px[idx] = new Color32(255, 255, 255, a);
                }
            }
        }

        void StampDeadSegments(Color32[] px, int w, int h, int rowCenter, int rowCount, byte alpha)
        {
            for (int r = 0; r < rowCount; r++)
            {
                int y = Mathf.Clamp(rowCenter + r, 0, h - 1);
                int segments = UnityEngine.Random.Range(4, 8);
                for (int s = 0; s < segments; s++)
                {
                    int x0 = UnityEngine.Random.Range(0, Mathf.Max(1, w - 20));
                    int x1 = Mathf.Min(w - 1, x0 + UnityEngine.Random.Range(8, 28));
                    for (int x = x0; x <= x1; x++)
                    {
                        int idx = y * w + x;
                        byte a = (byte)UnityEngine.Random.Range((int)(alpha * 0.55f), alpha + 1);
                        if (a > px[idx].a)
                            px[idx] = new Color32(255, 255, 255, a);
                    }
                }
            }
        }

        void StampRadial(Color32[] px, int w, int h, int cx, int cy, int radius, Color32 color)
        {
            int r2 = radius * radius;
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    int d2 = (x * x) + (y * y);
                    if (d2 > r2) continue;

                    int pxX = cx + x;
                    int pxY = cy + y;
                    if (pxX < 0 || pxX >= w || pxY < 0 || pxY >= h) continue;

                    float d = Mathf.Sqrt(d2) / Mathf.Max(1f, radius);
                    byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(color.a * (1f - d)), 0, 255);

                    int idx = pxY * w + pxX;
                    if (a > px[idx].a)
                        px[idx] = new Color32(color.r, color.g, color.b, a);
                }
            }
        }

        void DrawHorizontalBand(Color32[] px, int w, int h, int yCenter, int thickness, int xMin, int xMax, Color32 color)
        {
            int minY = Mathf.Clamp(yCenter - thickness, 0, h - 1);
            int maxY = Mathf.Clamp(yCenter + thickness, 0, h - 1);
            int minX = Mathf.Clamp(xMin, 0, w - 1);
            int maxX = Mathf.Clamp(xMax, 0, w - 1);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int idx = y * w + x;
                    if (color.a > px[idx].a)
                        px[idx] = color;
                }
            }
        }

        void DrawVerticalBand(Color32[] px, int w, int h, int xCenter, int thickness, int yMin, int yMax, Color32 color)
        {
            int minX = Mathf.Clamp(xCenter - thickness, 0, w - 1);
            int maxX = Mathf.Clamp(xCenter + thickness, 0, w - 1);
            int minY = Mathf.Clamp(yMin, 0, h - 1);
            int maxY = Mathf.Clamp(yMax, 0, h - 1);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    int idx = y * w + x;
                    if (color.a > px[idx].a)
                        px[idx] = color;
                }
            }
        }



        Vector2 PickImpactUV()
        {

            int i = _shotIndex % 6;
            switch (i)
            {
                case 0: return new Vector2(0.72f, 0.36f);
                case 1: return new Vector2(0.28f, 0.42f);
                case 2: return new Vector2(0.62f, 0.68f);
                case 3: return new Vector2(0.38f, 0.62f);
                case 4: return new Vector2(0.78f, 0.56f);
                default: return new Vector2(0.22f, 0.56f);
            }
        }

        Vector2 PickPeripheralUv(int i)
        {
            switch ((i + _shotIndex) % 6)
            {
                case 0: return new Vector2(0.14f, 0.24f);
                case 1: return new Vector2(0.83f, 0.20f);
                case 2: return new Vector2(0.18f, 0.78f);
                case 3: return new Vector2(0.80f, 0.76f);
                case 4: return new Vector2(0.52f, 0.18f);
                default: return new Vector2(0.46f, 0.84f);
            }
        }

        Vector2 RandomEdgePoint01(int seed)
        {
            int i = (seed + _shotIndex) % 4;
            float t = 0.15f + (0.18f * ((seed * 37) % 4));
            if (i == 0) return new Vector2(t, 0.02f);
            if (i == 1) return new Vector2(0.98f, t);
            if (i == 2) return new Vector2(1f - t, 0.98f);
            return new Vector2(0.02f, 1f - t);
        }



        static Camera FindHMDCamera()
        {
            Camera[] cams = Resources.FindObjectsOfTypeAll<Camera>();
            for (int i = 0; i < cams.Length; i++)
            {
                Camera c = cams[i];
                if (!c) continue;
                if (!c.gameObject.scene.IsValid()) continue;
                if (!c.gameObject.activeInHierarchy) continue;

                if (c.name == "HMD") return c;
            }
            return null;
        }
    }
}
