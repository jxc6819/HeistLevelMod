using UnhollowerRuntimeLib;
using UnityEngine;
using System;
using MelonLoader;
using UnhollowerBaseLib.Attributes;
using System.Collections;

namespace IEYTD_Mod2Code
{
    public class LaserEmitter : MonoBehaviour
    {
        public LaserEmitter(IntPtr ptr) : base(ptr) { }
        public LaserEmitter() : base(ClassInjector.DerivedConstructorPointer<LaserEmitter>())
            => ClassInjector.DerivedConstructorBody(this);

        public float maxDistance = 25f;
        public LayerMask hitMask = ~0;
        public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
        public bool startEnabled = true;
        public Material beamMaterial;
        public float baseWidth = 0.012f;
        public float widthPulseAmount = 0.06f;
        public float pulseSpeed = 1.2f;
        public float scrollSpeed = 0.8f;
        public bool useUnscaledTime = false;
        public Color beamColor = new Color(1f, 0.05f, 0.05f, 1f);
        public bool softenEnd = true;
        public float endAlpha = 0.15f;
        public bool autoCreateLineRenderer = true;
        public int sortingOrder = 10;
        public float textureTileX = 4f;

        public float settleDelay = 0.5f;

        public bool logHitChanges = false;
        public bool logTrips = true;
        public bool logBaseline = false;
        public bool logBeamClears = false;

        public string[] ignoredNameContains = new string[] { "PREF", "Bolt", "Glass" };

        public bool detectHandsWithoutColliders = true;
        public float handTripRadius = 0.115f;
        public float botHandTripRadius = 0.145f;
        public float handForwardProbe = 0.095f;
        public float handSideProbe = 0.055f;
        public float handRayEndPadding = 0.015f;
        public bool logHandTrips = true;

        static Transform[] s_hands = new Transform[4];
        static string[] s_handNames = new string[] { "RightHand", "LeftHand", "BotRightHand", "BotLeftHand" };
        static float s_nextHandRefreshTime = -999f;
        static int s_handRefreshFrame = -1;

        public bool IsEnabled => _enabled;
        public bool HasHit => _hasHit;
        public float CurrentDistance => _currentDistance;
        public RaycastHit CurrentHitInfo => _currentHit;
        public Collider CurrentHitCollider => _currentHitCollider;

        bool _enabled;
        bool _hasHit;
        float _currentDistance;

        RaycastHit _currentHit;
        Collider _currentHitCollider;
        int _lastHitInstanceId;

        Collider _lastHitCollider;
        RaycastHit _lastHitInfo;

        bool _settled;
        bool _baselineCaptured;
        int _baselineId;
        string _baselineSummary;

        bool _handTripFired;

        LineRenderer _lr;
        Material _runtimeMat;
        float _scrollT;

        Gradient _cachedGradient;
        GradientColorKey[] _cachedColorKeys;
        GradientAlphaKey[] _cachedAlphaKeys;
        Color _lastGradientColor;
        float _lastGradientEndAlpha = -999f;
        bool _lastGradientSoften;

        static readonly int MainTexProp = Shader.PropertyToID("_MainTex");
        static readonly int ColorProp = Shader.PropertyToID("_Color");
        static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");

        struct HitFacts
        {
            public bool HasCollider;
            public int Id;
            public string Name;
            public string ParentName;
            public string RootName;
            public bool IsIgnored;
            public string Summary;
        }

        void Awake()
        {
            _enabled = startEnabled;
            ResetHitState(maxDistance);
            EnsureRenderer();
            ApplyRendererState(true);
        }

        void OnEnable()
        {
            ResetHitState(maxDistance);
            EnsureRenderer();
            ApplyRendererState(true);
            BeginSettleWindow();
        }

        void OnDisable()
        {
            if (_lr) _lr.enabled = false;

            if (_runtimeMat)
            {
#if UNITY_EDITOR
                DestroyImmediate(_runtimeMat);
#else
                Destroy(_runtimeMat);
#endif
                _runtimeMat = null;
            }
        }

        [HideFromIl2Cpp]
        IEnumerator Co_WaitForSettle()
        {
            float d = Mathf.Max(0f, settleDelay);
            if (d > 0f)
                yield return new WaitForSeconds(d);

            _settled = true;
            TryCaptureBaselineFromCurrent("SettleComplete");
        }

        void Update()
        {
            if (!_enabled)
            {
                if (_lr && _lr.enabled) _lr.enabled = false;
                return;
            }

            StepRaycast();
            UpdateBeamVisuals();
        }

        public void SetEnabled(bool enabled)
        {
            if (_enabled == enabled)
                return;

            _enabled = enabled;

            if (!_enabled)
            {
                if (_hasHit)
                    OnBeamCleared(_currentHitCollider, _currentHit);

                ResetHitState(maxDistance);
                if (_lr) _lr.enabled = false;
            }
            else
            {
                ResetHitState(maxDistance);
                EnsureRenderer();
                if (_lr) _lr.enabled = true;
                BeginSettleWindow();
            }
        }

        void BeginSettleWindow()
        {
            _settled = false;
            _baselineCaptured = false;
            _baselineId = 0;
            _baselineSummary = "none";
            _handTripFired = false;
            MelonCoroutines.Start(Co_WaitForSettle());
        }

        void StepRaycast()
        {
            Vector3 origin = transform.position;
            Vector3 dir = transform.forward;

            bool hit = Physics.Raycast(origin, dir, out RaycastHit hitInfo, maxDistance, hitMask, triggerInteraction);
            float rayEndDistance = hit ? hitInfo.distance : maxDistance;

            if (_settled && detectHandsWithoutColliders)
            {
                float handDist;
                string handName;
                if (TryGetNearestHandHit(origin, dir, Mathf.Max(0f, rayEndDistance - handRayEndPadding), out handDist, out handName))
                {
                    _hasHit = true;
                    _currentHit = default;
                    _currentHitCollider = null;
                    _currentDistance = Mathf.Clamp(handDist, 0f, maxDistance);

                    if (!_handTripFired)
                    {
                        _handTripFired = true;

                        if (logHandTrips || logTrips)
                        {
                            MelonLogger.Msg(
                                "[LaserEmitter] HAND TRIP emitter=" + GetEmitterTag() +
                                " hand=" + handName +
                                " dist=" + handDist.ToString("F4") +
                                " physicsEnd=" + rayEndDistance.ToString("F4")
                            );
                        }

                        HeistLevelManager.PoisonDeath();
                    }

                    return;
                }
                else
                {
                    _handTripFired = false;
                }
            }

            if (!hit)
            {
                if (_hasHit)
                    OnBeamCleared(_currentHitCollider, _currentHit);

                ResetHitState(maxDistance);
                return;
            }

            _hasHit = true;
            _currentHit = hitInfo;
            _currentHitCollider = hitInfo.collider;
            _currentDistance = hitInfo.distance;

            int hitId = _currentHitCollider ? _currentHitCollider.GetInstanceID() : 0;
            if (hitId != _lastHitInstanceId)
            {
                Collider oldCollider = _lastHitCollider;
                RaycastHit oldHit = _lastHitInfo;
                int oldId = _lastHitInstanceId;

                _lastHitInstanceId = hitId;
                OnHitObjectChanged(_currentHitCollider, _currentHit, oldCollider, oldHit, oldId);

                _lastHitCollider = _currentHitCollider;
                _lastHitInfo = _currentHit;
            }
        }

        bool TryGetNearestHandHit(Vector3 origin, Vector3 dir, float maxDist, out float bestDist, out string bestName)
        {
            bestDist = 999999f;
            bestName = null;

            if (maxDist <= 0.001f)
                return false;

            RefreshHandCacheIfNeeded();

            bool found = false;
            for (int i = 0; i < s_hands.Length; i++)
            {
                Transform h = s_hands[i];
                if (h == null || !h.gameObject.activeInHierarchy)
                    continue;

                bool bot = false;
                try { bot = h.name != null && h.name.IndexOf("Bot", StringComparison.OrdinalIgnoreCase) >= 0; } catch { }

                float r = bot ? botHandTripRadius : handTripRadius;

                TryHandProbePoint(h.position, s_handNames[i], origin, dir, maxDist, r, ref found, ref bestDist, ref bestName);
                TryHandProbePoint(h.position + h.forward * handForwardProbe, s_handNames[i] + "/forward", origin, dir, maxDist, r, ref found, ref bestDist, ref bestName);
                TryHandProbePoint(h.position + h.right * handSideProbe, s_handNames[i] + "/right", origin, dir, maxDist, r, ref found, ref bestDist, ref bestName);
                TryHandProbePoint(h.position - h.right * handSideProbe, s_handNames[i] + "/left", origin, dir, maxDist, r, ref found, ref bestDist, ref bestName);
            }

            return found;
        }

        static void TryHandProbePoint(Vector3 point, string name, Vector3 origin, Vector3 dir, float maxDist, float radius, ref bool found, ref float bestDist, ref string bestName)
        {
            Vector3 toPoint = point - origin;
            float t = Vector3.Dot(toPoint, dir);

            if (t < 0f || t > maxDist)
                return;

            Vector3 closest = origin + dir * t;
            float sqr = (point - closest).sqrMagnitude;
            float rr = radius * radius;

            if (sqr > rr)
                return;

            if (t < bestDist)
            {
                found = true;
                bestDist = t;
                bestName = name;
            }
        }

        static void RefreshHandCacheIfNeeded()
        {

            if (s_handRefreshFrame == Time.frameCount)
                return;

            float t = Time.unscaledTime;
            if (t < s_nextHandRefreshTime)
                return;

            s_handRefreshFrame = Time.frameCount;
            s_nextHandRefreshTime = t + 0.35f;

            for (int i = 0; i < s_handNames.Length; i++)
            {
                Transform old = s_hands[i];
                if (old != null && old.gameObject.activeInHierarchy)
                    continue;

                GameObject g = GameObject.Find(s_handNames[i]);
                s_hands[i] = g ? g.transform : null;
            }
        }

        void ResetHitState(float distanceIfNoHit)
        {
            _hasHit = false;
            _currentHit = default;
            _currentHitCollider = null;
            _currentDistance = distanceIfNoHit;
            _lastHitInstanceId = 0;
            _lastHitCollider = null;
            _lastHitInfo = default;
            _handTripFired = false;
        }

        protected virtual void OnHitObjectChanged(Collider newHitCollider, RaycastHit newHitInfo, Collider oldHitCollider, RaycastHit oldHitInfo, int oldHitId)
        {
            if (newHitCollider == null)
                return;

            HitFacts oldFacts = BuildFacts(oldHitCollider, oldHitInfo, oldHitId);
            HitFacts newFacts = BuildFacts(newHitCollider, newHitInfo, newHitCollider.GetInstanceID());

            if (logHitChanges)
            {
                float distDelta = oldFacts.HasCollider ? (newHitInfo.distance - oldHitInfo.distance) : 0f;
                MelonLogger.Msg(
                    "[LaserEmitter] Hit changed " +
                    "emitter=" + GetEmitterTag() +
                    " settled=" + _settled +
                    " baselineCaptured=" + _baselineCaptured +
                    " baselineId=" + _baselineId +
                    " oldIgnored=" + oldFacts.IsIgnored +
                    " newIgnored=" + newFacts.IsIgnored +
                    " distDelta=" + distDelta.ToString("F4") +
                    " old={" + oldFacts.Summary + "}" +
                    " new={" + newFacts.Summary + "}"
                );
            }

            if (!_settled)
                return;

            if (newFacts.IsIgnored)
            {
                if (logTrips)
                    MelonLogger.Msg("[LaserEmitter] IGNORE emitter=" + GetEmitterTag() + " collider=" + newFacts.Name);
                return;
            }

            if (_baselineCaptured && newFacts.Id == _baselineId)
            {
                if (logTrips)
                    MelonLogger.Msg("[LaserEmitter] SAFE baseline-return emitter=" + GetEmitterTag() + " collider=" + newFacts.Name);
                return;
            }

            if (!_baselineCaptured && logTrips)
            {
                MelonLogger.Msg(
                    "[LaserEmitter] TRIP no-baseline emitter=" + GetEmitterTag() +
                    " collider=" + newFacts.Name +
                    " reason=NoValidRestingBaseline"
                );
            }
            else if (logTrips)
            {
                MelonLogger.Msg(
                    "[LaserEmitter] TRIP emitter=" + GetEmitterTag() +
                    " collider=" + newFacts.Name +
                    " baseline={" + _baselineSummary + "}"
                );
            }

            HeistLevelManager.PoisonDeath();
        }

        void TryCaptureBaselineFromCurrent(string reason)
        {
            if (_baselineCaptured)
                return;
            if (!_hasHit || _currentHitCollider == null)
                return;

            HitFacts f = BuildFacts(_currentHitCollider, _currentHit, _currentHitCollider.GetInstanceID());
            if (f.IsIgnored)
            {
                if (logBaseline)
                {
                    MelonLogger.Msg(
                        "[LaserEmitter] BASELINE skipped emitter=" + GetEmitterTag() +
                        " reason=" + reason +
                        " hit={" + f.Summary + "}"
                    );
                }
                return;
            }

            CaptureBaseline(f, reason);
        }

        void CaptureBaseline(HitFacts f, string reason)
        {
            _baselineCaptured = true;
            _baselineId = f.Id;
            _baselineSummary = f.Summary;

            if (logBaseline)
            {
                MelonLogger.Msg(
                    "[LaserEmitter] BASELINE captured emitter=" + GetEmitterTag() +
                    " reason=" + reason +
                    " baseline={" + f.Summary + "}"
                );
            }
        }

        protected virtual void OnBeamCleared(Collider previousHitCollider, RaycastHit previousHit)
        {
            if (!logBeamClears)
                return;

            HitFacts prev = BuildFacts(previousHitCollider, previousHit, previousHitCollider ? previousHitCollider.GetInstanceID() : 0);
            MelonLogger.Msg(
                "[LaserEmitter] Beam cleared " +
                "emitter=" + GetEmitterTag() +
                " settled=" + _settled +
                " baselineCaptured=" + _baselineCaptured +
                " prev={" + prev.Summary + "}"
            );
        }

        HitFacts BuildFacts(Collider col, RaycastHit hit, int id)
        {
            HitFacts f = new HitFacts();
            f.HasCollider = col != null;
            f.Id = id;

            if (col == null)
            {
                f.Name = "none";
                f.ParentName = "null";
                f.RootName = "null";
                f.Summary = "none";
                f.IsIgnored = false;
                return f;
            }

            f.Name = SafeName(col);
            f.ParentName = SafeName(col.transform.parent);
            f.RootName = GetRootName(col.transform);
            f.IsIgnored = MatchesIgnoredRules(f.Name) || MatchesIgnoredRules(f.ParentName) || MatchesIgnoredRules(f.RootName);

            string rbName = "null";
            try
            {
                if (col.attachedRigidbody != null)
                    rbName = SafeName(col.attachedRigidbody.gameObject);
            }
            catch { }

            f.Summary =
                "name=" + f.Name +
                ",id=" + id +
                ",layer=" + col.gameObject.layer +
                ",trigger=" + col.isTrigger +
                ",parent=" + f.ParentName +
                ",root=" + f.RootName +
                ",rb=" + rbName +
                ",ignored=" + f.IsIgnored +
                ",dist=" + hit.distance.ToString("F4") +
                ",point=" + FormatVec3(hit.point) +
                ",normal=" + FormatVec3(hit.normal);

            return f;
        }

        bool MatchesIgnoredRules(string value)
        {
            return ContainsAny(value, ignoredNameContains);
        }

        static bool ContainsAny(string value, string[] needles)
        {
            if (string.IsNullOrEmpty(value) || needles == null)
                return false;

            for (int i = 0; i < needles.Length; i++)
            {
                string needle = needles[i];
                if (string.IsNullOrEmpty(needle))
                    continue;

                if (value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        string GetEmitterTag()
        {
            return SafeName(gameObject) + "@" + GetPath(transform);
        }

        static string SafeName(UnityEngine.Object obj)
        {
            if (obj == null)
                return "null";
            try
            {
                return obj.name ?? "null";
            }
            catch
            {
                return "null";
            }
        }

        static string GetRootName(Transform t)
        {
            if (t == null)
                return "null";
            try
            {
                return t.root != null ? SafeName(t.root.gameObject) : SafeName(t.gameObject);
            }
            catch
            {
                return "null";
            }
        }

        static string GetPath(Transform t)
        {
            if (t == null)
                return "null";

            string path = SafeName(t.gameObject);
            Transform cur = t.parent;

            while (cur != null)
            {
                path = SafeName(cur.gameObject) + "/" + path;
                cur = cur.parent;
            }

            return path;
        }

        static string FormatVec3(Vector3 v)
        {
            return "(" + v.x.ToString("F3") + ", " + v.y.ToString("F3") + ", " + v.z.ToString("F3") + ")";
        }

        void EnsureRenderer()
        {
            if (_lr == null)
                _lr = GetComponent<LineRenderer>();

            if (_lr == null && autoCreateLineRenderer)
                _lr = gameObject.AddComponent<LineRenderer>();

            if (_lr == null)
                return;

            _lr.useWorldSpace = true;
            _lr.positionCount = 2;
            _lr.alignment = LineAlignment.View;
            _lr.textureMode = LineTextureMode.Tile;
            _lr.numCapVertices = 6;
            _lr.numCornerVertices = 2;
            _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lr.receiveShadows = false;
            _lr.sortingOrder = sortingOrder;

            if (beamMaterial != null)
            {
                _lr.material = beamMaterial;
            }
            else
            {
                _runtimeMat = CreateRuntimeBeamMaterial();
                _lr.material = _runtimeMat;
            }

            _lr.startColor = beamColor;
            _lr.endColor = beamColor;
            ApplyColorToMaterial(_lr.material, beamColor);
            _lr.enabled = _enabled;
        }

        Material CreateRuntimeBeamMaterial()
        {
            Shader s =
                Shader.Find("Sprites/Default") ??
                Shader.Find("Unlit/Transparent") ??
                Shader.Find("Unlit/Texture") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Standard");

            var mat = new Material(s);

            if (mat.HasProperty(MainTexProp))
                mat.SetTexture(MainTexProp, Texture2D.whiteTexture);

            ApplyColorToMaterial(mat, beamColor);
            return mat;
        }

        void UpdateBeamVisuals()
        {
            if (!_lr)
                return;

            if (!_lr.enabled)
                _lr.enabled = true;

            Vector3 start = transform.position;
            Vector3 end = start + transform.forward * (_hasHit ? _currentDistance : maxDistance);

            _lr.SetPosition(0, start);
            _lr.SetPosition(1, end);

            float t = useUnscaledTime ? Time.unscaledTime : Time.time;

            float pulse = 1f;
            if (widthPulseAmount > 0f)
            {
                float s = Mathf.Sin(t * pulseSpeed * 3.1415926535897f * 2f);
                pulse = 1f + s * widthPulseAmount;
                pulse = Mathf.Clamp(pulse, 1f - widthPulseAmount, 1f + widthPulseAmount);
            }

            float w = Mathf.Max(0.0005f, baseWidth * pulse);
            _lr.startWidth = w;
            _lr.endWidth = w;

            ApplyGradientIfNeeded();

            if (scrollSpeed != 0f && _lr.material != null && _lr.material.HasProperty(MainTexProp))
            {
                float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                _scrollT += dt * scrollSpeed;

                _lr.material.mainTextureOffset = new Vector2(_scrollT, 0f);
                _lr.material.mainTextureScale = new Vector2(Mathf.Max(0.01f, textureTileX), 1f);
            }

            ApplyColorToMaterial(_lr.material, beamColor);
        }

        void ApplyGradientIfNeeded()
        {
            if (_lr == null)
                return;

            float wantedEndAlpha = softenEnd ? Mathf.Clamp01(endAlpha) : beamColor.a;

            if (_cachedGradient != null &&
                _lastGradientColor.Equals(beamColor) &&
                Mathf.Abs(_lastGradientEndAlpha - wantedEndAlpha) < 0.0001f &&
                _lastGradientSoften == softenEnd)
                return;

            if (_cachedGradient == null)
            {
                _cachedGradient = new Gradient();
                _cachedColorKeys = new GradientColorKey[2];
                _cachedAlphaKeys = new GradientAlphaKey[2];
            }

            Color c0 = beamColor;
            Color c1 = softenEnd ? new Color(beamColor.r, beamColor.g, beamColor.b, wantedEndAlpha) : beamColor;

            _cachedColorKeys[0] = new GradientColorKey { color = c0, time = 0f };
            _cachedColorKeys[1] = new GradientColorKey { color = c1, time = 1f };

            _cachedAlphaKeys[0] = new GradientAlphaKey { alpha = c0.a, time = 0f };
            _cachedAlphaKeys[1] = new GradientAlphaKey { alpha = c1.a, time = 1f };

            _cachedGradient.SetKeys(_cachedColorKeys, _cachedAlphaKeys);
            _lr.colorGradient = _cachedGradient;
            _lr.startColor = c0;
            _lr.endColor = c1;

            _lastGradientColor = beamColor;
            _lastGradientEndAlpha = wantedEndAlpha;
            _lastGradientSoften = softenEnd;
        }

        void ApplyRendererState(bool force)
        {
            if (!_lr)
                return;

            bool shouldShow = _enabled;
            if (force || _lr.enabled != shouldShow)
                _lr.enabled = shouldShow;

            _lr.startColor = beamColor;
            _lr.endColor = beamColor;

            ApplyColorToMaterial(_lr.material, beamColor);
        }

        static void ApplyColorToMaterial(Material m, Color c)
        {
            if (!m) return;

            if (m.HasProperty(BaseColorProp)) m.SetColor(BaseColorProp, c);
            if (m.HasProperty(ColorProp)) m.SetColor(ColorProp, c);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Vector3 origin = transform.position;
            Vector3 dir = transform.forward;

            float dist = maxDistance;

            if (!Application.isPlaying)
            {
                if (Physics.Raycast(origin, dir, out RaycastHit hitInfo, maxDistance, hitMask, triggerInteraction))
                    dist = hitInfo.distance;
            }
            else
            {
                dist = _hasHit ? _currentDistance : maxDistance;
            }

            Gizmos.DrawLine(origin, origin + dir * dist);
        }
#endif
    }
}
