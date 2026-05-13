using System;
using System.Collections;
using MelonLoader;
using UnhollowerRuntimeLib;
using UnhollowerBaseLib.Attributes;
using UnityEngine;

namespace IEYTD_Mod2Code
{

    public class GlassDriver : MonoBehaviour
    {
        public GlassDriver(IntPtr ptr) : base(ptr) { }
        public GlassDriver() : base(ClassInjector.DerivedConstructorPointer<GlassDriver>())
            => ClassInjector.DerivedConstructorBody(this);

        public int shardCount = 22;
        public float shardSizeMin = 0.07f;
        public float shardSizeMax = 0.18f;
        public float shardThickness = 0.012f;
        public float spawnSurfaceOffset = 0.01f;

        public float burstForceMin = 1.8f;
        public float burstForceMax = 4.2f;
        public float sidewaysForce = 1.4f;
        public float upwardForce = 0.5f;
        public float torqueMin = 240f;
        public float torqueMax = 720f;

        public float shardLifetimeMin = 2.25f;
        public float shardLifetimeMax = 4.0f;
        public bool fadeBeforeDestroy = false;
        public float fadeDuration = 0.35f;

        public bool disableOriginalColliders = true;
        public bool disableOriginalRenderers = true;
        public bool disableOriginalPickups = false;
        public bool spawnIntoWorldSpace = true;
        public bool disableShardShadows = true;

        public bool invertNormal = false;

        public string preferredRendererName = "";

        bool _broken;

        Renderer[] _renderers;
        Collider[] _colliders;
        MonoBehaviour[] _monoBehaviours;
        Material _sourceMaterial;
        Bounds _combinedBounds;
        Transform _sampleTransform;
        Vector3 _sampleRight;
        Vector3 _sampleUp;
        Vector3 _sampleNormal;
        float _planeWidth;
        float _planeHeight;
        GameObject _shardRoot;

        void Awake()
        {
            CacheTargets();
        }

        void Start()
        {
            CacheTargets();
        }

        void OnTriggerEnter(Collider other)
        {
            if (other == null) return;

            DroneHand hand = other.GetComponentInParent<DroneHand>();
            if (hand == null)
                hand = other.GetComponent<DroneHand>();

            if (hand == null || !hand._launching)
                return;

            hand.NotifyGlassBroken(this);
        }

        public void Break()
        {
            if (_broken) return;
            _broken = true;
            AudioUtil.PlayAt("WindowBreak.ogg", transform.position);
            CacheTargets();
            HideOriginalGlass();
            SpawnShards();
        }

        public void ResetGlass()
        {
            _broken = false;

            if (_renderers != null)
            {
                for (int i = 0; i < _renderers.Length; i++)
                    if (_renderers[i] != null)
                        _renderers[i].enabled = true;
            }

            if (_colliders != null)
            {
                for (int i = 0; i < _colliders.Length; i++)
                    if (_colliders[i] != null)
                        _colliders[i].enabled = true;
            }

            if (_monoBehaviours != null && disableOriginalPickups)
            {
                for (int i = 0; i < _monoBehaviours.Length; i++)
                {
                    var mb = _monoBehaviours[i];
                    if (mb == null) continue;
                    if (mb == this) continue;
                    mb.enabled = true;
                }
            }

            if (_shardRoot != null)
            {
                Destroy(_shardRoot);
                _shardRoot = null;
            }
        }

        void CacheTargets()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
            _monoBehaviours = GetComponentsInChildren<MonoBehaviour>(true);

            Renderer best = FindBestRenderer();
            if (best != null)
            {
                _sourceMaterial = best.sharedMaterial;
                _sampleTransform = best.transform;
                _combinedBounds = best.bounds;

                for (int i = 0; i < _renderers.Length; i++)
                {
                    var r = _renderers[i];
                    if (r == null || r == best) continue;
                    _combinedBounds.Encapsulate(r.bounds);
                }

                DeterminePlaneAxes(best);
            }
            else
            {
                _sampleTransform = transform;
                _combinedBounds = new Bounds(transform.position, Vector3.one * 0.5f);
                _sampleRight = transform.right;
                _sampleUp = transform.up;
                _sampleNormal = invertNormal ? -transform.forward : transform.forward;
                _planeWidth = 0.5f;
                _planeHeight = 0.5f;
            }
        }

        Renderer FindBestRenderer()
        {
            if (_renderers == null || _renderers.Length == 0)
                return null;

            if (!string.IsNullOrEmpty(preferredRendererName))
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    var r = _renderers[i];
                    if (r != null && r.name == preferredRendererName)
                        return r;
                }
            }

            Renderer best = null;
            float bestArea = -1f;

            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;

                Vector3 s = r.bounds.size;
                float a = s.x * s.y + s.x * s.z + s.y * s.z;
                if (a > bestArea)
                {
                    bestArea = a;
                    best = r;
                }
            }

            return best;
        }

        void DeterminePlaneAxes(Renderer r)
        {
            Transform t = r.transform;
            MeshFilter mf = r.GetComponent<MeshFilter>();

            _sampleRight = t.right;
            _sampleUp = t.up;
            _sampleNormal = invertNormal ? -t.forward : t.forward;

            if (mf != null && mf.sharedMesh != null)
            {
                Vector3 size = mf.sharedMesh.bounds.size;
                Vector3 lossy = t.lossyScale;

                float sx = Mathf.Abs(size.x * lossy.x);
                float sy = Mathf.Abs(size.y * lossy.y);
                float sz = Mathf.Abs(size.z * lossy.z);

                if (sx <= sy && sx <= sz)
                {
                    _sampleNormal = invertNormal ? -t.right : t.right;
                    _sampleRight = t.forward;
                    _sampleUp = t.up;
                    _planeWidth = Mathf.Max(0.05f, sz);
                    _planeHeight = Mathf.Max(0.05f, sy);
                    return;
                }
                if (sy <= sx && sy <= sz)
                {
                    _sampleNormal = invertNormal ? -t.up : t.up;
                    _sampleRight = t.right;
                    _sampleUp = t.forward;
                    _planeWidth = Mathf.Max(0.05f, sx);
                    _planeHeight = Mathf.Max(0.05f, sz);
                    return;
                }

                _sampleNormal = invertNormal ? -t.forward : t.forward;
                _sampleRight = t.right;
                _sampleUp = t.up;
                _planeWidth = Mathf.Max(0.05f, sx);
                _planeHeight = Mathf.Max(0.05f, sy);
                return;
            }

            Vector3 b = _combinedBounds.size;
            _planeWidth = Mathf.Max(0.05f, b.x);
            _planeHeight = Mathf.Max(0.05f, b.y);
        }

        void HideOriginalGlass()
        {
            if (disableOriginalRenderers && _renderers != null)
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] != null)
                        _renderers[i].enabled = false;
                }
            }

            if (disableOriginalColliders && _colliders != null)
            {
                for (int i = 0; i < _colliders.Length; i++)
                {
                    if (_colliders[i] != null)
                        _colliders[i].enabled = false;
                }
            }

            if (disableOriginalPickups && _monoBehaviours != null)
            {
                for (int i = 0; i < _monoBehaviours.Length; i++)
                {
                    var mb = _monoBehaviours[i];
                    if (mb == null) continue;
                    if (mb == this) continue;
                    mb.enabled = false;
                }
            }
        }

        void SpawnShards()
        {
            if (_shardRoot != null)
                Destroy(_shardRoot);

            _shardRoot = new GameObject(name + "_GlassShards");

            if (spawnIntoWorldSpace)
            {
                _shardRoot.transform.position = Vector3.zero;
                _shardRoot.transform.rotation = Quaternion.identity;
            }
            else
            {
                _shardRoot.transform.SetParent(transform, false);
            }

            Vector3 center = _combinedBounds.center;
            int count = Mathf.Max(3, shardCount);

            for (int i = 0; i < count; i++)
            {
                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Quad);
                shard.name = "GlassShard_" + i;

                if (spawnIntoWorldSpace)
                {
                    shard.transform.SetParent(_shardRoot.transform, true);
                }
                else
                {
                    shard.transform.SetParent(_shardRoot.transform, false);
                }

                float px = UnityEngine.Random.Range(-0.5f, 0.5f) * _planeWidth;
                float py = UnityEngine.Random.Range(-0.5f, 0.5f) * _planeHeight;

                Vector3 spawnPos = center
                    + _sampleRight * px
                    + _sampleUp * py
                    + _sampleNormal * spawnSurfaceOffset;

                shard.transform.position = spawnPos;

                Quaternion baseRot = Quaternion.LookRotation(_sampleNormal, _sampleUp);
                Quaternion randomRot = Quaternion.Euler(
                    UnityEngine.Random.Range(-25f, 25f),
                    UnityEngine.Random.Range(-25f, 25f),
                    UnityEngine.Random.Range(0f, 360f));
                shard.transform.rotation = baseRot * randomRot;

                float size = UnityEngine.Random.Range(shardSizeMin, shardSizeMax);
                float width = size * UnityEngine.Random.Range(0.55f, 1.15f);
                float height = size * UnityEngine.Random.Range(0.55f, 1.15f);
                shard.transform.localScale = new Vector3(width, height, 1f);

                var col = shard.GetComponent<Collider>();
                if (col != null) Destroy(col);

                BoxCollider bc = shard.AddComponent<BoxCollider>();
                bc.size = new Vector3(1f, 1f, Mathf.Max(0.001f, shardThickness / Mathf.Max(0.0001f, size)));

                MeshRenderer mr = shard.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    if (_sourceMaterial != null)
                    {
                        Material inst = new Material(_sourceMaterial);
                        inst.name = "GlassShardMat_" + i;
                        mr.material = inst;
                    }

                    if (disableShardShadows)
                    {
                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        mr.receiveShadows = false;
                    }
                }

                Rigidbody rb = shard.AddComponent<Rigidbody>();
                rb.mass = 0.035f;
                rb.drag = 0.08f;
                rb.angularDrag = 0.05f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                Vector3 fromCenter = (spawnPos - center);
                if (fromCenter.sqrMagnitude < 0.0001f)
                    fromCenter = (_sampleRight * UnityEngine.Random.Range(-1f, 1f)) + (_sampleUp * UnityEngine.Random.Range(-1f, 1f));
                fromCenter.Normalize();

                Vector3 launchDir = (_sampleNormal * UnityEngine.Random.Range(burstForceMin, burstForceMax))
                    + (fromCenter * sidewaysForce)
                    + (Vector3.up * upwardForce * UnityEngine.Random.Range(0.35f, 1.1f));

                rb.velocity = launchDir;

                Vector3 torqueAxis = new Vector3(
                    UnityEngine.Random.Range(-1f, 1f),
                    UnityEngine.Random.Range(-1f, 1f),
                    UnityEngine.Random.Range(-1f, 1f)).normalized;
                if (torqueAxis.sqrMagnitude < 0.0001f) torqueAxis = Vector3.up;

                rb.AddTorque(
                    torqueAxis * UnityEngine.Random.Range(torqueMin, torqueMax),
                    ForceMode.Acceleration);

                float life = UnityEngine.Random.Range(shardLifetimeMin, shardLifetimeMax);
                if (fadeBeforeDestroy)
                    MelonCoroutines.Start(Co_FadeAndDestroy(shard, mr, life));
                else
                    Destroy(shard, life);
            }

            Destroy(_shardRoot, shardLifetimeMax + 0.5f);
        }

        [HideFromIl2Cpp]
        IEnumerator Co_FadeAndDestroy(GameObject go, MeshRenderer mr, float totalLife)
        {
            if (go == null) yield break;

            float hold = Mathf.Max(0f, totalLife - fadeDuration);
            if (hold > 0f)
                yield return new WaitForSeconds(hold);

            if (mr == null)
            {
                Destroy(go);
                yield break;
            }

            Material mat = null;
            try { mat = mr.material; } catch { }

            Color c0 = Color.white;
            bool hasColor = false;
            if (mat != null)
            {
                if (mat.HasProperty("_Color"))
                {
                    c0 = mat.GetColor("_Color");
                    hasColor = true;
                }
                else if (mat.HasProperty("_BaseColor"))
                {
                    c0 = mat.GetColor("_BaseColor");
                    hasColor = true;
                }
            }

            float t = 0f;
            float dur = Mathf.Max(0.01f, fadeDuration);
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / dur);

                if (mat != null && hasColor)
                {
                    Color c = c0;
                    c.a = c0.a * k;

                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                }

                yield return null;
            }

            Destroy(go);
        }
    }
}
