using System;
using System.Collections.Generic;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class LaserColumnSpawner : MonoBehaviour
    {
        public LaserColumnSpawner(IntPtr ptr) : base(ptr) { }
        public LaserColumnSpawner() : base(ClassInjector.DerivedConstructorPointer<LaserColumnSpawner>())
            => ClassInjector.DerivedConstructorBody(this);

        public List<Transform> columnAnchors = new List<Transform>();

        public int lasersPerColumn = 14;

        public Vector2 yRange = new Vector2(0.16f, 7.15f);

        public float localZHalfWidth = 0.16f;
        public float localXHalfWidth = 0.04f;

        public float maxDistance = 25f;
        public LayerMask hitMask = ~0;
        public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        public bool generateOnStart = true;

        public string childFolderName = "Spawned_Lasers";

        public string laserFinalCheckpointName = "LaserFinalCheckpoint";
        public string laserMidCheckpointName = "LaserMidCheckpoint";

        public List<LaserEmitter> emitters = new List<LaserEmitter>();

        public float playerSafeRadiusXZ = 0.90f;
        public float playerSpawnSafeRadiusXZ = 1.05f;
        public float playerSafeHeightY = 3.10f;
        public float lowLaserHeightCutoff = 2.65f;
        public float outwardNudgeDistance = 0.28f;

        static readonly float[] HeightPattern = new float[]
        {
            0.00f, 0.03f, 0.07f, 0.11f, 0.18f, 0.26f, 0.35f,
            0.46f, 0.57f, 0.68f, 0.78f, 0.87f, 0.94f, 0.99f
        };

        static readonly float[] LocalZPattern = new float[]
        {
            -0.98f,  0.98f, -0.82f,  0.82f, -0.62f,  0.62f, -0.40f,
             0.40f, -0.18f,  0.18f, -0.74f,  0.74f, -0.30f,  0.30f
        };

        static readonly float[] LocalXPattern = new float[]
        {
            -0.55f,  0.55f, -0.38f,  0.38f, -0.22f,  0.22f, -0.12f,
             0.12f,  0.28f, -0.28f,  0.08f, -0.08f,  0.18f, -0.18f
        };

        static readonly float[] PitchPattern = new float[]
        {
             4f,  -4f,   6f,  -6f,   8f, -10f,  12f,
           -14f,  16f, -18f,  20f, -22f,  16f, -18f
        };

        static readonly float[] YawPatternA = new float[]
        {
            -80f,  80f, -68f,  68f, -54f,  54f, -40f,
             40f, -22f,  22f, -60f,  60f, -32f,  32f
        };

        static readonly float[] YawPatternB = new float[]
        {
             80f, -80f,  68f, -68f,  54f, -54f,  40f,
            -40f,  22f, -22f,  60f, -60f,  32f, -32f
        };

        void Start()
        {
            GameObject laserParent = GameObject.Find("Lasers");
            if (!laserParent) return;

            int count = laserParent.transform.childCount;
            List<Transform> lasers = new List<Transform>();

            for (int i = 0; i < count; i++)
            {
                Transform child = laserParent.transform.GetChild(i);
                if (ShouldSkipAnchor(child))
                    continue;

                lasers.Add(child);
            }

            columnAnchors = lasers;

            if (generateOnStart)
                Generate();
        }

        public void Generate()
        {
            if (columnAnchors == null || columnAnchors.Count == 0)
            {
                Debug.LogWarning("[LaserColumnSpawner] No columnAnchors assigned.");
                return;
            }

            emitters.Clear();

            for (int i = 0; i < columnAnchors.Count; i++)
            {
                Transform anchor = columnAnchors[i];
                if (!anchor)
                    continue;

                if (ShouldSkipAnchor(anchor))
                {
                    Transform oldFolder = anchor.Find(childFolderName);
                    if (oldFolder)
                        ClearChildren(oldFolder);

                    Debug.Log("[LaserColumnSpawner] Skipping laser spawn for checkpoint anchor '" + anchor.name + "'.");
                    continue;
                }

                Transform folder = GetOrCreateFolder(anchor);
                ClearChildren(folder);

                for (int n = 0; n < lasersPerColumn; n++)
                    CreateLaser(anchor, folder, i, n);

                Debug.Log("[LaserColumnSpawner] Spawned " + lasersPerColumn + "/" + lasersPerColumn + " lasers for column '" + anchor.name + "'.");
            }
        }

        bool ShouldSkipAnchor(Transform anchor)
        {
            if (anchor == null)
                return false;

            string n = anchor.name;
            return string.Equals(n, laserFinalCheckpointName, StringComparison.Ordinal) ||
                   string.Equals(n, laserMidCheckpointName, StringComparison.Ordinal);
        }

        Transform GetOrCreateFolder(Transform anchor)
        {
            Transform folder = anchor.Find(childFolderName);
            if (folder)
                return folder;

            GameObject go = new GameObject(childFolderName);
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        void ClearChildren(Transform parent)
        {
#if UNITY_EDITOR
            for (int i = parent.childCount - 1; i >= 0; i--)
                DestroyImmediate(parent.GetChild(i).gameObject);
#else
            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
#endif
        }

        void CreateLaser(Transform anchor, Transform folder, int anchorIndex, int laserIndex)
        {
            int idx = PositiveMod(laserIndex, HeightPattern.Length);
            bool flip = (PositiveMod(anchorIndex, 2) == 1);

            float yMin = Mathf.Min(yRange.x, yRange.y);
            float yMax = Mathf.Max(yRange.x, yRange.y);
            float y = Mathf.Lerp(yMin, yMax, HeightPattern[idx]);

            float localZ = LocalZPattern[idx] * Mathf.Max(0f, localZHalfWidth);
            float localX = LocalXPattern[idx] * Mathf.Max(0f, localXHalfWidth);

            if (flip)
            {
                localZ = -localZ;
                localX = -localX;
            }

            float yaw = flip ? YawPatternB[idx] : YawPatternA[idx];
            float pitch = PitchPattern[idx];

            Vector3 localPos = new Vector3(localX, y, localZ);
            Quaternion localRot = Quaternion.Euler(pitch, yaw, 0f);

            AdjustForPlayerBreathingRoom(anchor, ref localPos, ref localRot);

            GameObject laserGo = new GameObject("Laser_" + anchor.name + "_" + laserIndex.ToString("00"));
            laserGo.transform.SetParent(folder, false);
            laserGo.transform.localPosition = localPos;
            laserGo.transform.localRotation = localRot;

            LaserEmitter emitter = laserGo.AddComponent<LaserEmitter>();
            emitter.maxDistance = maxDistance;
            emitter.hitMask = hitMask;
            emitter.triggerInteraction = triggerInteraction;
            emitter.startEnabled = true;
            emitters.Add(emitter);
        }

        void AdjustForPlayerBreathingRoom(Transform anchor, ref Vector3 localPos, ref Quaternion localRot)
        {
            if (anchor == null)
                return;

            Vector3 worldPos = anchor.TransformPoint(localPos);
            Vector3 forward = anchor.TransformDirection(localRot * Vector3.forward);

            bool lowLaser = worldPos.y < lowLaserHeightCutoff;
            float spawnDistXZ = DistanceXZ(worldPos, Vector3.zero);
            float beamDistXZ = ClosestDistanceRayToPointXZ(worldPos, forward, Vector3.zero);

            if (!lowLaser)
            {
                if (beamDistXZ < playerSafeRadiusXZ)
                    localPos.y = Mathf.Max(localPos.y, playerSafeHeightY);
                return;
            }

            bool tooCloseSpawn = spawnDistXZ < playerSpawnSafeRadiusXZ;
            bool tooCloseBeam = beamDistXZ < playerSafeRadiusXZ;

            if (!tooCloseSpawn && !tooCloseBeam)
                return;

            Vector3 outwardWorld = new Vector3(worldPos.x, 0f, worldPos.z);
            if (outwardWorld.sqrMagnitude < 0.0001f)
                outwardWorld = new Vector3(forward.x, 0f, forward.z);
            if (outwardWorld.sqrMagnitude < 0.0001f)
                outwardWorld = Vector3.forward;

            outwardWorld.Normalize();
            Vector3 nudgedWorld = worldPos + outwardWorld * outwardNudgeDistance;
            Vector3 nudgedLocal = anchor.InverseTransformPoint(nudgedWorld);

            localPos.x = nudgedLocal.x;
            localPos.z = nudgedLocal.z;
            localPos.y = Mathf.Max(localPos.y, playerSafeHeightY);
        }

        static float DistanceXZ(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static float ClosestDistanceRayToPointXZ(Vector3 rayOrigin, Vector3 rayDir, Vector3 point)
        {
            Vector2 o = new Vector2(rayOrigin.x, rayOrigin.z);
            Vector2 d = new Vector2(rayDir.x, rayDir.z);
            Vector2 p = new Vector2(point.x, point.z);

            float dSqr = d.sqrMagnitude;
            if (dSqr < 0.000001f)
                return (o - p).magnitude;

            Vector2 op = o - p;
            float t = -Vector2.Dot(op, d) / dSqr;
            if (t < 0f)
                return op.magnitude;

            Vector2 closest = o + d * t;
            return (closest - p).magnitude;
        }

        static int PositiveMod(int value, int mod)
        {
            if (mod <= 0) return 0;
            int r = value % mod;
            return r < 0 ? r + mod : r;
        }
    }
}
