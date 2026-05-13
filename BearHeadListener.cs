using MelonLoader;
using SG.Phoenix.Assets.Code.Interactables;
using System;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class BearHeadListener : MonoBehaviour
    {
        public BearHeadListener(IntPtr ptr) : base(ptr) { }
        public BearHeadListener() : base(ClassInjector.DerivedConstructorPointer<BearHeadListener>())
            => ClassInjector.DerivedConstructorBody(this);

        public PickUp pickup;

        Vector3 headsetLocalPos = new Vector3(-0.122f, -0.0481f, 0.454f);
        Vector3 headsetLocalRot = new Vector3(-3.199f, 22.931f, -89.315f);

        bool mounted;
        bool wasHeld;
        bool needsTriggerExit;
        float remountBlockedUntil = -1f;
        float remountBlockSeconds = 2f;

        void Start()
        {
            pickup = GameObject.Find("PickUp_HOST_Headset").GetComponent<PickUp>();
        }

        void Update()
        {
            bool held = pickup.isHeld;

            if (!wasHeld && held)
                RemoveFromBear();

            wasHeld = held;
        }

        void RemoveFromBear()
        {
            if (!mounted)
                return;

            GameObject headset = pickup.gameObject;
            Rigidbody rb = headset.GetComponent<Rigidbody>();

            MelonLogger.Msg("[BearHeadListener] Removing headset from bear");

            if (headset.transform.parent == transform)
                headset.transform.parent = null;

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.constraints = RigidbodyConstraints.None;
            }

            mounted = false;
            remountBlockedUntil = Time.time + remountBlockSeconds;
            needsTriggerExit = true;
        }

        public void OnTriggerEnter(Collider other)
        {
            if (IsHeadset(other))
                TryMountHeadset();
        }

        public void OnTriggerStay(Collider other)
        {
            if (IsHeadset(other))
                TryMountHeadset();
        }

        public void OnTriggerExit(Collider other)
        {
            if (!IsHeadset(other))
                return;

            if (needsTriggerExit)
            {
                MelonLogger.Msg("[BearHeadListener] Headset fully exited bear trigger; remount allowed again");
                needsTriggerExit = false;
            }
        }

        bool IsHeadset(Collider other)
        {
            return other != null && other.gameObject.name.ToLower().Contains("headset");
        }

        void TryMountHeadset()
        {
            if (!pickup.isHeld || mounted)
                return;

            if (Time.time < remountBlockedUntil || needsTriggerExit)
                return;

            GameObject headset = pickup.gameObject;
            Rigidbody rb = headset.GetComponent<Rigidbody>();

            MelonLogger.Msg("[BearHeadListener] Mounting headset onto bear");

            pickup.ReleaseFromCurrentHand();

            if (rb != null)
            {
                rb.constraints = RigidbodyConstraints.FreezeAll;
                rb.isKinematic = true;
            }

            headset.transform.parent = transform;
            headset.transform.localPosition = headsetLocalPos;
            headset.transform.localRotation = Quaternion.Euler(headsetLocalRot);

            TriggerSouvenir();
            mounted = true;
        }

        void TriggerSouvenir()
        {
            if (!SaveManager.HasSouvenir(1))
                HeistLevelManager.FoundSouvenir(1);
        }
    }
}
