using System;
using System.Collections.Generic;
using MelonLoader;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public static class AudioUtil
    {
        static readonly Dictionary<string, AudioClip> ClipCache = new Dictionary<string, AudioClip>();

        public static AudioClip ResolveClip(string clipName, bool verbose = true)
        {
            if (string.IsNullOrEmpty(clipName))
                return null;

            string key = clipName.ToLowerInvariant();

            if (ClipCache.TryGetValue(key, out AudioClip clip) && clip != null)
                return clip;

            ClipCache.Remove(key);

            clip = HeistBundle2Manager.GetAudio(clipName);
            if (clip == null)
                clip = FindLoadedClip(clipName);

            if (clip != null)
                ClipCache[key] = clip;

            return clip;
        }

        public static void ClearResolvedClipCache(string reason = null)
        {
            ClipCache.Clear();
        }

        public static AudioSource PlayAt(
            string clipName,
            Vector3 position,
            float volume = 1f,
            bool flat = false,
            float minDistance = 8f,
            float maxDistance = 250f,
            bool ignoreListenerVolume = true,
            float startTime = 0f)
        {
            AudioClip clip = ResolveClip(clipName);
            if (clip == null)
                return null;

            GameObject audioObject = new GameObject("OneShotAudio_" + clipName);
            audioObject.transform.position = position;

            AudioSource source = audioObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.spatialBlend = flat ? 0f : 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.ignoreListenerVolume = ignoreListenerVolume;
            source.ignoreListenerPause = true;
            source.dopplerLevel = 0f;
            source.reverbZoneMix = 0f;
            source.spatialize = false;
            source.playOnAwake = false;
            source.loop = false;

            if (startTime > 0f)
                source.time = Mathf.Min(startTime, Mathf.Max(0f, clip.length - 0.01f));

            source.Play();
            UnityEngine.Object.Destroy(audioObject, clip.length + 0.1f);
            return source;
        }

        public static void Stop(AudioSource source)
        {
            if (source == null)
                return;

            source.Stop();
            UnityEngine.Object.Destroy(source.gameObject);
        }

        static AudioClip FindLoadedClip(string clipName)
        {
            string key = clipName.ToLowerInvariant();
            AudioClip[] clips = Resources.FindObjectsOfTypeAll<AudioClip>();

            for (int i = 0; i < clips.Length; i++)
            {
                AudioClip clip = clips[i];
                if (clip == null)
                    continue;

                string name = (clip.name ?? "").ToLowerInvariant();
                if (name == key || name.Contains(key))
                    return clip;

                string fullName = clip.ToString().ToLowerInvariant();
                if (fullName == key || fullName.Contains(key))
                    return clip;
            }

            return null;
        }
    }

    public class LoopingSfx : MonoBehaviour
    {
        public LoopingSfx(IntPtr ptr) : base(ptr) { }
        public LoopingSfx()
            : base(ClassInjector.DerivedConstructorPointer<LoopingSfx>())
            => ClassInjector.DerivedConstructorBody(this);

        public AudioSource _source;

        string _clipName;
        float _volume;
        float _minDistance;
        float _maxDistance;
        bool _initialized;
        GameObject _audioHost;

        void LateUpdate()
        {
            if (_audioHost == null)
                return;

            _audioHost.transform.position = transform.position;
            _audioHost.transform.rotation = transform.rotation;
        }

        void OnDestroy()
        {
            if (_audioHost != null)
                UnityEngine.Object.Destroy(_audioHost);

            _audioHost = null;
            _source = null;
        }

        public void InitAndPlay(
            string clipName,
            float volume = 0.6f,
            float minDistance = 1f,
            float maxDistance = 20f)
        {
            _clipName = clipName;
            _volume = volume;
            _minDistance = minDistance;
            _maxDistance = maxDistance;
            _initialized = !string.IsNullOrEmpty(_clipName);

            EnsureSource();
            if (_source == null || _source.clip == null)
                return;

            _source.Stop();
            _source.time = 0f;
            _source.Play();
        }

        public void TurnOn()
        {
            if (!_initialized)
                return;

            EnsureSource();
            if (_source != null && _source.clip != null && !_source.isPlaying)
                _source.Play();
        }

        public void TurnOff()
        {
            if (_source != null)
                _source.Stop();
        }

        public void SetVolume(float volume)
        {
            _volume = volume;

            if (_source != null)
                _source.volume = Mathf.Clamp01(_volume);
        }

        void EnsureSource()
        {
            if (!_initialized || string.IsNullOrEmpty(_clipName))
                return;

            if (_audioHost == null)
            {
                _audioHost = new GameObject("__LoopingSfxAudio_" + _clipName + "_" + GetInstanceID());
                _audioHost.transform.position = transform.position;
                _audioHost.transform.rotation = transform.rotation;
            }

            if (_source == null)
                _source = _audioHost.GetComponent<AudioSource>() ?? _audioHost.AddComponent<AudioSource>();

            AudioClip clip = AudioUtil.ResolveClip(_clipName);
            if (clip == null)
            {
                MelonLogger.Warning("[LoopingSfx] Could not resolve clip: " + _clipName);
                return;
            }

            _source.clip = clip;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.volume = Mathf.Clamp01(_volume);
            _source.spatialBlend = 1f;
            _source.rolloffMode = AudioRolloffMode.Logarithmic;
            _source.minDistance = _minDistance;
            _source.maxDistance = _maxDistance;
            _source.ignoreListenerVolume = true;
            _source.ignoreListenerPause = true;
            _source.dopplerLevel = 0f;
            _source.reverbZoneMix = 0f;
            _source.spatialize = false;
        }
    }
}
