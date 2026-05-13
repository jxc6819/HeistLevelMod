using HarmonyLib;
using MelonLoader;
using SG.Phoenix.Assets.Code.InputManagement;
using SG.Phoenix.Assets.Code.Interactables;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnhollowerBaseLib.Attributes;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class HeadsetScript : MonoBehaviour
    {
        public HeadsetScript(IntPtr ptr) : base(ptr) { }
        public HeadsetScript()
            : base(ClassInjector.DerivedConstructorPointer<HeadsetScript>())
            => ClassInjector.DerivedConstructorBody(this);

        public static HeadsetScript Instance;

        const float DRONE_RIG_SCALE = 0.2f;
        const float HEAD_ATTACH_DIST = 0.2f;
        const float GRAB_OFF_DIST = 0.05f;

        static readonly Vector3 WORN_LOCAL_POS = Vector3.zero;
        static readonly Quaternion WORN_LOCAL_ROT = Quaternion.identity;

        ObjectBank bank;
        PickUp pickup;
        Rigidbody rb;
        MeshRenderer mr;

        GameObject HMD;
        GameObject Drone;
        GameObject DroneHost;
        PickUp _droneHostPickUp;
        Rigidbody _droneHostRb;
        GameObject Rig;

        HeadsetDriver driver;
        WireVisionManager wvm;

        GameObject RightHand;
        GameObject LeftHand;

        GameObject BotRightHand;
        GameObject BotLeftHand;

        GameObject StaticRightHand;
        GameObject StaticLeftHand;
        GameObject elevatorFloor;

        public LoopingSfx headsetLoop;
        public LoopingSfx deadLoop;
        public Hotspot hotSpot;
        public bool vaultShiftTriggered = false;
        Vector3 _vaultPos;

        bool wasHeld = false;
        bool wearing = false;
        public bool IsWearing => wearing;
        internal GameObject DroneHostObj => DroneHost;
        internal GameObject DroneObj => Drone;
        internal bool InDroneTkTransition => _enteredHeadsetFromDroneTK;
        internal bool InDroneTkExitLockout => _droneTkExitLockoutActive;

        Vector3 _droneLocalPos;
        Vector3 _droneLocalRot;

        Vector3 _droneChildStartLocalPos;
        Quaternion _droneChildStartLocalRot;
        Vector3 _droneChildStartLocalScale;

        bool _enteredHeadsetFromDroneTK = false;
        bool _restoreTkStateR;
        bool _restoreTkStateL;
        bool _droneTkExitLockoutActive = false;
        float _droneTkExitLockoutEndTime = -1f;

        Vector3 _droneReturnWorldPos;
        Quaternion _droneReturnWorldRot;
        bool _hasDroneReturnPose = false;

        bool _hasDroneRbState = false;
        bool _savedDroneRbKinematic = false;
        bool _savedDroneRbUseGravity = false;
        RigidbodyConstraints _savedDroneRbConstraints = RigidbodyConstraints.None;

        bool _hasDronePickUpEnabledState = false;
        bool _savedDronePickUpEnabled = true;

        Transform _headsetRoot;

        const int DRONE_OFF_GUARD_FRAMES = 8;
        const float DRONE_OFF_GUARD_POS_THRESHOLD = 0.08f;
        const float DRONE_OFF_GUARD_ROT_THRESHOLD = 8f;
        bool _droneOffGuardRunning = false;
        int _droneHandEnableToken = 0;

        Vector3 _headsetRootStartScale;
        Vector3 _headsetStartScale;

        GameObject RightHandRoot;
        GameObject LeftHandRoot;

        GameObject _droneRightHolding;
        GameObject _droneLeftHolding;

        DroneHand _rightDroneHand;
        DroneHand _leftDroneHand;

        TelekinesisHandState tkStateR;
        TelekinesisHandState tkStateL;
        CustomHiddenVolume hv;

        public AgentAvatarDriver _agentAvatar;

        readonly Dictionary<int, float> _savedRotMotionAngles = new Dictionary<int, float>();
        const float ROT_SAVE_EPSILON = 0.01f;

        void Start()
        {
            Instance = this;

            bank = ObjectBank.Instance;
            if (bank == null)
            {
                MelonLogger.Error("[HeadsetScript] ObjectBank.Instance is null.");
                return;
            }

            pickup = transform.parent ? transform.parent.GetComponent<PickUp>() : null;
            rb = transform.parent ? transform.parent.GetComponent<Rigidbody>() : null;
            mr = GetComponent<MeshRenderer>();

            _headsetRoot = transform.parent;
            if (_headsetRoot != null) _headsetRootStartScale = _headsetRoot.localScale;
            _headsetStartScale = transform.localScale;

            HMD = bank.HMD;
            Drone = bank.Drone;
            DroneHost = ResolveDroneHost();
            _droneHostPickUp = DroneHost != null ? DroneHost.GetComponent<PickUp>() : null;
            _droneHostRb = DroneHost != null ? DroneHost.GetComponent<Rigidbody>() : null;
            Rig = bank.PlayerRig;
            elevatorFloor = GameObject.Find("ElevatorFloor");

            if (Drone != null)
            {
                _droneChildStartLocalPos = Drone.transform.localPosition;
                _droneChildStartLocalRot = Drone.transform.localRotation;
                _droneChildStartLocalScale = Drone.transform.localScale;
            }

            driver = GetComponent<HeadsetDriver>();

            RightHand = GameObject.Find("RightHand");
            LeftHand = GameObject.Find("LeftHand");

            BotRightHand = GameObject.Find("BotRightHand");
            BotLeftHand = GameObject.Find("BotLeftHand");

            StaticRightHand = GameObject.Find("SM_elbow_R_low");
            StaticLeftHand = GameObject.Find("SM_elbow_L_low");

            RightHandRoot = GameObject.Find("RightHandRoot");
            LeftHandRoot = GameObject.Find("LeftHandRoot");

            var vpp = GameObject.Find("PlayerVaultPos");
            if (vpp != null) _vaultPos = vpp.transform.position;

            var mgr = GameObject.Find("Manager");
            if (mgr != null) wvm = mgr.GetComponent<WireVisionManager>();

            if (BotLeftHand != null && BotLeftHand.GetComponent<DroneHand>() == null) _leftDroneHand = BotLeftHand.AddComponent<DroneHand>();
            if (BotRightHand != null && BotRightHand.GetComponent<DroneHand>() == null) _rightDroneHand = BotRightHand.AddComponent<DroneHand>();

            if (RightHandRoot != null) tkStateR = RightHandRoot.GetComponent<TelekinesisHandState>();
            if (LeftHandRoot != null) tkStateL = LeftHandRoot.GetComponent<TelekinesisHandState>();

            headsetLoop = gameObject.AddComponent<LoopingSfx>();
            headsetLoop.InitAndPlay("HeadsetLoop.ogg", 0.5f);
            headsetLoop.TurnOff();

            SafeSetActive(BotLeftHand, false);
            SafeSetActive(BotRightHand, false);

            IgnoreHeadsetDroneCollisions();
        }

        GameObject ResolveDroneHost()
        {
            if (Drone != null && Drone.transform.parent != null)
                return Drone.transform.parent.gameObject;

            GameObject found = GameObject.Find("PickUp_HOST_Drone");
            if (found != null)
                return found;

            return null;
        }

        bool IsDroneCurrentlyHeldWithTK()
        {
            if (_droneHostPickUp == null) return false;
            if (!_droneHostPickUp.isHeld) return false;
            if (_droneHostPickUp.heldHand == null) return false;

            try
            {
                return _droneHostPickUp.heldHand.TelekinesisEnabled;
            }
            catch
            {
                return false;
            }
        }

        void SaveDroneReturnPoseAndFreeze(string reason)
        {
            if (DroneHost == null && Drone == null) return;

            Transform droneTf = DroneHost != null ? DroneHost.transform : Drone.transform;
            _droneReturnWorldPos = droneTf.position;
            _droneReturnWorldRot = droneTf.rotation;
            _hasDroneReturnPose = true;

            if (_droneHostRb != null)
            {
                if (!_hasDroneRbState)
                {
                    _savedDroneRbKinematic = _droneHostRb.isKinematic;
                    _savedDroneRbUseGravity = _droneHostRb.useGravity;
                    _savedDroneRbConstraints = _droneHostRb.constraints;
                    _hasDroneRbState = true;
                }

                try { _droneHostRb.isKinematic = true; } catch { }
                try { _droneHostRb.useGravity = false; } catch { }
                _droneHostRb.velocity = Vector3.zero;
                _droneHostRb.angularVelocity = Vector3.zero;
            }

            ApplySavedDroneReturnPose(forceKinematic: true);
            MelonLogger.Msg("[HeadsetScript] Saved/froze drone return pose for " + reason + ". pos=" + _droneReturnWorldPos);
        }

        void ApplySavedDroneReturnPose(bool forceKinematic)
        {
            if (!_hasDroneReturnPose) return;

            if (DroneHost != null)
            {
                DroneHost.transform.position = _droneReturnWorldPos;
                DroneHost.transform.rotation = _droneReturnWorldRot;
            }
            else if (Drone != null)
            {
                Drone.transform.position = _droneReturnWorldPos;
                Drone.transform.rotation = _droneReturnWorldRot;
            }

            if (Drone != null)
            {
                Drone.transform.localPosition = _droneChildStartLocalPos;
                Drone.transform.localRotation = _droneChildStartLocalRot;
                Drone.transform.localScale = _droneChildStartLocalScale;
            }

            if (_droneHostRb != null)
            {
                if (forceKinematic)
                {
                    try { _droneHostRb.isKinematic = true; } catch { }
                    try { _droneHostRb.useGravity = false; } catch { }
                }

                _droneHostRb.velocity = Vector3.zero;
                _droneHostRb.angularVelocity = Vector3.zero;
            }
        }

        void RestoreDronePhysicsAfterExit()
        {
            if (_droneHostPickUp != null && _hasDronePickUpEnabledState)
            {
                try { _droneHostPickUp.enabled = _savedDronePickUpEnabled; } catch { }
            }

            if (_droneHostRb != null && _hasDroneRbState)
            {
                try { _droneHostRb.constraints = _savedDroneRbConstraints; } catch { }
                try { _droneHostRb.useGravity = _savedDroneRbUseGravity; } catch { }
                try { _droneHostRb.isKinematic = _savedDroneRbKinematic; } catch { }
                _droneHostRb.velocity = Vector3.zero;
                _droneHostRb.angularVelocity = Vector3.zero;
            }

            _hasDroneRbState = false;
            _hasDronePickUpEnabledState = false;
        }

        void ForceBreakDroneTkHoldPreservePose()
        {
            if (_droneHostPickUp == null) return;

            Vector3 keepPos = DroneHost != null ? DroneHost.transform.position : (_hasDroneReturnPose ? _droneReturnWorldPos : Vector3.zero);
            Quaternion keepRot = DroneHost != null ? DroneHost.transform.rotation : (_hasDroneReturnPose ? _droneReturnWorldRot : Quaternion.identity);

            try
            {
                if (_droneHostPickUp.heldHand != null)
                    _droneHostPickUp.heldHand.ReleaseHeldObject();
            }
            catch { }

            try { _droneHostPickUp.heldHand = null; } catch { }

            if (DroneHost != null)
            {
                DroneHost.transform.position = keepPos;
                DroneHost.transform.rotation = keepRot;
            }

            if (Drone != null)
            {
                Drone.transform.localPosition = _droneChildStartLocalPos;
                Drone.transform.localRotation = _droneChildStartLocalRot;
                Drone.transform.localScale = _droneChildStartLocalScale;
            }

            if (_droneHostRb != null)
            {
                _droneHostRb.velocity = Vector3.zero;
                _droneHostRb.angularVelocity = Vector3.zero;
                try { _droneHostRb.useGravity = false; } catch { }
            }
        }

        void EnterDroneTkTransitionSuppression()
        {
            _enteredHeadsetFromDroneTK = IsDroneCurrentlyHeldWithTK();

            if (!_enteredHeadsetFromDroneTK)
                return;

            MelonLogger.Msg("[HeadsetScript] Entering headset while drone is TK-held. Releasing TK once and preserving drone pose.");



            _restoreTkStateR = false;
            _restoreTkStateL = false;

            SaveDroneReturnPoseAndFreeze("TK headset entry");
            ForceExitVentRailForHeadsetMode();
            ForceBreakDroneTkHoldPreservePose();
            ApplySavedDroneReturnPose(forceKinematic: true);
            ClearTkStateForHand(RightHandRoot, false, true, false);
            ClearTkStateForHand(LeftHandRoot, false, true, false);
        }

        void ForceExitVentRailForHeadsetMode()
        {
            try
            {
                DroneVentRailTK rail = null;

                if (DroneHost != null)
                    rail = DroneHost.GetComponent<DroneVentRailTK>() ?? DroneHost.GetComponentInChildren<DroneVentRailTK>(true);

                if (rail == null && Drone != null)
                {
                    rail = Drone.GetComponent<DroneVentRailTK>();

                    if (rail == null && Drone.transform.parent != null)
                        rail = Drone.transform.parent.GetComponent<DroneVentRailTK>();
                }

                if (rail != null)
                    rail.ForceExitRailForHeadsetMode();
            }
            catch { }
        }

        void ExitDroneTkTransitionSuppression()
        {
            if (!_enteredHeadsetFromDroneTK)
                return;

            _droneTkExitLockoutActive = true;
            _droneTkExitLockoutEndTime = Time.time + 0.20f;


            ForceExitVentRailForHeadsetMode();
            ForceBreakDroneTkHoldPreservePose();
            ApplySavedDroneReturnPose(forceKinematic: true);
            ClearTkStateForHand(RightHandRoot, false, true, false);
            ClearTkStateForHand(LeftHandRoot, false, true, false);
        }

        void ProcessDroneTkExitRecovery()
        {
            if (!_droneTkExitLockoutActive) return;

            ForceBreakDroneTkHoldPreservePose();
            ApplySavedDroneReturnPose(forceKinematic: true);
            ClearTkStateForHand(RightHandRoot, false, true, false);
            ClearTkStateForHand(LeftHandRoot, false, true, false);

            if (Time.time < _droneTkExitLockoutEndTime)
                return;

            ApplySavedDroneReturnPose(forceKinematic: false);
            RestoreDronePhysicsAfterExit();

            RestoreRealHandStatesAfterHeadsetOff();

            ClearTkStateForHand(RightHandRoot, false, true, false);
            ClearTkStateForHand(LeftHandRoot, false, true, false);
            RestoreRealHandStatesAfterHeadsetOff();

            _droneTkExitLockoutActive = false;
            _droneTkExitLockoutEndTime = -1f;
            _enteredHeadsetFromDroneTK = false;
            _restoreTkStateR = false;
            _restoreTkStateL = false;
            _hasDroneReturnPose = false;
        }

        void ProcessDroneFreezeWhileWearing()
        {


            if (!wearing) return;
            if (!_hasDroneReturnPose) return;
            ApplySavedDroneReturnPose(forceKinematic: true);
        }

        void ClearTkStateForHand(GameObject handRoot, bool includeHeadsetTargets, bool includeDroneTargets, bool forceReticleReset)
        {
            if (handRoot == null) return;

            try
            {
                VRHandInput vhi = handRoot.GetComponent<VRHandInput>();
                if (vhi == null) return;

                HeadsetTKBlockPatch.EnsureReflectionForExternal(vhi);
                HeadsetTKBlockPatch.ClearFocusedAndHeldMembersExternally(vhi, includeHeadsetTargets, includeDroneTargets, forceReticleReset);

                if (forceReticleReset)
                {
                    TelekinesisHandState tk = handRoot.GetComponent<TelekinesisHandState>();
                    if (tk != null)
                    {
                        try
                        {
                            if (tk.TelekinesisBeam != null)
                                tk.TelekinesisBeam.enabled = false;
                        }
                        catch { }

                        try
                        {
                            if (tk._SmoothedReticleRenderer != null)
                                tk._SmoothedReticleRenderer.enabled = false;
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        [HideFromIl2Cpp]
        System.Collections.IEnumerator Co_HeadsetTakeoffProbe(string label)
        {
            Vector3 prevHeadsetPos = _headsetRoot != null ? _headsetRoot.position : Vector3.zero;

            for (int i = 0; i < 12; i++)
            {
                yield return null;

                string heldHandName = "null";
                Vector3 heldHandPos = Vector3.zero;
                bool heldHandActive = false;
                float handDist = -1f;

                try
                {
                    if (pickup != null && pickup.heldHand != null)
                    {
                        heldHandName = pickup.heldHand.gameObject != null ? pickup.heldHand.gameObject.name : "(no-go)";
                        if (pickup.heldHand.transform != null)
                        {
                            heldHandPos = pickup.heldHand.transform.position;
                            heldHandActive = pickup.heldHand.gameObject != null && pickup.heldHand.gameObject.activeInHierarchy;
                            if (_headsetRoot != null)
                                handDist = Vector3.Distance(_headsetRoot.position, pickup.heldHand.transform.position);
                        }
                    }
                }
                catch { }

                string parentName = _headsetRoot != null && _headsetRoot.parent != null ? _headsetRoot.parent.name : "null";
                string headsetPos = _headsetRoot != null ? _headsetRoot.position.ToString() : "(null)";
                float headsetToHmdDist = -1f;
                float headsetMoveDelta = -1f;
                try
                {
                    if (_headsetRoot != null)
                    {
                        if (HMD != null) headsetToHmdDist = Vector3.Distance(_headsetRoot.position, HMD.transform.position);
                        headsetMoveDelta = Vector3.Distance(_headsetRoot.position, prevHeadsetPos);
                        prevHeadsetPos = _headsetRoot.position;
                    }
                }
                catch { }
                string nhsR = "null";
                string tkR = "null";
                try
                {
                    if (RightHandRoot != null)
                    {
                        var n = RightHandRoot.GetComponent<NormalHandState>();
                        var t = RightHandRoot.GetComponent<TelekinesisHandState>();
                        nhsR = n != null ? n.enabled.ToString() : "null";
                        tkR = t != null ? t.enabled.ToString() : "null";
                    }
                }
                catch { }

                MelonLogger.Msg("[HeadsetScript] TakeoffProbe[" + label + "] frame " + (i + 1) +
                    " | wearing=" + wearing +
                    " | exitLockout=" + _droneTkExitLockoutActive +
                    " | headsetHeld=" + (pickup != null && pickup.isHeld) +
                    " | heldHand=" + heldHandName +
                    " | heldHandActive=" + heldHandActive +
                    " | handDist=" + handDist.ToString("F3") +
                    " | headsetParent=" + parentName +
                    " | headsetPos=" + headsetPos +
                    " | headsetToHmdDist=" + headsetToHmdDist.ToString("F3") +
                    " | headsetMoveDelta=" + headsetMoveDelta.ToString("F3") +
                    " | heldHandPos=" + heldHandPos +
                    " | rightHandActive=" + (RightHand != null && RightHand.activeInHierarchy) +
                    " | botRightHandActive=" + (BotRightHand != null && BotRightHand.activeInHierarchy) +
                    " | nhsR=" + nhsR +
                    " | tkR=" + tkR);
            }
        }

        void LogPickUpState(string tag, PickUp pu, Rigidbody body)
        {
            try
            {
                if (pu == null)
                {
                    MelonLogger.Msg("[HeadsetScript] " + tag + " | pickup=null");
                    return;
                }

                string heldHandName = "null";
                bool teleEnabled = false;
                try
                {
                    if (pu.heldHand != null)
                    {
                        heldHandName = pu.heldHand.gameObject != null ? pu.heldHand.gameObject.name : "(no-go)";
                        teleEnabled = pu.heldHand.TelekinesisEnabled;
                    }
                }
                catch { }

                string rbInfo = "rb=null";
                if (body != null)
                {
                    rbInfo = "rb.isKinematic=" + body.isKinematic +
                             " vel=" + body.velocity +
                             " angVel=" + body.angularVelocity +
                             " constraints=" + body.constraints;
                }

                MelonLogger.Msg("[HeadsetScript] " + tag +
                    " | isHeld=" + pu.isHeld +
                    " | heldHand=" + heldHandName +
                    " | heldHandNull=" + (pu.heldHand == null) +
                    " | heldHandTK=" + teleEnabled +
                    " | " + rbInfo);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[HeadsetScript] " + tag + " log failed: " + e.Message);
            }
        }

        void LogHeadsetGripState(string tag)
        {
            try
            {
                string parentName = _headsetRoot != null && _headsetRoot.parent != null ? _headsetRoot.parent.name : "null";
                LogPickUpState(tag + " [headset] parent=" + parentName, pickup, rb);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[HeadsetScript] " + tag + " headset log failed: " + e.Message);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            ProcessDroneTkExitRecovery();

            if (pickup == null) return;

            if (!wasHeld && pickup.isHeld) OnGrab();
            if (wasHeld && !pickup.isHeld) OnRelease();
        }

        void LateUpdate()
        {
            if (!wearing) return;
            ForceWearTether();
        }

        void NormalizeHeldHeadsetStateIfNeeded(string reason)
        {
            if (wearing) return;
            if (pickup == null || !pickup.isHeld || pickup.heldHand == null) return;
            if (_headsetRoot == null) return;

            VRHandInput heldHand = pickup.heldHand;
            bool tkHeld = false;
            try { tkHeld = heldHand.TelekinesisEnabled; } catch { }


            if (tkHeld) return;

            try
            {
                float dist = Vector3.Distance(_headsetRoot.position, heldHand.transform.position);

                if (dist <= 0.28f && _headsetRoot.parent == null)
                    return;

                CorrectNormalHeldHeadsetToHand(heldHand, reason, dist);
            }
            catch { }
        }

        void CorrectNormalHeldHeadsetToHand(VRHandInput heldHand, string reason, float dist)
        {
            if (heldHand == null || _headsetRoot == null) return;

            try
            {
                if (_headsetRoot.parent != null)
                    _headsetRoot.SetParent(null, true);

                _headsetRoot.position = heldHand.transform.position;
                _headsetRoot.rotation = heldHand.transform.rotation;


                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;

                RestoreHeadsetScale();

                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                MelonLogger.Warning("[HeadsetScript] Repaired normal-held headset offset. reason=" + reason +
                                    " dist=" + dist.ToString("F3") +
                                    " heldHand=" + (heldHand.gameObject != null ? heldHand.gameObject.name : "null") +
                                    " pos=" + _headsetRoot.position);
            }
            catch { }
        }

        void ResetHeadsetPickUpFollowSnapshot(VRHandInput heldHand, string reason)
        {



        }

        void ClearHeadsetTelekinesisAttachTransform(string reason)
        {

        }

        void PullHeadsetToTakeoffHandIfStranded(VRHandInput takeoffHand, string label)
        {
            if (takeoffHand == null) return;
            if (_headsetRoot == null) return;
            if (pickup == null || !pickup.isHeld) return;

            try
            {
                float dist = Vector3.Distance(_headsetRoot.position, takeoffHand.transform.position);
                if (dist <= 0.035f && _headsetRoot.parent == null)
                    return;

                if (_headsetRoot.parent != null)
                    _headsetRoot.SetParent(null, true);

                _headsetRoot.position = takeoffHand.transform.position;
                _headsetRoot.rotation = takeoffHand.transform.rotation;

                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                MelonLogger.Warning("[HeadsetScript] Corrected stranded headset after " + label + " takeoff. dist=" + dist.ToString("F3"));
            }
            catch { }
        }

        [HideFromIl2Cpp]
        System.Collections.IEnumerator Co_StabilizeHeadsetTakeoffHold(VRHandInput takeoffHand, string label)
        {
            if (takeoffHand == null) yield break;

            float endTime = Time.time + 0.25f;
            while (Time.time < endTime)
            {
                yield return null;

                if (wearing) yield break;
                if (_headsetRoot == null) yield break;
                if (pickup == null || !pickup.isHeld) yield break;
                if (pickup.heldHand != takeoffHand) yield break;

                try
                {
                    float dist = Vector3.Distance(_headsetRoot.position, takeoffHand.transform.position);

                    if (dist > 0.035f || _headsetRoot.parent != null)
                        PullHeadsetToTakeoffHandIfStranded(takeoffHand, label + ":stabilize");
                }
                catch { }
            }
        }

        void OnGrab()
        {
            wasHeld = true;

            if (wearing &&
                pickup.heldHand != null &&
                Vector3.Distance(pickup.heldHand.transform.position, transform.position) < GRAB_OFF_DIST)
            {
                bool droneTkPathTakeoff = _enteredHeadsetFromDroneTK;
                string takeoffLabel = droneTkPathTakeoff ? "DRONE_TK" : "BASELINE";
                VRHandInput takeoffHand = pickup.heldHand;
                MelonLogger.Msg("[HeadsetScript] OnGrab takeoff path=" + takeoffLabel);

                if (_headsetRoot != null)
                    _headsetRoot.parent = null;

                if (rb != null)
                {
                    rb.constraints = RigidbodyConstraints.None;
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                string takeoffHeldHandName = "null";
                try
                {
                    if (pickup != null && pickup.heldHand != null && pickup.heldHand.gameObject != null)
                        takeoffHeldHandName = pickup.heldHand.gameObject.name;
                }
                catch { }

                MelonLogger.Msg("[HeadsetScript] OnGrab takeoff start label=" + takeoffLabel +
                    " heldHand=" + takeoffHeldHandName +
                    " headsetPos=" + (_headsetRoot != null ? _headsetRoot.position.ToString() : "(null)") +
                    " hmdPos=" + (HMD != null ? HMD.transform.position.ToString() : "(null)"));

                wearing = false;
                HeadsetOffLogic();

                PullHeadsetToTakeoffHandIfStranded(takeoffHand, takeoffLabel);
                MelonCoroutines.Start(Co_StabilizeHeadsetTakeoffHold(takeoffHand, takeoffLabel));

                MelonLogger.Msg("[HeadsetScript] OnGrab after HeadsetOffLogic label=" + takeoffLabel +
                    " pickupHeld=" + (pickup != null && pickup.isHeld) +
                    " heldHand=" + ((pickup != null && pickup.heldHand != null && pickup.heldHand.gameObject != null) ? pickup.heldHand.gameObject.name : "null") +
                    " headsetParent=" + (_headsetRoot != null && _headsetRoot.parent != null ? _headsetRoot.parent.name : "null") +
                    " headsetPos=" + (_headsetRoot != null ? _headsetRoot.position.ToString() : "(null)"));

                RestoreHeadsetScale();
                MelonCoroutines.Start(Co_HeadsetTakeoffProbe(takeoffLabel));
            }
        }

        public void ForceHeadsetOff()
        {
            if (!wearing) return;

            wasHeld = false;

            if (_headsetRoot != null)
                _headsetRoot.parent = null;

            if (pickup != null && pickup.isHeld)
            {
                try
                {
                    pickup.heldHand = null;
                }
                catch { }
            }

            if (rb != null)
            {
                rb.constraints = RigidbodyConstraints.None;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            wearing = false;
            HeadsetOffLogic();
            RestoreHeadsetScale();
            DropHeadsetAtFeet();
        }

        void DropHeadsetAtFeet()
        {
            if (_headsetRoot == null) return;

            Transform feetRef = null;
            if (Rig != null) feetRef = Rig.transform;
            else if (HMD != null) feetRef = HMD.transform;
            else feetRef = transform;

            Vector3 basePos = feetRef.position;
            Vector3 dropPos = basePos + new Vector3(0f, 0f, 0.18f);
            dropPos.y = basePos.y - 0.28f;

            _headsetRoot.position = dropPos;
            _headsetRoot.rotation = Quaternion.identity;

            if (rb != null)
            {
                rb.velocity = new Vector3(0f, 0.4f, 1.35f);
                rb.angularVelocity = new Vector3(0f, 120f, 0f);
            }
        }

        void OnRelease()
        {
            wasHeld = false;

            if (!wearing && HMD != null && Vector3.Distance(transform.position, HMD.transform.position) < HEAD_ATTACH_DIST)
            {
                wearing = true;
                OnHead();
            }
        }

        void OnHead()
        {
            RestoreHeadsetScale();

            if (rb != null)
            {
                rb.constraints = RigidbodyConstraints.FreezeAll;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            ForceWearTether();

            if (mr != null) mr.enabled = false;



            ForceExitVentRailForHeadsetMode();

            EnterDroneTkTransitionSuppression();
            HeadsetOnLogic();
        }

        void ForceWearTether()
        {
            if (!wearing) return;
            if (_headsetRoot == null) return;
            if (HMD == null) return;

            RestoreHeadsetScale();

            if (_headsetRoot.parent != HMD.transform)
                _headsetRoot.SetParent(HMD.transform, false);

            _headsetRoot.localPosition = WORN_LOCAL_POS;
            _headsetRoot.localRotation = WORN_LOCAL_ROT;

            if (rb != null)
            {
                rb.constraints = RigidbodyConstraints.FreezeAll;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        bool enteredKeypad = false;
        void HeadsetOnLogic()
        {
            RefreshDroneVectors();

            SafeSetActive(Drone, false);
            AudioUtil.PlayAt("HeadsetOn.ogg", transform.position);
            headsetLoop.TurnOn();

            if (Rig != null)
            {
                Rig.transform.position = _droneLocalPos;
                Rig.transform.rotation = Quaternion.Euler(0f, _droneLocalRot.y, 0f);
                Rig.transform.localScale = Vector3.one * DRONE_RIG_SCALE;
            }

            if (RightHand != null && BotRightHand != null)
            {
                BotRightHand.transform.parent = RightHand.transform.parent;
                BotRightHand.transform.localScale = Vector3.one * DRONE_RIG_SCALE;
                BotRightHand.transform.localPosition = Vector3.zero;
                BotRightHand.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            }

            if (LeftHand != null && BotLeftHand != null)
            {
                BotLeftHand.transform.parent = LeftHand.transform.parent;
                BotLeftHand.transform.localScale = Vector3.one * DRONE_RIG_SCALE;
                BotLeftHand.transform.localPosition = Vector3.zero;
                BotLeftHand.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            }
            if (hotSpot != null && !enteredKeypad && hotSpot.gameObject.name == "KeypadHotspot")
            {
                enteredKeypad = true;
                HeistLevelManager.playHandler("Handler_KeypadHint.wav", 0.5f);
            }

            SafeSetActive(RightHand, false);
            SafeSetActive(LeftHand, false);
            SafeSetActive(BotRightHand, false);
            SafeSetActive(BotLeftHand, false);

            int enableToken = ++_droneHandEnableToken;
            MelonCoroutines.Start(Co_EnableBotHandsAfterTransition(enableToken));

            ToggleVisual(true);
            if (wvm != null) wvm.Enable(true);
            ToggleDronePickups(true);
            CatchStateGlitch();
            Keycard.headsetOn = true;
            KeypadButton.headsetOn = true;
            _agentAvatar.TurnOn();
        }

        void HeadsetOffLogic()
        {
            ++_droneHandEnableToken;
            UpdateDroneHands();
            headsetLoop.TurnOff();
            AudioUtil.PlayAt("HeadsetOff.ogg", transform.position);
            ExitDroneTkTransitionSuppression();

            Vector3 expectedDronePos = _hasDroneReturnPose ? _droneReturnWorldPos : (DroneHost != null ? DroneHost.transform.position : (Drone != null ? Drone.transform.position : Vector3.zero));
            Quaternion expectedDroneRot = _hasDroneReturnPose ? _droneReturnWorldRot : (DroneHost != null ? DroneHost.transform.rotation : (Drone != null ? Drone.transform.rotation : Quaternion.identity));

            SafeSetActive(Drone, true);
            ApplySavedDroneReturnPose(forceKinematic: true);
            BeginDroneOffGuard(expectedDronePos, expectedDroneRot);

            if (Rig != null)
            {
                Rig.transform.position = new Vector3(0f, elevatorFloor.transform.position.y + 1.4f, 0f);
                Rig.transform.rotation = Quaternion.identity;
                Rig.transform.localScale = Vector3.one;
            }

            SafeSetActive(BotRightHand, false);
            SafeSetActive(BotLeftHand, false);
            SafeSetActive(RightHand, true);
            SafeSetActive(LeftHand, true);
            RestoreRealHandStatesAfterHeadsetOff();

            ToggleVisual(false);
            if (wvm != null) wvm.Enable(false);
            if (mr != null) mr.enabled = true;

            ToggleDronePickups(false);

            RestoreHeadsetScale();
            CatchStateGlitch();
            Keycard.headsetOn = false;
            KeypadButton.headsetOn = false;
            _agentAvatar.TurnOff();
        }

        void MatchCloneScaleToSource(Transform clone, Transform source)
        {
            if (clone == null || source == null)
                return;

            Vector3 sourceWorldScale = source.lossyScale;
            Transform parent = clone.parent;

            if (parent == null)
            {
                clone.localScale = sourceWorldScale;
                return;
            }

            Vector3 parentWorldScale = parent.lossyScale;

            clone.localScale = new Vector3(
                Mathf.Abs(parentWorldScale.x) > 0.0001f ? sourceWorldScale.x / parentWorldScale.x : sourceWorldScale.x,
                Mathf.Abs(parentWorldScale.y) > 0.0001f ? sourceWorldScale.y / parentWorldScale.y : sourceWorldScale.y,
                Mathf.Abs(parentWorldScale.z) > 0.0001f ? sourceWorldScale.z / parentWorldScale.z : sourceWorldScale.z
            );
        }

        public void UpdateDroneHands()
        {
            MelonLogger.Msg("[UpdateDroneHands] - Checkpoint1");

            if (BotRightHand == null || BotLeftHand == null)
            {
                MelonLogger.Warning("[UpdateDroneHands] Bot hands missing.");
                return;
            }

            DroneHand rightH = BotRightHand.GetComponent<DroneHand>();
            DroneHand leftH = BotLeftHand.GetComponent<DroneHand>();

            if (rightH == null || leftH == null)
            {
                MelonLogger.Warning("[UpdateDroneHands] DroneHand missing on bot hands.");
                return;
            }

            GameObject right = rightH.holding ? rightH.holding.gameObject : null;
            GameObject left = leftH.holding ? leftH.holding.gameObject : null;

            GameObject rightC = (right != null && right.transform.childCount > 0) ? right.transform.GetChild(0).gameObject : null;
            GameObject leftC = (left != null && left.transform.childCount > 0) ? left.transform.GetChild(0).gameObject : null;

            MelonLogger.Msg("[UpdateDroneHands] - Checkpoint2");

            if (_droneRightHolding != null && (right == null || rightC == null || (rightC.name + "(Clone)") != _droneRightHolding.name))
            {
                Destroy(_droneRightHolding);
                _droneRightHolding = null;
            }

            if (_droneLeftHolding != null && (left == null || leftC == null || (leftC.name + "(Clone)") != _droneLeftHolding.name))
            {
                Destroy(_droneLeftHolding);
                _droneLeftHolding = null;
            }

            MelonLogger.Msg("[UpdateDroneHands] - Checkpoint3");

            if (right != null && rightC != null && !right.name.Contains("Headset"))
            {
                if (_droneRightHolding == null)
                {
                    MelonLogger.Msg("[UpdateDroneHands] - Checkpoint4a");
                    GameObject cloneR = Instantiate(rightC);

                    if (cloneR.transform.childCount > 0)
                        Destroy(cloneR.transform.GetChild(0).gameObject);

                    Collider col = cloneR.GetComponent<Collider>();
                    if (col) col.enabled = false;

                    cloneR.transform.SetParent(StaticRightHand.transform, false);
                    MatchCloneScaleToSource(cloneR.transform, rightC.transform);

                    DronePickUp p = right.GetComponent<DronePickUp>();
                    if (p != null)
                    {
                        cloneR.transform.localPosition = p.StaticRightHand_PosOffset;
                        cloneR.transform.localRotation = Quaternion.Euler(p.StaticRightHand_RotOffsetEuler);
                    }

                    _droneRightHolding = cloneR;
                    MelonLogger.Msg("[UpdateDroneHands] - Checkpoint5a");
                }
            }

            MelonLogger.Msg("[UpdateDroneHands] - Checkpoint6");

            if (left != null && leftC != null && !left.name.Contains("Headset"))
            {
                if (_droneLeftHolding == null)
                {
                    MelonLogger.Msg("[UpdateDroneHands] - Checkpoint4b");
                    GameObject cloneL = Instantiate(leftC);

                    if (cloneL.transform.childCount > 0)
                        Destroy(cloneL.transform.GetChild(0).gameObject);

                    Collider col = cloneL.GetComponent<Collider>();
                    if (col) col.enabled = false;

                    cloneL.transform.SetParent(StaticLeftHand.transform, false);
                    MatchCloneScaleToSource(cloneL.transform, leftC.transform);

                    DronePickUp p = left.GetComponent<DronePickUp>();
                    if (p != null)
                    {
                        cloneL.transform.localPosition = p.StaticLeftHand_PosOffset;
                        cloneL.transform.localRotation = Quaternion.Euler(p.StaticLeftHand_RotOffsetEuler);
                    }

                    _droneLeftHolding = cloneL;
                    MelonLogger.Msg("[UpdateDroneHands] - Checkpoint5b");
                }
            }

            MelonLogger.Msg("[UpdateDroneHands] - Checkpoint7");

            if (rightH != null) rightH._delayDrop = true;
            if (leftH != null) leftH._delayDrop = true;

            MelonLogger.Msg("[UpdateDroneHands] - Checkpoint8");
        }

        void BeginDroneOffGuard(Vector3 expectedPos, Quaternion expectedRot)
        {
            if (DroneHost == null && Drone == null) return;
            if (_droneOffGuardRunning) return;

            MelonCoroutines.Start(Co_DroneOffGuard(expectedPos, expectedRot));
        }

        [HideFromIl2Cpp]
        System.Collections.IEnumerator Co_DroneOffGuard(Vector3 expectedPos, Quaternion expectedRot)
        {
            _droneOffGuardRunning = true;

            Rigidbody droneRb = _droneHostRb != null ? _droneHostRb : (DroneHost != null ? DroneHost.GetComponent<Rigidbody>() : null);

            for (int frame = 0; frame < DRONE_OFF_GUARD_FRAMES; frame++)
            {
                yield return null;

                if (DroneHost == null && Drone == null) break;

                Vector3 currentPos = DroneHost != null ? DroneHost.transform.position : Drone.transform.position;
                Quaternion currentRot = DroneHost != null ? DroneHost.transform.rotation : Drone.transform.rotation;

                float posDelta = Vector3.Distance(currentPos, expectedPos);
                float rotDelta = Quaternion.Angle(currentRot, expectedRot);

                bool childDirty = false;
                if (Drone != null)
                {
                    if (Vector3.Distance(Drone.transform.localPosition, _droneChildStartLocalPos) > 0.0005f) childDirty = true;
                    else if (Quaternion.Angle(Drone.transform.localRotation, _droneChildStartLocalRot) > 0.05f) childDirty = true;
                    else if (Vector3.Distance(Drone.transform.localScale, _droneChildStartLocalScale) > 0.0005f) childDirty = true;
                }

                if (posDelta > DRONE_OFF_GUARD_POS_THRESHOLD || rotDelta > DRONE_OFF_GUARD_ROT_THRESHOLD || childDirty)
                {
                    string heldName = "null";
                    bool hostHeld = false;
                    try
                    {
                        if (_droneHostPickUp != null)
                        {
                            hostHeld = _droneHostPickUp.isHeld;
                            if (_droneHostPickUp.heldHand != null && _droneHostPickUp.heldHand.gameObject != null)
                                heldName = _droneHostPickUp.heldHand.gameObject.name;
                        }
                    }
                    catch { }

                    MelonLogger.Warning($"[HeadsetScript] Drone drifted after headset-off on frame {frame + 1}. posDelta={posDelta:F3}, rotDelta={rotDelta:F2}, hostHeld={hostHeld}, heldHand={heldName}. Resetting drone pose.");

                    if (DroneHost != null)
                    {
                        DroneHost.transform.position = expectedPos;
                        DroneHost.transform.rotation = expectedRot;
                    }
                    else if (Drone != null)
                    {
                        Drone.transform.position = expectedPos;
                        Drone.transform.rotation = expectedRot;
                    }

                    if (Drone != null)
                    {
                        Drone.transform.localPosition = _droneChildStartLocalPos;
                        Drone.transform.localRotation = _droneChildStartLocalRot;
                        Drone.transform.localScale = _droneChildStartLocalScale;
                    }

                    if (droneRb != null)
                    {
                        droneRb.velocity = Vector3.zero;
                        droneRb.angularVelocity = Vector3.zero;
                    }
                }
            }

            _droneOffGuardRunning = false;
        }

        void RestoreRealHandStatesAfterHeadsetOff()
        {
            try
            {
                SafeSetActive(RightHand, true);
                SafeSetActive(LeftHand, true);

                if (RightHandRoot == null) RightHandRoot = GameObject.Find("RightHandRoot");
                if (LeftHandRoot == null) LeftHandRoot = GameObject.Find("LeftHandRoot");

                RestoreSingleRealHandState(RightHandRoot);
                RestoreSingleRealHandState(LeftHandRoot);
            }
            catch { }
        }

        void RestoreSingleRealHandState(GameObject handRoot)
        {
            if (handRoot == null) return;

            try
            {
                NormalHandState normal = handRoot.GetComponent<NormalHandState>();
                TelekinesisHandState tk = handRoot.GetComponent<TelekinesisHandState>();


                if (normal != null) normal.enabled = true;
                if (tk != null) tk.enabled = false;
            }
            catch { }
        }

        void CatchStateGlitch()
        {
            if (!RightHandRoot) RightHandRoot = GameObject.Find("RightHandRoot");
            if (!LeftHandRoot) LeftHandRoot = GameObject.Find("LeftHandRoot");
            if (!RightHandRoot || !LeftHandRoot) return;

            NormalHandState nhsR = RightHandRoot.GetComponent<NormalHandState>();
            TelekinesisHandState tkhsR = RightHandRoot.GetComponent<TelekinesisHandState>();
            NormalHandState nhsL = LeftHandRoot.GetComponent<NormalHandState>();
            TelekinesisHandState tkhsL = LeftHandRoot.GetComponent<TelekinesisHandState>();

            if (nhsR != null && tkhsR != null && nhsR.enabled && tkhsR.enabled) tkhsR.enabled = false;
            if (nhsL != null && tkhsL != null && nhsL.enabled && tkhsL.enabled) tkhsL.enabled = false;
        }

        void RestoreHeadsetScale()
        {
            try
            {
                if (_headsetRoot != null)
                    _headsetRoot.localScale = _headsetRootStartScale;

                transform.localScale = _headsetStartScale;
            }
            catch { }
        }

        void RefreshDroneVectors()
        {
            if (hotSpot != null)
            {
                _droneLocalPos = hotSpot.Position;
                _droneLocalRot = hotSpot.Rotation;
            }
            else if (DroneHost != null)
            {
                _droneLocalPos = DroneHost.transform.position;
                _droneLocalRot = DroneHost.transform.rotation.eulerAngles;
            }
            else if (Drone != null)
            {
                _droneLocalPos = Drone.transform.position;
                _droneLocalRot = Drone.transform.rotation.eulerAngles;
            }
        }

        void ToggleDronePickups(bool headsetOn)
        {
            if (bank == null || bank.PickUps == null) return;

            for (int i = 0; i < bank.PickUps.Count; i++)
            {
                GameObject pu = bank.PickUps[i];
                if (pu == null) continue;

                var dpu = pu.GetComponent<DronePickUp>();
                if (dpu != null) dpu.enabled = headsetOn;
            }

            for (int i = 0; i < bank.RotMotions.Count; i++)
            {
                GameObject rm = bank.RotMotions[i];
                if (rm == null) continue;

                DroneRotationalMotion drm = rm.GetComponent<DroneRotationalMotion>();
                if (drm != null) drm.enabled = headsetOn;

                RotationalMotion rotMot = GetHostRotationalMotion(rm);
                if (rotMot == null) continue;

                if (headsetOn)
                {
                    SaveRotationalMotionState(rotMot);
                    rotMot.enabled = false;
                }
                else
                {
                    rotMot.enabled = true;
                    RestoreRotationalMotionState(rotMot);
                }
            }
        }

        RotationalMotion GetHostRotationalMotion(GameObject rm)
        {
            if (rm == null) return null;

            RotationalMotion rotMot = null;

            if (rm.transform.parent != null)
                rotMot = rm.transform.parent.GetComponent<RotationalMotion>();

            if (rotMot == null)
                rotMot = rm.GetComponent<RotationalMotion>();

            return rotMot;
        }

        void SaveRotationalMotionState(RotationalMotion rotMot)
        {
            if (rotMot == null) return;

            float current = rotMot.CurrentRotation;
            float start = rotMot.StartRotation;
            float end = rotMot.EndRotation;

            if (Mathf.Abs(current - start) <= ROT_SAVE_EPSILON && Mathf.Abs(end - start) > ROT_SAVE_EPSILON)
                return;

            int key = rotMot.gameObject.GetInstanceID();
            _savedRotMotionAngles[key] = current;
        }

        void RestoreRotationalMotionState(RotationalMotion rotMot)
        {
            if (rotMot == null) return;

            int key = rotMot.gameObject.GetInstanceID();
            if (!_savedRotMotionAngles.TryGetValue(key, out float saved))
                return;

            MelonCoroutines.Start(Co_RestoreRotationalMotionState(rotMot, saved));
        }

        [HideFromIl2Cpp]
        System.Collections.IEnumerator Co_RestoreRotationalMotionState(RotationalMotion rotMot, float saved)
        {
            yield return null;
            if (rotMot == null) yield break;

            try
            {
                rotMot.SetRotation(saved);
            }
            catch { }

            yield return null;
            if (rotMot == null) yield break;

            float current = rotMot.CurrentRotation;
            if (Mathf.Abs(current - saved) > 0.05f)
            {
                try
                {
                    rotMot.SetRotation(saved);
                }
                catch { }
            }
        }

        void ToggleVisual(bool turnOn)
        {
            MelonCoroutines.Start(Co_ToggleVisual(turnOn));
        }

        [HideFromIl2Cpp]
        System.Collections.IEnumerator Co_ToggleVisual(bool turnOn)
        {
            yield return null;

            if (driver == null)
                driver = GetComponent<HeadsetDriver>();

            if (driver == null)
                yield break;

            if (turnOn) driver.Enable();
            else driver.Disable();
        }

        void SafeSetActive(GameObject go, bool on)
        {
            if (go == null) return;
            if (go.activeSelf == on) return;
            go.SetActive(on);
        }

        void IgnoreHeadsetDroneCollisions()
        {
            if (Drone == null && DroneHost == null) return;
            if (_headsetRoot == null) return;

            try
            {
                var headsetCols = _headsetRoot.GetComponentsInChildren<Collider>(true);
                var droneCols = DroneHost != null ? DroneHost.GetComponentsInChildren<Collider>(true) : Drone.GetComponentsInChildren<Collider>(true);

                for (int i = 0; i < headsetCols.Length; i++)
                {
                    var a = headsetCols[i];
                    if (a == null) continue;

                    for (int j = 0; j < droneCols.Length; j++)
                    {
                        var b = droneCols[j];
                        if (b == null) continue;

                        Physics.IgnoreCollision(a, b, true);
                    }
                }
            }
            catch { }
        }
        [HideFromIl2Cpp]
        System.Collections.IEnumerator Co_EnableBotHandsAfterTransition(int token)
        {
            float endTime = Time.time + 0.25f;

            while (Time.time < endTime)
            {
                if (!wearing || token != _droneHandEnableToken)
                    yield break;

                bool anyPressed = false;
                try
                {
                    if (RightHandRoot != null)
                    {
                        VRHandInput vhiR = RightHandRoot.GetComponent<VRHandInput>();
                        if (vhiR != null && vhiR.IsAnyInputPressed()) anyPressed = true;
                    }
                    if (LeftHandRoot != null)
                    {
                        VRHandInput vhiL = LeftHandRoot.GetComponent<VRHandInput>();
                        if (vhiL != null && vhiL.IsAnyInputPressed()) anyPressed = true;
                    }
                }
                catch { }

                if (!anyPressed)
                    break;

                yield return null;
            }

            if (!wearing || token != _droneHandEnableToken)
                yield break;

            SafeSetActive(BotRightHand, true);
            SafeSetActive(BotLeftHand, true);
        }

    }

    [HarmonyPatch(typeof(TelekinesisHandState), "HandUpdate")]
    internal static class HeadsetTKBlockPatch
    {
        static bool _reflected = false;

        static PropertyInfo _piHeldInteractable;
        static FieldInfo _fiHeldInteractable;
        static FieldInfo _fiHeldObject;

        static PropertyInfo _piFocusedInteractable;
        static FieldInfo _fiFocusedInteractable;
        static FieldInfo _fiFocusedObject;

        static MethodInfo _miResetReticleTarget;
        static MethodInfo _miResetRaycastOrigin;

        static void Postfix(TelekinesisHandState __instance)
        {
            if (__instance == null) return;

            HeadsetScript hs = HeadsetScript.Instance;
            if (hs == null || (!hs.IsWearing && !hs.InDroneTkExitLockout)) return;

            VRHandInput vhi = __instance.GetComponent<VRHandInput>();
            if (vhi == null) vhi = __instance.gameObject.GetComponent<VRHandInput>();
            if (vhi == null) return;

            EnsureReflection(vhi);


            bool clearHeadsetTargets = false;
            bool clearDroneTargets = hs.IsWearing || hs.InDroneTkExitLockout;

            bool changed = false;

            changed |= ClearMemberIfTarget(vhi, _piHeldInteractable, clearHeadsetTargets, clearDroneTargets);
            changed |= ClearMemberIfTarget(vhi, _fiHeldInteractable, clearHeadsetTargets, clearDroneTargets);
            changed |= ClearMemberIfTarget(vhi, _fiHeldObject, clearHeadsetTargets, clearDroneTargets);

            changed |= ClearMemberIfTarget(vhi, _piFocusedInteractable, clearHeadsetTargets, clearDroneTargets);
            changed |= ClearMemberIfTarget(vhi, _fiFocusedInteractable, clearHeadsetTargets, clearDroneTargets);
            changed |= ClearMemberIfTarget(vhi, _fiFocusedObject, clearHeadsetTargets, clearDroneTargets);

            if (changed)
            {
                TryInvoke(vhi, _miResetReticleTarget);
                TryInvoke(vhi, _miResetRaycastOrigin);
            }
        }

        internal static void EnsureReflectionForExternal(VRHandInput vhi)
        {
            EnsureReflection(vhi);
        }

        internal static void ClearFocusedAndHeldMembersExternally(VRHandInput vhi, bool includeHeadsetTargets, bool includeDroneTargets, bool forceReticleReset)
        {
            if (vhi == null) return;

            bool changed = false;
            changed |= ClearMemberIfTarget(vhi, _piHeldInteractable, includeHeadsetTargets, includeDroneTargets);
            changed |= ClearMemberIfTarget(vhi, _fiHeldInteractable, includeHeadsetTargets, includeDroneTargets);
            changed |= ClearMemberIfTarget(vhi, _fiHeldObject, includeHeadsetTargets, includeDroneTargets);
            changed |= ClearMemberIfTarget(vhi, _piFocusedInteractable, includeHeadsetTargets, includeDroneTargets);
            changed |= ClearMemberIfTarget(vhi, _fiFocusedInteractable, includeHeadsetTargets, includeDroneTargets);
            changed |= ClearMemberIfTarget(vhi, _fiFocusedObject, includeHeadsetTargets, includeDroneTargets);

            if (changed || forceReticleReset)
            {
                if (forceReticleReset)
                {
                    try { vhi.FocusedInteractable = null; } catch { }
                    try { vhi._FocusedInteractable_k__BackingField = null; } catch { }
                    try { vhi._ReticleTarget = null; } catch { }
                }

                TryInvoke(vhi, _miResetReticleTarget);
                TryInvoke(vhi, _miResetRaycastOrigin);
            }
        }

        static void EnsureReflection(VRHandInput vhi)
        {
            if (_reflected) return;
            _reflected = true;

            try
            {
                Type t = vhi.GetType();
                BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                _piHeldInteractable = t.GetProperty("HeldInteractable", F);
                _fiHeldInteractable = t.GetField("heldInteractable", F)
                    ?? t.GetField("_heldInteractable", F)
                    ?? t.GetField("m_heldInteractable", F);

                _fiHeldObject = t.GetField("heldObject", F)
                    ?? t.GetField("_heldObject", F)
                    ?? t.GetField("m_heldObject", F);

                _piFocusedInteractable = t.GetProperty("FocusedInteractable", F);
                _fiFocusedInteractable = t.GetField("focusedInteractable", F)
                    ?? t.GetField("_focusedInteractable", F)
                    ?? t.GetField("m_focusedInteractable", F);

                _fiFocusedObject = t.GetField("focusedObject", F)
                    ?? t.GetField("_focusedObject", F)
                    ?? t.GetField("m_focusedObject", F);

                _miResetReticleTarget = t.GetMethod("ResetReticleTarget", F);
                _miResetRaycastOrigin = t.GetMethod("ResetRaycastOrigin", F);

                MelonLogger.Msg(
                    "[HeadsetTKBlock] Reflection ready: " +
                    "heldProp=" + (_piHeldInteractable != null) + " " +
                    "heldField=" + (_fiHeldInteractable != null) + " " +
                    "heldObj=" + (_fiHeldObject != null) + " " +
                    "focusProp=" + (_piFocusedInteractable != null) + " " +
                    "focusField=" + (_fiFocusedInteractable != null) + " " +
                    "focusObj=" + (_fiFocusedObject != null)
                );
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[HeadsetTKBlock] Reflection init failed: " + e.Message);
            }
        }

        static void TryInvoke(object target, MethodInfo mi)
        {
            if (target == null || mi == null) return;

            try
            {
                mi.Invoke(target, null);
            }
            catch { }
        }

        static bool ClearMemberIfTarget(object target, PropertyInfo p, bool includeHeadsetTargets, bool includeDroneTargets)
        {
            if (target == null || p == null) return false;

            try
            {
                object value = p.GetValue(target, null);
                if (!IsBlockedTarget(value, includeHeadsetTargets, includeDroneTargets)) return false;
                if (!p.CanWrite) return false;

                p.SetValue(target, null, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        static bool ClearMemberIfTarget(object target, FieldInfo f, bool includeHeadsetTargets, bool includeDroneTargets)
        {
            if (target == null || f == null) return false;

            try
            {
                object value = f.GetValue(target);
                if (!IsBlockedTarget(value, includeHeadsetTargets, includeDroneTargets)) return false;

                f.SetValue(target, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        static bool IsBlockedTarget(object o, bool includeHeadsetTargets, bool includeDroneTargets)
        {
            if (o == null) return false;

            GameObject go = null;

            if (o is GameObject)
                go = (GameObject)o;
            else if (o is Component)
                go = ((Component)o).gameObject;
            else
                return false;

            if (go == null) return false;

            if (includeHeadsetTargets)
            {
                if (go.name.Contains("Headset"))
                    return true;

                if (go.GetComponent<HeadsetScript>() != null)
                    return true;

                if (go.GetComponentInChildren<HeadsetScript>(true) != null)
                    return true;
            }

            HeadsetScript hs = HeadsetScript.Instance;
            if (includeDroneTargets && hs != null)
            {
                if (hs.DroneHostObj != null && (go == hs.DroneHostObj || go.transform.IsChildOf(hs.DroneHostObj.transform)))
                    return true;

                if (hs.DroneObj != null && (go == hs.DroneObj || go.transform.IsChildOf(hs.DroneObj.transform)))
                    return true;
            }

            if (includeHeadsetTargets && go.transform.parent != null)
            {
                Transform root = go.transform.root;
                if (root != null)
                {
                    if (root.name.Contains("Headset"))
                        return true;

                    if (root.GetComponent<HeadsetScript>() != null)
                        return true;

                    if (root.GetComponentInChildren<HeadsetScript>(true) != null)
                        return true;
                }
            }

            return false;
        }

    }
}
