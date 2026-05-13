using System;
using MelonLoader;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public static class PoisonGasFixer
    {
        private static Material _sharedRuntimeMat;
        private static Texture2D _sharedSmokeTex;
        private static Shader _cachedShader;

        public static void ApplyAll()
        {
            try
            {
                GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
                int found = 0;
                int fixedCount = 0;

                for (int i = 0; i < all.Length; i++)
                {
                    GameObject go = all[i];
                    if (go == null) continue;
                    if (!string.Equals(go.name, "PoisonGas_Emitter", StringComparison.Ordinal)) continue;

                    found++;
                    if (ApplyTo(go))
                        fixedCount++;
                }

                MelonLogger.Msg("[PoisonGasMaterialFix] Found " + found + " emitter(s), fixed " + fixedCount + ".");
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[PoisonGasMaterialFix] ApplyAll failed: " + e);
            }
        }

        public static bool ApplyTo(GameObject go)
        {
            if (go == null) return false;

            try
            {
                ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
                if (psr == null)
                {
                    MelonLogger.Warning("[PoisonGasMaterialFix] No ParticleSystemRenderer on " + go.name);
                    return false;
                }

                Material mat = GetOrBuildSharedMaterial();
                if (mat == null)
                {
                    MelonLogger.Warning("[PoisonGasMaterialFix] Failed to build shared runtime material.");
                    return false;
                }

                RemoveDebugQuad(go);

                bool wasEnabled = false;
                try { wasEnabled = psr.enabled; } catch { }

                psr.sharedMaterial = mat;
                try { psr.enabled = wasEnabled; } catch { }

                MelonLogger.Msg("[PoisonGasMaterialFix] Applied shared shader '" + mat.shader.name + "' to '" + go.name + "'. rendererEnabledPreserved=" + wasEnabled);
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[PoisonGasMaterialFix] ApplyTo failed on '" + go.name + "': " + e);
                return false;
            }
        }

        static Material GetOrBuildSharedMaterial()
        {
            if (_sharedRuntimeMat != null)
                return _sharedRuntimeMat;

            _cachedShader = FindBestShader();
            if (_cachedShader == null)
                return null;

            _sharedSmokeTex = BuildDenseSmokeTex128();

            Material mat = new Material(_cachedShader);
            mat.name = "PoisonGas_RuntimeMat_Shared";
            mat.renderQueue = 3000;

            Color tint = new Color(0.10f, 0.22f, 0.10f, 0.42f);

            TrySetTex(mat, "_MainTex", _sharedSmokeTex);
            TrySetTex(mat, "_BaseMap", _sharedSmokeTex);
            TrySetTex(mat, "_BaseColorMap", _sharedSmokeTex);

            TrySetColor(mat, "_Color", tint);
            TrySetColor(mat, "_TintColor", tint);
            TrySetColor(mat, "_BaseColor", tint);
            try { mat.color = tint; } catch { }

            TrySetFloat(mat, "_Mode", 2f);
            TrySetFloat(mat, "_Surface", 1f);
            TrySetFloat(mat, "_Blend", 0f);
            TrySetFloat(mat, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            TrySetFloat(mat, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            TrySetFloat(mat, "_ZWrite", 0f);
            TrySetFloat(mat, "_Cull", (float)UnityEngine.Rendering.CullMode.Off);

            try { mat.EnableKeyword("_ALPHABLEND_ON"); } catch { }
            try { mat.DisableKeyword("_ALPHATEST_ON"); } catch { }
            try { mat.DisableKeyword("_ALPHAPREMULTIPLY_ON"); } catch { }

            _sharedRuntimeMat = mat;
            return _sharedRuntimeMat;
        }

        static void RemoveDebugQuad(GameObject emitter)
        {
            try
            {
                if (emitter == null) return;
                Transform existing = emitter.transform.Find("ZZZ_PoisonGas_DebugQuad");
                if (existing != null)
                    UnityEngine.Object.Destroy(existing.gameObject);
            }
            catch { }
        }

        static Shader FindBestShader()
        {
            string[] names = new string[]
            {
                "Phoenix/SH_Shared_GUIUnlitAlpha_01",
                "Phoenix_SH_Shared_GUIUnlitAlpha_01",
                "Phoenix/SH_Shared_DefaultUnlit_TransparentAdditive_VertexColor",
                "Phoenix_SH_Shared_DefaultUnlit_TransparentAdditive_VertexColor",
                "Legacy Shaders/Particles/Alpha Blended",
                "Mobile/Particles/Alpha Blended",
                "Sprites/Default"
            };

            for (int i = 0; i < names.Length; i++)
            {
                Shader s = null;
                try { s = Shader.Find(names[i]); } catch { }
                if (s != null)
                    return s;
            }

            return null;
        }

        static Texture2D BuildDenseSmokeTex128()
        {
            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (float)x / (size - 1);
                    float ny = (float)y / (size - 1);

                    float dx = nx - 0.5f;
                    float dy = ny - 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) * 2.0f;

                    float n1 = Mathf.PerlinNoise(nx * 3.0f, ny * 3.0f);
                    float n2 = Mathf.PerlinNoise(nx * 6.2f + 9.1f, ny * 6.2f + 4.8f);
                    float n3 = Mathf.PerlinNoise(nx * 11.5f + 2.7f, ny * 11.5f + 13.4f);

                    float n = (n1 * 0.45f) + (n2 * 0.35f) + (n3 * 0.20f);

                    float radial = Mathf.Clamp01(1.0f - dist);
                    radial = radial * radial * (3f - 2f * radial);
                    radial = Mathf.Pow(radial, 0.95f);

                    float a = Mathf.Clamp01(radial * (0.55f + 1.25f * n));
                    a *= 1.55f;
                    if (a > 1f) a = 1f;

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply(false, false);
            return tex;
        }

        static void TrySetTex(Material m, string prop, Texture t)
        {
            try
            {
                if (m != null && t != null && m.HasProperty(prop))
                    m.SetTexture(prop, t);
            }
            catch { }
        }

        static void TrySetColor(Material m, string prop, Color c)
        {
            try
            {
                if (m != null && m.HasProperty(prop))
                    m.SetColor(prop, c);
            }
            catch { }
        }

        static void TrySetFloat(Material m, string prop, float v)
        {
            try
            {
                if (m != null && m.HasProperty(prop))
                    m.SetFloat(prop, v);
            }
            catch { }
        }
    }
}
