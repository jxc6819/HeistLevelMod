using System;
using HarmonyLib;
using MelonLoader;
using UnhollowerBaseLib.Attributes;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class PhoenixButtonHook : MonoBehaviour
    {
        public PhoenixButtonHook(IntPtr ptr) : base(ptr) { }
        public PhoenixButtonHook()
            : base(ClassInjector.DerivedConstructorPointer<PhoenixButtonHook>())
            => ClassInjector.DerivedConstructorBody(this);

        public string buttonObjectName = "P_WinRoom_INT_DebriefCaseButton_01";
        public string dialObjectName = "P_WinRoom_INT_DebriefCaseDial_01";
        public bool hookOnEnable = true;

        public bool blockDefaultPressLogic = false;
        public bool suppressUnityEventRaisers = true;

        public float pressCooldown = 0.25f;
        public float actionDelaySeconds = 0.55f;
        public string vanSceneName = "Van";

        public static bool restarting = false;
        public static bool returningToVan = false;

        static int _targetInstanceId = 0;
        static bool _blockDefault = false;
        static bool _suppressEventRaisers = true;
        static float _cooldown = 0.25f;
        static float _actionDelay = 0.55f;
        static float _lastPressTime = -999f;
        static bool _patchInstalled = false;
        static bool _routeInProgress = false;

        static string _dialObjectName = "P_WinRoom_INT_DebriefCaseDial_01";
        static string _vanSceneName = "Van";

        Button _btn;

        void OnEnable()
        {
            EnsurePatchInstalled();
            if (hookOnEnable)
                TryHook();
        }

        public void TryHook()
        {
            EnsurePatchInstalled();

            GameObject go = null;
            if (!string.IsNullOrEmpty(buttonObjectName))
                go = GameObject.Find(buttonObjectName);

            if (go == null)
                go = gameObject;

            _btn = go.GetComponent<Button>();
            if (_btn == null)
            {
                MelonLogger.Error($"[PhoenixButtonHook] No SG.Phoenix Button on '{go.name}'.");
                return;
            }

            if (!string.IsNullOrEmpty(dialObjectName))
                _dialObjectName = dialObjectName;

            if (!string.IsNullOrEmpty(vanSceneName))
                _vanSceneName = vanSceneName;

            _targetInstanceId = _btn.gameObject.GetInstanceID();
            _blockDefault = blockDefaultPressLogic;
            _suppressEventRaisers = suppressUnityEventRaisers;
            _cooldown = Mathf.Max(0f, pressCooldown);
            _actionDelay = Mathf.Max(0f, actionDelaySeconds);

            int disabled = 0;
            if (_suppressEventRaisers)
                disabled = DisableUnityEventRaisers(go);

            MelonLogger.Msg($"[PhoenixButtonHook] Target set => '{_btn.gameObject.name}' (InstanceID={_targetInstanceId}) blockDefault={_blockDefault} suppressEventRaisers={_suppressEventRaisers} disabledEventRaisers={disabled} dial='{_dialObjectName}'");
        }

        public static void ResetState()
        {
            restarting = false;
            returningToVan = false;
            _routeInProgress = false;
            _lastPressTime = -999f;
        }

        public static void HandlePressed(Button btn)
        {
            if (btn == null)
                return;

            float t = Time.unscaledTime;
            if (t - _lastPressTime < _cooldown)
                return;

            if (_routeInProgress)
            {
                MelonLogger.Msg("[PhoenixButtonHook] Press ignored because route is already in progress.");
                return;
            }

            _lastPressTime = t;

            if (_suppressEventRaisers)
                DisableUnityEventRaisers(btn.gameObject);

            float rot = ReadDialRotation();
            bool shouldRestart = IsCloserToRestart(rot);

            restarting = false;
            returningToVan = false;
            _routeInProgress = true;

            MelonLogger.Msg($"[PhoenixButtonHook] PRESSED => {btn.gameObject.name} dialRotation={rot:0.###} action={(shouldRestart ? "Restart" : "ReturnToVan")} delay={_actionDelay:0.###}");
            MelonCoroutines.Start(Co_ExecuteAfterButtonAnim(shouldRestart));
        }

        [HideFromIl2Cpp]
        static IEnumerator Co_ExecuteAfterButtonAnim(bool restart)
        {
            float end = Time.realtimeSinceStartup + Mathf.Max(0f, _actionDelay);
            while (Time.realtimeSinceStartup < end)
                yield return null;

            try
            {
                Time.timeScale = 1f;

                if (restart)
                {
                    MelonLogger.Msg("[PhoenixButtonHook] Delayed action firing: Restart.");
                    returningToVan = false;
                    restarting = true;
                    _routeInProgress = false;
                    yield break;
                }

                MelonLogger.Msg("[PhoenixButtonHook] Delayed action firing: ReturnToVan.");
                restarting = false;
                returningToVan = true;

                try
                {
                    LevelUtil.HellenKeller(false);
                }
                catch { }

                SceneManager.LoadScene(_vanSceneName, LoadSceneMode.Single);
            }
            finally
            {
                _routeInProgress = false;
            }
        }

        static int DisableUnityEventRaisers(GameObject root)
        {
            if (root == null)
                return 0;

            int count = 0;

            try
            {
                var comps = root.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < comps.Length; i++)
                {
                    MonoBehaviour mb = comps[i];
                    if (mb == null)
                        continue;

                    string typeName = "";
                    try { typeName = mb.GetIl2CppType().FullName; } catch { }
                    if (string.IsNullOrEmpty(typeName))
                    {
                        try { typeName = mb.GetType().FullName; } catch { }
                    }

                    string lower = (typeName ?? "").ToLowerInvariant();
                    if (!lower.Contains("unityeventraiser"))
                        continue;

                    try
                    {
                        if (mb.enabled)
                        {
                            mb.enabled = false;
                            count++;
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[PhoenixButtonHook] DisableUnityEventRaisers failed: " + ex.Message);
            }

            return count;
        }

        static float ReadDialRotation()
        {
            try
            {
                GameObject dial = null;

                if (!string.IsNullOrEmpty(_dialObjectName))
                    dial = GameObject.Find(_dialObjectName);

                if (dial == null)
                    dial = GameObject.Find("P_WinRoom_INT_DebriefCaseDial_01");

                if (dial == null)
                    dial = GameObject.Find("P_WinRoom_INT_DebriefCaseDial_DeathRm_01");

                if (dial == null)
                {
                    MelonLogger.Warning("[PhoenixButtonHook] Dial object not found. Defaulting to restart.");
                    return 360f;
                }

                RotationalMotion rm = null;

                try
                {
                    if (dial.transform.childCount > 0)
                        rm = dial.transform.GetChild(0).gameObject.GetComponent<RotationalMotion>();
                }
                catch { }

                if (rm == null)
                    rm = dial.GetComponent<RotationalMotion>();

                if (rm == null)
                    rm = dial.GetComponentInChildren<RotationalMotion>(true);

                if (rm == null)
                {
                    MelonLogger.Warning("[PhoenixButtonHook] RotationalMotion not found on dial. Defaulting to restart.");
                    return 360f;
                }

                return NormalizeDialDegrees(rm._currentRotation);
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[PhoenixButtonHook] ReadDialRotation error: " + ex);
                return 360f;
            }
        }

        static bool IsCloserToRestart(float rotation)
        {
            float distToRestart = AngularDistance(rotation, 360f);
            float distToVan = AngularDistance(rotation, 270f);
            return distToRestart <= distToVan;
        }

        static float NormalizeDialDegrees(float degrees)
        {
            while (degrees < 0f)
                degrees += 360f;

            while (degrees > 360f)
                degrees -= 360f;

            if (degrees < 0.001f)
                return 360f;

            return degrees;
        }

        static float AngularDistance(float a, float b)
        {
            a = NormalizeDialDegrees(a);
            b = NormalizeDialDegrees(b);

            float diff = Mathf.Abs(a - b);
            if (diff > 180f)
                diff = 360f - diff;

            return diff;
        }

        static void EnsurePatchInstalled()
        {
            if (_patchInstalled) return;
            _patchInstalled = true;

            try
            {
                var harmony = new HarmonyLib.Harmony("IEYTD_Mod2Code.PhoenixButtonHook");
                harmony.PatchAll(typeof(PhoenixButtonHookPatches));
                MelonLogger.Msg("[PhoenixButtonHook] Harmony patches installed.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[PhoenixButtonHook] Failed to install Harmony patches: " + ex);
            }
        }

        [HarmonyPatch]
        static class PhoenixButtonHookPatches
        {
            [HarmonyPatch(typeof(Button), "PressButton")]
            [HarmonyPrefix]
            static bool PressButton_Prefix(Button __instance)
            {
                try
                {
                    if (__instance == null) return true;

                    if (_targetInstanceId != 0 && __instance.gameObject.GetInstanceID() == _targetInstanceId)
                    {
                        HandlePressed(__instance);

                        if (_blockDefault)
                        {
                            MelonLogger.Warning("[PhoenixButtonHook] Blocking default PressButton() logic on target button. Animation may not play in this mode.");
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error("[PhoenixButtonHook] PressButton_Prefix error: " + ex);
                }

                return true;
            }
        }
    }
}
