using System;
using UnityEngine;
using MelonLoader;
using UnhollowerRuntimeLib;

namespace IEYTD_Mod2Code
{
    public class WireVisionManager : MonoBehaviour
    {
        public WireVisionManager(IntPtr ptr) : base(ptr) { }
        public WireVisionManager() : base(ClassInjector.DerivedConstructorPointer<WireVisionManager>())
            => ClassInjector.DerivedConstructorBody(this);

        public Transform lasersRoot;
        public Transform electricalBox;
        public Transform laserFinalCheckpoint;
        public Transform laserMidCheckpoint;

        public Color wireColor = new Color(0.22f, 0.72f, 1.00f, 0.75f);
        public float wireWidth = 0.02f;

        public float fadeSpeed = 8f;
        public float scrollSpeed = 0.7f;
        public float textureTileX = 6f;

        public int sortingOrder = 20000;
        public bool buildOnStart = true;

        public bool _enabled;
        private float _alpha;
        private float _scrollT;

        private LaserEmitter[] _emitters;
        private LineRenderer[] _lines;
        private Transform[] _anchors;

        private Material _wireMat;
        private Texture2D _wireTex;

        void Start()
        {
            GameObject lasersObj = GameObject.Find("Lasers");
            if (lasersObj != null) lasersRoot = lasersObj.transform;

            GameObject boxObj = GameObject.Find("electricalBox");
            if (boxObj != null) electricalBox = boxObj.transform;

            GameObject finalObj = GameObject.Find("LaserFinalCheckpoint");
            if (finalObj != null) laserFinalCheckpoint = finalObj.transform;

            laserMidCheckpoint = GameObject.Find("LaserMidCheckpoint").transform;

            if (buildOnStart)
                Build();
        }

        void OnDestroy()
        {
            if (_wireMat != null)
            {
                try { Destroy(_wireMat); } catch { }
                _wireMat = null;
            }

            if (_wireTex != null)
            {
                try { Destroy(_wireTex); } catch { }
                _wireTex = null;
            }
        }

        public void Enable(bool on)
        {
            MelonLogger.Msg($"[WVM] - Checkpoint 1 - Enable: {on}");
            _enabled = on;

            if (_lines == null || _lines.Length == 0)
                Build();

            SetLinesEnabled(on);
            MelonLogger.Msg($"[WVM] - Checkpoint 2 - Enable: {on}");

        }

        public bool killed = false;
        public void Kill()
        {
            GameObject.Find("Lasers").SetActive(false);
            killed = true;
        }

        public void Build()
        {
            if (lasersRoot == null)
            {
                MelonLogger.Warning("[WireVisionManager] lasersRoot is null.");
                return;
            }

            if (electricalBox == null)
            {
                MelonLogger.Warning("[WireVisionManager] electricalBox is null.");
                return;
            }

            if (laserFinalCheckpoint == null)
            {
                Transform found = lasersRoot.Find("LaserFinalCheckpoint");
                if (found != null)
                    laserFinalCheckpoint = found;
            }

            if (laserFinalCheckpoint == null)
            {
                MelonLogger.Warning("[WireVisionManager] LaserFinalCheckpoint not found under lasersRoot and not assigned.");
                return;
            }

            _emitters = lasersRoot.GetComponentsInChildren<LaserEmitter>(true);
            if (_emitters == null || _emitters.Length == 0)
            {
                MelonLogger.Warning("[WireVisionManager] No LaserEmitter found under lasersRoot.");
                return;
            }

            if (_wireMat == null)
                _wireMat = CreateXrayWireMaterial();

            _lines = new LineRenderer[_emitters.Length];
            _anchors = new Transform[_emitters.Length];

            int made = 0;
            int noAnchor = 0;

            for (int i = 0; i < _emitters.Length; i++)
            {
                LaserEmitter em = _emitters[i];
                if (em == null)
                    continue;

                Transform anchor = FindAnchorWithCheckpoint1(em.transform, lasersRoot);
                _anchors[i] = anchor;

                if (anchor == null)
                {
                    noAnchor++;
                    continue;
                }

                Transform existing = anchor.Find("__WireVisionLine");
                if (existing != null)
                {
                    try { Destroy(existing.gameObject); } catch { }
                }

                GameObject go = new GameObject("__WireVisionLine");
                go.transform.SetParent(anchor, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                go.layer = anchor.gameObject.layer;

                LineRenderer lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.alignment = LineAlignment.View;
                lr.textureMode = LineTextureMode.Tile;
                lr.numCapVertices = 4;
                lr.numCornerVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.allowOcclusionWhenDynamic = false;
                lr.sortingOrder = sortingOrder;
                lr.material = _wireMat;
                lr.startWidth = wireWidth;
                lr.endWidth = wireWidth;
                lr.widthMultiplier = 1f;
                lr.startColor = wireColor;
                lr.endColor = wireColor;

                _lines[i] = lr;
                made++;

                UpdateSingleWire(i);
            }

            _alpha = _enabled ? 1f : 0f;
            SetLinesEnabled(true);
            ApplyAlphaToAll(_alpha);

            MelonLogger.Msg("[WireVisionManager] Build complete. Emitters=" + _emitters.Length +
                            " LinesMade=" + made + " NoAnchor=" + noAnchor);
        }

        void Update()
        {
            if (_lines == null || _lines.Length == 0 || _emitters == null)
                return;

            float target = _enabled ? 1f : 0f;
            float step = Time.deltaTime * Mathf.Max(0.01f, fadeSpeed);
            _alpha = Mathf.MoveTowards(_alpha, target, step);

            ApplyAlphaToAll(_alpha);

            if (_wireMat != null && scrollSpeed != 0f)
            {
                _scrollT += Time.deltaTime * scrollSpeed;
                _wireMat.mainTextureOffset = new Vector2(_scrollT, 0f);
                _wireMat.mainTextureScale = new Vector2(Mathf.Max(0.01f, textureTileX), 1f);
            }

            for (int i = 0; i < _lines.Length; i++)
                UpdateSingleWire(i);

            if (!_enabled && _alpha <= 0.0001f)
                SetLinesEnabled(false);
        }

        private void SetLinesEnabled(bool on)
        {
            if (_lines == null) return;
            for (int i = 0; i < _lines.Length; i++)
            {
                LineRenderer lr = _lines[i];
                if (lr != null) lr.enabled = on;
            }
        }

        private void ApplyAlphaToAll(float a)
        {
            if (_lines == null) return;

            Color c = wireColor;
            c.a = wireColor.a * Mathf.Clamp01(a);

            for (int i = 0; i < _lines.Length; i++)
            {
                LineRenderer lr = _lines[i];
                if (lr == null) continue;

                lr.startColor = c;
                lr.endColor = c;
            }

            if (_wireMat != null)
                ApplyColorToMaterial(_wireMat, c);
        }

        private void UpdateSingleWire(int index)
        {
            if (index < 0 || index >= _emitters.Length)
                return;

            LaserEmitter em = _emitters[index];
            LineRenderer lr = _lines[index];
            Transform anchor = _anchors[index];

            if (em == null || lr == null || anchor == null)
                return;

            Transform cp1 = FindChildStartsWith(anchor, "Checkpoint1");
            if (cp1 == null)
                return;

            Transform cp2 = FindChildStartsWith(anchor, "Checkpoint2");

            Vector3 laserPos = em.transform.position;
            Vector3 cp1Pos = cp1.position;

            Vector3 p0 = new Vector3(laserPos.x, laserPos.y, cp1Pos.z);
            Vector3 p1 = cp1Pos;

            Vector3 finalPos = laserFinalCheckpoint.position;
            float boxY = electricalBox.position.y;
            Vector3 pEnd = new Vector3(finalPos.x, boxY, finalPos.z);

            if (cp2 != null)
            {
                Vector3 p2 = laserMidCheckpoint.position;

                lr.positionCount = 5;
                lr.SetPosition(0, p0);
                lr.SetPosition(1, p1);
                lr.SetPosition(2, p2);
                lr.SetPosition(3, finalPos);
                lr.SetPosition(4, pEnd);
            }
            else
            {
                lr.positionCount = 4;
                lr.SetPosition(0, p0);
                lr.SetPosition(1, p1);
                lr.SetPosition(2, finalPos);
                lr.SetPosition(3, pEnd);
            }
        }

        private Transform FindAnchorWithCheckpoint1(Transform start, Transform stopAt)
        {
            Transform t = start;
            while (t != null)
            {
                if (HasChildStartsWith(t, "Checkpoint1"))
                    return t;

                if (t == stopAt)
                    break;

                t = t.parent;
            }
            return null;
        }

        private bool HasChildStartsWith(Transform parent, string prefix)
        {
            if (parent == null) return false;

            int count = parent.childCount;
            for (int i = 0; i < count; i++)
            {
                Transform c = parent.GetChild(i);
                if (c == null) continue;

                string n = c.name;
                if (n != null && n.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private Transform FindChildStartsWith(Transform parent, string prefix)
        {
            if (parent == null) return null;

            int count = parent.childCount;
            for (int i = 0; i < count; i++)
            {
                Transform c = parent.GetChild(i);
                if (c == null) continue;

                string n = c.name;
                if (n != null && n.StartsWith(prefix, StringComparison.Ordinal))
                    return c;
            }
            return null;
        }

        private Material CreateXrayWireMaterial()
        {
            if (_wireTex == null)
                _wireTex = CreateWireTexture();

            Shader s = Shader.Find("Sprites/Default");
            if (s == null)
            {
                s = Shader.Find("Unlit/Transparent") ?? Shader.Find("Particles/Alpha Blended") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
                MelonLogger.Warning("[WireVisionManager] Sprites/Default not found. Using fallback shader: " + (s != null ? s.name : "null"));
            }

            Material m = new Material(s);
            m.mainTexture = _wireTex;

            try
            {
                m.SetInt("_ZWrite", 0);
                m.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.renderQueue = 5000;
            }
            catch { }

            ApplyColorToMaterial(m, wireColor);
            return m;
        }

        private Texture2D CreateWireTexture()
        {
            const int width = 128;
            const int height = 8;

            Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            for (int x = 0; x < width; x++)
            {
                float u = (float)x / (float)(width - 1);

                float pulseA = 0.70f + 0.30f * Mathf.Sin(u * 18.849555f);
                float pulseB = 0.80f + 0.20f * Mathf.Sin(u * 37.699111f + 0.9f);
                float pulse = pulseA * pulseB;

                float stripeMask = (Mathf.Sin(u * 50.265482f) > 0.55f) ? 1f : 0f;
                float brightness = 0.84f + stripeMask * 0.16f;

                for (int y = 0; y < height; y++)
                {
                    float v = (float)y / (float)(height - 1);
                    float d = Mathf.Abs(v - 0.5f) / 0.5f;
                    float alpha = 1f - d;
                    alpha *= alpha;
                    alpha *= 0.82f;
                    alpha *= pulse;

                    tex.SetPixel(x, y, new Color(brightness, brightness, brightness, alpha));
                }
            }

            tex.Apply(false, false);
            return tex;
        }

        private void ApplyColorToMaterial(Material m, Color c)
        {
            if (m == null) return;

            if (m.HasProperty("_Color"))
                m.SetColor("_Color", c);

            if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", c);

            if (m.HasProperty("_TintColor"))
                m.SetColor("_TintColor", c);
        }
    }
}
