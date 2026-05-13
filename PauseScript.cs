using System;
using HarmonyLib;
using MelonLoader;
using TMPro;
using UnhollowerBaseLib.Attributes;
using UnhollowerRuntimeLib;
using UnityEngine;
using UnityEngine.UI;

namespace IEYTD_Mod2Code
{
    public class PauseScript : MonoBehaviour
    {
        public PauseScript(IntPtr ptr) : base(ptr) { }
        public PauseScript() : base(ClassInjector.DerivedConstructorPointer<PauseScript>())
            => ClassInjector.DerivedConstructorBody(this);

        public string PauseRootName = "GameMenu_Van";
        public string UiChildName = "UI";
        public string RestartButtonName = "RestartButton";
        public string VanButtonName = "BacktoVanButton";

        public Vector3 MenuPosition = new Vector3(0f, 1.5f, 2f);
        public Vector3 MenuEuler = new Vector3(0f, 0f, 0f);
        public Vector3 MenuScale = new Vector3(0.6f, 0.6f, 0.6f);

        public float ButtonCooldown = 0.35f;
        public bool RepositionEveryPause = true;
        public bool ForceButtonVisualRefresh = true;

        static bool _patchInstalled;
        static int _restartButtonInstanceId;
        static int _vanButtonInstanceId;
        static float _lastHandledTime = -999f;
        static bool _transitionRunning;
        static PauseScript _activeInstance;

        GameObject _menuRoot;
        GameObject _uiRoot;
        Button _restartButton;
        Button _vanButton;
        bool _wasPaused;
        bool _hookLogPrinted;
        float _nextResolveTime;

        void Awake()
        {
            _activeInstance = this;
            EnsurePatchInstalled();
        }

        void Start()
        {
            ResolveAndHook(forceLog: true);
        }

        void OnEnable()
        {
            _activeInstance = this;
            EnsurePatchInstalled();
            ResolveAndHook(forceLog: true);
        }

        void OnDestroy()
        {
            if (ReferenceEquals(_activeInstance, this))
                _activeInstance = null;
        }

        void Update()
        {
            if (Time.unscaledTime >= _nextResolveTime)
            {
                _nextResolveTime = Time.unscaledTime + 0.5f;

                if (_menuRoot == null || _uiRoot == null || _restartButton == null || _vanButton == null || _restartButtonInstanceId == 0 || _vanButtonInstanceId == 0)
                    ResolveAndHook(forceLog: false);
            }

            bool pausedNow = IsPauseMenuVisible();
            if (pausedNow && !_wasPaused)
                OnPauseOpened();

            _wasPaused = pausedNow;
        }

        public void ResolveAndHook()
        {
            ResolveAndHook(forceLog: true);
        }

        public void ResolveAndHook(bool forceLog)
        {
            try
            {
                _menuRoot = GameObject.Find(PauseRootName);
                if (_menuRoot == null)
                    return;

                Transform ui = FindDeepChildFlexible(_menuRoot.transform, UiChildName);
                _uiRoot = ui != null ? ui.gameObject : null;

                ApplyMenuPose();

                GameObject restartObj = FindButtonObject(_menuRoot.transform, RestartButtonName, "restart", null);
                GameObject vanObj = FindButtonObject(_menuRoot.transform, VanButtonName, "van", "exit");

                _restartButton = restartObj != null ? restartObj.GetComponent<Button>() : null;
                _vanButton = vanObj != null ? vanObj.GetComponent<Button>() : null;

                if (_restartButton != null)
                {
                    _restartButtonInstanceId = _restartButton.gameObject.GetInstanceID();
                    UnlockButton(_restartButton.gameObject);
                }

                if (_vanButton != null)
                {
                    _vanButtonInstanceId = _vanButton.gameObject.GetInstanceID();
                    UnlockButton(_vanButton.gameObject);
                }

                if (forceLog || !_hookLogPrinted)
                {
                    _hookLogPrinted = true;
                    MelonLogger.Msg("[PauseScript] Hooked pause menu. RestartId=" + _restartButtonInstanceId + " VanId=" + _vanButtonInstanceId +
                                    " RestartFound=" + (_restartButton != null) + " VanFound=" + (_vanButton != null));
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[PauseScript] ResolveAndHook failed: " + e);
            }
        }

        void OnPauseOpened()
        {
            ResolveAndHook(forceLog: false);
            ApplyMenuPose();

            if (_restartButton != null)
                UnlockButton(_restartButton.gameObject);

            if (_vanButton != null)
                UnlockButton(_vanButton.gameObject);

            MelonLogger.Msg("[PauseScript] Pause opened. Buttons refreshed. RestartId=" + _restartButtonInstanceId + " VanId=" + _vanButtonInstanceId);
        }

        bool IsPauseMenuVisible()
        {
            try
            {
                if (_uiRoot == null) return false;
                return _uiRoot.activeSelf || _uiRoot.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }

        void ApplyMenuPose()
        {
            if (_menuRoot == null) return;

            try
            {
                _menuRoot.transform.position = MenuPosition;
                _menuRoot.transform.rotation = Quaternion.Euler(MenuEuler);
                _menuRoot.transform.localScale = MenuScale;
            }
            catch { }
        }

        void UnlockButton(GameObject buttonObj)
        {
            if (buttonObj == null) return;

            try
            {
                buttonObj.SetActive(true);

                Button b = buttonObj.GetComponent<Button>();
                if (b != null)
                {
                    b.enabled = true;
                    b.interactable = true;

                    ColorBlock cb = b.colors;
                    Color normal = cb.normalColor;
                    normal.a = 1f;
                    cb.normalColor = normal;

                    Color highlighted = cb.highlightedColor;
                    highlighted.a = 1f;
                    cb.highlightedColor = highlighted;

                    Color pressed = cb.pressedColor;
                    pressed.a = 1f;
                    cb.pressedColor = pressed;

                    Color selected = cb.selectedColor;
                    selected.a = 1f;
                    cb.selectedColor = selected;

                    Color disabled = cb.disabledColor;
                    disabled.a = 1f;
                    cb.disabledColor = normal;
                    b.colors = cb;
                }

                CanvasGroup[] groups = buttonObj.GetComponentsInChildren<CanvasGroup>(true);
                for (int i = 0; i < groups.Length; i++)
                {
                    CanvasGroup cg = groups[i];
                    if (cg == null) continue;
                    cg.alpha = 1f;
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                }

                Graphic[] graphics = buttonObj.GetComponentsInChildren<Graphic>(true);
                for (int i = 0; i < graphics.Length; i++)
                {
                    Graphic g = graphics[i];
                    if (g == null) continue;
                    g.gameObject.SetActive(true);
                    g.enabled = true;

                    bool isTextGraphic = IsTextGraphic(g);
                    g.raycastTarget = !isTextGraphic;

                    Color c = g.color;
                    c.a = 1f;

                    if (!isTextGraphic)
                    {
                        string graphicName = "";
                        try { graphicName = g.gameObject.name ?? ""; } catch { }

                        bool isIconGraphic = graphicName.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) >= 0;
                        bool isBackgroundGraphic =
                            graphicName.IndexOf("BG", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            graphicName.IndexOf("Background", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            graphicName.IndexOf("Shadow", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (isIconGraphic)
                        {

                            float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                            if (min > 0.70f)
                            {
                                c.r = 0.12f;
                                c.g = 0.10f;
                                c.b = 0.09f;
                            }
                        }
                        else if (isBackgroundGraphic)
                        {

                            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                            if (max < 0.65f)
                            {
                                c.r = Mathf.Max(c.r, 0.85f);
                                c.g = Mathf.Max(c.g, 0.85f);
                                c.b = Mathf.Max(c.b, 0.85f);
                            }
                        }
                    }
                    else
                    {
                        float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                        if (min > 0.70f)
                        {
                            c.r = 0.12f;
                            c.g = 0.10f;
                            c.b = 0.09f;
                        }
                    }

                    g.color = c;
                }

                Selectable[] selectables = buttonObj.GetComponentsInChildren<Selectable>(true);
                for (int i = 0; i < selectables.Length; i++)
                {
                    Selectable s = selectables[i];
                    if (s == null) continue;
                    s.gameObject.SetActive(true);
                    s.enabled = true;
                    s.interactable = true;
                }

                if (ForceButtonVisualRefresh)
                    RefreshKnownVisualChildren(buttonObj);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[PauseScript] UnlockButton failed for " + buttonObj.name + ": " + e.Message);
            }
        }

        static bool IsTextGraphic(Graphic g)
        {
            if (g == null) return false;

            try
            {
                if (g is Text) return true;
                if (g is TextMeshProUGUI) return true;
            }
            catch { }

            string typeName = "";
            try { typeName = g.GetType().Name ?? ""; } catch { }
            if (typeName.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (typeName.IndexOf("TMP", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            string name = "";
            try { name = g.gameObject.name ?? ""; } catch { }
            return name.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        void RefreshKnownVisualChildren(GameObject buttonObj)
        {
            if (buttonObj == null) return;

            string[] visualTokens =
            {
                "BG",
                "Background",
                "Shadow",
                "Icon",
                "Text"
            };

            for (int i = 0; i < visualTokens.Length; i++)
            {
                Transform t = FindChildContains(buttonObj.transform, visualTokens[i]);
                if (t == null) continue;

                try
                {
                    t.gameObject.SetActive(false);
                    t.gameObject.SetActive(true);
                }
                catch { }
            }
        }

        static bool CanHandlePress()
        {
            if (_transitionRunning) return false;

            float now = Time.unscaledTime;
            if (now - _lastHandledTime < 0.35f)
                return false;

            _lastHandledTime = now;
            return true;
        }

        static void HandleRestartPressed()
        {
            if (!CanHandlePress()) return;

            MelonLogger.Msg("[PauseScript] Restart mission pressed.");
            _transitionRunning = true;
            MelonCoroutines.Start(CoRestartMission());
        }

        static void HandleVanPressed()
        {
            if (!CanHandlePress()) return;

            MelonLogger.Msg("[PauseScript] Exit to Van pressed.");
            _transitionRunning = true;
            MelonCoroutines.Start(CoLoadVanOnly());
        }

        [HideFromIl2Cpp]
        static IEnumerator CoRestartMission()
        {
            Time.timeScale = 1f;
            LevelUtil.HellenKeller(true);

            var load = SceneManager.LoadSceneAsync("Van", LoadSceneMode.Single);
            if (load != null)
            {
                while (!load.isDone)
                    yield return null;
            }

            Time.timeScale = 1f;
            LevelUtil.HellenKeller(true);

            yield return new WaitForSecondsRealtime(0.5f);

            Time.timeScale = 1f;
            LevelUtil.HellenKeller(true);

            MyMod._RequestLoad();
            _transitionRunning = false;
        }

        [HideFromIl2Cpp]
        static IEnumerator CoLoadVanOnly()
        {
            Time.timeScale = 1f;
            LevelUtil.HellenKeller(true);

            var load = SceneManager.LoadSceneAsync("Van", LoadSceneMode.Single);
            if (load != null)
            {
                while (!load.isDone)
                    yield return null;
            }

            Time.timeScale = 1f;
            yield return new WaitForSecondsRealtime(0.25f);

            Time.timeScale = 1f;
            LevelUtil.HellenKeller(false);
            _transitionRunning = false;
        }

        static void EnsurePatchInstalled()
        {
            if (_patchInstalled) return;
            _patchInstalled = true;

            try
            {
                var harmony = new HarmonyLib.Harmony("IEYTD_Mod2Code.PauseScript");
                harmony.PatchAll(typeof(PauseScriptPatches));
                MelonLogger.Msg("[PauseScript] Harmony patch installed.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[PauseScript] Failed to install Harmony patch: " + ex);
            }
        }

        static GameObject FindButtonObject(Transform root, string exactOrLikelyName, string requiredTokenA, string requiredTokenB)
        {
            if (root == null) return null;

            Transform exact = FindDeepChildFlexible(root, exactOrLikelyName);
            if (exact != null)
                return exact.gameObject;

            string normalizedName = NormalizeName(exactOrLikelyName);
            if (!string.IsNullOrEmpty(normalizedName))
            {
                Transform byNormalizedName = FindChildByNormalizedName(root, normalizedName);
                if (byNormalizedName != null)
                    return byNormalizedName.gameObject;
            }

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            if (buttons == null) return null;

            for (int i = 0; i < buttons.Length; i++)
            {
                Button b = buttons[i];
                if (b == null) continue;

                string objName = (b.gameObject.name ?? "").ToLowerInvariant();
                string text = GetButtonTextLower(b.gameObject);
                string combined = objName + " " + text;

                bool hasA = string.IsNullOrEmpty(requiredTokenA) || combined.Contains(requiredTokenA.ToLowerInvariant());
                bool hasB = string.IsNullOrEmpty(requiredTokenB) || combined.Contains(requiredTokenB.ToLowerInvariant());

                if (hasA && hasB)
                    return b.gameObject;
            }

            return null;
        }

        static string GetButtonTextLower(GameObject go)
        {
            if (go == null) return "";

            string result = "";

            try
            {
                TextMeshProUGUI[] tmpUis = go.GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int i = 0; i < tmpUis.Length; i++)
                {
                    if (tmpUis[i] != null && !string.IsNullOrEmpty(tmpUis[i].text))
                        result += " " + tmpUis[i].text;
                }
            }
            catch { }

            try
            {
                TextMeshPro[] tmps = go.GetComponentsInChildren<TextMeshPro>(true);
                for (int i = 0; i < tmps.Length; i++)
                {
                    if (tmps[i] != null && !string.IsNullOrEmpty(tmps[i].text))
                        result += " " + tmps[i].text;
                }
            }
            catch { }

            try
            {
                Text[] texts = go.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    if (texts[i] != null && !string.IsNullOrEmpty(texts[i].text))
                        result += " " + texts[i].text;
                }
            }
            catch { }

            return result.ToLowerInvariant();
        }

        static Transform FindDeepChildFlexible(Transform root, string exactName)
        {
            if (root == null || string.IsNullOrEmpty(exactName)) return null;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform c = root.GetChild(i);
                if (c == null) continue;

                if (string.Equals(c.name, exactName, StringComparison.Ordinal) ||
                    string.Equals(c.name, exactName, StringComparison.OrdinalIgnoreCase))
                    return c;

                Transform r = FindDeepChildFlexible(c, exactName);
                if (r != null)
                    return r;
            }

            return null;
        }

        static Transform FindChildByNormalizedName(Transform root, string normalizedName)
        {
            if (root == null || string.IsNullOrEmpty(normalizedName)) return null;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform c = root.GetChild(i);
                if (c == null) continue;

                if (NormalizeName(c.name) == normalizedName)
                    return c;

                Transform r = FindChildByNormalizedName(c, normalizedName);
                if (r != null)
                    return r;
            }

            return null;
        }

        static string NormalizeName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";

            string low = s.ToLowerInvariant();
            string outS = "";
            for (int i = 0; i < low.Length; i++)
            {
                char ch = low[i];
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
                    outS += ch;
            }
            return outS;
        }

        static Transform FindChildContains(Transform root, string token)
        {
            if (root == null || string.IsNullOrEmpty(token)) return null;

            string low = token.ToLowerInvariant();
            for (int i = 0; i < root.childCount; i++)
            {
                Transform c = root.GetChild(i);
                if (c == null) continue;

                string n = (c.name ?? "").ToLowerInvariant();
                if (n.Contains(low))
                    return c;

                Transform r = FindChildContains(c, token);
                if (r != null)
                    return r;
            }

            return null;
        }

        [HarmonyPatch]
        static class PauseScriptPatches
        {
            static MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(Button), "Press");
            }

            static bool Prefix(Button __instance)
            {
                try
                {
                    if (__instance == null || __instance.gameObject == null)
                        return true;

                    int id = __instance.gameObject.GetInstanceID();

                    if (_restartButtonInstanceId != 0 && id == _restartButtonInstanceId)
                    {
                        HandleRestartPressed();
                        return false;
                    }

                    if (_vanButtonInstanceId != 0 && id == _vanButtonInstanceId)
                    {
                        HandleVanPressed();
                        return false;
                    }
                }
                catch (Exception e)
                {
                    MelonLogger.Warning("[PauseScript] Button Press prefix failed: " + e.Message);
                }

                return true;
            }
        }
    }
}
