using System;
using System.Collections.Generic;
using UnhollowerRuntimeLib;
using UnityEngine;
using SG.Phoenix.Assets.Code.Interactables;
using UnhollowerBaseLib.Attributes;
using MelonLoader;

namespace IEYTD_Mod2Code
{
    public class WrenchStartPose : MonoBehaviour
    {
        public WrenchStartPose(IntPtr ptr) : base(ptr) { }
        public WrenchStartPose() : base(ClassInjector.DerivedConstructorPointer<WrenchStartPose>())
            => ClassInjector.DerivedConstructorBody(this);

        PickUp pickup;
        Rigidbody body;
        bool isGrabbed = false;
        GameObject bucket;
        BoxCollider wrenchCollider;
        MeshCollider bucketCollider;

        Vector3 normalColliderSize;
        Vector3 normalColliderCenter;
        bool savedNormalCollider = false;

        public void Start()
        {
            pickup = GetComponent<PickUp>();
            body = GetComponent<Rigidbody>();
            bucket = GameObject.Find("Bucket_Yellow_LoD_1");

            wrenchCollider = transform.GetChild(0).gameObject.GetComponent<BoxCollider>();

            if (bucket != null)
                bucketCollider = bucket.GetComponent<MeshCollider>();

            if (wrenchCollider != null)
            {
                normalColliderSize = wrenchCollider.size;
                normalColliderCenter = wrenchCollider.center;
                savedNormalCollider = true;

                wrenchCollider.size = normalColliderSize * 2f;
                wrenchCollider.center = normalColliderCenter;
            }

            if (body != null)
            {
                body.isKinematic = true;
                body.constraints = RigidbodyConstraints.FreezeAll;
            }

            if (bucketCollider != null)
                bucketCollider.enabled = false;
        }

        void Update()
        {
            if (!isGrabbed && pickup != null && pickup.isHeld)
            {
                isGrabbed = true;
                Grabbed();
            }
        }

        void Grabbed()
        {
            RestoreNormalColliderSize();

            if (body != null)
            {
                body.constraints = RigidbodyConstraints.None;
                body.isKinematic = false;
            }

            MelonCoroutines.Start(ActivateCollision());
        }

        void RestoreNormalColliderSize()
        {
            if (wrenchCollider == null || !savedNormalCollider) return;

            wrenchCollider.size = normalColliderSize;
            wrenchCollider.center = normalColliderCenter;
        }

        [HideFromIl2Cpp]
        System.Collections.IEnumerator ActivateCollision()
        {
            yield return new WaitForSeconds(2);

            if (bucketCollider != null)
                bucketCollider.enabled = true;

            yield return new WaitForSeconds(1);
            Kill();
        }

        void Kill()
        {
            this.enabled = false;
        }
    }
}
