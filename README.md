# IEYTD2 Bank Heist Level Mod

A custom **bank heist level mod** for *I Expect You To Die 2*, built using **MelonLoader**, **Unity 2019.4**, and **IL2CPP-safe scripting**.

This project adds a full custom mission centered around infiltrating a Zoraxis-controlled bank, using a remote drone, bypassing security systems, breaking into the vault, and escaping with stolen intelligence.

The focus is on hands-on VR interaction, physical puzzle solving, custom gadgets, and building a full level experience directly inside the live game.

You can keep up with my other IEYTD mods progress here: https://www.youtube.com/@jamesconnors3820

---

## Features

- Adds a custom bank/vault mission to *I Expect You To Die 2*
- Introduces a remote-controlled drone with custom grabbing, pulling, rotating, vent-crawling, and rocket-launch behavior
- Adds a wearable headset view with custom screen/static effects
- Merges selected assets from existing game scenes into the custom level
- Includes vault, keypad, laser, turret, elevator, suitcase, grate, gas, and hidden-object puzzle systems
- Adds custom visual effects such as sparks, explosions, glass breaking, rocket plume effects, screen overlays, alarms, and camera shake
- Integrates with the vanilla van, cassette, souvenir, speedrun, and WinRoom/debrief flow
- Supports custom save data for completion state, best time, last time, and souvenirs
- Uses Schell's Phoenix interaction systems to make custom objects work correctly in VR
- Designed to be MelonLoader-friendly and IL2CPP-safe

---

## Core Architecture

- **MyMod.cs** is the main MelonLoader entry point and registers the custom IL2CPP components
- **LevelLoader.cs** handles asset bundle loading, scene merging, and level startup
- **HeistLevelManager.cs** acts as the main runtime manager for the custom level
- **ObjectBank.cs** stores references to important scene objects
- **LevelUtil.cs** contains shared helper methods for scene setup, grabbables, textures, materials, and player utilities
- **SaveManager.cs** and **ModSaveData.cs** store custom level progress
- Systems are split into focused scripts instead of one giant controller
- Visual effects are handled by lightweight custom drivers instead of relying only on Unity ParticleSystem

---

## Script Overview

### Core / Bootstrap

- **MyMod.cs**  
  Main mod entry point. Registers custom scripts, initializes the level loader, gathers donor assets, and starts setup once the custom scene is ready.

- **LevelLoader.cs**  
  Loads the custom asset bundle, merges the mod scene into the game, manages cleanup, and handles scene load flow.

- **HeistLevelManager.cs**  
  Main level manager for the bank heist mission. Coordinates major puzzle and mission state behavior.

- **ObjectBank.cs**  
  Central reference hub for important scene objects, gathered assets, the player rig, drone, manager objects, and pickups.

- **LevelUtil.cs**  
  Shared utility class for making objects grabbable, staging objects, changing layers, swapping textures, handling Phoenix materials, and other level setup tasks.

- **GatherGameAssets.cs**  
  Loads donor scenes, clones required base-game objects, keeps them alive through scene loads, and plants them into the custom level.

- **HeistBundle2Manager.cs**  
  Loads and serves custom textures, prefabs, audio, and other bundled assets.

- **PhoenixMaterialUtil.cs**  
  Helper methods for converting materials to Phoenix-compatible shaders and fixing objects that would otherwise render incorrectly.

- **PhoenixButtonHook.cs**  
  Hooks Schell/Phoenix button interactions into custom mod logic.

---

### Drone System

- **DroneDriver.cs**  
  Controls the drone's hover behavior, stabilization, glass eye material, and general physical behavior.

- **DroneHand.cs**  
  Main drone hand behavior used in the level. Handles launching, grabbing, held-object behavior, and input-driven interactions.

- **DroneHandVan.cs**  
  Van/lobby version of the drone hand behavior used for souvenir interaction.

- **DronePickUp.cs**  
  Custom pickup bridge for objects grabbed by the drone hand.

- **DronePackedState.cs**  
  Handles the drone's packed/stored state before it becomes usable.

- **DroneVentRailTK.cs**  
  Controls the drone while it is inside vent rails, including handoff between vanilla telekinesis and custom vent movement.

- **DroneVentScript.cs**  
  Handles vent entry/exit triggers and vent-related drone behavior.

- **DronePullMotion.cs**  
  Adds drone-controlled pull interactions for objects like wires or pullable puzzle parts.

- **DroneRotationalMotion.cs**  
  Adds drone-controlled rotation interactions for doors, handles, wheels, and similar objects.

- **InfiniteDroneRotationalMotion.cs**  
  Variant of drone rotational interaction for objects that can keep rotating past a fixed range.

- **IDroneGrabbable.cs**  
  Interface used by objects that support custom drone grab behavior.

---

### Headset / Hidden Vision System

- **HeadsetScript.cs**  
  Main headset behavior and logic for entering headset/drone-view interactions.

- **HeadsetDriver.cs**  
  Builds the headset screen overlay, including static, scanlines, grain, and view effects.

- **HeadsetPackedState.cs**  
  Handles the headset's packed/stored state and when it can be grabbed or used.

- **Hotspot.cs**  
  Defines headset/drone hotspots that can move or align the view to important areas.

- **HiddenVolumeController.cs**  
  Manages hidden-volume behavior and visibility logic.

- **CustomHiddenVolume.cs**  
  Custom through-wall/highlight interaction behavior for objects that need to be discoverable while obstructed.

- **HiddenTrophy.cs**  
  Handles hidden trophy/souvenir logic.

---

### Vault, Bank, and Puzzle Systems

- **VaultPuzzleManager.cs**  
  Coordinates the main vault puzzle flow and related puzzle state.

- **Keypad.cs**  
  Handles keypad input, display text, success/denial logic, screen coloring, and audio feedback.

- **KeypadButton.cs**  
  Handles individual keypad button presses and button movement animation.

- **Keycard.cs**  
  Keycard-related interaction behavior.

- **Dial.cs**  
  Dial interaction behavior used by puzzle objects.

- **LeverScript.cs**  
  Handles lever interaction and shutter/vent state changes.

- **GrateScript.cs**  
  Handles grate behavior and related interaction state.

- **MainGear.cs**  
  Main gear puzzle behavior.

- **SubGear.cs**  
  Secondary gear puzzle behavior.

- **WrenchStartPose.cs**  
  Sets up the wrench's starting pose.

- **PickUpPile.cs**  
  Handles grouped pickup/object pile behavior.

- **TrashTrigger.cs**  
  Trigger behavior for trash/disposal-related interactions.

---

### Lasers, Turrets, and Security

- **LaserColumnSpawner.cs**  
  Spawns laser emitters along configured columns and avoids unsafe spawn paths.

- **LaserEmitter.cs**  
  Handles laser raycasts, hit detection, beam rendering, pulsing, scrolling, and beam state.

- **LaserPointer.cs**  
  Van/lobby laser pointer behavior with trigger-controlled beam visuals.

- **WireVisionManager.cs**  
  Renders hidden wiring paths for laser/security systems.

- **Turret.cs**  
  Turret object behavior.

- **TurretDriver.cs**  
  Controls turret aiming, firing, tracking, and security behavior.

- **GuardReactionDriver.cs**  
  Handles guard reaction behavior.

- **AlarmDriver.cs**  
  Controls alarm visuals/audio and alert state.

---

### Elevator, Suitcase, and Mission Objects

- **ElevatorScript.cs**  
  Moves the elevator platform and associated beam pivots while carrying the player rig with it.

- **SuitcaseStateManager.cs**  
  Tracks suitcase open/closed state and related mission object behavior.

- **SuitcaseColliderProbe.cs**  
  Probe/helper script for suitcase collider behavior.

- **ZorCase.cs**  
  Handles the final case/briefcase interaction that completes the mission.

- **VanStartButtonHook.cs**  
  Hooks the van start button into the custom level launch flow.

- **VanSceneManager.cs**  
  Updates van screens, cassette behavior, souvenir display, speedrun display, and custom van state.

- **WinRoomScript.cs**  
  Customizes the WinRoom/debrief sequence, including time display, speedrun display, replay/return behavior, and custom debrief audio.

---

### Visual Effects

- **ExplosionDriver.cs**  
  Custom explosion effect driver.

- **SparkDriver.cs**  
  Electrical spark effect driver.

- **RocketDriver.cs**  
  Rocket plume and trail effect for the drone hand launch.

- **GlassDriver.cs**  
  Handles breakable glass behavior.

- **BankGlassFix.cs**  
  Fixes bank glass materials so they render properly with Phoenix-compatible shaders.

- **CameraShakeDriver.cs**  
  Adds screen/camera shake for impacts or major events.

- **DamageOverlayDriver.cs**  
  Displays a damage overlay when the player is hurt.

- **PoisonGasController.cs**  
  Controls poison gas visuals and behavior.

- **PoisonGasFixer.cs**  
  Fixes or adjusts poison gas setup at runtime.

- **ScreenDriver.cs**  
  Creates animated screen visuals, password display, scanlines, noise, flicker, and baked text.

---

### Audio, Input, and Utilities

- **AudioUtil.cs**  
  Utility for playing custom audio clips consistently in world space or during mission events.

- **CollisionSound.cs**  
  Plays collision sounds when objects hit surfaces.

- **PauseScript.cs**  
  Handles pause-related behavior.

- **ThumbstickTest.cs**  
  Input test/debug script for thumbstick behavior.

- **TKGrabHarness.cs**  
  Helper script for testing or managing telekinesis-style grab behavior.

- **InfinitePlayerWheelTK.cs**  
  Player-controlled infinite wheel/rotation behavior through telekinesis-style input.

---

### Avatar / Misc Runtime Drivers

- **AgentAvatarDriver.cs**  
  Controls custom avatar/agent presentation behavior.

- **BearHeadListener.cs**  
  Detects headset-on-bear souvenir behavior.

- **CameraShakeDriver.cs**  
  Handles camera shake events.

- **CollisionSound.cs**  
  Adds physical collision sound feedback.

---

## Notes

This mod was built as a custom level experiment for *I Expect You To Die 2* using MelonLoader and Unity scene/asset-bundle workflows.

There was minimal AI used in the creation of this project's code. AI was mostly used to help with cleanup, repetitive driver code, custom visual effects, and IL2CPP/MelonLoader-specific problems that are tedious to solve manually.

The project is not affiliated with Schell Games. *I Expect You To Die* and *I Expect You To Die 2* are owned by Schell Games.
