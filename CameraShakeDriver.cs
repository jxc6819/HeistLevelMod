using System;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class CameraShakeDriver : MonoBehaviour
    {
        public CameraShakeDriver(IntPtr p) : base(p) { }
        public CameraShakeDriver() : base(ClassInjector.DerivedConstructorPointer<CameraShakeDriver>())
            => ClassInjector.DerivedConstructorBody(this);

        public Transform ShakeTarget;
        public bool StartEnabled = false;

        public float PositionAmplitudeX = 0.00035f;
        public float PositionAmplitudeY = 0.00028f;
        public float PositionAmplitudeZ = 0.00018f;

        public float RotationAmplitudeX = 0.22f;
        public float RotationAmplitudeY = 0.16f;
        public float RotationAmplitudeZ = 0.08f;

        public float LowFrequency = 3.6f;
        public float HighFrequency = 11.5f;
        public float HighFrequencyWeight = 0.38f;

        public float Intensity = 1f;

        private bool _on;
        private float _shakeUntil = -1f;

        private Vector3 _baseLocalPos;
        private Quaternion _baseLocalRot;
        private bool _haveBasePose;

        private float _seedPX;
        private float _seedPY;
        private float _seedPZ;
        private float _seedRX;
        private float _seedRY;
        private float _seedRZ;

        void Awake()
        {
            if (ShakeTarget == null)
                ShakeTarget = transform;

            GenerateSeeds();
            CaptureBasePose();

            if (StartEnabled)
                TurnOn();
            else
                RestoreBasePose();
        }

        void OnEnable()
        {
            if (ShakeTarget == null)
                ShakeTarget = transform;

            CaptureBasePose();
        }

        void OnDisable()
        {
            _on = false;
            _shakeUntil = -1f;
            RestoreBasePose();
        }

        void LateUpdate()
        {
            if (ShakeTarget == null)
                return;

            if (!_on)
                return;

            if (_shakeUntil > 0f && Time.time >= _shakeUntil)
            {
                TurnOff();
                return;
            }

            if (!_haveBasePose)
                CaptureBasePose();

            float t = Time.time;
            float k = Mathf.Max(0f, Intensity);

            float px = LayeredSignedNoise(_seedPX, t) * PositionAmplitudeX * k;
            float py = LayeredSignedNoise(_seedPY, t + 7.13f) * PositionAmplitudeY * k;
            float pz = LayeredSignedNoise(_seedPZ, t + 13.81f) * PositionAmplitudeZ * k;

            float rx = LayeredSignedNoise(_seedRX, t + 1.71f) * RotationAmplitudeX * k;
            float ry = LayeredSignedNoise(_seedRY, t + 4.27f) * RotationAmplitudeY * k;
            float rz = LayeredSignedNoise(_seedRZ, t + 9.64f) * RotationAmplitudeZ * k;

            ShakeTarget.localPosition = _baseLocalPos + new Vector3(px, py, pz);
            ShakeTarget.localRotation = _baseLocalRot * Quaternion.Euler(rx, ry, rz);
        }

        public void TurnOn()
        {
            if (ShakeTarget == null)
                ShakeTarget = transform;

            CaptureBasePose();
            _shakeUntil = -1f;
            _on = true;
        }

        public void TurnOff()
        {
            _on = false;
            _shakeUntil = -1f;
            RestoreBasePose();
        }

        public void ShakeFor(float seconds)
        {
            if (seconds <= 0f)
            {
                TurnOff();
                return;
            }

            if (ShakeTarget == null)
                ShakeTarget = transform;

            CaptureBasePose();
            _on = true;
            _shakeUntil = Time.time + seconds;
        }

        public void SetIntensity(float intensity)
        {
            Intensity = Mathf.Max(0f, intensity);
        }

        private void CaptureBasePose()
        {
            if (ShakeTarget == null)
                return;

            _baseLocalPos = ShakeTarget.localPosition;
            _baseLocalRot = ShakeTarget.localRotation;
            _haveBasePose = true;
        }

        private void RestoreBasePose()
        {
            if (ShakeTarget == null || !_haveBasePose)
                return;

            ShakeTarget.localPosition = _baseLocalPos;
            ShakeTarget.localRotation = _baseLocalRot;
        }

        private void GenerateSeeds()
        {
            _seedPX = UnityEngine.Random.Range(0f, 1000f);
            _seedPY = UnityEngine.Random.Range(0f, 1000f);
            _seedPZ = UnityEngine.Random.Range(0f, 1000f);
            _seedRX = UnityEngine.Random.Range(0f, 1000f);
            _seedRY = UnityEngine.Random.Range(0f, 1000f);
            _seedRZ = UnityEngine.Random.Range(0f, 1000f);
        }

        private float LayeredSignedNoise(float seed, float t)
        {
            float low = SampleSignedNoise(seed, t * LowFrequency);
            float high = SampleSignedNoise(seed + 41.37f, t * HighFrequency);
            return low + high * HighFrequencyWeight;
        }

        private float SampleSignedNoise(float xSeed, float y)
        {
            return (Mathf.PerlinNoise(xSeed, y) - 0.5f) * 2f;
        }
    }
}
