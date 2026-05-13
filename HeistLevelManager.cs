using System;
using System.Collections;
using MelonLoader;
using SG.GlobalEvents.Variables;
using SG.Phoenix.Assets.Code.InputManagement;
using SG.Phoenix.Assets.Code.Interactables;
using SG.Phoenix.Assets.Code.WorldAttributes;
using UnhollowerBaseLib.Attributes;
using UnhollowerRuntimeLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IEYTD_Mod2Code
{
    public class HeistLevelManager : MonoBehaviour
    {
        public HeistLevelManager(IntPtr ptr) : base(ptr) { }
        public HeistLevelManager() : base(ClassInjector.DerivedConstructorPointer<HeistLevelManager>())
            => ClassInjector.DerivedConstructorBody(this);

        GameObject droneTrigger;
        GameObject Drone;
        GameObject VRRig;
        HeadsetDriver headsetDriver;
        HeadsetScript headsetScript;
        Turret[] turrets = new Turret[4];

        public float turretPauseAfterAllAimed = 1.5f;
        public float turretAllAimedStableTime = 0.25f;
        public float maxWaitForAllTurretsAimed = 10f;

        public bool onlyUseTurretsWithDirectLineOfSight = true;
        public bool logTurretLineOfSightChecks = true;
        public bool logTurretAimWaitDetails = true;
        public float turretAimWaitLogInterval = 0.75f;
        public float skipUnreachableTurretSettledTime = 0.35f;
        bool[] activeDeathTurrets = new bool[4];
        float[] activeDeathTurretSettledButNotReadyTimers = new float[4];
        int activeDeathTurretCount = 0;

        bool turretDroneDeathRunning = false;
        object deathRoutineHandle;

        public float crackDelayAfterFireStarts = 0.18f;
        public float spiralStartAfterCrack = 0.08f;
        public float spiralDuration = 1.85f;
        public float fadeDelayAfterSpiral = 0.08f;

        public float backwardJolt = 0.30f;
        public float forwardRecovery = 0.08f;
        public float sideRadius = 0.18f;
        public float verticalRadius = 0.10f;
        public float downwardDrop = 0.08f;

        public float maxYaw = 42f;
        public float maxRoll = 24f;
        public float maxPitch = 12f;

        public float yawFrequency = 2.5f;
        public float rollFrequency = 3.4f;
        public float pitchFrequency = 2.6f;
        public float spiralFrequency = 1.55f;

        public float poisonAlarmDelay = 0.5f;
        public float poisonLeadInDelay = 1f;
        public float poisonOverlayStartDelay = 0.06f;
        public float poisonDeathSfxDelay = 0.02f;

        GameObject saferoomHB;
        HiddenVolume saferoomHV;
        HiddenVolume ventHV;
        BoxCollider windowCol;
        AlarmDriver alarm;

        public float windowFlipStateHoldTime = 0.75f;
        bool _lastInVent = false;
        bool _pendingInVent;
        float _pendingInVentTimer = 0f;
        bool _pendingInVentInitialized = false;
        LoopingSfx poisonSound;
        ElevatorScript elevator;

        static int _poisonSequenceVersion = 0;
        int _myPoisonSequenceVersion = 0;
        object poisonDeathRoutineHandle;
        object poisonAlarmRoutineHandle;
        object poisonDelayedDeathRoutineHandle;
        public static HeistLevelManager Instance;
        PickUp dronePu;
        Rigidbody droneRB;
        bool _dronePickedUp = false;

        bool _speedrunTimerRunning = false;
        bool _speedrunTimerFinished = false;
        float _speedrunStartTime = 0f;

        public float WinRoomFadeToBlackDuration = 0.85f;
        GameObject _sceneFadeQuad;
        MeshRenderer _sceneFadeRenderer;
        Material _sceneFadeMaterial;
        Camera _sceneFadeCamera;

        void Awake()
        {
            Instance = this;

            _poisonSequenceVersion++;
            _myPoisonSequenceVersion = _poisonSequenceVersion;
            MelonLogger.Msg("[HeistLevelManager] Poison sequence version armed: " + _myPoisonSequenceVersion);
        }
        public void Start()
        {
            droneTrigger = GameObject.Find("DroneTrigger");
            Drone = GameObject.Find("Drone");
            VRRig = GameObject.Find("VRRig");

            GameObject _headset = GameObject.Find("Headset");
            if (_headset != null)
            {
                headsetDriver = _headset.GetComponent<HeadsetDriver>();
                headsetScript = _headset.GetComponent<HeadsetScript>();
            }

            GameObject turret1 = GameObject.Find("TurretPivot1");
            GameObject turret2 = GameObject.Find("TurretPivot2");
            GameObject turret3 = GameObject.Find("TurretPivot3");
            GameObject turret4 = GameObject.Find("TurretPivot4");

            if (turret1 != null) turrets[0] = turret1.GetComponent<Turret>();
            if (turret2 != null) turrets[1] = turret2.GetComponent<Turret>();
            if (turret3 != null) turrets[2] = turret3.GetComponent<Turret>();
            if (turret4 != null) turrets[3] = turret4.GetComponent<Turret>();

            windowCol = GameObject.Find("WindowCol").GetComponent<BoxCollider>();

            saferoomHB = GameObject.Find("SaferoomHB");
            if (saferoomHB != null)
                saferoomHV = saferoomHB.GetComponent<HiddenVolume>();

            ventHV = GameObject.Find("VentCollider").GetComponent<HiddenVolume>();

            elevator = GameObject.Find("ElevatorFloor").GetComponent<ElevatorScript>();

            alarm = GetComponent<AlarmDriver>();
            if (alarm != null)
                alarm.SetVolume(1f);

            GameObject bankBear = GameObject.Find("BankBear1");
            if (bankBear != null)
            {
                poisonSound = bankBear.AddComponent<LoopingSfx>();
                poisonSound.InitAndPlay("GasLoop.ogg", 0.7f);
                poisonSound.TurnOff();
            }

            ResetPoisonRuntimeForFreshLevel("StartFreshLevel");
            LevelUtil.HellenKeller(true);
            IntroSequence();
            dronePu = GameObject.Find("PickUp_HOST_Drone").GetComponent<PickUp>();
            droneRB = dronePu.gameObject.GetComponent<Rigidbody>();
            PhoenixButtonHook.restarting = false;

            BoxCollider[] hbcols = saferoomHB.GetComponents<BoxCollider>();
            BoxCollider hbCol = hbcols[1];

            BoxCollider winCol = windowCol.GetComponent<BoxCollider>();

            hbCol.enabled = true;
            winCol.enabled = true;
        }

        void OnDestroy()
        {

            _poisonSequenceVersion++;
            CleanupPoisonRuntime("OnDestroy", true);

            if (Instance == this)
                Instance = null;
        }

        void ResetPoisonRuntimeForFreshLevel(string reason)
        {
            poisonDeathTriggered = false;
            CleanupPoisonRuntime(reason, true);
        }

        void CleanupPoisonRuntime(string reason, bool forceGasOff)
        {
            try
            {
                if (poisonDeathRoutineHandle != null)
                {
                    MelonCoroutines.Stop(poisonDeathRoutineHandle);
                    poisonDeathRoutineHandle = null;
                }
            }
            catch { }

            try
            {
                if (poisonAlarmRoutineHandle != null)
                {
                    MelonCoroutines.Stop(poisonAlarmRoutineHandle);
                    poisonAlarmRoutineHandle = null;
                }
            }
            catch { }

            try
            {
                if (poisonDelayedDeathRoutineHandle != null)
                {
                    MelonCoroutines.Stop(poisonDelayedDeathRoutineHandle);
                    poisonDelayedDeathRoutineHandle = null;
                }
            }
            catch { }

            try
            {
                if (poisonSound != null)
                    poisonSound.TurnOff();
            }
            catch { }

            if (forceGasOff)
            {
                try
                {
                    PoisonGasController.ForceFreshAllOff(reason);
                }
                catch (Exception e)
                {
                    MelonLogger.Warning("[HeistLevelManager] Poison gas cleanup failed during " + reason + ": " + e.Message);
                }
            }
        }

        bool IsPoisonSequenceCurrent(int version)
        {
            return Instance == this && version == _poisonSequenceVersion && version == _myPoisonSequenceVersion;
        }

        void Update()
        {
            if (Drone != null && droneTrigger != null)
                droneTrigger.transform.position = Drone.transform.position;

            if (saferoomHB != null && saferoomHV != null)
            {
                if (saferoomHV._hiddenInteractables._size >= 1 || ventHV._hiddenInteractables._size >= 2) { saferoomHB.layer = 18; windowCol.isTrigger = true; }
                else { saferoomHB.layer = 0; windowCol.isTrigger = false; }
            }

            if (droneRB.constraints == RigidbodyConstraints.FreezeAll)
            {
                if (dronePu.isHeld) droneRB.constraints = RigidbodyConstraints.None;
            }







        }

        public void IntroSequence()
        {
            MelonCoroutines.Start(Instance.Co_IntroSequence());
        }

        [HideFromIl2Cpp]
        System.Collections.IEnumerator Co_IntroSequence()
        {
            yield return new WaitForSeconds(1);
            AudioUtil.PlayAt("StartStinger.wav", VRRig.transform.position);
            yield return null;
            LevelUtil.HellenKeller(false);
            StartSpeedrunTimer();
            yield return new WaitForSeconds(5.5f);
            playHandler("HandlerIntroLine.wav", true);

        }

        void StartSpeedrunTimer()
        {
            _speedrunStartTime = Time.realtimeSinceStartup;
            _speedrunTimerRunning = true;
            _speedrunTimerFinished = false;

            MelonLogger.Msg("[HeistLevelManager] Speedrun timer started.");
        }

        void FinishAndSaveSpeedrunTimer()
        {
            if (_speedrunTimerFinished)
                return;

            if (!_speedrunTimerRunning)
            {
                MelonLogger.Warning("[HeistLevelManager] Tried to finish speedrun timer, but it was not running.");
                return;
            }

            float elapsed = Time.realtimeSinceStartup - _speedrunStartTime;
            _speedrunTimerRunning = false;
            _speedrunTimerFinished = true;

            SaveManager.MarkLevelComplete();
            SaveManager.RecordSpeedrunTime(elapsed);

            MelonLogger.Msg("[HeistLevelManager] Speedrun timer finished: " + SaveManager.FormatTime(elapsed));
        }

        public static bool VaultUnlocked = false;

        public void UnlockVault()
        {
            GameObject wheel = GameObject.Find("RealVaultWheel");
            wheel.GetComponent<MeshCollider>().enabled = false;
            GameObject vaultPivot = GameObject.Find("VaultPivot");
            vaultPivot.GetComponent<BoxCollider>().enabled = true;
            vaultPivot.transform.parent.gameObject.GetComponent<RotationalMotion>().enabled = true;
            wheel.transform.parent = vaultPivot.transform;
            VaultUnlocked = true;
            AudioUtil.PlayAt("VaultUnlock.wav", vaultPivot.transform.position, 1);

            playStinger(0.5f);
            playHandler("Handler_VaultUnlocked.wav", 1.8f, true);
        }

        public static void ZorCaseGrabbed()
        {
            MelonCoroutines.Start(Instance.Co_ZorCaseGrabbed());
        }

        [HideFromIl2Cpp]
        System.Collections.IEnumerator Co_ZorCaseGrabbed()
        {
            yield return new WaitForSeconds(1);

            playHandler("Handler_GotBriefcase.wav");

            yield return WaitForHandlerQueueToFinish(20f);

            FinishAndSaveSpeedrunTimer();

            yield return FadeToBlackBeforeSceneSwitch(WinRoomFadeToBlackDuration);

            WinRoomScript.ArmForHeistWinRoom();
            yield return SceneManager.LoadSceneAsync("WinRoom", LoadSceneMode.Single);
            LevelUtil.HellenKeller(false);
        }

        [HideFromIl2Cpp]
        System.Collections.IEnumerator FadeToBlackBeforeSceneSwitch(float duration)
        {
            EnsureSceneFadeOverlay();

            if (_sceneFadeRenderer == null || _sceneFadeMaterial == null)
            {
                LevelUtil.blindPlayer(true);
                if (duration > 0f)
                    yield return new WaitForSeconds(duration);
                yield break;
            }

            _sceneFadeQuad.SetActive(true);

            float dur = Mathf.Max(0.01f, duration);
            float t = 0f;

            SetSceneFadeAlpha(0f);

            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);
                u = u * u * (3f - 2f * u);

                FitSceneFadeOverlay();
                SetSceneFadeAlpha(u);
                yield return null;
            }

            FitSceneFadeOverlay();
            SetSceneFadeAlpha(1f);

            LevelUtil.blindPlayer(true);
            yield return null;
        }

        void EnsureSceneFadeOverlay()
        {
            if (_sceneFadeQuad != null && _sceneFadeRenderer != null && _sceneFadeMaterial != null)
                return;

            _sceneFadeCamera = FindFadeCamera();
            if (_sceneFadeCamera == null)
                return;

            _sceneFadeQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _sceneFadeQuad.name = "HeistLevelManager_SceneFadeBlack";
            _sceneFadeQuad.transform.SetParent(_sceneFadeCamera.transform, false);
            _sceneFadeQuad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            Collider col = _sceneFadeQuad.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Shader shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");

            if (shader == null)
            {
                Destroy(_sceneFadeQuad);
                _sceneFadeQuad = null;
                return;
            }

            _sceneFadeMaterial = new Material(shader);
            _sceneFadeMaterial.name = "HeistLevelManager_SceneFadeBlack_Mat";
            _sceneFadeMaterial.renderQueue = 5000;

            _sceneFadeRenderer = _sceneFadeQuad.GetComponent<MeshRenderer>();
            if (_sceneFadeRenderer != null)
            {
                _sceneFadeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _sceneFadeRenderer.receiveShadows = false;
                _sceneFadeRenderer.material = _sceneFadeMaterial;
            }

            SetSceneFadeAlpha(0f);
            FitSceneFadeOverlay();
            _sceneFadeQuad.SetActive(false);
        }

        Camera FindFadeCamera()
        {
            GameObject hmd = GameObject.Find("HMD");
            if (hmd != null)
            {
                Camera cam = hmd.GetComponent<Camera>();
                if (cam != null) return cam;

                cam = hmd.GetComponentInChildren<Camera>(true);
                if (cam != null) return cam;
            }

            if (Camera.main != null)
                return Camera.main;

            Camera[] cams = GameObject.FindObjectsOfType<Camera>();
            if (cams != null && cams.Length > 0)
                return cams[0];

            return null;
        }

        void FitSceneFadeOverlay()
        {
            if (_sceneFadeQuad == null || _sceneFadeCamera == null)
                return;

            float zWorld = Mathf.Max(0.05f, _sceneFadeCamera.nearClipPlane + 0.08f);

            Vector3 s = _sceneFadeCamera.transform.lossyScale;
            float invX = Mathf.Abs(s.x) < 0.0001f ? 1f : 1f / s.x;
            float invY = Mathf.Abs(s.y) < 0.0001f ? 1f : 1f / s.y;
            float invZ = Mathf.Abs(s.z) < 0.0001f ? 1f : 1f / s.z;

            _sceneFadeQuad.transform.localPosition = new Vector3(0f, 0f, zWorld * invZ);

            float hWorld = 2f * zWorld * Mathf.Tan(_sceneFadeCamera.fieldOfView * 0.5f * 0.0174532924f);
            float wWorld = hWorld * _sceneFadeCamera.aspect;

            _sceneFadeQuad.transform.localScale = new Vector3(wWorld * 1.5f * invX, hWorld * 1.5f * invY, 1f);
        }

        void SetSceneFadeAlpha(float alpha)
        {
            if (_sceneFadeMaterial == null)
                return;

            Color c = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));

            if (_sceneFadeMaterial.HasProperty("_Color"))
                _sceneFadeMaterial.SetColor("_Color", c);

            if (_sceneFadeRenderer != null)
            {
                _sceneFadeRenderer.enabled = alpha > 0.001f;
            }
        }

        public static void TurretDeath()
        {
            if (Instance.headsetScript.IsWearing)
            {
                Instance.TurretDroneDeath();
            }
            else
            {
                Instance.TurretHumanDeath();
            }
        }

        public void TurretDroneDeath()
        {
            if (turretDroneDeathRunning) return;

            turretDroneDeathRunning = true;

            if (deathRoutineHandle != null)
                MelonCoroutines.Stop(deathRoutineHandle);

            deathRoutineHandle = MelonCoroutines.Start(Co_TurretDroneDeath());
        }

        bool turretHumanDeathRunning = false;
        public void TurretHumanDeath()
        {
            if (turretHumanDeathRunning) return;
            turretHumanDeathRunning = true;
            if (deathRoutineHandle != null)
                MelonCoroutines.Stop(deathRoutineHandle);

            deathRoutineHandle = MelonCoroutines.Start(Co_TurretHumanDeath());

        }

        bool poisonDeathTriggered = false;

        public static void PoisonDeath()
        {
            if (Instance == null) return;
            if (Instance.poisonDeathTriggered) return;

            Instance.poisonDeathTriggered = true;

            if (Instance.poisonDeathRoutineHandle != null)
                MelonCoroutines.Stop(Instance.poisonDeathRoutineHandle);

            int version = Instance._myPoisonSequenceVersion;
            Instance.poisonDeathRoutineHandle = MelonCoroutines.Start(Instance.Co_PoisonDeath(version));
        }

        public void ExplosionDroneDeath()
        {
            MelonCoroutines.Start(Co_ExplosionDroneDeath());
        }

        [HideFromIl2Cpp]
        IEnumerator Co_PoisonDeath(int version)
        {
            if (!IsPoisonSequenceCurrent(version)) yield break;

            TriggerPoisonAlarm(poisonAlarmDelay, version);

            yield return new WaitForSeconds(1);
            if (!IsPoisonSequenceCurrent(version)) yield break;

            AudioUtil.PlayAt("Intruder_BearGas.wav", VRRig.transform.position, 1f, true);

            yield return new WaitForSeconds(4.5f);
            if (!IsPoisonSequenceCurrent(version)) yield break;

            yield return new WaitForSeconds(poisonLeadInDelay);
            if (!IsPoisonSequenceCurrent(version)) yield break;

            PoisonGasController.StartAll();
            if (poisonSound != null) poisonSound.TurnOn();

            AudioUtil.PlayAt("PoisonDeath.wav", VRRig.transform.position, 0.8f);
            DamageOverlayDriver.TriggerPoisonDeath();

            if (poisonDelayedDeathRoutineHandle != null)
                MelonCoroutines.Stop(poisonDelayedDeathRoutineHandle);

            poisonDelayedDeathRoutineHandle = MelonCoroutines.Start(Co_PoisonDelayedDeath(11f, "Poison Gas", version));
        }

        void TriggerPoisonAlarm(float delay, int version)
        {
            if (poisonAlarmRoutineHandle != null)
                MelonCoroutines.Stop(poisonAlarmRoutineHandle);

            poisonAlarmRoutineHandle = MelonCoroutines.Start(Co_TriggerPoisonAlarm(delay, version));
        }

        [HideFromIl2Cpp]
        IEnumerator Co_TriggerPoisonAlarm(float delay, int version)
        {
            yield return new WaitForSeconds(delay);
            if (!IsPoisonSequenceCurrent(version)) yield break;

            if (alarm != null)
                alarm.StartAlarm();
        }

        [HideFromIl2Cpp]
        IEnumerator Co_PoisonDelayedDeath(float delay, string msg, int version)
        {
            yield return new WaitForSeconds(delay);
            if (!IsPoisonSequenceCurrent(version)) yield break;

            LevelUtil.TriggerDeath(msg);
        }

        float GetMaxTurretWindup()
        {
            float maxWindup = 0f;

            for (int i = 0; i < turrets.Length; i++)
            {
                if (turrets[i] != null)
                    maxWindup = Mathf.Max(maxWindup, turrets[i].windupBeforeFire);
            }

            return maxWindup;
        }

        void ClearActiveDeathTurrets()
        {
            activeDeathTurretCount = 0;

            if (activeDeathTurrets == null || activeDeathTurrets.Length != turrets.Length)
                activeDeathTurrets = new bool[turrets.Length];

            if (activeDeathTurretSettledButNotReadyTimers == null || activeDeathTurretSettledButNotReadyTimers.Length != turrets.Length)
                activeDeathTurretSettledButNotReadyTimers = new float[turrets.Length];

            for (int i = 0; i < activeDeathTurrets.Length; i++)
            {
                activeDeathTurrets[i] = false;
                activeDeathTurretSettledButNotReadyTimers[i] = 0f;
            }
        }

        bool ShouldUseTurretForDeath(int index)
        {
            if (index < 0 || index >= turrets.Length || turrets[index] == null)
                return false;

            if (!onlyUseTurretsWithDirectLineOfSight)
                return true;

            bool hasLine = false;

            try
            {
                hasLine = turrets[index].HasDirectLineOfSightToTarget();
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[HeistLevelManager] Turret line-of-sight check failed for index " + index + ": " + e.Message);
                hasLine = false;
            }

            if (logTurretLineOfSightChecks)
                MelonLogger.Msg("[HeistLevelManager] Turret " + index + " direct eyeline to VRRig = " + hasLine);

            return hasLine;
        }

        void StartAllTurretsAimingOnly()
        {
            ClearActiveDeathTurrets();

            for (int i = 0; i < turrets.Length; i++)
            {
                if (turrets[i] == null)
                    continue;

                if (!ShouldUseTurretForDeath(i))
                {
                    try { turrets[i].StopShooting(); }
                    catch { }
                    continue;
                }

                activeDeathTurrets[i] = true;
                activeDeathTurretCount++;

                try
                {
                    turrets[i].waitForExternalFireCommand = true;
                    turrets[i].ShootPlayer();
                }
                catch (Exception e)
                {
                    activeDeathTurrets[i] = false;
                    activeDeathTurretCount--;
                    MelonLogger.Warning("[HeistLevelManager] Turret ShootPlayer failed: " + e.Message);
                }
            }

            if (activeDeathTurretCount <= 0)
                MelonLogger.Warning("[HeistLevelManager] No turrets have a direct eyeline to VRRig. No turrets will fire for this death sequence.");
        }

        void CullActiveTurretsThatCannotActuallyAim()
        {
            if (activeDeathTurretCount <= 0)
                return;

            for (int i = 0; i < turrets.Length; i++)
            {
                if (!activeDeathTurrets[i] || turrets[i] == null)
                    continue;

                bool settledButNotReady = false;

                try
                {
                    settledButNotReady = turrets[i].IsMechanicallySettledButNotReady();
                }
                catch
                {
                    settledButNotReady = false;
                }

                if (settledButNotReady)
                    activeDeathTurretSettledButNotReadyTimers[i] += Time.deltaTime;
                else
                    activeDeathTurretSettledButNotReadyTimers[i] = 0f;

                if (activeDeathTurretSettledButNotReadyTimers[i] >= Mathf.Max(0.05f, skipUnreachableTurretSettledTime))
                {
                    float angle = 180f;
                    float mech = 999f;

                    try { angle = turrets[i].GetAimAngleToTarget(); }
                    catch { }

                    try { mech = turrets[i].GetMechanicalAimError(); }
                    catch { }

                    MelonLogger.Warning("[HeistLevelManager] Skipping turret " + i +
                                        " because it has eyeline but cannot actually aim at VRRig. " +
                                        "angle=" + angle.ToString("0.00") +
                                        " mechanicalError=" + mech.ToString("0.00"));

                    activeDeathTurrets[i] = false;
                    activeDeathTurretCount--;
                    activeDeathTurretSettledButNotReadyTimers[i] = 0f;

                    try { turrets[i].StopShooting(); }
                    catch { }
                }
            }
        }

        bool AreAllActiveDeathTurretsReady()
        {
            if (activeDeathTurretCount <= 0)
                return false;

            for (int i = 0; i < turrets.Length; i++)
            {
                if (!activeDeathTurrets[i])
                    continue;

                if (turrets[i] == null)
                    continue;

                if (!turrets[i].IsReadyToFireAtTarget())
                    return false;
            }

            return true;
        }

        void AllowActiveDeathTurretsToFire()
        {
            for (int i = 0; i < turrets.Length; i++)
            {
                if (!activeDeathTurrets[i] || turrets[i] == null)
                    continue;

                try { turrets[i].AllowFireNow(); }
                catch (Exception e)
                {
                    MelonLogger.Warning("[HeistLevelManager] Turret AllowFireNow failed: " + e.Message);
                }
            }
        }

        void LogActiveTurretAimState(float waitTimer)
        {
            for (int i = 0; i < turrets.Length; i++)
            {
                if (!activeDeathTurrets[i] || turrets[i] == null)
                    continue;

                try
                {
                    MelonLogger.Msg("[HeistLevelManager] Aim wait t=" + waitTimer.ToString("0.00") +
                                    " turret=" + i +
                                    " ready=" + turrets[i].IsReadyToFireAtTarget() +
                                    " exactAimed=" + turrets[i].IsAimedAtTarget() +
                                    " angle=" + turrets[i].GetAimAngleToTarget().ToString("0.00"));
                }
                catch (Exception e)
                {
                    MelonLogger.Warning("[HeistLevelManager] Aim wait log failed for turret " + i + ": " + e.Message);
                }
            }
        }

        [HideFromIl2Cpp]
        IEnumerator Co_AimAllTurretsThenFire()
        {
            StartAllTurretsAimingOnly();

            if (activeDeathTurretCount <= 0)
                yield break;

            float aimedStableTimer = 0f;
            float waitTimer = 0f;
            float nextAimLogTime = 0f;
            float timeout = Mathf.Max(0.25f, maxWaitForAllTurretsAimed);

            while (waitTimer < timeout)
            {
                CullActiveTurretsThatCannotActuallyAim();

                if (activeDeathTurretCount <= 0)
                {
                    MelonLogger.Warning("[HeistLevelManager] All active eyeline turrets were skipped because none could actually aim at VRRig.");
                    yield break;
                }

                if (AreAllActiveDeathTurretsReady())
                    aimedStableTimer += Time.deltaTime;
                else
                    aimedStableTimer = 0f;

                if (aimedStableTimer >= Mathf.Max(0f, turretAllAimedStableTime))
                    break;

                if (logTurretAimWaitDetails && waitTimer >= nextAimLogTime)
                {
                    LogActiveTurretAimState(waitTimer);
                    nextAimLogTime = waitTimer + Mathf.Max(0.1f, turretAimWaitLogInterval);
                }

                waitTimer += Time.deltaTime;
                yield return null;
            }

            if (waitTimer >= timeout)
                MelonLogger.Warning("[HeistLevelManager] Timed out waiting for active eyeline turrets to aim. Firing active turrets anyway so death sequence does not hang.");

            if (turretPauseAfterAllAimed > 0f)
                yield return new WaitForSeconds(turretPauseAfterAllAimed);

            AllowActiveDeathTurretsToFire();
        }

        public void TriggerDeath(float delay, string msg)
        {
            MelonCoroutines.Start(Co_TriggerDeath(delay, msg));
        }

        [HideFromIl2Cpp]
        IEnumerator Co_TriggerDeath(float delay, string msg)
        {
            yield return new WaitForSeconds(delay);
            LevelUtil.TriggerDeath(msg);
        }

        void StopAllTurrets()
        {
            for (int i = 0; i < turrets.Length; i++)
            {
                if (turrets[i] != null)
                {
                    try { turrets[i].StopShooting(); }
                    catch { }
                }
            }
        }

        public void TriggerAlarm(float delay)
        {
            MelonCoroutines.Start(Co_TriggerAlarm(delay));
        }

        [HideFromIl2Cpp]
        IEnumerator Co_TriggerAlarm(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (alarm != null)
                alarm.StartAlarm();
        }

        [HideFromIl2Cpp]
        IEnumerator Co_TurretDroneDeath()
        {
            GameObject hs = GameObject.Find("Headset");
            if (hs != null)
            {
                BoxCollider bc = hs.GetComponent<BoxCollider>();
                if (bc != null) bc.enabled = false;
            }

            yield return new WaitForSeconds(1);
            TriggerAlarm(0.3f);
            yield return new WaitForSeconds(1);
            AudioUtil.PlayAt("Intruder_Turret.wav", VRRig.transform.position, 1, true);
            yield return new WaitForSeconds(4.3f);

            yield return Co_AimAllTurretsThenFire();

            float waitForActualFire = Mathf.Max(0f, crackDelayAfterFireStarts);
            if (waitForActualFire > 0f)
                yield return new WaitForSeconds(waitForActualFire);

            if (headsetDriver != null)
            {
                try { headsetDriver.DroneShot(); }
                catch (Exception e)
                {
                    MelonLogger.Warning("[HeistLevelManager] Headset DroneShot failed: " + e.Message);
                }
            }

            if (hs != null)
            {
                HeadsetScript hss = hs.GetComponent<HeadsetScript>();
                if (hss != null && hss.headsetLoop != null && hss.headsetLoop._source != null)
                {
                    hss.headsetLoop._source.clip = HeistBundle2Manager.GetAudio("DroneDeadStatic.wav");
                    hss.headsetLoop._source.volume = 1f;
                    hss.headsetLoop._source.Play();
                }
            }

            if (spiralStartAfterCrack > 0f)
                yield return new WaitForSeconds(spiralStartAfterCrack);

            if (VRRig == null)
            {
                LevelUtil.blindPlayer(true);
                StopAllTurrets();
                deathRoutineHandle = null;
                yield break;
            }

            Vector3 startPos = VRRig.transform.position;
            Quaternion startRot = VRRig.transform.rotation;

            float t = 0f;
            while (t < spiralDuration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / spiralDuration);

                float amp = 1f - (u * 0.55f);
                amp = Mathf.Clamp01(amp);

                float hitCurve = Mathf.Sin(Mathf.Clamp01(u * 1.1f) * 3.14159274f);
                float spiralAngle = t * spiralFrequency * 6.28318548f;

                Vector3 rigRight = startRot * Vector3.right;
                Vector3 rigUp = startRot * Vector3.up;
                Vector3 rigForward = startRot * Vector3.forward;

                Vector3 backwardOffset = -rigForward * (backwardJolt * hitCurve);
                Vector3 recoveryOffset = rigForward * (forwardRecovery * Mathf.Sin(u * 6.28318548f) * amp);
                Vector3 dropOffset = Vector3.down * (downwardDrop * Mathf.Sin(u * 3.14159274f));

                Vector3 spiralOffset =
                    (rigRight * Mathf.Sin(spiralAngle) * sideRadius +
                     rigUp * Mathf.Cos(spiralAngle) * verticalRadius) * amp;

                Vector3 extraSway =
                    rigRight * (Mathf.Sin(t * yawFrequency * 6.28318548f + 0.75f) * sideRadius * 0.35f * amp);

                VRRig.transform.position = startPos + backwardOffset + recoveryOffset + dropOffset + spiralOffset + extraSway;

                float yaw =
                    Mathf.Sin(t * yawFrequency * 6.28318548f) * maxYaw * amp +
                    Mathf.Sin(spiralAngle) * maxYaw * 0.40f * amp;

                float roll =
                    Mathf.Cos(spiralAngle) * maxRoll * amp +
                    Mathf.Sin(t * rollFrequency * 6.28318548f) * maxRoll * 0.45f * amp;

                float pitch =
                    Mathf.Sin(t * pitchFrequency * 6.28318548f + 0.8f) * maxPitch * amp +
                    Mathf.Sin(spiralAngle + 1.2f) * maxPitch * 0.30f * amp;

                VRRig.transform.rotation = startRot * Quaternion.Euler(pitch, yaw, roll);
                yield return null;
            }

            if (fadeDelayAfterSpiral > 0f)
                yield return new WaitForSeconds(fadeDelayAfterSpiral);

            LevelUtil.blindPlayer(true);
            StopAllTurrets();
            deathRoutineHandle = null;
            LevelUtil.TriggerDeath("Automated Turret");
        }

        [HideFromIl2Cpp]
        IEnumerator Co_TurretHumanDeath()
        {
            yield return new WaitForSeconds(1);
            TriggerAlarm(0.3f);
            yield return new WaitForSeconds(1);
            AudioUtil.PlayAt("Intruder_Turret.wav", VRRig.transform.position, 1, true);
            yield return new WaitForSeconds(4.3f);

            yield return Co_AimAllTurretsThenFire();

            float waitForActualFire = Mathf.Max(0f, crackDelayAfterFireStarts);
            if (waitForActualFire > 0f)
                yield return new WaitForSeconds(waitForActualFire);

            yield return null;
            DamageOverlayDriver.TriggerTurretDeath();
            yield return new WaitForSeconds(1.5f);
            LevelUtil.blindPlayer(true);
            LevelUtil.TriggerDeath("Automated Turret");

        }

        public float explosionOverlayDelay = 0.02f;
        public float explosionShakeDuration = 1.5f;
        public float explosionShakeStrengthPos = 0.045f;
        public float explosionShakeStrengthRot = 2.2f;
        public float explosionShakeFrequency = 30f;
        public float explosionInitialJoltBack = 0.11f;
        public float explosionInitialJoltDown = 0.045f;
        public float blindDelayAfterExplosion = 0.02f;

        [HideFromIl2Cpp]
        IEnumerator Co_ExplosionDroneDeath()
        {
            GameObject hs = GameObject.Find("Headset");
            if (hs != null)
            {
                BoxCollider bc = hs.GetComponent<BoxCollider>();
                if (bc != null) bc.enabled = false;
            }

            if (headsetDriver != null)
            {
                try { headsetDriver.DroneShot(); }
                catch (Exception e)
                {
                    MelonLogger.Warning("[HeistLevelManager] Headset DroneShot failed: " + e.Message);
                }
            }

            if (hs != null)
            {
                HeadsetScript hss = hs.GetComponent<HeadsetScript>();
                if (hss != null && hss.headsetLoop != null && hss.headsetLoop._source != null)
                {
                    hss.headsetLoop._source.clip = HeistBundle2Manager.GetAudio("DroneDeadStatic.wav");
                    hss.headsetLoop._source.volume = 1f;
                    hss.headsetLoop._source.Play();
                }
            }

            if (explosionOverlayDelay > 0f)
                yield return new WaitForSeconds(explosionOverlayDelay);

            if (VRRig == null)
            {
                LevelUtil.blindPlayer(true);
                deathRoutineHandle = null;
                yield break;
            }

            Transform rigT = VRRig.transform;
            Vector3 startPos = rigT.position;
            Quaternion startRot = rigT.rotation;

            Vector3 rigRight = startRot * Vector3.right;
            Vector3 rigUp = startRot * Vector3.up;
            Vector3 rigForward = startRot * Vector3.forward;

            float t = 0f;
            float dur = explosionShakeDuration;
            if (dur < 0.01f) dur = 0.01f;

            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);

                float fade = 1f - u;
                fade *= fade;

                float trauma = Mathf.Sin(u * 3.14159274f);
                float amp = fade * (0.7f + trauma * 0.3f);

                float nx = Mathf.PerlinNoise(Time.time * explosionShakeFrequency, 0.13f) * 2f - 1f;
                float ny = Mathf.PerlinNoise(0.27f, Time.time * (explosionShakeFrequency * 0.93f)) * 2f - 1f;
                float nz = Mathf.PerlinNoise(Time.time * (explosionShakeFrequency * 0.81f), 0.61f) * 2f - 1f;

                float rx = Mathf.PerlinNoise(Time.time * (explosionShakeFrequency * 0.88f), 1.17f) * 2f - 1f;
                float ry = Mathf.PerlinNoise(2.03f, Time.time * (explosionShakeFrequency * 1.02f)) * 2f - 1f;
                float rz = Mathf.PerlinNoise(Time.time * (explosionShakeFrequency * 1.11f), 2.71f) * 2f - 1f;

                float joltIn = 1f - Mathf.Clamp01(t / 0.16f);
                Vector3 initialJolt =
                    (-rigForward * explosionInitialJoltBack + Vector3.down * explosionInitialJoltDown) * joltIn;

                Vector3 shakeOffset =
                    rigRight * (nx * explosionShakeStrengthPos * amp) +
                    rigUp * (ny * explosionShakeStrengthPos * 0.75f * amp) +
                    rigForward * (nz * explosionShakeStrengthPos * 0.35f * amp);

                float pitch = rx * explosionShakeStrengthRot * 0.85f * amp;
                float yaw = ry * explosionShakeStrengthRot * 0.65f * amp;
                float roll = rz * explosionShakeStrengthRot * 1.15f * amp;

                rigT.position = startPos + initialJolt + shakeOffset;
                rigT.rotation = startRot * Quaternion.Euler(pitch, yaw, roll);

                yield return null;
            }

            rigT.position = startPos;
            rigT.rotation = startRot;

            if (blindDelayAfterExplosion > 0f)
                yield return new WaitForSeconds(blindDelayAfterExplosion);

            LevelUtil.blindPlayer(true);
            deathRoutineHandle = null;
        }

        public static void playStinger(float delay = 0)
        {
            if (delay == 0) AudioUtil.PlayAt("SuccessStinger.wav", Instance.VRRig.transform.position, 0.7f);
            else MelonCoroutines.Start(Instance.stingerDelay(delay));
        }

        [HideFromIl2Cpp]
        System.Collections.IEnumerator stingerDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            AudioUtil.PlayAt("SuccessStinger.wav", Instance.VRRig.transform.position, 0.7f);
        }

        public static void playHandler(string clip)
        {
            playHandler(clip, 0f, false);
        }

        public static void playHandler(string clip, bool interruptibleByFutureClips)
        {
            playHandler(clip, 0f, interruptibleByFutureClips);
        }

        public static void playHandler(string clip, float delay)
        {
            playHandler(clip, delay, false);
        }

        public static void playHandler(string clip, float delay, bool interruptibleByFutureClips = false)
        {
            if (Instance == null) return;

            Instance.handlerRequestsActive++;
            MelonCoroutines.Start(Instance.HandlerDelay(clip, delay, interruptibleByFutureClips));
        }

        bool handlerTalking = false;
        bool currentHandlerCanBeInterrupted = false;
        AudioSource currentHandlerSource = null;
        int handlerQueueGeneration = 0;
        int handlerRequestsActive = 0;

        void CutOffCurrentHandler()
        {
            handlerQueueGeneration++;
            handlerTalking = false;
            currentHandlerCanBeInterrupted = false;

            if (currentHandlerSource != null)
            {
                try { currentHandlerSource.Stop(); } catch { }
                currentHandlerSource = null;
            }
        }

        [HideFromIl2Cpp]
        private System.Collections.IEnumerator HandlerDelay(string clipName, float delay, bool interruptibleByFutureClips = false)
        {
            int myGeneration = handlerQueueGeneration;

            try
            {
                if (delay > 0f)
                    yield return new WaitForSeconds(delay);

                if (myGeneration != handlerQueueGeneration)
                    yield break;

                while (handlerTalking)
                {
                    if (myGeneration != handlerQueueGeneration)
                        yield break;


                    if (currentHandlerCanBeInterrupted)
                    {
                        CutOffCurrentHandler();
                        myGeneration = handlerQueueGeneration;
                        break;
                    }

                    yield return null;
                }

                if (myGeneration != handlerQueueGeneration)
                    yield break;

                handlerTalking = true;
                currentHandlerCanBeInterrupted = interruptibleByFutureClips;

                var src = AudioUtil.PlayAt(clipName, VRRig.transform.position, 1, true);
                currentHandlerSource = src;

                float len = 0f;
                if (src != null && src.clip != null) len = src.clip.length;
                else
                {
                    var clip = HeistBundle2Manager.GetAudio(clipName);
                    if (clip != null) len = clip.length;
                }

                if (len > 0.01f) yield return new WaitForSeconds(len + 0.8f);
                else yield return new WaitForSeconds(1.0f);

                if (myGeneration == handlerQueueGeneration)
                {
                    handlerTalking = false;
                    currentHandlerCanBeInterrupted = false;

                    if (currentHandlerSource == src)
                        currentHandlerSource = null;
                }
            }
            finally
            {
                handlerRequestsActive--;
                if (handlerRequestsActive < 0)
                    handlerRequestsActive = 0;
            }
        }

        [HideFromIl2Cpp]
        System.Collections.IEnumerator WaitForHandlerQueueToFinish(float maxWaitSeconds)
        {
            float elapsed = 0f;

            while ((handlerTalking || handlerRequestsActive > 0) && elapsed < maxWaitSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (elapsed >= maxWaitSeconds)
                MelonLogger.Warning("[HeistLevelManager] Timed out waiting for handler queue before WinRoom load.");
        }

        public static void FoundSouvenir(int id)
        {
            if (SaveManager.HasSouvenir(id)) return;

            SaveManager.UnlockSouvenir(id);
            if (id != 0) AudioUtil.PlayAt("MementoStinger.ogg", Instance.VRRig.transform.position, 1, true);

            playSouvenirLine(id);
        }

        static void playSouvenirLine(int id)
        {
            if (id == 0) return;
            string[] lines = new string[] {"", "Handler_BearDown.wav", "Handler_Housekeeping.wav", "Handler_RedTape.wav",
                                           "Handler_SmashNGrab.wav", "Handler_FriendlyFire.wav" };
            playHandler(lines[id], 2f);
        }

        public static void TriggerElevator()
        {
            MelonCoroutines.Start(Instance.Co_TriggerElevator());
        }

        [HideFromIl2Cpp]
        System.Collections.IEnumerator Co_TriggerElevator()
        {
            yield return new WaitForSeconds(0.5f);
            playStinger();
            playHandler("Handler_ElevatorStart.wav", 1);

            yield return new WaitForSeconds(1.5f);

            AudioSource groundshake = AudioUtil.PlayAt("GroundShaking.wav", VRRig.transform.position, 0.6f, true);
            GameObject.Find("HMD").GetComponent<CameraShakeDriver>().TurnOn();

            float elapsed = 0f;
            bool forcedOff = false;

            while (elapsed < 11f)
            {
                if (!forcedOff && headsetScript != null && headsetScript.IsWearing)
                {

                    if (elapsed >= 10.3f)
                    {
                        headsetScript.ForceHeadsetOff();
                        forcedOff = true;
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (elevator == null)
                elevator = GameObject.Find("ElevatorFloor").GetComponent<ElevatorScript>();

            elevator.Descend();
            AudioUtil.PlayAt("Elevator.wav", elevator.transform.position);

            yield return new WaitForSeconds(11f);

            groundshake.Stop();
            playHandler("Handler_ElevatorEnd.wav");
        }

        public static void flipWindowCols(bool inVent)
        {

            MelonLogger.Msg($"[FlipWindowCols] - Flipping - inVent = {inVent}");
            BoxCollider[] hbcols = Instance.saferoomHB.GetComponents<BoxCollider>();
            BoxCollider hbCol = hbcols[1];

            BoxCollider winCol = Instance.windowCol.GetComponent<BoxCollider>();

            if (inVent)
            {
                winCol.enabled = false;
                hbCol.enabled = true;
            }
            else
            {
                winCol.enabled = true;
                hbCol.enabled = false;
            }
        }

    }
}
