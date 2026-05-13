using MelonLoader;
using System;
using System.Collections.Generic;
using UnhollowerRuntimeLib;
using UnityEngine;
using SG.Phoenix.Assets.Code;
using SG.Phoenix.Assets.Code.Interactables;

namespace IEYTD_Mod2Code
{
    public class SuitcaseColliderProbe : MonoBehaviour
    {
        public SuitcaseColliderProbe(IntPtr ptr) : base(ptr) { }
        public SuitcaseColliderProbe() : base(ClassInjector.DerivedConstructorPointer<SuitcaseColliderProbe>())
            => ClassInjector.DerivedConstructorBody(this);

        public Transform suitcaseRoot;
        public string suitcaseNameHint = "NuclearFootball";

        public bool autoFindRoot = true;
        public bool logOnStart = true;
        public bool logHierarchy = true;
        public bool logColliders = true;
        public bool logPickUps = true;
        public bool logRotationalMotions = true;
        public bool logButtons = true;
        public bool logAnimatorInfo = true;
        public bool logOnlyWhenTriggered = false;

        public bool drawDebug = true;
        public float drawDuration = 20f;

        public string[] interestingNameHints = new string[]
        {
            "Latch",
            "Button",
            "ShellButton",
            "Handle",
            "Clip",
            "Lid",
            "Hinge",
            "Visual + Collision",
            "Collision",
            "Rot",
            "Confirm",
            "Encryption",
            "Targeting"
        };

        private readonly List<Collider> _colliders = new List<Collider>();
        private readonly List<PickUp> _pickUps = new List<PickUp>();
        private readonly List<RotationalMotion> _rotMotions = new List<RotationalMotion>();
        private readonly List<MonoBehaviour> _buttonLikeBehaviours = new List<MonoBehaviour>();
        private readonly HashSet<string> _loggedComponentTypes = new HashSet<string>();
        private bool _hasDumped;

        void Start()
        {
            TryResolveRoot();

            if (logOnStart && !logOnlyWhenTriggered)
                DumpEverything("Start");
        }

        void Update()
        {
            if (!_hasDumped && logOnlyWhenTriggered && Input.GetKeyDown(KeyCode.F8))
                DumpEverything("F8");

            if (Input.GetKeyDown(KeyCode.F7))
                DumpEverything("F7");
        }

        public void DumpEverything(string reason)
        {
            TryResolveRoot();
            if (suitcaseRoot == null)
            {
                MelonLogger.Warning("[SuitcaseColliderProbe] No suitcase root found. suitcaseNameHint=" + suitcaseNameHint);
                return;
            }

            _hasDumped = true;
            _colliders.Clear();
            _pickUps.Clear();
            _rotMotions.Clear();
            _buttonLikeBehaviours.Clear();
            _loggedComponentTypes.Clear();

            CollectAll();

            MelonLogger.Msg("[SuitcaseColliderProbe] ========================================");
            MelonLogger.Msg("[SuitcaseColliderProbe] Dump reason=" + reason);
            MelonLogger.Msg("[SuitcaseColliderProbe] Root path=" + GetPath(suitcaseRoot));
            MelonLogger.Msg("[SuitcaseColliderProbe] worldPos=" + suitcaseRoot.position + " worldRot=" + suitcaseRoot.rotation.eulerAngles + " lossyScale=" + suitcaseRoot.lossyScale);
            MelonLogger.Msg("[SuitcaseColliderProbe] Counts colliders=" + _colliders.Count + " pickups=" + _pickUps.Count + " rotMotions=" + _rotMotions.Count + " buttonLikes=" + _buttonLikeBehaviours.Count);

            if (logPickUps)
                DumpPickUps();
            if (logRotationalMotions)
                DumpRotMotions();
            if (logColliders)
                DumpColliders();
            if (logButtons)
                DumpButtonLikes();
            if (logAnimatorInfo)
                DumpAnimators();
            if (logHierarchy)
                DumpInterestingHierarchy();

            MelonLogger.Msg("[SuitcaseColliderProbe] ========================================");
        }

        private void TryResolveRoot()
        {
            if (suitcaseRoot != null)
                return;

            if (!autoFindRoot)
                return;

            GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
            if (all == null)
                return;

            for (int i = 0; i < all.Length; i++)
            {
                GameObject go = all[i];
                if (go == null)
                    continue;

                string name = SafeName(go.transform);
                if (string.IsNullOrEmpty(name))
                    continue;

                if (name.IndexOf(suitcaseNameHint, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    suitcaseRoot = go.transform;
                    MelonLogger.Msg("[SuitcaseColliderProbe] Auto-found suitcase root: " + GetPath(suitcaseRoot));
                    return;
                }
            }
        }

        private void CollectAll()
        {
            Collider[] cols = suitcaseRoot.GetComponentsInChildren<Collider>(true);
            PickUp[] pus = suitcaseRoot.GetComponentsInChildren<PickUp>(true);
            RotationalMotion[] rms = suitcaseRoot.GetComponentsInChildren<RotationalMotion>(true);
            MonoBehaviour[] monos = suitcaseRoot.GetComponentsInChildren<MonoBehaviour>(true);

            if (cols != null)
                _colliders.AddRange(cols);
            if (pus != null)
                _pickUps.AddRange(pus);
            if (rms != null)
                _rotMotions.AddRange(rms);

            if (monos != null)
            {
                for (int i = 0; i < monos.Length; i++)
                {
                    MonoBehaviour mb = monos[i];
                    if (mb == null)
                        continue;

                    string typeName = mb.GetType().Name;
                    if (LooksButtonLike(typeName) || LooksButtonLike(SafeName(mb.transform)))
                        _buttonLikeBehaviours.Add(mb);
                }
            }
        }

        private void DumpPickUps()
        {
            for (int i = 0; i < _pickUps.Count; i++)
            {
                PickUp pu = _pickUps[i];
                if (pu == null)
                    continue;

                string held = "unknown";
                try { held = pu.isHeld.ToString(); } catch { }

                MelonLogger.Msg("[SuitcaseColliderProbe] PickUp[" + i + "] path=" + GetPath(pu.transform) +
                    " enabled=" + pu.enabled +
                    " activeInHierarchy=" + pu.gameObject.activeInHierarchy +
                    " isHeld=" + held +
                    " componentType=" + pu.GetType().FullName);

                DumpComponentsOnObject(pu.transform, "PickUpHost");
            }
        }

        private void DumpRotMotions()
        {
            for (int i = 0; i < _rotMotions.Count; i++)
            {
                RotationalMotion rm = _rotMotions[i];
                if (rm == null)
                    continue;

                string rot = "?";
                try { rot = rm._currentRotation.ToString("F3"); } catch { }

                MelonLogger.Msg("[SuitcaseColliderProbe] RotMotion[" + i + "] path=" + GetPath(rm.transform) +
                    " enabled=" + rm.enabled +
                    " activeInHierarchy=" + rm.gameObject.activeInHierarchy +
                    " _currentRotation=" + rot +
                    " componentType=" + rm.GetType().FullName);

                DumpComponentsOnObject(rm.transform, "RotMotionHost");
            }
        }

        private void DumpColliders()
        {
            for (int i = 0; i < _colliders.Count; i++)
            {
                Collider c = _colliders[i];
                if (c == null)
                    continue;

                string extra = BuildColliderExtra(c);
                Bounds b = c.bounds;
                string boundsStr = "boundsCenter=" + b.center + " boundsSize=" + b.size;

                MelonLogger.Msg("[SuitcaseColliderProbe] Collider[" + i + "] path=" + GetPath(c.transform) +
                    " type=" + c.GetType().Name +
                    " enabled=" + c.enabled +
                    " trigger=" + c.isTrigger +
                    " attachedRB=" + (c.attachedRigidbody != null ? c.attachedRigidbody.name : "NULL") +
                    " " + extra +
                    " " + boundsStr);

                if (drawDebug)
                    DrawCollider(c);

                DumpComponentsOnObject(c.transform, "ColliderHost");
            }
        }

        private void DumpButtonLikes()
        {
            for (int i = 0; i < _buttonLikeBehaviours.Count; i++)
            {
                MonoBehaviour mb = _buttonLikeBehaviours[i];
                if (mb == null)
                    continue;

                MelonLogger.Msg("[SuitcaseColliderProbe] ButtonLike[" + i + "] path=" + GetPath(mb.transform) +
                    " type=" + mb.GetType().FullName +
                    " enabled=" + mb.enabled +
                    " activeInHierarchy=" + mb.gameObject.activeInHierarchy);

                DumpComponentsOnObject(mb.transform, "ButtonLikeHost");
            }
        }

        private void DumpAnimators()
        {
            Animator[] anims = suitcaseRoot.GetComponentsInChildren<Animator>(true);
            Animation[] legacyAnims = suitcaseRoot.GetComponentsInChildren<Animation>(true);

            for (int i = 0; i < anims.Length; i++)
            {
                Animator a = anims[i];
                if (a == null)
                    continue;

                MelonLogger.Msg("[SuitcaseColliderProbe] Animator[" + i + "] path=" + GetPath(a.transform) +
                    " enabled=" + a.enabled +
                    " runtimeController=" + (a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "NULL") +
                    " applyRootMotion=" + a.applyRootMotion +
                    " updateMode=" + a.updateMode +
                    " cullingMode=" + a.cullingMode);
            }

            for (int i = 0; i < legacyAnims.Length; i++)
            {
                Animation a = legacyAnims[i];
                if (a == null)
                    continue;

                MelonLogger.Msg("[SuitcaseColliderProbe] LegacyAnimation[" + i + "] path=" + GetPath(a.transform) +
                    " enabled=" + a.enabled +
                    " clip=" + (a.clip != null ? a.clip.name : "NULL") +
                    " playAutomatically=" + a.playAutomatically);
            }
        }

        private void DumpInterestingHierarchy()
        {
            Transform[] all = suitcaseRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null)
                    continue;

                string name = SafeName(t);
                if (!IsInteresting(name))
                    continue;

                MelonLogger.Msg("[SuitcaseColliderProbe] Interesting path=" + GetPath(t) +
                    " localPos=" + t.localPosition +
                    " localRot=" + t.localEulerAngles +
                    " localScale=" + t.localScale +
                    " childCount=" + t.childCount);

                DumpComponentsOnObject(t, "InterestingHost");
            }
        }

        private void DumpComponentsOnObject(Transform t, string context)
        {
            if (t == null)
                return;

            Component[] comps = t.GetComponents<Component>();
            if (comps == null)
                return;

            for (int i = 0; i < comps.Length; i++)
            {
                Component comp = comps[i];
                if (comp == null)
                    continue;

                string key = context + "|" + GetPath(t) + "|" + comp.GetType().FullName;
                if (_loggedComponentTypes.Contains(key))
                    continue;
                _loggedComponentTypes.Add(key);

                MelonLogger.Msg("[SuitcaseColliderProbe] " + context + " component path=" + GetPath(t) +
                    " type=" + comp.GetType().FullName);
            }
        }

        private string BuildColliderExtra(Collider c)
        {
            BoxCollider bc = c as BoxCollider;
            if (bc != null)
                return "center=" + bc.center + " size=" + bc.size;

            SphereCollider sc = c as SphereCollider;
            if (sc != null)
                return "center=" + sc.center + " radius=" + sc.radius;

            CapsuleCollider cc = c as CapsuleCollider;
            if (cc != null)
                return "center=" + cc.center + " radius=" + cc.radius + " height=" + cc.height + " direction=" + cc.direction;

            MeshCollider mc = c as MeshCollider;
            if (mc != null)
                return "convex=" + mc.convex + " sharedMesh=" + (mc.sharedMesh != null ? mc.sharedMesh.name : "NULL");

            return "";
        }

        private bool LooksButtonLike(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            string lower = value.ToLowerInvariant();
            return lower.Contains("button") ||
                   lower.Contains("latch") ||
                   lower.Contains("clip") ||
                   lower.Contains("lever") ||
                   lower.Contains("handle") ||
                   lower.Contains("rotational") ||
                   lower.Contains("rotation") ||
                   lower.Contains("confirm");
        }

        private bool IsInteresting(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            for (int i = 0; i < interestingNameHints.Length; i++)
            {
                string hint = interestingNameHints[i];
                if (string.IsNullOrEmpty(hint))
                    continue;

                if (name.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private string SafeName(Transform t)
        {
            try { return t != null ? t.name : "NULL"; }
            catch { return "<name threw>"; }
        }

        private string GetPath(Transform t)
        {
            if (t == null)
                return "NULL";

            string path = SafeName(t);
            Transform p = t.parent;
            int guard = 0;
            while (p != null && guard < 256)
            {
                path = SafeName(p) + "/" + path;
                p = p.parent;
                guard++;
            }
            return path;
        }

        private void DrawCollider(Collider c)
        {
            if (c == null)
                return;

            Bounds b = c.bounds;
            Vector3 center = b.center;
            Vector3 ext = b.extents;

            Vector3 p000 = center + new Vector3(-ext.x, -ext.y, -ext.z);
            Vector3 p001 = center + new Vector3(-ext.x, -ext.y, ext.z);
            Vector3 p010 = center + new Vector3(-ext.x, ext.y, -ext.z);
            Vector3 p011 = center + new Vector3(-ext.x, ext.y, ext.z);
            Vector3 p100 = center + new Vector3(ext.x, -ext.y, -ext.z);
            Vector3 p101 = center + new Vector3(ext.x, -ext.y, ext.z);
            Vector3 p110 = center + new Vector3(ext.x, ext.y, -ext.z);
            Vector3 p111 = center + new Vector3(ext.x, ext.y, ext.z);

            Debug.DrawLine(p000, p001, Color.yellow, drawDuration);
            Debug.DrawLine(p000, p010, Color.yellow, drawDuration);
            Debug.DrawLine(p000, p100, Color.yellow, drawDuration);
            Debug.DrawLine(p001, p011, Color.yellow, drawDuration);
            Debug.DrawLine(p001, p101, Color.yellow, drawDuration);
            Debug.DrawLine(p010, p011, Color.yellow, drawDuration);
            Debug.DrawLine(p010, p110, Color.yellow, drawDuration);
            Debug.DrawLine(p100, p101, Color.yellow, drawDuration);
            Debug.DrawLine(p100, p110, Color.yellow, drawDuration);
            Debug.DrawLine(p011, p111, Color.yellow, drawDuration);
            Debug.DrawLine(p101, p111, Color.yellow, drawDuration);
            Debug.DrawLine(p110, p111, Color.yellow, drawDuration);
        }
    }
}
