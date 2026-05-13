using System;
using System.Collections;
using HarmonyLib;
using MelonLoader;
using SG.Phoenix.Assets.Code.Interactables;
using TMPro;
using UnhollowerBaseLib.Attributes;
using UnhollowerRuntimeLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IEYTD_Mod2Code
{

    public class WinRoomScript : MonoBehaviour
    {
        public WinRoomScript(IntPtr ptr) : base(ptr) { }
        public WinRoomScript() : base(ClassInjector.DerivedConstructorPointer<WinRoomScript>())
            => ClassInjector.DerivedConstructorBody(this);

        const string HostName = "WinRoomScript_HOST";

        const string ButtonName = "P_WinRoom_INT_DebriefCaseButton_01";
        const string DialName = "P_WinRoom_INT_DebriefCaseDial_01";

        const float ReturnToVanRotation = 270f;
        const float ReplayMissionRotation = 360f;

        const float SpeedrunTargetSeconds = 125f;
        const string SpeedrunTargetDisplay = "2:05";

        const string CustomDebriefClipName = "HandlerDebrief.wav";
        const float CustomDebriefDelaySeconds = 1.75f;

        static bool _installed;
        static bool _buttonPatchInstalled;
        static Action<Scene, LoadSceneMode> _sceneLoadedHandler;

        static int _targetButtonInstanceId;
        static float _lastButtonPressTime = -999f;
        static bool _transitionRunning;

        object _routine;
        object _buttonRoutine;
        object _customDebriefRoutine;

        public static bool DebugLog = true;

        public static bool RequireHeistWinRoomArm = true;
        static bool _armedForHeistWinRoom = false;
        static string _lastArmReason = "";

        public static bool BlockDefaultButtonPress = false;

        static readonly string[] WinRoomSceneNameTokens =
        {
            "winroom",
            "win_room",
            "win room"
        };

        static readonly string[] KillTokens =
        {
            "handler",
            "debrief",
            "missioncomplete",
            "mission_complete",
            "winroom",
            "win_room",
            "briefing",
            "voiceover",
            "voice_over",
            "dialogue",
            "dialog",
            "announcer"
        };

        static readonly string[] AvoidTokens =
        {
            "music",
            "ambience",
            "ambient",
            "sfx",
            "ui",
            "button",
            "stinger"
        };

        public static void ArmForHeistWinRoom(string reason = "HeistComplete")
        {
            _armedForHeistWinRoom = true;
            _lastArmReason = reason ?? "";

            if (DebugLog)
                MelonLogger.Msg("[WinRoomScript] Armed for custom WinRoom flow. reason=" + _lastArmReason);
        }

        public static void DisarmForHeistWinRoom()
        {
            _armedForHeistWinRoom = false;
            _lastArmReason = "";

            if (DebugLog)
                MelonLogger.Msg("[WinRoomScript] Disarmed custom WinRoom flow.");
        }

        public static void Install()
        {
            if (_installed) return;
            _installed = true;

            try
            {
                EnsureButtonPatchInstalled();

                GameObject host = GameObject.Find(HostName);
                if (ReferenceEquals(host, null))
                {
                    host = new GameObject(HostName);
                    GameObject.DontDestroyOnLoad(host);
                }

                WinRoomScript existing = host.GetComponent<WinRoomScript>();
                if (ReferenceEquals(existing, null))
                    host.AddComponent<WinRoomScript>();

                _sceneLoadedHandler = OnSceneLoaded;
                SceneManager.sceneLoaded += _sceneLoadedHandler;

                if (DebugLog)
                    MelonLogger.Msg("[WinRoomScript] Installed.");
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[WinRoomScript] Install failed: " + e);
            }
        }

        static void EnsureButtonPatchInstalled()
        {
            if (_buttonPatchInstalled) return;
            _buttonPatchInstalled = true;

            try
            {
                var harmony = new HarmonyLib.Harmony("IEYTD_Mod2Code.WinRoomScript.ButtonOverride");
                harmony.PatchAll(typeof(WinRoomButtonPatch));
                MelonLogger.Msg("[WinRoomScript] Button Harmony patch installed.");
            }
            catch (Exception e)
            {
                MelonLogger.Error("[WinRoomScript] Button Harmony patch install failed: " + e);
            }
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                string sceneName = scene.name ?? "";

                if (DebugLog)
                    MelonLogger.Msg("[WinRoomScript] Scene loaded: " + sceneName + " mode=" + mode);

                if (!LooksLikeWinRoom(sceneName)) return;

                if (RequireHeistWinRoomArm && !_armedForHeistWinRoom)
                {
                    if (DebugLog)
                        MelonLogger.Msg("[WinRoomScript] WinRoom loaded but custom flow is not armed. Leaving vanilla WinRoom alone.");
                    ClearButtonTarget();
                    return;
                }

                string consumedReason = _lastArmReason;
                _armedForHeistWinRoom = false;
                _lastArmReason = "";

                WinRoomScript inst = GetFreshInstance();
                if (ReferenceEquals(inst, null))
                {
                    MelonLogger.Warning("[WinRoomScript] WinRoom loaded, but no fresh WinRoomScript instance was found.");
                    return;
                }

                if (DebugLog)
                    MelonLogger.Msg("[WinRoomScript] WinRoom loaded: " + sceneName + " armedReason=" + consumedReason);

                ClearButtonTarget();

                inst.BeginWinRoomWindow();
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[WinRoomScript] OnSceneLoaded failed: " + e);
            }
        }

        static WinRoomScript GetFreshInstance()
        {
            try
            {
                GameObject host = GameObject.Find(HostName);
                if (ReferenceEquals(host, null))
                {
                    host = new GameObject(HostName);
                    GameObject.DontDestroyOnLoad(host);
                }

                WinRoomScript inst = host.GetComponent<WinRoomScript>();
                if (ReferenceEquals(inst, null))
                    inst = host.AddComponent<WinRoomScript>();

                return inst;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[WinRoomScript] GetFreshInstance failed: " + e.Message);
                return null;
            }
        }

        static void ClearButtonTarget()
        {
            _targetButtonInstanceId = 0;
            _lastButtonPressTime = -999f;
            _transitionRunning = false;
        }

        void BeginWinRoomWindow()
        {
            try
            {
                if (_routine != null)
                {
                    MelonCoroutines.Stop(_routine);
                    _routine = null;
                }

                if (_customDebriefRoutine != null)
                {
                    MelonCoroutines.Stop(_customDebriefRoutine);
                    _customDebriefRoutine = null;
                }

                _routine = MelonCoroutines.Start(CoWinRoomWindow());
                _customDebriefRoutine = MelonCoroutines.Start(CoPlayCustomDebriefLine());
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[WinRoomScript] BeginWinRoomWindow failed: " + e);
            }
        }

        [HideFromIl2Cpp]
        IEnumerator CoWinRoomWindow()
        {
            float endTime = Time.realtimeSinceStartup + 12.0f;

            while (Time.realtimeSinceStartup < endTime)
            {
                StopMatchingAudioSources();
                RefreshWinRoomTimes();
                TryHookWinRoomButton();
                yield return null;
            }

            _routine = null;

            if (DebugLog)
                MelonLogger.Msg("[WinRoomScript] WinRoom startup window ended.");
        }

        [HideFromIl2Cpp]
        IEnumerator CoPlayCustomDebriefLine()
        {
            float waitUntil = Time.realtimeSinceStartup + Mathf.Max(0.1f, CustomDebriefDelaySeconds);
            while (Time.realtimeSinceStartup < waitUntil)
            {
                StopMatchingAudioSources();
                yield return null;
            }

            try
            {
                Vector3 pos = Vector3.zero;
                GameObject buttonObj = GameObject.Find(ButtonName);
                if (!ReferenceEquals(buttonObj, null))
                    pos = buttonObj.transform.position;

                AudioUtil.PlayAt(CustomDebriefClipName, pos, 1,true);

                if (DebugLog)
                    MelonLogger.Msg("[WinRoomScript] Played custom debrief handler line: " + CustomDebriefClipName);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[WinRoomScript] Failed to play custom debrief handler line: " + e);
            }

            _customDebriefRoutine = null;
        }

        static void TryHookWinRoomButton()
        {
            try
            {
                GameObject buttonObj = GameObject.Find(ButtonName);
                if (ReferenceEquals(buttonObj, null)) return;

                Button btn = buttonObj.GetComponent<Button>();
                if (ReferenceEquals(btn, null))
                {
                    if (DebugLog)
                        MelonLogger.Warning("[WinRoomScript] Found " + ButtonName + " but it has no SG.Phoenix Button component.");
                    return;
                }

                int id = buttonObj.GetInstanceID();
                if (_targetButtonInstanceId != id)
                {
                    _targetButtonInstanceId = id;
                    MelonLogger.Msg("[WinRoomScript] Hooked WinRoom button: " + buttonObj.name + " InstanceID=" + id);
                }

                NeutralizeDefaultButtonEvents(buttonObj);
            }
            catch (Exception e)
            {
                if (DebugLog)
                    MelonLogger.Warning("[WinRoomScript] TryHookWinRoomButton failed: " + e.Message);
            }
        }

        static void NeutralizeDefaultButtonEvents(GameObject buttonObj)
        {
            if (ReferenceEquals(buttonObj, null)) return;

            try
            {
                var behaviours = buttonObj.GetComponents<Behaviour>();
                for (int i = 0; i < behaviours.Length; i++)
                {
                    Behaviour b = behaviours[i];
                    if (ReferenceEquals(b, null)) continue;

                    string typeName = b.GetType().FullName ?? b.GetType().Name ?? "";

                    bool shouldDisable =
                        typeName.Contains("UnityEventRaiser") ||
                        typeName.Contains("GlobalEventRaiser") ||
                        typeName.Contains("EventRaiser");

                    if (!shouldDisable) continue;

                    if (b.enabled)
                    {
                        b.enabled = false;
                        MelonLogger.Msg("[WinRoomScript] Disabled default button event component: " + typeName);
                    }
                }
            }
            catch (Exception e)
            {
                if (DebugLog)
                    MelonLogger.Warning("[WinRoomScript] NeutralizeDefaultButtonEvents failed: " + e.Message);
            }
        }

        public static void HandleWinRoomButtonPressed(Button btn)
        {
            try
            {
                if (ReferenceEquals(btn, null)) return;
                if (_targetButtonInstanceId == 0) return;
                if (btn.gameObject.GetInstanceID() != _targetButtonInstanceId) return;

                float now = Time.unscaledTime;
                if (now - _lastButtonPressTime < 0.35f) return;
                _lastButtonPressTime = now;

                if (_transitionRunning)
                {
                    if (DebugLog)
                        MelonLogger.Msg("[WinRoomScript] Button press ignored because transition is already running.");
                    return;
                }

                WinRoomChoice choice = GetDialChoice();
                MelonLogger.Msg("[WinRoomScript] WinRoom button pressed. Dial choice=" + choice);

                WinRoomScript inst = GetFreshInstance();
                if (ReferenceEquals(inst, null)) return;

                if (inst._buttonRoutine != null)
                {
                    MelonCoroutines.Stop(inst._buttonRoutine);
                    inst._buttonRoutine = null;
                }

                inst._buttonRoutine = MelonCoroutines.Start(inst.CoHandleButtonChoice(choice));
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[WinRoomScript] HandleWinRoomButtonPressed failed: " + e);
            }
        }

        [HideFromIl2Cpp]
        IEnumerator CoHandleButtonChoice(WinRoomChoice choice)
        {
            _transitionRunning = true;

            yield return new WaitForSeconds(0.35f);

            if (choice == WinRoomChoice.ReturnToVan)
                yield return CoReturnToVan();
            else
                yield return CoReplayMission();

            _buttonRoutine = null;
        }

        [HideFromIl2Cpp]
        IEnumerator CoReturnToVan()
        {
            MelonLogger.Msg("[WinRoomScript] Loading Van from WinRoom button.");
            yield return SceneManager.LoadSceneAsync("Van", LoadSceneMode.Single);
            _transitionRunning = false;
        }

        [HideFromIl2Cpp]
        IEnumerator CoReplayMission()
        {
            MelonLogger.Msg("[WinRoomScript] Replaying mission from WinRoom button.");

            LevelUtil.HellenKeller(true);
            yield return SceneManager.LoadSceneAsync("Van", LoadSceneMode.Single);

            LevelUtil.HellenKeller(true);
            yield return new WaitForSeconds(0.5f);

            LevelUtil.HellenKeller(true);
            MyMod._RequestLoad();

            _transitionRunning = false;
        }

        enum WinRoomChoice
        {
            ReturnToVan,
            ReplayMission
        }

        static WinRoomChoice GetDialChoice()
        {
            float current = GetDialRotation();

            float distReturn = Mathf.Abs(Mathf.DeltaAngle(current, ReturnToVanRotation));
            float distReplay = Mathf.Abs(Mathf.DeltaAngle(current, ReplayMissionRotation));

            WinRoomChoice choice = (distReturn <= distReplay) ? WinRoomChoice.ReturnToVan : WinRoomChoice.ReplayMission;

            if (DebugLog)
            {
                MelonLogger.Msg("[WinRoomScript] Dial rotation=" + current.ToString("0.0") +
                                " distReturn=" + distReturn.ToString("0.0") +
                                " distReplay=" + distReplay.ToString("0.0") +
                                " => " + choice);
            }

            return choice;
        }

        static float GetDialRotation()
        {
            try
            {
                GameObject dialRoot = GameObject.Find(DialName);
                if (ReferenceEquals(dialRoot, null))
                {
                    MelonLogger.Warning("[WinRoomScript] Dial root not found. Defaulting to ReturnToVan.");
                    return ReturnToVanRotation;
                }

                Transform rootTf = dialRoot.transform;
                if (ReferenceEquals(rootTf, null) || rootTf.childCount <= 0)
                {
                    MelonLogger.Warning("[WinRoomScript] Dial root has no child 0. Defaulting to ReturnToVan.");
                    return ReturnToVanRotation;
                }

                Transform dialChild = rootTf.GetChild(0);
                if (ReferenceEquals(dialChild, null))
                {
                    MelonLogger.Warning("[WinRoomScript] Dial child 0 is null. Defaulting to ReturnToVan.");
                    return ReturnToVanRotation;
                }

                RotationalMotion rm = dialChild.gameObject.GetComponent<RotationalMotion>();
                if (ReferenceEquals(rm, null))
                {
                    MelonLogger.Warning("[WinRoomScript] Dial child has no RotationalMotion. Defaulting to ReturnToVan.");
                    return ReturnToVanRotation;
                }

                return NormalizeAngle(rm._currentRotation);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[WinRoomScript] GetDialRotation failed. Defaulting to ReturnToVan. " + e.Message);
                return ReturnToVanRotation;
            }
        }

        static float NormalizeAngle(float a)
        {
            while (a < 0f) a += 360f;
            while (a >= 360f) a -= 360f;
            if (Mathf.Abs(a) < 0.001f) a = 360f;
            return a;
        }

        static void RefreshWinRoomTimes()
        {
            try
            {
                if (SaveManager.Current == null)
                    SaveManager.Load();

                ModSaveData data = SaveManager.Current;
                if (data == null) return;

                SetChildTmpText("SuccessTime", 0, FormatMinutesSeconds(data.LastTimeSeconds));
                SetChildTmpText("RecordTime", 0, FormatMinutesSeconds(data.BestTimeSeconds));
                SetDirectTmpText("SpeedrunTime", SpeedrunTargetDisplay);

                bool beatSpeedrun = data.BestTimeSeconds > 0f && data.BestTimeSeconds < SpeedrunTargetSeconds;
                SetSpriteEnabled("Checkmark 7", beatSpeedrun);
            }
            catch (Exception e)
            {
                if (DebugLog)
                    MelonLogger.Warning("[WinRoomScript] RefreshWinRoomTimes failed: " + e.Message);
            }
        }

        static string FormatMinutesSeconds(float seconds)
        {
            if (seconds < 0f)
                return "--:--";

            int totalSeconds = Mathf.FloorToInt(seconds);
            int minutes = totalSeconds / 60;
            int sec = totalSeconds - (minutes * 60);

            return minutes.ToString() + ":" + sec.ToString("00");
        }

        static void SetChildTmpText(string rootName, int childIndex, string text)
        {
            try
            {
                GameObject root = GameObject.Find(rootName);
                if (ReferenceEquals(root, null)) return;

                Transform tr = root.transform;
                if (ReferenceEquals(tr, null)) return;
                if (tr.childCount <= childIndex) return;

                Transform child = tr.GetChild(childIndex);
                if (ReferenceEquals(child, null)) return;

                TextMeshPro tmp = child.GetComponent<TextMeshPro>();
                if (ReferenceEquals(tmp, null)) return;

                if (tmp.text != text)
                    tmp.text = text;
            }
            catch (Exception e)
            {
                if (DebugLog)
                    MelonLogger.Warning("[WinRoomScript] SetChildTmpText failed for " + rootName + ": " + e.Message);
            }
        }

        static void SetDirectTmpText(string objectName, string text)
        {
            try
            {
                GameObject go = GameObject.Find(objectName);
                if (ReferenceEquals(go, null)) return;

                TextMeshPro tmp = go.GetComponent<TextMeshPro>();
                if (ReferenceEquals(tmp, null)) return;

                if (tmp.text != text)
                    tmp.text = text;
            }
            catch (Exception e)
            {
                if (DebugLog)
                    MelonLogger.Warning("[WinRoomScript] SetDirectTmpText failed for " + objectName + ": " + e.Message);
            }
        }

        static void SetSpriteEnabled(string objectName, bool enabled)
        {
            try
            {
                GameObject go = GameObject.Find(objectName);
                if (ReferenceEquals(go, null)) return;

                SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
                if (ReferenceEquals(sr, null)) return;

                if (sr.enabled != enabled)
                    sr.enabled = enabled;
            }
            catch (Exception e)
            {
                if (DebugLog)
                    MelonLogger.Warning("[WinRoomScript] SetSpriteEnabled failed for " + objectName + ": " + e.Message);
            }
        }

        static void StopMatchingAudioSources()
        {
            AudioSource[] sources = null;

            try { sources = Resources.FindObjectsOfTypeAll<AudioSource>(); }
            catch (Exception e)
            {
                MelonLogger.Warning("[WinRoomScript] AudioSource scan failed: " + e.Message);
                return;
            }

            if (sources == null) return;

            for (int i = 0; i < sources.Length; i++)
            {
                try
                {
                    AudioSource src = sources[i];
                    if (ReferenceEquals(src, null)) continue;
                    if (!src.isPlaying) continue;
                    if (!ShouldStop(src)) continue;

                    AudioClip clip = src.clip;
                    string clipName = SafeObjectName(clip, "null");
                    string path = SafePath(src.gameObject);

                    src.Stop();
                    src.clip = null;
                    src.mute = true;

                    if (DebugLog)
                        MelonLogger.Msg("[WinRoomScript] Stopped vanilla handler audio | clip=" + clipName + " | path=" + path);
                }
                catch (Exception e)
                {
                    if (DebugLog)
                        MelonLogger.Warning("[WinRoomScript] Skipped AudioSource due to exception: " + e.Message);
                }
            }
        }

        static bool ShouldStop(AudioSource src)
        {
            try
            {
                AudioClip clip = src.clip;
                GameObject go = src.gameObject;

                string clipName = SafeObjectName(clip, "");
                if (clipName.ToLowerInvariant().Contains("handlerdebrief"))
                    return false;

                string objName = SafeObjectName(go, "");
                string path = SafePath(go);

                string combined = (clipName + " " + objName + " " + path).ToLowerInvariant();

                bool kill = ContainsAny(combined, KillTokens);
                if (!kill) return false;

                bool avoid = ContainsAny(combined, AvoidTokens);
                if (!avoid) return true;

                if (combined.Contains("handler")) return true;
                if (combined.Contains("debrief")) return true;
                if (combined.Contains("dialogue")) return true;
                if (combined.Contains("dialog")) return true;
                if (combined.Contains("voiceover")) return true;
                if (combined.Contains("voice_over")) return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        static bool LooksLikeWinRoom(string sceneName)
        {
            string s = (sceneName ?? "").ToLowerInvariant();
            return ContainsAny(s, WinRoomSceneNameTokens);
        }

        static bool ContainsAny(string haystack, string[] tokens)
        {
            if (string.IsNullOrEmpty(haystack) || tokens == null) return false;

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (string.IsNullOrEmpty(token)) continue;
                if (haystack.Contains(token.ToLowerInvariant()))
                    return true;
            }

            return false;
        }

        static string SafeObjectName(UnityEngine.Object obj, string fallback)
        {
            try
            {
                if (ReferenceEquals(obj, null)) return fallback;
                return obj.name ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }

        static string SafePath(GameObject go)
        {
            try
            {
                if (ReferenceEquals(go, null)) return "null";

                string path = go.name ?? "unnamed";
                Transform t = go.transform;

                while (!ReferenceEquals(t, null))
                {
                    Transform p = t.parent;
                    if (ReferenceEquals(p, null)) break;

                    path = (p.name ?? "unnamed") + "/" + path;
                    t = p;
                }

                return path;
            }
            catch
            {
                return "<path failed>";
            }
        }

        [HarmonyPatch]
        static class WinRoomButtonPatch
        {
            [HarmonyPatch(typeof(Button), "PressButton")]
            [HarmonyPrefix]
            static bool PressButton_Prefix(Button __instance)
            {
                try
                {
                    if (ReferenceEquals(__instance, null)) return true;
                    if (_targetButtonInstanceId != 0 && __instance.gameObject.GetInstanceID() == _targetButtonInstanceId)
                    {
                        HandleWinRoomButtonPressed(__instance);

                        if (BlockDefaultButtonPress)
                        {
                            MelonLogger.Warning("[WinRoomScript] Blocking default Button.PressButton on WinRoom button.");
                            return false;
                        }
                    }
                }
                catch (Exception e)
                {
                    MelonLogger.Warning("[WinRoomScript] PressButton_Prefix failed: " + e);
                }

                return true;
            }
        }
    }
}
