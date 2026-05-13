using System;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class ScreenDriver : MonoBehaviour
    {
        public ScreenDriver(IntPtr p) : base(p) { }
        public ScreenDriver() : base(ClassInjector.DerivedConstructorPointer<ScreenDriver>()) =>
            ClassInjector.DerivedConstructorBody(this);

        public enum ScreenFace { Forward, Back, Left, Right, Up, Down }

        public bool StartEnabled = true;
        public ScreenFace Face = ScreenFace.Back;

        public int Password = 1234;
        public bool RandomizePasswordOnStart = true;

        public float Fill = 1.6f;
        public float Bezel = 0.04f;
        public float ExtraSurfaceOffset = 0.0035f;
        public bool UseAutoSurfaceOffset = true;

        public bool DoubleSided = false;
        public bool AutoSideVisibility = true;

        public float TopLabelY = 0.9f;
        public float MidLabelY = 0.34f;
        public float CodeY = -0.09f;
        public float TextSafeY = 0.9f;

        public float GrainStrength = 0.14f;
        public float LineStrength = 0.10f;

        public float NoiseUpdateHz = 10f;
        public float GrainScrollSpeedX = 0.09f;
        public float GrainScrollSpeedY = 0.06f;
        public float LineScrollSpeedY = 0.030f;
        public float LinePulse = 0.20f;
        public float FlickerAmount = 0.05f;

        public float Darken = 0.10f;

        public Color ScreenTint = new Color(0.08f, 0.16f, 0.22f, 1f);

        public Color TextTint = new Color(0.95f, 0.98f, 1.00f, 1f);
        public bool SpaceDigits = true;

        public string TopLabel = "SECURITY TERMINAL";
        public string MidLabel = "PASSWORD";

        GameObject _root;
        MeshRenderer _bgR_F, _grainR_F, _linesR_F, _flickerR_F;

        Material _bgMat;
        Material _grainMat;
        Material _linesMat;
        Material _flickerMat;

        Texture2D _bgBaseTex;
        Texture2D _bgTex;
        Texture2D _noiseTex;
        Texture2D _linesTex;

        int _texW = 512;
        int _texH = 256;

        float _noiseTimer;
        bool _enabled;

        Vector2 _grainUV;
        Vector2 _linesUV;

        Camera _cam;
        Vector3 _faceNormalLocal = Vector3.forward;

        const float Z_BG = 0.0000f;
        const float Z_GRAIN = 0.00035f;
        const float Z_LINES = 0.00055f;

        string _lastBakedTop = "";
        string _lastBakedMid = "";
        string _lastBakedCode = "";
        bool _dirtyBake = true;

        public void Enable() => SetEnabled(true);
        public void Disable() => SetEnabled(false);

        public void SetPassword(int v0to9999)
        {
            Password = Mathf.Clamp(v0to9999, 0, 9999);
            UpdatePasswordText();
        }

        public string GetPasswordString() => Mathf.Clamp(Password, 0, 9999).ToString("0000");

        public void SetEnabled(bool on)
        {
            _enabled = on;
            if (_root) _root.SetActive(on);

            if (on)
            {
                _noiseTimer = 0f;
                _grainUV = Vector2.zero;
                _linesUV = Vector2.zero;

                ApplyStrengths();
                UpdatePasswordText();
                FitToFace();
                SetFrontVisible(true);
            }
        }

        void Awake()
        {
            Build();
            SetEnabled(StartEnabled);
        }

        void Start()
        {
            if (RandomizePasswordOnStart)
            {
                Password = UnityEngine.Random.Range(0, 10000);
                UpdatePasswordText();
            }

            FitToFace();
            SetFrontVisible(true);
            GameObject.Find("KeypadStandard").GetComponent<Keypad>().KeypadCombo = Password.ToString("D4");
        }

        void LateUpdate()
        {
            if (!_enabled || !_root) return;

            FitToFace();
            SetFrontVisible(true);

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _grainUV += new Vector2(GrainScrollSpeedX * dt, GrainScrollSpeedY * dt);
            _linesUV += new Vector2(0f, LineScrollSpeedY * dt);

            SafeSetTexOffset(_grainMat, "_MainTex", _grainUV);
            SafeSetTexOffset(_linesMat, "_MainTex", _linesUV);

            float hz = Mathf.Max(1f, NoiseUpdateHz);
            _noiseTimer += dt;
            if (_noiseTimer >= (1f / hz))
            {
                _noiseTimer = 0f;

                Vector2 j = new Vector2(
                    (UnityEngine.Random.value - 0.5f) * 0.015f,
                    (UnityEngine.Random.value - 0.5f) * 0.015f
                );
                _grainUV += j;

                if (_linesMat != null && _linesMat.HasProperty("_Color"))
                {
                    float roll = Mathf.PerlinNoise(0.1f, Time.time * 1.7f);
                    float pulse = Mathf.Clamp01(1f + LinePulse * (roll - 0.5f) * 2f);
                    float a = Mathf.Clamp01(LineStrength * pulse);
                    _linesMat.SetColor("_Color", new Color(1f, 1f, 1f, a));
                }

                if (_flickerMat != null && _flickerMat.HasProperty("_Color"))
                {
                    float n2 = Mathf.PerlinNoise(0.15f, Time.time * 1.35f);
                    float breathe = Mathf.Lerp(0.00f, 0.18f, 1f - n2);
                    float flick = Mathf.PerlinNoise(Time.time * 8.1f, 0.55f);
                    float fa = breathe + FlickerAmount * (flick - 0.5f) * 0.4f;
                    fa = Mathf.Clamp01(fa);
                    _flickerMat.SetColor("_Color", new Color(0f, 0f, 0f, fa));
                }
            }

            if (_dirtyBake)
                BakeTextIntoBG(force: false);
        }

        void OnDestroy()
        {
            try { if (_root) Destroy(_root); } catch { }
            try { if (_bgMat) Destroy(_bgMat); } catch { }
            try { if (_grainMat) Destroy(_grainMat); } catch { }
            try { if (_linesMat) Destroy(_linesMat); } catch { }
            try { if (_flickerMat) Destroy(_flickerMat); } catch { }
            try { if (_bgBaseTex) Destroy(_bgBaseTex); } catch { }
            try { if (_bgTex) Destroy(_bgTex); } catch { }
            try { if (_noiseTex) Destroy(_noiseTex); } catch { }
            try { if (_linesTex) Destroy(_linesTex); } catch { }
        }

        void Build()
        {
            if (_root) return;

            _root = new GameObject("ScreenDriver_Root");
            _root.transform.SetParent(transform, false);
            _root.transform.localScale = Vector3.one;

            Shader bgSh = Shader.Find("Unlit/Texture");
            if (!bgSh) bgSh = Shader.Find("Unlit/Color");

            Shader alphaPan = Shader.Find("FX/FX_Alpha_UVPan_Shader");
            if (!alphaPan) alphaPan = Shader.Find("FX/FX_Additive_UVPan_Shader");
            if (!alphaPan) alphaPan = Shader.Find("Unlit/Transparent");
            if (!alphaPan) alphaPan = Shader.Find("Sprites/Default");

            _bgBaseTex = BuildBackgroundTex(_texW, _texH, ScreenTint, Darken);
            _bgTex = DuplicateTex(_bgBaseTex);
            _noiseTex = BuildNoiseTex(128, 128);
            _linesTex = BuildScanlineTex(256, 256);

            _bgMat = new Material(bgSh) { name = "Screen_BG_Mat" };
            if (_bgMat.HasProperty("_MainTex")) _bgMat.SetTexture("_MainTex", _bgTex);
            _bgMat.renderQueue = 2000;

            if (_bgMat.HasProperty("_Color")) _bgMat.SetColor("_Color", Color.white);

            _grainMat = new Material(alphaPan) { name = "Screen_Grain_Mat" };
            if (_grainMat.HasProperty("_MainTex")) _grainMat.SetTexture("_MainTex", _noiseTex);
            _grainMat.renderQueue = 3990;

            _linesMat = new Material(alphaPan) { name = "Screen_Lines_Mat" };
            if (_linesMat.HasProperty("_MainTex")) _linesMat.SetTexture("_MainTex", _linesTex);
            _linesMat.renderQueue = 3991;

            ApplyStrengths();

            _bgR_F = MakeQuad("BG_Front", _bgMat, +Z_BG);
            _grainR_F = MakeQuad("Grain_Front", _grainMat, +Z_GRAIN);
            _linesR_F = MakeQuad("Lines_Front", _linesMat, +Z_LINES);

            _flickerMat = new Material(alphaPan) { name = "Screen_Flicker_Mat" };
            _flickerMat.renderQueue = 3993;
            if (_flickerMat.HasProperty("_Color")) _flickerMat.SetColor("_Color", new Color(0f, 0f, 0f, 0f));
            _flickerR_F = MakeQuad("Flicker_Front", _flickerMat, +0.00075f);

            UpdatePasswordText();
            BakeTextIntoBG(force: true);

            FitToFace();
            SetFrontVisible(true);
        }

        MeshRenderer MakeQuad(string name, Material mat, float localZ)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
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

        void FitToFace()
        {
            if (!_root) return;

            Vector3 meshSize = Vector3.one;
            var mf = GetComponent<MeshFilter>();
            if (mf == null && transform.parent) mf = transform.parent.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) meshSize = mf.sharedMesh.bounds.size;

            float sx = Mathf.Abs(transform.localScale.x);
            float sy = Mathf.Abs(transform.localScale.y);
            float sz = Mathf.Abs(transform.localScale.z);

            float halfX = (meshSize.x * sx) * 0.5f;
            float halfY = (meshSize.y * sy) * 0.5f;
            float halfZ = (meshSize.z * sz) * 0.5f;

            float axisThickness = (meshSize.z * sz);
            if (Face == ScreenFace.Left || Face == ScreenFace.Right) axisThickness = (meshSize.x * sx);
            if (Face == ScreenFace.Up || Face == ScreenFace.Down) axisThickness = (meshSize.y * sy);

            float autoPush = UseAutoSurfaceOffset ? Mathf.Max(0.0025f, axisThickness * 0.35f) : 0f;
            float push = autoPush + Mathf.Max(0f, ExtraSurfaceOffset);

            float w = 1f, h = 1f;
            Vector3 localPos = Vector3.zero;
            Quaternion localRot = Quaternion.identity;

            switch (Face)
            {
                default:
                case ScreenFace.Forward:
                    _faceNormalLocal = Vector3.forward;
                    w = (meshSize.x * sx);
                    h = (meshSize.y * sy);
                    localPos = new Vector3(0f, 0f, +halfZ + push);
                    localRot = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                    break;

                case ScreenFace.Back:
                    _faceNormalLocal = Vector3.back;
                    w = (meshSize.x * sx);
                    h = (meshSize.y * sy);
                    localPos = new Vector3(0f, 0f, -halfZ - push);
                    localRot = Quaternion.LookRotation(Vector3.back, Vector3.up);
                    break;

                case ScreenFace.Right:
                    _faceNormalLocal = Vector3.right;
                    w = (meshSize.z * sz);
                    h = (meshSize.y * sy);
                    localPos = new Vector3(+halfX + push, 0f, 0f);
                    localRot = Quaternion.LookRotation(Vector3.right, Vector3.up);
                    break;

                case ScreenFace.Left:
                    _faceNormalLocal = Vector3.left;
                    w = (meshSize.z * sz);
                    h = (meshSize.y * sy);
                    localPos = new Vector3(-halfX - push, 0f, 0f);
                    localRot = Quaternion.LookRotation(Vector3.left, Vector3.up);
                    break;

                case ScreenFace.Up:
                    _faceNormalLocal = Vector3.up;
                    w = (meshSize.x * sx);
                    h = (meshSize.z * sz);
                    localPos = new Vector3(0f, +halfY + push, 0f);
                    localRot = Quaternion.LookRotation(Vector3.up, Vector3.forward);
                    break;

                case ScreenFace.Down:
                    _faceNormalLocal = Vector3.down;
                    w = (meshSize.x * sx);
                    h = (meshSize.z * sz);
                    localPos = new Vector3(0f, -halfY - push, 0f);
                    localRot = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                    break;
            }

            _root.transform.localPosition = localPos;
            _root.transform.localRotation = localRot;

            float f = Mathf.Clamp(Fill - Bezel, 0.10f, 1.60f);
            float qw = Mathf.Max(0.001f, w * f);
            float qh = Mathf.Max(0.001f, h * f);

            if (_bgR_F) _bgR_F.transform.localScale = new Vector3(qw, qh, 1f);
            if (_grainR_F) _grainR_F.transform.localScale = new Vector3(qw, qh, 1f);
            if (_linesR_F) _linesR_F.transform.localScale = new Vector3(qw, qh, 1f);
            if (_flickerR_F) _flickerR_F.transform.localScale = new Vector3(qw, qh, 1f);
        }

        void SetFrontVisible(bool on)
        {
            if (_bgR_F) _bgR_F.enabled = on;
            if (_grainR_F) _grainR_F.enabled = on;
            if (_linesR_F) _linesR_F.enabled = on;
            if (_flickerR_F) _flickerR_F.enabled = on;
        }

        void UpdatePasswordText()
        {
            string raw = GetPasswordString();
            string code = SpaceDigits
                ? (raw[0] + " " + raw[1] + " " + raw[2] + " " + raw[3])
                : raw;

            if (TopLabel != _lastBakedTop || MidLabel != _lastBakedMid || code != _lastBakedCode)
                _dirtyBake = true;
        }

        void BakeTextIntoBG(bool force)
        {
            if (!_dirtyBake && !force) return;
            if (_bgBaseTex == null || _bgTex == null) return;

            string raw = GetPasswordString();
            string code = SpaceDigits
                ? (raw[0] + " " + raw[1] + " " + raw[2] + " " + raw[3])
                : raw;

            _lastBakedTop = TopLabel ?? "";
            _lastBakedMid = MidLabel ?? "";
            _lastBakedCode = code ?? "";
            _dirtyBake = false;

            try
            {
                Color32[] basePx = _bgBaseTex.GetPixels32();
                Color32[] px = _bgTex.GetPixels32();

                int len = Mathf.Min(basePx.Length, px.Length);
                for (int i = 0; i < len; i++)
                    px[i] = basePx[i];

                int safeTop = Mathf.RoundToInt(_texH * 0.10f);
                int safeBot = Mathf.RoundToInt(_texH * 0.10f);
                int safeH = _texH - safeTop - safeBot;

                int yTop = safeTop + Mathf.RoundToInt((1f - Mathf.Clamp01((TopLabelY * 0.5f + 0.5f) * TextSafeY)) * safeH);
                int yMid = safeTop + Mathf.RoundToInt((1f - Mathf.Clamp01((MidLabelY * 0.5f + 0.5f) * TextSafeY)) * safeH);
                int yCode = safeTop + Mathf.RoundToInt((1f - Mathf.Clamp01((CodeY * 0.5f + 0.5f) * TextSafeY)) * safeH);

                DrawStringCentered(px, _texW, _texH, _lastBakedTop, yTop, 2, TextTint);
                DrawStringCentered(px, _texW, _texH, _lastBakedMid, yMid, 3, TextTint);
                DrawStringCentered(px, _texW, _texH, _lastBakedCode, yCode, 5, TextTint);

                _bgTex.SetPixels32(px);
                _bgTex.Apply(false, false);

                if (_bgMat != null && _bgMat.HasProperty("_MainTex"))
                    _bgMat.SetTexture("_MainTex", _bgTex);
            }
            catch (Exception e)
            {
                Debug.Log("[ScreenDriver] BakeTextIntoBG failed: " + e);
            }
        }

        void ApplyStrengths()
        {
            if (_grainMat != null && _grainMat.HasProperty("_Color"))
                _grainMat.SetColor("_Color", new Color(1f, 1f, 1f, Mathf.Clamp01(GrainStrength)));

            if (_linesMat != null && _linesMat.HasProperty("_Color"))
                _linesMat.SetColor("_Color", new Color(1f, 1f, 1f, Mathf.Clamp01(LineStrength)));

            if (_bgMat != null && _bgMat.HasProperty("_Color"))
                _bgMat.SetColor("_Color", Color.white);
        }

        static void SafeSetTexOffset(Material m, string prop, Vector2 offset)
        {
            if (m == null) return;
            if (m.HasProperty(prop)) m.SetTextureOffset(prop, offset);
        }

        static bool TryGetGlyph(char c, out byte[] rows)
        {
            if (c >= 'a' && c <= 'z') c = (char)(c - 32);

            switch (c)
            {
                case ' ': rows = GLYPH_SPACE; return true;
                case '0': rows = GLYPH_0; return true;
                case '1': rows = GLYPH_1; return true;
                case '2': rows = GLYPH_2; return true;
                case '3': rows = GLYPH_3; return true;
                case '4': rows = GLYPH_4; return true;
                case '5': rows = GLYPH_5; return true;
                case '6': rows = GLYPH_6; return true;
                case '7': rows = GLYPH_7; return true;
                case '8': rows = GLYPH_8; return true;
                case '9': rows = GLYPH_9; return true;
                case 'A': rows = GLYPH_A; return true;
                case 'C': rows = GLYPH_C; return true;
                case 'D': rows = GLYPH_D; return true;
                case 'E': rows = GLYPH_E; return true;
                case 'I': rows = GLYPH_I; return true;
                case 'L': rows = GLYPH_L; return true;
                case 'M': rows = GLYPH_M; return true;
                case 'N': rows = GLYPH_N; return true;
                case 'O': rows = GLYPH_O; return true;
                case 'P': rows = GLYPH_P; return true;
                case 'R': rows = GLYPH_R; return true;
                case 'S': rows = GLYPH_S; return true;
                case 'T': rows = GLYPH_T; return true;
                case 'U': rows = GLYPH_U; return true;
                case 'W': rows = GLYPH_W; return true;
                case 'Y': rows = GLYPH_Y; return true;
                default: rows = GLYPH_SPACE; return true;
            }
        }

        static int MeasureStringWidthPx(string s, int scale)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            return s.Length * (5 * scale) + Mathf.Max(0, s.Length - 1) * (1 * scale);
        }

        static void DrawStringCentered(Color32[] px, int w, int h, string s, int yTopPx, int scale, Color col)
        {
            if (string.IsNullOrEmpty(s)) return;
            int totalW = MeasureStringWidthPx(s, scale);
            int x0 = (w - totalW) / 2;
            DrawString(px, w, h, s, x0, yTopPx, scale, col);
        }

        static void DrawString(Color32[] px, int w, int h, string s, int xLeftPx, int yTopPx, int scale, Color col)
        {
            if (string.IsNullOrEmpty(s)) return;

            Color32 c32 = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(col.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(col.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(col.b * 255f), 0, 255),
                255
            );

            int x = xLeftPx;
            for (int i = 0; i < s.Length; i++)
            {
                byte[] rows;
                TryGetGlyph(s[i], out rows);
                DrawGlyph(px, w, h, rows, x, yTopPx, scale, c32);
                x += (5 + 1) * scale;
            }
        }

        static void DrawGlyph(Color32[] px, int w, int h, byte[] rows, int xLeftPx, int yTopPx, int scale, Color32 col)
        {
            for (int ry = 0; ry < 7; ry++)
            {
                byte bits = rows[ry];
                for (int rx = 0; rx < 5; rx++)
                {
                    bool on = (bits & (1 << (4 - rx))) != 0;
                    if (!on) continue;

                    int pxX0 = xLeftPx + rx * scale;
                    int pxY0_TOP = yTopPx + ry * scale;

                    for (int sy = 0; sy < scale; sy++)
                    {
                        for (int sx = 0; sx < scale; sx++)
                        {
                            int X = pxX0 + sx;
                            int Y_TOP = pxY0_TOP + sy;

                            if ((uint)X >= (uint)w) continue;
                            if ((uint)Y_TOP >= (uint)h) continue;

                            int Y = (h - 1) - Y_TOP;
                            int idx = Y * w + X;
                            if ((uint)idx >= (uint)px.Length) continue;

                            px[idx] = col;
                        }
                    }
                }
            }
        }

        static Texture2D DuplicateTex(Texture2D src)
        {
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, true)
            {
                wrapMode = src.wrapMode,
                filterMode = src.filterMode,
                anisoLevel = 0
            };
            tex.SetPixels32(src.GetPixels32());
            tex.Apply(false, false);
            return tex;
        }

        static Texture2D BuildNoiseTex(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };
            var px = new Color32[w * h];
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
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point,
                anisoLevel = 0
            };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                bool darkRow = (y % 4) == 0;
                float baseA = darkRow ? 0.22f : 0.07f;
                if ((y % 49) == 0) baseA = 0.28f;
                for (int x = 0; x < w; x++)
                {
                    float jitter = (Mathf.PerlinNoise(x * 0.06f, y * 0.09f) - 0.5f) * 0.06f;
                    float a = Mathf.Clamp01(baseA + jitter);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        static Texture2D BuildBackgroundTex(int w, int h, Color screenTint, float darken)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };
            var px = new Color32[w * h];
            float tintScale = Mathf.Clamp01(1f - darken);
            for (int y = 0; y < h; y++)
            {
                float fy = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float fx = x / (float)(w - 1);
                    float dx = (fx - 0.5f) * 2f;
                    float dy = (fy - 0.5f) * 2f;
                    float v = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    v = Mathf.SmoothStep(0f, 1f, v);
                    float g = Mathf.Lerp(0.55f, 0.78f, 1f - fy);
                    float micro = Mathf.PerlinNoise(fx * 36f, fy * 20f);
                    micro = Mathf.Lerp(0.94f, 1.06f, micro);
                    float lum = g * Mathf.Lerp(0.70f, 1.0f, v) * micro;
                    float r = Mathf.Clamp01(lum * 0.55f * screenTint.r * tintScale);
                    float gg = Mathf.Clamp01(lum * 0.80f * screenTint.g * tintScale);
                    float b = Mathf.Clamp01(lum * 1.00f * screenTint.b * tintScale);
                    px[y * w + x] = new Color32((byte)(r * 255f), (byte)(gg * 255f), (byte)(b * 255f), 255);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        static readonly byte[] GLYPH_SPACE = { 0, 0, 0, 0, 0, 0, 0 };

        static readonly byte[] GLYPH_0 = { 0b01110, 0b10001, 0b10011, 0b10101, 0b11001, 0b10001, 0b01110 };
        static readonly byte[] GLYPH_1 = { 0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110 };
        static readonly byte[] GLYPH_2 = { 0b01110, 0b10001, 0b00001, 0b00010, 0b00100, 0b01000, 0b11111 };
        static readonly byte[] GLYPH_3 = { 0b11110, 0b00001, 0b00001, 0b01110, 0b00001, 0b00001, 0b11110 };
        static readonly byte[] GLYPH_4 = { 0b00010, 0b00110, 0b01010, 0b10010, 0b11111, 0b00010, 0b00010 };
        static readonly byte[] GLYPH_5 = { 0b11111, 0b10000, 0b10000, 0b11110, 0b00001, 0b00001, 0b11110 };
        static readonly byte[] GLYPH_6 = { 0b00111, 0b01000, 0b10000, 0b11110, 0b10001, 0b10001, 0b01110 };
        static readonly byte[] GLYPH_7 = { 0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b01000, 0b01000 };
        static readonly byte[] GLYPH_8 = { 0b01110, 0b10001, 0b10001, 0b01110, 0b10001, 0b10001, 0b01110 };
        static readonly byte[] GLYPH_9 = { 0b01110, 0b10001, 0b10001, 0b01111, 0b00001, 0b00010, 0b11100 };

        static readonly byte[] GLYPH_A = { 0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001 };
        static readonly byte[] GLYPH_C = { 0b01110, 0b10001, 0b10000, 0b10000, 0b10000, 0b10001, 0b01110 };
        static readonly byte[] GLYPH_D = { 0b11110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b11110 };
        static readonly byte[] GLYPH_E = { 0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111 };
        static readonly byte[] GLYPH_I = { 0b01110, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110 };
        static readonly byte[] GLYPH_L = { 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b11111 };
        static readonly byte[] GLYPH_M = { 0b10001, 0b11011, 0b10101, 0b10101, 0b10001, 0b10001, 0b10001 };
        static readonly byte[] GLYPH_N = { 0b10001, 0b11001, 0b10101, 0b10011, 0b10001, 0b10001, 0b10001 };
        static readonly byte[] GLYPH_O = { 0b01110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110 };
        static readonly byte[] GLYPH_P = { 0b11110, 0b10001, 0b10001, 0b11110, 0b10000, 0b10000, 0b10000 };
        static readonly byte[] GLYPH_R = { 0b11110, 0b10001, 0b10001, 0b11110, 0b10100, 0b10010, 0b10001 };
        static readonly byte[] GLYPH_S = { 0b01111, 0b10000, 0b10000, 0b01110, 0b00001, 0b00001, 0b11110 };
        static readonly byte[] GLYPH_T = { 0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100 };
        static readonly byte[] GLYPH_U = { 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110 };
        static readonly byte[] GLYPH_W = { 0b10001, 0b10001, 0b10001, 0b10101, 0b10101, 0b10101, 0b01010 };
        static readonly byte[] GLYPH_Y = { 0b10001, 0b10001, 0b01010, 0b00100, 0b00100, 0b00100, 0b00100 };
    }
}
