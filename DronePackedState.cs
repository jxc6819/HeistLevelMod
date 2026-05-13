using System;
using SG.Phoenix.Assets.Code.Interactables;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class DronePackedState : MonoBehaviour
    {
        public DronePackedState(IntPtr ptr) : base(ptr) { }
        public DronePackedState() : base(ClassInjector.DerivedConstructorPointer<DronePackedState>())
            => ClassInjector.DerivedConstructorBody(this);

        public Transform anchor;
        public GameObject suitcaseRoot;
        public GameObject suitcaseBottom;

        PickUp _pickUp;
        Rigidbody _rb;
        bool _releasedFromPacked;

        void Start()
        {
            _pickUp = GetComponent<PickUp>();
            _rb = GetComponent<Rigidbody>();
        }

        void Update()
        {
            if (_releasedFromPacked)
                return;

            if (_pickUp == null || _rb == null || anchor == null)
                return;

            if (_pickUp.isHeld)
            {
                ReleaseFromPackedMode();
                return;
            }

            transform.position = anchor.position;
            transform.rotation = anchor.rotation;

            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
            _rb.useGravity = false;
        }

        void ReleaseFromPackedMode()
        {
            _releasedFromPacked = true;

            transform.SetParent(null, true);

            if (_rb != null)
            {
                _rb.velocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.isKinematic = false;
                _rb.useGravity = false;
            }

            Collider[] cols = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null)
                    cols[i].enabled = true;
            }

            DroneDriver dd = GetComponent<DroneDriver>();
            if (dd != null) dd.enabled = true;
        }
    }
}
