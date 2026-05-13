using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public static class HeistBundle2Manager
    {
        private const string BundleFileName = "bundle2";

        private static AssetBundle _bundle;
        private static bool _tried;

        private static readonly Dictionary<string, Texture2D> _texCache =
            new Dictionary<string, Texture2D>();

        private static readonly Dictionary<string, AudioClip> _audioCache =
            new Dictionary<string, AudioClip>();

        private static readonly Dictionary<string, AnimationClip> _animCache =
            new Dictionary<string, AnimationClip>();

        private static readonly Dictionary<string, string> _audioNameMap =
            new Dictionary<string, string>();

        private static readonly Dictionary<string, string> _textureNameMap =
            new Dictionary<string, string>();

        private static readonly Dictionary<string, string> _animationNameMap =
            new Dictionary<string, string>();

        public static bool Init()
        {
            if (_bundle != null)
                return true;

            if (_tried)
            {
                MelonLogger.Warning("[HeistBundle2] Init previously tried and bundle is null. Forcing retry.");
                _tried = false;
            }

            _tried = true;

            try
            {
                var path = Path.Combine(MelonUtils.UserDataDirectory, BundleFileName);

                if (!File.Exists(path))
                {
                    MelonLogger.Warning("[HeistBundle2] Bundle not found: " + path);
                    return false;
                }

                _bundle = AssetBundle.LoadFromFile(path);
                if (_bundle == null)
                {
                    MelonLogger.Error("[HeistBundle2] LoadFromFile returned null.");
                    return false;
                }

                RebuildNameMaps();

                MelonLogger.Msg(
                    "[HeistBundle2] Loaded OK." +
                    " Assets=" + _bundle.GetAllAssetNames().Length +
                    " AudioNames=" + _audioNameMap.Count +
                    " TextureNames=" + _textureNameMap.Count +
                    " AnimationNames=" + _animationNameMap.Count);

                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[HeistBundle2] Init failed: " + ex);
                _bundle = null;
                return false;
            }
        }

        public static void Reset(string reason = null)
        {
            MelonLogger.Msg("[HeistBundle2] Reset called. reason=" + (reason ?? "none"));

            _texCache.Clear();
            _audioCache.Clear();
            _animCache.Clear();

            _audioNameMap.Clear();
            _textureNameMap.Clear();
            _animationNameMap.Clear();

            try
            {
                if (_bundle != null)
                {
                    _bundle.Unload(false);
                    MelonLogger.Msg("[HeistBundle2] Bundle unloaded.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[HeistBundle2] Bundle unload failed: " + ex.Message);
            }

            _bundle = null;
            _tried = false;
        }

        public static bool Reload(string reason = null)
        {
            Reset(reason ?? "Reload");
            return Init();
        }

        public static Texture2D GetTexture(string nameContains)
        {
            if (string.IsNullOrEmpty(nameContains))
            {
                MelonLogger.Warning("[HeistBundle2] GetTexture got null/empty query.");
                return null;
            }

            if (!Init()) return null;

            var key = nameContains.ToLowerInvariant();

            Texture2D cached;
            if (_texCache.TryGetValue(key, out cached))
            {
                if (cached != null) return cached;
                _texCache.Remove(key);
            }

            var assetName = FindBestAssetName(_textureNameMap, key);
            if (string.IsNullOrEmpty(assetName))
            {
                MelonLogger.Warning("[HeistBundle2] Texture not found containing: " + nameContains);
                return null;
            }

            try
            {
                var tex = _bundle.LoadAsset<Texture2D>(assetName);
                if (tex != null)
                {
                    _texCache[key] = tex;
                    return tex;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[HeistBundle2] Texture load failed for " + assetName + ": " + ex.Message);
            }

            MelonLogger.Warning("[HeistBundle2] Texture load returned null for: " + assetName);
            return null;
        }

        public static AnimationClip GetAnimation(string clipName)
        {
            if (string.IsNullOrEmpty(clipName))
            {
                MelonLogger.Warning("[HeistBundle2] GetAnimation got null/empty query.");
                return null;
            }

            if (!Init()) return null;

            var key = clipName.ToLowerInvariant();

            AnimationClip cached;
            if (_animCache.TryGetValue(key, out cached))
            {
                if (cached != null) return cached;
                _animCache.Remove(key);
            }

            string assetName = null;
            if (!_animationNameMap.TryGetValue(key, out assetName))
                assetName = FindBestAssetName(_animationNameMap, key);

            if (string.IsNullOrEmpty(assetName))
            {
                MelonLogger.Warning("[HeistBundle2] Animation not found: " + clipName);
                return null;
            }

            try
            {
                var clip = _bundle.LoadAsset<AnimationClip>(assetName);
                if (clip != null)
                {
                    _animCache[key] = clip;
                    return clip;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[HeistBundle2] Animation load failed for " + assetName + ": " + ex.Message);
            }

            MelonLogger.Warning("[HeistBundle2] Animation load returned null for: " + assetName);
            return null;
        }

        public static AudioClip GetAudio(string nameContains)
        {
            if (string.IsNullOrEmpty(nameContains))
            {
                MelonLogger.Warning("[HeistBundle2] GetAudio got null/empty query.");
                return null;
            }

            if (!Init()) return null;

            var key = nameContains.ToLowerInvariant();

            AudioClip cached;
            if (_audioCache.TryGetValue(key, out cached))
            {
                if (cached != null)
                {
                    MelonLogger.Msg("[HeistBundle2] GetAudio cache hit: " + key);
                    return cached;
                }

                _audioCache.Remove(key);
            }

            var assetName = FindBestAssetName(_audioNameMap, key);
            if (string.IsNullOrEmpty(assetName))
            {
                MelonLogger.Warning(
                    "[HeistBundle2] Audio not found containing: " + nameContains +
                    " assetCount=" + _audioNameMap.Count);
                return null;
            }

            try
            {
                var clip = _bundle.LoadAsset<AudioClip>(assetName);
                if (clip != null)
                {
                    _audioCache[key] = clip;

                    MelonLogger.Msg(
                        "[HeistBundle2] GetAudio loaded" +
                        " query=" + nameContains +
                        " asset=" + assetName +
                        " clipName=" + clip.name);

                    return clip;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[HeistBundle2] Audio load failed for " + assetName + ": " + ex.Message);
            }

            MelonLogger.Warning("[HeistBundle2] Audio load returned null for: " + assetName);
            return null;
        }

        public static GameObject GetGameObject(string nameContains)
        {
            if (string.IsNullOrEmpty(nameContains))
            {
                MelonLogger.Warning("[HeistBundle2] GetGameObject got null/empty query.");
                return null;
            }

            if (!Init()) return null;

            string key = nameContains.ToLowerInvariant();
            string[] names = _bundle.GetAllAssetNames();

            for (int i = 0; i < names.Length; i++)
            {
                string assetName = names[i];
                if (string.IsNullOrEmpty(assetName)) continue;

                string lower = assetName.ToLowerInvariant();
                if (!IsGameObjectAsset(lower)) continue;
                if (!lower.Contains(key)) continue;

                try
                {
                    GameObject prefab = _bundle.LoadAsset<GameObject>(assetName);
                    if (prefab != null)
                    {
                        MelonLogger.Msg(
                            "[HeistBundle2] GameObject loaded" +
                            " query=" + nameContains +
                            " asset=" + assetName +
                            " name=" + prefab.name);

                        return prefab;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning("[HeistBundle2] GameObject load failed for " + assetName + ": " + ex.Message);
                    return null;
                }

                MelonLogger.Warning("[HeistBundle2] GameObject load returned null for: " + assetName);
                return null;
            }

            MelonLogger.Warning("[HeistBundle2] GameObject prefab not found containing: " + nameContains);
            return null;
        }

        static void RebuildNameMaps()
        {
            _audioNameMap.Clear();
            _textureNameMap.Clear();
            _animationNameMap.Clear();

            if (_bundle == null)
                return;

            var names = _bundle.GetAllAssetNames();
            for (int i = 0; i < names.Length; i++)
            {
                var assetName = names[i];
                if (string.IsNullOrEmpty(assetName))
                    continue;

                var lower = assetName.ToLowerInvariant();

                if (IsAudioAsset(lower))
                    RegisterName(_audioNameMap, lower, assetName);

                if (IsTextureAsset(lower))
                    RegisterName(_textureNameMap, lower, assetName);

                if (IsAnimationAsset(lower))
                    RegisterName(_animationNameMap, lower, assetName);
            }
        }

        static void RegisterName(Dictionary<string, string> map, string lowerFullPath, string originalFullPath)
        {
            if (!map.ContainsKey(lowerFullPath))
                map.Add(lowerFullPath, originalFullPath);

            var fileName = Path.GetFileName(lowerFullPath);
            if (!string.IsNullOrEmpty(fileName) && !map.ContainsKey(fileName))
                map.Add(fileName, originalFullPath);

            var fileNameNoExt = Path.GetFileNameWithoutExtension(lowerFullPath);
            if (!string.IsNullOrEmpty(fileNameNoExt) && !map.ContainsKey(fileNameNoExt))
                map.Add(fileNameNoExt, originalFullPath);
        }

        static string FindBestAssetName(Dictionary<string, string> map, string key)
        {
            string exact;
            if (map.TryGetValue(key, out exact))
                return exact;

            foreach (var kvp in map)
            {
                if (kvp.Key.Contains(key))
                    return kvp.Value;
            }

            return null;
        }

        static bool IsAudioAsset(string lowerPath)
        {
            return lowerPath.EndsWith(".wav") ||
                   lowerPath.EndsWith(".mp3") ||
                   lowerPath.EndsWith(".ogg");
        }

        static bool IsTextureAsset(string lowerPath)
        {
            return lowerPath.EndsWith(".png") ||
                   lowerPath.EndsWith(".jpg") ||
                   lowerPath.EndsWith(".jpeg") ||
                   lowerPath.EndsWith(".tga") ||
                   lowerPath.EndsWith(".psd");
        }

        static bool IsAnimationAsset(string lowerPath)
        {
            return lowerPath.EndsWith(".anim");
        }

        static bool IsGameObjectAsset(string lowerPath)
        {
            return lowerPath.EndsWith(".prefab");
        }
    }
}
