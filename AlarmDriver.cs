using System;
using System.Collections;
using System.Collections.Generic;
using MelonLoader;
using UnhollowerBaseLib.Attributes;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class AlarmDriver : MonoBehaviour
    {
        public AlarmDriver(IntPtr ptr) : base(ptr) { }
        public AlarmDriver() : base(ClassInjector.DerivedConstructorPointer<AlarmDriver>())
            => ClassInjector.DerivedConstructorBody(this);

        public Transform audioAnchor;
        public string loopClipName = "alarm_triggered.ogg";
        public float alarmVolume = 0.6f;
        public bool playOnAwake = false;

        public bool affectExistingLights = true;
        public Color alarmColor = new Color(1f, 0.08f, 0.08f, 1f);
        public float pulseHz = 2f;
        public float pulseMin = 0.2f;
        public float pulseMax = 1f;
        public float existingIntensityMultiplier = 0.2f;
        public string[] ignoreNameContains = { "sun", "directional", "debug", "red light" };

        public bool spawnBeacons = true;
        public Transform beaconParent;
        public Vector3 roomCenterWorld = new Vector3(0f, 5f, 0f);
        public float roomZMax = 4f;
        public float roomZMin = -9f;
        public bool useTwoXLanes = true;
        public int beaconsPerLane = 7;
        public float beaconY = 5f;
        public float laneXInset = 1.8f;
        public LightType beaconType = LightType.Point;
        public float beaconRange = 12f;
        public float beaconBaseIntensity = 7f;
        public float chaseSpeed = 1.2f;
        public float chaseHotMultiplier = 2.5f;
        public float chaseWarmMultiplier = 1.2f;
        public float chaseFalloff = 1f;

        public LoopingSfx alarmAudio;

        bool isRunning;
        object alarmLoop;
        GameObject audioObject;

        readonly List<Light> roomLights = new List<Light>(256);
        readonly List<Light> beacons = new List<Light>(64);
        readonly Dictionary<int, bool> lightEnabled = new Dictionary<int, bool>(256);
        readonly Dictionary<int, float> lightIntensity = new Dictionary<int, float>(256);
        readonly Dictionary<int, Color> lightColor = new Dictionary<int, Color>(256);

        void Start()
        {
            CreateAudioObject();

            if (playOnAwake)
                StartAlarm();
        }

        public void SetVolume(float volume)
        {
            alarmVolume = volume;
            alarmAudio.SetVolume(alarmVolume);
        }

        public void Critical()
        {
            beaconBaseIntensity = 20f;
        }

        public void StartAlarm()
        {
            if (isRunning) return;

            isRunning = true;
            CreateAudioObject();

            if (affectExistingLights)
            {
                CacheRoomLights();
                ApplyAlarmColorToRoomLights();
            }

            if (spawnBeacons)
                BuildBeacons();

            alarmLoop = MelonCoroutines.Start(AlarmLoop());
            alarmAudio.TurnOn();
        }

        public void StopAlarm()
        {
            if (!isRunning) return;

            isRunning = false;

            if (alarmLoop != null)
            {
                MelonCoroutines.Stop(alarmLoop);
                alarmLoop = null;
            }

            if (affectExistingLights)
                RestoreRoomLights();

            DestroyBeacons();
            alarmAudio.TurnOff();
        }

        public void ToggleAlarm()
        {
            if (isRunning) StopAlarm();
            else StartAlarm();
        }

        void OnDisable()
        {
            StopAlarm();
        }

        void OnDestroy()
        {
            StopAlarm();

            if (audioObject)
                Destroy(audioObject);
        }

        void CreateAudioObject()
        {
            if (audioObject)
            {
                if (audioAnchor)
                    audioObject.transform.position = audioAnchor.position;
                return;
            }

            Transform hmd = GameObject.Find("HMD").transform;
            audioObject = new GameObject("Alarm Audio");
            audioObject.transform.SetParent(hmd, false);
            audioObject.transform.position = hmd.position;

            alarmAudio = audioObject.AddComponent<LoopingSfx>();
            alarmAudio.InitAndPlay(loopClipName, 0);
            alarmAudio.TurnOff();
            alarmAudio.SetVolume(alarmVolume);
        }

        bool ShouldIgnore(Light light)
        {
            if (light.type == LightType.Directional)
                return true;

            string lowerName = light.gameObject.name.ToLowerInvariant();
            for (int i = 0; i < ignoreNameContains.Length; i++)
            {
                string ignored = ignoreNameContains[i];
                if (!string.IsNullOrEmpty(ignored) && lowerName.Contains(ignored.ToLowerInvariant()))
                    return true;
            }

            return false;
        }

        void CacheRoomLights()
        {
            roomLights.Clear();
            lightEnabled.Clear();
            lightIntensity.Clear();
            lightColor.Clear();

            Light[] lights = Resources.FindObjectsOfTypeAll<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (!light.gameObject.scene.IsValid()) continue;
                if (!light.gameObject.activeInHierarchy) continue;
                if (ShouldIgnore(light)) continue;

                int id = light.GetInstanceID();
                roomLights.Add(light);
                lightEnabled[id] = light.enabled;
                lightIntensity[id] = light.intensity;
                lightColor[id] = light.color;
            }
        }

        void ApplyAlarmColorToRoomLights()
        {
            for (int i = 0; i < roomLights.Count; i++)
            {
                Light light = roomLights[i];
                light.enabled = true;
                light.intensity = lightIntensity[light.GetInstanceID()] * existingIntensityMultiplier;
                light.color = alarmColor;
            }
        }

        void RestoreRoomLights()
        {
            for (int i = 0; i < roomLights.Count; i++)
            {
                Light light = roomLights[i];
                if (!light) continue;

                int id = light.GetInstanceID();
                light.enabled = lightEnabled[id];
                light.intensity = lightIntensity[id];
                light.color = lightColor[id];
            }

            roomLights.Clear();
            lightEnabled.Clear();
            lightIntensity.Clear();
            lightColor.Clear();
        }

        void BuildBeacons()
        {
            DestroyBeacons();

            int count = Mathf.Max(1, beaconsPerLane);
            float leftX = roomCenterWorld.x - Mathf.Abs(laneXInset);
            float rightX = roomCenterWorld.x + Mathf.Abs(laneXInset);

            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                float z = Mathf.Lerp(roomZMax, roomZMin, t);

                if (useTwoXLanes)
                {
                    beacons.Add(CreateBeacon("ALARM_Beacon_L" + i, new Vector3(leftX, beaconY, z)));
                    beacons.Add(CreateBeacon("ALARM_Beacon_R" + i, new Vector3(rightX, beaconY, z)));
                }
                else
                {
                    beacons.Add(CreateBeacon("ALARM_Beacon_" + i, new Vector3(roomCenterWorld.x, beaconY, z)));
                }
            }
        }

        Light CreateBeacon(string beaconName, Vector3 position)
        {
            GameObject beacon = new GameObject(beaconName);
            beacon.transform.position = position;

            if (beaconParent)
                beacon.transform.SetParent(beaconParent, true);

            Light light = beacon.AddComponent<Light>();
            light.type = beaconType;
            light.color = alarmColor;
            light.range = beaconRange;
            light.intensity = beaconBaseIntensity;

            if (light.type == LightType.Spot)
                light.spotAngle = 110f;

            return light;
        }

        void DestroyBeacons()
        {
            for (int i = 0; i < beacons.Count; i++)
            {
                if (beacons[i])
                    Destroy(beacons[i].gameObject);
            }

            beacons.Clear();
        }

        [HideFromIl2Cpp]
        IEnumerator AlarmLoop()
        {
            float time = 0f;

            while (isRunning)
            {
                if (audioAnchor && audioObject)
                    audioObject.transform.position = audioAnchor.position;

                time += Time.deltaTime;

                float wave = 0.5f + 0.5f * Mathf.Sin(time * 6.28318548f * pulseHz);
                float pulse = Mathf.Lerp(pulseMin, pulseMax, wave);

                for (int i = 0; i < roomLights.Count; i++)
                {
                    Light light = roomLights[i];
                    if (!light) continue;

                    light.intensity = lightIntensity[light.GetInstanceID()] * existingIntensityMultiplier * pulse;
                    light.color = alarmColor;
                }

                for (int i = 0; i < beacons.Count; i++)
                    UpdateBeacon(beacons[i], i, time, pulse);

                yield return null;
            }
        }

        void UpdateBeacon(Light beacon, int index, float time, float pulse)
        {
            if (!beacon) return;

            int count = beacons.Count;
            int hotIndex = (int)((time * chaseSpeed) % count);
            int distance = Mathf.Abs(index - hotIndex);
            distance = Mathf.Min(distance, count - distance);

            float chaseMultiplier;
            if (distance == 0)
                chaseMultiplier = chaseHotMultiplier;
            else if (distance == 1)
                chaseMultiplier = chaseWarmMultiplier;
            else
                chaseMultiplier = 1f + 0.35f * Mathf.Exp(-chaseFalloff * (distance - 1));

            beacon.range = beaconRange;
            beacon.color = alarmColor;
            beacon.intensity = beaconBaseIntensity * pulse * chaseMultiplier;

            if (beacon.type == LightType.Spot)
                beacon.spotAngle = 110f;
        }
    }
}
