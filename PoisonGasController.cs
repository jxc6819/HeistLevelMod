using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public static class PoisonGasController
    {
        private static readonly List<ParticleSystem> _systems = new List<ParticleSystem>();
        private static readonly List<ParticleSystemRenderer> _renderers = new List<ParticleSystemRenderer>();
        private static bool _cached;
        private static bool _prepared;
        private static bool _running;

        public static void InitializeAllOff()
        {
            ForceFreshAllOff("InitializeAllOff");
        }

        public static void ForceFreshAllOff(string reason = "ForceFreshAllOff")
        {
            _systems.Clear();
            _renderers.Clear();
            _cached = false;
            _prepared = false;
            _running = false;

            RefreshCache();
            PrepareForFastStart();
            StopAll();

            MelonLogger.Msg("[PoisonGasController] Forced poison gas OFF. reason=" + reason);
        }

        public static void RefreshCache()
        {
            _systems.Clear();
            _renderers.Clear();
            _cached = false;
            _prepared = false;
            _running = false;

            try
            {
                GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
                for (int i = 0; i < all.Length; i++)
                {
                    GameObject go = all[i];
                    if (go == null) continue;
                    if (!string.Equals(go.name, "PoisonGas_Emitter", StringComparison.Ordinal)) continue;

                    ParticleSystem ps = null;
                    ParticleSystemRenderer psr = null;

                    try { ps = go.GetComponent<ParticleSystem>(); } catch { }
                    try { psr = go.GetComponent<ParticleSystemRenderer>(); } catch { }

                    if (ps != null) _systems.Add(ps);
                    if (psr != null) _renderers.Add(psr);
                }

                _cached = true;
                MelonLogger.Msg("[PoisonGasController] Cached " + _systems.Count + " poison gas system(s).");
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[PoisonGasController] RefreshCache failed: " + e);
            }
        }

        public static void PrepareForFastStart()
        {
            EnsureCache();
            if (_prepared) return;

            for (int i = 0; i < _systems.Count; i++)
            {
                ParticleSystem ps = _systems[i];
                if (ps == null) continue;

                try
                {
                    if (!ps.gameObject.activeSelf)
                        ps.gameObject.SetActive(true);
                }
                catch { }

                try
                {
                    var main = ps.main;
                    main.playOnAwake = false;
                }
                catch { }

                try { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); } catch { }
                try { ps.Clear(true); } catch { }
            }

            for (int i = 0; i < _renderers.Count; i++)
            {
                ParticleSystemRenderer psr = _renderers[i];
                if (psr == null) continue;

                try
                {
                    if (!psr.gameObject.activeSelf)
                        psr.gameObject.SetActive(true);
                }
                catch { }

                try { psr.enabled = false; } catch { }
            }

            _prepared = true;
        }

        public static void StartAll()
        {
            EnsureCache();
            PrepareForFastStart();
            if (_running) return;

            for (int i = 0; i < _renderers.Count; i++)
            {
                ParticleSystemRenderer psr = _renderers[i];
                if (psr == null) continue;
                try { psr.enabled = true; } catch { }
            }

            for (int i = 0; i < _systems.Count; i++)
            {
                ParticleSystem ps = _systems[i];
                if (ps == null) continue;
                try { ps.Play(true); } catch { }
            }

            _running = true;
            MelonLogger.Msg("[PoisonGasController] Started all poison gas.");
        }

        public static void StopAll()
        {
            EnsureCache();
            PrepareForFastStart();

            for (int i = 0; i < _systems.Count; i++)
            {
                ParticleSystem ps = _systems[i];
                if (ps == null) continue;

                try { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); } catch { }
                try { ps.Clear(true); } catch { }
            }

            for (int i = 0; i < _renderers.Count; i++)
            {
                ParticleSystemRenderer psr = _renderers[i];
                if (psr == null) continue;
                try { psr.enabled = false; } catch { }
            }

            _running = false;
            MelonLogger.Msg("[PoisonGasController] Stopped all poison gas.");
        }

        public static void SetAllActive(bool active)
        {
            if (active) StartAll();
            else StopAll();
        }

        public static bool HasAny()
        {
            EnsureCache();
            return _systems.Count > 0;
        }

        private static void EnsureCache()
        {
            if (!_cached || _systems.Count == 0)
                RefreshCache();
        }
    }
}
