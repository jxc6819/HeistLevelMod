using System.Collections;
using MelonLoader;
using UnityEngine;
using UnhollowerRuntimeLib;
using System;

namespace IEYTD_Mod2Code
{
    public class SparkDriver : MonoBehaviour
    {
        public SparkDriver(IntPtr ptr) : base(ptr) { }
        public SparkDriver() : base(ClassInjector.DerivedConstructorPointer<SparkDriver>())
            => ClassInjector.DerivedConstructorBody(this);
        public int segments = 8;
        public float sparkLength = 0.35f;
        public float jitter = 0.06f;
        public float width = 0.025f;

        public float sparkLifetime = 0.04f;
        public float sparksPerSecond = 18f;

        public float lightIntensity = 28f;
        public float lightRange = 9f;
        public float lightFadeSpeed = 40f;

        public Color sparkColor = new Color(0.35f, 0.85f, 1f, 1f);

        private LineRenderer _line;
        private Light _light;

        private object _loopHandle;
        private bool _playing;

        void Awake()
        {
            BuildIfNeeded();
            StopSpark();
        }

        void OnDisable()
        {
            StopSpark();
        }

        public void PlaySpark()
        {
            BuildIfNeeded();

            if (_playing) return;
            _playing = true;

            _loopHandle = MelonCoroutines.Start(SparkLoop());
        }

        public void StopSpark()
        {
            _playing = false;

            if (_loopHandle != null)
            {
                MelonCoroutines.Stop(_loopHandle);
                _loopHandle = null;
            }

            if (_line != null)
                _line.enabled = false;

            if (_light != null)
            {
                _light.enabled = false;
                _light.intensity = 0f;
            }
        }

        public void Burst()
        {
            BuildIfNeeded();
            MelonCoroutines.Start(SingleSpark());
        }

        private IEnumerator SparkLoop()
        {
            float delay = 1f / Mathf.Max(1f, sparksPerSecond);

            while (_playing)
            {

                yield return SingleSpark();

                yield return new WaitForSeconds(delay);
            }
        }

        private IEnumerator SingleSpark()
        {
            GenerateSpark();

            if (_line != null) _line.enabled = true;

            FlashLight();

            yield return new WaitForSeconds(sparkLifetime);

            if (_line != null) _line.enabled = false;
        }

        private void GenerateSpark()
        {
            if (_line == null) return;

            Vector3[] points = new Vector3[Mathf.Max(2, segments)];

            Vector3 dir = UnityEngine.Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y);
            dir.Normalize();

            int segCount = points.Length;

            for (int i = 0; i < segCount; i++)
            {
                float t = (segCount <= 1) ? 0f : (float)i / (segCount - 1);

                Vector3 basePos = dir * sparkLength * t;

                Vector3 offset = UnityEngine.Random.insideUnitSphere * jitter * (1f - t);

                points[i] = basePos + offset;
            }

            _line.positionCount = segCount;
            _line.SetPositions(points);
        }

        private void FlashLight()
        {
            if (_light == null) return;

            _light.color = sparkColor;
            _light.range = lightRange;
            _light.intensity = lightIntensity;
            _light.enabled = true;

            MelonCoroutines.Start(FadeLight());
        }

        private IEnumerator FadeLight()
        {
            if (_light == null) yield break;

            while (_light.intensity > 0.1f)
            {
                _light.intensity = Mathf.Lerp(_light.intensity, 0f, Time.deltaTime * lightFadeSpeed);
                yield return null;
            }

            _light.intensity = 0f;
            _light.enabled = false;
        }

        private void BuildIfNeeded()
        {
            if (_line != null && _light != null) return;

            if (_line == null)
            {
                GameObject lineGO = new GameObject("Spark_Line");
                lineGO.transform.SetParent(transform, false);
                lineGO.transform.localPosition = Vector3.zero;
                lineGO.transform.localRotation = Quaternion.identity;

                _line = lineGO.AddComponent<LineRenderer>();
                _line.useWorldSpace = false;
                _line.enabled = false;
                _line.positionCount = 0;

                _line.startWidth = width;
                _line.endWidth = width * 0.5f;
                _line.numCapVertices = 2;
                _line.numCornerVertices = 2;

                Shader shader =
                    Shader.Find("Unlit/Color") ??
                    Shader.Find("Sprites/Default") ??
                    Shader.Find("Standard");

                Material mat = new Material(shader);
                mat.name = "Spark_Line_Mat";
                mat.color = sparkColor;

                _line.material = mat;

                _line.sortingOrder = 50;
            }

            if (_light == null)
            {
                GameObject lightGO = new GameObject("Spark_Light");
                lightGO.transform.SetParent(transform, false);
                lightGO.transform.localPosition = Vector3.zero;
                lightGO.transform.localRotation = Quaternion.identity;

                _light = lightGO.AddComponent<Light>();
                _light.type = LightType.Point;
                _light.shadows = LightShadows.None;
                _light.enabled = false;
                _light.intensity = 0f;
                _light.range = lightRange;
                _light.color = sparkColor;
            }
        }
    }
}
