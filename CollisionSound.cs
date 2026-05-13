using System;
using MelonLoader;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class CollisionSound : MonoBehaviour
    {
        public CollisionSound(IntPtr ptr) : base(ptr) { }
        public CollisionSound() : base(ClassInjector.DerivedConstructorPointer<CollisionSound>())
            => ClassInjector.DerivedConstructorBody(this);

        public AudioClip Sound;

        public bool UseSoftHardSounds = false;
        public AudioClip SoftSound;
        public AudioClip HardSound;

        public float MinImpactVelocity = 0.75f;
        public float HardImpactVelocity = 2.25f;

        public float Volume = 1.0f;
        public float HardVolumeMultiplier = 1.0f;
        public float Cooldown = 0.12f;

        public bool UsePitchVariation = false;
        public float MinPitch = 0.95f;
        public float MaxPitch = 1.05f;

        public bool CreateAudioSourceIfMissing = true;
        public float SpatialBlend = 1.0f;
        public float MinDistance = 0.2f;
        public float MaxDistance = 12.0f;

        public bool DebugLog = false;

        AudioSource audioSource;
        Rigidbody rb;
        float nextSoundTime;
        bool ready;

        void Awake()
        {
            RefreshRefs();
        }

        void Start()
        {
            RefreshRefs();
        }

        public void Init(AudioClip sound, float minImpactVelocity = 0.75f, float volume = 1.0f, float cooldown = 0.12f)
        {
            UseSoftHardSounds = false;
            Sound = sound;
            SoftSound = null;
            HardSound = null;
            MinImpactVelocity = minImpactVelocity;
            Volume = volume;
            Cooldown = cooldown;
            RefreshRefs();
        }

        public void Init(AudioClip softSound, AudioClip hardSound, float minImpactVelocity, float hardImpactVelocity, float volume = 1.0f, float cooldown = 0.12f)
        {
            UseSoftHardSounds = true;
            Sound = null;
            SoftSound = softSound;
            HardSound = hardSound;
            MinImpactVelocity = minImpactVelocity;
            HardImpactVelocity = hardImpactVelocity;
            Volume = volume;
            Cooldown = cooldown;
            RefreshRefs();
        }

        public void InitFull(
            AudioClip sound,
            AudioClip softSound,
            AudioClip hardSound,
            bool useSoftHardSounds,
            float minImpactVelocity,
            float hardImpactVelocity,
            float volume,
            float hardVolumeMultiplier,
            float cooldown,
            bool usePitchVariation = false,
            float minPitch = 0.95f,
            float maxPitch = 1.05f,
            bool debugLog = false)
        {
            Sound = sound;
            SoftSound = softSound;
            HardSound = hardSound;
            UseSoftHardSounds = useSoftHardSounds;
            MinImpactVelocity = minImpactVelocity;
            HardImpactVelocity = hardImpactVelocity;
            Volume = volume;
            HardVolumeMultiplier = hardVolumeMultiplier;
            Cooldown = cooldown;
            UsePitchVariation = usePitchVariation;
            MinPitch = minPitch;
            MaxPitch = maxPitch;
            DebugLog = debugLog;
            RefreshRefs();
        }

        public void SetAudioSourceSettings(float spatialBlend = 1.0f, float minDistance = 0.2f, float maxDistance = 12.0f)
        {
            SpatialBlend = spatialBlend;
            MinDistance = minDistance;
            MaxDistance = maxDistance;

            RefreshRefs();
            ApplyAudioSourceSettings();
        }

        void RefreshRefs()
        {
            audioSource = GetComponent<AudioSource>();

            if (audioSource == null && CreateAudioSourceIfMissing)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }

            ApplyAudioSourceSettings();

            rb = GetComponent<Rigidbody>();
            if (rb == null && transform.parent != null)
                rb = transform.parent.GetComponent<Rigidbody>();

            ready = audioSource != null;
        }

        void ApplyAudioSourceSettings()
        {
            if (audioSource == null)
                return;

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = Mathf.Clamp01(SpatialBlend);
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = Mathf.Max(0.01f, MinDistance);
            audioSource.maxDistance = Mathf.Max(audioSource.minDistance + 0.01f, MaxDistance);
        }

        void OnCollisionEnter(Collision collision)
        {
            TryPlaySound(collision);
        }

        void OnCollisionStay(Collision collision)
        {
            TryPlaySound(collision);
        }

        void TryPlaySound(Collision collision)
        {
            if (!ready)
                RefreshRefs();

            if (Time.time < nextSoundTime || audioSource == null)
                return;

            float impactVelocity = GetImpactVelocity(collision);
            if (impactVelocity < MinImpactVelocity)
                return;

            bool hardHit = UseSoftHardSounds && impactVelocity >= HardImpactVelocity;
            AudioClip clip = PickClip(hardHit);

            if (clip == null)
                return;

            float oldPitch = audioSource.pitch;
            audioSource.pitch = UsePitchVariation ? UnityEngine.Random.Range(MinPitch, MaxPitch) : 1f;

            float finalVolume = Mathf.Max(0f, Volume);
            if (hardHit)
                finalVolume *= Mathf.Max(0f, HardVolumeMultiplier);

            audioSource.PlayOneShot(clip, finalVolume);
            audioSource.pitch = oldPitch;

            nextSoundTime = Time.time + Mathf.Max(0.01f, Cooldown);

            if (DebugLog)
                LogHit(collision, impactVelocity, clip, hardHit);
        }

        AudioClip PickClip(bool hardHit)
        {
            if (!UseSoftHardSounds)
                return Sound;

            if (hardHit && HardSound != null) return HardSound;
            if (!hardHit && SoftSound != null) return SoftSound;
            if (hardHit && SoftSound != null) return SoftSound;
            if (!hardHit && HardSound != null) return HardSound;

            return Sound;
        }

        float GetImpactVelocity(Collision collision)
        {
            float best = collision != null ? collision.relativeVelocity.magnitude : 0f;
            Rigidbody otherRb = null;

            if (collision != null)
            {
                if (collision.rigidbody != null)
                    otherRb = collision.rigidbody;
                else if (collision.collider != null)
                    otherRb = collision.collider.attachedRigidbody;
            }

            if (rb != null || otherRb != null)
            {
                Vector3 selfVel = rb != null ? rb.velocity : Vector3.zero;
                Vector3 otherVel = otherRb != null ? otherRb.velocity : Vector3.zero;
                best = Mathf.Max(best, (selfVel - otherVel).magnitude);
            }

            return best;
        }

        void LogHit(Collision collision, float impactVelocity, AudioClip clip, bool hardHit)
        {
            string otherName = collision != null && collision.gameObject != null ? collision.gameObject.name : "null";

            MelonLogger.Msg("[CollisionSound] " + gameObject.name + " hit by " + otherName +
                            " velocity=" + impactVelocity.ToString("F2") +
                            " clip=" + clip.name +
                            " hard=" + hardHit);
        }
    }
}
