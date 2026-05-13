using SG.Phoenix.Assets.Code.InputManagement;
using SG.Phoenix.Assets.Code.Interactables;
using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnhollowerBaseLib.Attributes;
using UnhollowerRuntimeLib;
using UnityEngine;
using HarmonyLib;

namespace IEYTD_Mod2Code
{

    public class DroneHandVan : MonoBehaviour
    {
        public DroneHandVan(IntPtr ptr) : base(ptr) { }
        public DroneHandVan() : base(ClassInjector.DerivedConstructorPointer<DroneHandVan>())
            => ClassInjector.DerivedConstructorBody(this);

        public GameObject[] fingers = new GameObject[3];
        public Vector3[] fingerRot = new Vector3[3];

        public float FingerAnimDuration = 0.25f;
        public float GripThreshold = 0.75f;
        public float TriggerThreshold = 0.75f;

        public float GrabZonePadding = 0.015f;

        public bool AutoCreateClawTriggerIfMissing = true;
        public string AutoTriggerName = "__DroneHandVan_ClawTrigger";
        public float AutoTriggerRadius = 0.13f;
        public Vector3 AutoTriggerLocalOffset = new Vector3(0f, 0f, 0.05f);

        public Vector3 HeldLocalPosition = new Vector3(0f, 0f, 0.12f);
        public Vector3 HeldLocalEuler = Vector3.zero;

        public bool DisableGrabbedCollidersWhileHeld = true;

        public bool AddKinematicRigidbodyIfMissing = false;

        public bool DebugLogs = false;

        public string GripSoundName = "DroneHandGrip.ogg";
        public float CloseSoundPitch = 1.0f;
        public float OpenSoundPitch = 1.28f;

        enum ControlButton { Grip, Trigger }

        PickUp _selfPickUp;
        Rigidbody _selfRb;

        readonly List<Collider> _detectors = new List<Collider>();
        readonly List<Collider> _overlapColliders = new List<Collider>();
        readonly List<PickUp> _manualCandidates = new List<PickUp>();

        bool _initialized;
        bool _wasArmHeld;
        bool _clawClosed;
        bool _controlWasPressed;
        char _handSide = 'R';
        ControlButton _controlButton = ControlButton.Trigger;

        PickUp _heldPickUp;
        Rigidbody _heldRb;
        Transform _heldOriginalParent;
        bool _heldOriginalPickUpEnabled;
        bool _heldOriginalRbKinematic;
        bool _heldOriginalUseGravity;
        RigidbodyConstraints _heldOriginalConstraints;
        Vector3 _heldOriginalWorldScale;
        Quaternion _heldRotationOffsetFromDetector;
        Collider[] _heldColliders;
        bool[] _heldColliderWasEnabled;

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

            ResolveSelfPickUp();
            ResolveHandSide();
            RegisterFingers();
            SetupClawDetectors();
        }

        void Update()
        {
            ResolveSelfPickUp();

            bool armHeld = IsArmHeld();

            if (armHeld && !_wasArmHeld)
            {
                OnArmGrabbed();
                _wasArmHeld = armHeld;
                return;
            }
            else if (!armHeld && _wasArmHeld)
            {
                OnArmReleased();
            }

            _wasArmHeld = armHeld;

            if (!armHeld) return;

            bool controlPressed = IsControlPressed();

            if (controlPressed && !_controlWasPressed) CloseClawAndTryGrab();
            else if (!controlPressed && _controlWasPressed) OpenClawAndRelease();

            _controlWasPressed = controlPressed;
        }

        void LateUpdate()
        {
            if (_heldPickUp == null) return;

            ApplyHeldPose(_heldPickUp.transform, _heldOriginalWorldScale, _heldRotationOffsetFromDetector);
        }

        void OnDisable()
        {
            OpenClawAndRelease(false);
            _wasArmHeld = false;
            _controlWasPressed = false;
        }

        void OnDestroy()
        {
            OpenClawAndRelease(false);
        }

        void OnArmGrabbed()
        {
            ResolveHandSide();

            bool gripDown = IsGripPressed();
            bool triggerDown = IsTriggerPressed();

            if (gripDown && !triggerDown) _controlButton = ControlButton.Trigger;
            else if (triggerDown && !gripDown) _controlButton = ControlButton.Grip;
            else _controlButton = ControlButton.Trigger;

            _controlWasPressed = false;

        }

        void OnArmReleased()
        {
            OpenClawAndRelease();
            _controlWasPressed = false;

        }

        void CloseClawAndTryGrab()
        {
            if (_clawClosed) return;

            _clawClosed = true;
            PlayGripSound(false);
            HandAnimation(true);

            _manualCandidates.Clear();

            PickUp target = FindBestPickUp();
            if (target == null)
            {
                if (DebugLogs) MelonLogger.Msg("[DroneHandVan] Claw closed with no PickUp target");
                return;
            }

            GrabPickUp(target);
        }

        void OpenClawAndRelease(bool playSound = true)
        {
            if (!_clawClosed && _heldPickUp == null) return;

            bool wasClosed = _clawClosed;

            ReleaseHeldPickUp();
            _clawClosed = false;

            if (playSound && wasClosed)
                PlayGripSound(true);

            HandAnimation(false);
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
            catch (Exception e)
            {
                if (DebugLogs)
                    MelonLogger.Warning("[DroneHandVan] PlayGripSound failed: " + e);
            }
        }

        void GrabPickUp(PickUp target)
        {
            if (!IsValidGrabTarget(target)) return;

            if (_heldPickUp != null) ReleaseHeldPickUp();

            _heldPickUp = target;
            _heldRb = target.GetComponent<Rigidbody>();
            if (_heldRb == null) _heldRb = target.GetComponentInParent<Rigidbody>();

            _heldOriginalParent = target.transform.parent;
            _heldOriginalPickUpEnabled = target.enabled;
            _heldOriginalWorldScale = target.transform.lossyScale;
            _heldRotationOffsetFromDetector = Quaternion.Inverse(GetCurrentAnchorRotation()) * target.transform.rotation;

            if (_heldRb != null)
            {
                _heldOriginalRbKinematic = _heldRb.isKinematic;
                _heldOriginalUseGravity = _heldRb.useGravity;
                _heldOriginalConstraints = _heldRb.constraints;

                _heldRb.velocity = Vector3.zero;
                _heldRb.angularVelocity = Vector3.zero;
                _heldRb.useGravity = false;
                _heldRb.isKinematic = true;
                _heldRb.constraints = RigidbodyConstraints.FreezeAll;
            }

            target.enabled = false;

            CacheAndDisableHeldColliders(target);

            target.transform.SetParent(transform, true);
            ApplyHeldPose(target.transform, _heldOriginalWorldScale, _heldRotationOffsetFromDetector);

            ClearCandidateCaches();

        }

        void ApplyHeldPose(Transform held, Vector3 desiredWorldScale, Quaternion detectorRelativeRotationOffset)
        {
            if (held == null) return;

            Collider anchor = GetPrimaryDetector();
            Vector3 anchorPos = (anchor != null) ? GetColliderWorldCenter(anchor) : transform.position;
            Quaternion anchorRot = (anchor != null) ? anchor.transform.rotation : transform.rotation;

            held.position = anchorPos + (anchorRot * HeldLocalPosition);

            held.rotation = anchorRot * detectorRelativeRotationOffset;

            SetWorldScale(held, desiredWorldScale);
        }

        Quaternion GetCurrentAnchorRotation()
        {
            Collider anchor = GetPrimaryDetector();
            return (anchor != null) ? anchor.transform.rotation : transform.rotation;
        }

        Collider GetPrimaryDetector()
        {
            if (_detectors != null)
            {
                for (int i = 0; i < _detectors.Count; i++)
                {
                    Collider d = _detectors[i];
                    if (d != null && d.enabled)
                        return d;
                }
            }

            return null;
        }

        Vector3 GetColliderWorldCenter(Collider c)
        {
            if (c == null) return transform.position;

            try
            {
                if (c is SphereCollider)
                {
                    SphereCollider s = (SphereCollider)c;
                    return s.transform.TransformPoint(s.center);
                }

                if (c is BoxCollider)
                {
                    BoxCollider b = (BoxCollider)c;
                    return b.transform.TransformPoint(b.center);
                }

                if (c is CapsuleCollider)
                {
                    CapsuleCollider cap = (CapsuleCollider)c;
                    return cap.transform.TransformPoint(cap.center);
                }
            }
            catch { }

            try { return c.bounds.center; } catch { }
            return c.transform.position;
        }

        void SetWorldScale(Transform target, Vector3 desiredWorldScale)
        {
            if (target == null) return;

            Vector3 parentScale = Vector3.one;
            if (target.parent != null)
                parentScale = target.parent.lossyScale;

            float x = (Mathf.Abs(parentScale.x) < 0.0001f) ? desiredWorldScale.x : desiredWorldScale.x / parentScale.x;
            float y = (Mathf.Abs(parentScale.y) < 0.0001f) ? desiredWorldScale.y : desiredWorldScale.y / parentScale.y;
            float z = (Mathf.Abs(parentScale.z) < 0.0001f) ? desiredWorldScale.z : desiredWorldScale.z / parentScale.z;

            target.localScale = new Vector3(x, y, z);
        }

        void ReleaseHeldPickUp()
        {
            if (_heldPickUp == null) return;

            PickUp released = _heldPickUp;
            Rigidbody releasedRb = _heldRb;

            try
            {
                released.transform.SetParent(_heldOriginalParent, true);
                released.enabled = _heldOriginalPickUpEnabled;
                RestoreHeldColliders();

                if (releasedRb != null)
                {
                    releasedRb.isKinematic = _heldOriginalRbKinematic;
                    releasedRb.useGravity = _heldOriginalUseGravity;
                    releasedRb.constraints = _heldOriginalConstraints;
                    releasedRb.velocity = Vector3.zero;
                    releasedRb.angularVelocity = Vector3.zero;
                }

                if (DebugLogs)
                    MelonLogger.Msg("[DroneHandVan] Released PickUp: " + released.name);
            }
            catch (Exception e)
            {

            }

            ClearCandidateCaches();

            _heldPickUp = null;
            _heldRb = null;
            _heldOriginalParent = null;
            _heldColliders = null;
            _heldColliderWasEnabled = null;
        }

        void CacheAndDisableHeldColliders(PickUp target)
        {
            _heldColliders = null;
            _heldColliderWasEnabled = null;

            if (!DisableGrabbedCollidersWhileHeld || target == null)
                return;

            Collider[] cols = target.GetComponentsInChildren<Collider>(true);
            if (cols == null || cols.Length == 0)
                cols = target.GetComponentsInParent<Collider>(true);

            if (cols == null || cols.Length == 0)
                return;

            List<Collider> usable = new List<Collider>();
            for (int i = 0; i < cols.Length; i++)
            {
                Collider c = cols[i];
                if (c == null) continue;

                if (c.transform == transform || c.transform.IsChildOf(transform)) continue;
                if (_selfPickUp != null && (c.transform == _selfPickUp.transform || c.transform.IsChildOf(_selfPickUp.transform))) continue;

                usable.Add(c);
            }

            _heldColliders = usable.ToArray();
            _heldColliderWasEnabled = new bool[_heldColliders.Length];

            for (int i = 0; i < _heldColliders.Length; i++)
            {
                Collider c = _heldColliders[i];
                if (c == null) continue;

                _heldColliderWasEnabled[i] = c.enabled;
                c.enabled = false;
            }
        }

        void RestoreHeldColliders()
        {
            if (_heldColliders == null || _heldColliderWasEnabled == null)
                return;

            int count = Mathf.Min(_heldColliders.Length, _heldColliderWasEnabled.Length);
            for (int i = 0; i < count; i++)
            {
                Collider c = _heldColliders[i];
                if (c == null) continue;
                c.enabled = _heldColliderWasEnabled[i];
            }
        }

        PickUp FindBestPickUp()
        {
            CleanOverlapList();
            RefreshManualCandidates();

            float bestDistSqr = float.MaxValue;
            PickUp best = null;
            Vector3 triggerCenter = GetGrabCenter();

            for (int i = 0; i < _manualCandidates.Count; i++)
            {
                PickUp pu = _manualCandidates[i];
                if (!IsValidGrabTarget(pu)) continue;

                ConsiderPickUpByTriggerDistance(pu, triggerCenter, ref best, ref bestDistSqr);
            }

            if (DebugLogs)
            {
                MelonLogger.Msg("[DroneHandVan] FindBestPickUp triggerCenter=" + triggerCenter +
                                " staleOverlapCols=" + _overlapColliders.Count +
                                " freshCandidates=" + _manualCandidates.Count +
                                " best=" + (best != null ? best.name : "null") +
                                " bestDist=" + (best != null ? Mathf.Sqrt(bestDistSqr).ToString("F3") : "n/a"));
            }

            return best;
        }

        void RefreshManualCandidates()
        {
            _manualCandidates.Clear();

            for (int i = 0; i < _detectors.Count; i++)
            {
                Collider d = _detectors[i];
                if (d == null || !d.enabled) continue;

                Collider[] hits = GetCollidersInsideDetector(d);
                if (hits == null) continue;

                for (int h = 0; h < hits.Length; h++)
                {
                    Collider c = hits[h];
                    if (c == null || !c.enabled) continue;
                    if (c.transform == transform || c.transform.IsChildOf(transform)) continue;
                    if (!ColliderActuallyTouchesDetector(c, d)) continue;

                    PickUp pu = c.GetComponentInParent<PickUp>();
                    if (!IsValidGrabTarget(pu)) continue;

                    if (!_manualCandidates.Contains(pu))
                        _manualCandidates.Add(pu);
                }
            }
        }

        void ConsiderPickUpByTriggerDistance(PickUp pu, Vector3 triggerCenter, ref PickUp best, ref float bestDistSqr)
        {
            if (!IsValidGrabTarget(pu)) return;

            float dSqr = GetPickUpDistanceSqrToTrigger(pu, triggerCenter);
            if (!PickUpTouchesAnyDetector(pu)) return;

            if (dSqr < bestDistSqr)
            {
                bestDistSqr = dSqr;
                best = pu;
            }
        }

        float GetPickUpDistanceSqrToTrigger(PickUp pu, Vector3 triggerCenter)
        {
            if (pu == null) return float.MaxValue;

            Collider nearest = FindNearestColliderForPickUp(pu, triggerCenter);
            if (nearest != null)
            {
                try
                {
                    Vector3 closest = nearest.ClosestPoint(triggerCenter);
                    return (closest - triggerCenter).sqrMagnitude;
                }
                catch { }
            }

            return (pu.transform.position - triggerCenter).sqrMagnitude;
        }

        bool PickUpTouchesAnyDetector(PickUp pu)
        {
            if (pu == null) return false;

            Collider[] cols = pu.GetComponentsInChildren<Collider>(true);
            if (cols == null || cols.Length == 0)
                cols = pu.GetComponentsInParent<Collider>(true);

            if (cols == null || cols.Length == 0) return false;

            for (int i = 0; i < _detectors.Count; i++)
            {
                Collider d = _detectors[i];
                if (d == null || !d.enabled) continue;

                for (int c = 0; c < cols.Length; c++)
                {
                    Collider col = cols[c];
                    if (col == null || !col.enabled) continue;
                    if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

                    if (ColliderActuallyTouchesDetector(col, d))
                        return true;
                }
            }

            return false;
        }

        Collider[] GetCollidersInsideDetector(Collider detector)
        {
            if (detector == null) return null;

            float padding = Mathf.Max(0f, GrabZonePadding);

            try
            {
                SphereCollider s = detector as SphereCollider;
                if (s != null)
                {
                    Vector3 center = s.transform.TransformPoint(s.center);
                    Vector3 scale = s.transform.lossyScale;
                    float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                    float radius = Mathf.Max(0.001f, s.radius * maxScale + padding);
                    return Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);
                }

                BoxCollider b = detector as BoxCollider;
                if (b != null)
                {
                    Vector3 center = b.transform.TransformPoint(b.center);
                    Vector3 scale = b.transform.lossyScale;
                    Vector3 half = new Vector3(
                        Mathf.Abs(b.size.x * scale.x) * 0.5f + padding,
                        Mathf.Abs(b.size.y * scale.y) * 0.5f + padding,
                        Mathf.Abs(b.size.z * scale.z) * 0.5f + padding
                    );
                    return Physics.OverlapBox(center, half, b.transform.rotation, ~0, QueryTriggerInteraction.Collide);
                }

                CapsuleCollider cap = detector as CapsuleCollider;
                if (cap != null)
                {
                    Vector3 p0, p1;
                    float radius;
                    GetCapsuleWorldPoints(cap, padding, out p0, out p1, out radius);
                    return Physics.OverlapCapsule(p0, p1, radius, ~0, QueryTriggerInteraction.Collide);
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[DroneHandVan] Detector overlap failed: " + e);
                return null;
            }

            try
            {
                Bounds b = detector.bounds;
                return Physics.OverlapBox(b.center, b.extents + Vector3.one * padding, Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
            }
            catch { }

            return null;
        }

        bool ColliderActuallyTouchesDetector(Collider candidate, Collider detector)
        {
            if (candidate == null || detector == null) return false;
            if (!candidate.enabled || !detector.enabled) return false;

            try
            {
                Vector3 direction;
                float distance;
                bool touching = Physics.ComputePenetration(
                    detector, detector.transform.position, detector.transform.rotation,
                    candidate, candidate.transform.position, candidate.transform.rotation,
                    out direction, out distance
                );

                if (touching) return true;
            }
            catch { }

            try
            {
                Vector3 detectorCenter = GetColliderWorldCenter(detector);
                Vector3 pOnCandidate = candidate.ClosestPoint(detectorCenter);
                Vector3 pOnDetector = detector.ClosestPoint(pOnCandidate);
                float padding = Mathf.Max(0f, GrabZonePadding);
                return (pOnCandidate - pOnDetector).sqrMagnitude <= padding * padding;
            }
            catch { }

            return false;
        }

        void GetCapsuleWorldPoints(CapsuleCollider cap, float padding, out Vector3 p0, out Vector3 p1, out float radius)
        {
            Vector3 center = cap.transform.TransformPoint(cap.center);
            Vector3 scale = cap.transform.lossyScale;

            Vector3 axisLocal = Vector3.up;
            float axisScale = Mathf.Abs(scale.y);
            float radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));

            if (cap.direction == 0)
            {
                axisLocal = Vector3.right;
                axisScale = Mathf.Abs(scale.x);
                radiusScale = Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            }
            else if (cap.direction == 2)
            {
                axisLocal = Vector3.forward;
                axisScale = Mathf.Abs(scale.z);
                radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            }

            radius = Mathf.Max(0.001f, cap.radius * radiusScale + padding);
            float height = Mathf.Max(radius * 2f, cap.height * axisScale);
            float halfLine = Mathf.Max(0f, (height * 0.5f) - radius);

            Vector3 axisWorld = cap.transform.TransformDirection(axisLocal).normalized;
            p0 = center + axisWorld * halfLine;
            p1 = center - axisWorld * halfLine;
        }

        bool IsValidGrabTarget(PickUp pu)
        {
            if (pu == null || pu == _selfPickUp) return false;
            if (!pu.enabled) return false;

            try { if (pu.isHeld) return false; } catch { }

            if (pu.transform == transform) return false;
            if (pu.transform.IsChildOf(transform)) return false;

            try
            {
                if (_selfPickUp != null)
                {
                    if (pu.transform == _selfPickUp.transform) return false;
                    if (pu.transform.IsChildOf(_selfPickUp.transform)) return false;
                }
            }
            catch { }

            return true;
        }

        Collider FindNearestColliderForPickUp(PickUp pu)
        {
            return FindNearestColliderForPickUp(pu, GetGrabCenter());
        }

        Collider FindNearestColliderForPickUp(PickUp pu, Vector3 triggerCenter)
        {
            if (pu == null) return null;

            Collider[] cols = pu.GetComponentsInChildren<Collider>(true);
            if (cols == null || cols.Length == 0)
                cols = pu.GetComponentsInParent<Collider>(true);

            if (cols == null || cols.Length == 0) return null;

            float best = float.MaxValue;
            Collider bestCol = null;

            for (int i = 0; i < cols.Length; i++)
            {
                Collider c = cols[i];
                if (c == null || !c.enabled) continue;
                if (c.transform == transform || c.transform.IsChildOf(transform)) continue;

                Vector3 p;
                try { p = c.ClosestPoint(triggerCenter); }
                catch { p = c.transform.position; }

                float d = (p - triggerCenter).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    bestCol = c;
                }
            }

            return bestCol;
        }

        Vector3 GetGrabCenter()
        {
            Collider d = GetPrimaryDetector();
            if (d != null)
                return GetColliderWorldCenter(d);

            return transform.position;
        }

        void CleanOverlapList()
        {
            for (int i = _overlapColliders.Count - 1; i >= 0; i--)
            {
                Collider c = _overlapColliders[i];
                if (c == null || !c.enabled)
                {
                    _overlapColliders.RemoveAt(i);
                    continue;
                }

                PickUp pu = c.GetComponentInParent<PickUp>();
                if (!IsValidGrabTarget(pu))
                    _overlapColliders.RemoveAt(i);
            }
        }

        void ClearCandidateCaches()
        {
            _overlapColliders.Clear();
            _manualCandidates.Clear();
        }

        public void DetectorEnter(Collider col)
        {
            if (col == null) return;
            if (col.transform == transform || col.transform.IsChildOf(transform)) return;

            PickUp pu = col.GetComponentInParent<PickUp>();
            if (!IsValidGrabTarget(pu)) return;

            if (!_overlapColliders.Contains(col))
                _overlapColliders.Add(col);

            if (DebugLogs)
                MelonLogger.Msg("[DroneHandVan] DetectorEnter target=" + pu.name + " col=" + col.name);
        }

        public void DetectorExit(Collider col)
        {
            if (col == null) return;
            _overlapColliders.Remove(col);
        }

        void HandAnimation(bool grabbing)
        {
            for (int i = 0; i < 3; i++)
            {
                GameObject finger = fingers[i];
                if (finger == null) continue;

                Transform fingerT = finger.transform;
                Vector3 target;

                if (!grabbing)
                {
                    fingerT.localRotation = Quaternion.Euler(Vector3.zero);
                    target = fingerRot[i];
                }
                else
                {
                    fingerT.localRotation = Quaternion.Euler(fingerRot[i]);
                    target = Vector3.zero;
                }

                MelonCoroutines.Start(RotateRoutine(fingerT, target, FingerAnimDuration));
            }
        }

        [HideFromIl2Cpp]
        System.Collections.IEnumerator RotateRoutine(Transform target, Vector3 targetEuler, float duration)
        {
            if (target == null) yield break;

            Quaternion startRot = target.localRotation;
            Quaternion endRot = Quaternion.Euler(targetEuler);

            if (duration < 0.0001f)
            {
                target.localRotation = endRot;
                yield break;
            }

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                target.localRotation = Quaternion.Lerp(startRot, endRot, Mathf.Clamp01(t));
                yield return null;
            }

            target.localRotation = endRot;
        }

        void ResolveSelfPickUp()
        {
            if (_selfPickUp != null) return;

            _selfPickUp = GetComponent<PickUp>();
            if (_selfPickUp == null) _selfPickUp = GetComponentInParent<PickUp>();
            if (_selfPickUp == null) _selfPickUp = GetComponentInChildren<PickUp>(true);
        }

        bool IsArmHeld()
        {
            try { return _selfPickUp != null && _selfPickUp.isHeld; }
            catch { return false; }
        }

        void ResolveHandSide()
        {
            try
            {
                if (_selfPickUp != null && _selfPickUp.heldHand != null && _selfPickUp.heldHand.gameObject != null)
                {
                    string heldName = _selfPickUp.heldHand.gameObject.name.ToLower();
                    _handSide = heldName.Contains("left") ? 'L' : 'R';
                    return;
                }
            }
            catch { }

            string n = gameObject.name.ToLower();
            _handSide = n.Contains("left") || n.Contains("_l") ? 'L' : 'R';
        }

        void RegisterFingers()
        {
            ResolveHandSide();

            for (int i = 0; i < 3; i++)
            {
                if (fingers[i] != null) continue;

                string exact = "SM_finger_" + (i + 1) + "_" + _handSide + "_low";
                Transform found = FindChildExact(transform, exact);
                if (found == null)
                {
                    GameObject go = GameObject.Find(exact);
                    if (go != null) found = go.transform;
                }

                if (found != null) fingers[i] = found.gameObject;
            }

            int multiplier = (_handSide == 'L') ? -1 : 1;
            fingerRot[0] = new Vector3(0f, 0f, 15f * multiplier);
            fingerRot[1] = new Vector3(0f, 0f, -15f * multiplier);
            fingerRot[2] = new Vector3(-15f, 0f, 0f);
        }

        Transform FindChildExact(Transform root, string exactName)
        {
            if (root == null) return null;

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == exactName)
                    return all[i];
            }

            return null;
        }

        void SetupClawDetectors()
        {
            if (_selfRb == null) _selfRb = GetComponent<Rigidbody>();
            if (_selfRb == null) _selfRb = GetComponentInParent<Rigidbody>();

            if (_selfRb == null && AddKinematicRigidbodyIfMissing)
            {
                _selfRb = gameObject.AddComponent<Rigidbody>();
                _selfRb.isKinematic = true;
                _selfRb.useGravity = false;
            }

            FindExistingDetectorColliders();

            if (_detectors.Count == 0 && AutoCreateClawTriggerIfMissing)
                CreateDedicatedClawTrigger();

            AttachForwardersToDetectors();

            if (DebugLogs)
                MelonLogger.Msg("[DroneHandVan] Detector count=" + _detectors.Count + " rbFound=" + (_selfRb != null));
        }

        void FindExistingDetectorColliders()
        {
            _detectors.Clear();

            Collider[] cols = GetComponentsInChildren<Collider>(true);
            if (cols == null) return;

            for (int i = 0; i < cols.Length; i++)
            {
                Collider c = cols[i];
                if (c == null || !c.isTrigger) continue;

                string n = c.gameObject.name.ToLower();
                bool looksLikeDetector =
                    n.Contains("trigger") ||
                    n.Contains("hitbox") ||
                    n.Contains("claw") ||
                    n.Contains("grab") ||
                    n.Contains("finger");

                if (!looksLikeDetector && cols.Length > 1) continue;

                if (!_detectors.Contains(c)) _detectors.Add(c);
            }
        }

        void CreateDedicatedClawTrigger()
        {
            GameObject go = new GameObject(AutoTriggerName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = AutoTriggerLocalOffset;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            SphereCollider sc = go.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = Mathf.Max(0.005f, AutoTriggerRadius);

            _detectors.Add(sc);

            if (DebugLogs)
                MelonLogger.Msg("[DroneHandVan] Created dedicated claw trigger: " + AutoTriggerName +
                                " localOffset=" + AutoTriggerLocalOffset + " radius=" + sc.radius);
        }

        void AttachForwardersToDetectors()
        {
            for (int i = 0; i < _detectors.Count; i++)
            {
                Collider c = _detectors[i];
                if (c == null) continue;

                DroneHandVanTrigger fwd = c.gameObject.GetComponent<DroneHandVanTrigger>();
                if (fwd == null) fwd = c.gameObject.AddComponent<DroneHandVanTrigger>();
                fwd.owner = this;
            }
        }

        bool IsControlPressed()
        {
            return _controlButton == ControlButton.Grip ? IsGripPressed() : IsTriggerPressed();
        }

        VRHandInput GetHeldHandInput()
        {
            try
            {
                if (_selfPickUp != null && _selfPickUp.heldHand != null)
                    return _selfPickUp.heldHand;
            }
            catch { }

            return null;
        }

        bool IsGripPressed()
        {
            return DroneHandVanSchellInputBridge.IsGripPressed(GetHeldHandInput());
        }

        bool IsTriggerPressed()
        {
            return DroneHandVanSchellInputBridge.IsTriggerPressed(GetHeldHandInput());
        }
    }

    internal static class DroneHandVanSchellInputBridge
    {
        class HandButtonState
        {
            public bool Grip;
            public bool Trigger;
        }

        static readonly Dictionary<int, HandButtonState> _states = new Dictionary<int, HandButtonState>();
        static bool _loggedReflectionFailure = false;

        public static bool IsGripPressed(VRHandInput handInput)
        {
            HandButtonState state = GetState(handInput, false);
            if (state != null && state.Grip) return true;

            bool live;
            if (TryReadAnimatorGrip(handInput, out live) && live) return true;

            return false;
        }

        public static bool IsTriggerPressed(VRHandInput handInput)
        {
            HandButtonState state = GetState(handInput, false);
            if (state != null && state.Trigger) return true;

            bool live;
            if (TryReadAnimatorTrigger(handInput, out live) && live) return true;
            if (TryReadConfigInput(handInput, true, out live) && live) return true;

            return false;
        }

        public static void SetGrip(VRHandInput handInput, bool pressed)
        {
            HandButtonState state = GetState(handInput, true);
            if (state != null) state.Grip = pressed;
        }

        public static void SetTrigger(VRHandInput handInput, bool pressed)
        {
            HandButtonState state = GetState(handInput, true);
            if (state != null) state.Trigger = pressed;
        }

        public static void DebugInput(string eventName, VRHandInput handInput, bool triggerValue)
        {
            try
            {
                string handName = handInput != null && handInput.gameObject != null ? handInput.gameObject.name : "null";

            }
            catch { }
        }

        static HandButtonState GetState(VRHandInput handInput, bool create)
        {
            if (handInput == null) return null;

            int id = GetId(handInput);
            HandButtonState state;
            if (_states.TryGetValue(id, out state))
                return state;

            if (!create) return null;

            state = new HandButtonState();
            _states[id] = state;
            return state;
        }

        static int GetId(VRHandInput handInput)
        {
            try { return handInput.GetInstanceID(); }
            catch { return 0; }
        }

        static bool TryReadAnimatorGrip(VRHandInput handInput, out bool pressed)
        {
            pressed = false;
            if (handInput == null) return false;

            try
            {
                Animator a = handInput.VRHandAnimator;
                if (a == null) return false;

                string interact = VRHandInput._INTERACT_BOOL;
                if (!string.IsNullOrEmpty(interact) && HasAnimatorBool(a, interact))
                {
                    pressed = a.GetBool(interact);
                    return true;
                }
            }
            catch { }

            return false;
        }

        static bool TryReadAnimatorTrigger(VRHandInput handInput, out bool pressed)
        {
            pressed = false;
            if (handInput == null) return false;

            try
            {
                Animator a = handInput.VRHandAnimator;
                if (a == null) return false;

                string indexHover = VRHandInput._INDEX_HOVER_BOOL;
                if (!string.IsNullOrEmpty(indexHover) && HasAnimatorBool(a, indexHover))
                {
                    pressed = a.GetBool(indexHover);
                    return true;
                }

                string[] boolNames = new string[]
                {
                    "IndexTrigger", "IndexTriggerPressed", "IndexTriggerDown",
                    "Trigger", "TriggerPressed", "TriggerDown",
                    "Use", "UsePressed", "UseDown",
                    "Index", "IndexPressed", "IndexDown"
                };

                for (int i = 0; i < boolNames.Length; i++)
                {
                    string n = boolNames[i];
                    if (HasAnimatorBool(a, n))
                    {
                        pressed = a.GetBool(n);
                        return true;
                    }
                }

                string[] floatNames = new string[]
                {
                    "IndexTrigger", "Trigger", "Use", "Index", "IndexCurl", "IndexBlend"
                };

                for (int i = 0; i < floatNames.Length; i++)
                {
                    string n = floatNames[i];
                    if (HasAnimatorFloat(a, n))
                    {
                        pressed = a.GetFloat(n) > 0.35f;
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        static bool HasAnimatorBool(Animator a, string name)
        {
            if (a == null || string.IsNullOrEmpty(name)) return false;
            try
            {
                AnimatorControllerParameter[] ps = a.parameters;
                if (ps == null) return false;
                for (int i = 0; i < ps.Length; i++)
                {
                    AnimatorControllerParameter p = ps[i];
                    if (p != null && p.type == AnimatorControllerParameterType.Bool && p.name == name)
                        return true;
                }
            }
            catch { }
            return false;
        }

        static bool HasAnimatorFloat(Animator a, string name)
        {
            if (a == null || string.IsNullOrEmpty(name)) return false;
            try
            {
                AnimatorControllerParameter[] ps = a.parameters;
                if (ps == null) return false;
                for (int i = 0; i < ps.Length; i++)
                {
                    AnimatorControllerParameter p = ps[i];
                    if (p != null && p.type == AnimatorControllerParameterType.Float && p.name == name)
                        return true;
                }
            }
            catch { }
            return false;
        }

        static bool TryReadConfigInput(VRHandInput handInput, bool trigger, out bool pressed)
        {
            pressed = false;
            if (handInput == null) return false;

            try
            {
                object cfg = handInput.HandControllerConfig;
                if (cfg == null) return false;

                string[] wants = trigger
                    ? new string[] { "use", "trigger", "index", "alternate" }
                    : new string[] { "interact", "grip", "grab" };

                return TryReadObjectGraph(cfg, wants, 0, out pressed);
            }
            catch (Exception e)
            {
                if (!_loggedReflectionFailure)
                {
                    _loggedReflectionFailure = true;
                    MelonLogger.Warning("[DroneHandVan] HandControllerConfig reflection input fallback failed once: " + e.Message);
                }
            }

            return false;
        }

        static bool TryReadObjectGraph(object obj, string[] wantedNameParts, int depth, out bool pressed)
        {
            pressed = false;
            if (obj == null || depth > 2) return false;

            if (TryReadInputLikeObject(obj, out pressed))
                return true;

            Type t = obj.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            try
            {
                PropertyInfo[] props = t.GetProperties(flags);
                for (int i = 0; i < props.Length; i++)
                {
                    PropertyInfo p = props[i];
                    if (p == null || !p.CanRead) continue;
                    if (!NameMatches(p.Name, wantedNameParts) && depth == 0) continue;

                    object v = null;
                    try { v = p.GetValue(obj, null); } catch { continue; }
                    if (TryReadObjectGraph(v, wantedNameParts, depth + 1, out pressed))
                        return true;
                }
            }
            catch { }

            try
            {
                FieldInfo[] fields = t.GetFields(flags);
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo f = fields[i];
                    if (f == null) continue;
                    if (!NameMatches(f.Name, wantedNameParts) && depth == 0) continue;

                    object v = null;
                    try { v = f.GetValue(obj); } catch { continue; }
                    if (TryReadObjectGraph(v, wantedNameParts, depth + 1, out pressed))
                        return true;
                }
            }
            catch { }

            return false;
        }

        static bool NameMatches(string name, string[] parts)
        {
            if (string.IsNullOrEmpty(name) || parts == null) return false;
            string lower = name.ToLowerInvariant();
            for (int i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrEmpty(parts[i]) && lower.Contains(parts[i]))
                    return true;
            }
            return false;
        }

        static bool TryReadInputLikeObject(object obj, out bool pressed)
        {
            pressed = false;
            if (obj == null) return false;

            try
            {
                if (obj is bool)
                {
                    pressed = (bool)obj;
                    return true;
                }

                if (obj is float)
                {
                    pressed = ((float)obj) > 0.35f;
                    return true;
                }
            }
            catch { }

            Type t = obj.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            try
            {
                MethodInfo isPressed = t.GetMethod("IsPressed", flags, null, Type.EmptyTypes, null);
                if (isPressed != null)
                {
                    object r = isPressed.Invoke(obj, null);
                    if (r is bool)
                    {
                        pressed = (bool)r;
                        return true;
                    }
                }
            }
            catch { }

            try
            {
                MethodInfo readValueAsObject = t.GetMethod("ReadValueAsObject", flags, null, Type.EmptyTypes, null);
                if (readValueAsObject != null)
                {
                    object r = readValueAsObject.Invoke(obj, null);
                    if (TryReadInputLikeObject(r, out pressed))
                        return true;
                }
            }
            catch { }

            try
            {
                PropertyInfo triggered = t.GetProperty("triggered", flags) ?? t.GetProperty("Triggered", flags);
                if (triggered != null && triggered.CanRead)
                {
                    object r = triggered.GetValue(obj, null);
                    if (r is bool)
                    {
                        pressed = (bool)r;
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }
    }

    [HarmonyPatch(typeof(VRHandInput), "InteractPerformed")]
    internal static class Patch_DroneHandVanInput_InteractPerformed
    {
        static void Postfix(VRHandInput __instance)
        {
            DroneHandVanSchellInputBridge.SetGrip(__instance, true);
        }
    }

    [HarmonyPatch(typeof(VRHandInput), "InteractCancelled")]
    internal static class Patch_DroneHandVanInput_InteractCancelled
    {
        static void Postfix(VRHandInput __instance)
        {
            DroneHandVanSchellInputBridge.SetGrip(__instance, false);
        }
    }

    [HarmonyPatch(typeof(VRHandInput), "UsePerformed")]
    internal static class Patch_DroneHandVanInput_UsePerformed
    {
        static void Postfix(VRHandInput __instance)
        {
            DroneHandVanSchellInputBridge.SetTrigger(__instance, true);
            DroneHandVanSchellInputBridge.DebugInput("UsePerformed", __instance, true);
        }
    }

    [HarmonyPatch(typeof(VRHandInput), "AlternateUsePerformed")]
    internal static class Patch_DroneHandVanInput_AlternateUsePerformed
    {
        static void Postfix(VRHandInput __instance)
        {
            DroneHandVanSchellInputBridge.SetTrigger(__instance, true);
            DroneHandVanSchellInputBridge.DebugInput("AlternateUsePerformed", __instance, true);
        }
    }

    [HarmonyPatch(typeof(VRHandInput), "AlternateUseCancelled")]
    internal static class Patch_DroneHandVanInput_AlternateUseCancelled
    {
        static void Postfix(VRHandInput __instance)
        {
            DroneHandVanSchellInputBridge.SetTrigger(__instance, false);
            DroneHandVanSchellInputBridge.DebugInput("AlternateUseCancelled", __instance, false);
        }
    }

    [HarmonyPatch(typeof(VRHandInput), "IndexTriggerTouchPerformed")]
    internal static class Patch_DroneHandVanInput_IndexTriggerTouchPerformed
    {
        static void Postfix(VRHandInput __instance)
        {
            DroneHandVanSchellInputBridge.SetTrigger(__instance, true);
            DroneHandVanSchellInputBridge.DebugInput("IndexTriggerTouchPerformed", __instance, true);
        }
    }

    [HarmonyPatch(typeof(VRHandInput), "IndexTriggerTouchCancelled")]
    internal static class Patch_DroneHandVanInput_IndexTriggerTouchCancelled
    {
        static void Postfix(VRHandInput __instance)
        {
            DroneHandVanSchellInputBridge.SetTrigger(__instance, false);
            DroneHandVanSchellInputBridge.DebugInput("IndexTriggerTouchCancelled", __instance, false);
        }
    }

    public class DroneHandVanTrigger : MonoBehaviour
    {
        public DroneHandVanTrigger(IntPtr ptr) : base(ptr) { }
        public DroneHandVanTrigger() : base(ClassInjector.DerivedConstructorPointer<DroneHandVanTrigger>())
            => ClassInjector.DerivedConstructorBody(this);

        public DroneHandVan owner;

        void OnTriggerEnter(Collider other)
        {
            if (owner != null) owner.DetectorEnter(other);
        }

        void OnTriggerExit(Collider other)
        {
            if (owner != null) owner.DetectorExit(other);
        }
    }
}
