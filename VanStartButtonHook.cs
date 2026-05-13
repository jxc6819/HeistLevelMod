using System;
using System.Collections;
using MelonLoader;
using HarmonyLib;
using SG.Phoenix.Assets.Code.Interactables;
using UnhollowerBaseLib.Attributes;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{

    public class VanStartButtonHook : MonoBehaviour
    {
        public VanStartButtonHook(IntPtr ptr) : base(ptr) { }
        public VanStartButtonHook()
            : base(ClassInjector.DerivedConstructorPointer<VanStartButtonHook>())
            => ClassInjector.DerivedConstructorBody(this);

        public string startButtonName = "StartButton";
        public string cassetteSlotName = "P_Van_INT_CassetteSlot_01";

        public float pressCooldown = 0.25f;

        private static bool _patchInstalled = false;

        private static int _startButtonInstanceId = 0;

        private static float _cooldownSeconds = 0.25f;
        private static float _lastPress = -999f;
        private static VanStartButtonHook _currentHook;

        private Button _button;
        private AudioSource _briefingAudio;

        void OnEnable()
        {
            _currentHook = this;
            EnsurePatchInstalled();
            MelonCoroutines.Start(Co_TryHookSoon());
        }

        void OnDisable()
        {
            StopBriefingAudio();

            if (_currentHook == this)
                _currentHook = null;

            _button = null;
            _startButtonInstanceId = 0;
        }

        void OnDestroy()
        {
            StopBriefingAudio();

            if (_currentHook == this)
                _currentHook = null;
        }

        [HideFromIl2Cpp]
        private IEnumerator Co_TryHookSoon()
        {

            for (int i = 0; i < 90; i++)
            {
                TryHook();
                if (_button != null) yield break;
                yield return null;
            }

            MelonLogger.Warning("[VanStartButtonHook] Could not find StartButton to hook. Prefix can still override by live button name if the button exists later.");
        }

        private void TryHook()
        {

            if (_button != null)
                return;

            _button = null;
            _startButtonInstanceId = 0;

            var go = GameObject.Find(startButtonName);
            if (go == null) return;

            _button = go.GetComponent<Button>();
            if (_button == null)
            {
                MelonLogger.Warning($"[VanStartButtonHook] '{startButtonName}' found but has no SG.Phoenix Button component.");
                return;
            }

            _startButtonInstanceId = _button.gameObject.GetInstanceID();
            _cooldownSeconds = Mathf.Max(0f, pressCooldown);

            MelonLogger.Msg($"[VanStartButtonHook] Hooked '{_button.gameObject.name}' (InstanceID={_startButtonInstanceId})");
        }

        private static void EnsurePatchInstalled()
        {
            if (_patchInstalled) return;
            _patchInstalled = true;

            try
            {
                var harmony = new HarmonyLib.Harmony("IEYTD2_SubmarineCode.VanStartButtonHook");
                harmony.PatchAll(typeof(VanStartButtonHookPatches));
                MelonLogger.Msg("[VanStartButtonHook] Harmony patch installed.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[VanStartButtonHook] Failed to install Harmony patch: " + ex);
            }
        }

        private static bool ShouldOverrideVanStart()
        {
            var tape = VanSceneManager.SubTapeInstance;
            if (tape == null) return false;

            var slotGo = GameObject.Find("P_Van_INT_CassetteSlot_01");
            if (slotGo == null) return false;

            return tape.transform.IsChildOf(slotGo.transform);
        }

        private static void TriggerYourLoad()
        {
            if (MyMod.Instance != null)
                MyMod._RequestLoad();
            else
                MelonLogger.Warning("[VanStartButtonHook] MyMod.Instance is null, cannot load.");
        }

        private bool _wasTapeInserted = false;
        private bool _hasFiredTapeInserted = false;

        private bool IsTapeInserted()
        {
            var tape = VanSceneManager.SubTapeInstance;
            if (tape == null) return false;

            var slotGo = GameObject.Find(cassetteSlotName);
            if (slotGo == null) return false;

            return tape.transform.IsChildOf(slotGo.transform);
        }

        void Update()
        {

            if (_button == null && GameObject.Find(startButtonName) != null)
                TryHook();

            bool inserted = IsTapeInserted();

            if (!_wasTapeInserted && inserted)
            {
                _wasTapeInserted = true;

                if (!_hasFiredTapeInserted)
                {
                    _hasFiredTapeInserted = true;
                    MelonLogger.Msg("[VanStartButtonHook] Tape inserted.");
                    PlayBriefingAudio();
                    MelonCoroutines.Start(Co_UpdateScreens());
                }
            }
            else if (_wasTapeInserted && !inserted)
            {
                _wasTapeInserted = false;
                _hasFiredTapeInserted = false;

                MelonLogger.Msg("[VanStartButtonHook] Tape ejected.");
                StopBriefingAudio();
                MelonCoroutines.Start(Co_RestoreDefaultScreens());
            }
        }

        private void PlayBriefingAudio()
        {
            StopBriefingAudio();

            try
            {
                _briefingAudio = AudioUtil.PlayAt("HandlerBriefing.wav", transform.position, 1, true);

                if (_briefingAudio == null)
                    MelonLogger.Warning("[VanStartButtonHook] HandlerBriefing.wav PlayAt returned null.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[VanStartButtonHook] Failed to play HandlerBriefing.wav: " + ex);
                _briefingAudio = null;
            }
        }

        private static void StopBriefingAudioForStartMission()
        {
            if (_currentHook != null)
                _currentHook.StopBriefingAudio();
        }

        private void StopBriefingAudio()
        {
            if (_briefingAudio == null)
                return;

            try
            {
                _briefingAudio.Stop();

                if (_briefingAudio.gameObject != null)
                    Destroy(_briefingAudio.gameObject);
            }
            catch { }

            _briefingAudio = null;
        }

        [HideFromIl2Cpp]
        System.Collections.IEnumerator Co_UpdateScreens()
        {
            yield return new WaitForSeconds(0.1f);
            VanSceneManager.ReplaceScreenTexture();
            VanSceneManager.SetSouvenirScreen();
        }

        [HideFromIl2Cpp]
        System.Collections.IEnumerator Co_RestoreDefaultScreens()
        {
            yield return new WaitForSeconds(0.5f);
            VanSceneManager.RestoreDefaultVanScreens();
        }

        [HarmonyPatch]
        private static class VanStartButtonHookPatches
        {
            [HarmonyPatch(typeof(Button), "PressButton")]
            [HarmonyPrefix]
            private static bool PressButton_Prefix(Button __instance)
            {
                try
                {
                    if (__instance == null || __instance.gameObject == null)
                        return true;

                    if (!string.Equals(__instance.gameObject.name, "StartButton", StringComparison.Ordinal))
                        return true;

                    int liveId = __instance.gameObject.GetInstanceID();
                    if (_startButtonInstanceId != liveId)
                    {
                        _startButtonInstanceId = liveId;
                        MelonLogger.Msg("[VanStartButtonHook] Live StartButton resolved by prefix. InstanceID=" + liveId);
                    }

                    if (!ShouldOverrideVanStart())
                        return true;

                    float t = Time.unscaledTime;
                    if (t - _lastPress < _cooldownSeconds)
                        return false;

                    _lastPress = t;

                    MelonLogger.Msg("[VanStartButtonHook] Sub tape in slot -> overriding StartButton.");
                    StopBriefingAudioForStartMission();
                    TriggerYourLoad();
                    return false;
                }
                catch (Exception ex)
                {
                    MelonLogger.Error("[VanStartButtonHook] PressButton_Prefix error: " + ex);
                }

                return true;
            }
        }
    }
}
