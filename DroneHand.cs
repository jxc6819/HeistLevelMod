using SG.Phoenix.Assets.Code.InputManagement;
using System.Collections.Generic;
using UnhollowerRuntimeLib;
using UnityEngine;
using System;
using UnhollowerBaseLib.Attributes;
using MelonLoader;
using SG.Phoenix.Assets.Code.Interactables;
using HarmonyLib;

namespace IEYTD_Mod2Code
{
    public class DroneHand : MonoBehaviour
    {
        public DroneHand(IntPtr ptr) : base(ptr) { }
        public DroneHand() : base(ClassInjector.DerivedConstructorPointer<DroneHand>())
            => ClassInjector.DerivedConstructorBody(this);

        Transform hand;
        VRHandInput input;

        bool grabbing = false;
        public bool _launching = false;
        List<GameObject> hitbox = new List<GameObject>();

        IDroneGrabbable currentGrab;
        char handSide;
        public GameObject[] fingers = new GameObject[3];
        public Vector3[] fingerRot = new Vector3[3];
        GameObject root;
        public TelekinesisHandState tkState;
        NormalHandState nState;
        Transform tkPos;
        public DronePickUp holding;
        RocketDriver rocket;

        public bool _delayDrop = false;
        bool _prevInputPressed = false;
        bool _prevGripPressed = false;
        bool _prevTriggerPressed = false;
        int _prevLaunchPressSignal = 0;
        bool _wasTkEnabled = false;
        bool _blockLaunchUntilInputRelease = false;
        bool _droneModeEntryChecked = false;
        int _launchPressSignalAtDroneModeEntry = 0;

        CapsuleCollider _mainCol;
        object _activeMotionRoutine = null;
        Vector3 _lastLaunchWorldPos;
        bool _hasLastLaunchWorldPos = false;

        Transform _savedMotionParent;
        Vector3 _savedMotionLocalPos;
        Quaternion _savedMotionLocalRot;
        Vector3 _savedMotionLocalScale;
        bool _hasSavedMotionPose = false;

        public string launchIgnoreObjectName = "SaferoomHB";
        public int launchIgnoreBoxColliderIndex = 1;

        static Collider _cachedLaunchIgnoreCollider = null;
        static string _cachedLaunchIgnoreObjectName = null;
        static int _cachedLaunchIgnoreBoxColliderIndex = -1;
        bool _launchIgnoringGlass = false;
        bool _launchIgnoredColliderPrevEnabled = true;
        public LoopingSfx rocketLoop;

        GameObject _guardObj;
        GuardReactionDriver _guardReaction;
        bool _brokeGlassThisLaunch = false;
        bool _resolvedGuardReactionThisLaunch = false;
        readonly List<Collider> _tempIgnoredGlassColliders = new List<Collider>();
        readonly List<Collider> _tempIgnoredLaunchHostColliders = new List<Collider>();
        public bool extendFreeLaunchPastGlass = true;
        public float freeLaunchGlassContinueDistance = 2.5f;
        public float minAgentAvatarLaunchHitSpeed = 1.15f;

        public float immediateLaunchBlockDistance = 0.16f;
        public float immediateLaunchBlockRadius = 0.04f;

        public string GripSoundName = "DroneHandGrip.ogg";
        public float CloseSoundPitch = 1.0f;
        public float OpenSoundPitch = 1.28f;

        void Start()
        {
            hand = gameObject.transform;

            if (gameObject.name.Contains("Right"))
            {
                root = GameObject.Find("RightHandRoot");
                input = root.GetComponent<VRHandInput>();
                handSide = 'R';
                Keycard.droneHandR = this;
                rocket = GameObject.Find("BotRightHandRocket").GetComponent<RocketDriver>();
            }
            else
            {
                root = GameObject.Find("LeftHandRoot");
                input = root.GetComponent<VRHandInput>();
                handSide = 'L';
                Keycard.droneHandL = this;
                rocket = GameObject.Find("BotLeftHandRocket").GetComponent<RocketDriver>();
            }

            tkState = root.GetComponent<TelekinesisHandState>();
            nState = root.GetComponent<NormalHandState>();
            tkPos = root.transform.GetChild(2);

            gameObject.AddComponent<Rigidbody>().isKinematic = true;
            registerFingers();

            rocketLoop = gameObject.AddComponent<LoopingSfx>();
            rocketLoop.InitAndPlay("rocketloop.mp3");
            rocketLoop.TurnOff();

            _mainCol = GetComponent<CapsuleCollider>();
            if (_mainCol != null)
                _mainCol.isTrigger = true;

            _guardObj = GameObject.Find("Guard");
            RefreshGuardReactionReference();
            SyncLaunchInputState("Start");

        }

        void OnEnable()
        {
            if (rocketLoop != null) rocketLoop.TurnOff();

            _droneModeEntryChecked = false;

            SyncLaunchInputState("OnEnable");
        }

        void Update()
        {
            bool inputPressed = (input != null && input.IsAnyInputPressed());

            bool pressedThisFrame = inputPressed && !_prevInputPressed;
            bool releasedThisFrame = !inputPressed && _prevInputPressed;

            bool gripPressed = IsGripPressed();
            bool triggerPressed = IsTriggerPressed();

            int launchPressSignal = GetSchellLaunchPressSignal();
            bool gripPressedThisFrame = (launchPressSignal != _prevLaunchPressSignal);
            bool triggerPressedThisFrame = false;

            _prevInputPressed = inputPressed;
            _prevGripPressed = gripPressed;
            _prevTriggerPressed = triggerPressed;
            _prevLaunchPressSignal = launchPressSignal;

            if (_delayDrop && inputPressed) _delayDrop = false;

            bool tkEnabled = (tkState != null && tkState.enabled);
            if (tkEnabled && inputPressed && DropInvalidDroneTkPickupIfNeeded())
            {

                pressedThisFrame = false;
                releasedThisFrame = false;
                gripPressedThisFrame = false;
                triggerPressedThisFrame = false;
            }

            if (tkEnabled && !_droneModeEntryChecked)
            {

                _droneModeEntryChecked = true;
                _blockLaunchUntilInputRelease = true;
                _launchPressSignalAtDroneModeEntry = launchPressSignal;

                _prevInputPressed = inputPressed;
                _prevLaunchPressSignal = launchPressSignal;

                pressedThisFrame = false;
                gripPressedThisFrame = false;
                triggerPressedThisFrame = false;

                MelonLogger.Msg("[DroneHand] Drone mode TK entry detected -> blocking stale launch signal until fresh press");
            }

            if (_blockLaunchUntilInputRelease)
            {
                bool freshLaunchPressAfterEntry = (launchPressSignal != _launchPressSignalAtDroneModeEntry);
                if (freshLaunchPressAfterEntry)
                {
                    _blockLaunchUntilInputRelease = false;
                    MelonLogger.Msg("[DroneHand] Fresh launch press after drone mode TK entry -> rocket launch re-armed");

                }
                else
                {
                    pressedThisFrame = false;
                    gripPressedThisFrame = false;
                    triggerPressedThisFrame = false;
                }
            }

            bool carryingPickup = (holding != null);
            bool launchedHeldThisFrame = false;

            if (carryingPickup && !_launching && tkEnabled)
            {
                if (DidPressHeldLaunchInput(gripPressedThisFrame, triggerPressedThisFrame, tkEnabled))
                {
                    MelonLogger.Msg("[DroneHand] Held-item launch input detected");
                    launchHand();
                    launchedHeldThisFrame = true;
                }
            }

            if (!launchedHeldThisFrame)
            {
                if (!grabbing && holding == null && !_launching)
                {
                    if (tkEnabled)
                    {

                        if ((gripPressedThisFrame || triggerPressedThisFrame) && !_blockLaunchUntilInputRelease)
                            launchHand();
                    }
                    else if (pressedThisFrame)
                    {
                        BeginGrab();
                    }
                }
                else if ((grabbing || holding != null) && !_launching && releasedThisFrame)
                {
                    if (!_delayDrop)
                    {
                        if (holding != null)
                        {
                            if (tkEnabled)
                                MelonLogger.Msg("[DroneHand] Release edge while holding DronePickUp and TK is enabled -> forcing custom drop path");

                            EndGrab();
                        }
                        else if (!tkEnabled)
                        {
                            EndGrab();
                        }
                    }
                }
            }

            if (grabbing && currentGrab != null && !_launching)
            {
                currentGrab.OnDroneGrabUpdate(this);
            }

            _wasTkEnabled = tkEnabled;
        }

        void SyncLaunchInputState(string reason)
        {
            bool inputPressed = false;
            try { inputPressed = (input != null && input.IsAnyInputPressed()); } catch { inputPressed = false; }

            _prevInputPressed = inputPressed;
            _prevGripPressed = IsGripPressed();
            _prevTriggerPressed = IsTriggerPressed();
            _prevLaunchPressSignal = GetSchellLaunchPressSignal();
            _wasTkEnabled = (tkState != null && tkState.enabled);

            if (_wasTkEnabled && !_droneModeEntryChecked)
            {
                _droneModeEntryChecked = true;
                _blockLaunchUntilInputRelease = true;
                _launchPressSignalAtDroneModeEntry = _prevLaunchPressSignal;
                MelonLogger.Msg("[DroneHand] " + reason + " saw TK already enabled -> blocking stale launch signal until fresh press");
            }
        }

        bool DidPressHeldLaunchInput(
            bool gripPressedThisFrame,
            bool triggerPressedThisFrame,
            bool tkEnabled)
        {
            if (!tkEnabled)
                return false;

            bool pressedLaunchButton = gripPressedThisFrame || triggerPressedThisFrame;
            if (!pressedLaunchButton)
                return false;

            return GetThumbstickForwardAmount() > 0.35f;
        }

        bool DropInvalidDroneTkPickupIfNeeded()
        {
            if (input == null)
                return false;

            PickUp badPickUp = null;

            try
            {
                var allPickups = Resources.FindObjectsOfTypeAll<PickUp>();
                if (allPickups == null)
                    return false;

                for (int i = 0; i < allPickups.Length; i++)
                {
                    PickUp pu = allPickups[i];
                    if (pu == null) continue;
                    if (!pu.enabled) continue;
                    if (!pu.isHeld) continue;

                    VRHandInput heldHand = null;
                    try { heldHand = pu.heldHand; } catch { heldHand = null; }
                    if (heldHand == null || heldHand != input)
                        continue;

                    bool heldByTk = false;
                    try { heldByTk = heldHand.TelekinesisEnabled; } catch { heldByTk = false; }
                    if (!heldByTk)
                        continue;

                    if (HasDronePickUpComponent(pu))
                        continue;

                    badPickUp = pu;
                    break;
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[DroneHand] Invalid pickup guard scan failed: " + e);
                return false;
            }

            if (badPickUp == null)
                return false;

            MelonLogger.Msg("[DroneHand] Dropping TK object without DronePickUp: " + badPickUp.gameObject.name);

            try { input.ReleaseHeldObject(); }
            catch
            {
                try
                {
                    if (badPickUp.heldHand != null)
                        badPickUp.heldHand.ReleaseHeldObject();
                }
                catch { }
            }

            Rigidbody rb = null;
            try { rb = badPickUp.GetComponent<Rigidbody>(); } catch { rb = null; }
            if (rb == null)
            {
                try { rb = badPickUp.GetComponentInChildren<Rigidbody>(true); } catch { rb = null; }
            }

            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            return true;
        }

        bool HasDronePickUpComponent(PickUp pu)
        {
            if (pu == null)
                return false;

            try
            {
                if (pu.GetComponent<DronePickUp>() != null) return true;
                if (pu.GetComponentInParent<DronePickUp>() != null) return true;
                if (pu.GetComponentInChildren<DronePickUp>(true) != null) return true;
            }
            catch { }

            return false;
        }

        int GetSchellLaunchPressSignal()
        {
            return DroneHandSchellInputBridge.GetPressSignal(input);
        }

        float GetThumbstickForwardAmount()
        {
            try
            {
                if (input != null)
                    return input.TelekinesisVector.y;
            }
            catch { }

            return 0f;
        }

        bool IsGripPressed()
        {
            try
            {
                return input != null && input.IsAnyInputPressed();
            }
            catch
            {
                return false;
            }
        }

        bool IsTriggerPressed()
        {

            return false;
        }

        void BeginGrab()
        {
            grabbing = true;
            currentGrab = FindBestGrabbable();
            handAnimation(true);
            PlayGripSound(false);

            if (currentGrab != null)
                currentGrab.OnDroneGrabBegin(this);

            CatchStateGlitch();
        }

        void EndGrab()
        {
            grabbing = false;
            handAnimation(false);
            PlayGripSound(true);

            if (holding != null)
            {
                holding.isReleased(this);
                holding = null;
            }

            if (currentGrab != null)
            {
                currentGrab.OnDroneGrabEnd(this);
                currentGrab = null;
            }

            CatchStateGlitch();
        }

        void CatchStateGlitch()
        {
            if (tkState != null && nState != null && tkState.enabled && nState.enabled)
                tkState.enabled = false;
        }

        void PlayGripSound(bool opening)
        {
            if (string.IsNullOrEmpty(GripSoundName)) return;

            try
            {
                AudioSource src = AudioUtil.PlayAt(GripSoundName, transform.position);
                if (src != null)
                    src.pitch = opening ? OpenSoundPitch : CloseSoundPitch;
            }
            catch { }
        }

        void handAnimation(bool _grabbing)
        {
            float duration = 0.25f;

            for (int i = 0; i < 3; i++)
            {
                GameObject finger = fingers[i];
                Transform fingerT = finger.transform;
                Vector3 target;

                if (!_grabbing)
                {
                    fingerT.localRotation = Quaternion.Euler(Vector3.zero);
                    target = fingerRot[i];
                }
                else
                {
                    fingerT.localRotation = Quaternion.Euler(fingerRot[i]);
                    target = Vector3.zero;
                }

                MelonCoroutines.Start(RotateRoutine(fingerT, target, duration));
            }
        }

        void launchHand()
        {
            if (_launching) return;

            float maxSpeed = 10f;
            float accelTime = 0.20f;
            float minFrac = 0.35f;

            DroneRotationalMotion rm;
            RaycastHit rotHit;
            if (TryFindLaunchRotMotion(out rm, out rotHit))
            {
                AudioUtil.PlayAt("rocketstart.wav", transform.position);
                StartTrackedRoutine(Co_LaunchHand_ToRotationalMotion(transform, rm, rotHit, maxSpeed, accelTime, minFrac));
                return;
            }

            DronePullMotion pm;
            RaycastHit pullHit;
            if (TryFindLaunchPullMotion(out pm, out pullHit))
            {
                AudioUtil.PlayAt("rocketstart.wav", transform.position);
                StartTrackedRoutine(Co_LaunchHand_ToPullMotion(transform, pm, pullHit, maxSpeed, accelTime, minFrac));
                return;
            }

            Collider immediateBlocker;
            Vector3 immediateBlockPoint;
            Vector3 freeTargetWorldPos = (tkPos != null) ? tkPos.position : (transform.position + transform.forward);
            if (TryFindImmediateLaunchBlock(transform.position, freeTargetWorldPos, out immediateBlocker, out immediateBlockPoint))
            {
                MelonLogger.Msg($"[DroneHand] Free rocket blocked immediately before launch sound by '{immediateBlocker.name}' at {immediateBlockPoint}");
                return;
            }

            AudioUtil.PlayAt("rocketstart.wav", transform.position);
            StartTrackedRoutine(Co_MoveHand_DetachAccel(transform, tkPos, maxSpeed, accelTime, minFrac));
            rocketLoop.TurnOn();
        }

        [HideFromIl2Cpp]
        System.Collections.IEnumerator ToggleLoop(bool on, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (on) rocketLoop.TurnOn();
            else rocketLoop.TurnOff();
        }

        void StartTrackedRoutine(System.Collections.IEnumerator routine)
        {
            if (routine == null)
                return;

            if (_activeMotionRoutine != null)
            {
                try { MelonCoroutines.Stop(_activeMotionRoutine); } catch { }
                _activeMotionRoutine = null;
            }

            _activeMotionRoutine = MelonCoroutines.Start(Co_TrackedRoutine(routine));
        }

        public void CancelActiveMotionToRestPose()
        {
            if (_activeMotionRoutine != null)
            {
                try { MelonCoroutines.Stop(_activeMotionRoutine); } catch { }
                _activeMotionRoutine = null;
            }

            if (currentGrab != null)
            {
                try { currentGrab.OnDroneGrabEnd(this); } catch { }
                currentGrab = null;
            }

            SetLaunchGlassIgnore(false);
            FinalizeMotionState("CancelActiveMotionToRestPose");
        }

        void SaveMotionRestorePose(Transform t)
        {
            if (t == null) return;

            _savedMotionParent = t.parent;
            _savedMotionLocalPos = t.localPosition;
            _savedMotionLocalRot = t.localRotation;
            _savedMotionLocalScale = t.localScale;
            _hasSavedMotionPose = true;
        }

        void RestoreSavedMotionPose(Transform t)
        {
            if (t == null || !_hasSavedMotionPose) return;

            try
            {
                t.SetParent(_savedMotionParent, false);
                t.localPosition = _savedMotionLocalPos;
                t.localRotation = _savedMotionLocalRot;
                t.localScale = _savedMotionLocalScale;
            }
            catch { }

            _hasSavedMotionPose = false;
        }

        void FinalizeMotionState(string reason)
        {
            try
            {
                if (rocket != null)
                    rocket.Disable();
            }
            catch { }

            SetMainColliderEnabled(true);
            SetLaunchGlassIgnore(false);
            ResolveGuardReactionMissIfNeeded();
            ClearTempIgnoredGlassColliders();
            ClearTempIgnoredLaunchHostColliders();
            RestoreSavedMotionPose(transform);

            if (holding != null)
            {
                try
                {

                    Transform heldT = holding.transform;
                    heldT.position = transform.position;
                    heldT.rotation = transform.rotation;
                    heldT.SetParent(transform, false);
                    heldT.localPosition = holding.localHoldPos;
                    heldT.localRotation = holding.localHoldRot;

                    Rigidbody heldRb = holding.GetComponent<Rigidbody>();
                    if (heldRb != null)
                    {
                        heldRb.velocity = Vector3.zero;
                        heldRb.angularVelocity = Vector3.zero;
                        heldRb.useGravity = false;
                        heldRb.isKinematic = true;
                        heldRb.constraints = RigidbodyConstraints.FreezeAll;
                    }

                    if (holding.pickUp != null)
                        holding.pickUp.enabled = false;
                }
                catch { }
            }

            _delayDrop = false;
            _hasLastLaunchWorldPos = false;
            _launching = false;
            rocketLoop.TurnOff();
            AudioUtil.PlayAt("rocketend.wav", transform.position);
            grabbing = (holding != null);

            _prevInputPressed = (input != null && input.IsAnyInputPressed());
            _prevGripPressed = IsGripPressed();
            _prevTriggerPressed = IsTriggerPressed();
            _prevLaunchPressSignal = GetSchellLaunchPressSignal();
            _wasTkEnabled = (tkState != null && tkState.enabled);

            if (holding != null) handAnimation(true);
            else handAnimation(false);

            float distToTk = -1f;
            try
            {
                if (tkPos != null)
                    distToTk = Vector3.Distance(transform.position, tkPos.position);
            }
            catch { }

            MelonLogger.Msg($"[DroneHand] FinalizeMotionState ({reason}) holding={(holding != null)} pos={transform.position} distToTk={distToTk:F3}");
        }

        [HideFromIl2Cpp]
        private System.Collections.IEnumerator Co_TrackedRoutine(System.Collections.IEnumerator routine)
        {
            try
            {
                while (routine.MoveNext())
                    yield return routine.Current;
            }
            finally
            {
                _activeMotionRoutine = null;
            }
        }

        [HideFromIl2Cpp]
        private System.Collections.IEnumerator RotateRoutine(Transform target, Vector3 targetEuler, float duration)
        {
            Quaternion startRot = target.localRotation;
            Quaternion endRot = Quaternion.Euler(targetEuler);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                target.localRotation = Quaternion.Lerp(startRot, endRot, t);
                yield return null;
            }

            target.localRotation = endRot;
        }

        [HideFromIl2Cpp]
        private System.Collections.IEnumerator Co_MoveHand_DetachAccel(
            Transform hand,
            Transform targetTf,
            float maxSpeed,
            float accelTime = 0.12f,
            float minSpeedFrac = 0.15f)
        {
            if (hand == null || targetTf == null || maxSpeed <= 0f)
                yield break;
            yield return new WaitForSeconds(0.1f);

            Vector3 targetWorldPos = targetTf.position;

            grabbing = true;
            _launching = true;
            BeginLaunchImpactTracking(hand.position);
            SetLaunchGlassIgnore(true);

            bool carryingItem = (holding != null);

            rocket.Enable();

            if (carryingItem)
            {
                SetMainColliderEnabled(false);
                MelonLogger.Msg("[DroneHand] Free rocket w/held item -> collider OFF");
            }

            MelonLogger.Msg($"[DroneHand] Free rocket start -> start={hand.position} target={targetWorldPos} dist={Vector3.Distance(hand.position, targetWorldPos):F3} holding={(holding != null)}");

            Transform oldParent = hand.parent;
            Vector3 oldLocalPos = hand.localPosition;
            Quaternion oldLocalRot = hand.localRotation;
            Vector3 oldLocalScale = hand.localScale;
            SaveMotionRestorePose(hand);

            Vector3 startWorldPos = hand.position;
            Quaternion fixedWorldRot = hand.rotation;

            if (accelTime < 0.0001f) accelTime = 0.0001f;
            minSpeedFrac = Mathf.Clamp01(minSpeedFrac);

            try
            {
                hand.SetParent(null, true);

                yield return Co_MoveWithAccel(hand, startWorldPos, targetWorldPos, fixedWorldRot, maxSpeed, accelTime, minSpeedFrac);

                Vector3 returnStartPos = targetWorldPos;
                if (!carryingItem && extendFreeLaunchPastGlass && _brokeGlassThisLaunch && freeLaunchGlassContinueDistance > 0.001f)
                {
                    Vector3 extendDir = (targetWorldPos - startWorldPos);
                    if (extendDir.sqrMagnitude < 0.0001f && tkPos != null)
                        extendDir = tkPos.forward;
                    if (extendDir.sqrMagnitude < 0.0001f)
                        extendDir = hand.forward;

                    extendDir.Normalize();
                    Vector3 extendedTargetWorldPos = targetWorldPos + (extendDir * freeLaunchGlassContinueDistance);
                    MelonLogger.Msg($"[DroneHand] Free rocket glass continue -> baseTarget={targetWorldPos} extendedTarget={extendedTargetWorldPos} extra={freeLaunchGlassContinueDistance:F3}");
                    yield return Co_MoveWithAccel(hand, targetWorldPos, extendedTargetWorldPos, fixedWorldRot, maxSpeed, accelTime, minSpeedFrac);
                    returnStartPos = extendedTargetWorldPos;
                }

                rocket.Enable();
                yield return Co_MoveWithAccel(hand, returnStartPos, startWorldPos, fixedWorldRot, maxSpeed, accelTime, minSpeedFrac);
            }
            finally
            {
                if (carryingItem)
                    MelonLogger.Msg("[DroneHand] Free rocket w/held item -> collider ON");

                FinalizeMotionState("FreeRocketFinally");
            }
        }

        [HideFromIl2Cpp]
        private System.Collections.IEnumerator Co_LaunchHand_ToRotationalMotion(
            Transform hand,
            DroneRotationalMotion rm,
            RaycastHit hit,
            float maxSpeed,
            float accelTime = 0.12f,
            float minSpeedFrac = 0.15f)
        {
            if (hand == null || rm == null || maxSpeed <= 0f)
                yield break;

            grabbing = true;
            _launching = true;
            BeginLaunchImpactTracking(hand.position);
            SetLaunchGlassIgnore(true);
            handAnimation(true);

            rocket.Enable();
            SetMainColliderEnabled(false);
            MelonLogger.Msg("[DroneHand] Rocket latch start -> collider OFF");

            Transform oldParent = hand.parent;
            Vector3 oldLocalPos = hand.localPosition;
            Quaternion oldLocalRot = hand.localRotation;
            Vector3 oldLocalScale = hand.localScale;
            SaveMotionRestorePose(hand);

            Vector3 startWorldPos = hand.position;
            Quaternion startWorldRot = hand.rotation;

            Vector3 latchWorldPos = GetSafeRMLatchWorldPos(rm, hit, startWorldPos);
            Quaternion latchWorldRot = startWorldRot;

            MelonLogger.Msg(
                $"[DroneHand] Latch pose -> obj='{rm.name}' latchPos={latchWorldPos} startPos={startWorldPos} dist={Vector3.Distance(startWorldPos, latchWorldPos):F3}"
            );

            if (accelTime < 0.0001f) accelTime = 0.0001f;
            minSpeedFrac = Mathf.Clamp01(minSpeedFrac);

            try
            {
                hand.SetParent(null, true);

                yield return Co_MoveWithAccel(hand, startWorldPos, latchWorldPos, latchWorldRot, maxSpeed, accelTime, minSpeedFrac);

                hand.position = latchWorldPos;
                hand.rotation = latchWorldRot;

                if (rm.rocketArrivePause > 0f)
                    yield return new WaitForSeconds(rm.rocketArrivePause);

                currentGrab = rm;
                rm.BeginRocketGrab(this);
                rm.SetRocketTargetAngle(rm.GetRocketTargetAngle());

                Transform pivot = (rm.pivot != null) ? rm.pivot : rm.transform;
                Vector3 localHandPos = pivot.InverseTransformPoint(hand.position);
                Quaternion localHandRot = Quaternion.Inverse(pivot.rotation) * hand.rotation;

                while (!rm.IsAtRocketTarget())
                {
                    hand.position = pivot.TransformPoint(localHandPos);
                    hand.rotation = pivot.rotation * localHandRot;
                    yield return null;
                }

                hand.position = pivot.TransformPoint(localHandPos);
                hand.rotation = pivot.rotation * localHandRot;

                if (rm.rocketFinishPause > 0f)
                    yield return new WaitForSeconds(rm.rocketFinishPause);

                rm.EndRocketGrab();
                currentGrab = null;

                rocket.Enable();
                yield return Co_MoveWithAccel(hand, hand.position, startWorldPos, startWorldRot, maxSpeed, accelTime, minSpeedFrac);
            }
            finally
            {
                currentGrab = null;
                MelonLogger.Msg("[DroneHand] Rocket latch end -> collider ON");
                FinalizeMotionState("RotMotionFinally");
            }
        }

        [HideFromIl2Cpp]
        private System.Collections.IEnumerator Co_LaunchHand_ToPullMotion(
            Transform hand,
            DronePullMotion pm,
            RaycastHit hit,
            float maxSpeed,
            float accelTime = 0.12f,
            float minSpeedFrac = 0.15f)
        {
            if (hand == null || pm == null || maxSpeed <= 0f)
                yield break;

            grabbing = true;
            _launching = true;
            BeginLaunchImpactTracking(hand.position);
            SetLaunchGlassIgnore(true);
            handAnimation(true);

            rocket.Enable();
            SetMainColliderEnabled(false);
            MelonLogger.Msg("[DroneHand] Rocket pull start -> collider OFF");

            Transform oldParent = hand.parent;
            Vector3 oldLocalPos = hand.localPosition;
            Quaternion oldLocalRot = hand.localRotation;
            Vector3 oldLocalScale = hand.localScale;
            SaveMotionRestorePose(hand);

            Vector3 startWorldPos = hand.position;
            Quaternion startWorldRot = hand.rotation;

            Vector3 latchWorldPos = GetSafePullLatchWorldPos(pm, hit, startWorldPos);
            Quaternion latchWorldRot = startWorldRot;

            if (accelTime < 0.0001f) accelTime = 0.0001f;
            minSpeedFrac = Mathf.Clamp01(minSpeedFrac);

            try
            {
                hand.SetParent(null, true);

                yield return Co_MoveWithAccel(hand, startWorldPos, latchWorldPos, latchWorldRot, maxSpeed, accelTime, minSpeedFrac);

                hand.position = latchWorldPos;
                hand.rotation = latchWorldRot;

                if (pm.rocketArrivePause > 0f)
                    yield return new WaitForSeconds(pm.rocketArrivePause);

                currentGrab = pm;
                pm.BeginRocketGrab(this);

                Transform pivot = (pm.pivot != null) ? pm.pivot : pm.transform;
                Vector3 localHandPos = pivot.InverseTransformPoint(hand.position);
                Quaternion localHandRot = Quaternion.Inverse(pivot.rotation) * hand.rotation;

                float startPull = pm.PullDistance;
                float targetPull = Mathf.Max(pm.triggerDistance, pm.maxPullDistance);
                float pullSpeed = Mathf.Max(pm.rocketPullSpeed, 0.05f);
                float duration = Mathf.Max(0.05f, Mathf.Abs(targetPull - startPull) / pullSpeed);

                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime / duration;
                    float pull = Mathf.Lerp(startPull, targetPull, Mathf.SmoothStep(0f, 1f, t));
                    pm.SetRocketPullDistance(pull);

                    hand.position = pivot.TransformPoint(localHandPos);
                    hand.rotation = pivot.rotation * localHandRot;
                    yield return null;
                }

                pm.SetRocketPullDistance(targetPull);
                hand.position = pivot.TransformPoint(localHandPos);
                hand.rotation = pivot.rotation * localHandRot;

                if (pm.rocketFinishPause > 0f)
                    yield return new WaitForSeconds(pm.rocketFinishPause);

                pm.EndRocketGrab();
                currentGrab = null;

                rocket.Enable();
                yield return Co_MoveWithAccel(hand, hand.position, startWorldPos, startWorldRot, maxSpeed, accelTime, minSpeedFrac);
            }
            finally
            {
                currentGrab = null;
                MelonLogger.Msg("[DroneHand] Rocket pull end -> collider ON");
                FinalizeMotionState("PullMotionFinally");
            }
        }

        [HideFromIl2Cpp]
        private System.Collections.IEnumerator Co_MoveWithAccel(
            Transform hand,
            Vector3 from,
            Vector3 to,
            Quaternion fixedWorldRot,
            float maxSpeed,
            float accelTime,
            float minSpeedFrac)
        {
            float totalDist = Vector3.Distance(from, to);
            if (totalDist < 0.0001f)
            {
                hand.position = to;
                hand.rotation = fixedWorldRot;
                yield break;
            }

            float t = 0f;

            while (Vector3.Distance(hand.position, to) > 0.001f)
            {
                t += Time.deltaTime;

                float ramp = Mathf.Clamp01(t / accelTime);
                ramp = Mathf.SmoothStep(0f, 1f, ramp);

                float speedFrac = Mathf.Lerp(minSpeedFrac, 1f, ramp);
                float speed = maxSpeed * speedFrac;

                if (_launching)
                    UpdateLaunchImpactTracking(hand.position);

                hand.position = Vector3.MoveTowards(hand.position, to, speed * Time.deltaTime);
                hand.rotation = fixedWorldRot;

                yield return null;
            }

            rocket.Disable();

            hand.position = to;
            hand.rotation = fixedWorldRot;
            if (_launching)
                UpdateLaunchImpactTracking(hand.position);
        }

        bool TryFindImmediateLaunchBlock(Vector3 from, Vector3 target, out Collider blocker, out Vector3 blockPoint)
        {
            blocker = null;
            blockPoint = from;

            Vector3 dir = target - from;
            if (dir.sqrMagnitude < 0.000001f && tkPos != null)
                dir = tkPos.forward;
            if (dir.sqrMagnitude < 0.000001f)
                dir = transform.forward;
            if (dir.sqrMagnitude < 0.000001f)
                return false;

            dir.Normalize();

            float radius = Mathf.Clamp(immediateLaunchBlockRadius, 0.005f, 0.06f);
            float distance = Mathf.Clamp(immediateLaunchBlockDistance, 0.02f, 0.35f);
            float startOffset = Mathf.Min(0.035f, distance * 0.35f);

            Vector3 castOrigin = from + dir * startOffset;
            float castDistance = Mathf.Max(0.005f, distance - startOffset);

            RaycastHit[] hits = Physics.SphereCastAll(
                castOrigin,
                radius,
                dir,
                castDistance,
                ~0,
                QueryTriggerInteraction.Ignore
            );

            if (hits == null || hits.Length == 0)
                return false;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit h = hits[i];
                Collider c = h.collider;
                if (ShouldIgnoreImmediateLaunchBlocker(c))
                    continue;

                blocker = c;
                blockPoint = h.point;
                if (blockPoint == Vector3.zero)
                    blockPoint = c.ClosestPoint(castOrigin);

                return true;
            }

            return false;
        }

        bool ShouldIgnoreImmediateLaunchBlocker(Collider c)
        {
            if (c == null) return true;
            if (c.isTrigger) return true;
            if (_mainCol != null && c == _mainCol) return true;
            if (c.gameObject == gameObject) return true;
            if (c.transform.IsChildOf(transform)) return true;
            if (transform.IsChildOf(c.transform)) return true;

            Rigidbody ourRb = GetComponent<Rigidbody>();
            Rigidbody otherRb = c.attachedRigidbody;
            if (ourRb != null && otherRb != null && ourRb == otherRb) return true;

            if (IsPlayerOwnedLaunchBlocker(c))
                return true;

            string lowerName = (c.gameObject.name ?? "").ToLowerInvariant();
            if (lowerName.Contains("hand") || lowerName.Contains("finger") || lowerName.Contains("rocket") || lowerName.Contains("reticle"))
                return true;

            if (holding != null)
            {
                Transform heldTf = holding.transform;
                if (heldTf != null && (c.transform == heldTf || c.transform.IsChildOf(heldTf)))
                    return true;
            }

            GlassDriver gd = c.GetComponentInParent<GlassDriver>();
            if (gd != null)
                return true;

            Collider launchIgnore = ResolveLaunchIgnoreCollider();
            if (launchIgnore != null && c == launchIgnore)
                return true;

            return false;
        }

        bool IsPlayerOwnedLaunchBlocker(Collider c)
        {
            if (c == null) return false;

            try
            {
                if (c.GetComponentInParent<AgentAvatarDriver>() != null)
                    return true;
            }
            catch { }

            Transform ct = c.transform;

            try
            {
                if (root != null && ct != null && (ct == root.transform || ct.IsChildOf(root.transform)))
                    return true;
            }
            catch { }

            try
            {
                GameObject rig = GameObject.Find("VRRig");
                if (rig != null && ct != null && (ct == rig.transform || ct.IsChildOf(rig.transform)))
                    return true;
            }
            catch { }

            try
            {
                GameObject hmd = GameObject.Find("HMD");
                if (hmd != null && ct != null && (ct == hmd.transform || ct.IsChildOf(hmd.transform)))
                    return true;
            }
            catch { }

            try
            {
                GameObject rightRoot = GameObject.Find("RightHandRoot");
                if (rightRoot != null && ct != null && (ct == rightRoot.transform || ct.IsChildOf(rightRoot.transform)))
                    return true;

                GameObject leftRoot = GameObject.Find("LeftHandRoot");
                if (leftRoot != null && ct != null && (ct == leftRoot.transform || ct.IsChildOf(leftRoot.transform)))
                    return true;
            }
            catch { }

            Transform t = ct;
            while (t != null)
            {
                string n = (t.name ?? "").ToLowerInvariant();

                if (n == "vrrig" || n == "hmd" || n == "righthandroot" || n == "lefthandroot")
                    return true;

                if (n.Contains("agentavatar") || n.Contains("playeravatar") || n.Contains("playerbody") || n.Contains("bodyavatar"))
                    return true;

                t = t.parent;
            }

            return false;
        }

        Vector3 GetSafeRMLatchWorldPos(DroneRotationalMotion rm, RaycastHit hit, Vector3 startWorldPos)
        {
            Vector3 surfacePoint = hit.point;
            Vector3 surfaceNormal = hit.normal;

            if (hit.collider != null)
            {
                Ray preciseRay = new Ray(startWorldPos, ((tkPos != null) ? tkPos.forward : hand.forward).normalized);
                RaycastHit preciseHit;
                if (hit.collider.Raycast(preciseRay, out preciseHit, 15f) && preciseHit.collider != null)
                {
                    surfacePoint = preciseHit.point;
                    surfaceNormal = preciseHit.normal;
                }
                else
                {
                    Vector3 cp = hit.collider.ClosestPoint(startWorldPos);
                    if ((cp - startWorldPos).sqrMagnitude < 0.000001f)
                        cp = hit.collider.bounds.center;

                    surfacePoint = cp;

                    Vector3 n = (startWorldPos - surfacePoint);
                    surfaceNormal = (n.sqrMagnitude > 0.000001f) ? n.normalized : -((tkPos != null) ? tkPos.forward : hand.forward).normalized;
                }
            }

            Vector3 fallback = surfacePoint + surfaceNormal * Mathf.Max(rm.rocketHandClearance, 0.02f);
            return fallback;
        }

        Vector3 GetSafePullLatchWorldPos(DronePullMotion pm, RaycastHit hit, Vector3 startWorldPos)
        {
            Vector3 fallback;
            if (hit.collider != null)
            {
                Vector3 p = hit.point;
                if (p == Vector3.zero)
                    p = hit.collider.ClosestPoint(startWorldPos);
                fallback = p;
            }
            else fallback = pm.transform.position;

            Vector3 requested = fallback;
            try { requested = pm.GetRocketLatchWorldPos(hit); } catch { requested = fallback; }

            float hitDistFromStart = Vector3.Distance(startWorldPos, fallback);
            float requestedDist = Vector3.Distance(startWorldPos, requested);
            float requestedVsHit = Vector3.Distance(requested, fallback);

            if (requestedDist > hitDistFromStart + 0.75f || requestedVsHit > 0.5f)
            {
                MelonLogger.Msg($"[DroneHand] Pull latch fallback -> requested={requested} fallback={fallback}");
                return fallback;
            }

            return requested;
        }

        Collider ResolveLaunchIgnoreCollider()
        {
            if (string.IsNullOrEmpty(launchIgnoreObjectName))
                return null;

            if (_cachedLaunchIgnoreCollider != null
                && _cachedLaunchIgnoreObjectName == launchIgnoreObjectName
                && _cachedLaunchIgnoreBoxColliderIndex == launchIgnoreBoxColliderIndex)
            {
                return _cachedLaunchIgnoreCollider;
            }

            GameObject host = GameObject.Find(launchIgnoreObjectName);
            if (host == null)
            {
                MelonLogger.Warning($"[DroneHand] Launch ignore host '{launchIgnoreObjectName}' not found");
                return null;
            }

            BoxCollider[] cols = host.GetComponents<BoxCollider>();
            if (cols == null || cols.Length == 0)
            {
                MelonLogger.Warning($"[DroneHand] Launch ignore host '{launchIgnoreObjectName}' has no BoxColliders");
                return null;
            }

            if (launchIgnoreBoxColliderIndex < 0 || launchIgnoreBoxColliderIndex >= cols.Length)
            {
                MelonLogger.Warning($"[DroneHand] Launch ignore collider index {launchIgnoreBoxColliderIndex} invalid on '{launchIgnoreObjectName}' (count={cols.Length})");
                return null;
            }

            _cachedLaunchIgnoreCollider = cols[launchIgnoreBoxColliderIndex];
            _cachedLaunchIgnoreObjectName = launchIgnoreObjectName;
            _cachedLaunchIgnoreBoxColliderIndex = launchIgnoreBoxColliderIndex;
            return _cachedLaunchIgnoreCollider;
        }

        void SetLaunchGlassIgnore(bool ignore)
        {
            Collider glass = ResolveLaunchIgnoreCollider();
            if (glass == null)
                return;

            if (_launchIgnoringGlass == ignore)
                return;

            if (ignore)
            {
                _launchIgnoredColliderPrevEnabled = glass.enabled;
                glass.enabled = false;
                _launchIgnoringGlass = true;
                MelonLogger.Msg($"[DroneHand] Launch blocker DISABLED hand={gameObject.name} target={glass.name} idx={launchIgnoreBoxColliderIndex}");
            }
            else
            {
                glass.enabled = _launchIgnoredColliderPrevEnabled;
                _launchIgnoringGlass = false;
                MelonLogger.Msg($"[DroneHand] Launch blocker RESTORED hand={gameObject.name} target={glass.name} idx={launchIgnoreBoxColliderIndex} enabled={glass.enabled}");
            }
        }

        void RefreshGuardReactionReference()
        {
            if (_guardReaction != null)
                return;

            if (_guardObj == null)
                _guardObj = GameObject.Find("Guard");
            if (_guardObj == null)
                _guardObj = GameObject.Find("guard");

            if (_guardObj == null)
                return;

            _guardReaction = _guardObj.GetComponent<GuardReactionDriver>();
            if (_guardReaction == null)
                _guardReaction = _guardObj.GetComponentInChildren<GuardReactionDriver>(true);

            if (_guardReaction == null)
                MelonLogger.Msg("[DroneHand] GuardReactionDriver not found on guard root or children");
        }

        void BeginGuardReactionWindow()
        {
            RefreshGuardReactionReference();
            _brokeGlassThisLaunch = true;
            _resolvedGuardReactionThisLaunch = false;
        }

        void ResolveGuardReactionHit()
        {
            if (_resolvedGuardReactionThisLaunch) return;
            if (_guardReaction != null)
                _guardReaction.ReactToHit();
            _resolvedGuardReactionThisLaunch = true;
        }

        void ResolveGuardReactionMissIfNeeded()
        {
            RefreshGuardReactionReference();

            if (_brokeGlassThisLaunch && !_resolvedGuardReactionThisLaunch && _guardReaction != null)
            {
                MelonLogger.Msg("[DroneHand] Guard reaction MISS");
                _guardReaction.ReactToMiss();
            }

            _brokeGlassThisLaunch = false;
            _resolvedGuardReactionThisLaunch = false;
        }

        void ClearTempIgnoredGlassColliders()
        {
            if (_mainCol == null)
            {
                _tempIgnoredGlassColliders.Clear();
                return;
            }

            for (int i = 0; i < _tempIgnoredGlassColliders.Count; i++)
            {
                Collider c = _tempIgnoredGlassColliders[i];
                if (c == null) continue;
                Physics.IgnoreCollision(_mainCol, c, false);
            }

            _tempIgnoredGlassColliders.Clear();
        }

        void ClearTempIgnoredLaunchHostColliders()
        {
            if (_mainCol == null)
            {
                _tempIgnoredLaunchHostColliders.Clear();
                return;
            }

            for (int i = 0; i < _tempIgnoredLaunchHostColliders.Count; i++)
            {
                Collider c = _tempIgnoredLaunchHostColliders[i];
                if (c == null) continue;
                Physics.IgnoreCollision(_mainCol, c, false);
            }

            _tempIgnoredLaunchHostColliders.Clear();
        }

        void IgnoreGlassRootColliders(GlassDriver gd)
        {
            if (_mainCol == null || gd == null) return;

            Collider[] cols = gd.GetComponentsInChildren<Collider>(true);
            if (cols == null) return;

            for (int i = 0; i < cols.Length; i++)
            {
                Collider c = cols[i];
                if (c == null || c == _mainCol) continue;
                Physics.IgnoreCollision(_mainCol, c, true);
                if (!_tempIgnoredGlassColliders.Contains(c))
                    _tempIgnoredGlassColliders.Add(c);
            }
        }

        void IgnoreLaunchHostColliders()
        {
            if (_mainCol == null) return;

            GameObject host = GameObject.Find(launchIgnoreObjectName);
            if (host == null) return;

            Collider[] cols = host.GetComponentsInChildren<Collider>(true);
            if (cols == null) return;

            for (int i = 0; i < cols.Length; i++)
            {
                Collider c = cols[i];
                if (c == null || c == _mainCol) continue;
                Physics.IgnoreCollision(_mainCol, c, true);
                if (!_tempIgnoredLaunchHostColliders.Contains(c))
                    _tempIgnoredLaunchHostColliders.Add(c);
            }
        }

        public void NotifyGlassBroken(GlassDriver gd)
        {
            if (!_launching || gd == null) return;

            BeginGuardReactionWindow();
            gd.Break();
            IgnoreGlassRootColliders(gd);
            IgnoreLaunchHostColliders();
            MelonLogger.Msg("[DroneHand] Glass broken during launch -> continuing through ignored colliders");
        }

        bool TryHandleGuardReactionTrigger(Collider col)
        {
            if (!_launching || !_brokeGlassThisLaunch || _resolvedGuardReactionThisLaunch || col == null)
                return false;

            GuardReactionDriver grd = null;
            if (_guardObj != null && (col.gameObject == _guardObj || col.transform.IsChildOf(_guardObj.transform)))
            {
                if (_guardReaction == null)
                    RefreshGuardReactionReference();
                grd = _guardReaction;
            }

            if (grd == null)
                grd = col.GetComponentInParent<GuardReactionDriver>();

            if (grd == null)
                return false;

            if (_guardReaction == null)
                _guardReaction = grd;

            if (_guardObj == null && grd != null)
                _guardObj = grd.gameObject;

            if (grd != _guardReaction)
                return false;

            ResolveGuardReactionHit();
            return true;
        }

        void BeginLaunchImpactTracking(Vector3 startPos)
        {
            _lastLaunchWorldPos = startPos;
            _hasLastLaunchWorldPos = true;
        }

        void UpdateLaunchImpactTracking(Vector3 currentPos)
        {
            _lastLaunchWorldPos = currentPos;
            _hasLastLaunchWorldPos = true;
        }

        float GetLaunchImpactSpeed()
        {
            if (!_hasLastLaunchWorldPos)
                return 0f;

            float dt = Time.deltaTime;
            if (dt < 0.0001f)
                dt = 0.0001f;

            return Vector3.Distance(transform.position, _lastLaunchWorldPos) / dt;
        }

        Vector3 GetLaunchImpactDirection()
        {
            if (_hasLastLaunchWorldPos)
            {
                Vector3 delta = transform.position - _lastLaunchWorldPos;
                if (delta.sqrMagnitude > 0.000001f)
                    return delta.normalized;
            }

            if (tkPos != null)
                return tkPos.forward.normalized;

            return transform.forward.normalized;
        }

        bool TryHandleAgentAvatarLaunchHit(Collider col)
        {
            if (!_launching || col == null)
                return false;

            AgentAvatarDriver avatar = col.GetComponentInParent<AgentAvatarDriver>();
            if (avatar == null)
                return false;

            float impactSpeed = GetLaunchImpactSpeed();
            if (impactSpeed < minAgentAvatarLaunchHitSpeed)
            {
                MelonLogger.Msg("[DroneHand] Ignored AgentAvatar brush -> " + col.gameObject.name + " speed=" + impactSpeed.ToString("F2") + " min=" + minAgentAvatarLaunchHitSpeed.ToString("F2"));
                return false;
            }

            Vector3 impactDir = GetLaunchImpactDirection();
            avatar.NotifyDroneHandLaunchHit(impactDir, impactSpeed, gameObject.name);
            MelonLogger.Msg("[DroneHand] Launch hit AgentAvatar -> " + col.gameObject.name + " speed=" + impactSpeed.ToString("F2") + " dir=" + impactDir);
            return true;
        }

        void SetMainColliderEnabled(bool enabled)
        {
            if (_mainCol != null)
                _mainCol.enabled = enabled;
        }

        bool TryFindLaunchRotMotion(out DroneRotationalMotion bestRm, out RaycastHit bestHit)
        {
            bestRm = null;
            bestHit = default(RaycastHit);

            Transform rayTf = (tkPos != null) ? tkPos : hand;
            Vector3 origin = rayTf.position;
            Vector3 dir = rayTf.forward.normalized;

            RaycastHit[] hits = Physics.SphereCastAll(origin, 0.09f, dir, 15f, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
            {
                MelonLogger.Msg("[DroneHand] No RM target found for rocket launch");
                return false;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit h = hits[i];
                if (h.collider == null) continue;

                DroneRotationalMotion rm = h.collider.GetComponentInParent<DroneRotationalMotion>();
                if (rm == null || !rm.enabled) continue;

                RaycastHit preciseHit;
                Ray preciseRay = new Ray(origin, dir);
                if (h.collider.Raycast(preciseRay, out preciseHit, 15f) && preciseHit.collider != null)
                    h = preciseHit;
                else if (h.collider != null)
                {
                    Vector3 cp = h.collider.ClosestPoint(origin);
                    if ((cp - origin).sqrMagnitude > 0.000001f)
                    {
                        h.point = cp;
                        Vector3 n = (origin - cp);
                        h.normal = (n.sqrMagnitude > 0.000001f) ? n.normalized : -dir;
                        h.distance = Vector3.Distance(origin, cp);
                    }
                }

                bestRm = rm;
                bestHit = h;
                MelonLogger.Msg($"[DroneHand] Found RM target '{bestRm.name}' hitDist={bestHit.distance:F3} hitPoint={bestHit.point} hitNormal={bestHit.normal}");
                return true;
            }

            MelonLogger.Msg("[DroneHand] No RM target found for rocket launch");
            return false;
        }

        bool TryFindLaunchPullMotion(out DronePullMotion bestPm, out RaycastHit bestHit)
        {
            bestPm = null;
            bestHit = default(RaycastHit);

            Transform rayTf = (tkPos != null) ? tkPos : hand;
            Vector3 origin = rayTf.position;
            Vector3 dir = rayTf.forward.normalized;

            float maxDist = 8f;
            float radius = 0.06f;

            RaycastHit[] hits = Physics.SphereCastAll(origin, radius, dir, maxDist, ~0, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0)
            {
                MelonLogger.Msg("[DroneHand] No Pull target found (no cast hits)");
                return false;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null) continue;

                GameObject go = hit.collider.gameObject;
                if (go == gameObject || go.transform.IsChildOf(transform))
                    continue;
                if (hit.collider.name == "DroneTrigger")
                    continue;

                DronePullMotion pm = hit.collider.GetComponentInParent<DronePullMotion>();
                if (pm == null || !pm.enabled)
                    continue;

                Vector3 targetPoint = GetSafePullLatchWorldPos(pm, hit, origin);
                Vector3 toTarget = targetPoint - origin;
                float toTargetDist = toTarget.magnitude;
                if (toTargetDist <= 0.0001f)
                    continue;

                Vector3 losDir = toTarget / toTargetDist;
                bool blocked = false;

                RaycastHit[] blockers = Physics.SphereCastAll(origin, 0.035f, losDir, toTargetDist, ~0, QueryTriggerInteraction.Ignore);
                if (blockers != null && blockers.Length > 0)
                {
                    Array.Sort(blockers, (a, b) => a.distance.CompareTo(b.distance));

                    for (int b = 0; b < blockers.Length; b++)
                    {
                        RaycastHit blockHit = blockers[b];
                        if (blockHit.collider == null) continue;
                        if (blockHit.distance < 0.02f) continue;

                        DronePullMotion blockPm = blockHit.collider.GetComponentInParent<DronePullMotion>();
                        if (blockPm == pm)
                            continue;

                        DroneRotationalMotion blockRm = blockHit.collider.GetComponentInParent<DroneRotationalMotion>();
                        if (blockRm != null)
                        {
                            blocked = true;
                            MelonLogger.Msg($"[DroneHand] No Pull target found (blocked by door '{blockHit.collider.name}')");
                            break;
                        }

                        blocked = true;
                        MelonLogger.Msg($"[DroneHand] No Pull target found (blocked by solid '{blockHit.collider.name}')");
                        break;
                    }
                }

                if (blocked)
                    continue;

                bestPm = pm;
                bestHit = hit;
                MelonLogger.Msg($"[DroneHand] Found Pull target '{bestPm.name}' dist={bestHit.distance:F3} latchPos={targetPoint}");
                return true;
            }

            MelonLogger.Msg("[DroneHand] No Pull target found (all candidates blocked or invalid)");
            return false;
        }

        IDroneGrabbable FindBestGrabbable()
        {
            float bestDistSqr = float.MaxValue;
            IDroneGrabbable best = null;

            for (int i = 0; i < hitbox.Count; i++)
            {
                GameObject go = hitbox[i];
                if (go == null) continue;

                var list = new Il2CppSystem.Collections.Generic.List<MonoBehaviour>();
                go.GetComponentsInParent<MonoBehaviour>(true, list);

                for (int g = 0; g < list.Count; g++)
                {
                    var candidate = list[g] as IDroneGrabbable;
                    if (candidate == null) continue;

                    float d = (go.transform.position - hand.position).sqrMagnitude;
                    if (d < bestDistSqr)
                    {
                        bestDistSqr = d;
                        best = candidate;
                    }
                }
            }

            return best;
        }

        private void registerFingers()
        {
            for (int i = 0; i < 3; i++)
            {
                fingers[i] = GameObject.Find("SM_finger_" + (i + 1) + "_" + handSide + "_low");
            }

            int multiplier = 1;
            if (handSide == 'L') multiplier = -1;

            fingerRot[0] = new Vector3(0, 0, 15 * multiplier);
            fingerRot[1] = new Vector3(0, 0, -15 * multiplier);
            fingerRot[2] = new Vector3(-15, 0, 0);
        }
        void OnTriggerEnter(Collider col)
        {
            if (_launching && col != null)
            {
                GlassDriver gd = col.GetComponentInParent<GlassDriver>();
                if (gd != null)
                {
                    NotifyGlassBroken(gd);
                    return;
                }
            }

            if (TryHandleAgentAvatarLaunchHit(col))
                return;

            if (TryHandleGuardReactionTrigger(col))
                return;

            if (!hitbox.Contains(col.gameObject))
                hitbox.Add(col.gameObject);
        }

        void OnTriggerExit(Collider col)
        {
            if (hitbox.Contains(col.gameObject))
                hitbox.Remove(col.gameObject);
        }
    }

    internal static class DroneHandSchellInputBridge
    {
        static readonly Dictionary<int, int> _pressSignals = new Dictionary<int, int>();

        public static int GetPressSignal(VRHandInput handInput)
        {
            if (handInput == null) return 0;

            int id = GetId(handInput);
            int value;
            if (_pressSignals.TryGetValue(id, out value))
                return value;

            return 0;
        }

        public static void MarkPressed(VRHandInput handInput, string source)
        {
            if (handInput == null) return;

            int id = GetId(handInput);
            int value;
            if (!_pressSignals.TryGetValue(id, out value))
                value = 0;

            _pressSignals[id] = value + 1;
        }

        static int GetId(VRHandInput handInput)
        {
            try { return handInput.GetInstanceID(); }
            catch { return 0; }
        }
    }

    [HarmonyPatch(typeof(VRHandInput), "InteractPerformed")]
    internal static class Patch_DroneHandInput_InteractPerformed
    {
        static void Postfix(VRHandInput __instance)
        {
            DroneHandSchellInputBridge.MarkPressed(__instance, "InteractPerformed");
        }
    }

    [HarmonyPatch(typeof(VRHandInput), "UsePerformed")]
    internal static class Patch_DroneHandInput_UsePerformed
    {
        static void Postfix(VRHandInput __instance)
        {
            DroneHandSchellInputBridge.MarkPressed(__instance, "UsePerformed");
        }
    }

    [HarmonyPatch(typeof(VRHandInput), "AlternateUsePerformed")]
    internal static class Patch_DroneHandInput_AlternateUsePerformed
    {
        static void Postfix(VRHandInput __instance)
        {
            DroneHandSchellInputBridge.MarkPressed(__instance, "AlternateUsePerformed");
        }
    }

    [HarmonyPatch(typeof(VRHandInput), "FreezePerformed")]
    internal static class Patch_DroneHandInput_FreezePerformed
    {
        static void Postfix(VRHandInput __instance)
        {
            DroneHandSchellInputBridge.MarkPressed(__instance, "FreezePerformed");
        }
    }

}
