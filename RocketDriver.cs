using System;
using MelonLoader;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class RocketDriver : MonoBehaviour
    {
        public RocketDriver(IntPtr p) : base(p) { }
        public RocketDriver() : base(ClassInjector.DerivedConstructorPointer<RocketDriver>())
            => ClassInjector.DerivedConstructorBody(this);

        public bool PlayOnStart = false;

        public float Length = 0.45f;
        public float StartRadius = 0.02f;
        public float EndRadius = 0.08f;

        public float OuterRadiusMult = 1.55f;
        public float OuterAlphaMult = 0.55f;

        public float FlickerSpeed = 18f;
        public float FlickerAmount = 0.20f;

        public float WobbleAmount = 0.015f;
        public float WobbleSpeed = 10f;

        public int StreakCount = 3;
        public float StreakWidth = 0.018f;
        public float StreakTime = 0.08f;

        public bool UseLight = true;
        public float LightIntensity = 2.2f;
        public float LightRange = 0.6f;

        public float EnableFadeSpeed = 16f;

        public Color CoreColor = new Color(0.65f, 0.95f, 1.00f, 0.95f);
        public Color OuterColor = new Color(0.12f, 0.55f, 1.00f, 0.55f);

        public float ForwardOffset = 0.02f;

        GameObject _coreGO, _outerGO;
        MeshFilter _coreMF, _outerMF;
        MeshRenderer _coreR, _outerR;
        Mesh _coreMesh, _outerMesh;

        Material _matShared;
        Texture2D _maskTex;

        TrailRenderer[] _streaks;

        Light _light;

        bool _enabled;
        float _enable01;
        float _seed;

        void Start()
        {
            _seed = UnityEngine.Random.value * 1000f;
            BuildIfNeeded();

            _enable01 = 0f;
            SetVisible(false, true);

            if (PlayOnStart) Enable();
        }

        void OnDestroy()
        {
            try
            {
                if (_matShared != null) Destroy(_matShared);
                if (_maskTex != null) Destroy(_maskTex);

                if (_coreMesh != null) Destroy(_coreMesh);
                if (_outerMesh != null) Destroy(_outerMesh);
            }
            catch { }
        }

        public void Enable()
        {
            BuildIfNeeded();
            _enabled = true;
            SetVisible(true, false);
        }

        public void Disable()
        {
            _enabled = false;
        }

        void Update()
        {
            if (_coreGO == null) return;

            float dt = Time.deltaTime;
            float target = _enabled ? 1f : 0f;
            _enable01 = Mathf.MoveTowards(_enable01, target, EnableFadeSpeed * dt);

            if (_enable01 <= 0.0001f)
            {
                SetVisible(false, false);
                return;
            }

            SetVisible(true, false);

            float t = Time.time;

            float n = Mathf.PerlinNoise((_seed + t) * FlickerSpeed, 0.23f);
            float flick = Mathf.Lerp(1f - FlickerAmount, 1f + FlickerAmount, n);

            float wobX = (Mathf.PerlinNoise((_seed + t) * WobbleSpeed, 1.1f) - 0.5f) * 2f * WobbleAmount;
            float wobY = (Mathf.PerlinNoise((_seed + t) * WobbleSpeed, 2.2f) - 0.5f) * 2f * WobbleAmount;

            float coreLen = Length * flick;
            float coreEnd = EndRadius * Mathf.Lerp(0.92f, 1.10f, n);

            UpdatePlume(_coreGO, coreLen, coreEnd, wobX, wobY);
            UpdatePlume(_outerGO, coreLen * 1.08f, coreEnd * OuterRadiusMult, wobX * 1.4f, wobY * 1.4f);

            if (_matShared != null && _matShared.mainTexture != null)
            {
                float ox = t * (0.55f + n * 0.35f);
                float oy = t * (0.22f + n * 0.18f);
                _matShared.mainTextureOffset = new Vector2(ox, oy);
            }

            if (_streaks != null)
            {
                Color s0 = new Color(0.60f, 0.95f, 1.0f, 0.75f * _enable01);
                Color s1 = new Color(0.60f, 0.95f, 1.0f, 0.00f);
                for (int i = 0; i < _streaks.Length; i++)
                {
                    if (_streaks[i] == null) continue;
                    _streaks[i].startColor = s0;
                    _streaks[i].endColor = s1;
                }
            }

            if (_light != null)
            {
                _light.enabled = true;
                float lf = Mathf.Clamp(flick, 0.7f, 1.35f);
                _light.intensity = LightIntensity * lf * _enable01;
                _light.range = LightRange;
            }

            BakeConeVertexColors(_coreMesh, CoreColor, _enable01, flick, coreLen);
            BakeConeVertexColors(_outerMesh, new Color(OuterColor.r, OuterColor.g, OuterColor.b, OuterColor.a * OuterAlphaMult), _enable01, flick, coreLen);
        }

        void SetVisible(bool visible, bool clearTrails)
        {
            if (_coreR != null) _coreR.enabled = visible;
            if (_outerR != null) _outerR.enabled = visible;

            if (_streaks != null)
            {
                for (int i = 0; i < _streaks.Length; i++)
                {
                    if (_streaks[i] == null) continue;
                    if (!visible && clearTrails) _streaks[i].Clear();
                    _streaks[i].enabled = visible;
                }
            }

            if (_light != null) _light.enabled = visible && _enable01 > 0.001f;
        }

        void BuildIfNeeded()
        {
            if (_coreGO != null) return;

            Shader s = Shader.Find("Phoenix/SH_Shared_DefaultUnlit_TransparentAdditive_VertexColor");
            if (s == null)
            {

                s = Shader.Find("Phoenix/SH_Shared_DefaultUnlit_TransparentAdditive_VertexColor_01");
            }
            if (s == null)
            {

                s = Shader.Find("Unlit/Transparent");
                if (s == null) s = Shader.Find("Sprites/Default");
            }

            _matShared = new Material(s);
            _matShared.renderQueue = 3000;

            _maskTex = BuildFlameMaskTex64();
            _matShared.mainTexture = _maskTex;
            _matShared.mainTextureScale = new Vector2(1f, 1f);
            _matShared.mainTextureOffset = new Vector2(0f, 0f);

            _coreGO = new GameObject("RocketPlume_Core");
            _coreGO.transform.SetParent(transform, false);
            _coreGO.transform.localPosition = new Vector3(0f, 0f, ForwardOffset);

            _coreMF = _coreGO.AddComponent<MeshFilter>();
            _coreR = _coreGO.AddComponent<MeshRenderer>();
            _coreMesh = BuildTrueConeMesh(30);
            _coreMF.sharedMesh = _coreMesh;
            _coreR.material = _matShared;
            _coreR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _coreR.receiveShadows = false;

            _outerGO = new GameObject("RocketPlume_Outer");
            _outerGO.transform.SetParent(transform, false);
            _outerGO.transform.localPosition = new Vector3(0f, 0f, ForwardOffset);

            _outerMF = _outerGO.AddComponent<MeshFilter>();
            _outerR = _outerGO.AddComponent<MeshRenderer>();
            _outerMesh = BuildTrueConeMesh(30);
            _outerMF.sharedMesh = _outerMesh;
            _outerR.material = _matShared;
            _outerR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _outerR.receiveShadows = false;

            _streaks = new TrailRenderer[Mathf.Clamp(StreakCount, 0, 8)];
            for (int i = 0; i < _streaks.Length; i++)
            {
                var go = new GameObject("RocketPlume_Streak_" + i);
                go.transform.SetParent(transform, false);

                float ang = (360f / Mathf.Max(1, _streaks.Length)) * i;
                float rad = ang * 0.0174532924f;
                float r = StartRadius;

                go.transform.localPosition = new Vector3(Mathf.Cos(rad) * r, Mathf.Sin(rad) * r, ForwardOffset);

                var tr = go.AddComponent<TrailRenderer>();
                tr.time = StreakTime;
                tr.startWidth = StreakWidth;
                tr.endWidth = StreakWidth * 0.05f;
                tr.minVertexDistance = 0.01f;
                tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tr.receiveShadows = false;
                tr.autodestruct = false;

                tr.startColor = new Color(0.60f, 0.95f, 1.0f, 0.0f);
                tr.endColor = new Color(0.60f, 0.95f, 1.0f, 0.0f);

                tr.material = _matShared;
                tr.enabled = false;

                _streaks[i] = tr;
            }

            if (UseLight)
            {
                _light = gameObject.GetComponent<Light>();
                if (_light == null) _light = gameObject.AddComponent<Light>();
                _light.type = LightType.Point;
                _light.color = new Color(0.35f, 0.70f, 1.00f, 1f);
                _light.shadows = LightShadows.None;
                _light.enabled = false;
            }

            _enable01 = 0f;
            SetVisible(false, true);
        }

        void UpdatePlume(GameObject go, float length, float endRadius, float wobX, float wobY)
        {
            if (go == null) return;

            go.transform.localRotation = Quaternion.Euler(wobY * 120f, wobX * 120f, 0f);

            go.transform.localScale = new Vector3(endRadius, endRadius, length);
        }

        Mesh BuildTrueConeMesh(int sides)
        {
            sides = Mathf.Clamp(sides, 8, 80);

            int vertCount = sides + 1;
            Vector3[] v = new Vector3[vertCount];
            Vector2[] uv = new Vector2[vertCount];
            Vector3[] n = new Vector3[vertCount];
            Color32[] c = new Color32[vertCount];

            for (int i = 0; i < sides; i++)
            {
                float a = (i / (float)sides) * 6.283185307f;
                float x = Mathf.Cos(a);
                float y = Mathf.Sin(a);

                v[i] = new Vector3(x, y, 0f);
                uv[i] = new Vector2(i / (float)sides, 0f);
                n[i] = new Vector3(x, y, 0f).normalized;

                c[i] = new Color32(255, 255, 255, 255);
            }

            v[sides] = new Vector3(0f, 0f, 1f);
            uv[sides] = new Vector2(0.5f, 1f);
            n[sides] = Vector3.forward;
            c[sides] = new Color32(255, 255, 255, 0);

            int[] tris = new int[sides * 3];
            int t = 0;
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                tris[t++] = i;
                tris[t++] = next;
                tris[t++] = sides;
            }

            var m = new Mesh();
            m.vertices = v;
            m.uv = uv;
            m.normals = n;
            m.colors32 = c;
            m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }

        void BakeConeVertexColors(Mesh m, Color baseColor, float enable01, float flick, float length)
        {
            if (m == null) return;

            var v = m.vertices;
            var col = m.colors32;

            float aBase = Mathf.Clamp01(baseColor.a) * enable01;
            float boost = Mathf.Lerp(0.85f, 1.25f, Mathf.Clamp01((flick - 0.8f) / 0.6f));

            for (int i = 0; i < v.Length; i++)
            {
                float z = Mathf.Clamp01(v[i].z);
                float fade = Mathf.Clamp01(1f - z);
                fade = fade * fade;

                float a = aBase * fade * boost;

                byte r = (byte)Mathf.Clamp((baseColor.r * 255f), 0f, 255f);
                byte g = (byte)Mathf.Clamp((baseColor.g * 255f), 0f, 255f);
                byte b = (byte)Mathf.Clamp((baseColor.b * 255f), 0f, 255f);
                byte aa = (byte)Mathf.Clamp((a * 255f), 0f, 255f);

                col[i] = new Color32(r, g, b, aa);
            }

            m.colors32 = col;
        }

        Texture2D BuildFlameMaskTex64()
        {
            int w = 64, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float v = (y + 0.5f) / h;

                    float dx = (u - 0.5f) * 2f;
                    float dy = (v - 0.5f) * 2f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float radial = Mathf.Clamp01(1f - r);

                    float n = Mathf.PerlinNoise(u * 7.0f, v * 12.0f);
                    float n2 = Mathf.PerlinNoise(u * 18.0f, v * 26.0f) * 0.35f;
                    float noise = Mathf.Clamp01(n * 0.75f + n2);

                    float a = Mathf.Clamp01(radial * (0.45f + 0.65f * noise));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply(false, false);
            return tex;
        }
    }
}
