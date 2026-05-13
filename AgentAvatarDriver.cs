using System;
using System.Collections;
using MelonLoader;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class AgentAvatarDriver : MonoBehaviour
    {
        public AgentAvatarDriver(IntPtr ptr) : base(ptr) { }
        public AgentAvatarDriver() : base(ClassInjector.DerivedConstructorPointer<AgentAvatarDriver>())
            => ClassInjector.DerivedConstructorBody(this);

        public Animator animator;
        public Transform hmd;
        public Transform leftController;
        public Transform rightController;
        public Transform vrRig;
        public Transform bodyRoot;
        public Transform rootToMove;
        public Transform headBoneOverride;

        public float handPositionWeight = 1f;
        public float handRotationWeight = 0f;
        public float elbowHintWeight = 0.85f;
        public float headRotationWeight = 0.75f;

        public float positionScale = 1.05f;
        public float positionSmooth = 18f;
        public float rotationSmooth = 14f;
        public float headRotationSmooth = 12f;

        public float minReachFactor = 0.20f;
        public float maxReachFactor = 0.96f;
        public float sideMin = 0.08f;
        public float sideMax = 0.70f;
        public float verticalMin = -0.68f;
        public float verticalMax = 0.82f;
        public float forwardMin = -0.25f;
        public float forwardMax = 0.95f;

        public bool followHeadRotation = true;
        public bool driveHandRotation = false;
        public bool debugLogs = true;
        public bool debugDraw = false;

        public Vector3 leftHandPositionOffset = new Vector3(0f, 0f, 0.02f);
        public Vector3 rightHandPositionOffset = new Vector3(0f, 0f, 0.02f);
        public Vector3 leftHandRotationOffsetEuler = Vector3.zero;
        public Vector3 rightHandRotationOffsetEuler = Vector3.zero;

        public string hitBodyTrigger = "HitBody";
        public string hitHeadTrigger = "HitHead";
        public float minHitSpeed = 0.65f;
        public float hitCooldown = 0.35f;
        public float aboveDotThreshold = 0.94f;
        public float hitReactionDuration = 0.45f;
        public bool pauseIkDuringHit = true;

        bool _ready;
        bool _neutralPoseCached;
        bool _avatarOn;

        Transform _headBone;
        Transform _leftShoulder;
        Transform _rightShoulder;
        Transform _leftUpperArm;
        Transform _rightUpperArm;
        Transform _leftLowerArm;
        Transform _rightLowerArm;
        Transform _leftHandBone;
        Transform _rightHandBone;

        Vector3 _startRootPosition;
        Quaternion _startRootRotation = Quaternion.identity;
        Vector3 _startBodyPosition;
        Quaternion _startBodyRotation = Quaternion.identity;

        Vector3 _neutralHeadLocal;
        Quaternion _neutralHeadLocalRotation = Quaternion.identity;
        Vector3 _neutralLeftShoulderLocal;
        Vector3 _neutralRightShoulderLocal;
        Vector3 _neutralLeftHandLocal;
        Vector3 _neutralRightHandLocal;
        Quaternion _neutralLeftHandRotation = Quaternion.identity;
        Quaternion _neutralRightHandRotation = Quaternion.identity;

        float _leftArmLength = 0.65f;
        float _rightArmLength = 0.65f;

        Vector3 _leftHandTarget;
        Vector3 _rightHandTarget;
        Quaternion _leftHandRotationTarget = Quaternion.identity;
        Quaternion _rightHandRotationTarget = Quaternion.identity;
        Quaternion _headRotationTarget = Quaternion.identity;
        Vector3 _leftElbowHint;
        Vector3 _rightElbowHint;

        object _hitRoutine;
        float _lastHitTime = -999f;

        void Awake()
        {
            Initialize();
            TurnOff();
        }

        void LateUpdate()
        {
            if (!_avatarOn)
                return;

            if (!HasAvatarSetup())
                return;

            HoldBodyInPlace();
            UpdateHandTargets(Time.deltaTime);
            UpdateHeadRotation(Time.deltaTime);
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (!_avatarOn || animator == null || !animator.isHuman)
                return;

            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, handPositionWeight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, _leftHandTarget);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, driveHandRotation ? handRotationWeight : 0f);
            if (driveHandRotation)
                animator.SetIKRotation(AvatarIKGoal.LeftHand, _leftHandRotationTarget);

            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, handPositionWeight);
            animator.SetIKPosition(AvatarIKGoal.RightHand, _rightHandTarget);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, driveHandRotation ? handRotationWeight : 0f);
            if (driveHandRotation)
                animator.SetIKRotation(AvatarIKGoal.RightHand, _rightHandRotationTarget);

            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, elbowHintWeight);
            animator.SetIKHintPosition(AvatarIKHint.LeftElbow, _leftElbowHint);

            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, elbowHintWeight);
            animator.SetIKHintPosition(AvatarIKHint.RightElbow, _rightElbowHint);
        }

        public void TurnOn()
        {
            Initialize();

            if (!HasAvatarSetup())
            {
                Log("TurnOn aborted: missing avatar setup.");
                return;
            }

            gameObject.SetActive(true);
            animator.enabled = true;

            CacheStationaryPose();
            ResetAnimatorPose();
            CacheNeutralPose();

            _avatarOn = true;
            RefreshTargetsFromControllers();
            LogNeutralPose();
        }

        public void TurnOff()
        {
            _avatarOn = false;
            ClearIKWeights();
            ResetAnimatorPose();
            HoldBodyInPlace();
            gameObject.SetActive(false);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!_avatarOn || animator == null)
                return;

            if (Time.time - _lastHitTime < hitCooldown)
                return;

            Vector3 relativeVelocity = collision.relativeVelocity;
            float speed = relativeVelocity.magnitude;
            if (speed < minHitSpeed)
                return;

            Vector3 impactDirection = relativeVelocity / speed;
            float aboveDot = Vector3.Dot(impactDirection, -transform.up);
            bool hitFromAbove = aboveDot >= aboveDotThreshold;

            PlayHitReaction(
                hitFromAbove ? hitHeadTrigger : hitBodyTrigger,
                hitFromAbove ? "above" : "side",
                collision.gameObject.name,
                speed,
                aboveDot
            );
        }

        public void NotifyDroneHandLaunchHit(Vector3 incomingWorldDirection, float speed, string otherName)
        {
            if (!_avatarOn || animator == null)
                return;

            if (Time.time - _lastHitTime < hitCooldown || speed < minHitSpeed)
                return;

            Vector3 impactDirection = incomingWorldDirection;
            if (impactDirection.sqrMagnitude < 0.000001f)
                impactDirection = transform.forward;
            impactDirection.Normalize();

            float aboveDot = Vector3.Dot(impactDirection, -transform.up);
            bool hitFromAbove = aboveDot >= aboveDotThreshold;

            PlayHitReaction(
                hitFromAbove ? hitHeadTrigger : hitBodyTrigger,
                hitFromAbove ? "drone-above" : "drone-side",
                otherName,
                speed,
                aboveDot
            );
        }

        void PlayHitReaction(string triggerName, string hitType, string otherName, float speed, float aboveDot)
        {
            if (string.IsNullOrEmpty(triggerName) || triggerName.Contains("Bolt"))
                return;

            _lastHitTime = Time.time;

            if (_hitRoutine != null)
                MelonCoroutines.Stop(_hitRoutine);

            _hitRoutine = MelonCoroutines.Start(PlayHitReactionRoutine(triggerName));

            DamageOverlayDriver.TriggerHit(1.5f);
            AudioUtil.PlayAt("PlayerGrunt.mp3", transform.position, 0.7f);

            if (!SaveManager.HasSouvenir(5))
                HeistLevelManager.FoundSouvenir(5);

            Log("HitReaction " + hitType + " trigger=" + triggerName + " other=" + otherName + " speed=" + speed.ToString("F2") + " aboveDot=" + aboveDot.ToString("F2"));
        }

        IEnumerator PlayHitReactionRoutine(string triggerName)
        {
            bool restoreAvatar = _avatarOn;

            if (pauseIkDuringHit)
            {
                _avatarOn = false;
                ClearIKWeights();
            }

            animator.ResetTrigger(hitBodyTrigger);
            animator.ResetTrigger(hitHeadTrigger);
            animator.SetTrigger(triggerName);

            float endTime = Time.time + Mathf.Max(0.01f, hitReactionDuration);
            while (Time.time < endTime)
            {
                HoldBodyInPlace();
                yield return null;
            }

            if (restoreAvatar && pauseIkDuringHit)
            {
                RefreshTargetsFromControllers();
                _avatarOn = true;
            }

            _hitRoutine = null;
        }

        void ClearIKWeights()
        {
            if (animator == null)
                return;

            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0f);
        }

        void RefreshTargetsFromControllers()
        {
            if (!HasAvatarSetup())
                return;

            _leftHandTarget = GetDesiredHandPosition(true);
            _rightHandTarget = GetDesiredHandPosition(false);
            _leftHandRotationTarget = GetDesiredHandRotation(true);
            _rightHandRotationTarget = GetDesiredHandRotation(false);
            _leftElbowHint = GetElbowHint(true, _leftHandTarget);
            _rightElbowHint = GetElbowHint(false, _rightHandTarget);
            _headRotationTarget = _headBone != null ? _headBone.rotation : Quaternion.identity;
        }

        void Initialize()
        {
            if (_ready)
                return;

            GameObject hmdObject = GameObject.Find("HMD");
            GameObject leftHandObject = GameObject.Find("LeftHandRoot");
            GameObject rightHandObject = GameObject.Find("RightHandRoot");
            GameObject rigObject = GameObject.Find("VRRig");
            GameObject modelObject = GameObject.Find("PlayerModel");

            if (hmdObject != null) hmd = hmdObject.transform;
            if (leftHandObject != null) leftController = leftHandObject.transform;
            if (rightHandObject != null) rightController = rightHandObject.transform;
            if (rigObject != null) vrRig = rigObject.transform;

            if (modelObject != null)
            {
                bodyRoot = modelObject.transform;
                rootToMove = modelObject.transform;
                animator = modelObject.GetComponent<Animator>();
            }

            CacheBones();
            CacheStationaryPose();

            GameObject headsetObject = GameObject.Find("Headset");
            if (headsetObject != null)
            {
                HeadsetScript headset = headsetObject.GetComponent<HeadsetScript>();
                if (headset != null)
                    headset._agentAvatar = this;
            }

            _ready = true;
        }

        bool HasAvatarSetup()
        {
            return animator != null
                   && animator.isHuman
                   && bodyRoot != null
                   && hmd != null
                   && leftController != null
                   && rightController != null;
        }

        void CacheBones()
        {
            if (animator == null || !animator.isHuman)
                return;

            _headBone = headBoneOverride != null ? headBoneOverride : animator.GetBoneTransform(HumanBodyBones.Head);
            _leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            _rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            _leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _leftHandBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            _rightHandBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        void CacheStationaryPose()
        {
            if (rootToMove != null)
            {
                _startRootPosition = rootToMove.position;
                _startRootRotation = rootToMove.rotation;
            }

            if (bodyRoot != null)
            {
                _startBodyPosition = bodyRoot.position;
                _startBodyRotation = bodyRoot.rotation;
            }
        }

        void ResetAnimatorPose()
        {
            if (animator == null)
                return;

            animator.Rebind();
            animator.Update(0f);
            CacheBones();
        }

        void CacheNeutralPose()
        {
            if (_neutralPoseCached || animator == null || !animator.isHuman || bodyRoot == null)
                return;

            if (_headBone != null)
            {
                _neutralHeadLocal = bodyRoot.InverseTransformPoint(_headBone.position);
                _neutralHeadLocalRotation = Quaternion.Inverse(bodyRoot.rotation) * _headBone.rotation;
            }
            else
            {
                _neutralHeadLocal = new Vector3(0f, 1.62f, 0f);
            }

            _neutralLeftShoulderLocal = bodyRoot.InverseTransformPoint(GetShoulderWorld(true));
            _neutralRightShoulderLocal = bodyRoot.InverseTransformPoint(GetShoulderWorld(false));

            if (_leftHandBone != null)
            {
                _neutralLeftHandLocal = bodyRoot.InverseTransformPoint(_leftHandBone.position);
                _neutralLeftHandRotation = Quaternion.Inverse(bodyRoot.rotation) * _leftHandBone.rotation;
            }

            if (_rightHandBone != null)
            {
                _neutralRightHandLocal = bodyRoot.InverseTransformPoint(_rightHandBone.position);
                _neutralRightHandRotation = Quaternion.Inverse(bodyRoot.rotation) * _rightHandBone.rotation;
            }

            _leftArmLength = GetArmLength(true);
            _rightArmLength = GetArmLength(false);
            _neutralPoseCached = true;
        }

        void HoldBodyInPlace()
        {
            if (rootToMove != null)
            {
                rootToMove.position = _startRootPosition;
                rootToMove.rotation = _startRootRotation;
            }

            if (bodyRoot != null)
            {
                bodyRoot.position = _startBodyPosition;
                bodyRoot.rotation = _startBodyRotation;
            }

            if (animator != null)
            {
                animator.bodyPosition = _startBodyPosition;
                animator.bodyRotation = _startBodyRotation;
            }
        }

        void UpdateHandTargets(float dt)
        {
            Vector3 leftPosition = GetDesiredHandPosition(true);
            Vector3 rightPosition = GetDesiredHandPosition(false);
            Quaternion leftRotation = GetDesiredHandRotation(true);
            Quaternion rightRotation = GetDesiredHandRotation(false);

            float positionT = 1f - Mathf.Exp(-Mathf.Max(0.01f, positionSmooth) * dt);
            float rotationT = 1f - Mathf.Exp(-Mathf.Max(0.01f, rotationSmooth) * dt);

            _leftHandTarget = Vector3.Lerp(_leftHandTarget, leftPosition, positionT);
            _rightHandTarget = Vector3.Lerp(_rightHandTarget, rightPosition, positionT);
            _leftHandRotationTarget = Quaternion.Slerp(_leftHandRotationTarget, leftRotation, rotationT);
            _rightHandRotationTarget = Quaternion.Slerp(_rightHandRotationTarget, rightRotation, rotationT);

            _leftElbowHint = GetElbowHint(true, _leftHandTarget);
            _rightElbowHint = GetElbowHint(false, _rightHandTarget);

            if (debugDraw)
            {
                Debug.DrawLine(GetShoulderWorld(true), _leftHandTarget, Color.cyan);
                Debug.DrawLine(GetShoulderWorld(false), _rightHandTarget, Color.magenta);
                Debug.DrawLine(GetShoulderWorld(true), _leftElbowHint, Color.green);
                Debug.DrawLine(GetShoulderWorld(false), _rightElbowHint, Color.yellow);
            }
        }

        void UpdateHeadRotation(float dt)
        {
            if (!followHeadRotation || _headBone == null)
                return;

            Quaternion yawFrame = GetYawFrameRotation();
            Quaternion hmdLocalRotation = Quaternion.Inverse(yawFrame) * hmd.rotation;
            Quaternion desiredRotation = bodyRoot.rotation * hmdLocalRotation * _neutralHeadLocalRotation;
            desiredRotation = Quaternion.Slerp(_headBone.rotation, desiredRotation, headRotationWeight);

            float t = 1f - Mathf.Exp(-Mathf.Max(0.01f, headRotationSmooth) * dt);
            _headRotationTarget = Quaternion.Slerp(_headRotationTarget, desiredRotation, t);
            _headBone.rotation = _headRotationTarget;
        }

        Vector3 GetDesiredHandPosition(bool left)
        {
            Vector3 controllerLocal = GetControllerLocalPosition(left ? leftController : rightController);
            Vector3 offset = left ? leftHandPositionOffset : rightHandPositionOffset;
            Vector3 shoulderLocal = left ? _neutralLeftShoulderLocal : _neutralRightShoulderLocal;
            float armLength = left ? _leftArmLength : _rightArmLength;

            Vector3 handLocal = _neutralHeadLocal + controllerLocal * positionScale + offset;
            handLocal = ClampHandPosition(left, handLocal, shoulderLocal, armLength);
            return bodyRoot.TransformPoint(handLocal);
        }

        Quaternion GetDesiredHandRotation(bool left)
        {
            Quaternion neutralRotation = left ? _neutralLeftHandRotation : _neutralRightHandRotation;
            Quaternion offset = Quaternion.Euler(left ? leftHandRotationOffsetEuler : rightHandRotationOffsetEuler);

            if (!driveHandRotation)
                return bodyRoot.rotation * neutralRotation * offset;

            Transform controller = left ? leftController : rightController;
            Quaternion controllerLocalRotation = Quaternion.Inverse(GetYawFrameRotation()) * controller.rotation;
            return bodyRoot.rotation * controllerLocalRotation * offset;
        }

        Vector3 GetControllerLocalPosition(Transform controller)
        {
            Quaternion yawFrame = GetYawFrameRotation();
            Vector3 local = Quaternion.Inverse(yawFrame) * (controller.position - hmd.position);
            Vector3 rigScale = vrRig != null ? vrRig.lossyScale : Vector3.one;

            if (Mathf.Abs(rigScale.x) < 0.0001f) rigScale.x = 1f;
            if (Mathf.Abs(rigScale.y) < 0.0001f) rigScale.y = 1f;
            if (Mathf.Abs(rigScale.z) < 0.0001f) rigScale.z = 1f;

            local.x /= rigScale.x;
            local.y /= rigScale.y;
            local.z /= rigScale.z;
            return local;
        }

        Quaternion GetYawFrameRotation()
        {
            Vector3 forward = hmd != null ? hmd.forward : bodyRoot.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
                forward = bodyRoot.forward;

            forward.Normalize();
            return Quaternion.LookRotation(forward, Vector3.up);
        }

        Vector3 ClampHandPosition(bool left, Vector3 handLocal, Vector3 shoulderLocal, float armLength)
        {
            Vector3 fromShoulder = handLocal - shoulderLocal;

            float side = fromShoulder.x * (left ? -1f : 1f);
            side = Mathf.Clamp(side, sideMin, sideMax);

            fromShoulder.x = side * (left ? -1f : 1f);
            fromShoulder.y = Mathf.Clamp(fromShoulder.y, verticalMin, verticalMax);
            fromShoulder.z = Mathf.Clamp(fromShoulder.z, forwardMin, forwardMax);

            float minReach = Mathf.Max(0.12f, armLength * minReachFactor);
            float maxReach = Mathf.Max(minReach + 0.01f, armLength * maxReachFactor);
            float reach = fromShoulder.magnitude;

            if (reach < 0.0001f)
                fromShoulder = new Vector3(left ? -0.22f : 0.22f, -0.12f, 0.25f);
            else if (reach < minReach)
                fromShoulder = fromShoulder.normalized * minReach;
            else if (reach > maxReach)
                fromShoulder = fromShoulder.normalized * maxReach;

            return shoulderLocal + fromShoulder;
        }

        Vector3 GetElbowHint(bool left, Vector3 handWorld)
        {
            Vector3 shoulder = GetShoulderWorld(left);
            Vector3 shoulderToHand = handWorld - shoulder;

            if (shoulderToHand.sqrMagnitude < 0.0001f)
                shoulderToHand = bodyRoot.forward * 0.25f + bodyRoot.right * (left ? -0.15f : 0.15f);

            Vector3 side = bodyRoot.right * (left ? -1f : 1f);
            Vector3 bendDirection = side * 0.80f - bodyRoot.forward * 0.25f - bodyRoot.up * 0.08f;
            bendDirection.Normalize();

            float armLength = left ? _leftArmLength : _rightArmLength;
            Vector3 midArm = Vector3.Lerp(shoulder, handWorld, 0.50f);
            return midArm + bendDirection * (armLength * 0.24f);
        }

        float GetArmLength(bool left)
        {
            Transform upperArm = left ? _leftUpperArm : _rightUpperArm;
            Transform lowerArm = left ? _leftLowerArm : _rightLowerArm;
            Transform hand = left ? _leftHandBone : _rightHandBone;

            float length = 0f;
            if (upperArm != null && lowerArm != null)
                length += Vector3.Distance(upperArm.position, lowerArm.position);
            if (lowerArm != null && hand != null)
                length += Vector3.Distance(lowerArm.position, hand.position);

            return length < 0.30f ? 0.65f : length;
        }

        Vector3 GetShoulderWorld(bool left)
        {
            Transform shoulder = left ? _leftShoulder : _rightShoulder;
            if (shoulder != null)
                return shoulder.position;

            Transform upperArm = left ? _leftUpperArm : _rightUpperArm;
            if (upperArm != null)
                return upperArm.position;

            return bodyRoot.position + bodyRoot.up * 1.30f + bodyRoot.right * (left ? -0.18f : 0.18f);
        }

        void LogNeutralPose()
        {
            Log("TurnOn absolute-body-space mode");
            Log("VRRig lossyScale=" + FormatVector(vrRig != null ? vrRig.lossyScale : Vector3.one));
            Log("Neutral head local=" + FormatVector(_neutralHeadLocal));
            Log("Neutral shoulders local L=" + FormatVector(_neutralLeftShoulderLocal) + " R=" + FormatVector(_neutralRightShoulderLocal));
            Log("Neutral hands local L=" + FormatVector(_neutralLeftHandLocal) + " R=" + FormatVector(_neutralRightHandLocal));
            Log("Arm lengths L=" + _leftArmLength.ToString("F3") + " R=" + _rightArmLength.ToString("F3"));
            Log("DriveHandRotation=" + driveHandRotation + " handRotationWeight=" + handRotationWeight.ToString("F2"));
        }

        void Log(string message)
        {
            if (debugLogs)
                MelonLogger.Msg("[AgentAvatarDriver] " + message);
        }

        string FormatVector(Vector3 value)
        {
            return "(" + value.x.ToString("F3") + ", " + value.y.ToString("F3") + ", " + value.z.ToString("F3") + ")";
        }
    }
}
