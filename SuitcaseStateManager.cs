using SG.Phoenix.Assets.Code.Interactables;
using System;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class SuitcaseStateManager : MonoBehaviour
    {
        public SuitcaseStateManager(IntPtr ptr) : base(ptr) { }
        public SuitcaseStateManager() : base(ClassInjector.DerivedConstructorPointer<SuitcaseStateManager>())
            => ClassInjector.DerivedConstructorBody(this);

        PickUp pickup;
        RotationalMotion lid;
        BoxCollider closedCollider;
        GameObject drone;
        HeadsetPackedState headsetPacked;

        bool opened;
        readonly float openColliderHeight = 0.042f;

        void Start()
        {
            lid = GameObject.Find("Lid Rotation Root").GetComponent<RotationalMotion>();
            pickup = GetComponent<PickUp>();
            closedCollider = GameObject.Find("Interactable Collider (Closed)").GetComponent<BoxCollider>();
            drone = GameObject.Find("Drone");
            headsetPacked = FindHeadsetPacked();

            drone.SetActive(false);
        }

        void Update()
        {
            if (lid.enabled && pickup.enabled)
            {
                pickup.enabled = false;
                closedCollider.size = new Vector3(closedCollider.size.x, openColliderHeight, closedCollider.size.z);
                drone.SetActive(true);

                if (opened)
                    return;

                opened = true;
                headsetPacked.ReleaseFromPackedMode();
                HeistLevelManager.playStinger();
                HeistLevelManager.playHandler("Handler_ExplainDrone.wav", 1, true);
            }
            else if (!pickup.enabled)
            {
                pickup.enabled = true;
            }
        }

        HeadsetPackedState FindHeadsetPacked()
        {
            GameObject host = GameObject.Find("PickUp_HOST_Headset");
            if (host)
                return host.GetComponent<HeadsetPackedState>();

            Transform t = GameObject.Find("Headset").transform;
            while (t)
            {
                HeadsetPackedState packed = t.GetComponent<HeadsetPackedState>();
                if (packed)
                    return packed;

                t = t.parent;
            }

            return null;
        }
    }
}
