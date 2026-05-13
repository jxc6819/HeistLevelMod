using System;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{

    public class ExplosionDriver : MonoBehaviour
    {
        public ExplosionDriver(IntPtr p) : base(p) { }
        public ExplosionDriver() : base(ClassInjector.DerivedConstructorPointer<ExplosionDriver>())
            => ClassInjector.DerivedConstructorBody(this);

        public Vector3 localOffset = Vector3.zero;

        public int BurstParticles = 60;

        public float LifeMin = 0.35f;
        public float LifeMax = 0.85f;

        public float SpeedMin = 3.0f;
        public float SpeedMax = 7.0f;

        public float Drag = 3.5f;
        public float UpwardBias = 0.4f;

        public float SizeMin = 0.3f;
        public float SizeMax = 0.8f;

        public float Turbulence = 0.55f;
        public float TurbFrequency = 2.4f;

        public Color StartColor = new Color(1.2f, 1.05f, 0.85f, 1.0f);
        public Color EndColor = new Color(0.7f, 0.25f, 0.05f, 0.0f);

        struct Puff
        {
            public Transform tr;
            public Renderer r;
            public Vector3 vel;
            public float age, life, size, seed;
            public MaterialPropertyBlock mpb;
            public bool Active => age >= 0f && age < life;
        }

        const int MAX_POOL = 96;
        Puff[] _puffs;
        int _pi;
        Material _mat;
        Texture2D _tex;
        Camera _cam;

        void Awake()
        {
            _cam = Camera.main;

            var sh = Shader.Find("FX/FX_Additive_UVPan_Shader");
            if (!sh)
                sh = Shader.Find("Phoenix/SH_Shared_DefaultPBR_Metallic_TransparentColorAlpha_01");
            if (!sh)
                sh = Shader.Find("Unlit/Transparent");

            _tex = BuildRadialTex(64);

            _mat = new Material(sh);
            if (_mat.HasProperty("_MainTex")) _mat.SetTexture("_MainTex", _tex);
            SetAllColorProps(_mat, StartColor);
            if (_mat.HasProperty("_PanSpeed")) _mat.SetVector("_PanSpeed", Vector4.zero);
            if (_mat.HasProperty("_TilingOffset")) _mat.SetVector("_TilingOffset", new Vector4(1f, 1f, 0f, 0f));
            _mat.renderQueue = 3100;

            _puffs = new Puff[MAX_POOL];
            for (int i = 0; i < _puffs.Length; i++)
                _puffs[i] = MakePuff("ExplosionPuff_" + i, _mat);
        }

        Puff MakePuff(string name, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(transform, false);

            var col = go.GetComponent<Collider>();
            if (col) UnityEngine.Object.Destroy(col);

            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.enabled = false;

            Puff p;
            p.tr = go.transform;
            p.r = mr;
            p.vel = Vector3.zero;
            p.age = -1f;
            p.life = 1f;
            p.size = 0.1f;
            p.seed = UnityEngine.Random.value * 1000f;
            p.mpb = new MaterialPropertyBlock();
            return p;
        }

        void OnEnable()
        {
            if (_puffs == null) return;
            for (int i = 0; i < _puffs.Length; i++)
            {
                var p = _puffs[i];
                p.age = -1f;
                if (p.r) p.r.enabled = false;
                _puffs[i] = p;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _puffs == null) return;

            for (int i = 0; i < _puffs.Length; i++)
            {
                var p = _puffs[i];
                if (!p.Active)
                {
                    _puffs[i] = p;
                    continue;
                }

                p.age += dt;
                float t = p.age / p.life;

                float n1 = Mathf.PerlinNoise(p.seed, Time.time * TurbFrequency) - 0.5f;
                float n2 = Mathf.PerlinNoise(p.seed * 1.37f, Time.time * (TurbFrequency * 1.4f)) - 0.5f;
                Vector3 noise =
                    transform.right * n1 +
                    transform.forward * n2 +
                    Vector3.up * (n1 * 0.25f);

                p.vel -= p.vel * (Drag * dt);
                p.vel += noise * (Turbulence * dt);

                p.tr.position += p.vel * dt;

                if (_cam)
                {
                    Vector3 toCam = (_cam.transform.position - p.tr.position).normalized;
                    if (toCam.sqrMagnitude > 1e-4f)
                        p.tr.rotation = Quaternion.LookRotation(-toCam, Vector3.up);
                }

                float s = Mathf.Lerp(p.size, p.size * 2.2f, t);
                p.tr.localScale = new Vector3(s, s, 1f);

                Color col = Color.Lerp(StartColor, EndColor, t);
                float alpha = Mathf.Clamp01(1f - t);
                col.a *= alpha;
                p.mpb.SetColor("_Color", col);
                p.r.SetPropertyBlock(p.mpb);

                if (t >= 1f)
                {
                    p.age = -1f;
                    p.r.enabled = false;
                }

                _puffs[i] = p;
            }
        }

        public void TriggerExplosion()
        {
            if (_puffs == null || _puffs.Length == 0) return;

            int count = BurstParticles;
            if (count > _puffs.Length) count = _puffs.Length;

            Vector3 origin = transform.TransformPoint(localOffset);

            for (int i = 0; i < count; i++)
                SpawnPuff(origin);

        }

        void SpawnPuff(Vector3 origin)
        {
            var p = _puffs[_pi];
            _pi = (_pi + 1) % _puffs.Length;

            p.age = 0f;
            p.life = Lerp(LifeMin, LifeMax, Rand());
            p.size = Lerp(SizeMin, SizeMax, Rand());
            p.seed = Rand() * 1000f;

            Vector2 jitter = UnityEngine.Random.insideUnitCircle * (SizeMax * 0.15f);
            Vector3 pos = origin
                          + transform.right * jitter.x
                          + transform.forward * jitter.y;
            p.tr.position = pos;

            Vector3 dir = UnityEngine.Random.onUnitSphere;
            dir += Vector3.up * UpwardBias;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.up;
            dir.Normalize();

            float spd = Lerp(SpeedMin, SpeedMax, Rand());
            p.vel = dir * spd;

            p.r.enabled = true;
            _puffs[_pi == 0 ? _puffs.Length - 1 : _pi - 1] = p;
        }

        Texture2D BuildRadialTex(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;

            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / c;
                    float dy = (y - c) / c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    float a = Mathf.Clamp01(1f - Mathf.SmoothStep(0.2f, 1.0f, r));
                    a = Mathf.Pow(a, 1.3f);
                    byte v = (byte)(a * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, v);
                }
            }

            t.SetPixels32(px);
            t.Apply(true, false);
            return t;
        }

        static void SetAllColorProps(Material m, Color c)
        {
            if (!m) return;
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Tint")) m.SetColor("_Tint", c);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * 0.7f);
        }

        static float Rand() => UnityEngine.Random.value;
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
