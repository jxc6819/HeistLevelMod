using System;
using UnhollowerRuntimeLib;
using UnityEngine;
using UnhollowerBaseLib.Attributes;
using MelonLoader;

namespace IEYTD_Mod2Code
{
    public class PickUpPile : MonoBehaviour
    {
        public PickUpPile(IntPtr ptr) : base(ptr) { }
        public PickUpPile() : base(ClassInjector.DerivedConstructorPointer<PickUpPile>())
            => ClassInjector.DerivedConstructorBody(this);

        PickUp pu;
        GameObject pileObj;
        GameObject individualObj;

        bool grabbed = false;

        public bool Regenerative;
        GameObject previousBill;
        bool isMoney;

        public void Init(GameObject individual, bool Regenerative = true, bool isMoney = true, GameObject previousBill = null)
        {
            pu = GetComponent<PickUp>();
            pileObj = transform.GetChild(0).gameObject;

            this.individualObj = Instantiate(individual);
            this.Regenerative = Regenerative;
            this.previousBill = previousBill;
            this.isMoney = isMoney;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
                rb.isKinematic = true;

            if (previousBill != null)
            {
                BoxCollider pileCol = pileObj.GetComponent<BoxCollider>();
                BoxCollider prevCol = previousBill.GetComponent<BoxCollider>();
                if (pileCol != null && prevCol != null)
                    Physics.IgnoreCollision(pileCol, prevCol);
            }

            individualObj.SetActive(false);
        }

        void Update()
        {
            if (!grabbed && pu != null && pu.isHeld)
            {
                grabbed = true;
                PileGrabbed();
            }
        }

        void PileGrabbed()
        {
            if (pileObj == null || individualObj == null)
                return;

            individualObj.SetActive(true);

            if (Regenerative)
            {
                GameObject clone = Instantiate(this.gameObject);
                GameObject individualClone = Instantiate(individualObj);
                clone.GetComponent<PickUpPile>().Init(individualClone, true, isMoney, pileObj);
            }

            Transform marker = transform.childCount > 1 ? transform.GetChild(1) : null;
            Vector3 targetPos = marker != null ? marker.position : pileObj.transform.position;
            Quaternion targetRot = marker != null ? marker.rotation : individualObj.transform.rotation;

            individualObj.transform.position = targetPos;
            individualObj.transform.rotation = targetRot;

            pileObj.transform.position = targetPos;
            pileObj.transform.rotation = targetRot;

            individualObj.transform.SetParent(pileObj.transform, true);

            CopyColliderWorldShape(individualObj, pileObj);

            BoxCollider individualCol = individualObj.GetComponent<BoxCollider>();
            if (individualCol != null)
                individualCol.enabled = false;

            MeshRenderer pileRenderer = pileObj.GetComponent<MeshRenderer>();
            if (pileRenderer != null)
                pileRenderer.enabled = false;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.constraints = RigidbodyConstraints.None;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (isMoney)
            {
                MelonCoroutines.Start(SouvenirFoundDelay());
                AudioUtil.PlayAt("CashGrab.wav", transform.position);
            }
            else
            {
                this.gameObject.name = "PaperCrumpleTransformed";
                AudioUtil.PlayAt("PaperCrunch.wav", transform.position);
            }
        }

        [HideFromIl2Cpp]
        System.Collections.IEnumerator SouvenirFoundDelay()
        {
            yield return new WaitForSeconds(0.4f);
            if (!SaveManager.HasSouvenir(4))
            {
                HeistLevelManager.FoundSouvenir(4);
            }
        }

        void CopyColliderWorldShape(GameObject sourceObject, GameObject targetObject)
        {
            BoxCollider sourceCol = sourceObject.GetComponent<BoxCollider>();
            BoxCollider targetCol = targetObject.GetComponent<BoxCollider>();

            if (sourceCol == null || targetCol == null)
                return;

            Transform sourceT = sourceCol.transform;
            Transform targetT = targetCol.transform;

            Vector3 sourceWorldCenter = sourceT.TransformPoint(sourceCol.center);
            targetCol.center = targetT.InverseTransformPoint(sourceWorldCenter);

            Vector3 sourceScale = sourceT.lossyScale;
            Vector3 targetScale = targetT.lossyScale;

            float sx = Mathf.Abs(targetScale.x) < 0.0001f ? 1f : Mathf.Abs(sourceScale.x / targetScale.x);
            float sy = Mathf.Abs(targetScale.y) < 0.0001f ? 1f : Mathf.Abs(sourceScale.y / targetScale.y);
            float sz = Mathf.Abs(targetScale.z) < 0.0001f ? 1f : Mathf.Abs(sourceScale.z / targetScale.z);

            targetCol.size = new Vector3(
                Mathf.Abs(sourceCol.size.x * sx),
                Mathf.Abs(sourceCol.size.y * sy),
                Mathf.Abs(sourceCol.size.z * sz)
            );

            targetCol.isTrigger = sourceCol.isTrigger;
            targetCol.enabled = true;
        }
    }
}
