using System;
using System.Collections.Generic;
using UnhollowerRuntimeLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace IEYTD_Mod2Code
{
    public class TurretDriver : MonoBehaviour
    {
        public TurretDriver(IntPtr ptr) : base(ptr) { }
        public TurretDriver() : base(ClassInjector.DerivedConstructorPointer<TurretDriver>())
            => ClassInjector.DerivedConstructorBody(this);

        public Transform[] barrelMuzzles;
        public bool autoFindMuzzles = true;
        public string[] muzzleNameContains = new string[] { "muzzle", "tip", "flash" };

        public bool useLights = true;
        public float lightIntensity = 3.5f;
        public float lightRange = 1.4f;

        public float flashDuration = 0.035f;
        public float forwardOffset = 0.04f;
        public float flashLength = 0.42f;
        public float flashWidth = 0.14f;
        public float glowSize = 0.11f;
        public float tailLength = 0.18f;
        public float tailWidth = 0.06f;
        public float fadeSpeed = 30f;

        public Color coreColor = new Color(1f, 0.95f, 0.82f, 1f);
        public Color midColor = new Color(1f, 0.78f, 0.28f, 0.9f);
        public Color outerColor = new Color(1f, 0.38f, 0.05f, 0.7f);
        public Color tailColor = new Color(1f, 0.55f, 0.12f, 0.45f);

        readonly List<FlashInstance> _flashes = new List<FlashInstance>();
        readonly List<Material> _materials = new List<Material>();

        Texture2D _flashTex;
        Texture2D _glowTex;

        int _nextBarrel;
        bool _firing;

        class FlashInstance
        {
            public Transform muzzle;
            public GameObject root;
            public MeshRenderer coreA;
            public MeshRenderer coreB;
            public MeshRenderer outerA;
            public MeshRenderer outerB;
            public MeshRenderer glow;
            public MeshRenderer tail;
            public Light light;
            public float visible01;
            public float length;
            public float width;
            public float twist;
        }

        void Awake()
        {
            EnsureTextures();
            BuildIfNeeded();
            SetAllVisible(false);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            for (int i = 0; i < _flashes.Count; i++)
            {
                FlashInstance fx = _flashes[i];
                if (fx.visible01 > 0f)
                {
                    fx.visible01 = Mathf.MoveTowards(fx.visible01, 0f, fadeSpeed * dt);
                    ApplyFlashPose(fx);
                    if (fx.visible01 <= 0.001f)
                        ShowFlashImmediate(fx, false);
                }
            }
        }

        void OnDisable()
        {
            SetFiring(false);
            SetAllVisible(false);
        }

        void OnDestroy()
        {
            for (int i = 0; i < _materials.Count; i++)
            {
                if (_materials[i] != null)
                    Destroy(_materials[i]);
            }
        }

        public void SetFiring(bool on)
        {
            _firing = on;
            if (!on)
                SetAllVisible(false);
        }

        public void FirePulse()
        {
            BuildIfNeeded();
            if (_flashes.Count == 0)
                return;

            if (_nextBarrel >= _flashes.Count)
                _nextBarrel = 0;

            FlashInstance fx = _flashes[_nextBarrel];
            _nextBarrel++;

            fx.visible01 = 1f;
            fx.length = UnityEngine.Random.Range(flashLength * 0.9f, flashLength * 1.12f);
            fx.width = UnityEngine.Random.Range(flashWidth * 0.9f, flashWidth * 1.12f);
            fx.twist = UnityEngine.Random.Range(-8f, 8f);

            ApplyFlashPose(fx);
            ShowFlashImmediate(fx, true);
        }

        void EnsureTextures()
        {
            if (_flashTex != null && _glowTex != null)
                return;

            _flashTex = new Texture2D(64, 128, TextureFormat.ARGB32, false);
            _flashTex.wrapMode = TextureWrapMode.Clamp;
            _flashTex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < 128; y++)
            {
                float v = y / 127f;
                float centerBoost = Mathf.Clamp01(1f - Mathf.Abs(v - 0.3f) * 1.5f);
                for (int x = 0; x < 64; x++)
                {
                    float u = x / 63f;
                    float dx = Mathf.Abs(u - 0.5f) * 2f;
                    float width = Mathf.Lerp(0.95f, 0.06f, v);
                    float body = Mathf.Clamp01(1f - dx / Mathf.Max(0.001f, width));
                    float taper = Mathf.Clamp01(1f - v * 0.8f);
                    float tip = Mathf.Clamp01(1f - Mathf.Abs(v - 0.92f) * 6f);
                    float alpha = Mathf.Max(body * taper, body * tip * 0.85f) * (0.45f + centerBoost * 0.55f);
                    _flashTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            _flashTex.Apply(false, false);

            _glowTex = new Texture2D(64, 64, TextureFormat.ARGB32, false);
            _glowTex.wrapMode = TextureWrapMode.Clamp;
            _glowTex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < 64; y++)
            {
                float v = y / 63f;
                for (int x = 0; x < 64; x++)
                {
                    float u = x / 63f;
                    float dx = u - 0.5f;
                    float dy = v - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 0.5f;
                    float alpha = Mathf.Clamp01(1f - d);
                    alpha *= alpha;
                    _glowTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            _glowTex.Apply(false, false);
        }

        void BuildIfNeeded()
        {
            if (_flashes.Count > 0)
                return;

            if ((barrelMuzzles == null || barrelMuzzles.Length == 0) && autoFindMuzzles)
                AutoFindMuzzles();

            if (barrelMuzzles == null || barrelMuzzles.Length == 0)
                return;

            for (int i = 0; i < barrelMuzzles.Length; i++)
            {
                Transform muzzle = barrelMuzzles[i];
                if (muzzle == null)
                    continue;

                FlashInstance fx = BuildFlashForMuzzle(muzzle, i);
                if (fx != null)
                    _flashes.Add(fx);
            }
        }

        void AutoFindMuzzles()
        {
            List<Transform> found = new List<Transform>();
            Transform[] all = GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null)
                    continue;

                string n = (t.name ?? string.Empty).ToLowerInvariant();
                for (int k = 0; k < muzzleNameContains.Length; k++)
                {
                    string token = muzzleNameContains[k];
                    if (!string.IsNullOrEmpty(token) && n.Contains(token))
                    {
                        found.Add(t);
                        break;
                    }
                }
            }

            barrelMuzzles = found.ToArray();
        }

        FlashInstance BuildFlashForMuzzle(Transform muzzle, int index)
        {
            GameObject root = new GameObject("TurretMuzzleFlash_" + index);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            MeshRenderer coreA = CreateCard(root.transform, "CoreA", coreColor, 3997, _flashTex);
            MeshRenderer coreB = CreateCard(root.transform, "CoreB", coreColor, 3997, _flashTex);
            MeshRenderer outerA = CreateCard(root.transform, "OuterA", midColor, 3996, _flashTex);
            MeshRenderer outerB = CreateCard(root.transform, "OuterB", outerColor, 3995, _flashTex);
            MeshRenderer glow = CreateCard(root.transform, "Glow", coreColor, 3994, _glowTex);
            MeshRenderer tail = CreateCard(root.transform, "Tail", tailColor, 3993, _flashTex);

            Light L = null;
            if (useLights)
            {
                L = root.AddComponent<Light>();
                L.type = LightType.Point;
                L.color = new Color(1f, 0.72f, 0.28f, 1f);
                L.intensity = lightIntensity;
                L.range = lightRange;
                L.shadows = LightShadows.None;
                L.enabled = false;
            }

            FlashInstance fx = new FlashInstance();
            fx.muzzle = muzzle;
            fx.root = root;
            fx.coreA = coreA;
            fx.coreB = coreB;
            fx.outerA = outerA;
            fx.outerB = outerB;
            fx.glow = glow;
            fx.tail = tail;
            fx.light = L;
            fx.visible01 = 0f;
            fx.length = flashLength;
            fx.width = flashWidth;
            fx.twist = 0f;

            ShowFlashImmediate(fx, false);
            return fx;
        }

        MeshRenderer CreateCard(Transform parent, string name, Color color, int renderQueue, Texture2D texture)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            Collider c = go.GetComponent<Collider>();
            if (c != null)
                Destroy(c);

            MeshRenderer r = go.GetComponent<MeshRenderer>();
            SetupRenderer(r, color, renderQueue, texture);
            return r;
        }

        void SetupRenderer(MeshRenderer r, Color color, int renderQueue, Texture2D tex)
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.allowOcclusionWhenDynamic = false;

            Shader sh =
                Shader.Find("Legacy Shaders/Particles/Additive") ??
                Shader.Find("Particles/Additive") ??
                Shader.Find("Unlit/Transparent") ??
                Shader.Find("Sprites/Default");

            Material mat = new Material(sh);
            mat.renderQueue = renderQueue;
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", color);
            if (mat.HasProperty("_MainTex") && tex != null) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)CullMode.Off);

            r.material = mat;
            _materials.Add(mat);
        }

        void ApplyFlashPose(FlashInstance fx)
        {
            if (fx == null || fx.root == null || fx.muzzle == null)
                return;

            float t = fx.visible01;
            Vector3 fwd = fx.muzzle.forward.normalized;
            Vector3 up = fx.muzzle.up.normalized;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            if (up.sqrMagnitude < 0.001f) up = Vector3.up;

            fx.root.transform.position = fx.muzzle.position + fwd * forwardOffset;
            fx.root.transform.rotation = Quaternion.LookRotation(fwd, up) * Quaternion.Euler(0f, 0f, fx.twist);
            fx.root.transform.localScale = Vector3.one;

            float len = Mathf.Lerp(fx.length * 0.45f, fx.length, t);
            float wid = Mathf.Lerp(fx.width * 0.45f, fx.width, t);
            float glow = Mathf.Lerp(glowSize * 0.7f, glowSize, t);
            float tailLenNow = Mathf.Lerp(tailLength * 0.6f, tailLength, t);
            float tailWidNow = Mathf.Lerp(tailWidth * 0.6f, tailWidth, t);

            if (fx.coreA != null)
                SetCardAlongForward(fx.coreA.transform, len * 0.34f, len * 0.78f, wid * 0.7f, 0f);
            if (fx.coreB != null)
                SetCardAlongForward(fx.coreB.transform, len * 0.31f, len * 0.72f, wid * 0.58f, 90f);
            if (fx.outerA != null)
                SetCardAlongForward(fx.outerA.transform, len * 0.27f, len, wid, 45f);
            if (fx.outerB != null)
                SetCardAlongForward(fx.outerB.transform, len * 0.22f, len * 0.88f, wid * 1.15f, -45f);
            if (fx.glow != null)
                SetGlowCard(fx.glow.transform, glow, -0.005f);
            if (fx.tail != null)
                SetTailCard(fx.tail.transform, -tailLenNow * 0.18f, tailLenNow, tailWidNow);

            SetRendererAlpha(fx.coreA, coreColor, t);
            SetRendererAlpha(fx.coreB, coreColor, t * 0.95f);
            SetRendererAlpha(fx.outerA, midColor, t * 0.92f);
            SetRendererAlpha(fx.outerB, outerColor, t * 0.78f);
            SetRendererAlpha(fx.glow, coreColor, t * 0.85f);
            SetRendererAlpha(fx.tail, tailColor, t * 0.65f);

            if (fx.light != null)
            {
                fx.light.enabled = t > 0.02f;
                fx.light.intensity = lightIntensity * t;
            }
        }

        void SetCardAlongForward(Transform tr, float z, float length, float width, float rollDeg)
        {
            tr.localPosition = new Vector3(0f, 0f, z);
            tr.localRotation = Quaternion.Euler(0f, 0f, rollDeg);
            tr.localScale = new Vector3(width, length, 1f);
        }

        void SetGlowCard(Transform tr, float size, float z)
        {
            tr.localPosition = new Vector3(0f, 0f, z);
            tr.localRotation = Quaternion.identity;
            tr.localScale = new Vector3(size, size, 1f);
        }

        void SetTailCard(Transform tr, float z, float length, float width)
        {
            tr.localPosition = new Vector3(0f, 0f, z);
            tr.localRotation = Quaternion.Euler(180f, 0f, 0f);
            tr.localScale = new Vector3(width, length, 1f);
        }

        void SetRendererAlpha(MeshRenderer r, Color baseColor, float alphaMul)
        {
            if (r == null || r.material == null)
                return;

            Color c = baseColor;
            c.a *= alphaMul;
            if (r.material.HasProperty("_Color")) r.material.SetColor("_Color", c);
            if (r.material.HasProperty("_TintColor")) r.material.SetColor("_TintColor", c);
        }

        void SetAllVisible(bool visible)
        {
            for (int i = 0; i < _flashes.Count; i++)
            {
                _flashes[i].visible01 = visible ? 1f : 0f;
                ShowFlashImmediate(_flashes[i], visible);
            }
        }

        void ShowFlashImmediate(FlashInstance fx, bool visible)
        {
            if (fx == null)
                return;

            EnableRenderer(fx.coreA, visible);
            EnableRenderer(fx.coreB, visible);
            EnableRenderer(fx.outerA, visible);
            EnableRenderer(fx.outerB, visible);
            EnableRenderer(fx.glow, visible);
            EnableRenderer(fx.tail, visible);

            if (fx.light != null)
                fx.light.enabled = visible && fx.visible01 > 0.02f;
        }

        void EnableRenderer(MeshRenderer r, bool on)
        {
            if (r != null)
                r.enabled = on;
        }
    }
}
