using System;
using MelonLoader;
using UnhollowerRuntimeLib;
using UnhollowerBaseLib.Attributes;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace IEYTD_Mod2Code
{
    public class Keypad : MonoBehaviour
    {
        public Keypad(IntPtr p) : base(p) { }
        public Keypad() : base(ClassInjector.DerivedConstructorPointer<Keypad>()) =>
            ClassInjector.DerivedConstructorBody(this);

        public UnityEvent OnAccessGranted;
        public UnityEvent OnAccessDenied;

        public string KeypadCombo = "5567";
        public string AccessPendingText1 = "Keycard Required";
        public string AccessPendingText2 = "Code Required";
        public string AccessGrantedText = "Granted";
        public string AccessDeniedText = "Denied";

        public float DisplayResultTime = 1f;
        public float ScreenIntensity = 2.2f;

        public Color ScreenPendingColor = new Color(1f, 0.75f, 0.15f, 1f);
        public Color ScreenNormalColor = new Color(0.2f, 0.8f, 1f, 1f);
        public Color ScreenDeniedColor = new Color(1f, 0.1f, 0.1f, 1f);
        public Color ScreenGrantedColor = new Color(0.1f, 1f, 0.1f, 1f);

        public AudioClip ButtonClickedSfx;
        public AudioClip AccessDeniedSfx;
        public AudioClip AccessGrantedSfx;

        public MeshRenderer panelMesh;
        public TextMeshProUGUI keypadDisplayText;
        public TMP_FontAsset OriginalDisplayFont;
        public Material OriginalDisplayMaterial;
        public AudioSource audioSource;

        private Image _screenBg;

        private string _input = "";
        private bool _showingResult = false;
        private float _resultEndTime = 0f;

        bool waitingKeycard = false;
        bool elevatorTriggered = false;

        public bool CodeEntered = false;
        private void Awake()
        {

            if (OnAccessGranted == null) OnAccessGranted = new UnityEvent();
            if (OnAccessDenied == null) OnAccessDenied = new UnityEvent();

            TryAutoWireRefs();

            ApplySchellTmpLook();

            EnsureScreenBackground();

            FixDisplayTextLayout();

            ClearInput();
            SetScreenColor(ScreenNormalColor);
        }

        bool denied = false;

        private void Update()
        {
            if (_showingResult && Time.time >= _resultEndTime && denied && deniedCounter < 3)
            {
                _showingResult = false;
                ClearInput();
                SetScreenColor(ScreenNormalColor);
                denied = false;
            }

            if (waitingKeycard)
            {
                if (Keycard.Swiped) KeycardSwiped();
            }
        }

        public void PressDigit(string digit)
        {
            if (_showingResult) return;
            if (string.IsNullOrEmpty(digit)) return;

            if (_input.Length >= 9) return;

            _input += digit;
            UpdateDisplayText(_input);
            Play(ButtonClickedSfx);
        }

        public void PressClear()
        {
            if (_showingResult) return;
            ClearInput();
            SetScreenColor(ScreenNormalColor);
            Play(ButtonClickedSfx);
        }

        public void PressEnter()
        {
            if (_showingResult) return;

            bool ok = string.Equals(_input, KeypadCombo ?? "", StringComparison.Ordinal);

            if (ok)
            {
                CodeEntered = true;

                if (Keycard.Swiped)
                {
                    SetScreenColor(ScreenGrantedColor);
                    UpdateDisplayText(AccessGrantedText);
                    AccessGranted();
                }
                else
                {
                    SetScreenColor(ScreenPendingColor);
                    UpdateDisplayText(AccessPendingText1);
                    PlayAccessPending();
                    waitingKeycard = true;
                }
                Play(AccessGrantedSfx);
                try { OnAccessGranted.Invoke(); } catch { }
            }
            else
            {
                SetScreenColor(ScreenDeniedColor);
                UpdateDisplayText(AccessDeniedText);
                Play(AccessDeniedSfx);
                AccessDenied();
                try { OnAccessDenied.Invoke(); } catch { }
            }

            _showingResult = true;
            _resultEndTime = Time.time + Mathf.Max(0.05f, DisplayResultTime);
        }

        void KeycardSwiped()
        {
            SetScreenColor(ScreenGrantedColor);
            UpdateDisplayText(AccessGrantedText);
            waitingKeycard = false;
            AccessGranted();
        }

        public void AccessGranted()
        {
            MelonLogger.Msg("[Keypad] - ACCESS GRANTED");
            AudioUtil.PlayAt("AccessGranted.ogg", transform.position);
            if (!elevatorTriggered)
            {
                elevatorTriggered = true;
                HeistLevelManager.TriggerElevator();
            }
        }

        public int deniedCounter = 0;
        public void AccessDenied()
        {
            MelonLogger.Msg("[Keypad] - DENIED");
            denied = true;
            deniedCounter++;
            AudioUtil.PlayAt("AccessDenied.ogg", transform.position);
            if (deniedCounter >= 3)
            {
                HeistLevelManager.TurretDeath();
            }
        }

        public void PlayAccessGranted()
        {
            Play(AccessGrantedSfx);
        }

        public void PlayAccessPending()
        {
            AudioUtil.PlayAt("AccessPending.ogg", transform.position);
        }

        private void ClearInput()
        {
            _input = "";
            UpdateDisplayText(_input);
        }
        bool sizeHalfed = false;
        float _normalDisplayFontSize = -1f;
        public void UpdateDisplayText(string s)
        {
            if (keypadDisplayText == null) return;

            if (_normalDisplayFontSize <= 0f)
                _normalDisplayFontSize = keypadDisplayText.fontSize;

            keypadDisplayText.text = s;
            if (s == AccessPendingText1 || s == AccessPendingText2)
            {
                keypadDisplayText.fontSize = _normalDisplayFontSize * 0.5f;
                sizeHalfed = true;
            }
            else if (sizeHalfed)
            {
                keypadDisplayText.fontSize = _normalDisplayFontSize;
                sizeHalfed = false;
            }

            keypadDisplayText.color = Color.black;
            keypadDisplayText.alpha = 1f;

            keypadDisplayText.alignment = TextAlignmentOptions.Center;
        }

        public void SetScreenColor(Color c)
        {
            if (_screenBg != null)
            {

                Color outC = c * Mathf.Max(0f, ScreenIntensity);
                outC.a = 1f;
                _screenBg.color = outC;
            }

        }

        private void Play(AudioClip clip)
        {
            if (clip == null || audioSource == null) return;
            audioSource.PlayOneShot(clip);
        }

        private void TryAutoWireRefs()
        {

            if (panelMesh == null)
            {
                var panelTf = FindDeepChild(transform, "panel");
                if (panelTf != null) panelMesh = panelTf.GetComponent<MeshRenderer>();
            }

            if (keypadDisplayText == null)
            {
                var displayTf = FindDeepChild(transform, "DisplayText");
                if (displayTf != null) keypadDisplayText = displayTf.GetComponent<TextMeshProUGUI>();
            }

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
        }

        private void EnsureScreenBackground()
        {
            if (keypadDisplayText == null) return;

            var canvasTf = FindDeepChild(transform, "DisplayCanvas");
            if (canvasTf == null) return;

            var existing = canvasTf.Find("ScreenBG");
            if (existing != null)
            {
                _screenBg = existing.GetComponent<Image>();
                if (_screenBg == null) _screenBg = existing.gameObject.AddComponent<Image>();
                ForceBgRect(existing.GetComponent<RectTransform>());
                PushBgBehindText(existing);
                return;
            }

            var bgGo = new GameObject("ScreenBG");
            bgGo.transform.SetParent(canvasTf, false);

            var rt = bgGo.AddComponent<RectTransform>();
            ForceBgRect(rt);

            _screenBg = bgGo.AddComponent<Image>();
            _screenBg.raycastTarget = false;

            PushBgBehindText(bgGo.transform);
        }

        private void ForceBgRect(RectTransform rt)
        {
            if (rt == null) return;

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        private void PushBgBehindText(Transform bgTf)
        {

            try { bgTf.SetSiblingIndex(0); } catch { }
        }

        private void FixDisplayTextLayout()
        {
            if (keypadDisplayText == null) return;

            var rt = keypadDisplayText.GetComponent<RectTransform>();
            if (rt == null) return;

            var canvasTf = FindDeepChild(transform, "DisplayCanvas");
            if (canvasTf != null && rt.parent != canvasTf)
            {
                try { rt.SetParent(canvasTf, false); } catch { }
            }

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(6f, 2f);
            rt.offsetMax = new Vector2(-6f, -2f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            keypadDisplayText.alignment = TextAlignmentOptions.Center;
            keypadDisplayText.enableWordWrapping = false;
            keypadDisplayText.overflowMode = TextOverflowModes.Overflow;
            keypadDisplayText.margin = Vector4.zero;
        }

        private void ApplySchellTmpLook()
        {
            if (keypadDisplayText == null) return;

            TMP_FontAsset assignedFont = keypadDisplayText.font;
            Material assignedMat = keypadDisplayText.fontSharedMaterial;

            TMP_Text hiddenDigitalDonor = FindHiddenDigitalTextDonor();

            TMP_FontAsset keypadFont = OriginalDisplayFont != null
                ? OriginalDisplayFont
                : FindKeypadDigitalFont(assignedFont);

            if (!LooksLikeKeypadDigitalFont(keypadFont) && hiddenDigitalDonor != null && LooksLikeKeypadDigitalFont(hiddenDigitalDonor.font))
                keypadFont = hiddenDigitalDonor.font;

            Material keypadFontMat = OriginalDisplayMaterial != null
                ? OriginalDisplayMaterial
                : FindKeypadDigitalMaterial(assignedMat);

            if (!LooksLikeKeypadDigitalMaterial(keypadFontMat) && hiddenDigitalDonor != null && LooksLikeKeypadDigitalMaterial(hiddenDigitalDonor.fontSharedMaterial))
                keypadFontMat = hiddenDigitalDonor.fontSharedMaterial;

            TextMeshProUGUI donor = FindSchellTmpDonor();

            if (!LooksLikeKeypadDigitalFont(keypadFont))
            {
                MelonLogger.Warning("[Keypad] Digital font was not found. Keeping current font='" + SafeName(assignedFont) + "'. Assign OriginalDisplayFont to DigitsFonts in the prefab if you want the old keypad font.");
                keypadFont = assignedFont;
            }

            try
            {
                if (keypadFont != null)
                    keypadDisplayText.font = keypadFont;

                Material finalMat = null;

                if (donor != null && donor.fontSharedMaterial != null)
                {
                    finalMat = new Material(donor.fontSharedMaterial);
                    finalMat.name = "Keypad_DigitalFont_SchellSafe_TMP_Material";

                    Texture atlas = GetFontAtlasTexture(keypadFont, keypadFontMat != null ? keypadFontMat : assignedMat);
                    if (atlas != null)
                    {
                        if (finalMat.HasProperty("_MainTex")) finalMat.SetTexture("_MainTex", atlas);
                        finalMat.mainTexture = atlas;
                    }

                    ForceTmpMaterialBlack(finalMat);
                }
                else if (keypadFontMat != null)
                {
                    finalMat = new Material(keypadFontMat);
                    finalMat.name = "Keypad_DigitalFont_Original_TMP_Material_Copy";
                    ForceTmpMaterialBlack(finalMat);
                }

                if (finalMat != null)
                {
                    keypadDisplayText.fontSharedMaterial = finalMat;
                    keypadDisplayText.fontMaterial = finalMat;
                }

                keypadDisplayText.raycastTarget = false;
                keypadDisplayText.enableCulling = false;
                keypadDisplayText.enableWordWrapping = false;
                keypadDisplayText.overflowMode = TextOverflowModes.Overflow;
                keypadDisplayText.alignment = TextAlignmentOptions.Center;
                keypadDisplayText.color = Color.black;
                keypadDisplayText.alpha = 1f;
                keypadDisplayText.SetAllDirty();
                keypadDisplayText.UpdateMeshPadding();

                string donorName = donor != null && donor.gameObject != null ? donor.gameObject.name : "none";
                string hiddenName = hiddenDigitalDonor != null && hiddenDigitalDonor.gameObject != null ? hiddenDigitalDonor.gameObject.name : "none";
                MelonLogger.Msg("[Keypad] TMP font pass v3. finalFont='" + SafeName(keypadDisplayText.font) + "' finalMat='" + SafeName(keypadDisplayText.fontSharedMaterial) + "' schellDonor='" + donorName + "' hiddenDigitalDonor='" + hiddenName + "'.");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[Keypad] Failed to apply TMP font pass v3: " + ex.Message);
            }
        }

        private TMP_Text FindHiddenDigitalTextDonor()
        {

            try
            {
                TMP_Text[] localTexts = GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < localTexts.Length; i++)
                {
                    TMP_Text t = localTexts[i];
                    if (t == null || t == keypadDisplayText) continue;
                    if (LooksLikeKeypadDigitalFont(t.font) || LooksLikeKeypadDigitalMaterial(t.fontSharedMaterial))
                        return t;
                }
            }
            catch { }

            try
            {
                TMP_Text[] allTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();
                for (int i = 0; i < allTexts.Length; i++)
                {
                    TMP_Text t = allTexts[i];
                    if (t == null || t == keypadDisplayText) continue;
                    if (LooksLikeKeypadDigitalFont(t.font) || LooksLikeKeypadDigitalMaterial(t.fontSharedMaterial))
                        return t;
                }
            }
            catch { }

            return null;
        }

        private string SafeName(UnityEngine.Object obj)
        {
            return obj != null ? obj.name : "null";
        }

        private TMP_FontAsset FindKeypadDigitalFont(TMP_FontAsset assignedFont)
        {
            if (LooksLikeKeypadDigitalFont(assignedFont))
                return assignedFont;

            try
            {
                TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
                for (int i = 0; i < texts.Length; i++)
                {
                    TMP_Text t = texts[i];
                    if (t == null || t == keypadDisplayText || t.font == null) continue;
                    if (LooksLikeKeypadDigitalFont(t.font))
                        return t.font;
                }
            }
            catch { }

            try
            {
                TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                for (int i = 0; i < fonts.Length; i++)
                {
                    TMP_FontAsset f = fonts[i];
                    if (LooksLikeKeypadDigitalFont(f))
                        return f;
                }
            }
            catch { }

            return assignedFont;
        }

        private Material FindKeypadDigitalMaterial(Material assignedMat)
        {
            if (LooksLikeKeypadDigitalMaterial(assignedMat))
                return assignedMat;

            try
            {
                TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
                for (int i = 0; i < texts.Length; i++)
                {
                    TMP_Text t = texts[i];
                    if (t == null || t == keypadDisplayText || t.fontSharedMaterial == null) continue;
                    if (LooksLikeKeypadDigitalMaterial(t.fontSharedMaterial))
                        return t.fontSharedMaterial;
                }
            }
            catch { }

            try
            {
                Material[] mats = Resources.FindObjectsOfTypeAll<Material>();
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (LooksLikeKeypadDigitalMaterial(m))
                        return m;
                }
            }
            catch { }

            return assignedMat;
        }

        private bool LooksLikeKeypadDigitalFont(TMP_FontAsset font)
        {
            if (font == null || string.IsNullOrEmpty(font.name)) return false;

            string n = font.name;
            return n.IndexOf("Digit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   n.IndexOf("DIGIB", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   n.IndexOf("DS-", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool LooksLikeKeypadDigitalMaterial(Material mat)
        {
            if (mat == null || string.IsNullOrEmpty(mat.name)) return false;

            string n = mat.name;
            return n.IndexOf("Digit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   n.IndexOf("DIGIB", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   n.IndexOf("DS-", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private TextMeshProUGUI FindSchellTmpDonor()
        {
            TextMeshProUGUI fallback = null;

            TextMeshProUGUI[] texts;
            try { texts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>(); }
            catch { return null; }

            for (int i = 0; i < texts.Length; i++)
            {
                TextMeshProUGUI t = texts[i];
                if (t == null || t == keypadDisplayText) continue;

                string goName = t.gameObject != null ? t.gameObject.name ?? "" : "";
                string fontName = t.font != null ? t.font.name ?? "" : "";
                string matName = t.fontSharedMaterial != null ? t.fontSharedMaterial.name ?? "" : "";

                bool looksLikeSchellMenuText =
                    goName.IndexOf("Paused Text", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    goName.IndexOf("Paused Text BG", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fontName.IndexOf("GROTESKIA", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    matName.IndexOf("GROTESKIA", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    matName.IndexOf("MODAL", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!looksLikeSchellMenuText) continue;

                if (t.font != null && t.fontSharedMaterial != null)
                    return t;

                if (fallback == null)
                    fallback = t;
            }

            return fallback;
        }

        private Texture GetFontAtlasTexture(TMP_FontAsset font, Material originalMat)
        {
            try
            {
                if (font != null && font.atlasTexture != null)
                    return font.atlasTexture;
            }
            catch { }

            try
            {
                if (originalMat != null)
                {
                    if (originalMat.HasProperty("_MainTex"))
                    {
                        Texture t = originalMat.GetTexture("_MainTex");
                        if (t != null) return t;
                    }

                    if (originalMat.mainTexture != null)
                        return originalMat.mainTexture;
                }
            }
            catch { }

            return null;
        }

        private void ForceTmpMaterialBlack(Material mat)
        {
            if (mat == null) return;

            try
            {
                if (mat.HasProperty("_FaceColor")) mat.SetColor("_FaceColor", Color.black);
                if (mat.HasProperty("_OutlineColor")) mat.SetColor("_OutlineColor", Color.black);
                if (mat.HasProperty("_OutlineWidth")) mat.SetFloat("_OutlineWidth", 0f);
                if (mat.HasProperty("_GlowPower")) mat.SetFloat("_GlowPower", 0f);
                if (mat.HasProperty("_UnderlayColor")) mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0f));
            }
            catch { }
        }

        public void AddInput(string input)
        {
            if (string.IsNullOrEmpty(input)) return;

            input = input.ToLowerInvariant();

            if (input == "enter")
            {
                PressEnter();
                return;
            }

            if (input == "clear")
            {
                PressClear();
                return;
            }

            PressDigit(input);
        }

        private Transform FindDeepChild(Transform root, string name)
        {
            if (root == null) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c == null) continue;
                if (string.Equals(c.name, name, StringComparison.Ordinal)) return c;
                var r = FindDeepChild(c, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
