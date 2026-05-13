using Il2CppSystem.Text;
using MelonLoader;
using SG.Phoenix.Assets.Code.Interactables;
using SG.Phoenix.Assets.Code.Utility;
using System;
using System.Collections.Generic;
using UnhollowerBaseLib.Attributes;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class VaultPuzzleManager : MonoBehaviour
    {
        public VaultPuzzleManager(IntPtr ptr) : base(ptr) { }
        public VaultPuzzleManager() : base(ClassInjector.DerivedConstructorPointer<VaultPuzzleManager>())
            => ClassInjector.DerivedConstructorBody(this);

        public Dial dial1;
        public Dial dial2;
        public Dial dial3;
        public MainGear mainGear;
        public InfinitePlayerWheelTK vaultWheel;

        InfiniteDroneRotationalMotion gearMotion;
        HeistLevelManager heistLevelManager;
        LoopingSfx gearLoop;
        LoopingSfx wheelLoop;
        RotationalMotion vaultDoorMotion;

        public void Start()
        {
            dial1 = GameObject.Find("Dial1").AddComponent<Dial>();
            dial2 = GameObject.Find("Dial2").AddComponent<Dial>();
            dial3 = GameObject.Find("Dial3").AddComponent<Dial>();
            dial1.manager = this; dial2.manager = this; dial3.manager = this;

            GameObject _mainGear = GameObject.Find("MainGear");
            gearMotion = _mainGear.AddComponent<InfiniteDroneRotationalMotion>();
            gearMotion.allowRocketHands = false;
            gearMotion.localAxis = Vector3.right;
            mainGear = GameObject.Find("MainGear").AddComponent<MainGear>();
            mainGear.source = gearMotion;

            SubGear cylinder1 = dial1.transform.GetChild(0).gameObject.AddComponent<SubGear>();
            SubGear cylinder2 = dial2.transform.GetChild(0).gameObject.AddComponent<SubGear>();
            SubGear cylinder3 = dial3.transform.GetChild(0).gameObject.AddComponent<SubGear>();
            cylinder1.rotDirection = Vector3.right; cylinder2.rotDirection = Vector3.left; cylinder3.rotDirection = Vector3.right;
            cylinder1.mainGear = mainGear; cylinder2.mainGear = mainGear; cylinder3.mainGear = mainGear;
            cylinder1.speed = 1; cylinder2.speed = 0.6f; cylinder3.speed = 3.2f;
            cylinder1.redLightAngle = 90f; cylinder2.redLightAngle = 0; cylinder3.redLightAngle = 90f;

            vaultWheel = GameObject.Find("Rot_HOST_RealVaultWheel").GetComponent<InfinitePlayerWheelTK>();
            cylinder1.vaultWheel = vaultWheel; cylinder2.vaultWheel = vaultWheel; cylinder3.vaultWheel = vaultWheel;
            cylinder1.vaultPuzzleManager = this; cylinder2.vaultPuzzleManager = this; cylinder3.vaultPuzzleManager = this;

            foreach (GameObject go in UnityEngine.Object.FindObjectsOfType<GameObject>())
            {
                if (go != null && go.name.Contains("SubGear"))
                {
                    SubGear subGear = go.AddComponent<SubGear>();
                    subGear.rotDirection = Vector3.back;
                    subGear.mainGear = mainGear;
                    subGear.vaultWheel = vaultWheel;
                    subGear.vaultPuzzleManager = this;
                    if (go.transform.parent.name == "LeftGears")
                    {
                        subGear.speed *= -1;
                    }
                }
            }

            heistLevelManager = GameObject.Find("Manager").GetComponent<HeistLevelManager>();
            gearLoop = mainGear.gameObject.AddComponent<LoopingSfx>();
            gearLoop.InitAndPlay("GearsLoop.mp3", 0.3f);
            gearLoop.TurnOff();

            wheelLoop = vaultWheel.gameObject.AddComponent<LoopingSfx>();
            wheelLoop.InitAndPlay("GearTurningLoop.ogg", 0.6f);
            wheelLoop.TurnOff();
            vaultDoorMotion = GameObject.Find("Rot_HOST_VaultPivot").GetComponent<RotationalMotion>();
        }

        bool wasHeld = false;
        bool wasWheelActive = false;
        bool vaultOpen = false;

        public void Update()
        {
            bool releasedThisFrame = false;

            if (wasHeld && !gearMotion.isHeld)
            {
                releasedThisFrame = true;
            }
            wasHeld = gearMotion.isHeld;

            bool wheelActive = vaultWheel != null && vaultWheel.IsActive;
            if (wasWheelActive && !wheelActive)
            {
                releasedThisFrame = true;
            }
            wasWheelActive = wheelActive;

            if (releasedThisFrame)
            {
                MainGearReleased();
            }

            if (!vaultOpen && vaultDoorMotion.isHeld)
            {
                vaultOpen = true;
                AudioUtil.PlayAt("VaultOpen.mp3", vaultDoorMotion.gameObject.transform.position, 1);
                WireVisionManager wvm = GameObject.Find("Manager").GetComponent<WireVisionManager>();
                if(!wvm.killed)
                {
                    if(!SaveManager.HasSouvenir(3)) HeistLevelManager.FoundSouvenir(3);
                }
            }
        }

        void MainGearReleased()
        {
            dial1.Released();
            dial2.Released();
            dial3.Released();
        }

        public void GreenLightTriggered()
        {
            if (dial1.primed && dial2.primed && dial3.primed)
            {
                heistLevelManager.UnlockVault();
            }
        }

        public void RedLightTriggered()
        {
            MelonCoroutines.Start(Co_RedLightTriggered());
        }

        [HideFromIl2Cpp]
        System.Collections.IEnumerator Co_RedLightTriggered()
        {
            yield return new WaitForSeconds(1);
            GameObject wheel = wheelLoop.gameObject;
            AudioUtil.PlayAt("FuseLoop.ogg", wheel.transform.position);
            yield return new WaitForSeconds(4f);
            wheelLoop.gameObject.AddComponent<ExplosionDriver>().TriggerExplosion();
            dial2.gameObject.AddComponent<ExplosionDriver>().TriggerExplosion();
            AudioUtil.PlayAt("Explosion.ogg", wheel.transform.position, 1f);
            yield return null;
            HeadsetScript headset = GameObject.Find("Headset").GetComponent<HeadsetScript>();
            if (headset.IsWearing)
            {
                heistLevelManager.ExplosionDroneDeath();
                yield return new WaitForSeconds(1.5f);
            }
            else
            {
                DamageOverlayDriver.TriggerExplosionDeath();
                GameObject.Find("VRRig").GetComponent<TransformShaker>().ShakeDefault();
                yield return new WaitForSeconds(1.5f);
            }
            LevelUtil.TriggerDeath("Booby Trap");
        }

        public bool _wheelPlaying = false;
        public bool _gearPlaying = false;

        public void TurnOnGearSound()
        {
            if (_gearPlaying) return;
            gearLoop.TurnOn();
            _gearPlaying = true;
        }

        public void TurnOffGearSound()
        {
            if (!_gearPlaying) return;
            gearLoop.TurnOff();
            _gearPlaying = false;
        }

        public void TurnOnWheelSound()
        {
            if (_wheelPlaying) return;
            wheelLoop.TurnOn();
            _wheelPlaying = true;
        }

        public void TurnOffWheelSound()
        {
            if (!_wheelPlaying) return;
            wheelLoop.TurnOff();
            _wheelPlaying = false;
        }
    }
}
