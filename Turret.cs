using System;
using System.Collections;
using MelonLoader;
using UnhollowerBaseLib.Attributes;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class Turret : MonoBehaviour
    {
        public Turret(IntPtr ptr) : base(ptr) { }
        public Turret() : base(ClassInjector.DerivedConstructorPointer<Turret>())
            => ClassInjector.DerivedConstructorBody(this);

        public Transform target;
        public Transform turretPivot;
        public Transform turretBottom;

        public float yawSpeed = 24f;
        public float pitchSpeed = 18f;

        public bool useYawLimits = false;
        public float minYawFromStart = -180f;
        public float maxYawFromStart = 180f;

        public bool usePitchLimits = false;
        public float minPitchFromStart = -45f;
        public float maxPitchFromStart = 45f;

        public Vector3 localBarrelForward = Vector3.left;
        public Vector3 localPitchAxis = Vector3.up;

        public float windupBeforeFire = 2f;
        public float fireRate = 11f;

        public float firingAimBlend = 0f;

        public float recoilPitchImpulsePerShot = 90f;
        public float recoilPitchSpring = 260f;
        public float recoilPitchDamping = 28f;
        public float recoilPitchMax = 8.75f;

        public float recoilBackImpulsePerShot = 1.25f;
        public float recoilBackSpring = 150f;
        public float recoilBackDamping = 24f;
        public float recoilBackMax = 0.14f;

        public float recoilMicroJitterPerShot = 0.9f;
        public float recoilMicroJitterRecover = 18f;

        public bool autoFindDriver = true;

        public string aimingLoopClipName = "TurretAiming.wav";
        public string aimedDoneClipName = "TurretAimed.wav";
        public string shootingStartClipName = "TurretShooting.mp3";

        public float aimingVolume = 0.75f;
        public float aimedDoneVolume = 0.8f;
        public float shootingVolume = 0.7f;
        public float shootingFadeOutTime = 1f;

        public float aimingMinDistance = 4f;
        public float aimingMaxDistance = 60f;
        public float oneShotMinDistance = 6f;
        public float oneShotMaxDistance = 90f;

        public float aimedAngleThreshold = 4f;
        public float aimedAngleExitThreshold = 7f;

        public bool requireAimBeforeFire = true;
        public float aimStableTimeBeforeReady = 0.2f;
        public float maxAimWaitBeforeFire = 10f;

        public float fireReadyAngleThreshold = 8f;
        public float fireReadyStableTime = 0.25f;
        public bool latchAimSoundDuringExternalFireWait = true;

        public bool lockAimAfterReadyForExternalFireWait = true;

        public bool waitForExternalFireCommand = false;

        public float lineOfSightTargetHeightOffset = 0.15f;
        public float lineOfSightStartForwardOffset = 0.08f;

        Quaternion pivotStartLocalRot;
        Quaternion bottomStartLocalRot;
        Vector3 bottomStartLocalPos;

        float currentYawOffset;
        float currentPitchOffset;
        float desiredYawOffset;
        float desiredPitchOffset;
        bool hasDesiredYaw;
        bool hasDesiredPitch;

        float recoilPitchOffset;
        float recoilPitchVelocity;
        float recoilBackOffset;
        float recoilBackVelocity;
        float recoilMicroJitter;

        bool _trackingPlayer;
        bool _firing;
        bool _isAimLoopOn;
        bool _hasPlayedAimedSound;
        bool _firePermission;
        bool _readyToFireAtTarget;
        bool _aimLockedAfterReady;
        float _readyToFireStableTimer;

        object _shootRoutine;
        TurretDriver _driver;
        LoopingSfx _aimLoop;
        AudioSource _shootingStartSource;

        void Awake()
        {
            if (turretPivot == null)
                turretPivot = transform;
        }

        void Start()
        {

            usePitchLimits = false;

            if (turretPivot == null)
                turretPivot = transform;

            if (turretBottom == null && transform.childCount > 0 && transform.GetChild(0).childCount > 0)
                turretBottom = transform.GetChild(0).GetChild(0);

            if (target == null)
            {
                GameObject rig = GameObject.Find("VRRig");
                if (rig != null)
                    target = rig.transform;
            }

            pivotStartLocalRot = turretPivot.localRotation;

            if (turretBottom != null)
            {
                bottomStartLocalRot = turretBottom.localRotation;
                bottomStartLocalPos = turretBottom.localPosition;
            }

            if (autoFindDriver)
                _driver = GetComponent<TurretDriver>();

            _aimLoop = GetComponent<LoopingSfx>();
            if (_aimLoop == null)
                _aimLoop = gameObject.AddComponent<LoopingSfx>();

            _aimLoop.InitAndPlay(
                aimingLoopClipName,
                aimingVolume,
                aimingMinDistance,
                aimingMaxDistance
            );
            _aimLoop.TurnOff();
            _isAimLoopOn = false;
        }

        void LateUpdate()
        {
            if (turretPivot == null || turretBottom == null)
                return;

            if (_trackingPlayer && target != null)
            {
                UpdateYaw();
                UpdatePitch();
                UpdateAimAudioState();
            }

            UpdateRecoil();
            ApplyBottomTransform();
        }

        public void ShootPlayer()
        {
            if (target == null)
            {
                GameObject rig = GameObject.Find("VRRig");
                if (rig != null)
                    target = rig.transform;
            }

            if (_driver == null)
                _driver = GetComponent<TurretDriver>();

            _trackingPlayer = true;
            _hasPlayedAimedSound = false;
            _firePermission = false;
            _readyToFireAtTarget = false;
            _aimLockedAfterReady = false;
            _readyToFireStableTimer = 0f;

            TurnOnAimLoop();

            if (_shootRoutine != null)
                MelonCoroutines.Stop(_shootRoutine);

            _shootRoutine = MelonCoroutines.Start(CoShootPlayer());
        }

        public void StopShooting()
        {
            _trackingPlayer = false;
            _firing = false;
            _hasPlayedAimedSound = false;
            _firePermission = false;
            _readyToFireAtTarget = false;
            _aimLockedAfterReady = false;
            _readyToFireStableTimer = 0f;

            TurnOffAimLoop();
            StartShootSoundFade();

            if (_shootRoutine != null)
            {
                MelonCoroutines.Stop(_shootRoutine);
                _shootRoutine = null;
            }

            if (_driver != null)
                _driver.SetFiring(false);
        }

        [HideFromIl2Cpp]
        IEnumerator CoShootPlayer()
        {
            _firing = false;

            if (_driver != null)
                _driver.SetFiring(false);

            if (requireAimBeforeFire)
            {
                float aimedStableTimer = 0f;
                float aimWaitTimer = 0f;
                float timeout = Mathf.Max(0.25f, maxAimWaitBeforeFire);

                while (_trackingPlayer && aimWaitTimer < timeout)
                {
                    if (IsReadyToFireAtTarget())
                        aimedStableTimer += Time.deltaTime;
                    else
                        aimedStableTimer = 0f;

                    if (aimedStableTimer >= Mathf.Max(0f, aimStableTimeBeforeReady))
                        break;

                    aimWaitTimer += Time.deltaTime;
                    yield return null;
                }

                if (!_trackingPlayer)
                    yield break;
            }

            if (waitForExternalFireCommand)
            {
                while (_trackingPlayer && !_firePermission)
                    yield return null;

                if (!_trackingPlayer)
                    yield break;
            }
            else
            {
                float wait = Mathf.Max(0f, windupBeforeFire);
                if (wait > 0f)
                    yield return new WaitForSeconds(wait);

                if (!_trackingPlayer)
                    yield break;
            }

            TurnOffAimLoop();
            _shootingStartSource = AudioUtil.PlayAt(
            shootingStartClipName,
            turretBottom != null ? turretBottom.position : transform.position,
            shootingVolume, false,
            oneShotMinDistance,
            oneShotMaxDistance,
            true,
            0.6f
            );

            _firing = true;

            if (_driver != null)
                _driver.SetFiring(true);

            while (_trackingPlayer)
            {
                FireShot();
                yield return new WaitForSeconds(1f / Mathf.Max(0.01f, fireRate));
            }

            _firing = false;

            if (_driver != null)
                _driver.SetFiring(false);

            StartShootSoundFade();
            _shootRoutine = null;
        }

        void FireShot()
        {
            recoilPitchVelocity += recoilPitchImpulsePerShot;
            recoilBackVelocity += recoilBackImpulsePerShot;

            recoilMicroJitter = UnityEngine.Random.Range(-recoilMicroJitterPerShot, recoilMicroJitterPerShot);

            if (_driver != null)
                _driver.FirePulse();
        }

        public void AllowFireNow()
        {
            _firePermission = true;
        }

        public bool IsTrackingPlayer()
        {
            return _trackingPlayer;
        }

        public bool IsFiring()
        {
            return _firing;
        }

        public bool IsAimedAtTarget()
        {
            if (target == null || turretBottom == null)
                return false;

            return GetBarrelToTargetAngle() <= aimedAngleThreshold;
        }

        public bool IsReadyToFireAtTarget()
        {
            if (target == null || turretBottom == null)
                return false;

            if (_readyToFireAtTarget)
                return true;

            return GetBarrelToTargetAngle() <= Mathf.Max(aimedAngleThreshold, fireReadyAngleThreshold);
        }

        public bool IsMechanicallySettledButNotReady()
        {
            if (target == null || turretBottom == null)
                return false;

            if (!hasDesiredYaw || !hasDesiredPitch)
                return false;

            if (IsReadyToFireAtTarget())
                return false;

            float yawError = Mathf.Abs(Mathf.DeltaAngle(currentYawOffset, desiredYawOffset));
            float pitchError = Mathf.Abs(Mathf.DeltaAngle(currentPitchOffset, desiredPitchOffset));

            return yawError <= 0.75f && pitchError <= 0.75f;
        }

        public float GetMechanicalAimError()
        {
            if (!hasDesiredYaw || !hasDesiredPitch)
                return 999f;

            float yawError = Mathf.Abs(Mathf.DeltaAngle(currentYawOffset, desiredYawOffset));
            float pitchError = Mathf.Abs(Mathf.DeltaAngle(currentPitchOffset, desiredPitchOffset));
            return Mathf.Max(yawError, pitchError);
        }

        public float GetAimAngleToTarget()
        {
            if (target == null || turretBottom == null)
                return 180f;

            return GetBarrelToTargetAngle();
        }

        public bool HasDirectLineOfSightToTarget()
        {
            if (target == null)
            {
                GameObject rig = GameObject.Find("VRRig");
                if (rig != null)
                    target = rig.transform;
            }

            if (target == null)
                return false;

            Transform startTransform = turretBottom != null ? turretBottom : transform;
            Vector3 start = startTransform.position;

            if (turretBottom != null)
                start += turretBottom.TransformDirection(localBarrelForward.normalized) * lineOfSightStartForwardOffset;

            Vector3 end = target.position + Vector3.up * lineOfSightTargetHeightOffset;
            Vector3 toTarget = end - start;
            float dist = toTarget.magnitude;

            if (dist < 0.001f)
                return true;

            Vector3 dir = toTarget / dist;
            RaycastHit[] hits = Physics.RaycastAll(start, dir, dist, -1, QueryTriggerInteraction.Ignore);

            if (hits == null || hits.Length == 0)
                return true;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i].collider;
                if (c == null)
                    continue;

                Transform hitT = c.transform;
                if (hitT == null)
                    continue;

                if (hitT == transform || hitT.IsChildOf(transform))
                    continue;

                if (hitT == target || hitT.IsChildOf(target))
                    continue;

                return false;
            }

            return true;
        }

        void UpdateRecoil()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            float pitchAccel = (-recoilPitchOffset * recoilPitchSpring) - (recoilPitchVelocity * recoilPitchDamping);
            recoilPitchVelocity += pitchAccel * dt;
            recoilPitchOffset += recoilPitchVelocity * dt;
            recoilPitchOffset = Mathf.Clamp(recoilPitchOffset, -recoilPitchMax * 0.35f, recoilPitchMax);

            float backAccel = (-recoilBackOffset * recoilBackSpring) - (recoilBackVelocity * recoilBackDamping);
            recoilBackVelocity += backAccel * dt;
            recoilBackOffset += recoilBackVelocity * dt;
            recoilBackOffset = Mathf.Clamp(recoilBackOffset, -recoilBackMax * 0.2f, recoilBackMax);

            recoilMicroJitter = Mathf.MoveTowards(recoilMicroJitter, 0f, recoilMicroJitterRecover * dt);
        }

        void ApplyBottomTransform()
        {
            if (turretBottom == null)
                return;

            Vector3 recoilDirLocal = -localBarrelForward.normalized;
            turretBottom.localPosition = bottomStartLocalPos + recoilDirLocal * recoilBackOffset;

            float visualPitch = -(recoilPitchOffset + recoilMicroJitter);
            float finalPitch = currentPitchOffset + visualPitch;

            turretBottom.localRotation =
                bottomStartLocalRot *
                Quaternion.AngleAxis(finalPitch, localPitchAxis.normalized);
        }

        void UpdateYaw()
        {
            if (_aimLockedAfterReady && waitForExternalFireCommand)
            {
                turretPivot.localRotation =
                    pivotStartLocalRot *
                    Quaternion.AngleAxis(currentYawOffset, Vector3.up);
                return;
            }

            Vector3 toTargetWorld = target.position - turretPivot.position;
            if (toTargetWorld.sqrMagnitude < 0.0001f)
                return;

            Vector3 targetDirParentSpace = turretPivot.parent != null
                ? turretPivot.parent.InverseTransformDirection(toTargetWorld.normalized)
                : toTargetWorld.normalized;

            Vector3 targetFlat = Vector3.ProjectOnPlane(targetDirParentSpace, Vector3.up);
            if (targetFlat.sqrMagnitude < 0.0001f)
                return;

            targetFlat.Normalize();

            Vector3 baseForwardParentSpace =
                (pivotStartLocalRot * bottomStartLocalRot) * localBarrelForward.normalized;

            Vector3 baseFlat = Vector3.ProjectOnPlane(baseForwardParentSpace, Vector3.up);
            if (baseFlat.sqrMagnitude < 0.0001f)
                return;

            baseFlat.Normalize();

            float desired = Vector3.SignedAngle(baseFlat, targetFlat, Vector3.up);

            if (useYawLimits)
                desired = Mathf.Clamp(desired, minYawFromStart, maxYawFromStart);

            desiredYawOffset = desired;
            hasDesiredYaw = true;

            currentYawOffset = Mathf.MoveTowards(
                currentYawOffset,
                desiredYawOffset,
                yawSpeed * Time.deltaTime
            );

            turretPivot.localRotation =
                pivotStartLocalRot *
                Quaternion.AngleAxis(currentYawOffset, Vector3.up);
        }

        void UpdatePitch()
        {
            if (_aimLockedAfterReady && waitForExternalFireCommand)
                return;

            Vector3 toTargetWorld = target.position - turretBottom.position;
            if (toTargetWorld.sqrMagnitude < 0.0001f)
                return;

            Vector3 targetDirYawSpace = turretPivot.InverseTransformDirection(toTargetWorld.normalized);
            Vector3 neutralForwardYawSpace = (bottomStartLocalRot * localBarrelForward.normalized).normalized;
            Vector3 hingeAxisYawSpace = (bottomStartLocalRot * localPitchAxis.normalized).normalized;

            Vector3 neutralProjected = Vector3.ProjectOnPlane(neutralForwardYawSpace, hingeAxisYawSpace);
            Vector3 targetProjected = Vector3.ProjectOnPlane(targetDirYawSpace, hingeAxisYawSpace);

            if (neutralProjected.sqrMagnitude < 0.0001f || targetProjected.sqrMagnitude < 0.0001f)
                return;

            neutralProjected.Normalize();
            targetProjected.Normalize();

            float desired = Vector3.SignedAngle(
                neutralProjected,
                targetProjected,
                hingeAxisYawSpace
            );

            if (_firing)
                desired += firingAimBlend;

            if (usePitchLimits)
                desired = Mathf.Clamp(desired, minPitchFromStart, maxPitchFromStart);

            desiredPitchOffset = desired;
            hasDesiredPitch = true;

            currentPitchOffset = Mathf.MoveTowards(
                currentPitchOffset,
                desiredPitchOffset,
                pitchSpeed * Time.deltaTime
            );
        }

        void UpdateAimAudioState()
        {
            if (_firing || target == null || turretBottom == null)
                return;

            float angleToTarget = GetBarrelToTargetAngle();
            float readyThreshold = Mathf.Max(aimedAngleThreshold, fireReadyAngleThreshold);

            if (angleToTarget <= readyThreshold)
            {
                _readyToFireStableTimer += Time.deltaTime;
                if (_readyToFireStableTimer >= Mathf.Max(0f, fireReadyStableTime))
                {
                    _readyToFireAtTarget = true;

                    if (waitForExternalFireCommand && lockAimAfterReadyForExternalFireWait)
                        _aimLockedAfterReady = true;
                }
            }
            else
            {
                _readyToFireStableTimer = 0f;
            }

            if (!_hasPlayedAimedSound && angleToTarget <= readyThreshold)
            {
                _hasPlayedAimedSound = true;
                TurnOffAimLoop();
                PlayOneShotAtTurret(aimedDoneClipName, aimedDoneVolume);
            }
            else if (_hasPlayedAimedSound && angleToTarget >= aimedAngleExitThreshold)
            {
                if (waitForExternalFireCommand && latchAimSoundDuringExternalFireWait)
                    return;

                _hasPlayedAimedSound = false;
                _readyToFireAtTarget = false;
                _aimLockedAfterReady = false;
                TurnOnAimLoop();
            }
        }

        float GetBarrelToTargetAngle()
        {
            Vector3 barrelForwardWorld = turretBottom.TransformDirection(localBarrelForward.normalized);
            Vector3 toTargetWorld = (target.position - turretBottom.position).normalized;

            if (barrelForwardWorld.sqrMagnitude < 0.0001f || toTargetWorld.sqrMagnitude < 0.0001f)
                return 180f;

            return Vector3.Angle(barrelForwardWorld, toTargetWorld);
        }

        void TurnOnAimLoop()
        {
            if (_aimLoop == null || _isAimLoopOn)
                return;

            _aimLoop.TurnOn();
            _isAimLoopOn = true;
        }

        void TurnOffAimLoop()
        {
            if (_aimLoop == null || !_isAimLoopOn)
                return;

            _aimLoop.TurnOff();
            _isAimLoopOn = false;
        }

        AudioSource PlayOneShotAtTurret(string clipName, float volume)
        {
            Vector3 pos = turretBottom != null ? turretBottom.position : transform.position;
            return AudioUtil.PlayAt(
                clipName,
                pos,
                volume, false,
                oneShotMinDistance,
                oneShotMaxDistance,
                true
            );
        }

        void StartShootSoundFade()
        {
            if (_shootingStartSource == null)
                return;

            MelonCoroutines.Start(CoFadeAndStopShootSound(_shootingStartSource, shootingFadeOutTime));
            _shootingStartSource = null;
        }

        [HideFromIl2Cpp]
        IEnumerator CoFadeAndStopShootSound(AudioSource source, float fadeTime)
        {
            if (source == null)
                yield break;

            float startVolume = source.volume;
            float duration = Mathf.Max(0.01f, fadeTime);
            float t = 0f;

            while (source != null && t < duration)
            {
                t += Time.deltaTime;

                if (source != null)
                    source.volume = Mathf.Lerp(startVolume, 0f, t / duration);

                yield return null;
            }

            if (source != null)
                AudioUtil.Stop(source);
        }

        void OnDisable()
        {
            TurnOffAimLoop();
            StartShootSoundFade();
        }

        void OnDestroy()
        {
            TurnOffAimLoop();
            StartShootSoundFade();
        }
    }
}
