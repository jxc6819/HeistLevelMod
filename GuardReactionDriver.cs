
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using MelonLoader;
using UnhollowerRuntimeLib;
using UnhollowerBaseLib.Attributes;
using System;

namespace IEYTD_Mod2Code
{
    public class GuardReactionDriver : MonoBehaviour
    {
        public GuardReactionDriver(IntPtr ptr) : base(ptr) { }
        public GuardReactionDriver() : base(ClassInjector.DerivedConstructorPointer<GuardReactionDriver>())
            => ClassInjector.DerivedConstructorBody(this);
        public Transform guardRoot;
        public Transform chairRoot;
        public Animator animator;
        public GameObject lyingGuardSource;
        public AnimationClip upperBodyHitClip;
        public AnimationClip lyingAnimationClip;
        public AnimationClip sittingDodgeClip;
        public AnimationClip scaredClip;
        public bool doHit = false;
        public bool doMiss = false;
        public bool doReset = false;
        public bool debugLogs = true;



        private const float totalDuration = 0.62f;
        private const float detachAt = 0.84f;

        private const bool useNegativeForward = true;
        private static readonly Vector3 manualWorldShoveDirection = Vector3.zero;

        private const float initialBlastDistance = 0.42f;
        private const float toppleBackwardDistance = 0.58f;
        private const float backwardOvershoot = 0.12f;
        private const float upwardBump = 0.07f;
        private const float sideShove = 0.00f;
        private const float blastDuration = 0.10f;
        private const float earlyTipDegrees = 14f;
        private static readonly Vector3 finalChairLocalEuler = new Vector3(-108f, 0f, 0f);

        private const float clipStartNormalized = 0.27f;
        private const float clipEndNormalized = 0.52f;
        private const float clipStartDelay = 0.02f;
        private const float clipBlendIn = 0.05f;
        private const float clipBlendOut = 0.10f;
        private const float maxUpperBodyWeight = 0.90f;
        private const float sampledAnimDuration = 0f;

        private const bool cloneLyingGuard = true;
        private const bool copyLiveScale = true;
        private const bool copyLiveYaw = true;
        private const bool useLiveBoundsCenter = true;
        private const float lyingGuardFloorY = 3.429f;
        private const float lyingYawOffset = 180f;
        private const float lyingForwardOffset = 0f;
        private const float lyingRightOffset = 0f;
        private const float lyingUpOffset = 0f;

        private const float liveToLyingBlendDuration = 0.28f;
        private const float liveBlendPositionWeight = 1.00f;
        private const float liveBlendRotationWeight = 1.00f;
        private const float lyingRevealNormalized = 0.56f;
        private const float finalLiveFadeFrameDelay = 0.00f;
        private const bool alignLyingUsingHips = true;
        private const float liveToLyingArcHeight = 0.03f;
        private const float liveToLyingPreAlign = 0.95f;
        private const float swapDownwardBias = 0.02f;
        private const bool hideLiveGuardAtReveal = true;
        private const float chairClearStartDuringSwap = 0.00f;

        private const float floorY = 4.50f;
        private const float guardFloorClearance = 0.01f;
        private const float chairFloorClearance = 0.015f;
        private const float lyingRevealLift = 0.06f;
        private const float lyingRevealBackOffset = 0.035f;
        private const float lyingSettleDuration = 0.10f;
        private const bool forceChairClearAtReveal = true;

        private const float chairClearMargin = 0.28f;
        private const float chairMinMove = 0.74f;
        private const float chairImpactExtraMove = 0.34f;
        private const float chairClearLift = 0.024f;
        private const float chairClearDuration = 0.19f;
        private const float chairSettleDuration = 0.05f;
        private const float chairClearYaw = 10f;
        private const float fallbackChairMove = 0.80f;
        private const float maxChairMove = 1.02f;
        private const float feetPushWeight = 0.70f;
        private const float sidePushWeight = 0.62f;

        private const bool playLyingAnimation = true;
        private const string lyingAnimatorStateName = "";
        private const float lyingAnimStartNormalized = 0f;
        private const float lyingAnimSpeed = 1f;
        private const bool forceLyingAnimatorAlwaysAnimate = true;

        private const bool disableMainAnimatorDuringReaction = true;

        private const int missDodgeStopFrame = 50;
        private const bool missUseClipFrameRate = true;
        private const float missManualFrameRate = 30f;
        private const float missScaredBlendDuration = 0.20f;
        private const float missScaredStartNormalizedTime = 0f;
        private const float missScaredPlaybackSpeed = 1f;
        private const float missStartBlendDuration = 0.18f;
        private const float missDodgeStartNormalized = 0.00f;

        private const float missChairRollDelay = 0.02f;
        private const float missChairRollDistance = 0.36f;
        private const float missChairRollSide = 0.05f;
        private const float missChairRollDuration = 0.40f;
        private const float missChairRollYaw = 7.5f;

        private bool busy;
        private bool lastDoHitValue;
        private bool lastDoMissValue;
        private bool lastDoResetValue;
        private bool originalAnimatorEnabled;

        private readonly List<object> _routineHandles = new List<object>();
        private object hitRoutine;
        private object shutterRoutine;
        private object chairClearRoutine;
        private object lyingRevealRoutine;
        private object missChairRollRoutine;

        private object missRoutine;
        private PlayableGraph missGraph;
        private AnimationPlayableOutput missOutput;
        private AnimationClipPlayable missDodgePlayable;
        private AnimationClipPlayable missScaredPlayable;
        private AnimationMixerPlayable missMixer;
        private AnimatorControllerPlayable missBasePlayable;
        private bool missHasBasePlayable;
        private bool missGraphBuilt;

        private Transform originalParent;
        private Vector3 originalLocalPos;
        private Quaternion originalLocalRot;
        private Vector3 originalLocalScale;
        private Vector3 originalChairLocalPos;
        private Quaternion originalChairLocalRot;

        private readonly List<Renderer> liveRenderers = new List<Renderer>();
        private readonly List<Collider> liveColliders = new List<Collider>();
        private readonly List<Behaviour> liveBehavioursToDisable = new List<Behaviour>();

        public bool _triggered = false;
        GlassDriver glass;

        private class BoneLink
        {
            public Transform real;
            public Transform proxy;
            public Quaternion baseLocalRotation;
        }

        private readonly string[] upperBodyBoneNames = new string[]
        {
        "mixamorig:Spine","mixamorig:Spine1","mixamorig:Spine2","mixamorig:Neck","mixamorig:Head",
        "mixamorig:LeftShoulder","mixamorig:LeftArm","mixamorig:LeftForeArm","mixamorig:LeftHand",
        "mixamorig:RightShoulder","mixamorig:RightArm","mixamorig:RightForeArm","mixamorig:RightHand"
        };

        private PlayableGraph fallProxyGraph;
        private AnimationPlayableOutput fallProxyOutput;
        private AnimationClipPlayable fallProxyClipPlayable;
        private bool fallProxyGraphBuilt;
        private GameObject fallProxyRig;
        private Animator fallProxyAnimator;
        private readonly List<BoneLink> upperBodyLinks = new List<BoneLink>();

        private GameObject runtimeLyingGuard;
        private Transform activeLyingRoot;

        private PlayableGraph lyingGraph;
        private AnimationPlayableOutput lyingOutput;
        private AnimationClipPlayable lyingClipPlayable;
        private bool lyingGraphBuilt;
        private Animator runtimeLyingAnimator;

        public GameObject TopShutter;
        public GameObject BottomShutter;
        public AlarmDriver alarm;
        private void Awake()
        {
            if (guardRoot == null) guardRoot = transform;
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            CacheStartPose();
            CacheLiveGuardParts();
        }

        void Start()
        {
            guardRoot = transform;
            chairRoot = GameObject.Find("GuardChair").transform;
            animator = GetComponent<Animator>();
            lyingGuardSource = GameObject.Find("Guard_Lying");
            TopShutter = GameObject.Find("Saferoom_BlastDoorTop");
            BottomShutter = GameObject.Find("Saferoom_BlastDoorBottom");
            lyingAnimationClip = HeistBundle2Manager.GetAnimation("Assets/Guard/Animations/LayingMoaning.anim");
            sittingDodgeClip = HeistBundle2Manager.GetAnimation("Assets/Guard/Animations/Sitting Dodges.anim");
            scaredClip = HeistBundle2Manager.GetAnimation("Assets/Guard/Animations/Scared.anim");
            upperBodyHitClip = HeistBundle2Manager.GetAnimation("Assets/Guard/Animations/Sweep Fall.anim");
            glass = GameObject.Find("Saferoom_Window").GetComponent<GlassDriver>();
            alarm = GameObject.Find("Manager").GetComponent<AlarmDriver>();

            CacheStartPose();
            Log("START guard=" + (guardRoot != null ? guardRoot.name : "null")
                + " chair=" + (chairRoot != null ? chairRoot.name : "null")
                + " chairParent=" + (chairRoot != null && chairRoot.parent != null ? chairRoot.parent.name : "null"));

            if (chairRoot != null)
            {
                Log("START chair world pos=" + chairRoot.position + " rot=" + chairRoot.rotation.eulerAngles);
                Log("START chair local pos=" + chairRoot.localPosition + " rot=" + chairRoot.localRotation.eulerAngles);
                Log("CACHED chair local pos=" + originalChairLocalPos + " rot=" + originalChairLocalRot.eulerAngles);
            }
        }

        private void Update()
        {
            if (doHit && !lastDoHitValue) { ReactToHit(); doHit = false; }
            if (doMiss && !lastDoMissValue) { ReactToMiss(); doMiss = false; }
            if (doReset && !lastDoResetValue) { ResetToStartPose(); doReset = false; }
            lastDoHitValue = doHit;
            lastDoMissValue = doMiss;
            lastDoResetValue = doReset;
        }

        private void OnDestroy()
        {
            DestroyFallProxyOnly();
            DestroyLyingAnimationGraph();
            DestroyMissGraph();
            if (runtimeLyingGuard != null) Destroy(runtimeLyingGuard);
        }









        private void Log(string msg)
        {

        }

        private void Warn(string msg)
        {
            Debug.LogWarning("[GuardReactionDriver] " + msg, this);
        }

        private void CacheStartPose()
        {
            if (guardRoot != null)
            {
                originalParent = guardRoot.parent;
                originalLocalPos = guardRoot.localPosition;
                originalLocalRot = guardRoot.localRotation;
                originalLocalScale = guardRoot.localScale;
            }
            if (chairRoot != null)
            {
                originalChairLocalPos = chairRoot.localPosition;
                originalChairLocalRot = chairRoot.localRotation;
            }
            if (animator != null) originalAnimatorEnabled = animator.enabled;
        }
        public void ReactToHit()
        {
            if (busy) return;
            if (_triggered) return;
            _triggered = true;
            if (guardRoot == null || chairRoot == null)
            {
                Warn("Missing guardRoot or chairRoot.");
                return;
            }
            hitRoutine = StartManagedCoroutine(Co_ReactToHit());
            CloseShutters(2f);
        }

        void CloseShutters(float delay)
        {
            BottomShutter.transform.GetChild(0).gameObject.SetActive(true);
            BottomShutter.transform.GetChild(1).gameObject.SetActive(true);
            TopShutter.transform.GetChild(0).gameObject.SetActive(true);
            TopShutter.transform.GetChild(1).gameObject.SetActive(true);

            HeistLevelManager.TurretDeath();

            shutterRoutine = StartManagedCoroutine(Co_CloseShutters(1f, TopShutter.transform.localPosition, BottomShutter.transform.localPosition, delay));

        }
        [HideFromIl2Cpp]

        private System.Collections.IEnumerator Co_CloseShutters(float closeTime, Vector3 TopPos, Vector3 BottomPos, float waitTime)
        {
            if (waitTime > 0) yield return new WaitForSeconds(waitTime);
            AudioUtil.PlayAt("BlastDoors.ogg", TopShutter.transform.position);
            float elapsed = 0f;
            while (elapsed < closeTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / closeTime);
                BottomShutter.transform.localPosition = Vector3.Lerp(BottomPos, Vector3.zero, t);
                TopShutter.transform.localPosition = Vector3.Lerp(TopPos, Vector3.zero, t);
                yield return null;
            }
            BottomShutter.transform.localPosition = Vector3.zero;
            TopShutter.transform.localPosition = Vector3.zero;
        }

        public void ReactToMiss()
        {
            if (busy) return;
            if (_triggered) return;
            _triggered = true;
            if (guardRoot == null || animator == null)
            {
                Warn("Missing guardRoot or animator.");
                return;
            }
            if (sittingDodgeClip == null || scaredClip == null)
            {
                Warn("Missing sittingDodgeClip or scaredClip.");
                return;
            }
            missRoutine = StartManagedCoroutine(Co_ReactToMiss());
            CloseShutters(2f);
        }
        public void ResetToStartPose()
        {
            busy = false;
            StopAllManagedCoroutines();
            missRoutine = null;
            RestoreUpperBodyBones();
            DestroyFallProxyOnly();
            DestroyLyingAnimationGraph();
            DestroyMissGraph();
            activeLyingRoot = null;

            if (runtimeLyingGuard != null)
            {
                Destroy(runtimeLyingGuard);
                runtimeLyingGuard = null;
            }

            if (animator != null) animator.enabled = originalAnimatorEnabled;

            if (guardRoot != null)
            {
                ShowLiveGuard();
                guardRoot.gameObject.SetActive(true);
                guardRoot.SetParent(originalParent, true);
                guardRoot.localPosition = originalLocalPos;
                guardRoot.localRotation = originalLocalRot;
                guardRoot.localScale = originalLocalScale;
            }
            if (chairRoot != null)
            {
                chairRoot.localPosition = originalChairLocalPos;
                chairRoot.localRotation = originalChairLocalRot;
            }
        }
        [HideFromIl2Cpp]

        private IEnumerator Co_ReactToMiss()
        {
            busy = true;
            Log("ReactToMiss start");

            HideLiveGuard(false);
            ShowLiveGuard();

            DestroyFallProxyOnly();
            DestroyLyingAnimationGraph();
            if (runtimeLyingGuard != null)
            {
                Destroy(runtimeLyingGuard);
                runtimeLyingGuard = null;
            }
            activeLyingRoot = null;

            if (guardRoot != null)
            {
                guardRoot.SetParent(originalParent, true);
                guardRoot.localScale = originalLocalScale;
            }
            if (chairRoot != null)
            {
                chairRoot.localPosition = originalChairLocalPos;
                chairRoot.localRotation = originalChairLocalRot;
            }

            if (animator != null)
            {
                animator.enabled = true;
                animator.Update(0f);
            }

            BuildMissGraph();

            float dodgeStopTime = GetMissDodgeStopTime();
            missDodgePlayable.GetHandle().SetTime((double)(Mathf.Clamp01(missDodgeStartNormalized) * sittingDodgeClip.length));
            missDodgePlayable.GetHandle().SetSpeed(1.0);
            missScaredPlayable.GetHandle().SetTime((double)(Mathf.Clamp01(missScaredStartNormalizedTime) * scaredClip.length));
            missScaredPlayable.GetHandle().SetSpeed(0.0);

            if (missHasBasePlayable)
            {
                missMixer.GetHandle().SetInputWeight(0, 1f);
                missMixer.GetHandle().SetInputWeight(1, 0f);
                missMixer.GetHandle().SetInputWeight(2, 0f);


                missDodgePlayable.GetHandle().SetSpeed(0.0);

                float startBlendT = 0f;
                float startBlendDur = Mathf.Max(0.0001f, missStartBlendDuration);
                while (startBlendT < startBlendDur)
                {
                    startBlendT += Time.deltaTime;
                    float u = Mathf.Clamp01(startBlendT / startBlendDur);
                    float eased = EaseInOutCubic(u);

                    missMixer.GetHandle().SetInputWeight(0, 1f - eased);
                    missMixer.GetHandle().SetInputWeight(1, eased);
                    missMixer.GetHandle().SetInputWeight(2, 0f);

                    float speedRamp = Mathf.Lerp(0.15f, 1f, eased);
                    missDodgePlayable.GetHandle().SetSpeed((double)speedRamp);

                    yield return null;
                }

                missMixer.GetHandle().SetInputWeight(0, 0f);
                missMixer.GetHandle().SetInputWeight(1, 1f);
                missMixer.GetHandle().SetInputWeight(2, 0f);
                missDodgePlayable.GetHandle().SetSpeed(1.0);
            }
            else
            {
                missMixer.GetHandle().SetInputWeight(0, 1f);
                missMixer.GetHandle().SetInputWeight(1, 0f);
            }

            while (missDodgePlayable.GetHandle().IsValid() && missDodgePlayable.GetHandle().GetTime() < dodgeStopTime)
                yield return null;

            double heldTime = Mathf.Min(dodgeStopTime, sittingDodgeClip.length);
            missDodgePlayable.GetHandle().SetTime((double)heldTime);
            missDodgePlayable.GetHandle().SetSpeed(0.0);
            missScaredPlayable.GetHandle().SetSpeed((double)missScaredPlaybackSpeed);

            if (guardRoot != null && guardRoot.parent == chairRoot)
            {
                guardRoot.SetParent(null, true);
                Log("Guard detached from chair for miss/scared transition");
            }

            if (chairRoot != null)
                missChairRollRoutine = StartManagedCoroutine(Co_MissChairRoll());

            float t = 0f;
            float dur = Mathf.Max(0.0001f, missScaredBlendDuration);
            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);
                float eased = EaseInOutCubic(u);

                if (missHasBasePlayable)
                {
                    missMixer.GetHandle().SetInputWeight(0, 0f);
                    missMixer.GetHandle().SetInputWeight(1, 1f - eased);
                    missMixer.GetHandle().SetInputWeight(2, eased);
                }
                else
                {
                    missMixer.GetHandle().SetInputWeight(0, 1f - eased);
                    missMixer.GetHandle().SetInputWeight(1, eased);
                }

                yield return null;
            }

            if (missHasBasePlayable)
            {
                missMixer.GetHandle().SetInputWeight(0, 0f);
                missMixer.GetHandle().SetInputWeight(1, 0f);
                missMixer.GetHandle().SetInputWeight(2, 1f);
            }
            else
            {
                missMixer.GetHandle().SetInputWeight(0, 0f);
                missMixer.GetHandle().SetInputWeight(1, 1f);
            }

            missScaredPlayable.GetHandle().SetSpeed((double)missScaredPlaybackSpeed);

            missRoutine = null;
            busy = false;
            Log("ReactToMiss end");

        }
        [HideFromIl2Cpp]

        private IEnumerator Co_ReactToHit()
        {
            busy = true;
            Log("ReactToHit start");

            if (disableMainAnimatorDuringReaction && animator != null)
                animator.enabled = false;

            BuildFallProxyIfNeeded();

            if (guardRoot.parent != chairRoot)
                guardRoot.SetParent(chairRoot, true);

            Vector3 chairStartPos = chairRoot.localPosition;
            Quaternion chairStartRot = chairRoot.localRotation;

            Vector3 shoveDirWorld = GetShoveDirWorld();
            Vector3 shoveDirLocal = chairRoot.parent != null ? chairRoot.parent.InverseTransformDirection(shoveDirWorld) : shoveDirWorld;
            shoveDirLocal.Normalize();

            Vector3 sideDirLocal = chairRoot.parent != null ? chairRoot.parent.InverseTransformDirection(chairRoot.right) : chairRoot.right;
            sideDirLocal.Normalize();

            Vector3 blastPos = chairStartPos + shoveDirLocal * initialBlastDistance + sideDirLocal * sideShove + Vector3.up * upwardBump;
            Quaternion blastRot = chairStartRot * Quaternion.Euler(-earlyTipDegrees, 0f, 0f);

            Vector3 overshootPos = chairStartPos + shoveDirLocal * (initialBlastDistance + toppleBackwardDistance + backwardOvershoot) + sideDirLocal * sideShove + Vector3.up * (upwardBump * 0.25f);
            Vector3 settlePos = chairStartPos + shoveDirLocal * (initialBlastDistance + toppleBackwardDistance) + sideDirLocal * sideShove;
            Quaternion endRot = originalChairLocalRot * Quaternion.Euler(finalChairLocalEuler);

            float dur = Mathf.Max(0.01f, totalDuration);
            float blastSplit = Mathf.Clamp01(blastDuration / dur);

            bool detached = false;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                float u = Mathf.Clamp01(t);

                if (u < blastSplit)
                {
                    float phaseU = Mathf.Clamp01(u / blastSplit);
                    float eased = EaseOutCubic(phaseU);
                    chairRoot.localPosition = Vector3.Lerp(chairStartPos, blastPos, eased);
                    chairRoot.localRotation = Quaternion.Slerp(chairStartRot, blastRot, eased);
                }
                else
                {
                    float phaseU = Mathf.InverseLerp(blastSplit, 1f, u);
                    if (phaseU < 0.55f)
                    {
                        float posU = phaseU / 0.55f;
                        chairRoot.localPosition = Vector3.Lerp(blastPos, overshootPos, EaseOutCubic(posU));
                    }
                    else
                    {
                        float posU = (phaseU - 0.55f) / 0.45f;
                        chairRoot.localPosition = Vector3.Lerp(overshootPos, settlePos, Mathf.SmoothStep(0f, 1f, posU));
                    }
                    chairRoot.localRotation = Quaternion.Slerp(blastRot, endRot, Mathf.SmoothStep(0f, 1f, phaseU));
                }

                ClampChairToFloor();
                ApplyUpperBodyOverlay(t);

                if (!detached && u >= detachAt)
                {
                    guardRoot.SetParent(null, true);
                    detached = true;
                    Log("Guard detached from chair");
                }

                yield return null;
            }

            chairRoot.localPosition = settlePos;
            chairRoot.localRotation = endRot;
            RestoreUpperBodyBones();

            yield return null;
            yield return Co_SwapToLyingGuard(shoveDirWorld, true);

            DestroyFallProxyOnly();
            busy = false;
            Log("ReactToHit end");
        }
        [HideFromIl2Cpp]

        private IEnumerator Co_SwapToLyingGuard(Vector3 shoveDirWorld, bool startChairClear)
        {
            activeLyingRoot = null;

            if (lyingGuardSource == null)
            {
                Warn("No lyingGuardSource assigned.");
                yield break;
            }

            GameObject lyingGO = cloneLyingGuard ? Instantiate(lyingGuardSource) : lyingGuardSource;
            if (cloneLyingGuard) lyingGO.name = lyingGuardSource.name + "_Runtime";
            else lyingGO.SetActive(true);

            runtimeLyingGuard = lyingGO;
            Transform lyingRoot = lyingGO.transform;
            activeLyingRoot = lyingRoot;

            if (copyLiveScale)
                lyingRoot.localScale = guardRoot.lossyScale;

            float yaw = copyLiveYaw ? guardRoot.eulerAngles.y : lyingRoot.eulerAngles.y;
            lyingRoot.rotation = Quaternion.Euler(0f, yaw + lyingYawOffset, 0f);
            lyingRoot.position = guardRoot.position;

            SkinnedMeshRenderer liveRenderer = guardRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
            SkinnedMeshRenderer lyingRenderer = lyingRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (liveRenderer == null || lyingRenderer == null)
            {
                Warn("Swap failed: missing liveRenderer or lyingRenderer.");
                HideLiveGuard();
                yield break;
            }

            Transform liveHips = FindDeepChild(guardRoot, "mixamorig:Hips");
            Transform lyingHips = FindDeepChild(lyingRoot, "mixamorig:Hips");

            Bounds liveBounds = liveRenderer.bounds;
            Bounds lyingBounds = lyingRenderer.bounds;

            Vector3 forward = shoveDirWorld.sqrMagnitude > 0.0001f ? shoveDirWorld.normalized : guardRoot.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Vector3 targetRootPos = guardRoot.position;
            if (alignLyingUsingHips && liveHips != null && lyingHips != null)
            {
                Vector3 hipsDelta = liveHips.position - lyingHips.position;
                targetRootPos += new Vector3(hipsDelta.x, 0f, hipsDelta.z);
            }
            else
            {
                Vector3 targetCenter = useLiveBoundsCenter ? liveBounds.center : guardRoot.position;
                targetCenter.y = lyingBounds.center.y;
                Vector3 deltaXZ = new Vector3(targetCenter.x - lyingBounds.center.x, 0f, targetCenter.z - lyingBounds.center.z);
                targetRootPos += deltaXZ;
            }

            targetRootPos += forward * lyingForwardOffset + right * lyingRightOffset + Vector3.up * lyingUpOffset;
            lyingRoot.position = targetRootPos;

            if (alignLyingUsingHips && lyingHips != null)
            {
                float hipsYDelta = liveHips != null ? (liveHips.position.y - lyingHips.position.y) : 0f;
                lyingRoot.position += Vector3.up * hipsYDelta;
            }

            lyingBounds = lyingRenderer.bounds;
            float yDelta = lyingGuardFloorY - lyingBounds.min.y;
            lyingRoot.position += Vector3.up * yDelta;
            lyingRoot.position = new Vector3(lyingRoot.position.x, lyingGuardFloorY, lyingRoot.position.z);

            Vector3 finalLyingPos = lyingRoot.position;
            Quaternion finalLyingRot = lyingRoot.rotation;

            lyingGO.SetActive(false);

            Vector3 liveStartPos = guardRoot.position;
            Quaternion liveStartRot = guardRoot.rotation;
            Vector3 liveTargetPos = Vector3.Lerp(liveStartPos, finalLyingPos, liveBlendPositionWeight);
            Quaternion liveTargetRot = Quaternion.Slerp(liveStartRot, finalLyingRot, liveBlendRotationWeight);

            float dur = Mathf.Max(0.01f, liveToLyingBlendDuration);
            float revealTime = dur * lyingRevealNormalized;
            float chairStartTime = forceChairClearAtReveal ? revealTime : dur * chairClearStartDuringSwap;
            bool lyingShown = false;
            bool chairStarted = false;
            bool liveHidden = false;

            Vector3 preAlignPos = Vector3.Lerp(liveStartPos, liveTargetPos, liveToLyingPreAlign);
            Quaternion preAlignRot = Quaternion.Slerp(liveStartRot, liveTargetRot, liveToLyingPreAlign);
            preAlignPos += Vector3.down * swapDownwardBias;

            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);

                if (!lyingShown && t >= revealTime)
                {
                    lyingGO.SetActive(true);
                    StartLyingAnimation(lyingGO);
                    lyingShown = true;
                    Log("Lying guard revealed");
                    lyingRevealRoutine = StartManagedCoroutine(Co_AnimateLyingIntoFinal(lyingRoot, finalLyingPos, finalLyingRot, lyingRenderer, shoveDirWorld));

                    if (hideLiveGuardAtReveal)
                    {
                        HideLiveGuard();
                        liveHidden = true;
                        Log("Live guard hidden at reveal");
                    }
                }

                if (startChairClear && !chairStarted && t >= chairStartTime && activeLyingRoot != null)
                {
                    chairStarted = true;
                    Log("Starting chair clear");
                    chairClearRoutine = StartManagedCoroutine(Co_ClearChair(activeLyingRoot));
                }

                if (!liveHidden)
                {
                    float revealBlendPoint = Mathf.Clamp01(revealTime / dur);
                    float firstPhaseEnd = Mathf.Max(0.001f, revealBlendPoint);

                    if (u <= firstPhaseEnd)
                    {
                        float phaseU = Mathf.Clamp01(u / firstPhaseEnd);
                        float eased = EaseInOutCubic(phaseU);
                        Vector3 p = Vector3.Lerp(liveStartPos, preAlignPos, eased);
                        p += Vector3.up * (Mathf.Sin(phaseU * 3.14159274f) * liveToLyingArcHeight);
                        guardRoot.position = p;
                        guardRoot.rotation = Quaternion.Slerp(liveStartRot, preAlignRot, eased);
                    }
                    else
                    {
                        float phaseU = Mathf.Clamp01((u - firstPhaseEnd) / Mathf.Max(0.0001f, 1f - firstPhaseEnd));
                        float eased = EaseOutCubic(phaseU);
                        Vector3 p = Vector3.Lerp(preAlignPos, liveTargetPos, eased);
                        p += Vector3.up * (Mathf.Sin((1f - phaseU) * 3.14159274f * 0.5f) * liveToLyingArcHeight * 0.10f);
                        guardRoot.position = p;
                        guardRoot.rotation = Quaternion.Slerp(preAlignRot, liveTargetRot, eased);
                    }
                }

                yield return null;
            }

            if (!lyingShown)
            {
                lyingGO.SetActive(true);
                StartLyingAnimation(lyingGO);
                Log("Lying guard revealed");
                yield return Co_AnimateLyingIntoFinal(lyingRoot, finalLyingPos, finalLyingRot, lyingRenderer, shoveDirWorld);
            }

            if (!chairStarted && startChairClear && activeLyingRoot != null)
            {
                chairStarted = true;
                Log("Starting chair clear");
                chairClearRoutine = StartManagedCoroutine(Co_ClearChair(activeLyingRoot));
            }

            if (!liveHidden)
            {
                if (finalLiveFadeFrameDelay > 0f)
                    yield return new WaitForSeconds(finalLiveFadeFrameDelay);
                else
                    yield return null;

                HideLiveGuard();
                Log("Live guard hidden without disabling script host");
            }
        }
        [HideFromIl2Cpp]

        private IEnumerator Co_ClearChair(Transform lyingRoot)
        {
            if (chairRoot == null)
            {
                Warn("Chair clear failed: chairRoot is null.");
                yield break;
            }
            if (lyingRoot == null)
            {
                Warn("Chair clear failed: lyingRoot is null.");
                yield break;
            }

            SkinnedMeshRenderer bodyRenderer = lyingRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Renderer[] chairRenderers = chairRoot.GetComponentsInChildren<Renderer>(true);

            Transform hips = FindDeepChild(lyingRoot, "mixamorig:Hips");
            Transform leftFoot = FindDeepChild(lyingRoot, "mixamorig:LeftFoot");
            Transform rightFoot = FindDeepChild(lyingRoot, "mixamorig:RightFoot");

            Vector3 bodyCenter;
            Bounds bodyBounds;
            if (bodyRenderer != null)
            {
                bodyBounds = bodyRenderer.bounds;
                bodyCenter = bodyBounds.center;
            }
            else
            {
                bodyCenter = hips != null ? hips.position : lyingRoot.position;
                bodyBounds = new Bounds(bodyCenter, Vector3.one * 0.6f);
            }

            Bounds chairBounds;
            Vector3 chairCenter;
            if (chairRenderers != null && chairRenderers.Length > 0)
            {
                chairBounds = chairRenderers[0].bounds;
                for (int i = 1; i < chairRenderers.Length; i++) chairBounds.Encapsulate(chairRenderers[i].bounds);
                chairCenter = chairBounds.center;
            }
            else
            {
                chairCenter = chairRoot.position;
                chairBounds = new Bounds(chairCenter, Vector3.one * 0.6f);
            }

            Vector3 feetCenter;
            if (leftFoot != null && rightFoot != null) feetCenter = (leftFoot.position + rightFoot.position) * 0.5f;
            else if (leftFoot != null) feetCenter = leftFoot.position;
            else if (rightFoot != null) feetCenter = rightFoot.position;
            else feetCenter = lyingRoot.position + lyingRoot.forward;

            Vector3 feetDir = Vector3.ProjectOnPlane(feetCenter - bodyCenter, Vector3.up);
            if (feetDir.sqrMagnitude < 0.0001f) feetDir = Vector3.ProjectOnPlane(lyingRoot.forward, Vector3.up);
            if (feetDir.sqrMagnitude < 0.0001f) feetDir = Vector3.right;
            feetDir.Normalize();

            Vector3 bodyToChair = Vector3.ProjectOnPlane(chairCenter - bodyCenter, Vector3.up);
            if (bodyToChair.sqrMagnitude < 0.0001f) bodyToChair = feetDir;
            bodyToChair.Normalize();

            float sideSign = Mathf.Sign(Vector3.Dot(bodyToChair, Vector3.Cross(Vector3.up, feetDir)));
            if (Mathf.Approximately(sideSign, 0f)) sideSign = 1f;

            Vector3 sideDir = Vector3.Cross(Vector3.up, feetDir).normalized * sideSign;
            Vector3 pushDir = (feetDir * feetPushWeight) + (sideDir * sidePushWeight);
            pushDir = Vector3.ProjectOnPlane(pushDir, Vector3.up).normalized;

            float bodyHalf = ProjectedHalfExtent(bodyBounds, pushDir);
            float chairHalf = ProjectedHalfExtent(chairBounds, pushDir);
            float currentSep = Vector3.Dot(chairCenter - bodyCenter, pushDir);
            float requiredSep = bodyHalf + chairHalf + chairClearMargin;

            float move = Mathf.Max(chairMinMove, requiredSep - currentSep);
            move += chairImpactExtraMove;
            move = Mathf.Max(move, fallbackChairMove);
            move = Mathf.Min(move, maxChairMove);

            Vector3 startPos = chairRoot.position;
            Quaternion startRot = chairRoot.rotation;

            Vector3 endPos = startPos + pushDir * move;
            Vector3 c1 = startPos + pushDir * (move * 0.30f) + Vector3.up * chairClearLift;
            Vector3 c2 = startPos + pushDir * (move * 0.80f) + Vector3.up * (chairClearLift * 0.20f);

            Quaternion midRot = startRot * Quaternion.Euler(3f, chairClearYaw * 0.50f, 0f);
            Quaternion endRot = startRot * Quaternion.Euler(1.5f, chairClearYaw, 0f);

            Log("ChairClear pushDir=" + pushDir + " move=" + move);

            float dur = Mathf.Max(0.01f, chairClearDuration);
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);
                float eased = Mathf.SmoothStep(0f, 1f, u);

                Vector3 p01 = Vector3.Lerp(startPos, c1, eased);
                Vector3 p12 = Vector3.Lerp(c1, c2, eased);
                Vector3 p23 = Vector3.Lerp(c2, endPos, eased);
                Vector3 p012 = Vector3.Lerp(p01, p12, eased);
                Vector3 p123 = Vector3.Lerp(p12, p23, eased);
                Vector3 bez = Vector3.Lerp(p012, p123, eased);

                chairRoot.position = bez;
                ClampChairToFloor();
                chairRoot.rotation = Quaternion.Slerp(startRot, endRot, eased);
                yield return null;
            }

            Vector3 settleStartPos = chairRoot.position;
            Quaternion settleStartRot = chairRoot.rotation;
            Vector3 settleEndPos = endPos + pushDir * 0.02f;
            Quaternion settleEndRot = endRot * Quaternion.Euler(0.25f, 0.6f, 0f);

            float settleDur = Mathf.Max(0.01f, chairSettleDuration);
            t = 0f;
            while (t < settleDur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / settleDur);
                float eased = EaseOutCubic(u);
                chairRoot.position = Vector3.Lerp(settleStartPos, settleEndPos, eased);
                ClampChairToFloor();
                chairRoot.rotation = Quaternion.Slerp(settleStartRot, settleEndRot, eased);
                yield return null;
            }

            chairRoot.position = settleEndPos;
            ClampChairToFloor();
            chairRoot.rotation = settleEndRot;
        }

        private static float ProjectedHalfExtent(Bounds b, Vector3 dir)
        {
            dir = dir.normalized;
            Vector3 e = b.extents;
            return Mathf.Abs(dir.x) * e.x + Mathf.Abs(dir.y) * e.y + Mathf.Abs(dir.z) * e.z;
        }

        private void ClampRendererToFloor(Transform root, Renderer r, float floor, float clearance)
        {
            if (root == null || r == null) return;
            float lift = (floor + clearance) - r.bounds.min.y;
            if (lift > 0f) root.position += Vector3.up * lift;
        }

        private void ClampChairToFloor()
        {
            if (chairRoot == null) return;
            Renderer[] rs = chairRoot.GetComponentsInChildren<Renderer>(true);
            if (rs == null || rs.Length == 0)
            {
                Vector3 p = chairRoot.position;
                if (p.y < floorY + chairFloorClearance)
                {
                    p.y = floorY + chairFloorClearance;
                    chairRoot.position = p;
                }
                return;
            }

            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            float lift = (floorY + chairFloorClearance) - b.min.y;
            if (lift > 0f) chairRoot.position += Vector3.up * lift;
        }
        [HideFromIl2Cpp]

        private IEnumerator Co_AnimateLyingIntoFinal(Transform lyingRoot, Vector3 finalPos, Quaternion finalRot, Renderer lyingRenderer, Vector3 travelDir)
        {
            if (lyingRoot == null) yield break;

            Vector3 planarTravel = Vector3.ProjectOnPlane(travelDir, Vector3.up);
            if (planarTravel.sqrMagnitude < 0.0001f) planarTravel = Vector3.forward;
            planarTravel.Normalize();

            Vector3 startPos = finalPos - planarTravel * lyingRevealBackOffset + Vector3.up * lyingRevealLift;
            Quaternion startRot = finalRot * Quaternion.Euler(-4f, 0f, 0f);

            lyingRoot.position = new Vector3(startPos.x, Mathf.Max(startPos.y, lyingGuardFloorY), startPos.z);
            lyingRoot.rotation = startRot;

            float dur = Mathf.Max(0.01f, lyingSettleDuration);
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);
                float eased = Mathf.SmoothStep(0f, 1f, u);
                Vector3 p = Vector3.Lerp(startPos, finalPos, eased);
                p.y = Mathf.Max(p.y, lyingGuardFloorY);
                lyingRoot.position = p;
                lyingRoot.rotation = Quaternion.Slerp(startRot, finalRot, eased);
                yield return null;
            }

            lyingRoot.position = new Vector3(finalPos.x, lyingGuardFloorY, finalPos.z);
            lyingRoot.rotation = finalRot;
        }

        private void CacheLiveGuardParts()
        {
            liveRenderers.Clear();
            liveColliders.Clear();
            liveBehavioursToDisable.Clear();

            if (guardRoot == null) return;

            liveRenderers.AddRange(guardRoot.GetComponentsInChildren<Renderer>(true));
            liveColliders.AddRange(guardRoot.GetComponentsInChildren<Collider>(true));

            var behaviours = guardRoot.GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                Behaviour b = behaviours[i];
                if (b == null) continue;
                if (b == this) continue;
                if (b == animator) continue;
                if (b is Renderer) continue;
                if (b is Collider) continue;
                liveBehavioursToDisable.Add(b);
            }
        }

        private void DestroyLyingAnimationGraph()
        {
            if (lyingGraphBuilt)
            {
                lyingGraph.Destroy();
                lyingGraphBuilt = false;
            }

            runtimeLyingAnimator = null;
        }

        private void StartLyingAnimation(GameObject lyingGO)
        {
            if (!playLyingAnimation || lyingGO == null)
                return;

            Animator lyingAnimator = lyingGO.GetComponent<Animator>();
            if (lyingAnimator == null) lyingAnimator = lyingGO.GetComponentInChildren<Animator>(true);

            if (lyingAnimator == null)
            {
                Warn("Lying guard has no Animator, so it can only use a static pose.");
                return;
            }

            runtimeLyingAnimator = lyingAnimator;
            lyingAnimator.enabled = true;
            lyingAnimator.applyRootMotion = false;
            lyingAnimator.speed = Mathf.Max(0f, lyingAnimSpeed);

            if (forceLyingAnimatorAlwaysAnimate)
                lyingAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            DestroyLyingAnimationGraph();

            if (lyingAnimationClip != null)
            {
                lyingGraph = PlayableGraph.Create("GuardReaction_LyingGraph_" + gameObject.name);
                lyingGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

                lyingOutput = AnimationPlayableOutput.Create(lyingGraph, "Animation", lyingAnimator);
                lyingClipPlayable = AnimationClipPlayable.Create(lyingGraph, lyingAnimationClip);
                lyingClipPlayable.SetApplyFootIK(false);
                lyingClipPlayable.GetHandle().SetSpeed((double)Mathf.Max(0f, lyingAnimSpeed));
                lyingOutput.GetHandle().SetSourcePlayable(lyingClipPlayable.GetHandle(), 0);

                double startTime = Mathf.Clamp01(lyingAnimStartNormalized) * lyingAnimationClip.length;
                lyingClipPlayable.GetHandle().SetTime((double)startTime);

                lyingGraph.Play();
                lyingGraph.Evaluate(0f);
                lyingGraphBuilt = true;
                return;
            }

            if (lyingAnimator.runtimeAnimatorController == null)
            {
                Warn("Lying guard Animator has no controller and no lyingAnimationClip was assigned.");
                return;
            }

            lyingAnimator.Rebind();
            lyingAnimator.Update(0f);

            if (!string.IsNullOrEmpty(lyingAnimatorStateName))
            {
                lyingAnimator.Play(lyingAnimatorStateName, 0, Mathf.Clamp01(lyingAnimStartNormalized));
                lyingAnimator.Update(0f);
            }
        }
        [HideFromIl2Cpp]

        private IEnumerator Co_MissChairRoll()
        {
            if (chairRoot == null)
                yield break;

            if (missChairRollDelay > 0f)
                yield return new WaitForSeconds(missChairRollDelay);

            Vector3 startPos = chairRoot.position;
            Quaternion startRot = chairRoot.rotation;

            Vector3 rollBack = Vector3.ProjectOnPlane(-guardRoot.forward, Vector3.up);
            if (rollBack.sqrMagnitude < 0.0001f)
                rollBack = Vector3.ProjectOnPlane(-chairRoot.forward, Vector3.up);
            if (rollBack.sqrMagnitude < 0.0001f)
                rollBack = Vector3.left;
            rollBack.Normalize();

            Vector3 sideDir = Vector3.Cross(Vector3.up, rollBack).normalized;
            float sideSign = Vector3.Dot(sideDir, chairRoot.position - guardRoot.position) >= 0f ? 1f : -1f;
            sideDir *= sideSign;

            Vector3 endPos = startPos + rollBack * missChairRollDistance + sideDir * missChairRollSide;
            Quaternion endRot = startRot * Quaternion.Euler(0f, missChairRollYaw * sideSign, 0f);

            float t = 0f;
            float dur = Mathf.Max(0.01f, missChairRollDuration);
            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);
                float eased = EaseOutCubic(u);

                chairRoot.position = Vector3.Lerp(startPos, endPos, eased);
                chairRoot.rotation = Quaternion.Slerp(startRot, endRot, eased);
                ClampChairToFloor();

                yield return null;
            }

            chairRoot.position = endPos;
            chairRoot.rotation = endRot;
            ClampChairToFloor();
        }

        private void BuildMissGraph()
        {
            DestroyMissGraph();

            missGraph = PlayableGraph.Create("GuardReactionDriver_MissGraph");
            missGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            missOutput = AnimationPlayableOutput.Create(missGraph, "MissAnimation", animator);

            missHasBasePlayable = animator != null && animator.runtimeAnimatorController != null;
            int inputCount = missHasBasePlayable ? 3 : 2;

            if (missHasBasePlayable)
            {
                missBasePlayable = AnimatorControllerPlayable.Create(missGraph, animator.runtimeAnimatorController);
                missBasePlayable.GetHandle().SetSpeed(1.0);
            }

            missDodgePlayable = AnimationClipPlayable.Create(missGraph, sittingDodgeClip);
            missDodgePlayable.SetApplyFootIK(false);
            missDodgePlayable.SetApplyPlayableIK(false);

            missScaredPlayable = AnimationClipPlayable.Create(missGraph, scaredClip);
            missScaredPlayable.SetApplyFootIK(false);
            missScaredPlayable.SetApplyPlayableIK(false);

            missMixer = AnimationMixerPlayable.Create(missGraph, inputCount, true);

            int nextInput = 0;
            if (missHasBasePlayable)
            {
                missGraph.Connect(new Playable(missBasePlayable.GetHandle()), 0, new Playable(missMixer.GetHandle()), nextInput);
                missMixer.GetHandle().SetInputWeight(nextInput, 1f);
                nextInput++;
            }

            missGraph.Connect(new Playable(missDodgePlayable.GetHandle()), 0, new Playable(missMixer.GetHandle()), nextInput);
            missMixer.GetHandle().SetInputWeight(nextInput, missHasBasePlayable ? 0f : 1f);
            nextInput++;

            missGraph.Connect(new Playable(missScaredPlayable.GetHandle()), 0, new Playable(missMixer.GetHandle()), nextInput);
            missMixer.GetHandle().SetInputWeight(nextInput, 0f);

            missOutput.GetHandle().SetSourcePlayable(missMixer.GetHandle(), 0);
            missGraph.Play();
            missGraphBuilt = true;
        }

        private void DestroyMissGraph()
        {
            if (missGraphBuilt && missGraph.IsValid())
                missGraph.Destroy();

            missHasBasePlayable = false;
            missGraphBuilt = false;
        }

        private float GetMissDodgeStopTime()
        {
            float fps = (missUseClipFrameRate && sittingDodgeClip != null && sittingDodgeClip.frameRate > 0.01f)
                ? sittingDodgeClip.frameRate
                : Mathf.Max(1f, missManualFrameRate);

            float stopTime = missDodgeStopFrame / fps;
            if (sittingDodgeClip != null)
                stopTime = Mathf.Clamp(stopTime, 0f, Mathf.Max(0.01f, sittingDodgeClip.length));

            return stopTime;
        }

        private void HideLiveGuard(bool keepScriptHostActive = true)
        {
            if (animator != null) animator.enabled = false;

            for (int i = 0; i < liveRenderers.Count; i++)
                if (liveRenderers[i] != null) liveRenderers[i].enabled = false;

            for (int i = 0; i < liveColliders.Count; i++)
                if (liveColliders[i] != null) liveColliders[i].enabled = false;

            for (int i = 0; i < liveBehavioursToDisable.Count; i++)
                if (liveBehavioursToDisable[i] != null) liveBehavioursToDisable[i].enabled = false;
        }

        private void ShowLiveGuard()
        {
            for (int i = 0; i < liveRenderers.Count; i++)
                if (liveRenderers[i] != null) liveRenderers[i].enabled = true;

            for (int i = 0; i < liveColliders.Count; i++)
                if (liveColliders[i] != null) liveColliders[i].enabled = true;

            for (int i = 0; i < liveBehavioursToDisable.Count; i++)
                if (liveBehavioursToDisable[i] != null) liveBehavioursToDisable[i].enabled = true;

            if (animator != null) animator.enabled = originalAnimatorEnabled;
        }

        private void BuildFallProxyIfNeeded()
        {
            RestoreUpperBodyBones();
            DestroyFallProxyOnly();
            upperBodyLinks.Clear();

            if (upperBodyHitClip == null || guardRoot == null) return;

            fallProxyRig = Instantiate(guardRoot.gameObject);
            fallProxyRig.name = guardRoot.name + "_FallProxy";
            fallProxyRig.transform.position = new Vector3(9999f, 9999f, 9999f);
            fallProxyRig.transform.rotation = guardRoot.rotation;
            fallProxyRig.transform.localScale = guardRoot.lossyScale;

            var proxySelf = fallProxyRig.GetComponent<GuardReactionDriver>();
            if (proxySelf != null) Destroy(proxySelf);

            foreach (var r in fallProxyRig.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (var c in fallProxyRig.GetComponentsInChildren<Collider>(true)) c.enabled = false;

            fallProxyAnimator = fallProxyRig.GetComponent<Animator>();
            if (fallProxyAnimator == null) fallProxyAnimator = fallProxyRig.GetComponentInChildren<Animator>();
            if (fallProxyAnimator == null) return;

            fallProxyAnimator.enabled = true;
            fallProxyGraph = PlayableGraph.Create("GuardReactionFallProxyGraph_" + gameObject.name);
            fallProxyGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            fallProxyOutput = AnimationPlayableOutput.Create(fallProxyGraph, "Animation", fallProxyAnimator);
            fallProxyClipPlayable = AnimationClipPlayable.Create(fallProxyGraph, upperBodyHitClip);
            fallProxyClipPlayable.SetApplyFootIK(false);
            fallProxyOutput.GetHandle().SetSourcePlayable(fallProxyClipPlayable.GetHandle(), 0);
            fallProxyGraph.Play();
            fallProxyGraphBuilt = true;

            foreach (string boneName in upperBodyBoneNames)
            {
                Transform realBone = FindDeepChild(guardRoot, boneName);
                Transform proxyBone = FindDeepChild(fallProxyRig.transform, boneName);
                if (realBone == null || proxyBone == null) continue;

                upperBodyLinks.Add(new BoneLink
                {
                    real = realBone,
                    proxy = proxyBone,
                    baseLocalRotation = realBone.localRotation
                });
            }
        }

        private void ApplyUpperBodyOverlay(float elapsed)
        {
            if (!fallProxyGraphBuilt || upperBodyHitClip == null || upperBodyLinks.Count == 0) return;

            float segStart = Mathf.Clamp01(clipStartNormalized);
            float segEnd = Mathf.Clamp01(clipEndNormalized);
            if (segEnd <= segStart) segEnd = Mathf.Min(1f, segStart + 0.05f);

            float clipSegDuration = Mathf.Max(0.01f, (segEnd - segStart) * upperBodyHitClip.length);
            float totalAnimDur = sampledAnimDuration > 0f ? sampledAnimDuration : clipSegDuration;

            if (elapsed < clipStartDelay) { RestoreUpperBodyBones(); return; }

            float localT = elapsed - clipStartDelay;
            if (localT > totalAnimDur) { RestoreUpperBodyBones(); return; }

            float u = Mathf.Clamp01(localT / totalAnimDur);
            float weight = 1f;
            if (clipBlendIn > 0f) weight *= Mathf.Clamp01(localT / clipBlendIn);
            float timeRemaining = totalAnimDur - localT;
            if (clipBlendOut > 0f) weight *= Mathf.Clamp01(timeRemaining / clipBlendOut);
            weight = Mathf.Clamp01(weight * maxUpperBodyWeight);

            double clipTime = Mathf.Lerp(segStart * upperBodyHitClip.length, segEnd * upperBodyHitClip.length, Mathf.Min(u, 0.96f));
            fallProxyClipPlayable.GetHandle().SetTime((double)clipTime);
            fallProxyGraph.Evaluate(0f);

            for (int i = 0; i < upperBodyLinks.Count; i++)
            {
                BoneLink link = upperBodyLinks[i];
                if (link.real == null || link.proxy == null) continue;
                link.real.localRotation = Quaternion.Slerp(link.baseLocalRotation, link.proxy.localRotation, weight);
            }
        }

        private void RestoreUpperBodyBones()
        {
            for (int i = 0; i < upperBodyLinks.Count; i++)
            {
                BoneLink link = upperBodyLinks[i];
                if (link.real != null) link.real.localRotation = link.baseLocalRotation;
            }
        }

        private void DestroyFallProxyOnly()
        {
            if (fallProxyGraphBuilt) { fallProxyGraph.Destroy(); fallProxyGraphBuilt = false; }
            if (fallProxyRig != null) Destroy(fallProxyRig);
            fallProxyRig = null;
            fallProxyAnimator = null;
            upperBodyLinks.Clear();
        }

        private Vector3 GetShoveDirWorld()
        {
            if (manualWorldShoveDirection.sqrMagnitude > 0.0001f)
                return manualWorldShoveDirection.normalized;

            Vector3 dir = useNegativeForward ? -chairRoot.forward : chairRoot.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.back;
            return dir.normalized;
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeepChild(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        private static float EaseInOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        private object StartManagedCoroutine(IEnumerator routine)
        {
            if (routine == null) return null;
            object handle = MelonCoroutines.Start(routine);
            if (handle != null) _routineHandles.Add(handle);
            return handle;
        }

        private void StopManagedCoroutine(ref object handle)
        {
            if (handle == null) return;
            try { MelonCoroutines.Stop(handle); } catch { }
            _routineHandles.Remove(handle);
            handle = null;
        }

        private void StopAllManagedCoroutines()
        {
            for (int i = _routineHandles.Count - 1; i >= 0; i--)
            {
                object handle = _routineHandles[i];
                if (handle == null) continue;
                try { MelonCoroutines.Stop(handle); } catch { }
            }
            _routineHandles.Clear();
            hitRoutine = null;
            shutterRoutine = null;
            chairClearRoutine = null;
            lyingRevealRoutine = null;
            missChairRollRoutine = null;
            missRoutine = null;
        }

    }
}
