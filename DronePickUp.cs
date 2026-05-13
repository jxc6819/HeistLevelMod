using MelonLoader;
using SG.Phoenix.Assets.Code.InputManagement;
using SG.Phoenix.Assets.Code.Interactables;
using System;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class DronePickUp : MonoBehaviour
    {
        public DronePickUp(IntPtr ptr) : base(ptr) { }
        public DronePickUp() : base(ClassInjector.DerivedConstructorPointer<DronePickUp>())
            => ClassInjector.DerivedConstructorBody(this);

        public PickUp pickUp;
        public Vector3 localHoldPos;
        public Quaternion localHoldRot;

        bool _waitTK;

        GameObject BotRightHand;
        GameObject BotLeftHand;

        TelekinesisHandState _tkState;
        bool _tkWasEnabled;
        bool _tkForcedOff;
        public Vector3 StaticRightHand_PosOffset = new Vector3(1f, -1.7f, 0f);
        public Vector3 StaticRightHand_RotOffsetEuler = new Vector3(0f, 0f, 90f);

        public Vector3 StaticLeftHand_PosOffset = new Vector3(1f, -1.7f, 0f);
        public Vector3 StaticLeftHand_RotOffsetEuler = new Vector3(0f, 0f, 90f);

        bool _droneHoldingMe;
        public DroneHand ownerHand;
        Rigidbody rb;

        public float DirectTkRegrabMaxDistance = 0.35f;

        Transform _preWaitParent;
        Vector3 _preWaitLocalPos;
        Quaternion _preWaitLocalRot;
        RigidbodyConstraints _preWaitConstraints;
        bool _preWaitIsKinematic;
        bool _preWaitUseGravity;
        bool _hasFrozenWaitState;

        void Start()
        {
            pickUp = GetComponent<PickUp>() ?? transform.parent.gameObject.GetComponent<PickUp>();
            BotRightHand = GameObject.Find("BotRightHand");
            BotLeftHand = GameObject.Find("BotLeftHand");
            rb = GetComponent<Rigidbody>();
        }

        void Update()
        {

            if (_droneHoldingMe) return;

            if (pickUp != null && pickUp.isHeld && !_waitTK)
            {

                if (!pickUp.enabled) return;

                isTryGrabbed();
            }
        }

        void isTryGrabbed()
        {
            MelonLogger.Msg("[DronePickUp] - isTryGrabbed");
            VRHandInput heldHand = pickUp.heldHand;
            MelonLogger.Msg("[DronePickUp] - HeldHand is null: " + (heldHand == null));

            if (heldHand == null) return;

            bool tk = heldHand.TelekinesisEnabled;
            MelonLogger.Msg("[DronePickUp] - TK is " + tk);

            if (tk)
            {
                MelonLogger.Msg("[DronePickUp] - TK");

                if (TryDirectTkRegrabClaim(heldHand))
                    return;

                FreezeWhileWaitingForDroneHand();
                ReleaseSchellHeldObject(heldHand, "RocketWaitTK");

                ForceTKOff(heldHand);

                pickUp.enabled = false;
                _waitTK = true;
            }
            else
            {
                MelonLogger.Msg("[DronePickUp] - ELSE");

                GameObject dhObj = ResolveDroneHandObjectForHeldHand(heldHand);
                if (dhObj == null)
                {
                    MelonLogger.Warning("[DronePickUp] - DirectGrab failed: matching drone hand object was null");
                    return;
                }

                DroneHand dh = dhObj.GetComponent<DroneHand>();
                if (dh != null && dh.holding == null && TryClaimOwnership(dh, "DirectGrab"))
                {

                    FreezeWhileWaitingForDroneHand();
                    ReleaseSchellHeldObject(heldHand, "DirectGrab");

                    pickUp.enabled = false;
                    dh.holding = this;
                    assumeGrabPos(dh);

                    MelonLogger.Msg("[DronePickUp] - DirectGrab claimed by " + dh.name + " after clearing Schell held object");
                }
            }
        }

        public void handEnterHB(DroneHand dh)
        {
            MelonLogger.Msg("[DronePickUp] - handEnterHb checkpoint1");
            if (_waitTK && dh != null && dh.holding == null && TryClaimOwnership(dh, "HitboxClaim"))
            {
                MelonLogger.Msg("[DronePickUp] - handEnterHb checkpoint2");
                dh.holding = this;
                assumeGrabPos(dh);
                _waitTK = false;

                MelonLogger.Msg("[DronePickUp] - handEnterHb keeping TK suppressed until release");
            }
        }

        bool TryDirectTkRegrabClaim(VRHandInput heldHand)
        {
            if (heldHand == null)
                return false;

            GameObject dhObj = ResolveDroneHandObjectForHeldHand(heldHand);
            if (dhObj == null)
                return false;

            DroneHand dh = dhObj.GetComponent<DroneHand>();
            if (dh == null || dh.holding != null)
                return false;

            float maxDist = Mathf.Max(0.01f, DirectTkRegrabMaxDistance);
            float dist = Vector3.Distance(transform.position, dh.transform.position);
            if (dist > maxDist)
            {
                MelonLogger.Msg("[DronePickUp] - TK grab is not near drone hand, keeping rocket wait path. dist=" + dist.ToString("F3"));
                return false;
            }

            if (!TryClaimOwnership(dh, "DirectTkNearRegrab"))
                return false;

            FreezeWhileWaitingForDroneHand();

            ReleaseSchellHeldObject(heldHand, "DirectTkNearRegrab");

            ForceTKOff(heldHand);

            pickUp.enabled = false;
            _waitTK = false;
            dh.holding = this;
            assumeGrabPos(dh);

            MelonLogger.Msg("[DronePickUp] - DirectTkNearRegrab claimed by " + dh.name + " dist=" + dist.ToString("F3"));
            return true;
        }

        GameObject ResolveDroneHandObjectForHeldHand(VRHandInput heldHand)
        {
            if (heldHand == null || heldHand.gameObject == null)
                return null;

            string heldName = heldHand.gameObject.name.ToLower();
            if (heldName.Contains("right"))
            {
                if (BotRightHand == null) BotRightHand = GameObject.Find("BotRightHand");
                return BotRightHand;
            }

            if (BotLeftHand == null) BotLeftHand = GameObject.Find("BotLeftHand");
            return BotLeftHand;
        }

        void ReleaseSchellHeldObject(VRHandInput heldHand, string reason)
        {
            if (heldHand == null)
                return;

            try
            {
                heldHand.ReleaseHeldObject();
                MelonLogger.Msg("[DronePickUp] - " + reason + " called ReleaseHeldObject on " + heldHand.gameObject.name);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[DronePickUp] - " + reason + " ReleaseHeldObject failed: " + e.Message);
            }
        }

        bool TryClaimOwnership(DroneHand dh, string reason)
        {
            if (dh == null)
                return false;

            if (ownerHand != null && ownerHand != dh)
            {
                MelonLogger.Warning("[DronePickUp] Rejecting owner transfer on '" + name + "'. owner=" + ownerHand.name + " claimant=" + dh.name + " reason=" + reason);
                return false;
            }

            ownerHand = dh;
            _droneHoldingMe = true;
            return true;
        }

        void assumeGrabPos(DroneHand dh)
        {
            GameObject hand = dh.gameObject;

            _hasFrozenWaitState = false;
            transform.position = hand.transform.position;
            transform.parent = hand.transform;

            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.constraints = RigidbodyConstraints.FreezeAll;
            }
            else
            {
                GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
            }

            transform.localRotation = localHoldRot;
            transform.localPosition = localHoldPos;
        }

        public void isReleased(DroneHand releasingHand)
        {
            if (releasingHand != null && ownerHand != null && ownerHand != releasingHand)
            {
                MelonLogger.Warning("[DronePickUp] Rejecting release from non-owner hand on '" + name + "'. owner=" + ownerHand.name + " releaser=" + releasingHand.name);
                return;
            }

            MelonLogger.Msg("[DronePickUp] - release restoring TK");
            RestoreTK();

            ownerHand = null;
            _droneHoldingMe = false;
            _waitTK = false;
            _hasFrozenWaitState = false;

            pickUp.enabled = true;
            transform.parent = null;

            if (rb != null)
            {
                rb.constraints = RigidbodyConstraints.None;
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        public void isReleased()
        {
            isReleased(null);
        }

        void FreezeWhileWaitingForDroneHand()
        {
            if (rb == null) return;
            if (_hasFrozenWaitState) return;

            _preWaitParent = transform.parent;
            _preWaitLocalPos = transform.localPosition;
            _preWaitLocalRot = transform.localRotation;
            _preWaitConstraints = rb.constraints;
            _preWaitIsKinematic = rb.isKinematic;
            _preWaitUseGravity = rb.useGravity;
            _hasFrozenWaitState = true;

            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        void ForceTKOff(VRHandInput heldHand)
        {
            if (_tkForcedOff) return;

            _tkState = heldHand.gameObject.GetComponent<TelekinesisHandState>();
            if (_tkState == null) return;

            _tkWasEnabled = _tkState.enabled;
            _tkState.enabled = false;
            _tkForcedOff = true;
            MelonLogger.Msg("[DronePickUp] - ForceTKOff applied. tkWasEnabled=" + _tkWasEnabled + " hand=" + heldHand.gameObject.name);
        }

        void RestoreTK()
        {
            if (!_tkForcedOff) return;

            if (_tkState != null)
            {
                _tkState.enabled = _tkWasEnabled;
                MelonLogger.Msg("[DronePickUp] - RestoreTK applied. restoredEnabled=" + _tkWasEnabled);
            }
            else
            {
                MelonLogger.Warning("[DronePickUp] - RestoreTK had no tkState ref");
            }

            _tkState = null;
            _tkForcedOff = false;
        }

        public void SetLocals(Vector3 holdPos, Vector3 holdRot)
        {
            localHoldPos = holdPos;
            localHoldRot = Quaternion.Euler(holdRot);
        }

        public void SetLocals(Vector3 holdPos, Vector3 holdRot, Vector3 staticPos, Vector3 staticRot)
        {
            localHoldPos = holdPos;
            localHoldRot = Quaternion.Euler(holdRot);

            StaticRightHand_PosOffset = staticPos;
            StaticLeftHand_PosOffset = staticPos;
            StaticRightHand_RotOffsetEuler = staticRot;
            StaticLeftHand_RotOffsetEuler = staticRot;

        }
    }

    public class DronePickUpHitbox : MonoBehaviour
    {
        public DronePickUpHitbox(IntPtr ptr) : base(ptr) { }
        public DronePickUpHitbox() : base(ClassInjector.DerivedConstructorPointer<DronePickUpHitbox>())
            => ClassInjector.DerivedConstructorBody(this);

        public DronePickUp dpu;

        void OnTriggerEnter(Collider col)
        {
            DroneHand dh = col.gameObject.GetComponent<DroneHand>();
            if (dh != null)
            {
                MelonLogger.Msg("[DronePickUpHitbox] - DH trigger enter");
                if (dh._launching)
                {
                    dpu.handEnterHB(dh);
                }
            }
        }
    }
}
