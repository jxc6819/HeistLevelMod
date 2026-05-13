using System;
using MelonLoader;
using SG.Phoenix.Assets.Code.Interactables;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class DroneDriver : MonoBehaviour
    {
        public DroneDriver(IntPtr p) : base(p) { }
        public DroneDriver() : base(ClassInjector.DerivedConstructorPointer<DroneDriver>())
            => ClassInjector.DerivedConstructorBody(this);

        public string EyeRendererExactName = "SM_glassEye_low";
        public string PhoenixGlassShaderName = "Phoenix/Packed/SH_Shared_PackedPBR_Metallic_Transparent_01";

        public Color EyeTint = new Color(0.05f, 0.60f, 0.85f, 0.58f);
        public float EyeSmoothness = 0.98f;
        public float EyeMetallic = 0.08f;
        public float EyeSpecBoost = 1.25f;
        public Color EyeEmission = new Color(0.03f, 0.15f, 0.22f, 1f);
        public bool EyeDisableShadows = true;

        public float HoverHeight = 0.7f;
        public float HoverSpring = 30f;
        public float HoverDamping = 12f;
        public float MaxUpAccel = 60f;

        public float HorizontalDamp = 0.88f;
        public float AngularDamp = 0.88f;

        public float BobAmplitude = 0.015f;
        public float BobSpeed = 1f;

        public float LevelingStrength = 6f;
        public float YawSpinDegPerSec = 0f;

        public float GroundRayLength = 6.0f;
        public LayerMask GroundMask = ~0;
        public float MinAcceptableUpDot = 0.35f;

        PickUp pickup;
        Rigidbody rb;
        MeshRenderer eyeRenderer;
        Material eyeMaterial;
        Collider[] selfColliders;

        bool ready;
        bool wasHeld;

        void Awake()
        {
            pickup = GetComponentInParent<PickUp>();
            rb = GetComponent<Rigidbody>() ?? GetComponentInParent<Rigidbody>();
            selfColliders = GetComponentsInChildren<Collider>(true);

            FixEyeGlass(true);
        }

        void Start()
        {
            pickup = pickup ?? GetComponentInParent<PickUp>();
            rb = rb ?? GetComponentInParent<Rigidbody>();

            ready = true;
            wasHeld = IsHeld();

            rb.useGravity = false;
        }

        void OnDestroy()
        {
            if (eyeMaterial != null)
                Destroy(eyeMaterial);
        }

        void Update()
        {
            if (eyeRenderer == null || eyeRenderer.sharedMaterial == null)
                FixEyeGlass(false);
        }

        void FixedUpdate()
        {
            if (!ready || rb == null)
                return;

            bool held = IsHeld();

            if (wasHeld && !held)
            {
                Vector3 velocity = rb.velocity;
                velocity.y = 0f;
                rb.velocity = velocity;

                rb.useGravity = false;
                rb.isKinematic = false;
            }

            wasHeld = held;

            if (held)
                return;

            ApplyDrag();
            Hover();
            LevelOut();
            IdleYaw();
        }

        bool IsHeld()
        {
            return pickup != null && pickup.isHeld;
        }

        void ApplyDrag()
        {
            rb.useGravity = false;

            if (rb.isKinematic)
                rb.isKinematic = false;

            Vector3 velocity = rb.velocity;
            velocity.x *= HorizontalDamp;
            velocity.z *= HorizontalDamp;
            rb.velocity = velocity;

            rb.angularVelocity *= AngularDamp;
        }

        void Hover()
        {
            Vector3 origin = rb.worldCenterOfMass + Vector3.up * 0.10f;

            if (!TryGetGroundHit(origin, out RaycastHit hit))
            {
                Vector3 velocity = rb.velocity;
                velocity.y *= 0.96f;
                rb.velocity = velocity;
                return;
            }

            float bob = Mathf.Sin(Time.time * BobSpeed * 6.283185307f) * BobAmplitude;
            float desiredDistance = Mathf.Max(0.02f, HoverHeight + bob);
            float error = desiredDistance - hit.distance;
            float verticalVelocity = Vector3.Dot(rb.velocity, Vector3.up);

            float accel = error * HoverSpring - verticalVelocity * HoverDamping;
            rb.AddForce(Vector3.up * Mathf.Clamp(accel, -MaxUpAccel, MaxUpAccel), ForceMode.Acceleration);
        }

        bool TryGetGroundHit(Vector3 origin, out RaycastHit bestHit)
        {
            bestHit = default;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, GroundRayLength, GroundMask, QueryTriggerInteraction.Ignore);

            float bestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];

                if (IsSelfCollider(hit.collider))
                    continue;

                float upDot = Vector3.Dot(hit.normal.normalized, Vector3.up);
                if (upDot < MinAcceptableUpDot)
                    continue;

                if (hit.distance >= bestDistance)
                    continue;

                bestDistance = hit.distance;
                bestHit = hit;
                found = true;
            }

            return found;
        }

        bool IsSelfCollider(Collider collider)
        {
            for (int i = 0; i < selfColliders.Length; i++)
            {
                if (selfColliders[i] == collider)
                    return true;
            }

            return false;
        }

        void LevelOut()
        {
            if (LevelingStrength <= 0.01f)
                return;

            Vector3 euler = rb.rotation.eulerAngles;
            Quaternion target = Quaternion.Euler(0f, euler.y, 0f);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, target, LevelingStrength * Time.fixedDeltaTime));
        }

        void IdleYaw()
        {
            if (Mathf.Abs(YawSpinDegPerSec) < 0.001f)
                return;

            Quaternion turn = Quaternion.Euler(0f, YawSpinDegPerSec * Time.fixedDeltaTime, 0f);
            rb.MoveRotation(rb.rotation * turn);
        }

        void FixEyeGlass(bool logResult)
        {
            eyeRenderer = eyeRenderer ?? FindEyeRenderer(EyeRendererExactName);

            if (eyeRenderer == null)
            {
                if (logResult)
                    MelonLogger.Warning($"[DroneDriver] Could not find eye renderer '{EyeRendererExactName}'.");
                return;
            }

            Shader glassShader = Shader.Find(PhoenixGlassShaderName) ?? Shader.Find("Standard");
            if (glassShader == null)
                return;

            eyeRenderer.enabled = true;

            if (EyeDisableShadows)
            {
                eyeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                eyeRenderer.receiveShadows = false;
            }

            Material oldMaterial = eyeRenderer.sharedMaterial;

            if (eyeMaterial != null)
                Destroy(eyeMaterial);

            eyeMaterial = oldMaterial != null ? new Material(oldMaterial) : new Material(glassShader);
            eyeMaterial.shader = glassShader;

            if (oldMaterial != null && oldMaterial.mainTexture != null)
                eyeMaterial.mainTexture = oldMaterial.mainTexture;

            ConfigureGlass(eyeMaterial, EyeTint, EyeSmoothness, EyeMetallic, EyeSpecBoost, EyeEmission);

            eyeMaterial.renderQueue = 3000;
            eyeRenderer.material = eyeMaterial;
            eyeRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
            eyeRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;

            if (logResult)
                MelonLogger.Msg($"[DroneDriver] Eye glass fixed on '{eyeRenderer.name}' using '{eyeMaterial.shader.name}'.");
        }

        MeshRenderer FindEyeRenderer(string exactName)
        {
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (string.Equals(renderers[i].name, exactName, StringComparison.Ordinal))
                    return renderers[i];
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (string.Equals(renderers[i].name, exactName, StringComparison.OrdinalIgnoreCase))
                    return renderers[i];
            }

            return null;
        }

        static void ConfigureGlass(Material material, Color tint, float smoothness, float metallic, float specBoost, Color emission)
        {
            SetColor(material, "_Color", tint);
            SetColor(material, "_BaseColor", tint);
            SetColor(material, "_TintColor", tint);

            SetFloat(material, "_Smoothness", smoothness);
            SetFloat(material, "_Glossiness", smoothness);
            SetFloat(material, "_Metallic", metallic);
            SetFloat(material, "_SpecularHighlights", 1f);
            SetFloat(material, "_Spec", specBoost);
            SetColor(material, "_SpecColor", Color.white);

            material.EnableKeyword("_EMISSION");
            SetColor(material, "_EmissionColor", emission);

            SetFloat(material, "_Mode", 3f);
            SetInt(material, "_ZWrite", 0);
            SetInt(material, "_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            SetInt(material, "_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
        }

        static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }

        static void SetInt(Material material, string property, int value)
        {
            if (material.HasProperty(property))
                material.SetInt(property, value);
        }

        static void SetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property))
                material.SetColor(property, value);
        }
    }
}
