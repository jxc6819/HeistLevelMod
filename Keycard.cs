using MelonLoader;
using SG.Phoenix.Assets.Code.Interactables;
using SG.Phoenix.Assets.Code.InputManagement;
using SG.Phoenix.Assets.Code.VignetteNodes.Phoenix;
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnhollowerBaseLib.Attributes;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class Keycard : MonoBehaviour
    {
        public Keycard(IntPtr ptr) : base(ptr) { }
        public Keycard() : base(ClassInjector.DerivedConstructorPointer<Keycard>())
            => ClassInjector.DerivedConstructorBody(this);

        Vector3 cardStartPos = new Vector3(1.838f, 1.7279f, 13.4286f);
        Vector3 cardStartRot = new Vector3(-45, 0, 90);
        Vector3 cardEndPos = new Vector3(1.838f, 1.5628f, 13.2635f);

        public static float swipeTime = 0.5f;
        public static bool headsetOn;
        public static DroneHand droneHandR;
        public static DroneHand droneHandL;
        public DroneHand droneHandCurrent;

        public GameObject keycardClone;
        public static GameObject botKeycardClone;

        public Material mat;
        public MeshRenderer mr;
        public Rigidbody rb;
        PickUp pu;
        public static bool Swiped = false;

        GameObject frozenHandObj;
        VRHandInput frozenHeldHand;
        RigidbodyConstraints preSwipeConstraints = RigidbodyConstraints.None;

        public bool SuppressingVanillaTkDuringSwipe = false;
        bool normalSwipeVisualsHidden = false;
        bool swipeInProgress = false;

        public void Start()
        {
            keycardClone = Instantiate(gameObject);
            Keycard cloneK = keycardClone.GetComponent<Keycard>();
            if (cloneK != null) Destroy(cloneK);

            mr = GetComponent<MeshRenderer>();
            rb = transform.parent.GetComponent<Rigidbody>();
            mat = mr.material;
            pu = transform.parent.GetComponent<PickUp>();
            keycardClone.SetActive(false);
            keycardClone.transform.localScale = new Vector3(2, 2, 2);
            keycardClone.transform.rotation = Quaternion.Euler(cardStartRot);
            rb.constraints = RigidbodyConstraints.FreezeAll;
            Swiped = false;
        }

        public void Swipe()
        {
            MelonLogger.Msg("[Keycard] - Swipe");
            SwipeStartState();
        }

        bool held = false;
        void Update()
        {
            if (!held && pu.isHeld)
            {
                held = true;
                rb.constraints = RigidbodyConstraints.None;
            }
        }

        public void SwipeStartState()
        {
            if (swipeInProgress)
            {
                MelonLogger.Msg("[Keycard] Swipe ignored because swipe is already in progress.");
                return;
            }

            if (botKeycardClone != null)
                botKeycardClone.GetComponent<MeshRenderer>().material = mat;

            if (headsetOn)
            {

                DronePickUp dpu = transform.parent.GetComponent<DronePickUp>();
                if (droneHandR != null && droneHandR.holding == dpu) droneHandCurrent = droneHandR;
                else if (droneHandL != null && droneHandL.holding == dpu) droneHandCurrent = droneHandL;
                else MelonLogger.Msg("[Keycard] - Unknown DroneHand Holding");
            }

            if (headsetOn && droneHandCurrent != null && !droneHandCurrent._launching)
            {
                MelonCoroutines.Start(SwipeMotion(botKeycardClone, true));
            }

            else if (headsetOn && droneHandCurrent != null && droneHandCurrent._launching)
            {
                MelonCoroutines.Start(SwipeMotion(botKeycardClone, true));

            }

            else if (!headsetOn && pu != null && pu.isHeld)
            {
                FreezeCard();
                SuppressingVanillaTkDuringSwipe = true;
                MelonLogger.Msg("[Keycard] Suppressing vanilla TK during keycard swipe.");
                MelonCoroutines.Start(SwipeMotion(keycardClone, false));
            }

            else
            {
                MelonLogger.Msg("[Keycard] Unknown State Detected");
            }

            MelonLogger.Msg("[Keycard] - End StartSwipeState");

        }

        void FreezeCard()
        {
            normalSwipeVisualsHidden = true;

            frozenHandObj = null;
            frozenHeldHand = null;

            if (pu != null && pu.heldHand != null)
            {
                frozenHeldHand = pu.heldHand;
                frozenHandObj = pu.heldHand.gameObject;
                MelonLogger.Msg("[Keycard] FreezeCard cached hand WITHOUT disabling root: " +
                                (frozenHandObj != null ? frozenHandObj.name : "null"));
            }
            else
            {
                MelonLogger.Warning("[Keycard] FreezeCard could not cache heldHand. Swipe will continue anyway.");
            }

            if (mr != null)
                mr.enabled = false;

            if (rb != null)
            {
                preSwipeConstraints = rb.constraints;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.constraints = RigidbodyConstraints.FreezeAll;
            }
        }

        void UnFreezeCard()
        {
            if (!normalSwipeVisualsHidden)
                return;

            normalSwipeVisualsHidden = false;

            frozenHandObj = null;

            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.constraints = preSwipeConstraints;
            }

            if (mr != null)
                mr.enabled = true;
        }

        void StopSuppressingVanillaTk(string reason)
        {
            if (!SuppressingVanillaTkDuringSwipe)
                return;

            PlaceRealCardAtSwipeEnd();
            SyncVanillaTkTargetToCurrentCard(reason);
            SuppressingVanillaTkDuringSwipe = false;
            MelonLogger.Msg("[Keycard] Vanilla TK suppression ended: " + reason);

            frozenHeldHand = null;
        }

        void PlaceRealCardAtSwipeEnd()
        {
            try
            {
                Transform host = transform.parent != null ? transform.parent : transform;

                host.position = cardEndPos;
                host.rotation = Quaternion.Euler(cardStartRot);

                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                MelonLogger.Msg("[Keycard] Placed real card at swipe end before TK resume.");
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[Keycard] PlaceRealCardAtSwipeEnd failed err=" + e.Message);
            }
        }

        void SyncVanillaTkTargetToCurrentCard(string phase)
        {
            try
            {
                if (pu == null)
                    return;

                Vector3 pos = transform.parent != null ? transform.parent.position : transform.position;
                Quaternion rot = transform.parent != null ? transform.parent.rotation : transform.rotation;

                try { pu._targetPickUpPosition = pos; } catch { }
                try { pu._priorPosition = pos; } catch { }
                try { pu._targetPickUpRotation = rot; } catch { }
                try { pu._priorRotation = rot; } catch { }
                try { pu._onFreezePosition = pos; } catch { }
                try { pu._reelingEnabled = false; } catch { }

                VRHandInput vhi = frozenHeldHand;
                if (vhi == null && pu.heldHand != null)
                    vhi = pu.heldHand;

                if (vhi != null)
                {
                    TelekinesisHandState tk = null;
                    try { tk = vhi.gameObject.GetComponent<TelekinesisHandState>(); } catch { }

                    Transform tkOrigin = null;
                    Transform rayOrigin = null;
                    Transform reticleTarget = null;
                    Transform smoothedJoint = null;

                    try { tkOrigin = vhi._TelekinesisOrigin; } catch { }
                    try { rayOrigin = vhi._ReticleRaycastOrigin; } catch { }
                    try { reticleTarget = vhi._ReticleTarget; } catch { }
                    try { smoothedJoint = vhi._SmoothedPickUpJoint; } catch { }

                    Vector3 originPos = pos;
                    if (rayOrigin != null)
                        originPos = rayOrigin.position;
                    else if (tkOrigin != null)
                        originPos = tkOrigin.position;

                    Vector3 toCard = pos - originPos;
                    float dist = toCard.magnitude;
                    Vector3 dir = dist > 0.0001f ? (toCard / dist) : Vector3.forward;

                    try { vhi.RequestTelekinesisHand(); } catch { }
                    try { vhi.FocusedInteractable = pu; } catch { }
                    try { if (reticleTarget != null) reticleTarget.position = pos; } catch { }
                    try { if (smoothedJoint != null) smoothedJoint.position = pos; } catch { }

                    if (tk != null)
                    {
                        try { tk.reticleDistance = dist; } catch { }
                        try { tk.reticlePositionRay = new Ray(originPos, dir); } catch { }
                        try { tk._smoothReticleVelocity = Vector3.zero; } catch { }
                        try
                        {
                            if (tk._SmoothedReticle != null)
                                tk._SmoothedReticle.SetActive(true);
                        }
                        catch { }
                        try
                        {
                            if (tk._SmoothedReticleRenderer != null)
                                tk._SmoothedReticleRenderer.enabled = true;
                        }
                        catch { }
                    }
                }

                MelonLogger.Msg("[Keycard] Synced vanilla TK snapshot phase=" + phase + " pos=" + pos);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[Keycard] SyncVanillaTkTargetToCurrentCard failed phase=" + phase + " err=" + e.Message);
            }
        }

        void CleanupSwipeVisuals(GameObject _card, bool bot)
        {
            if (_card != null)
                _card.SetActive(false);

            if (!bot)
            {
                StopSuppressingVanillaTk("SwipeMotionComplete");
                UnFreezeCard();
            }
            else if (droneHandCurrent != null)
            {
                droneHandCurrent.gameObject.SetActive(true);
            }
        }

        [HideFromIl2Cpp]
        private System.Collections.IEnumerator SwipeMotion(GameObject _card, bool bot)
        {
            swipeInProgress = true;
            MelonLogger.Msg($"[Keycard] - SwipeMotion - Bot: {bot}");
            AudioUtil.PlayAt("CardSwipe.wav", transform.position);

            if (bot && droneHandCurrent != null)
                droneHandCurrent.gameObject.SetActive(false);

            if (_card != null)
                _card.SetActive(true);

            float t = 0;
            while (t < swipeTime)
            {
                t += Time.deltaTime;
                float progress = t / swipeTime;

                if (_card != null)
                    _card.transform.position = Vector3.Lerp(cardStartPos, cardEndPos, progress);

                yield return null;
            }

            yield return null;

            CleanupSwipeVisuals(_card, bot);

            MelonLogger.Msg("[Keycard] - End SwipeMotion");
            yield return new WaitForSeconds(0.2f);

            Swiped = true;
            swipeInProgress = false;

            GameObject keypadObj = GameObject.Find("KeypadStandard");
            if (keypadObj == null)
            {
                MelonLogger.Warning("[Keycard] KeypadStandard not found after swipe.");
                yield break;
            }

            Keypad kp = keypadObj.GetComponent<Keypad>();
            if (kp == null)
            {
                MelonLogger.Warning("[Keycard] Keypad component not found after swipe.");
                yield break;
            }

            if (kp.CodeEntered) kp.PlayAccessGranted();
            else
            {
                kp.PlayAccessPending();
                kp.SetScreenColor(kp.ScreenPendingColor);
                kp.UpdateDisplayText(kp.AccessPendingText2);
                yield return new WaitForSeconds(2);
                kp.SetScreenColor(kp.ScreenNormalColor);
                kp.UpdateDisplayText("");
            }
        }

        void OnDisable()
        {

            SuppressingVanillaTkDuringSwipe = false;

            if (normalSwipeVisualsHidden)
                UnFreezeCard();

            if (keycardClone != null)
                keycardClone.SetActive(false);

            if (botKeycardClone != null)
                botKeycardClone.SetActive(false);

            if (droneHandCurrent != null)
                droneHandCurrent.gameObject.SetActive(true);

            frozenHeldHand = null;
            frozenHandObj = null;
            swipeInProgress = false;
        }
    }

    public class KeycardTrigger : MonoBehaviour
    {
        public KeycardTrigger(IntPtr ptr) : base(ptr) { }
        public KeycardTrigger() : base(ClassInjector.DerivedConstructorPointer<KeycardTrigger>())
            => ClassInjector.DerivedConstructorBody(this);

        public Keycard keycard;

        public bool swiped = false;

        void OnTriggerEnter(Collider other)
        {
            MelonLogger.Msg("[KeycardTrigger] - Trigger");
            if (isKeycard(other) && !swiped)
            {
                MelonLogger.Msg("[KeycardTrigger] - Is keycard");
                keycard.Swipe();
                swiped = true;
            }
        }

        bool isKeycard(Collider other)
        {
            Keycard _keycard = other.GetComponent<Keycard>();
            if (_keycard == null) return false;
            else
            {
                keycard = _keycard;
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(TelekinesisHandState), "HandUpdate")]
    internal static class Patch_TK_HandUpdate_KeycardSwipe
    {
        private static FieldInfo _fiHeldInteractable;
        private static FieldInfo _fiHeldObject;
        private static PropertyInfo _piHeldInteractable;
        private static bool _reflected;

        private static bool Prefix(TelekinesisHandState __instance)
        {
            if (__instance == null || __instance.gameObject == null)
                return true;

            VRHandInput vhi = __instance.gameObject.GetComponent<VRHandInput>();
            if (vhi == null)
                return true;

            PickUp heldPickup = TryGetHeldPickup(vhi);
            if (heldPickup == null)
                return true;

            Keycard keycard = heldPickup.gameObject.GetComponentInChildren<Keycard>(true);
            if (keycard == null)
                return true;

            if (!keycard.SuppressingVanillaTkDuringSwipe)
                return true;

            return false;
        }

        private static PickUp TryGetHeldPickup(VRHandInput vhi)
        {
            if (vhi == null) return null;
            EnsureReflection(vhi);

            if (_piHeldInteractable != null)
            {
                try
                {
                    object o = _piHeldInteractable.GetValue(vhi, null);
                    PickUp pu = CoerceToPickUp(o);
                    if (pu != null) return pu;
                }
                catch { }
            }

            if (_fiHeldInteractable != null)
            {
                try
                {
                    object o = _fiHeldInteractable.GetValue(vhi);
                    PickUp pu = CoerceToPickUp(o);
                    if (pu != null) return pu;
                }
                catch { }
            }

            if (_fiHeldObject != null)
            {
                try
                {
                    object o = _fiHeldObject.GetValue(vhi);
                    PickUp pu = CoerceToPickUp(o);
                    if (pu != null) return pu;
                }
                catch { }
            }

            try
            {
                Component fi = vhi.FocusedInteractable;
                if (fi != null)
                {
                    PickUp pu = fi.GetComponent<PickUp>() ?? fi.GetComponentInChildren<PickUp>();
                    if (pu != null && pu.isHeld) return pu;
                }
            }
            catch { }

            return null;
        }

        private static PickUp CoerceToPickUp(object o)
        {
            if (o == null) return null;

            PickUp pu = o as PickUp;
            if (pu != null) return pu;

            Component comp = o as Component;
            if (comp != null)
                return comp.GetComponent<PickUp>() ?? comp.GetComponentInChildren<PickUp>();

            GameObject go = o as GameObject;
            if (go != null)
                return go.GetComponent<PickUp>() ?? go.GetComponentInChildren<PickUp>();

            return null;
        }

        private static void EnsureReflection(VRHandInput vhi)
        {
            if (_reflected) return;
            _reflected = true;

            try
            {
                Type t = vhi.GetType();

                _piHeldInteractable = t.GetProperty("HeldInteractable",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                _fiHeldInteractable = t.GetField("heldInteractable",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? t.GetField("_heldInteractable", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? t.GetField("m_heldInteractable", BindingFlags.Instance | BindingFlags.NonPublic);

                _fiHeldObject = t.GetField("heldObject",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? t.GetField("_heldObject", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? t.GetField("m_heldObject", BindingFlags.Instance | BindingFlags.NonPublic);

                MelonLogger.Msg("[Keycard] TK patch reflection ready: " +
                                "piHeld=" + (_piHeldInteractable != null) + " " +
                                "fiHeldI=" + (_fiHeldInteractable != null) + " " +
                                "fiHeldO=" + (_fiHeldObject != null));
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[Keycard] TK patch reflection init failed: " + e.Message);
            }
        }
    }
}
