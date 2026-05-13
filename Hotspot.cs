using System;
using System.Collections;
using MelonLoader;
using UnhollowerBaseLib.Attributes;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class Hotspot : MonoBehaviour
    {
        public Hotspot(IntPtr ptr) : base(ptr) { }
        public Hotspot() : base(ClassInjector.DerivedConstructorPointer<Hotspot>())
            => ClassInjector.DerivedConstructorBody(this);

        public Vector3 Position;
        public Vector3 Rotation;

        HeadsetScript headset;

        void Start()
        {
            headset = GameObject.Find("Headset").GetComponent<HeadsetScript>();
        }

        public void Init(Vector3 position, Vector3 rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!IsDroneTrigger(other.gameObject))
                return;

            headset.hotSpot = this;

            if (gameObject.name == "MaintenanceBox")
                EnterBox();
        }

        void OnTriggerExit(Collider other)
        {
            if (IsDroneTrigger(other.gameObject))
                headset.hotSpot = null;
        }

        bool IsDroneTrigger(GameObject obj)
        {
            return obj.name == "DroneTrigger";
        }

        void EnterBox()
        {
            MelonCoroutines.Start(BoxSnapDrone());
        }

        [HideFromIl2Cpp]
        IEnumerator BoxSnapDrone()
        {
            yield return new WaitForSeconds(0.5f);

            Position = new Vector3(-2f, -2.3138f, -3.55f);
            Rotation = Vector3.zero;

            GameObject droneHost = GameObject.Find("PickUp_HOST_Drone");
            droneHost.transform.position = Position;
            droneHost.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            Rigidbody rb = droneHost.GetComponent<Rigidbody>();
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeAll;

            droneHost.GetComponent<SG.Phoenix.Assets.Code.Interactables.PickUp>().ReleaseFromCurrentHand();
        }
    }
}
