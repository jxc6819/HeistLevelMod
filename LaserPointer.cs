using SG.Phoenix.Assets.Code.Interactables;
using System;
using MelonLoader;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{

    public class LaserPointer : MonoBehaviour
    {
        public LaserPointer(IntPtr ptr) : base(ptr) { }
        public LaserPointer() : base(ClassInjector.DerivedConstructorPointer<LaserPointer>())
            => ClassInjector.DerivedConstructorBody(this);

        public string LaserOriginName = "LaserOrigin";
        public Vector3 LaserLocalOffset = new Vector3(0f, 0f, 0f);
        public Vector3 LaserLocalEuler = new Vector3(270f, 0f, 0f);

        public float TriggerThreshold = 0.75f;
        public float MaxDistance = 12f;

        public float BeamWidth = 0.0022f;
        public float GlowWidth = 0.0075f;
        public Color BeamColor = new Color(1f, 0.02f, 0.01f, 1f);
        public Color GlowColor = new Color(1f, 0.03f, 0.01f, 0.16f);

        public bool UseBeamPulse = false;
        public float PulseSpeed = 9f;
        public float PulseWidthAmount = 0.04f;
        public float PulseAlphaAmount = 0.04f;

        public bool AddSmallHitDot = false;
        public float HitDotSize = 0.012f;
        public float HitDotSurfaceOffset = 0.002f;
        public float HitDotPulseAmount = 0f;

        public LayerMask HitMask = ~0;
        public QueryTriggerInteraction TriggerInteraction = QueryTriggerInteraction.Ignore;

        public bool DebugLogs = true;

        PickUp _pickUp;
        Transform _laserOrigin;

        LineRenderer _line;
        LineRenderer _glowLine;

        Material _lineMat;
        Material _glowMat;
        Material _hitDotMat;

        Texture2D _beamTex;
        Texture2D _hitDotTex;

        GameObject _hitDot;
        MeshRenderer _hitDotRenderer;

        bool _initialized;
        bool _wasHeld;
        bool _laserOn;
        bool _triggerWasPressed;
        char _handSide = 'R';

        float _seed;

        void Awake()
        {
            Init();
        }

        void Start()
        {
            Init();
        }

        public void Init()
        {
            if (_initialized) return;
            _initialized = true;

            _seed = UnityEngine.Random.value * 1000f;

            ResolvePickUp();
            ResolveLaserOrigin();
            BuildLaserVisuals();
            SetLaserVisible(false);

            if (DebugLogs)
                MelonLogger.Msg("[LaserPointer] Initialized on " + gameObject.name + " origin=" + (_laserOrigin != null ? _laserOrigin.name : "null"));
        }

        void Update()
        {
            ResolvePickUp();

            bool held = IsHeld();
            if (held && !_wasHeld)
            {
                ResolveHandSide();
                if (DebugLogs) MelonLogger.Msg("[LaserPointer] Held side=" + _handSide);
            }
            else if (!held && _wasHeld)
            {

                _triggerWasPressed = false;
                if (DebugLogs) MelonLogger.Msg("[LaserPointer] Released; laser stays " + (_laserOn ? "on" : "off"));
            }

            _wasHeld = held;

            if (!held)
                return;

            bool triggerPressed = IsTriggerPressed();

            if (triggerPressed && !_triggerWasPressed)
            {
                PlayPressSound();
                ToggleLaser();
            }

            _triggerWasPressed = triggerPressed;

            if (_laserOn)
                UpdateLaserBeam();
        }

        void LateUpdate()
        {
            if (_laserOn)
                UpdateLaserBeam();
        }

        void OnDisable()
        {
            TurnLaserOff();
            _wasHeld = false;
            _triggerWasPressed = false;
        }

        void OnDestroy()
        {
            TurnLaserOff();

            try { if (_lineMat != null) Destroy(_lineMat); } catch { }
            try { if (_glowMat != null) Destroy(_glowMat); } catch { }
            try { if (_hitDotMat != null) Destroy(_hitDotMat); } catch { }
            try { if (_beamTex != null) Destroy(_beamTex); } catch { }
            try { if (_hitDotTex != null) Destroy(_hitDotTex); } catch { }
        }

        void PlayPressSound()
        {
            try
            {
                AudioUtil.PlayAt("ButtonPress.ogg", transform.position);
            }
            catch (Exception e)
            {
                if (DebugLogs)
                    MelonLogger.Warning("[LaserPointer] ButtonPress.ogg failed: " + e);
            }
        }

        void ToggleLaser()
        {
            if (_laserOn) TurnLaserOff();
            else TurnLaserOn();
        }

        void TurnLaserOn()
        {
            if (_laserOn) return;
            _laserOn = true;
            SetLaserVisible(true);
            UpdateLaserBeam();
        }

        void TurnLaserOff()
        {
            if (!_laserOn) return;
            _laserOn = false;
            SetLaserVisible(false);
        }

        void UpdateLaserBeam()
        {
            if (_laserOrigin == null || _line == null) return;

            Vector3 start = _laserOrigin.position;
            Vector3 dir = _laserOrigin.forward;
            Vector3 end = start + dir * MaxDistance;

            RaycastHit hit;
            bool hasHit = false;
            try
            {
                hasHit = Physics.Raycast(start, dir, out hit, MaxDistance, HitMask, TriggerInteraction);
            }
            catch
            {
                hasHit = false;
                hit = default;
            }

            if (hasHit && hit.collider != null)
                end = hit.point;

            float widthMul = 1f;
            float alphaMul = 1f;

            if (UseBeamPulse)
            {
                float s = Mathf.Sin((Time.time + _seed) * PulseSpeed);
                widthMul = 1f + s * PulseWidthAmount;

                float n = Mathf.PerlinNoise((_seed + Time.time) * 6.5f, 0.37f);
                alphaMul = 1f - PulseAlphaAmount + (n * PulseAlphaAmount * 2f);
            }

            Color core = BeamColor;
            core.a = Mathf.Clamp01(BeamColor.a * alphaMul);

            Color glow = GlowColor;
            glow.a = Mathf.Clamp01(GlowColor.a * alphaMul);

            SetLinePositions(_line, start, end);
            _line.startWidth = Mathf.Max(0.0005f, BeamWidth * widthMul);
            _line.endWidth = Mathf.Max(0.0005f, BeamWidth * 0.70f * widthMul);
            _line.startColor = core;
            _line.endColor = new Color(core.r, core.g, core.b, core.a * 0.72f);

            if (_glowLine != null)
            {
                SetLinePositions(_glowLine, start, end);
                _glowLine.startWidth = Mathf.Max(0.001f, GlowWidth * widthMul);
                _glowLine.endWidth = Mathf.Max(0.001f, GlowWidth * 0.70f * widthMul);
                _glowLine.startColor = glow;
                _glowLine.endColor = new Color(glow.r, glow.g, glow.b, glow.a * 0.35f);
            }

            if (_lineMat != null)
            {
                try { _lineMat.mainTextureOffset = Vector2.zero; } catch { }
            }
            if (_glowMat != null)
            {
                try { _glowMat.mainTextureOffset = Vector2.zero; } catch { }
            }

            if (AddSmallHitDot && _hitDot != null)
            {
                _hitDot.SetActive(hasHit && hit.collider != null);
                if (hasHit && hit.collider != null)
                {
                    Vector3 normal = hit.normal;
                    if (normal.sqrMagnitude < 0.0001f) normal = -dir;
                    normal.Normalize();

                    float dotMul = 1f;
                    if (UseBeamPulse && HitDotPulseAmount > 0f)
                        dotMul = 1f + Mathf.Sin((Time.time + _seed) * PulseSpeed * 1.35f) * HitDotPulseAmount;

                    _hitDot.transform.position = hit.point + normal * HitDotSurfaceOffset;
                    _hitDot.transform.rotation = Quaternion.LookRotation(normal, Vector3.up);
                    _hitDot.transform.localScale = new Vector3(HitDotSize * dotMul, HitDotSize * dotMul, HitDotSize * dotMul);
                }
            }
        }

        void SetLinePositions(LineRenderer lr, Vector3 start, Vector3 end)
        {
            if (lr == null) return;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
        }

        void SetLaserVisible(bool visible)
        {
            if (_line != null)
                _line.enabled = visible;

            if (_glowLine != null)
                _glowLine.enabled = visible;

            if (_hitDot != null)
                _hitDot.SetActive(visible && AddSmallHitDot);
        }

        void ResolvePickUp()
        {
            if (_pickUp != null) return;
            _pickUp = GetComponent<PickUp>();
            if (_pickUp == null) _pickUp = GetComponentInParent<PickUp>();
        }

        bool IsHeld()
        {
            try { return _pickUp != null && _pickUp.isHeld; }
            catch { return false; }
        }

        void ResolveHandSide()
        {
            try
            {
                if (_pickUp != null && _pickUp.heldHand != null && _pickUp.heldHand.gameObject != null)
                {
                    string heldName = _pickUp.heldHand.gameObject.name.ToLower();
                    _handSide = heldName.Contains("left") ? 'L' : 'R';
                    return;
                }
            }
            catch { }

            _handSide = 'R';
        }

        bool IsTriggerPressed()
        {
            try
            {
                OVRInput.Controller controller = (_handSide == 'R') ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch;
                return OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controller) > TriggerThreshold;
            }
            catch { return false; }
        }

        void ResolveLaserOrigin()
        {
            if (_laserOrigin != null) return;

            Transform found = FindChildExact(transform, LaserOriginName);
            if (found != null)
            {
                _laserOrigin = found;
                return;
            }

            GameObject go = new GameObject(LaserOriginName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = LaserLocalOffset;
            go.transform.localRotation = Quaternion.Euler(LaserLocalEuler);
            go.transform.localScale = Vector3.one;
            _laserOrigin = go.transform;
        }

        Transform FindChildExact(Transform root, string exactName)
        {
            if (root == null || string.IsNullOrEmpty(exactName)) return null;

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t != null && t.name == exactName)
                    return t;
            }

            return null;
        }

        void BuildLaserVisuals()
        {
            if (_beamTex == null)
                _beamTex = BuildBeamTexture(64, 8);

            if (_line == null)
            {
                _line = MakeLineRenderer("LaserPointer_Beam_Core", BeamWidth, BeamColor, 120);
                _lineMat = CreateLaserMaterial(BeamColor, _beamTex);
                if (_lineMat != null)
                    _line.material = _lineMat;
            }

            if (_glowLine == null)
            {
                _glowLine = MakeLineRenderer("LaserPointer_Beam_Glow", GlowWidth, GlowColor, 119);
                _glowMat = CreateLaserMaterial(GlowColor, _beamTex);
                if (_glowMat != null)
                    _glowLine.material = _glowMat;
            }

            if (AddSmallHitDot && _hitDot == null)
            {
                if (_hitDotTex == null)
                    _hitDotTex = BuildSoftDotTexture(64);

                _hitDot = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _hitDot.name = "LaserPointer_HitDot";
                _hitDot.transform.SetParent(transform, true);

                Collider col = _hitDot.GetComponent<Collider>();
                if (col != null) Destroy(col);

                _hitDotRenderer = _hitDot.GetComponent<MeshRenderer>();
                if (_hitDotRenderer != null)
                {
                    _hitDotRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    _hitDotRenderer.receiveShadows = false;
                    _hitDotRenderer.sortingOrder = 121;

                    _hitDotMat = CreateHitDotMaterial(BeamColor, _hitDotTex);
                    if (_hitDotMat != null)
                        _hitDotRenderer.material = _hitDotMat;
                }
            }
        }

        LineRenderer MakeLineRenderer(string name, float width, Color color, int sortingOrder)
        {
            GameObject lineGo = new GameObject(name);
            lineGo.transform.SetParent(transform, false);
            lineGo.transform.localPosition = Vector3.zero;
            lineGo.transform.localRotation = Quaternion.identity;
            lineGo.transform.localScale = Vector3.one;

            LineRenderer lr = lineGo.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.alignment = LineAlignment.View;
            lr.textureMode = LineTextureMode.Tile;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 2;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.startColor = color;
            lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sortingOrder = sortingOrder;

            return lr;
        }

        Material CreateLaserMaterial(Color color, Texture2D tex)
        {
            Shader s =
                Shader.Find("Phoenix/SH_Shared_DefaultUnlit_TransparentAdditive_VertexColor") ??
                Shader.Find("Phoenix/SH_Shared_DefaultUnlit_TransparentAdditive_VertexColor_01") ??
                Shader.Find("FX/FX_Additive_UVPan_Shader") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Unlit/Transparent") ??
                Shader.Find("Standard");

            if (s == null) return null;

            Material mat = new Material(s);
            mat.renderQueue = 3990;

            try
            {
                if (tex != null)
                {
                    mat.mainTexture = tex;
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                }

                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            }
            catch { }

            return mat;
        }

        Material CreateHitDotMaterial(Color color, Texture2D tex)
        {
            Shader s =
                Shader.Find("Sprites/Default") ??
                Shader.Find("Unlit/Transparent") ??
                Shader.Find("Unlit/Texture") ??
                Shader.Find("Standard");

            if (s == null) return null;

            Material mat = new Material(s);
            mat.renderQueue = 3995;

            try
            {
                if (tex != null)
                {
                    mat.mainTexture = tex;
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                }

                Color c = color;
                c.a = Mathf.Clamp01(c.a * 0.65f);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            }
            catch { }

            return mat;
        }

        Texture2D BuildBeamTexture(int w, int h)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 0;

            Color32[] px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float fy = (h <= 1) ? 0.5f : y / (float)(h - 1);
                float centerDist = Mathf.Abs(fy - 0.5f) * 2f;
                float across = Mathf.Clamp01(1f - centerDist);
                across = across * across;

                for (int x = 0; x < w; x++)
                {
                    float fx = x / (float)Mathf.Max(1, w - 1);
                    float streak = 0.84f + 0.10f * Mathf.Sin(fx * 6.283185307f * 3f) + 0.06f * Mathf.Sin(fx * 6.283185307f * 11f);
                    byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(255f * across * streak), 0, 255);
                    px[y * w + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        Texture2D BuildSoftDotTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 0;

            Color32[] px = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            float maxR = Mathf.Max(1f, center);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / maxR;
                    float dy = (y - center) / maxR;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    float core = Mathf.Clamp01(1f - r);
                    float glow = core * core;
                    float alpha = Mathf.Clamp01(glow * 1.35f);

                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }
    }
}
