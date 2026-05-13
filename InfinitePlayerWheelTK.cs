using System;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using SG.Phoenix.Assets.Code.InputManagement;
using SG.Phoenix.Assets.Code.Interactables;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class InfinitePlayerWheelTK : MonoBehaviour
    {
        public InfinitePlayerWheelTK(IntPtr ptr) : base(ptr) { }
        public InfinitePlayerWheelTK() : base(ClassInjector.DerivedConstructorPointer<InfinitePlayerWheelTK>())
            => ClassInjector.DerivedConstructorBody(this);

        public Transform pivot;
        public Vector3 localAxis = Vector3.forward;
        public Transform[] handlePoints;

        public float sensitivity = 0.55f;
        public bool invert = false;
        public float maxDegreesPerSecond = 300.0f;
        public float handDeadzone = 0.0004f;
        public bool useSmoothing = true;
        public float smoothing = 20.0f;
        public bool debugLogs = true;

        public float lastRot;
        public float rot;
        public float continuousAngle;

        public bool IsActive => _active;

        private RotationalMotion _rm;
        private bool _active;
        private int _activeHandId = -1;
        private Vector3 _lastControlPos;
        private Vector3 _latchedLocalPoint;
        private float _smoothedDegPerSec;
        private float _lastDebugLogTime;
        private float _unwrappedAngle;
        private float _startAngle;
        private Quaternion _startLocalRotation;
        private LineRenderer[] _cachedBeamRenderers;

        private static readonly List<InfinitePlayerWheelTK> _instances = new List<InfinitePlayerWheelTK>();
        internal static readonly Dictionary<int, InfinitePlayerWheelTK> _activeByHand = new Dictionary<int, InfinitePlayerWheelTK>();
        private static bool _patchesApplied;

        bool audioTriggered = false;

        public void Init(Transform pivot)
        {
            this.pivot = pivot;
            if (pivot != null)
            {
                Transform[] children = new Transform[pivot.childCount];
                for (int i = 0; i < pivot.childCount; i++)
                {
                    children[i] = pivot.GetChild(i);
                }
                handlePoints = children;
            }
        }

        private void OnEnable()
        {
            if (!_instances.Contains(this)) _instances.Add(this);
        }

        private void OnDisable()
        {
            _instances.Remove(this);

            if (_activeHandId != -1)
            {
                InfinitePlayerWheelTK current;
                if (_activeByHand.TryGetValue(_activeHandId, out current) && current == this)
                    _activeByHand.Remove(_activeHandId);
            }

            _active = false;
            _activeHandId = -1;
            _cachedBeamRenderers = null;
        }

        private void Start()
        {
            if (pivot == null) pivot = transform;

            _rm = GetComponent<RotationalMotion>()
                ?? GetComponentInChildren<RotationalMotion>()
                ?? GetComponentInParent<RotationalMotion>();

            _startAngle = ReadLocalAngleDegrees();
            _unwrappedAngle = _startAngle;
            _startLocalRotation = pivot.localRotation;

            rot = _unwrappedAngle;
            lastRot = rot;
            continuousAngle = rot;

            if (debugLogs)
            {
                MelonLogger.Msg("[InfiniteWheelTK] Start obj=" + gameObject.name
                    + " rm=" + (_rm != null ? _rm.gameObject.name : "null")
                    + " pivot=" + (pivot != null ? pivot.name : "null")
                    + " axis=" + localAxis
                    + " startAngle=" + _startAngle);
            }
        }

        private void Update()
        {
            lastRot = rot;
            rot = _unwrappedAngle;
            continuousAngle = rot;
        }

        public static InfinitePlayerWheelTK FindActiveForHand(VRHandInput hand)
        {
            if (hand == null) return null;

            for (int i = 0; i < _instances.Count; i++)
            {
                var wheel = _instances[i];
                if (wheel == null || wheel._rm == null) continue;

                try
                {
                    if (wheel._rm.heldHand == hand)
                        return wheel;
                }
                catch { }
            }

            return null;
        }

        public void BeginControl(Transform handRoot, Transform controlPoint)
        {
            if (handRoot == null || pivot == null) return;

            _active = true;
            _activeHandId = handRoot.GetInstanceID();
            _lastControlPos = controlPoint != null ? controlPoint.position : handRoot.position;
            _latchedLocalPoint = ChooseLatchLocalPoint(_lastControlPos);
            _smoothedDegPerSec = 0.0f;
            _unwrappedAngle = ResolveCurrentAngleNear(_unwrappedAngle);
            if (_unwrappedAngle < _startAngle) _unwrappedAngle = _startAngle;
            _cachedBeamRenderers = handRoot.GetComponentsInChildren<LineRenderer>(true);
            _activeByHand[_activeHandId] = this;

            if (!audioTriggered && !HeistLevelManager.VaultUnlocked)
            {
                audioTriggered = true;
                HeistLevelManager.playHandler("Handler_VaultHint.wav", 1.5f, true);
            }

            if (debugLogs)
            {
                Vector3 latchWorld = GetLatchedWorldPoint();
                MelonLogger.Msg("[InfiniteWheelTK] Begin obj=" + gameObject.name
                    + " hand=" + handRoot.name
                    + " control=" + (controlPoint != null ? controlPoint.name : "<handRoot>")
                    + " latchWorld=" + latchWorld
                    + " startClamp=" + _startAngle);
            }
        }

        public void EndControl(Transform handRoot)
        {
            if (!_active) return;
            if (handRoot != null && handRoot.GetInstanceID() != _activeHandId) return;

            if (debugLogs)
            {
                MelonLogger.Msg("[InfiniteWheelTK] End obj=" + gameObject.name
                    + " hand=" + (handRoot != null ? handRoot.name : "null"));
            }

            if (_activeHandId != -1)
            {
                InfinitePlayerWheelTK current;
                if (_activeByHand.TryGetValue(_activeHandId, out current) && current == this)
                    _activeByHand.Remove(_activeHandId);
            }

            _active = false;
            _activeHandId = -1;
            _smoothedDegPerSec = 0.0f;
            _cachedBeamRenderers = null;
        }

        public void Tick(Transform handRoot, Transform controlPoint, float dt)
        {
            if (!_active || handRoot == null || pivot == null) return;
            if (handRoot.GetInstanceID() != _activeHandId) return;
            if (dt <= 0.0f) dt = 0.016f;

            Vector3 controlPos = controlPoint != null ? controlPoint.position : handRoot.position;
            Vector3 handDelta = controlPos - _lastControlPos;
            _lastControlPos = controlPos;

            if (handDelta.sqrMagnitude < handDeadzone * handDeadzone)
            {
                UpdateBeamEndpoint(handRoot);
                return;
            }

            Vector3 axisWorld = pivot.TransformDirection(localAxis.normalized);
            Vector3 latchWorld = GetLatchedWorldPoint();
            Vector3 radiusWorld = Vector3.ProjectOnPlane(latchWorld - pivot.position, axisWorld);
            float radius = radiusWorld.magnitude;

            if (radius < 0.0005f)
            {
                UpdateBeamEndpoint(handRoot);
                return;
            }

            Vector3 radiusDir = radiusWorld / radius;
            Vector3 tangentWorld = Vector3.Cross(axisWorld, radiusDir).normalized;
            float tangentialMeters = Vector3.Dot(handDelta, tangentWorld);
            float degrees = (tangentialMeters / radius) * 57.29578f * sensitivity;

            if (invert) degrees = -degrees;

            float rawDegPerSec = degrees / dt;
            float finalDegPerSec = rawDegPerSec;

            if (useSmoothing)
            {
                float lerpT = 1.0f - Mathf.Exp(-smoothing * dt);
                _smoothedDegPerSec = Mathf.Lerp(_smoothedDegPerSec, rawDegPerSec, lerpT);
                finalDegPerSec = _smoothedDegPerSec;
            }

            finalDegPerSec = Mathf.Clamp(finalDegPerSec, -maxDegreesPerSecond, maxDegreesPerSecond);
            float finalDegrees = finalDegPerSec * dt;

            _unwrappedAngle += finalDegrees;
            if (_unwrappedAngle < _startAngle)
            {
                _unwrappedAngle = _startAngle;
                if (finalDegPerSec < 0.0f) _smoothedDegPerSec = 0.0f;
            }

            ApplyAngle(_unwrappedAngle);

            if (_rm != null)
            {
                try
                {
                    _rm.SetRotation(_unwrappedAngle);
                }
                catch { }
            }

            UpdateBeamEndpoint(handRoot);

            if (debugLogs && Time.time - _lastDebugLogTime > 0.2f)
            {
                _lastDebugLogTime = Time.time;
                MelonLogger.Msg("[InfiniteWheelTK] Tick obj=" + gameObject.name
                    + " tangential=" + tangentialMeters
                    + " dDeg=" + finalDegrees
                    + " total=" + _unwrappedAngle
                    + " minClamp=" + _startAngle);
            }
        }

        private void ApplyAngle(float absoluteAngle)
        {
            float delta = absoluteAngle - _startAngle;
            pivot.localRotation = _startLocalRotation * Quaternion.AngleAxis(delta, localAxis.normalized);
        }

        private Vector3 GetLatchedWorldPoint()
        {
            return pivot.TransformPoint(_latchedLocalPoint);
        }

        private void UpdateBeamEndpoint(Transform handRoot)
        {
            if (handRoot == null) return;

            LineRenderer[] renderers = _cachedBeamRenderers;
            if (renderers == null || renderers.Length == 0)
                renderers = handRoot.GetComponentsInChildren<LineRenderer>(true);

            if (renderers == null || renderers.Length == 0)
                return;

            Vector3 latchWorld = GetLatchedWorldPoint();

            for (int i = 0; i < renderers.Length; i++)
            {
                var lr = renderers[i];
                if (lr == null || !lr.enabled) continue;

                int count = lr.positionCount;
                if (count <= 0) continue;

                try
                {
                    Vector3 startWorld;

                    if (count > 0)
                    {
                        startWorld = lr.GetPosition(0);
                    }
                    else
                    {
                        startWorld = handRoot.position;
                    }

                    if (count == 1)
                    {
                        lr.SetPosition(0, latchWorld);
                        continue;
                    }

                    for (int p = 1; p < count; p++)
                    {
                        float t = (float)p / (float)(count - 1);
                        lr.SetPosition(p, Vector3.Lerp(startWorld, latchWorld, t));
                    }
                }
                catch { }
            }

            _cachedBeamRenderers = renderers;
        }

        private float ResolveCurrentAngleNear(float referenceAngle)
        {
            float wrapped = ReadLocalAngleDegrees();
            float best = wrapped;
            float bestDiff = Mathf.Abs(best - referenceAngle);

            for (int k = -4; k <= 4; k++)
            {
                float candidate = wrapped + (360.0f * k);
                if (candidate < _startAngle) continue;

                float diff = Mathf.Abs(candidate - referenceAngle);
                if (diff < bestDiff)
                {
                    best = candidate;
                    bestDiff = diff;
                }
            }

            if (best < _startAngle) best = _startAngle;
            return best;
        }

        private float ReadLocalAngleDegrees()
        {
            if (pivot == null) return 0.0f;

            Vector3 e = pivot.localEulerAngles;
            Vector3 axis = localAxis.normalized;
            float absX = Mathf.Abs(axis.x);
            float absY = Mathf.Abs(axis.y);
            float absZ = Mathf.Abs(axis.z);

            float angle;
            if (absX >= absY && absX >= absZ) angle = e.x * Mathf.Sign(axis.x == 0.0f ? 1.0f : axis.x);
            else if (absY >= absX && absY >= absZ) angle = e.y * Mathf.Sign(axis.y == 0.0f ? 1.0f : axis.y);
            else angle = e.z * Mathf.Sign(axis.z == 0.0f ? 1.0f : axis.z);

            if (angle > 180.0f) angle -= 360.0f;
            return angle;
        }

        private Vector3 ChooseLatchLocalPoint(Vector3 handWorldPos)
        {
            Transform best = null;
            float bestSqr = float.MaxValue;

            if (handlePoints != null)
            {
                for (int i = 0; i < handlePoints.Length; i++)
                {
                    var hp = handlePoints[i];
                    if (hp == null) continue;

                    float sqr = (hp.position - handWorldPos).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = hp;
                    }
                }
            }

            if (best != null)
                return pivot.InverseTransformPoint(best.position);

            Vector3 axisWorld = pivot.TransformDirection(localAxis.normalized);
            Vector3 fromPivot = handWorldPos - pivot.position;
            Vector3 projected = Vector3.ProjectOnPlane(fromPivot, axisWorld);

            if (projected.sqrMagnitude < 0.000001f)
                projected = Vector3.ProjectOnPlane(pivot.right, axisWorld);

            return pivot.InverseTransformPoint(pivot.position + projected);
        }
    }

    [HarmonyPatch(typeof(TelekinesisHandState), "HandUpdate")]
    internal static class Patch_TK_HandUpdate_InfiniteWheelTK_RM
    {
        private static float _lastGlobalLogTime;

        private static void Postfix(TelekinesisHandState __instance)
        {
            if (__instance == null || __instance.gameObject == null) return;

            var handRoot = __instance.transform;
            if (handRoot == null) return;

            var vhi = __instance.gameObject.GetComponent<VRHandInput>();
            if (vhi == null) return;

            int handId = handRoot.GetInstanceID();
            InfinitePlayerWheelTK oldWheel = null;
            InfinitePlayerWheelTK._activeByHand.TryGetValue(handId, out oldWheel);

            InfinitePlayerWheelTK wheel = InfinitePlayerWheelTK.FindActiveForHand(vhi);
            Transform controlPoint = GetControlPoint(handRoot);

            if (wheel == null)
            {
                if (oldWheel != null) oldWheel.EndControl(handRoot);
                return;
            }

            if (oldWheel != wheel)
            {
                if (oldWheel != null) oldWheel.EndControl(handRoot);
                wheel.BeginControl(handRoot, controlPoint);
            }

            float dt = Time.deltaTime;
            if (dt <= 0.0f) dt = 0.016f;

            wheel.Tick(handRoot, controlPoint, dt);

            if (wheel.debugLogs && Time.time - _lastGlobalLogTime > 0.5f)
            {
                _lastGlobalLogTime = Time.time;
                string cpName = controlPoint != null ? controlPoint.name : "null";
                MelonLogger.Msg("[InfiniteWheelTK] Postfix hand=" + handRoot.name
                    + " wheel=" + wheel.gameObject.name
                    + " control=" + cpName);
            }
        }

        private static Transform GetControlPoint(Transform handRoot)
        {
            if (handRoot == null) return null;

            if (handRoot.childCount > 2)
            {
                Transform t = handRoot.GetChild(2);
                if (t != null) return t;
            }

            return handRoot;
        }
    }
}
