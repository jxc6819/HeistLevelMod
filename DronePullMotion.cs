using System;
using UnhollowerRuntimeLib;
using UnityEngine;
using System.Collections.Generic;
using MelonLoader;

namespace IEYTD_Mod2Code
{
    public class DronePullMotion : MonoBehaviour, IDroneGrabbable
    {
        public DronePullMotion(IntPtr ptr) : base(ptr) { }
        public DronePullMotion() : base(ClassInjector.DerivedConstructorPointer<DronePullMotion>())
            => ClassInjector.DerivedConstructorBody(this);

        public Transform pivot;
        public Vector3 localPullAxis = Vector3.forward;
        public float maxPullDistance = 0.15f;
        public bool invert = false;

        public float triggerDistance = 0.10f;
        public float minPullSpeed = 0.75f;
        public bool requireSpeed = true;

        public bool latchAfterTrigger = true;
        public bool allowResetOnRelease = false;
        public float returnSpeed = 12f;

        public bool debugLog = false;

        public event Action Triggered;

        private bool _grabbed;
        private bool _triggered;
        private DroneHand _hand;

        private Vector3 _grabStartHandPos;
        private float _pull01;
        private float _pullDistance;

        private float _lastPullDistance;
        private float _pullSpeed;

        private Vector3 _baseLocalPos;
        private bool _didInitBaseLocalPos = false;

        public Transform rocketLatch;
        public float rocketHandClearance = 0.03f;
        public float rocketPullSpeed = 0.60f;
        public float rocketArrivePause = 0.03f;
        public float rocketFinishPause = 0.05f;

        void EnsureInit()
        {
            if (pivot == null)
                pivot = transform;

            if (!_didInitBaseLocalPos && pivot != null)
            {
                _baseLocalPos = pivot.localPosition;
                _didInitBaseLocalPos = true;
            }
        }

        void Awake()
        {
            EnsureInit();
        }

        void Update()
        {
            EnsureInit();

            if (_grabbed)
            {
                float dt = Time.deltaTime;
                if (dt > 0.00001f)
                    _pullSpeed = (_pullDistance - _lastPullDistance) / dt;

                _lastPullDistance = _pullDistance;

                if (!_triggered && ShouldTrigger())
                    FireTrigger();
            }
            else
            {
                if (!_triggered && allowResetOnRelease)
                {
                    _pullDistance = Mathf.Lerp(_pullDistance, 0f, Time.deltaTime * returnSpeed);
                    _pull01 = (maxPullDistance <= 0.00001f) ? 0f : Mathf.Clamp01(_pullDistance / maxPullDistance);
                    ApplyVisual();
                }
            }
        }

        public void OnDroneGrabBegin(DroneHand hand)
        {
            EnsureInit();

            _hand = hand;
            _grabbed = true;

            _grabStartHandPos = hand.transform.position;

            _lastPullDistance = _pullDistance;
            _pullSpeed = 0f;
        }

        public void OnDroneGrabUpdate(DroneHand hand)
        {
            EnsureInit();

            if (!_grabbed) return;
            if (pivot == null)
            {
                MelonLogger.Error($"[DronePullMotion] OnDroneGrabUpdate: pivot null on '{name}'");
                return;
            }

            Vector3 axisWorld = pivot.TransformDirection(localPullAxis.normalized);
            if (invert) axisWorld = -axisWorld;

            Vector3 handDelta = hand.transform.position - _grabStartHandPos;

            float raw = Vector3.Dot(handDelta, axisWorld);
            float dist = Mathf.Clamp(raw, 0f, maxPullDistance);

            if (_triggered && latchAfterTrigger)
                dist = Mathf.Max(dist, triggerDistance);

            _pullDistance = dist;
            _pull01 = (maxPullDistance <= 0.00001f) ? 0f : Mathf.Clamp01(_pullDistance / maxPullDistance);

            ApplyVisual();
        }

        public void OnDroneGrabEnd(DroneHand hand)
        {
            EnsureInit();

            _grabbed = false;
            _hand = null;

            if (_triggered && latchAfterTrigger)
            {
                _pullDistance = Mathf.Clamp(triggerDistance, 0f, maxPullDistance);
                _pull01 = (maxPullDistance <= 0.00001f) ? 0f : Mathf.Clamp01(_pullDistance / maxPullDistance);
                ApplyVisual();
            }
        }

        bool ShouldTrigger()
        {
            if (_pullDistance < triggerDistance) return false;
            if (!requireSpeed) return true;

            return _pullSpeed >= minPullSpeed;
        }

        void FireTrigger()
        {
            _triggered = true;

            if (debugLog)
                Debug.Log($"[DronePullMotion] Triggered on {name} (dist={_pullDistance:F3}, speed={_pullSpeed:F3})");

            try { Triggered?.Invoke(); } catch { }
        }

        void ApplyVisual()
        {
            EnsureInit();

            if (pivot == null)
            {
                MelonLogger.Error($"[DronePullMotion] ApplyVisual: pivot still null on '{name}'");
                return;
            }

            Vector3 axisLocal = localPullAxis.normalized;
            if (invert) axisLocal = -axisLocal;

            pivot.localPosition = _baseLocalPos + axisLocal * _pullDistance;
        }

        public Vector3 GetRocketLatchWorldPos(RaycastHit hit)
        {
            EnsureInit();

            if (rocketLatch != null)
                return rocketLatch.position;

            if (pivot != null)
                return pivot.position;

            return transform.position;
        }

        public Quaternion GetRocketLatchWorldRot(Quaternion fallbackRot)
        {
            EnsureInit();

            if (rocketLatch != null)
                return rocketLatch.rotation;

            if (pivot != null)
                return pivot.rotation;

            return transform.rotation;
        }

        public void BeginRocketGrab(DroneHand hand)
        {
            EnsureInit();

            _hand = hand;
            _grabbed = true;

            _grabStartHandPos = hand.transform.position;
            _lastPullDistance = _pullDistance;
            _pullSpeed = 0f;
        }

        public void EndRocketGrab()
        {
            EnsureInit();

            _grabbed = false;
            _hand = null;

            if (_triggered && latchAfterTrigger)
            {
                _pullDistance = Mathf.Clamp(triggerDistance, 0f, maxPullDistance);
                _pull01 = (maxPullDistance <= 0.00001f) ? 0f : Mathf.Clamp01(_pullDistance / maxPullDistance);
                ApplyVisual();
            }
        }

        public void SetRocketPullDistance(float dist)
        {
            EnsureInit();

            float clamped = Mathf.Clamp(dist, 0f, maxPullDistance);

            _pullSpeed = (Time.deltaTime > 0.00001f)
                ? (clamped - _pullDistance) / Time.deltaTime
                : 0f;

            _pullDistance = clamped;
            _pull01 = (maxPullDistance <= 0.00001f) ? 0f : Mathf.Clamp01(_pullDistance / maxPullDistance);

            ApplyVisual();

            if (!_triggered && ShouldTrigger())
                FireTrigger();
        }

        public void ResetPull(bool resetVisual = true)
        {
            EnsureInit();

            _triggered = false;
            _pullDistance = 0f;
            _pull01 = 0f;
            _pullSpeed = 0f;

            if (resetVisual && pivot != null)
            {
                pivot.localPosition = _baseLocalPos;
            }
        }

        public bool IsTriggered => _triggered;
        public float PullDistance => _pullDistance;
        public float Pull01 => _pull01;
        public float PullSpeed => _pullSpeed;
    }

    public class WiresPullMotion : MonoBehaviour
    {
        public WiresPullMotion(IntPtr ptr) : base(ptr) { }
        public WiresPullMotion() : base(ClassInjector.DerivedConstructorPointer<WiresPullMotion>())
            => ClassInjector.DerivedConstructorBody(this);

        public DronePullMotion pull;
        public ObjectBank bank;
        public Transform rocketLatch;
        public float rocketHandClearance = 0.03f;
        public float rocketPullSpeed = 0.60f;
        public float rocketArrivePause = 0.03f;
        public float rocketFinishPause = 0.05f;

        void Start()
        {
            pull = GetComponent<DronePullMotion>();
            if (pull == null)
                pull = gameObject.AddComponent<DronePullMotion>();

            pull.pivot = transform;
            pull.localPullAxis = Vector3.forward;

            pull.rocketLatch = rocketLatch;
            pull.rocketHandClearance = rocketHandClearance;
            pull.rocketPullSpeed = rocketPullSpeed;
            pull.rocketArrivePause = rocketArrivePause;
            pull.rocketFinishPause = rocketFinishPause;

            pull.Triggered -= OnPulled;
            pull.Triggered += OnPulled;

            bank = ObjectBank.Instance;
        }

        void OnPulled()
        {
            MelonLogger.Msg("[WiresPullMotion - OnPulled] Checkpoint 1");

            GameObject goodWires = GameObject.Find("SM_wires");
            GameObject badWires = goodWires.transform.parent.GetChild(3).gameObject;
            MelonLogger.Msg($"[OnPulled] - BadWires: {badWires.name}");
            AudioUtil.PlayAt("WiresRipped.ogg", goodWires.transform.position);

            goodWires.SetActive(false);
            badWires.SetActive(true);

            GameObject spark = new GameObject("WireSpark");
            spark.transform.position = badWires.transform.position;
            spark.AddComponent<SparkDriver>().Burst();
            Destroy(spark, 2f);

            List<LaserEmitter> emitters = bank.Manager.GetComponent<LaserColumnSpawner>().emitters;
            for (int i = 0; i < emitters.Count; i++)
            {
                emitters[i].SetEnabled(false);
            }

            WireVisionManager wvm = GameObject.Find("Manager").GetComponent<WireVisionManager>();
            wvm.Enable(false);
            wvm.Kill();
            HeistLevelManager.playStinger();
            HeistLevelManager.playHandler("Handler_LasersDisarmed.wav", 2.2f);
        }
    }
}
