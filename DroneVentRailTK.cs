using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using SG.Phoenix.Assets.Code.Interactables;
using SG.Phoenix.Assets.Code.InputManagement;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{

    public class DroneVentRailTK : MonoBehaviour
    {
        public DroneVentRailTK(IntPtr ptr) : base(ptr) { }
        public DroneVentRailTK() : base(ClassInjector.DerivedConstructorPointer<DroneVentRailTK>())
            => ClassInjector.DerivedConstructorBody(this);

        public Vector3 VentStartPos = new Vector3(0.0571f, 8.1911f, 0.4597f);
        public float EndZ = 11.32f;

        public float ShutterZ = 2.880565f;
        public float ShutterBuffer = 0.25f;
        public bool ShuttersOpen = false;

        public float StickSpeed = 1.55f;
        public float StickDeadzone = 0.15f;
        public float ExitBackThreshold = 0.65f;
        public float MouthEpsilon = 0.08f;

        public float EnterForwardThreshold = 0.35f;
        public float RailToggleCooldown = 0.20f;

        public float HandDriveGain = 2.8f;
        public float HandDeadzone = 0.0025f;

        public float GrabInputIgnoreSeconds = 0.12f;
        public float ArmNeutralMultiplier = 0.60f;

        public float CenterBlendDistanceZ = 0.75f;
        public float CenterFollowSpeed = 2.2f;

        public float WiggleRadiusX = 0.055f;
        public float WiggleRadiusY = 0.028f;
        public float WiggleHandGainX = 0.55f;
        public float WiggleHandGainY = 0.35f;
        public float WiggleSpring = 18.0f;
        public float WiggleDamping = 10.0f;

        public bool ClampHandMotionInsideVent = true;
        public float VentRailClampExtraX = 0.005f;
        public float VentRailClampExtraY = 0.005f;

        public float DipStartZ = 11.0f;
        public float DipExitBuffer = 0.10f;
        public float DipY = 8.02f;
        public float DipGravity = 18.0f;
        public float DipRiseSpeed = 2.2f;

        public bool EnableDebugLogs = true;
        public float DebugLogInterval = 0.20f;

        public bool EnableTkBeamOverride = true;
        public float BeamStartForwardOffset = 0.015f;
        public float BeamEndBackOffset = 0.02f;

        public bool EnableRegrabDiagnostics = true;
        public float RegrabProbeDuration = 0.40f;
        public float RegrabProbeInterval = 0.05f;

        public bool EnableVanillaTkSnapshotSync = true;
        public bool SyncSnapshotOnVentEnter = true;
        public bool SyncSnapshotOnVentRegrab = true;
        public bool SyncSnapshotOnVentExit = true;

        public bool InVentRail = false;
        public bool SuppressingVanillaTk = false;

        private PickUp _pickup;
        private Rigidbody _rb;
        private Collider[] _cols;
        private bool[] _colWasEnabled;

        private GameObject _rightHand;
        private GameObject _leftHand;
        private TelekinesisHandState _rightTkState;
        private TelekinesisHandState _leftTkState;
        private LineRenderer _rightTkBeam;
        private LineRenderer _leftTkBeam;
        private Vector3[] _beamPositions = new Vector3[0];
        private bool _beamOverrideActive = false;
        private bool _loggedBeamMissing = false;
        private bool _useRight = true;
        private VRHandInput _railOwnerVhi;
        private bool _railOwnerUseRight = true;
        private float _suppressOwnerTkUntilTime = -999f;

        private float _trueCenterX;
        private float _standardY;
        private float _entryX;
        private float _entryY;
        private float _entryZ;
        private float _baseX;
        private float _baseY;
        private Vector3 _entryHandPos;
        private float _wiggleX;
        private float _wiggleY;
        private float _wiggleVx;
        private float _wiggleVy;
        private float _lastHandZ;
        private bool _insideVentVolume = false;
        private bool _exitLockout = false;
        private float _lastToggleTime = -999f;
        private bool _collidersTemporarilyRestoredForRegrab = false;
        private bool _movementArmed = false;
        private float _ignoreMovementUntilTime = -999f;
        private float _yVel = 0f;
        private bool _dipLatched = false;
        private bool _wasPressingIntoShutter = false;
        private float _lastShutterHitTime = -999f;
        public float ShutterHitSoundCooldown = 0.20f;
        private bool _prevShuttersOpen = true;
        private int _closedShutterSide = 0;
        private float _lastDebugLogTime = -999f;
        private bool _prevHeldInUpdate = false;
        private bool _regrabProbeActive = false;
        private float _regrabProbeEndTime = -999f;
        private float _regrabProbeNextLogTime = -999f;
        private int _regrabProbeTick = 0;
        private GameObject _rightTkReticle;
        private GameObject _leftTkReticle;
        private VRHandInput _rightVhi;
        private VRHandInput _leftVhi;
        private GameObject _ventColliderObject;

        private void Start()
        {
            _pickup = gameObject.GetComponent<PickUp>() ?? gameObject.GetComponentInChildren<PickUp>();
            _rb = gameObject.GetComponent<Rigidbody>();
            _rightHand = GameObject.Find("RightHandRoot");
            _leftHand = GameObject.Find("LeftHandRoot");
            _rightVhi = _rightHand != null ? _rightHand.GetComponent<VRHandInput>() : null;
            _leftVhi = _leftHand != null ? _leftHand.GetComponent<VRHandInput>() : null;
            _ventColliderObject = GameObject.Find("VentCollider");
            SetVentColliderLayer(0);
            _rightTkState = _rightHand != null ? _rightHand.GetComponent<TelekinesisHandState>() : null;
            _leftTkState = _leftHand != null ? _leftHand.GetComponent<TelekinesisHandState>() : null;
            _rightTkBeam = FindTkBeam(_rightTkState, _rightHand);
            _leftTkBeam = FindTkBeam(_leftTkState, _leftHand);
            _rightTkReticle = FindTkReticle(_rightTkState, _rightHand);
            _leftTkReticle = FindTkReticle(_leftTkState, _leftHand);

            _trueCenterX = VentStartPos.x;
            _standardY = VentStartPos.y;

            _cols = gameObject.GetComponentsInChildren<Collider>(true);
            _colWasEnabled = new bool[_cols.Length];
            for (int i = 0; i < _cols.Length; i++)
                _colWasEnabled[i] = _cols[i] != null && _cols[i].enabled;

            if (Mathf.Abs(DipY - 8.02f) < 0.0001f)
                DipY = _standardY - 0.18f;

            Vector3 p = gameObject.transform.position;
            _baseX = p.x;
            _baseY = p.y;
        }

        private void Update()
        {
            if (_pickup == null) return;

            UpdateShutterBarrierState();

            bool heldNow = _pickup.isHeld;
            bool releasedInVent = InVentRail && !heldNow && _prevHeldInUpdate;

            if (_regrabProbeActive)
                TickRegrabProbe();

            if (releasedInVent)
            {

                _suppressOwnerTkUntilTime = Time.time + 0.22f;
                RestoreVanillaTkAimAfterVentDrop();
            }

            if (InVentRail && heldNow && !_prevHeldInUpdate)
            {
                CaptureRailOwnerFromCurrentHeldHand();

                if (EnableVanillaTkSnapshotSync && SyncSnapshotOnVentRegrab)
                    SyncVanillaTkSnapshotToVentMouth("RegrabInVent");

                if (EnableRegrabDiagnostics)
                    StartRegrabProbe("HeldRiseInVent");
            }

            _prevHeldInUpdate = heldNow;

            if (heldNow && _insideVentVolume && !InVentRail && !_exitLockout && CanToggleNow())
            {
                RefreshHeldHandSide();
                EnterRailMode("GrabInsideVent", false);
            }

            if (!InVentRail)
            {
                if (_beamOverrideActive)
                    StopOverridingBeam();
                return;
            }

            RefreshHeldHandSide();

            if (!heldNow)
            {
                if (!_collidersTemporarilyRestoredForRegrab)
                {
                    RestoreAllCollidersToOriginal();
                    _collidersTemporarilyRestoredForRegrab = true;
                }

                ClampToRailNoMove(Time.deltaTime);
                return;
            }

            if (_collidersTemporarilyRestoredForRegrab)
            {
                DisableNonTriggerCollidersOnly();
                _collidersTemporarilyRestoredForRegrab = false;
                _movementArmed = false;
                _ignoreMovementUntilTime = Time.time + GrabInputIgnoreSeconds;

                GameObject hand = GetActiveHandObject();
                if (hand != null)
                {
                    _lastHandZ = hand.transform.position.z;
                    _entryHandPos = hand.transform.position;
                }
            }
        }

        public void RailTick(VRHandInput vhi, float dt)
        {
            if (!InVentRail) return;
            if (_rb == null || _pickup == null) return;

            if (!_pickup.isHeld)
            {
                ClampToRailNoMove(dt);
                return;
            }

            RefreshHeldHandSide();

            float rawStickY = GetStickY(vhi);
            if (Time.time < _ignoreMovementUntilTime)
            {
                GameObject bufferedHand = GetActiveHandObject();
                if (bufferedHand != null)
                    _lastHandZ = bufferedHand.transform.position.z;
                rawStickY = 0f;
            }

            float neutralCutoff = StickDeadzone * ArmNeutralMultiplier;
            if (!_movementArmed)
            {
                if (Mathf.Abs(rawStickY) <= neutralCutoff)
                    _movementArmed = true;
                rawStickY = 0f;
            }

            float stickY = rawStickY;
            if (Mathf.Abs(stickY) < StickDeadzone)
                stickY = 0f;

            float moveDeltaZ = stickY * StickSpeed * dt;

            GameObject activeHand = GetActiveHandObject();
            if (_movementArmed && HandDriveGain > 0.0001f && activeHand != null)
            {
                float hz = activeHand.transform.position.z;
                float handDz = hz - _lastHandZ;
                if (Mathf.Abs(handDz) > HandDeadzone)
                    moveDeltaZ += handDz * HandDriveGain;
                _lastHandZ = hz;
            }

            Vector3 p = gameObject.transform.position;
            float startZ = VentStartPos.z;
            float attemptedZ = p.z + moveDeltaZ;
            float z = attemptedZ;

            if (z > EndZ) z = EndZ;
            if (z < startZ) z = startZ;

            bool pressingIntoClosedShutter = false;
            z = ApplyClosedShutterBarrier(p.z, z, moveDeltaZ, out pressingIntoClosedShutter);

            if (!_dipLatched)
            {
                if (z >= DipStartZ) _dipLatched = true;
            }
            else
            {
                if (z <= DipStartZ - DipExitBuffer)
                {
                    _dipLatched = false;
                    _yVel = 0f;
                }
            }

            float t = 1f;
            if (CenterBlendDistanceZ > 0.0001f)
                t = Mathf.Clamp01((z - _entryZ) / CenterBlendDistanceZ);

            float desiredBaseX = Mathf.Lerp(_entryX, _trueCenterX, t);
            float desiredBaseY = Mathf.Lerp(_entryY, _standardY, t);
            _baseX = Mathf.MoveTowards(_baseX, desiredBaseX, CenterFollowSpeed * dt);
            _baseY = Mathf.MoveTowards(_baseY, desiredBaseY, CenterFollowSpeed * dt);

            float targetWiggleX = 0f;
            float targetWiggleY = 0f;
            if (_movementArmed && activeHand != null)
            {
                Vector3 d = activeHand.transform.position - _entryHandPos;
                targetWiggleX = Mathf.Clamp(d.x * WiggleHandGainX, -WiggleRadiusX, WiggleRadiusX);
                if (!_dipLatched)
                    targetWiggleY = Mathf.Clamp(d.y * WiggleHandGainY, -WiggleRadiusY, WiggleRadiusY);
            }

            _wiggleVx += (targetWiggleX - _wiggleX) * WiggleSpring * dt;
            _wiggleVx *= Mathf.Exp(-WiggleDamping * dt);
            _wiggleX += _wiggleVx * dt;

            _wiggleVy += (targetWiggleY - _wiggleY) * WiggleSpring * dt;
            _wiggleVy *= Mathf.Exp(-WiggleDamping * dt);
            _wiggleY += _wiggleVy * dt;

            _wiggleX = Mathf.Clamp(_wiggleX, -WiggleRadiusX, WiggleRadiusX);
            _wiggleY = Mathf.Clamp(_wiggleY, -WiggleRadiusY, WiggleRadiusY);

            float y = p.y;
            bool retracting = stickY < -StickDeadzone;
            if (_dipLatched && retracting && y < _standardY - 0.001f)
            {
                z = p.z;
                y = Mathf.MoveTowards(y, _standardY, DipRiseSpeed * dt);
                _yVel = 0f;
            }
            else
            {
                if (_dipLatched)
                {
                    if (y > DipY + 0.0005f)
                    {
                        _yVel -= DipGravity * dt;
                        y += _yVel * dt;
                        if (y <= DipY)
                        {
                            y = DipY;
                            _yVel = 0f;
                        }
                    }
                    else
                    {
                        y = DipY;
                        _yVel = 0f;
                    }
                }
                else
                {
                    y = _baseY + _wiggleY;
                    _yVel = 0f;
                }
            }

            float finalX = _baseX + _wiggleX;
            gameObject.transform.position = ClampPoseInsideVent(finalX, y, z);

            UpdateTkBeamOverride();

            bool atMouth = Mathf.Abs(z - startZ) <= MouthEpsilon;
            if (atMouth && stickY < -ExitBackThreshold)
            {
                ExitRailMode(true);
                return;
            }

            MaybeDebugLog("[VentRail] RailTick pos=" + FormatVec(gameObject.transform.position) +
                          " stickY=" + stickY.ToString("F3") +
                          " moveDeltaZ=" + moveDeltaZ.ToString("F3") +
                          " suppressing=" + SuppressingVanillaTk +
                          " held=" + _pickup.isHeld);
        }

        private void ClampToRailNoMove(float dt)
        {
            Vector3 p = gameObject.transform.position;
            float z = p.z;
            float startZ = VentStartPos.z;

            if (z < startZ) z = startZ;
            if (z > EndZ) z = EndZ;

            bool pressingIntoClosedShutter = false;
            z = ApplyClosedShutterBarrier(p.z, z, 0f, out pressingIntoClosedShutter);

            if (!_dipLatched)
            {
                if (z >= DipStartZ) _dipLatched = true;
            }
            else
            {
                if (z <= DipStartZ - DipExitBuffer)
                {
                    _dipLatched = false;
                    _yVel = 0f;
                }
            }

            _wiggleVx += (0f - _wiggleX) * WiggleSpring * dt;
            _wiggleVx *= Mathf.Exp(-WiggleDamping * dt);
            _wiggleX += _wiggleVx * dt;

            _wiggleVy += (0f - _wiggleY) * WiggleSpring * dt;
            _wiggleVy *= Mathf.Exp(-WiggleDamping * dt);
            _wiggleY += _wiggleVy * dt;

            _wiggleX = Mathf.Clamp(_wiggleX, -WiggleRadiusX, WiggleRadiusX);
            _wiggleY = Mathf.Clamp(_wiggleY, -WiggleRadiusY, WiggleRadiusY);

            float t = 1f;
            if (CenterBlendDistanceZ > 0.0001f)
                t = Mathf.Clamp01((z - _entryZ) / CenterBlendDistanceZ);

            float desiredBaseX = Mathf.Lerp(_entryX, _trueCenterX, t);
            float desiredBaseY = Mathf.Lerp(_entryY, _standardY, t);
            _baseX = Mathf.MoveTowards(_baseX, desiredBaseX, CenterFollowSpeed * dt);
            _baseY = Mathf.MoveTowards(_baseY, desiredBaseY, CenterFollowSpeed * dt);

            float y = p.y;
            if (_dipLatched)
            {
                if (y > DipY + 0.0005f)
                {
                    _yVel -= DipGravity * dt;
                    y += _yVel * dt;
                    if (y <= DipY)
                    {
                        y = DipY;
                        _yVel = 0f;
                    }
                }
                else
                {
                    y = DipY;
                    _yVel = 0f;
                }
            }
            else
            {
                y = _baseY + _wiggleY;
                _yVel = 0f;
            }

            float finalX = _baseX + _wiggleX;
            gameObject.transform.position = ClampPoseInsideVent(finalX, y, z);

            UpdateTkBeamOverride();

            if (_rb != null)
            {
                _rb.isKinematic = true;
                _rb.velocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
        }

        private float GetStickY(VRHandInput vhi = null)
        {
            try
            {

                if (vhi == null)
                    RefreshHeldHandSide();

                if (vhi == null)
                    vhi = GetActiveVhi();

                if (vhi != null)
                    return vhi.TelekinesisVector.y;
            }
            catch
            {
            }

            return 0f;
        }

        private void RefreshHeldHandSide()
        {
            if (_pickup != null && _pickup.heldHand != null && _pickup.heldHand.gameObject != null)
                _useRight = _pickup.heldHand.gameObject.name == "RightHandRoot";
        }

        private void CaptureRailOwnerFromCurrentHeldHand()
        {
            if (_pickup == null || _pickup.heldHand == null || _pickup.heldHand.gameObject == null)
                return;

            _railOwnerVhi = _pickup.heldHand;
            _railOwnerUseRight = _pickup.heldHand.gameObject.name == "RightHandRoot";
            _useRight = _railOwnerUseRight;
            _suppressOwnerTkUntilTime = Time.time + 0.08f;
        }

        private void ClearRailOwner()
        {
            _railOwnerVhi = null;
            _suppressOwnerTkUntilTime = -999f;
        }

        private bool HasRailOwner()
        {
            return _railOwnerVhi != null;
        }

        private bool UseRightForRailOwner()
        {
            return HasRailOwner() ? _railOwnerUseRight : _useRight;
        }

        private GameObject GetActiveHandObject()
        {
            return _useRight ? _rightHand : _leftHand;
        }

        private GameObject GetRailOwnerHandObject()
        {
            return UseRightForRailOwner() ? _rightHand : _leftHand;
        }

        private bool CanToggleNow()
        {
            return Time.time - _lastToggleTime >= RailToggleCooldown;
        }

        private void MarkToggled()
        {
            _lastToggleTime = Time.time;
        }

        private void TryEnterRailMode(string reason)
        {
            if (InVentRail) return;
            if (!_insideVentVolume) return;
            if (!CanToggleNow()) return;
            if (_pickup == null || !_pickup.isHeld) return;
            if (_exitLockout) return;

            RefreshHeldHandSide();
            EnterRailMode(reason, false);
        }

        private void EnterRailMode(string reason, bool snapToStart)
        {
            if (InVentRail) return;
            if (!CanToggleNow()) return;

            InVentRail = true;
            SetVentColliderLayer(18);
            SuppressingVanillaTk = true;
            _loggedBeamMissing = false;
            CaptureRailOwnerFromCurrentHeldHand();
            MarkToggled();
            _collidersTemporarilyRestoredForRegrab = false;
            _movementArmed = false;
            _ignoreMovementUntilTime = Time.time + GrabInputIgnoreSeconds;

            if (_rb != null)
            {
                _rb.isKinematic = true;
                _rb.velocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            DisableNonTriggerCollidersOnly();

            Vector3 p = gameObject.transform.position;
            float startZ = VentStartPos.z;
            float z = p.z;

            if (z < startZ) z = startZ;
            if (z > EndZ) z = EndZ;

            UpdateShutterBarrierState();
            if (!ShuttersOpen)
            {
                if (z <= ShutterZ - ShutterBuffer)
                    _closedShutterSide = -1;
                else if (z >= ShutterZ + ShutterBuffer)
                    _closedShutterSide = 1;
                else
                    _closedShutterSide = (z >= ShutterZ) ? 1 : -1;

                bool pressingIntoClosedShutter = false;
                z = ApplyClosedShutterBarrier(z, z, 0f, out pressingIntoClosedShutter);
            }

            if (snapToStart)
            {
                gameObject.transform.position = new Vector3(_trueCenterX, _standardY, VentStartPos.z);
                p = gameObject.transform.position;
                z = p.z;
            }
            else
            {
                gameObject.transform.position = new Vector3(p.x, p.y, z);
                p = gameObject.transform.position;
            }

            _entryX = p.x;
            _entryY = p.y;
            _entryZ = p.z;
            _baseX = _entryX;
            _baseY = _entryY;
            _wiggleX = 0f;
            _wiggleY = 0f;
            _wiggleVx = 0f;
            _wiggleVy = 0f;
            _dipLatched = false;
            _yVel = 0f;

            GameObject hand = GetActiveHandObject();
            if (hand != null)
            {
                _lastHandZ = hand.transform.position.z;
                _entryHandPos = hand.transform.position;
            }

            _prevHeldInUpdate = (_pickup != null && _pickup.isHeld);

            if (EnableVanillaTkSnapshotSync && SyncSnapshotOnVentEnter)
                SyncVanillaTkSnapshotToVentMouth("EnterRailMode:" + reason);

            MaybeDebugLog("[VentRail] EnterRailMode reason=" + reason +
                          " pos=" + FormatVec(gameObject.transform.position) +
                          " suppressingVanillaTk=True");
        }

        private void ExitRailMode(bool setExitLockout)
        {
            if (!InVentRail) return;
            if (!CanToggleNow()) return;

            if (EnableVanillaTkSnapshotSync && SyncSnapshotOnVentExit)
                SyncVanillaTkSnapshotToVentMouth("ExitRailMode");

            InVentRail = false;
            SetVentColliderLayer(0);
            SuppressingVanillaTk = false;
            StopOverridingBeam();
            ClearRailOwner();
            MarkToggled();
            _collidersTemporarilyRestoredForRegrab = false;

            if (setExitLockout)
                _exitLockout = true;

            _regrabProbeActive = false;

            RestoreAllCollidersToOriginal();
            _dipLatched = false;
            _yVel = 0f;

            if (_rb != null)
            {
                _rb.isKinematic = false;
                _rb.velocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            MaybeDebugLog("[VentRail] ExitRailMode lockout=" + setExitLockout +
                          " pos=" + FormatVec(gameObject.transform.position) +
                          " suppressingVanillaTk=False");
        }

        public void ForceExitRailForHeadsetMode()
        {
            bool wasInRail = InVentRail;
            bool wasSuppressing = SuppressingVanillaTk;

            InVentRail = false;
            SuppressingVanillaTk = false;
            SetVentColliderLayer(0);
            StopOverridingBeam();
            ForceClearTkBeamOverrideForHeadsetMode();
            ClearRailOwner();

            _collidersTemporarilyRestoredForRegrab = false;
            _movementArmed = false;
            _ignoreMovementUntilTime = -999f;
            _regrabProbeActive = false;
            _regrabProbeEndTime = -999f;
            _regrabProbeNextLogTime = -999f;
            _dipLatched = false;
            _yVel = 0f;
            _wiggleX = 0f;
            _wiggleY = 0f;
            _wiggleVx = 0f;
            _wiggleVy = 0f;
            _wasPressingIntoShutter = false;
            _prevHeldInUpdate = false;

            _exitLockout = false;
            _lastToggleTime = -999f;

            RestoreAllCollidersToOriginal();

            if (_rb != null)
            {
                _rb.isKinematic = false;
                _rb.velocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            if (wasInRail || wasSuppressing || _beamOverrideActive)
                MelonLogger.Msg("[VentRail] ForceExitRailForHeadsetMode wasInRail=" + wasInRail + " wasSuppressing=" + wasSuppressing + " pos=" + FormatVec(gameObject.transform.position));
        }

        private void SetVentColliderLayer(int layer)
        {
            if (_ventColliderObject == null)
                _ventColliderObject = GameObject.Find("VentCollider");

            if (_ventColliderObject != null)
                _ventColliderObject.layer = layer;
        }

        private void DisableNonTriggerCollidersOnly()
        {
            if (_cols == null) return;
            for (int i = 0; i < _cols.Length; i++)
            {
                Collider c = _cols[i];
                if (c == null) continue;
                _colWasEnabled[i] = c.enabled;
                if (!c.isTrigger)
                    c.enabled = false;
            }
        }

        private void RestoreAllCollidersToOriginal()
        {
            if (_cols == null) return;
            for (int i = 0; i < _cols.Length; i++)
            {
                Collider c = _cols[i];
                if (c == null) continue;
                c.enabled = _colWasEnabled[i];
            }
        }
        private Vector3 ClampPoseInsideVent(float x, float y, float z)
        {
            if (!ClampHandMotionInsideVent)
                return new Vector3(x, y, z);

            float safeHalfX = Mathf.Max(0.001f, WiggleRadiusX + VentRailClampExtraX);
            float safeHalfY = Mathf.Max(0.001f, WiggleRadiusY + VentRailClampExtraY);

            float clampedX = Mathf.Clamp(x, _trueCenterX - safeHalfX, _trueCenterX + safeHalfX);

            float minY;
            float maxY;
            if (_dipLatched)
            {
                minY = Mathf.Min(DipY, _standardY) - safeHalfY;
                maxY = Mathf.Max(DipY, _standardY) + safeHalfY;
            }
            else
            {
                minY = _standardY - safeHalfY;
                maxY = _standardY + safeHalfY;
            }

            float clampedY = Mathf.Clamp(y, minY, maxY);
            float clampedZ = Mathf.Clamp(z, VentStartPos.z, EndZ);

            return new Vector3(clampedX, clampedY, clampedZ);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;
            if (other.name != "VentCollider") return;

            _insideVentVolume = true;
            if (_exitLockout)
                return;
            TryEnterRailMode("TriggerEnter");
        }

        private void OnTriggerStay(Collider other)
        {
            if (other == null) return;
            if (other.name != "VentCollider") return;

            _insideVentVolume = true;

            if (_exitLockout)
            {
                float stickY = GetStickY();
                if (stickY > EnterForwardThreshold)
                {
                    _exitLockout = false;
                    TryEnterRailMode("ForwardReenter");
                }
                return;
            }

            if (!InVentRail)
            {
                float stickY = GetStickY();
                if (stickY > EnterForwardThreshold)
                    TryEnterRailMode("StayForward");
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null) return;
            if (other.name != "VentCollider") return;

            _insideVentVolume = false;

            if (InVentRail)
                ExitRailMode(false);
        }

        private TelekinesisHandState GetActiveTkState()
        {
            return _useRight ? _rightTkState : _leftTkState;
        }

        private LineRenderer GetActiveTkBeam()
        {
            return _useRight ? _rightTkBeam : _leftTkBeam;
        }

        private GameObject GetActiveTkReticle()
        {
            return _useRight ? _rightTkReticle : _leftTkReticle;
        }

        private VRHandInput GetActiveVhi()
        {
            return _useRight ? _rightVhi : _leftVhi;
        }

        private VRHandInput GetRailOwnerVhi()
        {
            if (_railOwnerVhi != null) return _railOwnerVhi;
            return GetActiveVhi();
        }

        private TelekinesisHandState GetRailOwnerTkState()
        {
            return UseRightForRailOwner() ? _rightTkState : _leftTkState;
        }

        private LineRenderer GetRailOwnerTkBeam()
        {
            return UseRightForRailOwner() ? _rightTkBeam : _leftTkBeam;
        }

        private GameObject GetRailOwnerTkReticle()
        {
            return UseRightForRailOwner() ? _rightTkReticle : _leftTkReticle;
        }

        private Vector3 GetCanonicalVentMouthWorldPos()
        {
            return new Vector3(_trueCenterX, _standardY, VentStartPos.z);
        }

        private Quaternion GetCanonicalVentMouthWorldRot()
        {
            if (gameObject != null)
                return gameObject.transform.rotation;
            return Quaternion.identity;
        }

        private void UpdateShutterBarrierState()
        {
            if (_prevShuttersOpen == ShuttersOpen)
                return;

            _prevShuttersOpen = ShuttersOpen;
            _wasPressingIntoShutter = false;

            if (ShuttersOpen)
            {
                _closedShutterSide = 0;
                return;
            }

            float z = transform.position.z;
            float nearStop = ShutterZ - ShutterBuffer;
            float farStop = ShutterZ + ShutterBuffer;

            if (z <= nearStop)
                _closedShutterSide = -1;
            else if (z >= farStop)
                _closedShutterSide = 1;
            else
                _closedShutterSide = (z >= ShutterZ) ? 1 : -1;
        }

        private float ApplyClosedShutterBarrier(float currentZ, float attemptedZ, float moveDeltaZ, out bool pressingIntoClosedShutter)
        {
            pressingIntoClosedShutter = false;

            if (ShuttersOpen)
                return attemptedZ;

            if (_closedShutterSide == 0)
                _closedShutterSide = (currentZ >= ShutterZ) ? 1 : -1;

            float nearStop = ShutterZ - ShutterBuffer;
            float farStop = ShutterZ + ShutterBuffer;
            float resolvedZ = attemptedZ;

            if (_closedShutterSide < 0)
            {
                pressingIntoClosedShutter = (attemptedZ > nearStop && moveDeltaZ > 0f);
                if (resolvedZ > nearStop)
                    resolvedZ = nearStop;
            }
            else
            {
                pressingIntoClosedShutter = (attemptedZ < farStop && moveDeltaZ < 0f);
                if (resolvedZ < farStop)
                    resolvedZ = farStop;
            }

            if (pressingIntoClosedShutter && !_wasPressingIntoShutter)
            {
                if (Time.time - _lastShutterHitTime >= ShutterHitSoundCooldown)
                {
                    _lastShutterHitTime = Time.time;
                    AudioUtil.PlayAt("ShutterHit.wav", transform.position);
                }
            }

            _wasPressingIntoShutter = pressingIntoClosedShutter;

            _wasPressingIntoShutter = pressingIntoClosedShutter;
            return resolvedZ;
        }

        private void RestoreVanillaTkAimAfterVentDrop()
        {
            try
            {
                StopOverridingBeam();

                if (UseRightForRailOwner())
                    RestoreTkAimForHand(_rightVhi, _rightTkState, _rightTkReticle, "Right");
                else
                    RestoreTkAimForHand(_leftVhi, _leftTkState, _leftTkReticle, "Left");
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[VentRail] RestoreVanillaTkAimAfterVentDrop failed err=" + e.Message);
            }
        }

        private void RestoreTkAimForHand(VRHandInput vhi, TelekinesisHandState tk, GameObject cachedReticle, string label)
        {
            if (vhi == null && tk == null && cachedReticle == null)
                return;

            if (VhiIsHoldingNonDrone(vhi))
            {
                MaybeDebugLog("[VentRail] RestoreVanillaTkAimAfterVentDrop skipped hand=" + label + " because that hand is holding a non-drone object.");
                return;
            }

            try
            {
                if (cachedReticle != null)
                    cachedReticle.SetActive(true);
            }
            catch { }

            if (tk != null)
            {
                try
                {
                    if (tk._SmoothedReticle != null)
                        tk._SmoothedReticle.SetActive(true);
                }
                catch { }

                try
                {
                    if (tk._SmoothedReticleRenderer != null)
                        tk._SmoothedReticleRenderer.enabled = true;
                }
                catch { }
            }

            if (vhi != null)
            {
                try { vhi.FocusedInteractable = null; } catch { }
                try { vhi.ResetReticleTarget(); } catch { }
                try { vhi.RequestTelekinesisHand(); } catch { }
            }

            MaybeDebugLog("[VentRail] RestoreVanillaTkAimAfterVentDrop hand=" + label);
        }

        private bool VhiIsHoldingNonDrone(VRHandInput vhi)
        {
            if (vhi == null) return false;

            try
            {
                Type t = vhi.GetType();
                BindingFlags f = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                object held = null;

                PropertyInfo pi = t.GetProperty("HeldInteractable", f);
                if (pi != null)
                {
                    try { held = pi.GetValue(vhi, null); } catch { held = null; }
                }

                if (held == null)
                {
                    FieldInfo fi = t.GetField("heldInteractable", f)
                                   ?? t.GetField("_heldInteractable", f)
                                   ?? t.GetField("m_heldInteractable", f)
                                   ?? t.GetField("heldObject", f)
                                   ?? t.GetField("_heldObject", f)
                                   ?? t.GetField("m_heldObject", f);
                    if (fi != null)
                    {
                        try { held = fi.GetValue(vhi); } catch { held = null; }
                    }
                }

                GameObject go = null;
                Component comp = held as Component;
                if (comp != null) go = comp.gameObject;
                else go = held as GameObject;

                if (go == null) return false;

                PickUp pu = go.GetComponent<PickUp>() ?? go.GetComponentInChildren<PickUp>(true) ?? go.transform.parent.GetComponent<PickUp>();
                if (pu == null) return false;
                if (_pickup != null && pu == _pickup) return false;

                string n = pu.gameObject != null ? pu.gameObject.name : go.name;
                return string.IsNullOrEmpty(n) || !n.ToLower().Contains("drone");
            }
            catch
            {
                return false;
            }
        }

        private void SyncVanillaTkSnapshotToVentMouth(string phase)
        {
            try
            {
                if (_pickup == null)
                    return;

                RefreshHeldHandSide();

                VRHandInput vhi = GetRailOwnerVhi();
                TelekinesisHandState tk = GetActiveTkState();
                if (vhi == null || tk == null)
                    return;

                Vector3 mouthPos = GetCanonicalVentMouthWorldPos();
                Quaternion mouthRot = GetCanonicalVentMouthWorldRot();

                Transform tkOrigin = null;
                Transform rayOrigin = null;
                Transform reticleTarget = null;
                Transform smoothedJoint = null;

                try { tkOrigin = vhi._TelekinesisOrigin; } catch { }
                try { rayOrigin = vhi._ReticleRaycastOrigin; } catch { }
                try { reticleTarget = vhi._ReticleTarget; } catch { }
                try { smoothedJoint = vhi._SmoothedPickUpJoint; } catch { }

                Vector3 originPos = mouthPos;
                if (rayOrigin != null)
                    originPos = rayOrigin.position;
                else if (tkOrigin != null)
                    originPos = tkOrigin.position;

                Vector3 toMouth = mouthPos - originPos;
                float dist = toMouth.magnitude;
                Vector3 dir = dist > 0.0001f ? (toMouth / dist) : Vector3.forward;

                try { vhi.RequestTelekinesisHand(); } catch { }

                try { vhi.FocusedInteractable = _pickup; } catch { }
                try { if (reticleTarget != null) reticleTarget.position = mouthPos; } catch { }
                try { if (smoothedJoint != null) smoothedJoint.position = mouthPos; } catch { }

                try { tk.reticleDistance = dist; } catch { }
                try { tk.reticlePositionRay = new Ray(originPos, dir); } catch { }
                try { tk._smoothReticleVelocity = Vector3.zero; } catch { }

                try { _pickup._targetPickUpPosition = mouthPos; } catch { }
                try { _pickup._priorPosition = mouthPos; } catch { }
                try { _pickup._targetPickUpRotation = mouthRot; } catch { }
                try { _pickup._priorRotation = mouthRot; } catch { }
                try { _pickup._onFreezePosition = mouthPos; } catch { }
                try { _pickup._reelingEnabled = false; } catch { }

                try
                {
                    if (tk._SmoothedReticle != null)
                        tk._SmoothedReticle.SetActive(false);
                }
                catch { }

                MaybeDebugLog("[VentRail] SyncVanillaTkSnapshot phase=" + phase +
                              " mouthPos=" + FormatVec(mouthPos) +
                              " originPos=" + FormatVec(originPos) +
                              " reticleDist=" + dist.ToString("F3") +
                              " focused=" + (_pickup != null ? _pickup.name : "null"));
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[VentRail] SyncVanillaTkSnapshot failed phase=" + phase + " err=" + e.Message);
            }
        }

        private GameObject FindTkReticle(TelekinesisHandState tkState, GameObject handRoot)
        {
            if (handRoot == null)
                return null;

            try
            {
                Transform[] trs = handRoot.GetComponentsInChildren<Transform>(true);
                if (trs != null)
                {
                    for (int i = 0; i < trs.Length; i++)
                    {
                        Transform t = trs[i];
                        if (t == null) continue;
                        string n = t.name != null ? t.name.ToLower() : "";
                        if (n.Contains("reticle") && n.Contains("tele"))
                            return t.gameObject;
                    }

                    for (int i = 0; i < trs.Length; i++)
                    {
                        Transform t = trs[i];
                        if (t == null) continue;
                        string n = t.name != null ? t.name.ToLower() : "";
                        if (n.Contains("reticle"))
                            return t.gameObject;
                    }
                }
            }
            catch { }

            return null;
        }

        private void StartRegrabProbe(string reason)
        {
            _regrabProbeActive = true;
            _regrabProbeEndTime = Time.time + Mathf.Max(0.05f, RegrabProbeDuration);
            _regrabProbeNextLogTime = Time.time;
            _regrabProbeTick = 0;

            LogRegrabProbe(reason + " start");
        }

        private void TickRegrabProbe()
        {
            if (!_regrabProbeActive)
                return;

            if (Time.time >= _regrabProbeNextLogTime)
            {
                _regrabProbeTick++;
                _regrabProbeNextLogTime = Time.time + Mathf.Max(0.01f, RegrabProbeInterval);
                LogRegrabProbe("tick " + _regrabProbeTick);
            }

            if (Time.time >= _regrabProbeEndTime)
            {
                LogRegrabProbe("end");
                _regrabProbeActive = false;
            }
        }

        private void LogRegrabProbe(string phase)
        {
            if (!EnableRegrabDiagnostics)
                return;

            RefreshHeldHandSide();

            TelekinesisHandState tk = GetActiveTkState();
            GameObject hand = GetActiveHandObject();
            GameObject reticle = GetActiveTkReticle();
            LineRenderer beam = GetActiveTkBeam();
            VRHandInput vhi = GetActiveVhi();
            Behaviour normal = null;
            string focused = "null";

            try
            {
                if (hand != null)
                    normal = hand.GetComponent("NormalHandState") as Behaviour;
            }
            catch { }

            try
            {
                if (vhi != null && vhi.FocusedInteractable != null)
                    focused = vhi.FocusedInteractable.name;
            }
            catch { }

            string heldHandName = "null";
            try
            {
                if (_pickup != null && _pickup.heldHand != null && _pickup.heldHand.gameObject != null)
                    heldHandName = _pickup.heldHand.gameObject.name;
            }
            catch { }

            MelonLogger.Msg(
                "[VentRail][RegrabProbe] " + phase +
                " | inVentRail=" + InVentRail +
                " | suppressing=" + SuppressingVanillaTk +
                " | pickupHeld=" + (_pickup != null && _pickup.isHeld) +
                " | heldHand=" + heldHandName +
                " | useRight=" + _useRight +
                " | tkEnabled=" + (tk != null && tk.enabled) +
                " | normalEnabled=" + (normal != null && normal.enabled) +
                " | beamObj=" + (beam != null ? beam.gameObject.name : "null") +
                " | beamEnabled=" + (beam != null && beam.enabled) +
                " | reticleObj=" + (reticle != null ? reticle.name : "null") +
                " | reticleActive=" + (reticle != null && reticle.activeInHierarchy) +
                " | focused=" + focused +
                " | pos=" + FormatVec(transform.position)
            );
        }

        private LineRenderer FindTkBeam(TelekinesisHandState tkState, GameObject handRoot)
        {
            try
            {
                if (tkState != null)
                {
                    var prop = typeof(TelekinesisHandState).GetProperty("TelekinesisBeam", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null)
                    {
                        object v = prop.GetValue(tkState, null);
                        LineRenderer lr = v as LineRenderer;
                        if (lr != null) return lr;
                    }

                    var field = typeof(TelekinesisHandState).GetField("_TelekinesisBeam", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                               ?? typeof(TelekinesisHandState).GetField("TelekinesisBeam", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (field != null)
                    {
                        object v = field.GetValue(tkState);
                        LineRenderer lr = v as LineRenderer;
                        if (lr != null) return lr;
                    }
                }
            }
            catch { }

            if (handRoot != null)
            {
                try
                {
                    Transform beamTf = handRoot.transform.Find("TelekinesisBeam");
                    if (beamTf != null)
                    {
                        LineRenderer lr = beamTf.GetComponent<LineRenderer>();
                        if (lr != null) return lr;
                    }
                }
                catch { }

                try
                {
                    LineRenderer[] lrs = handRoot.GetComponentsInChildren<LineRenderer>(true);
                    if (lrs != null)
                    {
                        for (int i = 0; i < lrs.Length; i++)
                        {
                            LineRenderer lr = lrs[i];
                            if (lr != null && lr.gameObject.name == "TelekinesisBeam")
                                return lr;
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        private bool IsHeadsetWearing()
        {
            try
            {
                return HeadsetScript.Instance != null && HeadsetScript.Instance.IsWearing;
            }
            catch
            {
                return false;
            }
        }

        public bool IsHeldByHand(VRHandInput vhi)
        {
            if (vhi == null || _pickup == null || !_pickup.isHeld || _pickup.heldHand == null)
                return false;

            try { return _pickup.heldHand == vhi; } catch { }
            try { return _pickup.heldHand.gameObject == vhi.gameObject; } catch { }
            return false;
        }

        public bool IsRailOwnerHand(VRHandInput vhi)
        {
            if (vhi == null) return false;

            try
            {
                if (_railOwnerVhi != null)
                    return _railOwnerVhi == vhi || _railOwnerVhi.gameObject == vhi.gameObject;
            }
            catch { }

            return IsHeldByHand(vhi);
        }

        public bool ShouldSuppressVanillaTkForRailOwner(VRHandInput vhi, PickUp handHeldPickup)
        {
            if (!InVentRail || !SuppressingVanillaTk) return false;
            if (!IsRailOwnerHand(vhi)) return false;

            if (_pickup != null && _pickup.isHeld) return true;

            if (Time.time < _suppressOwnerTkUntilTime) return true;

            if (LooksLikeHeadsetPickUp(handHeldPickup)) return true;
            if (VhiFocusedLooksLikeHeadset(vhi)) return true;

            return false;
        }

        private bool VhiFocusedLooksLikeHeadset(VRHandInput vhi)
        {
            if (vhi == null) return false;
            try
            {
                Component c = vhi.FocusedInteractable;
                if (c == null) return false;
                if (NameOrParentsContain(c.gameObject, "headset")) return true;

                PickUp pu = ResolvePickUpFromLocalGameObject(c.gameObject);
                return LooksLikeHeadsetPickUp(pu);
            }
            catch { }
            return false;
        }

        private bool LooksLikeHeadsetPickUp(PickUp pu)
        {
            if (pu == null || pu.gameObject == null) return false;
            return NameOrParentsContain(pu.gameObject, "headset");
        }

        private bool NameOrParentsContain(GameObject go, string needle)
        {
            if (go == null || string.IsNullOrEmpty(needle)) return false;
            string lowNeedle = needle.ToLower();

            Transform walk = go.transform;
            while (walk != null)
            {
                try
                {
                    string n = walk.name != null ? walk.name.ToLower() : "";
                    if (n.Contains(lowNeedle)) return true;
                }
                catch { }
                walk = walk.parent;
            }

            return false;
        }

        private PickUp ResolvePickUpFromLocalGameObject(GameObject go)
        {
            if (go == null) return null;

            PickUp pu = go.GetComponent<PickUp>() ?? go.GetComponentInChildren<PickUp>(true);
            Transform walk = go.transform.parent;
            while (pu == null && walk != null)
            {
                pu = walk.GetComponent<PickUp>();
                walk = walk.parent;
            }

            return pu;
        }

        public void ForceClearTkBeamOverrideForHeadsetMode()
        {
            _beamOverrideActive = false;
            ForceHideTkBeamOnly(_rightTkBeam);
            ForceHideTkBeamOnly(_leftTkBeam);
        }

        private void ForceHideTkBeamOnly(LineRenderer beam)
        {
            try
            {
                if (beam != null)
                    beam.enabled = false;
            }
            catch { }
        }

        private void UpdateTkBeamOverride()
        {
            if (!EnableTkBeamOverride)
                return;

            if (IsHeadsetWearing() || _pickup == null || !_pickup.isHeld)
            {
                ForceClearTkBeamOverrideForHeadsetMode();
                return;
            }

            LineRenderer beam = GetRailOwnerTkBeam();
            GameObject hand = GetRailOwnerHandObject();
            if (beam == null || hand == null)
            {
                if (!_loggedBeamMissing)
                {
                    _loggedBeamMissing = true;
                    MaybeDebugLog("[VentRail] TK beam override skipped: beam or hand missing.");
                }
                return;
            }

            int count = beam.positionCount;
            if (count < 2) count = 2;

            if (_beamPositions == null || _beamPositions.Length != count)
                _beamPositions = new Vector3[count];

            Vector3 start = hand.transform.position + (hand.transform.forward * BeamStartForwardOffset);
            Vector3 end = gameObject.transform.position - (hand.transform.forward * BeamEndBackOffset);

            for (int i = 0; i < count; i++)
            {
                float t = (count <= 1) ? 0f : ((float)i / (count - 1));
                _beamPositions[i] = Vector3.Lerp(start, end, t);
            }

            try
            {
                if (!beam.enabled)
                    beam.enabled = true;

                beam.useWorldSpace = true;
                beam.positionCount = count;
                beam.SetPositions(_beamPositions);
                _beamOverrideActive = true;
            }
            catch { }

        }

        private void StopOverridingBeam()
        {
            _beamOverrideActive = false;
        }

        private void MaybeDebugLog(string msg)
        {
            if (!EnableDebugLogs) return;
            if (Time.time - _lastDebugLogTime < DebugLogInterval) return;
            _lastDebugLogTime = Time.time;
            MelonLogger.Msg(msg);
        }

        private string FormatVec(Vector3 v)
        {
            return "(" + v.x.ToString("F3") + ", " + v.y.ToString("F3") + ", " + v.z.ToString("F3") + ")";
        }
    }

    [HarmonyPatch(typeof(TelekinesisHandState), "HandUpdate")]
    internal static class Patch_TK_HandUpdate_VentRail
    {
        private static FieldInfo _fiHeldInteractable;
        private static FieldInfo _fiHeldObject;
        private static PropertyInfo _piHeldInteractable;
        private static bool _reflected;

        private static bool Prefix(TelekinesisHandState __instance)
        {
            if (__instance == null || __instance.gameObject == null)
                return true;

            VRHandInput vhi = __instance.gameObject.GetComponent<VRHandInput>();
            if (vhi == null)
                return true;

            PickUp heldPickup = TryGetHeldPickup(vhi);
            DroneVentRailTK rail = null;

            if (heldPickup != null)
            {
                rail = heldPickup.gameObject.GetComponent<DroneVentRailTK>()
                       ?? heldPickup.gameObject.GetComponentInChildren<DroneVentRailTK>(true);
            }

            if (rail == null)
                rail = TryGetDroneRailForHand(vhi);

            if (rail == null)
                return true;

            if (!rail.ShouldSuppressVanillaTkForRailOwner(vhi, heldPickup))
                return true;

            float dt = Time.deltaTime;
            if (dt <= 0f) dt = 0.016f;
            rail.RailTick(vhi, dt);

            return false;
        }

        private static DroneVentRailTK TryGetDroneRailForHand(VRHandInput vhi)
        {
            if (vhi == null) return null;

            try
            {
                GameObject host = GameObject.Find("PickUp_HOST_Drone");
                DroneVentRailTK rail = null;

                if (host != null)
                    rail = host.GetComponent<DroneVentRailTK>() ?? host.GetComponentInChildren<DroneVentRailTK>(true);

                if (rail == null)
                {
                    GameObject drone = GameObject.Find("Drone");
                    if (drone != null)
                    {
                        rail = drone.GetComponent<DroneVentRailTK>() ?? drone.GetComponentInChildren<DroneVentRailTK>(true);
                        if (rail == null && drone.transform.parent != null)
                            rail = drone.transform.parent.GetComponent<DroneVentRailTK>();
                    }
                }

                if (rail != null && rail.IsRailOwnerHand(vhi))
                    return rail;
            }
            catch { }

            return null;
        }

        private static PickUp TryGetHeldPickup(VRHandInput vhi)
        {
            if (vhi == null) return null;
            EnsureReflection(vhi);

            if (_piHeldInteractable != null)
            {
                try
                {
                    object o = _piHeldInteractable.GetValue(vhi, null);
                    PickUp pu = CoerceToPickUp(o);
                    if (pu != null) return pu;
                }
                catch { }
            }

            if (_fiHeldInteractable != null)
            {
                try
                {
                    object o = _fiHeldInteractable.GetValue(vhi);
                    PickUp pu = CoerceToPickUp(o);
                    if (pu != null) return pu;
                }
                catch { }
            }

            if (_fiHeldObject != null)
            {
                try
                {
                    object o = _fiHeldObject.GetValue(vhi);
                    PickUp pu = CoerceToPickUp(o);
                    if (pu != null) return pu;
                }
                catch { }
            }

            return null;
        }

        private static PickUp CoerceToPickUp(object o)
        {
            if (o == null) return null;

            PickUp pu = o as PickUp;
            if (pu != null) return pu;

            Component comp = o as Component;
            if (comp != null)
                return ResolvePickUpFromGameObject(comp.gameObject);

            GameObject go = o as GameObject;
            if (go != null)
                return ResolvePickUpFromGameObject(go);

            return null;
        }

        private static PickUp ResolvePickUpFromGameObject(GameObject go)
        {
            if (go == null) return null;

            PickUp pu = go.GetComponent<PickUp>() ?? go.GetComponentInChildren<PickUp>(true);
            Transform walk = go.transform.parent;
            while (pu == null && walk != null)
            {
                pu = walk.GetComponent<PickUp>();
                walk = walk.parent;
            }

            return pu;
        }

        private static void EnsureReflection(VRHandInput vhi)
        {
            if (_reflected) return;
            _reflected = true;

            try
            {
                Type t = vhi.GetType();

                _piHeldInteractable = t.GetProperty("HeldInteractable",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                _fiHeldInteractable = t.GetField("heldInteractable",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? t.GetField("_heldInteractable", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? t.GetField("m_heldInteractable", BindingFlags.Instance | BindingFlags.NonPublic);

                _fiHeldObject = t.GetField("heldObject",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? t.GetField("_heldObject", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? t.GetField("m_heldObject", BindingFlags.Instance | BindingFlags.NonPublic);

                MelonLogger.Msg("[VentRail] Reflection ready: " +
                                "piHeld=" + (_piHeldInteractable != null) + " " +
                                "fiHeldI=" + (_fiHeldInteractable != null) + " " +
                                "fiHeldO=" + (_fiHeldObject != null));
            }
            catch (Exception e)
            {
                MelonLogger.Msg("[VentRail] Reflection init failed: " + e.Message);
            }
        }
    }
}
