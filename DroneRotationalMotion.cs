using UnhollowerRuntimeLib;
using UnityEngine;
using System;

namespace IEYTD_Mod2Code
{
    public class DroneRotationalMotion : MonoBehaviour, IDroneGrabbable
    {
        public DroneRotationalMotion(IntPtr ptr) : base(ptr) { }
        public DroneRotationalMotion() : base(ClassInjector.DerivedConstructorPointer<DroneRotationalMotion>())
            => ClassInjector.DerivedConstructorBody(this);

        public Transform pivot;
        public Vector3 localAxis = Vector3.up;

        public float minAngle = 0f;
        public float maxAngle = 180f;
        public bool invert = false;

        public bool infiniteRotation = false;

        public bool useSmoothing = true;
        public float smoothSpeed = 20f;

        public bool allowRocketHands = true;
        public bool useRocketLatchPose = false;
        public Vector3 rocketLatchLocalPos = Vector3.zero;
        public Vector3 rocketLatchLocalEuler = Vector3.zero;
        public float rocketSurfaceOffset = 0.01f;

        public float rocketRotateSpeed = 90f;
        public float rocketArrivePause = 0.04f;
        public float rocketFinishPause = 0.05f;
        public float rocketDoneAngleEpsilon = 0.2f;

        public float rocketHandClearance = 0.08f;

        public bool isHeld = false;
        public bool IsRocketHeld => isHeld && _rocketMode;

        private bool _rocketMode;
        private DroneHand _hand;

        private Quaternion _baseLocalRotation;
        private float _startAngle;
        private Vector3 _grabStartDirWorld;
        private float _targetAngle;
        private float _currentAngle;

        void Awake()
        {
            if (pivot == null) pivot = transform;
        }

        void Update()
        {
            if (!isHeld) return;

            if (_rocketMode)
            {
                _currentAngle = Mathf.MoveTowards(_currentAngle, _targetAngle, rocketRotateSpeed * Time.deltaTime);
                ApplyAngle(_currentAngle);
            }
            else
            {
                if (useSmoothing)
                {
                    _currentAngle = Mathf.Lerp(_currentAngle, _targetAngle, Time.deltaTime * smoothSpeed);
                    ApplyAngle(_currentAngle);
                }
                else
                {
                    _currentAngle = _targetAngle;
                    ApplyAngle(_currentAngle);
                }
            }
        }

        public void OnDroneGrabBegin(DroneHand hand)
        {
            _hand = hand;
            isHeld = true;
            _rocketMode = false;

            _baseLocalRotation = pivot.localRotation;

            _startAngle = infiniteRotation
                ? _currentAngle
                : Mathf.Clamp(_currentAngle, minAngle, maxAngle);

            _grabStartDirWorld = GetProjectedHandDirWorld(_hand.transform);
            _targetAngle = _startAngle;
        }

        public void OnDroneGrabUpdate(DroneHand hand)
        {
            if (!isHeld || _rocketMode) return;

            Vector3 currentDirWorld = GetProjectedHandDirWorld(hand.transform);
            Vector3 axisWorld = pivot.TransformDirection(localAxis.normalized);

            float signed = Vector3.SignedAngle(_grabStartDirWorld, currentDirWorld, axisWorld);
            if (invert) signed = -signed;

            float desired = _startAngle + signed;

            _targetAngle = infiniteRotation
                ? desired
                : Mathf.Clamp(desired, minAngle, maxAngle);
        }

        public void OnDroneGrabEnd(DroneHand hand)
        {
            isHeld = false;
            _rocketMode = false;
            _hand = null;

            if (!infiniteRotation)
                _currentAngle = Mathf.Clamp(_currentAngle, minAngle, maxAngle);
        }

        public void BeginRocketGrab(DroneHand hand)
        {
            if (!allowRocketHands) return;

            _hand = hand;
            isHeld = true;
            _rocketMode = true;

            _baseLocalRotation = pivot.localRotation;

            _startAngle = infiniteRotation
                ? _currentAngle
                : Mathf.Clamp(_currentAngle, minAngle, maxAngle);

            _targetAngle = _startAngle;
        }

        public void EndRocketGrab()
        {
            isHeld = false;
            _rocketMode = false;
            _hand = null;

            if (!infiniteRotation)
                _currentAngle = Mathf.Clamp(_currentAngle, minAngle, maxAngle);
        }

        public float GetCurrentAngle()
        {
            if (infiniteRotation)
                return _currentAngle;

            return Mathf.Clamp(_currentAngle, minAngle, maxAngle);
        }

        public float GetRocketTargetAngle()
        {
            if (infiniteRotation)
                return _currentAngle + 360f;

            float current = GetCurrentAngle();
            float toMin = Mathf.Abs(current - minAngle);
            float toMax = Mathf.Abs(maxAngle - current);

            return (toMax >= toMin) ? maxAngle : minAngle;
        }

        public void SetRocketTargetAngle(float angle)
        {
            _targetAngle = infiniteRotation
                ? angle
                : Mathf.Clamp(angle, minAngle, maxAngle);
        }

        public bool IsAtRocketTarget()
        {
            return Mathf.Abs(_currentAngle - _targetAngle) <= rocketDoneAngleEpsilon;
        }

        public Vector3 GetRocketLatchWorldPos(RaycastHit hit)
        {
            if (useRocketLatchPose)
                return pivot.TransformPoint(rocketLatchLocalPos);

            return hit.point + hit.normal * rocketHandClearance;
        }

        public Quaternion GetRocketLatchWorldRot(Quaternion fallbackRot)
        {
            if (useRocketLatchPose)
                return pivot.rotation * Quaternion.Euler(rocketLatchLocalEuler);

            return fallbackRot;
        }

        public void SetRocketLatchPose(Vector3 localPos, Vector3 localEuler)
        {
            useRocketLatchPose = true;
            rocketLatchLocalPos = localPos;
            rocketLatchLocalEuler = localEuler;
        }

        private Vector3 GetProjectedHandDirWorld(Transform handTf)
        {
            Vector3 axisWorld = pivot.TransformDirection(localAxis.normalized);
            Vector3 fromPivot = handTf.position - pivot.position;

            Vector3 projected = Vector3.ProjectOnPlane(fromPivot, axisWorld);
            if (projected.sqrMagnitude < 0.000001f)
                projected = Vector3.ProjectOnPlane(pivot.forward, axisWorld);

            return projected.normalized;
        }

        private void ApplyAngle(float angle)
        {
            Quaternion rot = Quaternion.AngleAxis(angle - _startAngle, localAxis.normalized);
            pivot.localRotation = _baseLocalRotation * rot;
        }

        public void SetRocketLatchFromTransform(Transform latchTransform)
        {
            if (latchTransform == null) return;

            Transform p = pivot != null ? pivot : transform;

            Vector3 localPos = p.InverseTransformPoint(latchTransform.position);
            Quaternion localRot = Quaternion.Inverse(p.rotation) * latchTransform.rotation;

            rocketLatchLocalPos = localPos;
            rocketLatchLocalEuler = localRot.eulerAngles;
            useRocketLatchPose = true;
        }
    }
}
