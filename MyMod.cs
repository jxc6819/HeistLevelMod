using MelonLoader;
using System;
using UnhollowerRuntimeLib;
using UnityEngine;

[assembly: MelonInfo(typeof(IEYTD_Mod2Code.MyMod), "IEYTD2 New Mod Bootstrap", "1.0.2", "James Connors")]
[assembly: MelonGame("Schell Games", "I Expect You To Die 2")]

namespace IEYTD_Mod2Code
{
    public class MyMod : MelonMod
    {

        public string bundleFileName = "mod2_bundle";
        public string fallbackSceneAssetPath = "Assets/Scenes/SampleScene.unity";

        public readonly string mergedRootName = "ModLevel_ROOT";
        public readonly float sceneLoadTimeout = 15f;

        public string[] keepNameContains = new[]
        {
            "vr", "camera", "rig", "phoenix", "gamemenu"
        };

        public string[] gatherSceneNames = new string[] { "BackStage_Level", "Elevator_Level" };
        public string[][] gatherObjectNamesPerScene = new string[][]
        {
            new string[] { "P_BSP_BannerPulleyLever"},
            new string[] { "ELV_NuclearFootball" }
        };

        public bool reloadVanSingleAfterGather = true;

        private LevelLoader _levelLoader;
        private ObjectBank _bank;

        private GatherGameAssets _preGather;
        private bool _gatherInProgress;

        private bool _readyFired;
        public static MyMod Instance;

        public override void OnInitializeMelon()
        {
            _levelLoader = new LevelLoader(
                bundleFileName: bundleFileName,
                fallbackSceneAssetPath: fallbackSceneAssetPath,
                mergedRootName: mergedRootName,
                sceneLoadTimeout: sceneLoadTimeout,
                keepNameContains: keepNameContains
            );

            _levelLoader.OnMergeFinished = (root) =>
            {
                _readyFired = false;

                SetupObjectBank(root);

                if (_preGather != null && _preGather.Done)
                {
                    _preGather.PlantInto(root);

                    if (_bank != null)
                        _bank.RefreshAll();
                }

                MelonCoroutines.Start(Co_WaitForFullyReady(root));
            };

            ClassInjector.RegisterTypeInIl2Cpp<ObjectBank>();
            ClassInjector.RegisterTypeInIl2Cpp<GatherGameAssets>();
            ClassInjector.RegisterTypeInIl2Cpp<HeadsetScript>();
            ClassInjector.RegisterTypeInIl2Cpp<HeadsetDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<LaserEmitter>();
            ClassInjector.RegisterTypeInIl2Cpp<LaserColumnSpawner>();
            ClassInjector.RegisterTypeInIl2Cpp<WireVisionManager>();
            ClassInjector.RegisterTypeInIl2Cpp<LeverScript>();
            ClassInjector.RegisterTypeInIl2Cpp<Hotspot>();
            ClassInjector.RegisterTypeInIl2Cpp<DroneHand>();
            ClassInjector.RegisterTypeInIl2Cpp<DroneRotationalMotion>();
            ClassInjector.RegisterTypeInIl2Cpp<DronePullMotion>();
            ClassInjector.RegisterTypeInIl2Cpp<WiresPullMotion>();
            ClassInjector.RegisterTypeInIl2Cpp<SparkDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<DronePickUp>();
            ClassInjector.RegisterTypeInIl2Cpp<DronePickUpHitbox>();
            ClassInjector.RegisterTypeInIl2Cpp<DroneDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<RocketDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<ScreenDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<Keypad>();
            ClassInjector.RegisterTypeInIl2Cpp<KeypadButton>();
            ClassInjector.RegisterTypeInIl2Cpp<DroneVentScript>();
            ClassInjector.RegisterTypeInIl2Cpp<DroneVentRailTK>();
            ClassInjector.RegisterTypeInIl2Cpp<ElevatorScript>();
            ClassInjector.RegisterTypeInIl2Cpp<HeistLevelManager>();
            ClassInjector.RegisterTypeInIl2Cpp<GrateScript>();
            ClassInjector.RegisterTypeInIl2Cpp<BoltScript>();
            ClassInjector.RegisterTypeInIl2Cpp<VaultPuzzleManager>();
            ClassInjector.RegisterTypeInIl2Cpp<Dial>();
            ClassInjector.RegisterTypeInIl2Cpp<SubGear>();
            ClassInjector.RegisterTypeInIl2Cpp<MainGear>();
            ClassInjector.RegisterTypeInIl2Cpp<InfiniteDroneRotationalMotion>();
            ClassInjector.RegisterTypeInIl2Cpp<Keycard>();
            ClassInjector.RegisterTypeInIl2Cpp<KeycardTrigger>();
            ClassInjector.RegisterTypeInIl2Cpp<Turret>();
            ClassInjector.RegisterTypeInIl2Cpp<TurretDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<PhoenixButtonHook>();
            ClassInjector.RegisterTypeInIl2Cpp<DamageOverlayDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<GlassDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<LoopingSfx>();
            ClassInjector.RegisterTypeInIl2Cpp<GuardReactionDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<AlarmDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<InfinitePlayerWheelTK>();
            ClassInjector.RegisterTypeInIl2Cpp<ExplosionDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<CameraShakeDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<AgentAvatarDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<DronePackedState>();
            ClassInjector.RegisterTypeInIl2Cpp<SuitcaseColliderProbe>();
            ClassInjector.RegisterTypeInIl2Cpp<SuitcaseStateManager>();
            ClassInjector.RegisterTypeInIl2Cpp<HeadsetPackedState>();
            ClassInjector.RegisterTypeInIl2Cpp<ZorCase>();
            ClassInjector.RegisterTypeInIl2Cpp<HiddenVolumeController>();
            ClassInjector.RegisterTypeInIl2Cpp<CustomHiddenVolume>();
            ClassInjector.RegisterTypeInIl2Cpp<VanStartButtonHook>();
            ClassInjector.RegisterTypeInIl2Cpp<PickUpPile>();
            ClassInjector.RegisterTypeInIl2Cpp<BearHeadListener>();
            ClassInjector.RegisterTypeInIl2Cpp<TrashTrigger>();
            ClassInjector.RegisterTypeInIl2Cpp<WrenchStartPose>();
            ClassInjector.RegisterTypeInIl2Cpp<CollisionSound>();
            ClassInjector.RegisterTypeInIl2Cpp<HiddenTrophy>();
            ClassInjector.RegisterTypeInIl2Cpp<DroneHandVan>();
            ClassInjector.RegisterTypeInIl2Cpp<DroneHandVanTrigger>();
            ClassInjector.RegisterTypeInIl2Cpp<LaserPointer>();
            ClassInjector.RegisterTypeInIl2Cpp<PauseScript>();
            ClassInjector.RegisterTypeInIl2Cpp<WinRoomScript>();

            HarmonyInstance.PatchAll();
            Instance = this;
            HeistBundle2Manager.Init();
            SaveManager.Load();
            WinRoomScript.Install();

        }

        public override void OnUpdate()
        {

        }

        static void DebugRayHit(Transform rayOrigin, float maxDist = 15f)
        {
            if (rayOrigin == null) { MelonLogger.Warning("[RayDbg] rayOrigin is null"); return; }

            Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, maxDist, ~0, QueryTriggerInteraction.Ignore))
            {
                var go = hit.collider.gameObject;
                MelonLogger.Msg(
                    $"[RayDbg] Hit '{go.name}' | layer={go.layer} | collider={hit.collider.GetType().Name} | dist={hit.distance:F3}"
                );
            }
            else
            {
                MelonLogger.Msg("[RayDbg] No hit.");
            }
            Debug.DrawRay(ray.origin, ray.direction * 10f, Color.red, 2f);
        }

        public static void _RequestLoad()
        {
            Instance.RequestLoad();
        }
        private void RequestLoad()
        {
            LevelUtil.HellenKeller(true);
            if (_levelLoader == null)
            {
                MelonLogger.Error("[MyMod] LevelLoader not initialized.");
                return;
            }

            if (_levelLoader.IsLoading)
            {
                MelonLogger.Warning("[MyMod] Merge already in progress.");
                return;
            }

            if (IsGatherConfigured())
            {
                if (_gatherInProgress)
                {
                    MelonLogger.Warning("[MyMod] Gather already in progress.");
                    return;
                }

                _gatherInProgress = true;
                MelonCoroutines.Start(Co_GatherThenMerge());
                return;
            }

            StartMerge();
        }

        private IEnumerator Co_GatherThenMerge()
        {
            try
            {
                ResetPreGatherer();
                EnsurePreGatherer();

                _preGather.BeginPreMerge();

                while (_preGather != null && !_preGather.Done)
                    yield return null;

                if (_preGather == null)
                {
                    MelonLogger.Error("[MyMod] Pre-gatherer disappeared before merge could begin.");
                    yield break;
                }

                StartMerge();
            }
            finally
            {
                _gatherInProgress = false;
            }
        }

        private void StartMerge()
        {
            MelonLogger.Msg("[MyMod] Starting merge...");
            _readyFired = false;
            _levelLoader.BeginMerge();
        }

        private bool IsGatherConfigured()
        {
            if (gatherSceneNames == null || gatherObjectNamesPerScene == null) return false;
            if (gatherSceneNames.Length == 0 || gatherObjectNamesPerScene.Length == 0) return false;

            if (gatherObjectNamesPerScene.Length != gatherSceneNames.Length)
            {
                MelonLogger.Error("[MyMod] Gather arrays mismatch. objectNamesPerScene must match sceneNames length.");
                return false;
            }

            for (int i = 0; i < gatherObjectNamesPerScene.Length; i++)
            {
                var arr = gatherObjectNamesPerScene[i];
                if (arr != null && arr.Length > 0)
                    return true;
            }

            return false;
        }

        private void ResetPreGatherer()
        {
            if (_preGather == null) return;

            try
            {
                _preGather.ClearGathered();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MyMod] ResetPreGatherer ClearGathered failed: {ex}");
            }

            try
            {
                if (_preGather.gameObject != null)
                    UnityEngine.Object.Destroy(_preGather.gameObject);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MyMod] ResetPreGatherer destroy failed: {ex}");
            }

            _preGather = null;
        }

        private void EnsurePreGatherer()
        {
            if (_preGather != null) return;

            var go = new GameObject("GatherGameAssets_PRE");
            GameObject.DontDestroyOnLoad(go);

            _preGather = go.AddComponent<GatherGameAssets>();
            _preGather.sceneNames = gatherSceneNames;
            _preGather.objectNamesPerScene = gatherObjectNamesPerScene;

            _preGather.reloadVanSingleAfterGather = reloadVanSingleAfterGather;
            _preGather.reloadSceneName = "Van";

            _preGather.ensureDonorPickUpClone = true;
        }

        private IEnumerator Co_WaitForFullyReady(GameObject root)
        {

            if (_readyFired) yield break;
            _readyFired = true;

            MergeDone(root);
        }

        private void MergeDone(GameObject root)
        {
            SetPlayerPos();
            LevelUtil.OnLevelLoaded(root, null);
            setGrabbables();
            attachScripts();
            MiscStuff();
        }

        public void SetPlayerPos()
        {
            GameObject spawnObj = (_bank != null && _bank.PlayerSpawn != null) ? _bank.PlayerSpawn : GameObject.Find("PlayerSpawn");
            GameObject rig = (_bank != null && _bank.PlayerRig != null) ? _bank.PlayerRig : GameObject.Find("VRRig");

            if (!spawnObj || !rig)
            {
                MelonLogger.Warning("[SetPlayerPos] Missing PlayerSpawn or VRRig.");
                return;
            }

            Vector3 playerSpawn = spawnObj.transform.position;
            rig.transform.position = new Vector3(playerSpawn.x, rig.transform.position.y, playerSpawn.z);
            rig.transform.rotation = Quaternion.Euler(new Vector3(0, 0, 0));
        }

        public void setGrabbables()
        {
            GameObject drone = GameObject.Find("Drone");
            GameObject headset = GameObject.Find("Headset");

            if (drone != null)
                LevelUtil.MakeGrabbable(drone);

            if (headset != null)
                LevelUtil.MakeGrabbable(headset);

            GameObject droneHost = GameObject.Find("PickUp_HOST_Drone");
            droneHost.transform.localScale = new Vector3(0.07f, 0.07f, 0.07f);
            droneHost.GetComponent<PickUp>().enabled = true;

            GameObject headsetHost = GameObject.Find("PickUp_HOST_Headset");
            headsetHost.GetComponent<PickUp>().enabled = true;

            Vector3 wrenchLocalPos = new Vector3(3.5f, -1.7f, 0);
            Vector3 wrenchLocalRot = new Vector3(0, 0, 90);
            Vector3 wrenchStaticPos = new Vector3(2, -1.7f, 0);
            Vector3 wrenchStaticRot = new Vector3(0, 0, 90);
            LevelUtil.MakeGrabbable("Wrench");
            GameObject wrenchHost = GameObject.Find("PickUp_HOST_Wrench");
            wrenchHost.GetComponent<PickUp>().enabled = true;
            _bank.PickUps.Add(wrenchHost);
            MakeDroneGrabbable(wrenchHost);
            wrenchHost.GetComponent<DronePickUp>().SetLocals(wrenchLocalPos, wrenchLocalRot, wrenchStaticPos, wrenchStaticRot);
            CollisionSound wrench1Col = wrenchHost.AddComponent<CollisionSound>();
            wrench1Col.Init(HeistBundle2Manager.GetAudio("WrenchCollision.ogg"));

            LevelUtil.MakeGrabbable("Wrench2");
            GameObject wrench2Host = GameObject.Find("PickUp_HOST_Wrench2");
            wrench2Host.GetComponent<PickUp>().enabled = true;
            _bank.PickUps.Add(wrench2Host);
            MakeDroneGrabbable(wrench2Host);
            wrench2Host.GetComponent<DronePickUp>().SetLocals(wrenchLocalPos, wrenchLocalRot, wrenchStaticPos, wrenchStaticRot);
            wrench2Host.AddComponent<WrenchStartPose>();
            CollisionSound wrench2Col = wrench2Host.AddComponent<CollisionSound>();
            wrench2Col.Init(HeistBundle2Manager.GetAudio("WrenchCollision.ogg"));

            string[] cardColors = new string[] { "Blue", "Red", "Yellow" };
            Vector3 cardLocalPos = new Vector3(0, -2.7f, 0);
            Vector3 cardLocalRot = new Vector3(0, 0, 90);

            for (int i = 0; i < cardColors.Length; i++)
            {
                string color = cardColors[i];
                string cardName = "Keycard" + color;
                string cardParentName = "PickUp_HOST_" + cardName;
                LevelUtil.MakeGrabbable(cardName);
                GameObject keyCardBHost = GameObject.Find(cardParentName);
                keyCardBHost.GetComponent<PickUp>().enabled = true;
                _bank.PickUps.Add(keyCardBHost);
                MakeDroneGrabbable(keyCardBHost);
                keyCardBHost.GetComponent<DronePickUp>().SetLocals(cardLocalPos, cardLocalRot, new Vector3(0, -2.3f, 0), cardLocalRot);

            }

            GameObject grid = GameObject.Find("GridBlts");
            LevelUtil.MakeGrabbable(grid);
            GameObject gridP = grid.transform.parent.gameObject;
            gridP.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
            MakeDroneGrabbable(gridP);
            gridP.GetComponent<PickUp>().enabled = true;
            _bank.PickUps.Add(gridP);
            gridP.GetComponent<DronePickUp>().SetLocals(new Vector3(-7, -1.7f, -7), new Vector3(90, 0, 0));
            CollisionSound gridColSound = gridP.AddComponent<CollisionSound>();
            gridColSound.Init(HeistBundle2Manager.GetAudio("VentCollision.ogg"), HeistBundle2Manager.GetAudio("VentCollision_Hard.ogg"), 0.75f, 3f);

            GameObject trashcan = GameObject.Find("TrashcanObj");
            LevelUtil.MakeGrabbable(trashcan);
            GameObject trashP = trashcan.transform.parent.gameObject;
            trashP.GetComponent<PickUp>().enabled = true;
            trashP.GetComponent<PickUp>()._BoundRadius = 0.4f;
            trashP.GetComponent<PickUp>()._LocalBoundCenter = new Vector3(0, 0.4f, 0);

            GameObject zorCase = GameObject.Find("ZoraxisCase");
            LevelUtil.MakeGrabbable(zorCase);
            GameObject zorCaseP = zorCase.transform.parent.gameObject;
            MakeDroneGrabbable(zorCaseP);
            zorCaseP.GetComponent<PickUp>().enabled = true;
            _bank.PickUps.Add(zorCaseP);

            zorCase.AddComponent<ZorCase>();

            GameObject hiddenTrophy = GameObject.Find("HiddenTrophy");
            LevelUtil.MakeGrabbable(hiddenTrophy);
            GameObject hiddenTrophyP = hiddenTrophy.transform.parent.gameObject;
            MakeDroneGrabbable(hiddenTrophyP);
            hiddenTrophyP.GetComponent<PickUp>().enabled = true;
            _bank.PickUps.Add(hiddenTrophyP);

            hiddenTrophy.AddComponent<HiddenTrophy>();
            hiddenTrophyP.GetComponent<DronePickUp>().SetLocals(
                new Vector3(0, -2.5f, 0), new Vector3(0, 270, 270), new Vector3(0, -1.7f, 0), new Vector3(0, 90, 90));

            GameObject moneyPiles = GameObject.Find("MoneyPiles");
            GameObject moneyBill = GameObject.Find("MoneyBill");
            GameObject[] piles = new GameObject[moneyPiles.transform.childCount];
            for(int i = 0; i < moneyPiles.transform.childCount; i++)
            {
                piles[i] = moneyPiles.transform.GetChild(i).gameObject;
            }

            for(int i = 0; i < piles.Length; i++)
            {
                GameObject moneyPile = piles[i];
                LevelUtil.MakeGrabbable(moneyPile);
                GameObject moneyPileP = moneyPile.transform.parent.gameObject;
                moneyPileP.GetComponent<Rigidbody>().isKinematic = true;
                moneyPileP.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
                moneyPileP.GetComponent<PickUp>().enabled = true;
                PickUpPile moneyPUP = moneyPileP.AddComponent<PickUpPile>();
                moneyPUP.Init(moneyBill);
            }

            GameObject PaperScatters = GameObject.Find("PaperScatters");
            GameObject PaperCrumples = GameObject.Find("PaperCrumples");
            GameObject[] paperScatters = new GameObject[PaperScatters.transform.childCount];
            GameObject[] paperCrumples = new GameObject[PaperCrumples.transform.childCount];
            for(int i = 0; i < paperScatters.Length; i++)
            {
                paperScatters[i] = PaperScatters.transform.GetChild(i).gameObject;
            }
            for (int i = 0; i < paperCrumples.Length; i++)
            {
                paperCrumples[i] = PaperCrumples.transform.GetChild(i).gameObject;
            }

            for (int i = 0; i < paperScatters.Length; i++)
            {
                GameObject scatter = paperScatters[i];
                LevelUtil.MakeGrabbable(scatter);
                GameObject scatterP = scatter.transform.parent.gameObject;
                scatterP.GetComponent<Rigidbody>().isKinematic = true;
                scatterP.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
                scatterP.GetComponent<PickUp>().enabled = true;
                PickUpPile scatterPUP = scatterP.AddComponent<PickUpPile>();

                GameObject crumple = paperCrumples[UnityEngine.Random.Range(0, paperCrumples.Length)];
                scatterPUP.Init(crumple, false, false);
            }

        }

        void attachScripts()
        {
            GameObject Manager = GameObject.Find("Manager");
            if (Manager != null)
            {
                if (Manager.GetComponent<LaserColumnSpawner>() == null) Manager.AddComponent<LaserColumnSpawner>();
                if (Manager.GetComponent<WireVisionManager>() == null) Manager.AddComponent<WireVisionManager>();
            }
            Manager.AddComponent<HeistLevelManager>();
            Manager.AddComponent<AlarmDriver>();
            Manager.AddComponent<VaultPuzzleManager>();
            Manager.AddComponent<DamageOverlayDriver>();
            Manager.AddComponent<PauseScript>();

            GameObject Headset = GameObject.Find("Headset");
            if (Headset != null)
            {
                if (Headset.GetComponent<HeadsetScript>() == null) Headset.AddComponent<HeadsetScript>();
                if (Headset.GetComponent<HeadsetDriver>() == null) Headset.AddComponent<HeadsetDriver>();
            }

            _bank.Lever.AddComponent<LeverScript>();
            GameObject electricalDoor = GameObject.Find("SM_door");
            electricalDoor.AddComponent<DroneRotationalMotion>().enabled = false;
            LevelUtil.MakeRotationalMotion(electricalDoor, Vector3.up, 0, 180);
            _bank.RotMotions.Add(electricalDoor);
            electricalDoor.transform.parent.gameObject.SetActive(true);
            Transform handle = electricalDoor.transform.parent.Find("Pulley Handle Center");
            handle.localPosition = new Vector3(0f, 0.5f, -1.1f);
            GameObject.Find("SM_wires").AddComponent<WiresPullMotion>();

            GameObject maintenanceDoor = GameObject.Find("MaintenanceDoorEmpty");
            DroneRotationalMotion mDRM = maintenanceDoor.AddComponent<DroneRotationalMotion>();
            mDRM.enabled = false;
            mDRM.localAxis = Vector3.right;
            mDRM.maxAngle = 90;
            LevelUtil.MakeRotationalMotion(maintenanceDoor, Vector3.right, 0, 90);
            _bank.RotMotions.Add(maintenanceDoor);
            maintenanceDoor.transform.parent.gameObject.SetActive(true);
            maintenanceDoor.transform.parent.gameObject.GetComponent<RotationalMotion>()._TKSpeedMultiplier = 6;

            GameObject vaultPivot = GameObject.Find("VaultPivot");
            DroneRotationalMotion vDRM = vaultPivot.AddComponent<DroneRotationalMotion>();
            vDRM.enabled = false;
            vDRM.localAxis = Vector3.up;
            vDRM.maxAngle = 90;
            LevelUtil.MakeRotationalMotion(vaultPivot, Vector3.up, 0, 90);
            vaultPivot.transform.parent.gameObject.SetActive(true);
            vaultPivot.transform.parent.gameObject.GetComponent<RotationalMotion>()._TKSpeedMultiplier = 4.5f;
            vaultPivot.transform.parent.gameObject.GetComponent<RotationalMotion>().enabled = false;
            Transform vaultHandle = vaultPivot.transform.parent.Find("Pulley Handle Center");
            vaultHandle.localPosition = new Vector3(-2.7f, -0.6f, 0);

            GameObject.Find("HMD").AddComponent<CameraShakeDriver>();

            Light mLight = GameObject.Find("MaintenanceLight").GetComponent<Light>();
            mLight.type = LightType.Point;
            mLight.range = 3f;
            mLight.intensity = 0.005f;
            mLight.shadows = LightShadows.Hard;
            mLight.shadowResolution = UnityEngine.Rendering.LightShadowResolution.High;
            mLight.shadowStrength = 1f;
            mLight.shadowBias = 0.02f;
            mLight.shadowNormalBias = 0.03f;
            mLight.shadowNearPlane = 0.1f;

            GameObject vaultWheel = GameObject.Find("RealVaultWheel");
            vaultWheel.AddComponent<DroneRotationalMotion>().enabled = false;
            LevelUtil.MakeRotationalMotion(vaultWheel, Vector3.back, 0, 0);
            _bank.RotMotions.Add(vaultWheel);
            vaultWheel.transform.parent.gameObject.SetActive(true);
            vaultWheel.transform.parent.gameObject.GetComponent<RotationalMotion>()._TKSpeedMultiplier = 6;
            InfinitePlayerWheelTK vaultWheelTK = vaultWheel.transform.parent.gameObject.AddComponent<InfinitePlayerWheelTK>();
            vaultWheelTK.Init(vaultWheel.transform);

            Keycard keyCardBlue = GameObject.Find("KeycardBlue").AddComponent<Keycard>();
            Keycard keyCardYellow = GameObject.Find("KeycardYellow").AddComponent<Keycard>();
            Keycard keyCardRed = GameObject.Find("KeycardRed").AddComponent<Keycard>();
            KeycardTrigger cardSwiper = GameObject.Find("CardSwiper").AddComponent<KeycardTrigger>();

            Hotspot electricalHotspot = GameObject.Find("ElectricalBoxHotspot").AddComponent<Hotspot>();
            electricalHotspot.Init(new Vector3(4.45f, 6.2f, -6.7f), new Vector3(0, 180, 0));

            Hotspot shutterHotspot = GameObject.Find("ShutterHotspot").AddComponent<Hotspot>();
            shutterHotspot.Init(new Vector3(0.1f, 8.05f, 2.4f), new Vector3(0, 0, 0));

            Hotspot ventHotspot = GameObject.Find("VentHotspot").AddComponent<Hotspot>();
            ventHotspot.Init(new Vector3(-0.0626f, 7.7209f, 11.5f), new Vector3(0, 180, 0));

            Hotspot keypadHotspot = GameObject.Find("KeypadHotspot").AddComponent<Hotspot>();
            keypadHotspot.Init(new Vector3(1.6589f, 1.7422f, 13.2f), Vector3.zero);

            Hotspot vaultHotspot = GameObject.Find("MaintenanceBox").AddComponent<Hotspot>();
            vaultHotspot.Init(new Vector3(-2f, -2.3138f, -3.55f), Vector3.zero);

            GameObject Drone = GameObject.Find("Drone");
            Drone.AddComponent<DroneDriver>();
            GameObject.Find("PickUp_HOST_Drone").AddComponent<DroneVentRailTK>().enabled = false;
            GameObject.Find("BotLeftHandRocket").AddComponent<RocketDriver>();
            GameObject.Find("BotRightHandRocket").AddComponent<RocketDriver>();

            GameObject keypad = GameObject.Find("KeypadStandard");
            Keypad keypadScript = keypad.AddComponent<Keypad>();
            Transform buttonsRoot = keypad.transform.GetChild(0).GetChild(0);
            for (int i = 0; i < buttonsRoot.childCount; i++)
            {
                GameObject child = buttonsRoot.GetChild(i).gameObject;
                KeypadButton button = child.AddComponent<KeypadButton>();
                button.value = i.ToString();
                button.keypad = keypadScript;
            }
            buttonsRoot.GetChild(10).gameObject.GetComponent<KeypadButton>().value = "enter";
            buttonsRoot.GetChild(11).gameObject.GetComponent<KeypadButton>().value = "clear";
            buttonsRoot.GetChild(12).gameObject.GetComponent<KeypadButton>().value = "*";

            GameObject.Find("SaferoomScreen").AddComponent<ScreenDriver>();
            GameObject.Find("ElevatorFloor").AddComponent<ElevatorScript>();
            GameObject.Find("GridBlts").AddComponent<GrateScript>();

            GameObject turret1 = GameObject.Find("TurretPivot1");
            turret1.AddComponent<Turret>();
            turret1.AddComponent<TurretDriver>();
            GameObject turret2 = GameObject.Find("TurretPivot2");
            turret2.AddComponent<Turret>();
            turret2.AddComponent<TurretDriver>();

            GameObject turret3 = GameObject.Find("TurretPivot3");
            turret3.AddComponent<Turret>();
            turret3.AddComponent<TurretDriver>();
            GameObject turret4 = GameObject.Find("TurretPivot4");
            turret4.AddComponent<Turret>();
            turret4.AddComponent<TurretDriver>();
            GameObject.Find("Saferoom_Window").AddComponent<GlassDriver>();

            GameObject.Find("Guard").AddComponent<GuardReactionDriver>();

            GameObject.Find("PlayerModel").AddComponent<AgentAvatarDriver>();
            GameObject.Find("PickUp_HOST_Drone").AddComponent<CustomHiddenVolume>();

            GameObject.Find("BankBear1").AddComponent<BearHeadListener>();
            GameObject.Find("BankBear2").AddComponent<BearHeadListener>();

            MelonLogger.Msg("ATTACHSCRIPTS CHECKPOINT");
            GameObject trashCan = GameObject.Find("TrashcanObj");
            TrashTrigger trashInTrigger = trashCan.transform.GetChild(0).gameObject.AddComponent<TrashTrigger>();
            trashInTrigger.Init(1);
            TrashTrigger trashTopTrigger = trashCan.transform.GetChild(1).gameObject.AddComponent<TrashTrigger>();
            trashTopTrigger.Init(0);
            TrashTrigger trashAllTrigger = trashCan.transform.GetChild(2).gameObject.AddComponent<TrashTrigger>();
            trashAllTrigger.Init(2);
        }

        void MiscStuff()
        {

            GameObject vent = GameObject.Find("VentCollider");
            if (vent != null)
            {

                HiddenVolume hv = vent.AddComponent<HiddenVolume>();
                hv._BoxCollider = vent.GetComponent<BoxCollider>();
            }
            GameObject saferoomHB = GameObject.Find("SaferoomHB");

            HiddenVolume shv = saferoomHB.AddComponent<HiddenVolume>();
            shv._BoxCollider = saferoomHB.GetComponent<BoxCollider>();

            PickUp dronePU = GameObject.Find("PickUp_HOST_Drone").GetComponent<PickUp>();
            GameObject droneObj = GameObject.Find("Drone");
            dronePU.GetComponent<Rigidbody>().collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            LevelUtil.SetHiddenVolumeRenderers(dronePU, droneObj);

            GameObject lever = _bank.Lever;
            GameObject leverRoot = new GameObject("leverRoot");
            leverRoot.transform.position = lever.transform.position;
            lever.transform.parent = leverRoot.transform;
            leverRoot.transform.rotation = Quaternion.Euler(new Vector3(90, 270, 0));
            leverRoot.transform.position = GameObject.Find("ShutterLeverPos").transform.position;
            lever.transform.GetChild(0).gameObject.GetComponent<CapsuleCollider>().radius = 0.2f;
            lever.SetActive(true);

            GameObject.Find("SaferoomScreen").GetComponent<MeshRenderer>().enabled = false;
            GameObject vrrig = GameObject.Find("VRRig");

            Keycard.botKeycardClone = GameObject.Find("BotKeycardHand");
            Keycard.botKeycardClone.SetActive(false);

            PoisonGasFixer.ApplyAll();
            PoisonGasController.InitializeAllOff();

            MakeWindowMat();

            GameObject suitcase = _bank.Suitcase;
            suitcase.SetActive(true);
            GameObject hollowedSuitcaseBottom = GameObject.Find("HollowedSuitcaseBottom");
            GameObject donorSuitcaseBottom = null;
            GameObject hollowedSuitcaseTop = GameObject.Find("HollowedSuitcaseTop");
            GameObject donorSuitcaseTop = null;

            Transform suitcaseVisualRoot = suitcase.transform.GetChild(1);
            List<Transform> suitcaseVisualChildren = LevelUtil.GetAllChildrenRecursive(suitcaseVisualRoot);
            for(int i = 0; i < suitcaseVisualChildren.Count; i++)
            {
                Transform child = suitcaseVisualChildren[i];
                if (child.gameObject.name == "SM_Elevator_INT_NuclearFootBall_BottomShell") donorSuitcaseBottom = child.gameObject;
                else if(child.gameObject.name == "SM_Elevator_INT_NulcearFootBall_TopShell") donorSuitcaseTop = child.gameObject;
                MeshRenderer mr = child.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = true;
            }

            MeshRenderer hollowedMRB = hollowedSuitcaseBottom.transform.GetChild(1).GetChild(0).GetComponent<MeshRenderer>();
            MeshRenderer donorMRB = donorSuitcaseBottom.GetComponent<MeshRenderer>();

            suitcase.transform.position = new Vector3(0, 1, 1);
            hollowedSuitcaseBottom.transform.parent = donorSuitcaseBottom.transform;
            hollowedSuitcaseBottom.transform.localPosition = Vector3.zero;
            hollowedSuitcaseBottom.transform.localRotation = Quaternion.identity;
            hollowedMRB.material = donorMRB.material;
            donorMRB.enabled = false;

            MeshRenderer hollowedMRT = hollowedSuitcaseTop.transform.GetChild(0).GetComponent<MeshRenderer>();
            MeshRenderer donorMRT = donorSuitcaseTop.GetComponent<MeshRenderer>();
            hollowedSuitcaseTop.transform.parent = donorSuitcaseTop.transform;
            hollowedSuitcaseTop.transform.localPosition = Vector3.zero;
            hollowedSuitcaseTop.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            hollowedMRT.material = donorMRT.material;
            hollowedMRT.material.mainTexture = HeistBundle2Manager.GetTexture("SuitcaseTop.png");
            donorMRT.enabled = false;

            suitcase.AddComponent<SuitcaseStateManager>();

            suitcase.GetComponent<PickUp>().enabled = true;
            suitcase.transform.position = new Vector3(10.124f, 0.3f, -5.583f);
            suitcase.transform.rotation = Quaternion.Euler(new Vector3(0, 0, 90));

            GameObject drone = GameObject.Find("PickUp_HOST_Drone");

            GameObject dronePackedAnchor = new GameObject("DronePackedAnchor");
            dronePackedAnchor.transform.SetParent(hollowedSuitcaseBottom.transform, false);
            dronePackedAnchor.transform.localPosition = new Vector3(0.11f, 0.06f, 0f);
            dronePackedAnchor.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            drone.transform.SetParent(null, true);
            drone.transform.position = dronePackedAnchor.transform.position;
            drone.transform.rotation = dronePackedAnchor.transform.rotation;

            Rigidbody rb = drone.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            PickUp pu = drone.GetComponent<PickUp>();
            if (pu != null) pu.enabled = true;

            Collider[] cols = drone.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null)
                    cols[i].enabled = true;
            }

            DroneDriver dd = drone.GetComponent<DroneDriver>();
            if (dd != null) dd.enabled = false;

            LevelUtil.IgnoreCollisionRecursive(drone, suitcase);
            LevelUtil.IgnoreCollisionRecursive(drone, hollowedSuitcaseBottom);

            DronePackedState packed = drone.GetComponent<DronePackedState>();
            if (packed == null) packed = drone.AddComponent<DronePackedState>();
            packed.anchor = dronePackedAnchor.transform;
            packed.suitcaseRoot = suitcase;
            packed.suitcaseBottom = hollowedSuitcaseBottom;

            GameObject headsetHost = GameObject.Find("PickUp_HOST_Headset");
            GameObject headsetObj = GameObject.Find("Headset");

            GameObject headsetPackedAnchor = new GameObject("HeadsetPackedAnchor");
            headsetPackedAnchor.transform.SetParent(hollowedSuitcaseBottom.transform, false);
            headsetPackedAnchor.transform.localPosition = new Vector3(-0.23f, 0.06f, 0);
            headsetPackedAnchor.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            headsetHost.transform.SetParent(null, true);
            headsetHost.transform.position = headsetPackedAnchor.transform.position;
            headsetHost.transform.rotation = headsetPackedAnchor.transform.rotation;

            Rigidbody headsetRb = headsetHost.GetComponent<Rigidbody>();
            if (headsetRb != null)
            {
                headsetRb.velocity = Vector3.zero;
                headsetRb.angularVelocity = Vector3.zero;
                headsetRb.isKinematic = true;
                headsetRb.useGravity = false;
            }

            PickUp headsetPu = headsetHost.GetComponent<PickUp>();
            if (headsetPu != null) headsetPu.enabled = true;

            Collider[] headsetCols = headsetHost.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < headsetCols.Length; i++)
            {
                if (headsetCols[i] != null)
                    headsetCols[i].enabled = true;
            }

            LevelUtil.IgnoreCollisionRecursive(headsetHost, suitcase);
            LevelUtil.IgnoreCollisionRecursive(headsetHost, hollowedSuitcaseBottom);

            HeadsetPackedState headsetPacked = headsetHost.GetComponent<HeadsetPackedState>();
            if (headsetPacked == null) headsetPacked = headsetHost.AddComponent<HeadsetPackedState>();
            headsetPacked.anchor = headsetPackedAnchor.transform;

            GameObject.Find("Bucket_Yellow_LoD_1").layer = 18;

            TrashTrigger trolleyTrigger = GameObject.Find("Trolley").transform.GetChild(0).gameObject.AddComponent<TrashTrigger>();
            trolleyTrigger.Init(3);

            BankGlassFix.Apply();

        }

        void MakeWindowMat()
        {
            GameObject windowObj = GameObject.Find("Saferoom_Window");
            MeshRenderer windowR = windowObj.GetComponent<MeshRenderer>();

            Shader glassShader = Shader.Find("Phoenix/SH_Shared_GUIUnlitAlpha_01");
            Material glassMat = new Material(glassShader);
            glassMat.name = "WindowGlass_RuntimeMat";

            Texture2D whiteTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            whiteTex.SetPixel(0, 0, Color.white);
            whiteTex.SetPixel(1, 0, Color.white);
            whiteTex.SetPixel(0, 1, Color.white);
            whiteTex.SetPixel(1, 1, Color.white);
            whiteTex.Apply(false, false);

            glassMat.SetTexture("_MainTex", whiteTex);

            Color glassTint = new Color(0.92f, 0.96f, 0.98f, 0.07f);
            glassMat.color = glassTint;
            glassMat.SetColor("_Color", glassTint);

            glassMat.SetColor("_TintColor", new Color(0.96f, 0.98f, 1.00f, 0.07f));

            glassMat.renderQueue = 3000;

            windowR.sharedMaterial = glassMat;
            windowR.material = glassMat;
            windowR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            windowR.receiveShadows = false;

            MelonLoader.MelonLogger.Msg("[miscstuff] Applied cleaned window glass to Saferoom_Window.");
        }

        public void MakeDroneGrabbable(GameObject host)
        {
            DronePickUp dpu = host.GetComponent<DronePickUp>();
            if (dpu == null)
                dpu = host.AddComponent<DronePickUp>();

            dpu.enabled = false;

            GameObject original = host.transform.GetChild(0).gameObject;
            Collider phys = original.GetComponent<Collider>();

            GameObject hb = new GameObject("DroneGrabHitbox");
            hb.transform.SetParent(phys.transform, false);
            hb.layer = phys.gameObject.layer;

            float scale = 1.5f;

            BoxCollider box = phys.TryCast<BoxCollider>();
            if (box != null)
            {
                BoxCollider c = hb.AddComponent<BoxCollider>();
                c.center = box.center;
                c.size = box.size * scale;
                c.isTrigger = true;
            }
            else
            {
                SphereCollider sphere = phys.TryCast<SphereCollider>();
                if (sphere != null)
                {
                    SphereCollider c = hb.AddComponent<SphereCollider>();
                    c.center = sphere.center;
                    c.radius = sphere.radius * scale;
                    c.isTrigger = true;
                }
                else
                {
                    CapsuleCollider capsule = phys.TryCast<CapsuleCollider>();
                    CapsuleCollider c = hb.AddComponent<CapsuleCollider>();
                    c.center = capsule.center;
                    c.radius = capsule.radius * scale;
                    c.height = capsule.height * scale;
                    c.direction = capsule.direction;
                    c.isTrigger = true;
                }
            }

            DronePickUpHitbox dpuh = hb.AddComponent<DronePickUpHitbox>();
            dpuh.dpu = dpu;
        }

        private void SetupObjectBank(GameObject root)
        {
            var bankGO = new GameObject("ObjectBank");
            bankGO.transform.SetParent(root.transform, false);

            _bank = bankGO.AddComponent<ObjectBank>();
            _bank.RefreshAll();
        }

        private int _vanLoadToken = 0;
        private int _lastHandledVanLoadToken = -1;
        private object _vanInitRoutine;
        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName != "Van")
                return;

            _vanLoadToken++;

            if (_vanInitRoutine != null)
            {
                MelonCoroutines.Stop(_vanInitRoutine);
                _vanInitRoutine = null;
            }

            _vanInitRoutine = MelonCoroutines.Start(Co_HandleFreshVanLoad(_vanLoadToken));
        }

        [UnhollowerBaseLib.Attributes.HideFromIl2Cpp]
        private IEnumerator Co_HandleFreshVanLoad(int token)
        {
            float timeout = 10f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                bool ready =
                    GameObject.Find("P_Van_INT_CassetteTutorial") != null &&
                    GameObject.Find("SM_Van_INT_ObjectiveMonitorScreen_02") != null;

                if (ready)
                    break;

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (token != _vanLoadToken)
                yield break;

            if (_lastHandledVanLoadToken == token)
                yield break;

            _lastHandledVanLoadToken = token;

            VanSceneManager.OnFreshVanLoaded();

            _vanInitRoutine = null;
        }

    }
}
