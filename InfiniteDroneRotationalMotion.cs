using UnhollowerRuntimeLib;
using UnityEngine;
using System;

namespace IEYTD_Mod2Code
{
    public class InfiniteDroneRotationalMotion : MonoBehaviour, IDroneGrabbable
    {
        public InfiniteDroneRotationalMotion(IntPtr ptr) : base(ptr) { }
        public InfiniteDroneRotationalMotion() : base(ClassInjector.DerivedConstructorPointer<InfiniteDroneRotationalMotion>())
            => ClassInjector.DerivedConstructorBody(this);

        public Transform pivot;
        public Vector3 localAxis = Vector3.up;
        public bool invert = false;

        public bool useSmoothing = false;
        public float smoothSpeed = 20f;

        public bool allowRocketHands = false;

        public bool isHeld = false;
        public bool IsRocketHeld => false;

        public float continuousAngle = 0f;
        public float deltaAngleThisFrame = 0f;

                private Vector3 _grabAxisWorld;
        private Vector3 _grabRefDirWorld;
        private Vector3 _grabRefPerpWorld;

        private float _lastHandAngle;
        private float _targetAngle;
        private float _smoothedAngle;

        public float axisDeadzoneEnter = 0.006f;
        public float axisDeadzoneExit = 0.010f;

        public float maxDeltaPerFrame = 120f;

        private bool _inAxisDeadzone = false;
        private bool _hasStableAngle = false;
        private float _lastPlanarMag = 0f;

        void Awake()
        {
            if (pivot == null) pivot = transform;
            _targetAngle = continuousAngle;
            _smoothedAngle = continuousAngle;
        }

        void Update()
        {
            deltaAngleThisFrame = 0f;

            if (!isHeld)
                return;

            float appliedDelta;

            if (useSmoothing)
            {
                float prev = _smoothedAngle;
                _smoothedAngle = Mathf.Lerp(_smoothedAngle, _targetAngle, Time.deltaTime * smoothSpeed);
                appliedDelta = _smoothedAngle - prev;
            }
            else
            {
                appliedDelta = _targetAngle - _smoothedAngle;
                _smoothedAngle = _targetAngle;
            }

            if (Mathf.Abs(appliedDelta) > 0.0001f)
            {
                ApplyDelta(appliedDelta);
                continuousAngle += appliedDelta;
                deltaAngleThisFrame = appliedDelta;
            }
        }

        public void OnDroneGrabBegin(DroneHand hand)
        {
            isHeld = true;

            _grabAxisWorld = pivot.TransformDirection(localAxis.normalized);

            Vector3 fallbackForward = Vector3.ProjectOnPlane(pivot.forward, _grabAxisWorld);
            if (fallbackForward.sqrMagnitude < 0.000001f)
                fallbackForward = Vector3.ProjectOnPlane(pivot.up, _grabAxisWorld);
            if (fallbackForward.sqrMagnitude < 0.000001f)
                fallbackForward = Vector3.ProjectOnPlane(Vector3.forward, _grabAxisWorld);
            if (fallbackForward.sqrMagnitude < 0.000001f)
                fallbackForward = Vector3.right;

            _grabRefDirWorld = fallbackForward.normalized;
            _grabRefPerpWorld = Vector3.Cross(_grabAxisWorld, _grabRefDirWorld).normalized;

            _targetAngle = continuousAngle;
            _smoothedAngle = continuousAngle;

            Vector3 planar;
            float planarMag;
            float currentHandAngle = GetHandAngleInFrozenBasis(hand.transform, out planar, out planarMag);

            _lastHandAngle = currentHandAngle;
            _lastPlanarMag = planarMag;
            _inAxisDeadzone = planarMag < axisDeadzoneEnter;
            _hasStableAngle = !_inAxisDeadzone;
        }

        public void OnDroneGrabUpdate(DroneHand hand)
        {
            if (!isHeld)
                return;

            Vector3 planar;
            float planarMag;
            float currentHandAngle = GetHandAngleInFrozenBasis(hand.transform, out planar, out planarMag);

            bool nowInDeadzone = _inAxisDeadzone
                ? (planarMag < axisDeadzoneExit)
                : (planarMag < axisDeadzoneEnter);

            if (nowInDeadzone)
            {
                _inAxisDeadzone = true;
                _hasStableAngle = false;
                _lastPlanarMag = planarMag;
                return;
            }

            if (_inAxisDeadzone || !_hasStableAngle)
            {
                _inAxisDeadzone = false;
                _hasStableAngle = true;
                _lastHandAngle = currentHandAngle;
                _lastPlanarMag = planarMag;
                return;
            }

            float delta = Mathf.DeltaAngle(_lastHandAngle, currentHandAngle);

            bool cameFromNearAxis = _lastPlanarMag < (axisDeadzoneExit * 1.5f);
            if (cameFromNearAxis && Mathf.Abs(delta) > maxDeltaPerFrame)
            {
                _lastHandAngle = currentHandAngle;
                _lastPlanarMag = planarMag;
                return;
            }

            if (invert)
                delta = -delta;

            _targetAngle += delta;
            _lastHandAngle = currentHandAngle;
            _lastPlanarMag = planarMag;
        }

        public void OnDroneGrabEnd(DroneHand hand)
        {
            isHeld = false;
            _inAxisDeadzone = false;
            _hasStableAngle = false;
            _lastPlanarMag = 0f;
        }

        public void BeginRocketGrab(DroneHand hand)
        {
            if (!allowRocketHands) return;
        }

        public void EndRocketGrab()
        {
        }

        public float GetCurrentAngle()
        {
            return continuousAngle;
        }

        public float GetRocketTargetAngle()
        {
            return continuousAngle;
        }

        public void SetRocketTargetAngle(float angle)
        {
            _targetAngle = angle;
            _smoothedAngle = angle;
        }

        public bool IsAtRocketTarget()
        {
            return true;
        }

        public Vector3 GetRocketLatchWorldPos(RaycastHit hit)
        {
            return hit.point;
        }

        public Quaternion GetRocketLatchWorldRot(Quaternion fallbackRot)
        {
            return fallbackRot;
        }

        public void SetRocketLatchPose(Vector3 localPos, Vector3 localEuler)
        {
        }

        public void SetRocketLatchFromTransform(Transform latchTransform)
        {
        }

        private float GetHandAngleInFrozenBasis(Transform handTf, out Vector3 planar, out float planarMag)
        {
            Vector3 toHand = handTf.position - pivot.position;
            planar = Vector3.ProjectOnPlane(toHand, _grabAxisWorld);
            planarMag = planar.magnitude;

            if (planarMag < 0.000001f)
                planar = _grabRefDirWorld;
            else
                planar /= planarMag;

            float x = Vector3.Dot(planar, _grabRefDirWorld);
            float y = Vector3.Dot(planar, _grabRefPerpWorld);

            return Mathf.Atan2(y, x) * 57.29578f;
        }

        private void ApplyDelta(float deltaAngle)
        {
            pivot.localRotation = pivot.localRotation * Quaternion.AngleAxis(deltaAngle, localAxis.normalized);
        }
    }
}
