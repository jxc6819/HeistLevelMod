using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using UnhollowerRuntimeLib;
using UnhollowerBaseLib.Attributes;
using UnityEngine;
using SG.Phoenix.Assets.Code.Interactables;
using SG.Phoenix.Assets.Code.WorldAttributes;

namespace IEYTD_Mod2Code
{
    public class HiddenVolumeController : MonoBehaviour
    {
        public HiddenVolumeController(IntPtr ptr) : base(ptr) { }
        public HiddenVolumeController() : base(ClassInjector.DerivedConstructorPointer<HiddenVolumeController>())
            => ClassInjector.DerivedConstructorBody(this);

        public readonly List<Collider> startingColliders = new List<Collider>();

        public GameObject drone;
        public PickUp dronePickup;
        public Transform hmd;
        public Behaviour hiddenVolume;

        public float triggerConfirmTime = 0.5f;
        public float visibilityCheckInterval = 0.2f;

        public string[] startupExclusionNameContains = new string[0];
        bool systemArmed = false;
        bool droneInsideConfirmed = false;
        bool rawDroneInside = false;
        bool wasHeldLastFrame = false;

        object visibilityRoutine;
        object triggerStateRoutine;
        BoxCollider col;

        void Start()
        {
            BoxCollider trigger = GetComponent<BoxCollider>();
            if (trigger == null) return;

            trigger.isTrigger = true;

            hiddenVolume = GetComponent<HiddenVolume>();
            drone = GameObject.Find("PickUp_HOST_Drone");
            dronePickup = drone.GetComponent<PickUp>();
            col = GetComponent<BoxCollider>();

            if (hmd == null)
            {
                GameObject hmdObj = GameObject.Find("HMD");
                if (hmdObj != null) hmd = hmdObj.transform;
            }
            if (hiddenVolume == null) hiddenVolume = gameObject.GetComponent<HiddenVolume>();

            if (col != null)
                col.enabled = false;

            CacheStartingColliders(trigger);
        }

        void Update()
        {
            if (dronePickup == null) return;

            bool held = dronePickup.isHeld;

            if (!systemArmed && held)
            {
                systemArmed = true;
                MelonLogger.Msg("[HiddenVolumeController] System armed for " + name);
            }

            wasHeldLastFrame = held;
        }

        void CacheStartingColliders(BoxCollider trigger)
        {
            Vector3 worldCenter = transform.TransformPoint(trigger.center);
            Vector3 scaledSize = Vector3.Scale(trigger.size, transform.lossyScale);
            Vector3 halfExtents = new Vector3(
                Mathf.Abs(scaledSize.x) * 0.5f,
                Mathf.Abs(scaledSize.y) * 0.5f,
                Mathf.Abs(scaledSize.z) * 0.5f
            );

            Collider[] hits = Physics.OverlapBox(
                worldCenter,
                halfExtents,
                transform.rotation,
                ~0,
                QueryTriggerInteraction.Collide
            );

            startingColliders.Clear();

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null) continue;
                if (hit == trigger) continue;
                if (!ShouldIncludeInStartupList(hit.gameObject)) continue;
                if (startingColliders.Contains(hit)) continue;

                startingColliders.Add(hit);
            }

            MelonLogger.Msg("[HiddenVolumeController] Cached " + startingColliders.Count + " starting colliders for " + name);
        }

        bool ShouldIncludeInStartupList(GameObject go)
        {
            if (go == null) return false;

            string n = go.name;
            if (string.IsNullOrEmpty(n)) return false;

            if (!n.StartsWith("PREF", StringComparison.OrdinalIgnoreCase))
                return false;

            if (MatchesStartupExclusion(go))
                return false;

            return true;
        }

        bool MatchesStartupExclusion(GameObject go)
        {
            if (go == null) return false;
            if (startupExclusionNameContains == null) return false;

            string n = go.name;
            if (string.IsNullOrEmpty(n)) return false;

            string lowerName = n.ToLowerInvariant();

            for (int i = 0; i < startupExclusionNameContains.Length; i++)
            {
                string s = startupExclusionNameContains[i];
                if (string.IsNullOrEmpty(s)) continue;

                if (lowerName.Contains(s.ToLowerInvariant()))
                    return true;
            }

            return false;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!systemArmed) return;
            if (!isDrone(other.gameObject)) return;

            rawDroneInside = true;
            RestartTriggerStateRoutine();
        }

        void OnTriggerExit(Collider other)
        {
            if (!systemArmed) return;
            if (!isDrone(other.gameObject)) return;

            rawDroneInside = false;
            RestartTriggerStateRoutine();
        }

        void RestartTriggerStateRoutine()
        {
            if (triggerStateRoutine != null)
                MelonCoroutines.Stop(triggerStateRoutine);

            triggerStateRoutine = MelonCoroutines.Start(Co_ResolveTriggerState());
        }

        [HideFromIl2Cpp]
        IEnumerator Co_ResolveTriggerState()
        {
            bool targetState = rawDroneInside;
            yield return new WaitForSeconds(triggerConfirmTime);

            if (rawDroneInside != targetState)
            {
                triggerStateRoutine = null;
                yield break;
            }

            if (targetState && !droneInsideConfirmed)
            {
                droneInsideConfirmed = true;
                MelonLogger.Msg("[HiddenVolumeController] Drone confirmed INSIDE " + name);

                toggleColliderLayer(true);
                StartVisibilityRoutine();
            }
            else if (!targetState && droneInsideConfirmed)
            {
                droneInsideConfirmed = false;
                MelonLogger.Msg("[HiddenVolumeController] Drone confirmed OUTSIDE " + name);

                toggleColliderLayer(false);

                if (hiddenVolume != null)
                    col.enabled = false;

                StopVisibilityRoutine();
            }

            triggerStateRoutine = null;
        }

        void StartVisibilityRoutine()
        {
            if (visibilityRoutine != null)
                MelonCoroutines.Stop(visibilityRoutine);

            visibilityRoutine = MelonCoroutines.Start(Co_CheckDroneVisibility());
        }

        void StopVisibilityRoutine()
        {
            if (visibilityRoutine != null)
            {
                MelonCoroutines.Stop(visibilityRoutine);
                visibilityRoutine = null;
            }
        }

        [HideFromIl2Cpp]
        IEnumerator Co_CheckDroneVisibility()
        {
            while (droneInsideConfirmed)
            {
                if (!systemArmed)
                {
                    if (hiddenVolume != null) col.enabled = false;
                    yield return new WaitForSeconds(visibilityCheckInterval);
                    continue;
                }

                if (hiddenVolume != null && drone != null && hmd != null && dronePickup != null)
                {
                    if (!dronePickup.isHeld)
                    {
                        bool obstructed = IsDroneObstructed();
                        col.enabled = obstructed;

                    }
                    else
                    {

                        col.enabled = false;

                    }
                }

                yield return new WaitForSeconds(visibilityCheckInterval);
            }

            visibilityRoutine = null;
        }

        bool IsDroneObstructed()
        {
            if (drone == null || hmd == null) return false;

            Vector3 hmdPos = hmd.position;
            Vector3 dronePos = drone.transform.position;

            Vector3 dir = dronePos - hmdPos;
            float dist = dir.magnitude;
            if (dist <= 0.0001f) return false;

            dir /= dist;

            RaycastHit[] hits = Physics.RaycastAll(
                hmdPos,
                dir,
                dist,
                ~0,
                QueryTriggerInteraction.Ignore
            );

            if (hits == null || hits.Length == 0)
                return false;

            float bestDist = float.MaxValue;
            RaycastHit bestHit = default;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null) continue;

                Transform hitTf = hit.collider.transform;
                if (drone != null && (hitTf == drone.transform || hitTf.IsChildOf(drone.transform)))
                    continue;

                if (hit.distance < bestDist)
                {
                    bestDist = hit.distance;
                    bestHit = hit;
                    found = true;
                }
            }

            if (!found)
                return false;

            return true;
        }

        void toggleColliderLayer(bool on)
        {
            int layer = 0;
            if (on) layer = 18;

            for (int i = 0; i < startingColliders.Count; i++)
            {
                Collider c = startingColliders[i];
                if (c == null) continue;
                c.gameObject.layer = layer;
            }
        }

        bool isDrone(GameObject obj)
        {
            if (obj == null) return false;
            if (obj.name.Contains("DroneTrigger")) return true;
            return false;
        }
    }
}
