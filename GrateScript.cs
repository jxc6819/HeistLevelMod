using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using SG.Phoenix.Assets.Code.InputManagement;
using SG.Phoenix.Assets.Code.Interactables;
using SG.Phoenix.Assets.Code.VignetteNodes.Phoenix;
using UnhollowerBaseLib.Attributes;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class GrateScript : MonoBehaviour
    {
        public GrateScript(IntPtr ptr) : base(ptr) { }
        public GrateScript() : base(ClassInjector.DerivedConstructorPointer<GrateScript>())
            => ClassInjector.DerivedConstructorBody(this);

        GameObject[] bolts = new GameObject[4];

        int boltsUnscrewed = 0;
        bool primed = false;

        PickUp pu;
        GameObject parent;
        Vector3 lockedPos;
        Quaternion lockedRot;
        Rigidbody rb;

        const float JIGGLE_STRENGTH = 0.06f;
        const float JIGGLE_FREQ = 30f;
        const float JIGGLE_DURATION = 0.4f;

        float jiggleTimer = 0f;
        Vector3 jiggleAxis;

        bool _jiggled = false;

        public void Start()
        {
            int count = Mathf.Min(4, transform.childCount);

            parent = transform.parent.gameObject;
            pu = parent.GetComponent<PickUp>();
            rb = parent.GetComponent<Rigidbody>();

            for (int i = 0; i < 4; i++)
            {
                GameObject bolt = GameObject.Find("Bolt" + (i + 1));

                BoltScript bs = bolt.GetComponent<BoltScript>();
                if (bs == null) bs = bolt.AddComponent<BoltScript>();

                bs.grate = this;
                bs.boltIndex = i;
                bolts[i] = bolt;
            }

            lockedPos = parent.transform.position;
            lockedRot = parent.transform.rotation;

            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        void Update()
        {
            if (primed && pu.isHeld)
            {
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.constraints = RigidbodyConstraints.None;
                }
                GameObject.Find("GridBoltsCol").SetActive(false);
                GameObject.Find("PickUp_HOST_Drone").GetComponent<DroneVentRailTK>().enabled = true;
                GameObject.Find("VentCollider").layer = 18;
                AudioUtil.PlayAt("GrateGrab.wav", transform.position);
                this.enabled = false;
                return;
            }

            if (!primed)
            {
                if (pu != null && pu.isHeld && pu.heldHand != null)
                {
                    pu.heldHand.ReleaseHeldObject();
                    AudioUtil.PlayAt("GrateShake.wav", transform.position);
                    if (!_jiggled)
                    {
                        _jiggled = true;
                        HeistLevelManager.playHandler("Handler_VentHint.wav");
                    }
                    jiggleTimer = JIGGLE_DURATION;
                    jiggleAxis = UnityEngine.Random.onUnitSphere;
                }

                Vector3 pos = lockedPos;
                Quaternion rot = lockedRot;

                if (jiggleTimer > 0f)
                {
                    jiggleTimer -= Time.deltaTime;

                    float t = 1f - (jiggleTimer / JIGGLE_DURATION);
                    float damper = 1f - t;

                    float offset = Mathf.Sin(Time.time * JIGGLE_FREQ) * JIGGLE_STRENGTH * damper;

                    pos += jiggleAxis * offset;
                }

                parent.transform.position = pos;
                parent.transform.rotation = rot;

                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }

        public void BoltUnscrewed()
        {
            boltsUnscrewed++;

            if (boltsUnscrewed >= 4)
            {
                primed = true;
            }
        }
    }

    public class BoltScript : MonoBehaviour
    {
        public BoltScript(IntPtr ptr) : base(ptr) { }
        public BoltScript() : base(ClassInjector.DerivedConstructorPointer<BoltScript>())
            => ClassInjector.DerivedConstructorBody(this);

        public GrateScript grate;
        public int boltIndex = -1;

        public bool screwed = true;

        public Vector3 localUnscrewAxis = Vector3.forward;
        public float unscrewDistance = 0.045f;
        public float turns = 4.5f;
        public float unscrewDuration = 1.15f;

        public Vector3 wrenchLocalPos = new Vector3(0.05f, 0f, 0f);
        public Vector3 wrenchLocalEuler = new Vector3(0f, 0f, 90f);

        public float releaseDelay = 0.03f;
        public Vector3 dropAngularVelocityDeg = new Vector3(220f, 70f, 110f);

        public bool showDroneHandProxy = true;

        bool _busy = false;
        Rigidbody _rb;
        Collider[] _boltCols;
        Vector3 _startLocalPos;
        Quaternion _startLocalRot;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _boltCols = GetComponents<Collider>();
            _startLocalPos = transform.localPosition;
            _startLocalRot = transform.localRotation;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!screwed || _busy || other == null)
                return;

            WrenchContext ctx;
            if (!TryResolveWrench(other, out ctx))
                return;

            screwed = false;
            _busy = true;
            transform.parent = grate.gameObject.transform;
            grate.BoltUnscrewed();
            MelonCoroutines.Start(Co_Unscrew(ctx));
        }

        bool TryResolveWrench(Collider other, out WrenchContext ctx)
        {
            ctx = new WrenchContext();
            if (other == null) return false;

            Transform t = other.transform;
            if (t == null) return false;

            DronePickUp dpu = other.GetComponentInParent<DronePickUp>();
            PickUp pu = other.GetComponentInParent<PickUp>();
            Rigidbody rb = other.attachedRigidbody;

            GameObject candidate = null;
            if (dpu != null) candidate = dpu.gameObject;
            else if (pu != null) candidate = pu.gameObject;
            else if (rb != null) candidate = rb.gameObject;
            else if (t.root != null) candidate = t.root.gameObject;
            else candidate = t.gameObject;

            if (candidate == null) return false;
            if (!LooksLikeWrench(candidate, other, dpu)) return false;

            ctx.root = candidate;
            ctx.transform = candidate.transform;
            ctx.pickUp = dpu != null ? dpu.pickUp : null;
            if (ctx.pickUp == null)
                ctx.pickUp = pu != null ? pu : candidate.GetComponent<PickUp>() ?? candidate.GetComponentInChildren<PickUp>(true);

            ctx.dronePickUp = dpu != null ? dpu : candidate.GetComponent<DronePickUp>() ?? candidate.GetComponentInChildren<DronePickUp>(true);
            ctx.rb = candidate.GetComponent<Rigidbody>() ?? candidate.GetComponentInChildren<Rigidbody>(true);
            ctx.renderers = candidate.GetComponentsInChildren<Renderer>(true);
            ctx.colliders = candidate.GetComponentsInChildren<Collider>(true);

            if (ctx.dronePickUp != null)
            {
                Transform p = ctx.dronePickUp.transform.parent;
                if (p != null)
                {
                    DroneHand dh = p.GetComponent<DroneHand>();
                    if (dh != null && dh.holding == ctx.dronePickUp)
                        ctx.droneHand = dh;
                }
            }

            ctx.droneHeld = (ctx.droneHand != null);
            ctx.pickupHeld = (ctx.pickUp != null && ctx.pickUp.isHeld);
            return ctx.root != null;
        }

        bool LooksLikeWrench(GameObject candidate, Collider other, DronePickUp dpu)
        {
            try
            {
                string n = candidate.name ?? "";
                if (n.ToLower().Contains("wrench")) return true;
            }
            catch { }

            try
            {
                string n = other.name ?? "";
                if (n.ToLower().Contains("wrench")) return true;
            }
            catch { }

            if (dpu != null)
            {
                try
                {
                    string n = dpu.gameObject.name ?? "";
                    if (n.ToLower().Contains("wrench")) return true;
                }
                catch { }
            }

            return false;
        }

        [HideFromIl2Cpp]
        System.Collections.IEnumerator Co_Unscrew(WrenchContext ctx)
        {
            GameObject proxy = null;
            GameObject handProxy = null;
            GameObject anchorGO = null;
            bool restoreNeeded = false;
            bool suppressNormalTk = false;
            AudioSource source = AudioUtil.PlayAt("Bolts.mp3", transform.position);

            try
            {
                if (ctx.root == null || ctx.transform == null)
                    yield break;

                if (ctx.droneHeld && ctx.droneHand != null && ctx.droneHand._launching)
                {
                    try { ctx.droneHand.CancelActiveMotionToRestPose(); } catch { }
                }

                SaveActualState(ref ctx);



                if (!ctx.droneHeld && ctx.pickUp != null && ctx.pickUp.isHeld)
                {
                    BoltWrenchTkSuppressor.Begin(ctx.pickUp, ctx.transform, ctx.rb);
                    suppressNormalTk = true;
                }

                proxy = CreateVisualProxy(ctx);
                if (ctx.droneHeld && showDroneHandProxy)
                    handProxy = CreateDroneHandProxy(ctx);

                FreezeActualWrench(ctx);
                HideActualWrench(ctx);
                if (ctx.droneHeld)
                    HideActualDroneHand(ctx);
                restoreNeeded = true;

                anchorGO = new GameObject("BoltWrenchAnchor");
                Transform anchor = anchorGO.transform;
                anchor.SetParent(transform, false);
                anchor.localPosition = wrenchLocalPos;
                anchor.localRotation = Quaternion.Euler(wrenchLocalEuler);
                anchor.localScale = Vector3.one;

                if (proxy != null)
                {
                    Transform proxyTf = proxy.transform;
                    Vector3 proxyWorldScale = GetWorldScale(proxyTf);
                    proxyTf.SetParent(anchor, false);
                    proxyTf.localPosition = Vector3.zero;
                    proxyTf.localRotation = Quaternion.identity;
                    SetWorldScale(proxyTf, proxyWorldScale);
                }

                if (handProxy != null)
                    AttachHandProxyToWrenchProxy(ctx, handProxy, anchor);

                if (_rb != null)
                {
                    _rb.velocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                    _rb.useGravity = false;
                    _rb.isKinematic = false;
                    _rb.constraints = RigidbodyConstraints.FreezeAll;
                }

                _startLocalPos = transform.localPosition;
                _startLocalRot = transform.localRotation;

                Vector3 axisLocal = localUnscrewAxis;
                if (axisLocal.sqrMagnitude < 0.000001f)
                    axisLocal = Vector3.forward;
                axisLocal.Normalize();

                float elapsed = 0f;
                while (elapsed < unscrewDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, unscrewDuration));
                    float eased = Mathf.SmoothStep(0f, 1f, t);
                    float angle = eased * 360f * turns;

                    transform.localRotation = _startLocalRot * Quaternion.AngleAxis(angle, axisLocal);
                    transform.localPosition = _startLocalPos + axisLocal * (unscrewDistance * eased);

                    if (suppressNormalTk && ctx.pickUp != null)
                        BoltWrenchTkSuppressor.TickLock(ctx.pickUp);

                    yield return null;

                    if (suppressNormalTk && ctx.pickUp != null)
                        BoltWrenchTkSuppressor.TickLock(ctx.pickUp);
                }

                transform.localRotation = _startLocalRot * Quaternion.AngleAxis(360f * turns, axisLocal);
                transform.localPosition = _startLocalPos + axisLocal * unscrewDistance;

                if (releaseDelay > 0f)
                    yield return new WaitForSeconds(releaseDelay);

                if (proxy != null)
                {
                    UnityEngine.Object.Destroy(proxy);
                    proxy = null;
                }

                if (handProxy != null)
                {
                    UnityEngine.Object.Destroy(handProxy);
                    handProxy = null;
                }

                if (anchorGO != null)
                {
                    UnityEngine.Object.Destroy(anchorGO);
                    anchorGO = null;
                }

                yield return null;
            }
            finally
            {
                if (proxy != null)
                    UnityEngine.Object.Destroy(proxy);

                if (handProxy != null)
                    UnityEngine.Object.Destroy(handProxy);

                if (anchorGO != null)
                    UnityEngine.Object.Destroy(anchorGO);

                if (restoreNeeded)
                {
                    if (ctx.droneHeld)
                        ForceResnapRealWrenchToDroneHand(ctx);

                    RestoreActualWrench(ctx);

                    if (ctx.droneHeld)
                        RestoreActualDroneHand(ctx);
                }

                if (suppressNormalTk && ctx.pickUp != null)
                    BoltWrenchTkSuppressor.End(ctx.pickUp);

                ReleaseBoltToPhysics();
                if (source != null) source.Stop();
                _busy = false;
            }
        }

        void SaveActualState(ref WrenchContext ctx)
        {
            if (ctx.transform != null)
            {
                ctx.savedParent = ctx.transform.parent;
                ctx.savedLocalPos = ctx.transform.localPosition;
                ctx.savedLocalRot = ctx.transform.localRotation;
                ctx.savedLocalScale = ctx.transform.localScale;
                ctx.savedWorldPos = ctx.transform.position;
                ctx.savedWorldRot = ctx.transform.rotation;
                ctx.savedWorldScale = ctx.transform.lossyScale;
            }

            if (ctx.renderers != null)
            {
                ctx.rendererEnabled = new bool[ctx.renderers.Length];
                for (int i = 0; i < ctx.renderers.Length; i++)
                    ctx.rendererEnabled[i] = (ctx.renderers[i] != null && ctx.renderers[i].enabled);
            }

            if (ctx.colliders != null)
            {
                ctx.colliderEnabled = new bool[ctx.colliders.Length];
                for (int i = 0; i < ctx.colliders.Length; i++)
                    ctx.colliderEnabled[i] = (ctx.colliders[i] != null && ctx.colliders[i].enabled);
            }

            if (ctx.rb != null)
            {
                ctx.savedUseGravity = ctx.rb.useGravity;
                ctx.savedIsKinematic = ctx.rb.isKinematic;
                ctx.savedConstraints = ctx.rb.constraints;
                ctx.savedVelocity = ctx.rb.velocity;
                ctx.savedAngularVelocity = ctx.rb.angularVelocity;
            }

            if (ctx.droneHand != null)
            {
                ctx.droneHandRenderers = ctx.droneHand.GetComponentsInChildren<Renderer>(true);
                if (ctx.droneHandRenderers != null)
                {
                    ctx.droneHandRendererEnabled = new bool[ctx.droneHandRenderers.Length];
                    for (int i = 0; i < ctx.droneHandRenderers.Length; i++)
                        ctx.droneHandRendererEnabled[i] = (ctx.droneHandRenderers[i] != null && ctx.droneHandRenderers[i].enabled);
                }
            }
        }

        void FreezeActualWrench(WrenchContext ctx)
        {
            if (ctx.rb == null || ctx.droneHeld)
                return;

            try
            {
                ctx.rb.velocity = Vector3.zero;
                ctx.rb.angularVelocity = Vector3.zero;
                ctx.rb.useGravity = false;
                ctx.rb.isKinematic = true;
                ctx.rb.constraints = RigidbodyConstraints.FreezeAll;
            }
            catch { }
        }

        void HideActualWrench(WrenchContext ctx)
        {
            if (ctx.renderers != null)
            {
                for (int i = 0; i < ctx.renderers.Length; i++)
                    if (ctx.renderers[i] != null)
                        ctx.renderers[i].enabled = false;
            }

            if (ctx.colliders != null)
            {
                for (int i = 0; i < ctx.colliders.Length; i++)
                    if (ctx.colliders[i] != null)
                        ctx.colliders[i].enabled = false;
            }
        }

        void RestoreActualWrench(WrenchContext ctx)
        {
            if (ctx.transform != null)
            {
                try
                {
                    if (ctx.savedParent != null)
                    {
                        ctx.transform.SetParent(ctx.savedParent, false);
                        ctx.transform.localPosition = ctx.savedLocalPos;
                        ctx.transform.localRotation = ctx.savedLocalRot;
                        ctx.transform.localScale = ctx.savedLocalScale;
                    }
                    else
                    {
                        ctx.transform.SetParent(null, true);
                        ctx.transform.position = ctx.savedWorldPos;
                        ctx.transform.rotation = ctx.savedWorldRot;
                        SetWorldScale(ctx.transform, ctx.savedWorldScale);
                    }
                }
                catch { }
            }

            if (ctx.renderers != null && ctx.rendererEnabled != null)
            {
                for (int i = 0; i < ctx.renderers.Length && i < ctx.rendererEnabled.Length; i++)
                    if (ctx.renderers[i] != null)
                        ctx.renderers[i].enabled = ctx.rendererEnabled[i];
            }

            if (ctx.colliders != null && ctx.colliderEnabled != null)
            {
                for (int i = 0; i < ctx.colliders.Length && i < ctx.colliderEnabled.Length; i++)
                    if (ctx.colliders[i] != null)
                        ctx.colliders[i].enabled = ctx.colliderEnabled[i];
            }

            if (ctx.rb != null && !ctx.droneHeld)
            {
                try
                {
                    ctx.rb.useGravity = ctx.savedUseGravity;
                    ctx.rb.isKinematic = ctx.savedIsKinematic;
                    ctx.rb.constraints = ctx.savedConstraints;
                    ctx.rb.velocity = Vector3.zero;
                    ctx.rb.angularVelocity = Vector3.zero;
                }
                catch { }
            }
        }

        void ForceResnapRealWrenchToDroneHand(WrenchContext ctx)
        {
            if (ctx.droneHand == null || ctx.dronePickUp == null || ctx.transform == null)
                return;

            Transform wrenchTf = ctx.transform;
            Transform handTf = ctx.droneHand.transform;
            if (wrenchTf == null || handTf == null)
                return;

            Vector3 savedWorldScale = GetWorldScale(wrenchTf);

            wrenchTf.SetParent(handTf, true);
            wrenchTf.localPosition = ctx.dronePickUp.localHoldPos;
            wrenchTf.localRotation = ctx.dronePickUp.localHoldRot;

            if (ctx.savedParent == handTf)
                wrenchTf.localScale = ctx.savedLocalScale;
            else
                SetWorldScale(wrenchTf, savedWorldScale);

            Rigidbody realRb = ctx.rb;
            if (realRb == null)
                realRb = ctx.root.GetComponent<Rigidbody>() ?? ctx.root.GetComponentInChildren<Rigidbody>(true);

            if (realRb != null)
            {
                realRb.velocity = Vector3.zero;
                realRb.angularVelocity = Vector3.zero;
                realRb.useGravity = false;
                realRb.isKinematic = true;
                realRb.constraints = RigidbodyConstraints.FreezeAll;
            }

            if (ctx.droneHand.holding != ctx.dronePickUp)
                ctx.droneHand.holding = ctx.dronePickUp;

            if (ctx.pickUp != null)
                ctx.pickUp.enabled = false;

            MelonLogger.Msg("[BoltScript] ForceResnapRealWrenchToDroneHand savedParent=" + (ctx.savedParent != null ? ctx.savedParent.name : "null") + " localScale=" + wrenchTf.localScale + " worldScale=" + wrenchTf.lossyScale);
        }

        GameObject CreateVisualProxy(WrenchContext ctx)
        {
            if (ctx.root == null || ctx.transform == null) return null;

            Vector3 worldPos = ctx.transform.position;
            Quaternion worldRot = ctx.transform.rotation;
            Vector3 worldScale = ctx.transform.lossyScale;

            GameObject proxy = UnityEngine.Object.Instantiate(ctx.root);
            proxy.name = ctx.root.name + "_BoltProxy";

            Transform pt = proxy.transform;
            pt.position = worldPos;
            pt.rotation = worldRot;
            SetWorldScale(pt, worldScale);

            StripInteractiveComponents(proxy);
            ForceEnableRenderers(proxy);
            return proxy;
        }

        void HideActualDroneHand(WrenchContext ctx)
        {
            if (ctx.droneHandRenderers == null) return;
            for (int i = 0; i < ctx.droneHandRenderers.Length; i++)
            {
                if (ctx.droneHandRenderers[i] != null)
                    ctx.droneHandRenderers[i].enabled = false;
            }
        }

        void RestoreActualDroneHand(WrenchContext ctx)
        {
            if (ctx.droneHandRenderers == null || ctx.droneHandRendererEnabled == null) return;
            for (int i = 0; i < ctx.droneHandRenderers.Length && i < ctx.droneHandRendererEnabled.Length; i++)
            {
                if (ctx.droneHandRenderers[i] != null)
                    ctx.droneHandRenderers[i].enabled = ctx.droneHandRendererEnabled[i];
            }
        }

        GameObject CreateDroneHandProxy(WrenchContext ctx)
        {
            if (ctx.droneHand == null) return null;

            Vector3 worldPos = ctx.droneHand.transform.position;
            Quaternion worldRot = ctx.droneHand.transform.rotation;
            Vector3 worldScale = ctx.droneHand.transform.lossyScale;

            GameObject proxy = UnityEngine.Object.Instantiate(ctx.droneHand.gameObject);
            proxy.name = ctx.droneHand.gameObject.name + "_BoltHandProxy";

            Transform pt = proxy.transform;
            pt.position = worldPos;
            pt.rotation = worldRot;
            SetWorldScale(pt, worldScale);

            StripHeldObjectFromHandProxy(ctx, proxy);
            StripInteractiveComponents(proxy);
            ForceEnableRenderers(proxy);
            return proxy;
        }

        void StripHeldObjectFromHandProxy(WrenchContext ctx, GameObject handProxy)
        {
            if (ctx.droneHand == null || ctx.dronePickUp == null || ctx.dronePickUp.transform == null || handProxy == null)
                return;

            Transform realHeldTf = ctx.dronePickUp.transform;
            Transform realHandTf = ctx.droneHand.transform;
            Transform proxyHandTf = handProxy.transform;

            if (realHeldTf.parent != realHandTf)
                return;

            int childIndex = realHeldTf.GetSiblingIndex();
            if (childIndex < 0 || childIndex >= proxyHandTf.childCount)
                return;

            Transform proxyHeldTf = proxyHandTf.GetChild(childIndex);
            if (proxyHeldTf == null)
                return;

            UnityEngine.Object.Destroy(proxyHeldTf.gameObject);
        }

        void AttachHandProxyToWrenchProxy(WrenchContext ctx, GameObject handProxy, Transform wrenchAnchor)
        {
            if (ctx.transform == null || ctx.droneHand == null || handProxy == null || wrenchAnchor == null)
                return;

            Transform realWrench = ctx.transform;
            Transform realHand = ctx.droneHand.transform;
            Transform proxyHand = handProxy.transform;

            Vector3 handWorldScale = GetWorldScale(proxyHand);
            Vector3 localPos = realWrench.InverseTransformPoint(realHand.position);
            Quaternion localRot = Quaternion.Inverse(realWrench.rotation) * realHand.rotation;

            proxyHand.SetParent(wrenchAnchor, false);
            proxyHand.localPosition = localPos;
            proxyHand.localRotation = localRot;
            SetWorldScale(proxyHand, handWorldScale);
        }

        void StripInteractiveComponents(GameObject go)
        {
            Rigidbody[] rbs = go.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rbs.Length; i++)
                if (rbs[i] != null)
                    UnityEngine.Object.Destroy(rbs[i]);

            Collider[] cols = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
                if (cols[i] != null)
                    cols[i].enabled = false;

            PickUp[] pus = go.GetComponentsInChildren<PickUp>(true);
            for (int i = 0; i < pus.Length; i++)
                if (pus[i] != null)
                    UnityEngine.Object.Destroy(pus[i]);

            DronePickUp[] dpus = go.GetComponentsInChildren<DronePickUp>(true);
            for (int i = 0; i < dpus.Length; i++)
                if (dpus[i] != null)
                    UnityEngine.Object.Destroy(dpus[i]);

            DronePickUpHitbox[] hb = go.GetComponentsInChildren<DronePickUpHitbox>(true);
            for (int i = 0; i < hb.Length; i++)
                if (hb[i] != null)
                    UnityEngine.Object.Destroy(hb[i]);

            DroneHand[] dh = go.GetComponentsInChildren<DroneHand>(true);
            for (int i = 0; i < dh.Length; i++)
                if (dh[i] != null)
                    UnityEngine.Object.Destroy(dh[i]);

            HeadsetScript[] hs = go.GetComponentsInChildren<HeadsetScript>(true);
            for (int i = 0; i < hs.Length; i++)
                if (hs[i] != null)
                    UnityEngine.Object.Destroy(hs[i]);

            BoltScript[] bolts = go.GetComponentsInChildren<BoltScript>(true);
            for (int i = 0; i < bolts.Length; i++)
                if (bolts[i] != null)
                    UnityEngine.Object.Destroy(bolts[i]);

            GrateScript[] grates = go.GetComponentsInChildren<GrateScript>(true);
            for (int i = 0; i < grates.Length; i++)
                if (grates[i] != null)
                    UnityEngine.Object.Destroy(grates[i]);
        }

        void ForceEnableRenderers(GameObject go)
        {
            if (go == null) return;

            Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] != null)
                    rs[i].enabled = true;
            }
        }

        Vector3 GetWorldScale(Transform t)
        {
            if (t == null) return Vector3.one;
            return t.lossyScale;
        }

        void SetWorldScale(Transform t, Vector3 worldScale)
        {
            if (t == null) return;

            Vector3 parentScale = Vector3.one;
            if (t.parent != null)
                parentScale = t.parent.lossyScale;

            float sx = Mathf.Abs(parentScale.x) < 0.0001f ? 0.0001f : parentScale.x;
            float sy = Mathf.Abs(parentScale.y) < 0.0001f ? 0.0001f : parentScale.y;
            float sz = Mathf.Abs(parentScale.z) < 0.0001f ? 0.0001f : parentScale.z;

            t.localScale = new Vector3(
                worldScale.x / sx,
                worldScale.y / sy,
                worldScale.z / sz
            );
        }

        void ReleaseBoltToPhysics()
        {
            if (_boltCols != null && _boltCols.Length > 0)
            {
                bool hadSolid = false;
                for (int i = 0; i < _boltCols.Length; i++)
                {
                    if (_boltCols[i] == null) continue;
                    if (!_boltCols[i].isTrigger) hadSolid = true;
                }

                if (!hadSolid && _boltCols[0] != null)
                {
                    BoxCollider box = _boltCols[0].gameObject.GetComponent<BoxCollider>();
                    _boltCols[0].enabled = false;
                    box.enabled = true;
                }
            }

            if (_rb != null)
            {
                _rb.constraints = RigidbodyConstraints.None;
                _rb.isKinematic = false;
                _rb.useGravity = true;
                _rb.velocity = Vector3.zero;
                _rb.angularVelocity = dropAngularVelocityDeg * 0.0174532924f;
            }
        }

        struct WrenchContext
        {
            public GameObject root;
            public Transform transform;
            public PickUp pickUp;
            public DronePickUp dronePickUp;
            public Rigidbody rb;
            public DroneHand droneHand;

            public bool droneHeld;
            public bool pickupHeld;

            public Transform savedParent;
            public Vector3 savedLocalPos;
            public Quaternion savedLocalRot;
            public Vector3 savedLocalScale;
            public Vector3 savedWorldPos;
            public Quaternion savedWorldRot;
            public Vector3 savedWorldScale;

            public Renderer[] renderers;
            public Collider[] colliders;
            public bool[] rendererEnabled;
            public bool[] colliderEnabled;

            public bool savedUseGravity;
            public bool savedIsKinematic;
            public RigidbodyConstraints savedConstraints;
            public Vector3 savedVelocity;
            public Vector3 savedAngularVelocity;

            public Renderer[] droneHandRenderers;
            public bool[] droneHandRendererEnabled;
        }
    }

    internal static class BoltWrenchTkSuppressor
    {
        class SuppressedWrench
        {
            public PickUp pickUp;
            public VRHandInput heldHand;
            public Transform tf;
            public Rigidbody rb;
            public Transform parent;
            public Vector3 localPos;
            public Quaternion localRot;
            public Vector3 localScale;
            public Vector3 worldPos;
            public Quaternion worldRot;
            public Vector3 worldScale;
            public bool hasSavedReelingEnabled;
            public bool savedReelingEnabled;
            public GameObject tkBeamObject;
            public LineRenderer tkBeamRenderer;
            public bool hasSavedTkBeamObjectActive;
            public bool savedTkBeamObjectActive;
            public bool hasSavedTkBeamRendererEnabled;
            public bool savedTkBeamRendererEnabled;
        }

        static readonly System.Collections.Generic.List<SuppressedWrench> _suppressed = new System.Collections.Generic.List<SuppressedWrench>();

        public static void Begin(PickUp pu, Transform tf, Rigidbody rb)
        {
            if (pu == null) return;

            SuppressedWrench rec = Find(pu);
            if (rec == null)
            {
                rec = new SuppressedWrench();
                rec.pickUp = pu;
                _suppressed.Add(rec);
            }

            rec.heldHand = null;
            try { rec.heldHand = pu.heldHand; } catch { }

            rec.tf = tf != null ? tf : pu.transform;
            rec.rb = rb != null ? rb : (rec.tf != null ? rec.tf.GetComponent<Rigidbody>() : null);
            if (rec.rb == null && pu != null)
                rec.rb = pu.GetComponent<Rigidbody>() ?? pu.GetComponentInChildren<Rigidbody>(true);

            if (rec.tf != null)
            {
                rec.parent = rec.tf.parent;
                rec.localPos = rec.tf.localPosition;
                rec.localRot = rec.tf.localRotation;
                rec.localScale = rec.tf.localScale;
                rec.worldPos = rec.tf.position;
                rec.worldRot = rec.tf.rotation;
                rec.worldScale = rec.tf.lossyScale;
            }

            rec.hasSavedReelingEnabled = false;
            rec.savedReelingEnabled = false;
            try
            {
                rec.savedReelingEnabled = pu._reelingEnabled;
                rec.hasSavedReelingEnabled = true;
            }
            catch { }

            try { pu._reelingEnabled = false; } catch { }

            CacheAndHideTkBeam(rec);
            ForceLock(rec);

            MelonLogger.Msg("[BoltWrenchTK] Suppressing normal TK for " + pu.name +
                            " hand=" + (rec.heldHand != null ? rec.heldHand.gameObject.name : "null") +
                            " pos=" + FormatVec(rec.worldPos));
        }

        public static void End(PickUp pu)
        {
            if (pu == null) return;

            SuppressedWrench rec = Find(pu);
            bool restoreReeling = false;
            bool savedReeling = false;

            if (rec != null)
            {
                restoreReeling = rec.hasSavedReelingEnabled;
                savedReeling = rec.savedReelingEnabled;
                _suppressed.Remove(rec);
            }



            if (restoreReeling)
            {
                try { pu._reelingEnabled = savedReeling; } catch { }
            }

            RestoreTkBeam(rec);

            MelonLogger.Msg("[BoltWrenchTK] Restored normal TK for " + pu.name +
                            " reelingRestored=" + restoreReeling +
                            " savedReeling=" + savedReeling);
        }

        public static void TickLock(PickUp pu)
        {
            SuppressedWrench rec = Find(pu);
            if (rec == null) return;
            HideTkBeam(rec);
            ForceLock(rec);
        }

        public static bool IsSuppressed(PickUp pu)
        {
            if (pu == null) return false;
            return Find(pu) != null;
        }

        public static bool ShouldSuppressForHand(VRHandInput vhi, PickUp heldPickup)
        {
            if (heldPickup != null && IsSuppressed(heldPickup))
                return true;

            if (vhi == null)
                return false;

            for (int i = 0; i < _suppressed.Count; i++)
            {
                SuppressedWrench rec = _suppressed[i];
                if (rec == null || rec.pickUp == null)
                    continue;

                if (rec.heldHand == vhi)
                    return true;

                try
                {
                    if (rec.pickUp.heldHand == vhi)
                        return true;
                }
                catch { }
            }

            return false;
        }

        static void CacheAndHideTkBeam(SuppressedWrench rec)
        {
            if (rec == null)
                return;

            rec.tkBeamObject = null;
            rec.tkBeamRenderer = null;
            rec.hasSavedTkBeamObjectActive = false;
            rec.savedTkBeamObjectActive = false;
            rec.hasSavedTkBeamRendererEnabled = false;
            rec.savedTkBeamRendererEnabled = false;

            GameObject handObj = null;
            try
            {
                if (rec.heldHand != null)
                    handObj = rec.heldHand.gameObject;
            }
            catch { }

            if (handObj != null)
            {
                try
                {
                    Transform direct = handObj.transform.Find("TelekinesisBeam");
                    if (direct != null)
                        rec.tkBeamObject = direct.gameObject;
                }
                catch { }

                if (rec.tkBeamObject == null)
                {
                    try
                    {
                        LineRenderer[] lrs = handObj.GetComponentsInChildren<LineRenderer>(true);
                        if (lrs != null)
                        {
                            for (int i = 0; i < lrs.Length; i++)
                            {
                                LineRenderer lr = lrs[i];
                                if (lr == null) continue;
                                string n = lr.gameObject.name != null ? lr.gameObject.name.ToLower() : "";
                                if (n.Contains("telekinesisbeam") || (n.Contains("tele") && n.Contains("beam")))
                                {
                                    rec.tkBeamRenderer = lr;
                                    rec.tkBeamObject = lr.gameObject;
                                    break;
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (rec.tkBeamRenderer == null && rec.tkBeamObject != null)
                {
                    try { rec.tkBeamRenderer = rec.tkBeamObject.GetComponent<LineRenderer>(); } catch { }
                }
            }

            if (rec.tkBeamObject != null)
            {
                try
                {
                    rec.savedTkBeamObjectActive = rec.tkBeamObject.activeSelf;
                    rec.hasSavedTkBeamObjectActive = true;
                }
                catch { }
            }

            if (rec.tkBeamRenderer != null)
            {
                try
                {
                    rec.savedTkBeamRendererEnabled = rec.tkBeamRenderer.enabled;
                    rec.hasSavedTkBeamRendererEnabled = true;
                }
                catch { }
            }

            HideTkBeam(rec);
        }

        static void HideTkBeam(SuppressedWrench rec)
        {
            if (rec == null)
                return;

            try
            {
                if (rec.tkBeamRenderer != null)
                    rec.tkBeamRenderer.enabled = false;
            }
            catch { }

            try
            {
                if (rec.tkBeamObject != null)
                    rec.tkBeamObject.SetActive(false);
            }
            catch { }
        }

        static void RestoreTkBeam(SuppressedWrench rec)
        {
            if (rec == null)
                return;

            try
            {
                if (rec.tkBeamObject != null && rec.hasSavedTkBeamObjectActive)
                    rec.tkBeamObject.SetActive(rec.savedTkBeamObjectActive);
            }
            catch { }

            try
            {
                if (rec.tkBeamRenderer != null && rec.hasSavedTkBeamRendererEnabled)
                    rec.tkBeamRenderer.enabled = rec.savedTkBeamRendererEnabled;
            }
            catch { }
        }

        static SuppressedWrench Find(PickUp pu)
        {
            if (pu == null) return null;
            for (int i = 0; i < _suppressed.Count; i++)
            {
                SuppressedWrench rec = _suppressed[i];
                if (rec != null && rec.pickUp == pu)
                    return rec;
            }
            return null;
        }

        static void ForceLock(SuppressedWrench rec)
        {
            if (rec == null)
                return;

            Transform tf = rec.tf;
            if (tf != null)
            {
                try
                {
                    if (rec.parent != null)
                    {
                        if (tf.parent != rec.parent)
                            tf.SetParent(rec.parent, false);

                        tf.localPosition = rec.localPos;
                        tf.localRotation = rec.localRot;
                        tf.localScale = rec.localScale;
                    }
                    else
                    {
                        if (tf.parent != null)
                            tf.SetParent(null, true);

                        tf.position = rec.worldPos;
                        tf.rotation = rec.worldRot;
                        SetWorldScaleStatic(tf, rec.worldScale);
                    }
                }
                catch { }
            }

            Rigidbody rb = rec.rb;
            if (rb != null)
            {
                try
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.useGravity = false;
                    rb.isKinematic = true;
                    rb.constraints = RigidbodyConstraints.FreezeAll;
                }
                catch { }
            }

            if (rec.pickUp != null)
            {
                try { rec.pickUp._targetPickUpPosition = rec.worldPos; } catch { }
                try { rec.pickUp._priorPosition = rec.worldPos; } catch { }
                try { rec.pickUp._targetPickUpRotation = rec.worldRot; } catch { }
                try { rec.pickUp._priorRotation = rec.worldRot; } catch { }
                try { rec.pickUp._onFreezePosition = rec.worldPos; } catch { }
                try { rec.pickUp._reelingEnabled = false; } catch { }
            }
        }

        static void SetWorldScaleStatic(Transform t, Vector3 worldScale)
        {
            if (t == null) return;

            Vector3 parentScale = Vector3.one;
            if (t.parent != null)
                parentScale = t.parent.lossyScale;

            float sx = Mathf.Abs(parentScale.x) < 0.0001f ? 0.0001f : parentScale.x;
            float sy = Mathf.Abs(parentScale.y) < 0.0001f ? 0.0001f : parentScale.y;
            float sz = Mathf.Abs(parentScale.z) < 0.0001f ? 0.0001f : parentScale.z;

            t.localScale = new Vector3(worldScale.x / sx, worldScale.y / sy, worldScale.z / sz);
        }

        static string FormatVec(Vector3 v)
        {
            return "(" + v.x.ToString("F3") + ", " + v.y.ToString("F3") + ", " + v.z.ToString("F3") + ")";
        }
    }

    [HarmonyPatch(typeof(TelekinesisHandState), "HandUpdate")]
    internal static class Patch_TK_HandUpdate_BoltWrenchSuppressor
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

            if (!BoltWrenchTkSuppressor.ShouldSuppressForHand(vhi, heldPickup))
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

                MelonLogger.Msg("[BoltWrenchTK] Reflection ready: " +
                                "piHeld=" + (_piHeldInteractable != null) + " " +
                                "fiHeldI=" + (_fiHeldInteractable != null) + " " +
                                "fiHeldO=" + (_fiHeldObject != null));
            }
            catch (Exception e)
            {
                MelonLogger.Msg("[BoltWrenchTK] Reflection init failed: " + e.Message);
            }
        }
    }
}
