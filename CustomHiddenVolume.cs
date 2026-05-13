using System;
using System.Collections.Generic;
using MelonLoader;
using SG.Phoenix.Assets.Code.Interactables;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class CustomHiddenVolume : MonoBehaviour
    {
        public CustomHiddenVolume(IntPtr ptr) : base(ptr) { }
        public CustomHiddenVolume() : base(ClassInjector.DerivedConstructorPointer<CustomHiddenVolume>())
            => ClassInjector.DerivedConstructorBody(this);

        public PickUp pickup;
        public Transform viewOrigin;
        public Rigidbody targetBody;
        public Transform targetRoot;

        public float checkInterval = 0.05f;
        public float enterDelay = 0.08f;
        public float exitDelay = 0.12f;

        public float rayInset = 0.01f;
        public LayerMask obstructionMask = ~0;
        public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        public bool requireFullyHidden = true;
        public bool usePartialObscureThreshold = true;
        public float enterObscuredFraction = 0.55f;
        public float exitObscuredFraction = 0.35f;
        public bool activateOnlyAfterFirstGrab = true;
        public bool ignoreWhileHeld = false;
        public bool ignoreViewOriginHierarchy = false;
        public bool ignoreHeadsetObjects = true;
        public bool debugDraw = false;
        public bool debugLog = false;

        public bool pushBlockingCollidersToHiddenLayer = true;
        public int hiddenBlockerLayer = 18;
        public bool ignoreForcedHiddenLayerDuringDetection = true;

        public bool suppressHiddenVolumeWhenHardBarrierPresent = true;
        public LayerMask hardBarrierMask = 0;
        public string hardBarrierNameContainsCsv = "window,glass";

        public bool logBlockerDetails = true;
        public bool logEveryCheck = false;

        Camera _hmdCam;
        Collider[] _selfColliders;
        Collider[] _targetColliders;

        readonly List<Collider> _sampleColliders = new List<Collider>();
        readonly List<Vector3> _samplePoints = new List<Vector3>();
        readonly List<RaycastHit> _hits = new List<RaycastHit>();
        readonly List<Collider> _currentBlockingColliders = new List<Collider>();
        readonly HashSet<Collider> _currentBlockingSet = new HashSet<Collider>();
        readonly Dictionary<GameObject, int> _forcedBlockerOriginalLayers = new Dictionary<GameObject, int>();
        readonly List<GameObject> _restoreScratch = new List<GameObject>();
        readonly List<string> _hardBarrierTokens = new List<string>();
        readonly HashSet<GameObject> _currentBlockingObjects = new HashSet<GameObject>();
        readonly List<Collider> _currentRawBlockingColliders = new List<Collider>();
        readonly HashSet<Collider> _currentRawBlockingSet = new HashSet<Collider>();
        readonly HashSet<GameObject> _currentRawBlockingObjects = new HashSet<GameObject>();

        bool _hiddenActive;
        bool _hasBeenHeldOnce;
        float _nextCheckTime;
        float _hiddenTimer;
        float _visibleTimer;

        bool _lastVisuallyObstructed;
        bool _lastAllowedHidden;
        bool _lastHardBarrierPresent;
        float _lastObscuredFraction;
        string _lastBlockerSummary = "";

        void Awake()
        {
            RebuildHardBarrierTokens();
            AutoResolveReferences();
            CacheColliders();
        }

        void Start()
        {
            RebuildHardBarrierTokens();
            AutoResolveReferences();
            CacheColliders();
        }

        void OnEnable()
        {
            _hiddenActive = false;
            _hiddenTimer = 0f;
            _visibleTimer = 0f;
            _nextCheckTime = 0f;
            _lastVisuallyObstructed = false;
            _lastAllowedHidden = false;
            _lastHardBarrierPresent = false;
            _lastObscuredFraction = 0f;
            _lastBlockerSummary = "";
            ClearCurrentBlockingCache();
        }

        void OnDisable()
        {
            ForceVisibleState();
        }

        void OnDestroy()
        {
            ForceVisibleState();
        }

        void Update()
        {
            if (pickup == null)
                AutoResolveReferences();

            if (pickup == null)
                return;

            if (pickup.isHeld)
                _hasBeenHeldOnce = true;

            if (activateOnlyAfterFirstGrab && !_hasBeenHeldOnce)
            {
                ForceVisibleState();
                return;
            }

            if (ignoreWhileHeld && pickup.isHeld)
            {
                ForceVisibleState();
                return;
            }

            if (Time.time < _nextCheckTime)
                return;

            _nextCheckTime = Time.time + Mathf.Max(0.01f, checkInterval);

            AutoResolveReferences();
            CacheCollidersIfNeeded();
            RebuildHardBarrierTokens();

            if (viewOrigin == null || targetRoot == null)
                return;

            bool visuallyObstructed = IsObstructedFromView();
            bool hardBarrierPresent = CurrentBlockersContainHardBarrier();
            bool allowHidden = visuallyObstructed && (!suppressHiddenVolumeWhenHardBarrierPresent || !hardBarrierPresent);

            MaybeLogState(visuallyObstructed, hardBarrierPresent, allowHidden);

            if (allowHidden)
            {
                _hiddenTimer += checkInterval;
                _visibleTimer = 0f;

                if (!_hiddenActive)
                {
                    if (_hiddenTimer >= enterDelay)
                        SetHiddenState(true);
                }
                else
                {
                    ApplyBlockingColliderLayers();
                }
            }
            else
            {
                _visibleTimer += checkInterval;
                _hiddenTimer = 0f;

                if (_hiddenActive && _visibleTimer >= exitDelay)
                    SetHiddenState(false);
                else
                    RestoreAllForcedBlockerLayers();
            }
        }

        void AutoResolveReferences()
        {
            if (pickup == null)
                pickup = GetComponent<PickUp>() ?? GetComponentInParent<PickUp>() ?? GetComponentInChildren<PickUp>(true);

            if (targetBody == null)
            {
                targetBody = GetComponent<Rigidbody>() ??
                             GetComponentInParent<Rigidbody>() ??
                             GetComponentInChildren<Rigidbody>(true);
            }

            if (targetRoot == null)
            {
                if (targetBody != null)
                    targetRoot = targetBody.transform;
                else if (pickup != null)
                    targetRoot = pickup.transform;
                else
                    targetRoot = transform;
            }

            if (viewOrigin == null)
            {
                if (_hmdCam == null)
                    _hmdCam = FindHMDCamera();

                if (_hmdCam != null)
                    viewOrigin = _hmdCam.transform;
                else
                {
                    GameObject hmd = GameObject.Find("HMD");
                    if (hmd != null)
                        viewOrigin = hmd.transform;
                }
            }
        }

        void CacheCollidersIfNeeded()
        {
            if (_selfColliders == null || _selfColliders.Length == 0 || _targetColliders == null || _targetColliders.Length == 0)
                CacheColliders();
        }

        void CacheColliders()
        {
            _selfColliders = GetComponentsInChildren<Collider>(true);

            if (targetRoot != null)
                _targetColliders = targetRoot.GetComponentsInChildren<Collider>(true);
            else
                _targetColliders = new Collider[0];

            _sampleColliders.Clear();
            for (int i = 0; i < _targetColliders.Length; i++)
            {
                Collider c = _targetColliders[i];
                if (c == null) continue;
                if (!c.enabled) continue;
                if (c.isTrigger) continue;
                _sampleColliders.Add(c);
            }
        }

        bool IsObstructedFromView()
        {
            BuildSamplePoints();
            ClearCurrentBlockingCache();

            if (_samplePoints.Count == 0)
            {
                _lastObscuredFraction = 0f;
                return false;
            }

            int blockedCount = 0;
            Vector3 origin = viewOrigin.position;

            for (int i = 0; i < _samplePoints.Count; i++)
            {
                Vector3 target = _samplePoints[i];
                bool blocked = IsRayBlocked(origin, target);

                if (debugDraw)
                    Debug.DrawLine(origin, target, blocked ? Color.red : Color.cyan, checkInterval);

                if (blocked)
                    blockedCount++;
            }

            _lastObscuredFraction = (float)blockedCount / (float)_samplePoints.Count;

            if (!usePartialObscureThreshold)
            {
                if (requireFullyHidden)
                    return blockedCount >= _samplePoints.Count;

                return blockedCount > 0;
            }

            float threshold = _hiddenActive ? exitObscuredFraction : enterObscuredFraction;
            threshold = Mathf.Clamp01(threshold);

            return _lastObscuredFraction >= threshold;
        }

        void BuildSamplePoints()
        {
            _samplePoints.Clear();

            Bounds bounds;
            if (!TryGetCombinedBounds(out bounds))
            {
                Transform t = targetRoot != null ? targetRoot : transform;
                _samplePoints.Add(t.position);
                return;
            }

            Vector3 c = bounds.center;
            Vector3 e = bounds.extents;

            float ix = Mathf.Max(0f, e.x - rayInset);
            float iy = Mathf.Max(0f, e.y - rayInset);
            float iz = Mathf.Max(0f, e.z - rayInset);

            _samplePoints.Add(c);
            _samplePoints.Add(c + new Vector3(ix, 0f, 0f));
            _samplePoints.Add(c + new Vector3(-ix, 0f, 0f));
            _samplePoints.Add(c + new Vector3(0f, iy, 0f));
            _samplePoints.Add(c + new Vector3(0f, -iy, 0f));
            _samplePoints.Add(c + new Vector3(0f, 0f, iz));
            _samplePoints.Add(c + new Vector3(0f, 0f, -iz));
        }

        bool TryGetCombinedBounds(out Bounds bounds)
        {
            bounds = default(Bounds);
            bool hasBounds = false;

            for (int i = 0; i < _sampleColliders.Count; i++)
            {
                Collider c = _sampleColliders[i];
                if (c == null) continue;
                if (!c.enabled) continue;

                if (!hasBounds)
                {
                    bounds = c.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(c.bounds);
                }
            }

            return hasBounds;
        }

        bool IsRayBlocked(Vector3 origin, Vector3 target)
        {
            Vector3 delta = target - origin;
            float dist = delta.magnitude;
            if (dist <= 0.0001f)
                return false;

            Vector3 dir = delta / dist;
            RaycastHit[] hits = Physics.RaycastAll(origin, dir, dist + 0.02f, obstructionMask, triggerInteraction);
            if (hits == null || hits.Length == 0)
                return false;

            _hits.Clear();
            for (int i = 0; i < hits.Length; i++)
                _hits.Add(hits[i]);

            _hits.Sort((a, b) => a.distance.CompareTo(b.distance));

            bool foundPhysicalBlocker = false;
            bool foundVisibleBlocker = false;

            for (int i = 0; i < _hits.Count; i++)
            {
                Collider hitCol = _hits[i].collider;
                if (hitCol == null) continue;
                if (!hitCol.enabled) continue;
                if (IsIgnoredColliderForAnyPurpose(hitCol)) continue;

                if (IsTargetCollider(hitCol))
                {
                    if (foundPhysicalBlocker)
                        return true;

                    return false;
                }

                RegisterRawBlockingCollider(hitCol);
                foundPhysicalBlocker = true;

                if (!ShouldIgnoreColliderForVisibleState(hitCol))
                {
                    RegisterVisibleBlockingCollider(hitCol);
                    foundVisibleBlocker = true;
                }
            }

            return foundPhysicalBlocker || foundVisibleBlocker;
        }

        void RegisterVisibleBlockingCollider(Collider c)
        {
            if (c == null)
                return;

            if (_currentBlockingSet.Add(c))
            {
                _currentBlockingColliders.Add(c);
                if (c.gameObject != null)
                    _currentBlockingObjects.Add(c.gameObject);
            }
        }

        void RegisterRawBlockingCollider(Collider c)
        {
            if (c == null)
                return;

            if (_currentRawBlockingSet.Add(c))
            {
                _currentRawBlockingColliders.Add(c);
                if (c.gameObject != null)
                    _currentRawBlockingObjects.Add(c.gameObject);
            }
        }

        void ClearCurrentBlockingCache()
        {
            _currentBlockingSet.Clear();
            _currentBlockingColliders.Clear();
            _currentBlockingObjects.Clear();

            _currentRawBlockingSet.Clear();
            _currentRawBlockingColliders.Clear();
            _currentRawBlockingObjects.Clear();
        }

        bool IsIgnoredColliderForAnyPurpose(Collider c)
        {
            if (c == null)
                return true;

            if (IsSelfCollider(c))
                return true;

            if (ignoreHeadsetObjects && IsHeadsetRelatedCollider(c))
                return true;

            if (ignoreViewOriginHierarchy && viewOrigin != null)
            {
                Transform root = viewOrigin.root;
                if (root != null && c.transform.IsChildOf(root))
                    return true;
            }

            return false;
        }

        bool ShouldIgnoreColliderForVisibleState(Collider c)
        {
            if (c == null)
                return true;

            if (!ignoreForcedHiddenLayerDuringDetection)
                return false;

            GameObject go = c.gameObject;
            if (go == null)
                return false;

            if (!_forcedBlockerOriginalLayers.ContainsKey(go))
                return false;

            return true;
        }

        bool IsTargetCollider(Collider c)
        {
            if (c == null || _targetColliders == null)
                return false;

            for (int i = 0; i < _targetColliders.Length; i++)
            {
                if (_targetColliders[i] == c)
                    return true;
            }

            return false;
        }

        bool IsSelfCollider(Collider c)
        {
            if (c == null || _selfColliders == null)
                return false;

            for (int i = 0; i < _selfColliders.Length; i++)
            {
                if (_selfColliders[i] == c)
                    return true;
            }

            return false;
        }

        bool IsHeadsetRelatedCollider(Collider c)
        {
            if (c == null)
                return false;

            GameObject go = c.gameObject;
            if (go == null)
                return false;

            if (NameContainsHeadset(go.name))
                return true;

            if (go.GetComponent<HeadsetScript>() != null)
                return true;

            if (go.GetComponentInChildren<HeadsetScript>(true) != null)
                return true;

            Transform root = go.transform.root;
            if (root != null)
            {
                if (NameContainsHeadset(root.name))
                    return true;

                if (root.GetComponent<HeadsetScript>() != null)
                    return true;

                if (root.GetComponentInChildren<HeadsetScript>(true) != null)
                    return true;
            }

            return false;
        }

        bool NameContainsHeadset(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            return name.IndexOf("headset", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        bool CurrentBlockersContainHardBarrier()
        {
            for (int i = 0; i < _currentRawBlockingColliders.Count; i++)
            {
                Collider c = _currentRawBlockingColliders[i];
                if (IsHardBarrier(c))
                    return true;
            }

            return false;
        }

        bool IsHardBarrier(Collider c)
        {
            if (c == null || c.gameObject == null)
                return false;

            GameObject go = c.gameObject;
            if (LayerIsInMask(go.layer, hardBarrierMask))
                return true;

            if (NameContainsHardBarrierToken(go.name))
                return true;

            Transform root = go.transform.root;
            if (root != null && NameContainsHardBarrierToken(root.name))
                return true;

            return false;
        }

        bool IsEligibleForForcedHiddenLayer(Collider c)
        {
            if (c == null || c.gameObject == null)
                return false;

            if (IsTargetCollider(c))
                return false;

            if (IsSelfCollider(c))
                return false;

            if (ignoreHeadsetObjects && IsHeadsetRelatedCollider(c))
                return false;

            if (c.gameObject.layer == hiddenBlockerLayer)
                return false;

            if (IsHardBarrier(c))
                return false;

            return true;
        }

        bool LayerIsInMask(int layer, LayerMask mask)
        {
            int bit = 1 << layer;
            return (mask.value & bit) != 0;
        }

        void RebuildHardBarrierTokens()
        {
            _hardBarrierTokens.Clear();

            if (string.IsNullOrEmpty(hardBarrierNameContainsCsv))
                return;

            string[] raw = hardBarrierNameContainsCsv.Split(',');
            for (int i = 0; i < raw.Length; i++)
            {
                string token = raw[i];
                if (string.IsNullOrEmpty(token))
                    continue;

                token = token.Trim();
                if (token.Length == 0)
                    continue;

                _hardBarrierTokens.Add(token);
            }
        }

        bool NameContainsHardBarrierToken(string name)
        {
            if (string.IsNullOrEmpty(name) || _hardBarrierTokens.Count == 0)
                return false;

            for (int i = 0; i < _hardBarrierTokens.Count; i++)
            {
                string token = _hardBarrierTokens[i];
                if (name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        void ApplyBlockingColliderLayers()
        {
            if (!pushBlockingCollidersToHiddenLayer)
                return;

            _restoreScratch.Clear();
            foreach (KeyValuePair<GameObject, int> kvp in _forcedBlockerOriginalLayers)
                _restoreScratch.Add(kvp.Key);

            for (int i = 0; i < _restoreScratch.Count; i++)
            {
                GameObject go = _restoreScratch[i];
                if (go == null)
                {
                    _forcedBlockerOriginalLayers.Remove(go);
                    continue;
                }

                if (!_currentRawBlockingObjects.Contains(go))
                    RestoreForcedLayer(go, "no-longer-physically-blocking");
            }

            for (int i = 0; i < _currentRawBlockingColliders.Count; i++)
            {
                Collider c = _currentRawBlockingColliders[i];
                if (!IsEligibleForForcedHiddenLayer(c))
                    continue;

                GameObject go = c.gameObject;
                if (go == null)
                    continue;

                if (!_forcedBlockerOriginalLayers.ContainsKey(go))
                {
                    _forcedBlockerOriginalLayers.Add(go, go.layer);
                    if (debugLog && logBlockerDetails)
                        MelonLogger.Msg("[CustomHiddenVolume] Force->hidden layer on '" + DescribeCollider(c) + "' oldLayer=" + go.layer + " newLayer=" + hiddenBlockerLayer);
                }

                go.layer = hiddenBlockerLayer;
            }
        }

        void RestoreForcedLayer(GameObject go, string reason)
        {
            if (go == null)
            {
                _forcedBlockerOriginalLayers.Remove(go);
                return;
            }

            int oldLayer;
            if (_forcedBlockerOriginalLayers.TryGetValue(go, out oldLayer))
            {
                int currentLayer = go.layer;
                go.layer = oldLayer;
                _forcedBlockerOriginalLayers.Remove(go);

                if (debugLog && logBlockerDetails)
                    MelonLogger.Msg("[CustomHiddenVolume] Restore layer on '" + go.name + "' reason=" + reason + " currentLayer=" + currentLayer + " restoredLayer=" + oldLayer);
            }
        }

        void RestoreAllForcedBlockerLayers()
        {
            _restoreScratch.Clear();
            foreach (KeyValuePair<GameObject, int> kvp in _forcedBlockerOriginalLayers)
                _restoreScratch.Add(kvp.Key);

            for (int i = 0; i < _restoreScratch.Count; i++)
                RestoreForcedLayer(_restoreScratch[i], "restore-all");

            _forcedBlockerOriginalLayers.Clear();
        }

        void SetHiddenState(bool hidden)
        {
            if (pickup == null)
                return;

            if (_hiddenActive == hidden)
                return;

            _hiddenActive = hidden;

            try
            {
                if (hidden)
                {
                    ApplyBlockingColliderLayers();
                    pickup.OnEnterHiddenVolume();
                }
                else
                {
                    RestoreAllForcedBlockerLayers();
                    pickup.OnExitHiddenVolume();
                }

                if (debugLog)
                    MelonLogger.Msg("[CustomHiddenVolume] '" + gameObject.name + "' hidden=" + hidden + " blockers=" + _currentBlockingColliders.Count + " forcedObjects=" + _forcedBlockerOriginalLayers.Count);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[CustomHiddenVolume] Failed to toggle hidden-volume state on '" + gameObject.name + "': " + e);
            }
        }

        void ForceVisibleState()
        {
            _hiddenTimer = 0f;
            _visibleTimer = 0f;
            ClearCurrentBlockingCache();

            if (_hiddenActive)
                SetHiddenState(false);
            else
                RestoreAllForcedBlockerLayers();
        }

        void MaybeLogState(bool visuallyObstructed, bool hardBarrierPresent, bool allowHidden)
        {
            if (!debugLog)
                return;

            string blockerSummary = BuildBlockerSummary();
            bool blockerChanged = blockerSummary != _lastBlockerSummary;
            bool stateChanged = visuallyObstructed != _lastVisuallyObstructed ||
                                hardBarrierPresent != _lastHardBarrierPresent ||
                                allowHidden != _lastAllowedHidden;

            if (logEveryCheck || stateChanged || blockerChanged)
            {
                MelonLogger.Msg(
                    "[CustomHiddenVolume] Check '" + gameObject.name +
                    "' visuallyObstructed=" + visuallyObstructed +
                    " hardBarrierPresent=" + hardBarrierPresent +
                    " allowHidden=" + allowHidden +
                    " hiddenActive=" + _hiddenActive +
                    " visibleBlockers=" + _currentBlockingColliders.Count +
                    " rawBlockers=" + _currentRawBlockingColliders.Count +
                    " forcedObjects=" + _forcedBlockerOriginalLayers.Count +
                    " summary=" + blockerSummary);
            }

            if (_hiddenActive && visuallyObstructed && _currentBlockingColliders.Count == 0 && _currentRawBlockingColliders.Count > 0)
            {
                MelonLogger.Msg("[CustomHiddenVolume] Hidden is being sustained by raw physical blockers that are currently forced to hidden layer. This is expected and prevents enter/exit flashing.");
            }

            if (visuallyObstructed && hardBarrierPresent && suppressHiddenVolumeWhenHardBarrierPresent)
            {
                if (stateChanged || blockerChanged)
                    MelonLogger.Msg("[CustomHiddenVolume] Suppressing hidden-volume because at least one current blocker looks like a hard barrier.");
            }

            _lastVisuallyObstructed = visuallyObstructed;
            _lastHardBarrierPresent = hardBarrierPresent;
            _lastAllowedHidden = allowHidden;
            _lastBlockerSummary = blockerSummary;
        }

        string BuildBlockerSummary()
        {
            if (_currentRawBlockingColliders.Count == 0)
                return "<none>";

            string s = "";
            for (int i = 0; i < _currentRawBlockingColliders.Count; i++)
            {
                Collider c = _currentRawBlockingColliders[i];
                if (c == null)
                    continue;

                if (s.Length > 0)
                    s += " | ";

                s += DescribeCollider(c);
                if (c != null && c.gameObject != null && _forcedBlockerOriginalLayers.ContainsKey(c.gameObject))
                    s += "[FORCED]";
                if (IsHardBarrier(c))
                    s += "[HARD]";
                else if (IsEligibleForForcedHiddenLayer(c))
                    s += "[SOFT]";
                else
                    s += "[SKIP]";
            }

            return s;
        }

        string DescribeCollider(Collider c)
        {
            if (c == null)
                return "<null>";

            GameObject go = c.gameObject;
            string goName = go != null ? go.name : "<null-go>";
            string rootName = (go != null && go.transform != null && go.transform.root != null) ? go.transform.root.name : "<null-root>";
            int layer = go != null ? go.layer : -1;
            return goName + "{root=" + rootName + ",layer=" + layer + ",type=" + c.GetType().Name + "}";
        }

        static Camera FindHMDCamera()
        {
            Camera[] cams = Resources.FindObjectsOfTypeAll<Camera>();
            for (int i = 0; i < cams.Length; i++)
            {
                Camera c = cams[i];
                if (c == null) continue;
                if (!c.gameObject.scene.IsValid()) continue;
                if (!c.gameObject.activeInHierarchy) continue;
                if (c.name == "HMD") return c;
            }

            return Camera.main;
        }
    }
}
