using System;
using SG.Phoenix.Assets.Code.Interactables;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class HeadsetPackedState : MonoBehaviour
    {
        public HeadsetPackedState(IntPtr ptr) : base(ptr) { }
        public HeadsetPackedState() : base(ClassInjector.DerivedConstructorPointer<HeadsetPackedState>())
            => ClassInjector.DerivedConstructorBody(this);

        public Transform anchor;

        PickUp pickUp;
        Rigidbody rb;
        Collider[] colliders;

        bool suitcaseOpened;
        bool released;

        void Start()
        {
            pickUp = GetComponent<PickUp>();
            rb = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>(true);

            LockInSuitcase();
        }

        void Update()
        {
            if (released)
                return;

            if (!suitcaseOpened)
            {
                LockInSuitcase();
                FollowAnchor();
                return;
            }

            HoldInOpenSuitcase();

            if (pickUp.isHeld)
            {
                FinishRelease();
                return;
            }

            FollowAnchor();
        }

        void FollowAnchor()
        {
            transform.position = anchor.position;
            transform.rotation = anchor.rotation;

            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        void LockInSuitcase()
        {
            pickUp.enabled = false;
            SetColliders(false);

            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        void HoldInOpenSuitcase()
        {
            SetColliders(true);
            pickUp.enabled = true;

            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        void SetColliders(bool on)
        {
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = on;
        }

        public void ReleaseFromPackedMode()
        {
            if (released)
                return;

            suitcaseOpened = true;
            HoldInOpenSuitcase();
        }

        void FinishRelease()
        {
            released = true;

            transform.SetParent(null, true);
            SetColliders(true);

            pickUp.enabled = true;

            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = false;
            rb.useGravity = false;
        }
    }
}
