using MelonLoader;
using System;
using System.Collections.Generic;
using UnhollowerRuntimeLib;
using UnityEngine;
using SG.Phoenix.Assets.Code.Interactables;

namespace IEYTD_Mod2Code
{
    public class TrashTrigger : MonoBehaviour
    {
        public TrashTrigger(IntPtr ptr) : base(ptr) { }
        public TrashTrigger() : base(ClassInjector.DerivedConstructorPointer<TrashTrigger>())
            => ClassInjector.DerivedConstructorBody(this);

        public int num = -1;

        BoxCollider trashCollider;

        bool waitingForRelease = false;
        PickUp paperPickup;

        public void Init(int num)
        {
            this.num = num;
        }
        void Start()
        {
            trashCollider = transform.parent.GetComponent<BoxCollider>();
        }

        void Update()
        {
            if (!waitingForRelease) return;
            if (paperPickup == null) return;
            if(waitingForRelease && (!paperPickup.isHeld))
            {
                TriggerSouvenir();
                waitingForRelease = false;
                paperPickup = null;
            }
        }
        public void OnTriggerEnter(Collider other)
        {

            if(other.gameObject.name.Contains("PaperCrumple"))
            {
                MelonLogger.Msg($"OnTriggerEnter from Paper - {this.gameObject.name}");
                PaperEnter(other.gameObject);
            }
            else if(other.gameObject.transform.parent.gameObject.name.Contains("PaperCrumple"))
            {
                MelonLogger.Msg($"OnTriggerEnter from Paper - {this.gameObject.name}");
                PaperEnter(other.gameObject.transform.parent.gameObject);
            }
            else
            {
                MelonLogger.Msg($"OnTriggerEnter from Other: {other.gameObject.name} - {this.gameObject.name}");
            }

        }

        public void OnTriggerExit(Collider other)
        {
            if(other.gameObject.name.Contains("PaperCrumple"))
            {
                PaperExit(other.gameObject);
            }
            else if (other.gameObject.transform.parent.gameObject.name.Contains("PaperCrumple"))
            {

                PaperEnter(other.gameObject.transform.parent.gameObject);
            }
        }

        void PaperEnter(GameObject other)
        {

            BoxCollider paperCol = other.gameObject.GetComponent<BoxCollider>();
            if (paperCol == null) paperCol = other.gameObject.transform.GetChild(0).gameObject.GetComponent<BoxCollider>();
            if(paperCol == null)
            {
                MelonLogger.Warning("paper col is null");
                return;
            }
            MelonLogger.Msg($"[TrashTrigger] - Other is {other.gameObject.name}");

            if(num == 0)
            {
                Physics.IgnoreCollision(paperCol, trashCollider);
            }
            else if(num == 1)
            {
                GameObject paper = other.gameObject;
                PickUp pu = paper.GetComponent<PickUp>();
                if(pu.isHeld) pu.ReleaseFromCurrentHand();
                Rigidbody rb = paper.GetComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.constraints = RigidbodyConstraints.FreezeAll;
                paper.transform.parent = trashCollider.gameObject.transform;
                paper.transform.localPosition = new Vector3(0, 0.1f, 0);
                paper.transform.GetChild(0).localPosition = Vector3.zero;
                pu.enabled = false;
                TriggerSouvenir();
            }
            else if(num == 3)
            {
                waitingForRelease = true;
                paperPickup = other.gameObject.GetComponent<PickUp>();
            }
        }

        void PaperExit(GameObject other)
        {
            if(num == 2)
            {
                BoxCollider paperCol = other.gameObject.GetComponent<BoxCollider>();
                Physics.IgnoreCollision(paperCol, trashCollider, false);
            }
            else if(num == 3)
            {
                waitingForRelease = false;
                paperPickup = null;
            }
        }

        void TriggerSouvenir()
        {

            if (SaveManager.HasSouvenir(2)) return;
            HeistLevelManager.FoundSouvenir(2);

        }

    }
}
