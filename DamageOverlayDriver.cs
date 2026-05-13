using System;
using System.Collections;
using MelonLoader;
using UnhollowerBaseLib.Attributes;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{

    public class DamageOverlayDriver : MonoBehaviour
    {
        public DamageOverlayDriver(IntPtr p) : base(p) { }
        public DamageOverlayDriver() : base(ClassInjector.DerivedConstructorPointer<DamageOverlayDriver>())
            => ClassInjector.DerivedConstructorBody(this);

        public static DamageOverlayDriver Instance;

        public bool StartEnabled = false;
        public float Overscan = 1.75f;
        public float OverlayDistance = 0.032f;

        public int TextureSize = 160;

        public float PoisonDuration = 10.5f;
        public float TurretDuration = 1.46f;
        public float ExplosionDuration = 1.5f;

        public float PoisonBloodMaxAlpha = 0.68f;
        public float PoisonBlackMaxAlpha = 1.0f;
        public float PoisonBreathStrength = 0.16f;
        public float PoisonBreathSpeed = 1.85f;
        public float PoisonPeripheralPulse = 0.42f;
        public float PoisonJitter = 0.0010f;
        public float PoisonDarkenStart = 0.44f;
        public float PoisonPreFinalBlackCeiling = 0.84f;
        public float PoisonFinalCollapseStart = 0.88f;
        public float PoisonFinalBlackHold = 0.12f;

        public float PoisonBloodScrollX = 0.0018f;
        public float PoisonBloodScrollY = 0.0036f;
        public float PoisonBloodUVBreath = 0.0032f;

        public float TurretBloodMaxAlpha = 1.0f;
        public float TurretBlackMaxAlpha = 1.0f;
        public float TurretFlashAlpha = 0.55f;
        public float TurretJitter = 0.0038f;

        public float ExplosionBloodMaxAlpha = 1.0f;
        public float ExplosionBlackMaxAlpha = 0.42f;
        public float ExplosionJitter = 0.0014f;
        public float ExplosionBlurPulse = 0.10f;
        public float ExplosionBloodScrollX = 0.012f;
        public float ExplosionBloodScrollY = 0.008f;

        public float HitDuration = 1.25f;
        public float HitBloodMaxAlpha = 0.52f;
        public float HitFlashMaxAlpha = 0.42f;
        public float HitBlackMaxAlpha = 0.10f;
        public float HitJitter = 0.0022f;
        public float HitBloodScrollX = 0.006f;
        public float HitBloodScrollY = 0.004f;

        public Color BloodColor = new Color(0.34f, 0.03f, 0.03f, 1f);
        public Color BloodDarkColor = new Color(0.08f, 0.00f, 0.00f, 1f);
        public Color BloodWetColor = new Color(0.50f, 0.08f, 0.08f, 1f);
        public Color FlashColor = new Color(0.42f, 0.03f, 0.03f, 1f);
        public float BloodContrast = 1.10f;
        public float BloodWetness = 0.12f;
        public float PeripheralDarken = 0.36f;

        Camera _cam;
        GameObject _root;
        MeshRenderer _bloodR;
        MeshRenderer _flashR;
        MeshRenderer _blackR;
        Material _bloodMat;
        Material _flashMat;
        Material _blackMat;

        Texture2D _poisonTex;
        Texture2D _turretTex;
        Texture2D _clearTex;

        bool _built;
        bool _overlayOn;
        object _animHandle;

        int _texW;
        int _texH;
        Color32[] _scratch;
        float[] _distField;
        float[] _noiseA;
        float[] _noiseB;
        float[] _noiseC;
        float[] _wetNoise;

        Vector2 _bloodUV;
        int _mode;

        public static void TriggerPoisonDeath() => Instance?.PoisonDeath();
        public static void TriggerTurretDeath() => Instance?.TurretDeath();
        public static void TriggerExplosionDeath() => Instance?.ExplosionDeath();
        public static void TriggerHit() => Instance?.Hit();
        public static void TriggerHit(float duration) => Instance?.Hit(duration);

        public void Enable() => SetEnabled(true);
        public void Disable() => SetEnabled(false);

        void Awake()
        {
            Instance = this;
            EnsureBuilt();
        }

        void Start()
        {
            EnsureBuilt();
        }

        void LateUpdate()
        {
            if (!_overlayOn || _cam == null || _root == null) return;
            FitToCamera();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;

            try { if (_animHandle != null) MelonCoroutines.Stop(_animHandle); } catch { }
            try { if (_poisonTex) Destroy(_poisonTex); } catch { }
            try { if (_turretTex) Destroy(_turretTex); } catch { }
            try { if (_clearTex) Destroy(_clearTex); } catch { }
            try { if (_bloodMat) Destroy(_bloodMat); } catch { }
            try { if (_flashMat) Destroy(_flashMat); } catch { }
            try { if (_blackMat) Destroy(_blackMat); } catch { }
            try { if (_root) Destroy(_root); } catch { }
        }

        public void SetEnabled(bool on)
        {
            _overlayOn = on;
            if (_root) _root.SetActive(on);
            if (on) FitToCamera();
        }

        public void ClearOverlay()
        {
            if (_animHandle != null)
            {
                MelonCoroutines.Stop(_animHandle);
                _animHandle = null;
            }

            _mode = 0;
            _bloodUV = Vector2.zero;

            if (_bloodMat != null)
            {
                if (_bloodMat.HasProperty("_MainTex") && _clearTex != null)
                    _bloodMat.SetTexture("_MainTex", _clearTex);
                if (_bloodMat.HasProperty("_Color"))
                    _bloodMat.SetColor("_Color", new Color(1f, 1f, 1f, 0f));
                SafeSetTexOffset(_bloodMat, _bloodUV);
            }

            SetFlashAlpha(0f);
            SetBlackAlpha(0f);
            ResetRootOffset();
            SetEnabled(false);
        }

        public void PoisonDeath()
        {
            EnsureBuilt();
            if (!_built) return;
            StartAnimation(Co_PlayPoison());
        }

        public void TurretDeath()
        {
            EnsureBuilt();
            if (!_built) return;
            StartAnimation(Co_PlayTurret());
        }

        public void ExplosionDeath()
        {
            EnsureBuilt();
            if (!_built) return;
            StartAnimation(Co_PlayExplosion());
        }

        public void Hit()
        {
            Hit(HitDuration);
        }

        public void Hit(float duration)
        {
            EnsureBuilt();
            if (!_built) return;
            StartAnimation(Co_PlayHit(duration));
        }

        void StartAnimation(IEnumerator routine)
        {
            if (_animHandle != null)
            {
                MelonCoroutines.Stop(_animHandle);
                _animHandle = null;
            }

            SetEnabled(true);
            _animHandle = MelonCoroutines.Start(routine);
        }

        void EnsureBuilt()
        {
            if (_cam == null) _cam = FindHMDCamera();
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            if (_built) return;

            BuildOverlay();
            InitFields(Mathf.Clamp(TextureSize, 96, 192), Mathf.Clamp(TextureSize, 96, 192));
            BakeReusableTextures();

            _built = true;
            SetEnabled(StartEnabled);
            if (!StartEnabled) ClearOverlay();
        }

        void BuildOverlay()
        {
            _root = new GameObject("DamageOverlayRoot");
            _root.transform.SetParent(_cam.transform, false);
            _root.transform.localRotation = Quaternion.identity;
            _root.transform.localScale = Vector3.one;

            Shader alphaSh = Shader.Find("FX/FX_Alpha_UVPan_Shader");
            if (alphaSh == null) alphaSh = Shader.Find("Unlit/Transparent");
            if (alphaSh == null) alphaSh = Shader.Find("Sprites/Default");

            _bloodMat = new Material(alphaSh);
            _bloodMat.name = "DamageOverlay_BloodMat";
            _bloodMat.renderQueue = 4997;
            if (_bloodMat.HasProperty("_Color"))
                _bloodMat.SetColor("_Color", new Color(1f, 1f, 1f, 0f));

            _flashMat = new Material(alphaSh);
            _flashMat.name = "DamageOverlay_FlashMat";
            _flashMat.renderQueue = 4998;
            if (_flashMat.HasProperty("_Color"))
                _flashMat.SetColor("_Color", new Color(FlashColor.r, FlashColor.g, FlashColor.b, 0f));

            _blackMat = new Material(alphaSh);
            _blackMat.name = "DamageOverlay_BlackMat";
            _blackMat.renderQueue = 5000;
            if (_blackMat.HasProperty("_Color"))
                _blackMat.SetColor("_Color", new Color(0f, 0f, 0f, 0f));
            try
            {
                if (_blackMat.HasProperty("_ZTest")) _blackMat.SetInt("_ZTest", 8);
                if (_blackMat.HasProperty("_ZWrite")) _blackMat.SetInt("_ZWrite", 0);
                if (_blackMat.HasProperty("_Cull")) _blackMat.SetInt("_Cull", 0);
            }
            catch { }

            _clearTex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            _clearTex.wrapMode = TextureWrapMode.Clamp;
            _clearTex.filterMode = FilterMode.Bilinear;
            _clearTex.SetPixels32(new Color32[] {
                new Color32(0,0,0,0), new Color32(0,0,0,0),
                new Color32(0,0,0,0), new Color32(0,0,0,0)
            });
            _clearTex.Apply(false, false);

            _bloodR = MakeQuad("Blood", _bloodMat, -0.0003f);
            _flashR = MakeQuad("Flash", _flashMat, -0.0002f);
            _blackR = MakeQuad("Black", _blackMat, -0.0001f);
            if (_blackR != null)
            {
                _blackR.allowOcclusionWhenDynamic = false;
                _blackR.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                _blackR.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }

            FitToCamera();
            SetFlashAlpha(0f);
            SetBlackAlpha(0f);
        }

        MeshRenderer MakeQuad(string name, Material mat, float localZ)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Damage_" + name;
            go.transform.SetParent(_root.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, localZ);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var col = go.GetComponent<Collider>();
            if (col) Destroy(col);

            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        void FitToCamera()
        {
            if (_cam == null || _root == null) return;

            float zWorld = Mathf.Max(0.03f, _cam.nearClipPlane + OverlayDistance);

            Vector3 s = _cam.transform.lossyScale;
            float invX = Mathf.Abs(s.x) < 0.0001f ? 1f : 1f / s.x;
            float invY = Mathf.Abs(s.y) < 0.0001f ? 1f : 1f / s.y;
            float invZ = Mathf.Abs(s.z) < 0.0001f ? 1f : 1f / s.z;

            Vector3 lp = _root.transform.localPosition;
            _root.transform.localPosition = new Vector3(lp.x, lp.y, zWorld * invZ);

            const float DEG2RAD = 0.0174532924f;
            float hWorld = 2f * zWorld * Mathf.Tan(_cam.fieldOfView * 0.5f * DEG2RAD);
            float wWorld = hWorld * _cam.aspect;

            _root.transform.localScale = new Vector3(
                wWorld * Overscan * invX,
                hWorld * Overscan * invY,
                1f);
        }

        void InitFields(int w, int h)
        {
            _texW = w;
            _texH = h;
            int count = w * h;

            _scratch = new Color32[count];
            _distField = new float[count];
            _noiseA = new float[count];
            _noiseB = new float[count];
            _noiseC = new float[count];
            _wetNoise = new float[count];

            int idx = 0;
            for (int y = 0; y < h; y++)
            {
                float fy = y / (float)(h - 1);
                float dy = (fy - 0.5f) * 2f;

                for (int x = 0; x < w; x++, idx++)
                {
                    float fx = x / (float)(w - 1);
                    float dx = (fx - 0.5f) * 2f;
                    float ax = dx * 0.88f;

                    _distField[idx] = Mathf.Sqrt(ax * ax + dy * dy);
                    _noiseA[idx] = Mathf.PerlinNoise(fx * 4.8f + 1.2f, fy * 4.8f + 3.1f);
                    _noiseB[idx] = Mathf.PerlinNoise(fx * 8.2f + 4.3f, fy * 8.2f + 5.2f);
                    _noiseC[idx] = Mathf.PerlinNoise(fx * 11.6f + 8.1f, fy * 11.6f + 1.7f);
                    _wetNoise[idx] = Mathf.PerlinNoise(fx * 14.0f + 7.4f, fy * 14.0f + 14.2f);
                }
            }
        }

        void BakeReusableTextures()
        {
            _poisonTex = new Texture2D(_texW, _texH, TextureFormat.RGBA32, false, true);
            _poisonTex.wrapMode = TextureWrapMode.Clamp;
            _poisonTex.filterMode = FilterMode.Bilinear;
            _poisonTex.anisoLevel = 0;

            _turretTex = new Texture2D(_texW, _texH, TextureFormat.RGBA32, false, true);
            _turretTex.wrapMode = TextureWrapMode.Clamp;
            _turretTex.filterMode = FilterMode.Bilinear;
            _turretTex.anisoLevel = 0;

            BuildBloodTexture(_poisonTex, true);
            BuildBloodTexture(_turretTex, false);
        }

        void BuildBloodTexture(Texture2D target, bool poisonMode)
        {
            if (target == null || _scratch == null) return;

            float feather = poisonMode ? 0.34f : 0.18f;
            float startRadius = poisonMode ? 0.62f : 0.40f;
            float noiseAmp = poisonMode ? 0.12f : 0.20f;
            float safeFeather = Mathf.Max(0.0001f, feather);

            for (int i = 0; i < _scratch.Length; i++)
            {
                float dist = _distField[i];

                float edgeNoise = ((_noiseA[i] - 0.5f) * 0.55f) + ((_noiseB[i] - 0.5f) * 0.30f) + ((_noiseC[i] - 0.5f) * 0.15f);
                edgeNoise *= 2f * noiseAmp;

                float localRadius = startRadius + edgeNoise;
                float mask = Mathf.Clamp01(Mathf.InverseLerp(localRadius, localRadius + safeFeather, dist));

                if (mask <= 0.0001f)
                {
                    _scratch[i] = new Color32(0, 0, 0, 0);
                    continue;
                }

                float blotch = Mathf.Lerp(0.78f, 1.14f, _noiseC[i]);
                float wet = Mathf.Lerp(0.92f, 1.10f, _wetNoise[i]);
                float density = Mathf.Clamp01(mask * blotch * wet * BloodContrast);

                float rimDark = Mathf.Lerp(1f - PeripheralDarken, 1f,
                    Mathf.Clamp01((localRadius + safeFeather * 0.5f) - dist + 0.5f));

                float shine = Mathf.Clamp01(((_wetNoise[i] - 0.55f) * 2.0f) * mask) * BloodWetness;

                Color c = Color.Lerp(BloodDarkColor, BloodColor, Mathf.Clamp01(density * rimDark));
                c = Color.Lerp(c, BloodWetColor, shine);

                float alpha = poisonMode
                    ? Mathf.Clamp01(mask * Mathf.Lerp(0.70f, 0.92f, blotch))
                    : Mathf.Clamp01(mask * Mathf.Lerp(0.88f, 1.00f, blotch));

                _scratch[i].r = ToByte(c.r);
                _scratch[i].g = ToByte(c.g);
                _scratch[i].b = ToByte(c.b);
                _scratch[i].a = ToByte(alpha);
            }

            target.SetPixels32(_scratch);
            target.Apply(false, false);
        }

        [HideFromIl2Cpp]
        IEnumerator Co_PlayPoison()
        {
            _mode = 1;
            _bloodUV = Vector2.zero;

            if (_bloodMat != null)
            {
                if (_bloodMat.HasProperty("_MainTex"))
                    _bloodMat.SetTexture("_MainTex", _poisonTex != null ? _poisonTex : _clearTex);
                SafeSetTexOffset(_bloodMat, _bloodUV);
            }

            float dur = Mathf.Max(0.1f, PoisonDuration);
            float t = 0f;

            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);

                float breath = Mathf.Sin(Time.time * PoisonBreathSpeed) * 0.5f + 0.5f;
                float breathCentered = (breath - 0.5f) * 2f;

                float suffocationStage = Mathf.Clamp01(Mathf.InverseLerp(0.03f, 0.46f, u));
                float bloodStage = Mathf.Clamp01(Mathf.InverseLerp(0.28f, 1f, u));
                float blackoutStage = Mathf.Clamp01(Mathf.InverseLerp(PoisonDarkenStart, 1f, u));
                float finalStage = Mathf.Clamp01(Mathf.InverseLerp(0.78f, 1f, u));

                float bloodBase = Mathf.SmoothStep(0f, 1f, bloodStage);
                float bloodPulse = Mathf.Max(0f, breathCentered) * PoisonBreathStrength * (1f - finalStage * 0.8f);
                float bloodAlpha = Mathf.Clamp01(0.06f + bloodBase * 0.62f + bloodPulse) * PoisonBloodMaxAlpha;

                float breathDip = (1f - breath) * PoisonPeripheralPulse * (0.35f + suffocationStage * 0.85f);
                float blackBase = 0.04f + suffocationStage * 0.24f + bloodStage * 0.10f;
                float blackRamp = Mathf.Pow(blackoutStage, 1.55f) * PoisonBlackMaxAlpha;
                float preFinalBlack = Mathf.Clamp01(blackBase + breathDip + blackRamp);
                preFinalBlack = Mathf.Min(preFinalBlack, PoisonPreFinalBlackCeiling);

                float finalCollapse = Mathf.Clamp01(Mathf.InverseLerp(PoisonFinalCollapseStart, 1f, u));
                float blackAlpha = Mathf.Lerp(preFinalBlack, PoisonBlackMaxAlpha, Mathf.Pow(finalCollapse, 1.35f));

                _bloodUV.x += Time.deltaTime * (PoisonBloodScrollX + breathCentered * 0.0007f);
                _bloodUV.y += Time.deltaTime * (PoisonBloodScrollY + breath * 0.0012f);
                Vector2 breathOffset = new Vector2(0f, breathCentered * PoisonBloodUVBreath * (1f - finalStage * 0.8f));
                SafeSetTexOffset(_bloodMat, _bloodUV + breathOffset);

                SetBloodAlpha(bloodAlpha);
                SetFlashAlpha(0f);
                SetBlackAlpha(blackAlpha);
                ApplyJitter(PoisonJitter * (0.30f + 0.70f * u), 0.35f + breathDip * 1.35f);

                yield return null;
            }

            SetBloodAlpha(PoisonBloodMaxAlpha);
            SetFlashAlpha(0f);
            SetBlackAlpha(PoisonBlackMaxAlpha);
            SafeSetTexOffset(_bloodMat, _bloodUV);

            if (PoisonFinalBlackHold > 0.001f)
                yield return new WaitForSeconds(PoisonFinalBlackHold);

            _animHandle = null;
        }

        [HideFromIl2Cpp]
        IEnumerator Co_PlayExplosion()
        {
            _mode = 3;
            _bloodUV = Vector2.zero;

            if (_bloodMat != null)
            {
                if (_bloodMat.HasProperty("_MainTex"))
                    _bloodMat.SetTexture("_MainTex", _turretTex != null ? _turretTex : _clearTex);
                SafeSetTexOffset(_bloodMat, _bloodUV);
            }

            float dur = Mathf.Max(0.08f, ExplosionDuration);
            float t = 0f;

            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);

                float rush = Mathf.SmoothStep(0f, 1f, u);
                float bloodAlpha = Mathf.Clamp01(Mathf.Lerp(0.08f, ExplosionBloodMaxAlpha, Mathf.Pow(rush, 0.62f)));

                float blackStage = Mathf.Clamp01(Mathf.InverseLerp(0.18f, 1f, u));
                float blackAlpha = Mathf.Clamp01(Mathf.Pow(blackStage, 1.55f) * ExplosionBlackMaxAlpha);

                float blurPulse = (1f - Mathf.Abs((u * 2f) - 1f));
                float intensity = 0.18f + blurPulse * ExplosionBlurPulse;

                _bloodUV.x += Time.deltaTime * (ExplosionBloodScrollX + blurPulse * 0.008f);
                _bloodUV.y += Time.deltaTime * (ExplosionBloodScrollY + blurPulse * 0.004f);
                SafeSetTexOffset(_bloodMat, _bloodUV);

                SetBloodAlpha(bloodAlpha);
                SetFlashAlpha(0f);
                SetBlackAlpha(blackAlpha);
                ApplyJitter(ExplosionJitter * (1.10f - u * 0.40f), intensity);

                yield return null;
            }

            SetBloodAlpha(ExplosionBloodMaxAlpha);
            SetFlashAlpha(0f);
            SetBlackAlpha(ExplosionBlackMaxAlpha);
            SafeSetTexOffset(_bloodMat, _bloodUV);
            _animHandle = null;
        }

        [HideFromIl2Cpp]
        IEnumerator Co_PlayTurret()
        {
            _mode = 2;
            _bloodUV = Vector2.zero;

            if (_bloodMat != null)
            {
                if (_bloodMat.HasProperty("_MainTex"))
                    _bloodMat.SetTexture("_MainTex", _turretTex != null ? _turretTex : _clearTex);
                SafeSetTexOffset(_bloodMat, _bloodUV);
            }

            float dur = Mathf.Max(0.08f, TurretDuration);
            float t = 0f;

            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);

                float pulseWindow = Mathf.InverseLerp(0f, 0.68f, u);
                float bloodAlpha;
                if (u < 0.68f)
                    bloodAlpha = Mathf.Clamp01(Mathf.Lerp(0.06f, 0.40f, Mathf.Pow(pulseWindow, 0.95f)));
                else
                    bloodAlpha = Mathf.Clamp01(Mathf.Lerp(0.40f, TurretBloodMaxAlpha, Mathf.Pow(Mathf.InverseLerp(0.68f, 1f, u), 0.58f)));

                float blackAlpha = 0f;
                if (u >= 0.72f)
                    blackAlpha = Mathf.Clamp01(Mathf.Pow(Mathf.InverseLerp(0.72f, 1f, u), 0.85f) * TurretBlackMaxAlpha);

                float flicker = 0f;
                if (u < 0.68f)
                {
                    float pulse1 = 1f - Mathf.Clamp01(Mathf.Abs(u - 0.06f) / 0.030f);
                    float pulse2 = 1f - Mathf.Clamp01(Mathf.Abs(u - 0.17f) / 0.032f);
                    float pulse3 = 1f - Mathf.Clamp01(Mathf.Abs(u - 0.29f) / 0.034f);
                    float pulse4 = 1f - Mathf.Clamp01(Mathf.Abs(u - 0.42f) / 0.036f);
                    float pulse5 = 1f - Mathf.Clamp01(Mathf.Abs(u - 0.56f) / 0.040f);

                    pulse1 *= pulse1;
                    pulse2 *= pulse2;
                    pulse3 *= pulse3;
                    pulse4 *= pulse4;
                    pulse5 *= pulse5;

                    flicker = Mathf.Clamp01(
                        pulse1 * 1.00f +
                        pulse2 * 0.96f +
                        pulse3 * 0.92f +
                        pulse4 * 0.88f +
                        pulse5 * 0.84f);
                }

                _bloodUV.x += Time.deltaTime * 0.010f;
                _bloodUV.y += Time.deltaTime * 0.006f;
                SafeSetTexOffset(_bloodMat, _bloodUV);

                float flashAlpha = TurretFlashAlpha * flicker;
                SetBloodAlpha(bloodAlpha);
                SetFlashAlpha(flashAlpha);
                SetBlackAlpha(blackAlpha);
                ApplyJitter(TurretJitter * (1.16f - u * 0.28f), Mathf.Max(flicker, 0.20f * (1f - u)));

                yield return null;
            }

            SetBloodAlpha(TurretBloodMaxAlpha);
            SetFlashAlpha(0f);
            SetBlackAlpha(1f);
            SafeSetTexOffset(_bloodMat, _bloodUV);
            _animHandle = null;
        }

        [HideFromIl2Cpp]
        IEnumerator Co_PlayHit(float duration)
        {
            _mode = 4;
            _bloodUV = Vector2.zero;

            if (_bloodMat != null)
            {
                if (_bloodMat.HasProperty("_MainTex"))
                    _bloodMat.SetTexture("_MainTex", _turretTex != null ? _turretTex : _clearTex);
                SafeSetTexOffset(_bloodMat, _bloodUV);
            }

            float dur = Mathf.Max(0.08f, duration);
            float t = 0f;

            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);

                float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.InverseLerp(0f, 0.22f, u)));
                float release = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.InverseLerp(0.34f, 1f, u)));
                float k = Mathf.Clamp01(attack * release);

                float rebound = 1f - Mathf.Clamp01(Mathf.Abs(u - 0.28f) / 0.16f);
                rebound *= rebound * 0.14f * release;
                float hitK = Mathf.Clamp01(k + rebound);

                _bloodUV.x += Time.deltaTime * HitBloodScrollX;
                _bloodUV.y += Time.deltaTime * HitBloodScrollY;
                SafeSetTexOffset(_bloodMat, _bloodUV);

                SetBloodAlpha(HitBloodMaxAlpha * hitK);
                SetFlashAlpha(HitFlashMaxAlpha * Mathf.Clamp01(attack * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.InverseLerp(0.26f, 0.78f, u))))));
                SetBlackAlpha(HitBlackMaxAlpha * hitK);
                ApplyJitter(HitJitter * hitK, hitK);

                yield return null;
            }

            SetBloodAlpha(0f);
            SetFlashAlpha(0f);
            SetBlackAlpha(0f);
            ResetRootOffset();
            _mode = 0;
            _animHandle = null;
            SetEnabled(false);
        }

        void SafeSetTexOffset(Material m, Vector2 offset)
        {
            if (m == null) return;
            try
            {
                if (m.HasProperty("_MainTex"))
                    m.SetTextureOffset("_MainTex", offset);
            }
            catch { }
        }

        void SetBloodAlpha(float a)
        {
            if (_bloodMat != null && _bloodMat.HasProperty("_Color"))
                _bloodMat.SetColor("_Color", new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
        }

        void SetFlashAlpha(float a)
        {
            if (_flashMat != null && _flashMat.HasProperty("_Color"))
                _flashMat.SetColor("_Color", new Color(FlashColor.r, FlashColor.g, FlashColor.b, Mathf.Clamp01(a)));
        }

        void SetBlackAlpha(float a)
        {
            if (_blackMat == null || !_blackMat.HasProperty("_Color")) return;

            float alpha = Mathf.Clamp01(a);
            if (alpha >= 0.985f) alpha = 1f;
            _blackMat.SetColor("_Color", new Color(0f, 0f, 0f, alpha));
        }

        void ApplyJitter(float amt, float intensity)
        {
            if (_root == null) return;
            Vector3 lp = _root.transform.localPosition;

            if (amt <= 0.000001f)
            {
                _root.transform.localPosition = new Vector3(0f, 0f, lp.z);
                return;
            }

            float jx = (Mathf.PerlinNoise(Time.time * 23.7f, 0.11f) - 0.5f) * 2f;
            float jy = (Mathf.PerlinNoise(0.21f, Time.time * 21.4f) - 0.5f) * 2f;
            float kick = 1f + Mathf.Clamp01(intensity) * 0.65f;
            _root.transform.localPosition = new Vector3(jx * amt * kick, jy * amt * kick, lp.z);
        }

        void ResetRootOffset()
        {
            if (_root == null) return;
            Vector3 lp = _root.transform.localPosition;
            _root.transform.localPosition = new Vector3(0f, 0f, lp.z);
        }

        byte ToByte(float v)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(v) * 255f), 0, 255);
        }

        static Camera FindHMDCamera()
        {
            var cams = Resources.FindObjectsOfTypeAll<Camera>();
            for (int i = 0; i < cams.Length; i++)
            {
                var c = cams[i];
                if (!c) continue;
                if (!c.gameObject.scene.IsValid()) continue;
                if (!c.gameObject.activeInHierarchy) continue;
                if (c.name == "HMD") return c;
            }
            return null;
        }

    }
}
