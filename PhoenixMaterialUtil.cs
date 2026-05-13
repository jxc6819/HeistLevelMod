using System;
using MelonLoader;
using UnhollowerBaseLib.Attributes;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public static class PhoenixMaterialUtil
    {
        static Texture2D _goldPackedTex;

        [HideFromIl2Cpp]
        public static void ConvertToPhoenix(GameObject root, bool forceMetallic = false)
        {
            if (root == null) return;

            Shader shOpaque = FindPhoenixOpaqueShader();
            Shader shTrans = FindPhoenixTransparentShader();

            if (shOpaque == null) shOpaque = Shader.Find("Standard");
            if (shTrans == null) shTrans = shOpaque;

            Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                Renderer r = rends[i];
                if (r == null) continue;

                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;

                Material[] mats = r.sharedMaterials;
                if (mats == null) continue;

                for (int m = 0; m < mats.Length; m++)
                {
                    Material src = mats[m];
                    if (src == null) continue;

                    string oldShaderName = src.shader != null ? src.shader.name : "";
                    bool alreadyPhoenix = oldShaderName.StartsWith("Phoenix/", StringComparison.Ordinal);

                    bool transparent = IsProbablyTransparent(src);
                    Shader target = transparent ? shTrans : shOpaque;
                    if (target == null) continue;

                    Material dst = alreadyPhoenix ? src : new Material(src);
                    if (!alreadyPhoenix) dst.name = src.name + "_PhoenixRuntime";

                    Texture mainTex = FindMainTexture(src);
                    Texture normalTex = FindNormalTexture(src);
                    Color color = FindMainColor(src);

                    dst.shader = target;

                    if (mainTex != null)
                        SetBaseTextureSlots(dst, mainTex);

                    if (normalTex != null)
                        SetNormalTextureSlots(dst, normalTex);

                    SetColorEverywhere(dst, color);
                    ConfigureOpaqueOrTransparent(dst, transparent);

                    if (forceMetallic && IsGoldBodyCandidate(r, src, dst))
                        ForceGold(dst, mainTex, normalTex);

                    mats[m] = dst;
                }

                r.sharedMaterials = mats;
            }
        }

        [HideFromIl2Cpp]
        public static void ForceGoldBarPhoenix(GameObject root)
        {
            if (root == null) return;

            Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0) return;

            float largestScore = 0f;
            for (int i = 0; i < rends.Length; i++)
            {
                Renderer r = rends[i];
                if (r == null) continue;
                if (IsNeverGoldBodyRenderer(r)) continue;

                float s = RendererSizeScore(r);
                if (s > largestScore) largestScore = s;
            }

            int changed = 0;

            for (int i = 0; i < rends.Length; i++)
            {
                Renderer r = rends[i];
                if (r == null) continue;
                if (IsNeverGoldBodyRenderer(r)) continue;

                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;

                Material[] mats = r.sharedMaterials;
                if (mats == null) continue;

                bool rendererNameLooksGold = NameLooksGoldBody(r.name);
                bool rendererIsLargeBody = largestScore > 0f && RendererSizeScore(r) >= largestScore * 0.55f;

                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    if (mat == null) continue;

                    bool materialNameLooksGold = NameLooksGoldBody(mat.name);

                    if (!rendererNameLooksGold && !materialNameLooksGold && !rendererIsLargeBody)
                        continue;

                    ForceGold(mat, FindMainTexture(mat), FindNormalTexture(mat));
                    mats[m] = mat;
                    changed++;
                }

                r.sharedMaterials = mats;
            }

            MelonLogger.Msg("[PhoenixMaterialUtil] ForceGoldBarPhoenix changed material slots: " + changed);
        }

        static Shader FindPhoenixOpaqueShader()
        {
            return Shader.Find("Phoenix/SH_Shared_DefaultPBR_Metallic_Opaque_01")
                ?? Shader.Find("Phoenix/Packed/SH_Shared_PackedPBR_Metallic_Opaque_01")
                ?? Shader.Find("Phoenix/SH_Shared_DefaultPBR_Opaque_01")
                ?? Shader.Find("Phoenix/SH_Shared_DefaultPBR_Opaque");
        }

        static Shader FindPhoenixTransparentShader()
        {
            return Shader.Find("Phoenix/SH_Shared_DefaultPBR_Metallic_TransparentColorAlpha_01")
                ?? Shader.Find("Phoenix/Packed/SH_Shared_PackedPBR_Metallic_Transparent_01")
                ?? Shader.Find("Phoenix/SH_Shared_DefaultPBR_TransparentColorAlpha_01")
                ?? Shader.Find("Phoenix/SH_Shared_DefaultPBR_Transparent_01");
        }

        static bool IsProbablyTransparent(Material m)
        {
            if (m == null) return false;
            try { if (m.renderQueue >= 3000) return true; } catch { }
            try { if (m.IsKeywordEnabled("_ALPHABLEND_ON")) return true; } catch { }
            try { if (m.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON")) return true; } catch { }
            return false;
        }

        static bool IsGoldBodyCandidate(Renderer r, Material src, Material dst)
        {
            if (r != null && IsNeverGoldBodyRenderer(r)) return false;

            string all = "";
            if (r != null) all += r.name + " ";
            if (src != null) all += src.name + " ";
            if (dst != null) all += dst.name + " ";

            return NameLooksGoldBody(all);
        }

        static bool NameLooksGoldBody(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return false;
            string n = raw.ToLowerInvariant();

            if (n.Contains("text") || n.Contains("font") || n.Contains("letter") || n.Contains("word")) return false;
            if (n.Contains("logo") || n.Contains("decal") || n.Contains("sticker") || n.Contains("label")) return false;
            if (n.Contains("plaque") || n.Contains("emblem") || n.Contains("stamp") || n.Contains("print")) return false;
            if (n.Contains("paper") || n.Contains("card") || n.Contains("canvas")) return false;

            return n.Contains("gold") || n.Contains("bar") || n.Contains("bullion") || n.Contains("ingot");
        }

        static bool IsNeverGoldBodyRenderer(Renderer r)
        {
            if (r == null) return true;
            string n = r.name.ToLowerInvariant();

            if (n.Contains("text") || n.Contains("font") || n.Contains("letter") || n.Contains("word")) return true;
            if (n.Contains("logo") || n.Contains("decal") || n.Contains("sticker") || n.Contains("label")) return true;
            if (n.Contains("plaque") || n.Contains("emblem") || n.Contains("stamp") || n.Contains("print")) return true;
            if (n.Contains("paper") || n.Contains("card") || n.Contains("canvas")) return true;

            return false;
        }

        static float RendererSizeScore(Renderer r)
        {
            if (r == null) return 0f;
            try
            {
                Vector3 s = r.bounds.size;
                return Mathf.Max(0f, s.x) * Mathf.Max(0f, s.y) * Mathf.Max(0f, s.z);
            }
            catch { return 0f; }
        }

        static void ForceGold(Material m, Texture mainTex, Texture normalTex)
        {
            if (m == null) return;

            EnsureGoldTextures();

            Shader metalShader = Shader.Find("Phoenix/SH_Shared_DefaultPBR_Metallic_Opaque_01")
                ?? Shader.Find("Phoenix/Packed/SH_Shared_PackedPBR_Metallic_Opaque_01")
                ?? Shader.Find("Standard");
            if (metalShader != null) m.shader = metalShader;

            if (mainTex != null)
            {
                SetBaseTextureSlots(m, mainTex);
                SetColorEverywhere(m, Color.white);
            }
            else
            {
                SetColorEverywhere(m, new Color(1.0f, 0.76f, 0.24f, 1f));
            }

            if (normalTex != null)
                SetNormalTextureSlots(m, normalTex);

            SetFloatIfExists(m, "_Metallic", 1.0f);
            SetFloatIfExists(m, "_Metalness", 1.0f);
            SetFloatIfExists(m, "_Metallicness", 1.0f);
            SetFloatIfExists(m, "_Smoothness", 0.98f);
            SetFloatIfExists(m, "_Glossiness", 0.98f);
            SetFloatIfExists(m, "_Gloss", 0.98f);
            SetFloatIfExists(m, "_Roughness", 0.02f);
            SetFloatIfExists(m, "_Spec", 1.0f);
            SetFloatIfExists(m, "_SpecularHighlights", 1.0f);
            SetFloatIfExists(m, "_EnvironmentReflections", 1.0f);
            SetFloatIfExists(m, "_ReflectionStrength", 1.0f);
            SetFloatIfExists(m, "_ReflectionIntensity", 1.0f);

            SetColorIfExists(m, "_SpecColor", new Color(1f, 0.92f, 0.62f, 1f));
            SetColorIfExists(m, "_EmissionColor", Color.black);

            string[] packedSlots = new string[]
            {
                "_MetallicGlossMap",
                "_MetallicMap",
                "_MetalnessMap",
                "_MaskMap",
                "_PackedMap",
                "_SurfaceMap",
                "_ORMMap",
                "_PBRMap",
                "_MRAOMap",
                "_SpecGlossMap",
                "_SpecularMap",
                "_SmoothnessMap",
                "_GlossMap"
            };

            for (int i = 0; i < packedSlots.Length; i++)
                SetTextureIfExists(m, packedSlots[i], _goldPackedTex);

            try { m.EnableKeyword("_METALLICGLOSSMAP"); } catch { }
            try { m.EnableKeyword("_SPECGLOSSMAP"); } catch { }
            try { m.EnableKeyword("_MASKMAP"); } catch { }
            try { m.EnableKeyword("_PACKEDMAP"); } catch { }

            ConfigureOpaqueOrTransparent(m, false);
        }

        static void EnsureGoldTextures()
        {
            if (_goldPackedTex == null)
            {
                _goldPackedTex = MakeSolidTex("Gold_PackedMetalSmooth", new Color(1f, 1f, 1f, 0.98f));
            }
        }

        static Texture2D MakeSolidTex(string name, Color c)
        {
            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
            tex.name = name;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            Color[] px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        static Texture FindMainTexture(Material m)
        {
            if (m == null) return null;

            string[] names = new string[]
            {
                "_MainTex", "_BaseMap", "_BaseColorMap", "_BaseColorTexture", "_Albedo", "_AlbedoMap", "_Diffuse", "_DiffuseMap"
            };

            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    if (m.HasProperty(names[i]))
                    {
                        Texture t = m.GetTexture(names[i]);
                        if (t != null) return t;
                    }
                }
                catch { }
            }

            try { if (m.mainTexture != null) return m.mainTexture; } catch { }
            return null;
        }

        static Texture FindNormalTexture(Material m)
        {
            if (m == null) return null;

            string[] names = new string[]
            {
                "_BumpMap", "_NormalMap", "_Normal", "_NormalTex"
            };

            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    if (m.HasProperty(names[i]))
                    {
                        Texture t = m.GetTexture(names[i]);
                        if (t != null) return t;
                    }
                }
                catch { }
            }

            return null;
        }

        static Color FindMainColor(Material m)
        {
            if (m == null) return Color.white;

            string[] names = new string[]
            {
                "_Color", "_BaseColor", "_TintColor"
            };

            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    if (m.HasProperty(names[i])) return m.GetColor(names[i]);
                }
                catch { }
            }

            try { return m.color; } catch { }
            return Color.white;
        }

        static void SetBaseTextureSlots(Material m, Texture t)
        {
            if (m == null || t == null) return;

            string[] names = new string[]
            {
                "_MainTex", "_BaseMap", "_BaseColorMap", "_BaseColorTexture", "_Albedo", "_AlbedoMap", "_Diffuse", "_DiffuseMap"
            };

            for (int i = 0; i < names.Length; i++)
                SetTextureIfExists(m, names[i], t);

            try { m.mainTexture = t; } catch { }
        }

        static void SetNormalTextureSlots(Material m, Texture t)
        {
            if (m == null || t == null) return;

            string[] names = new string[]
            {
                "_BumpMap", "_NormalMap", "_Normal", "_NormalTex"
            };

            for (int i = 0; i < names.Length; i++)
                SetTextureIfExists(m, names[i], t);

            try { m.EnableKeyword("_NORMALMAP"); } catch { }
        }

        static void SetColorEverywhere(Material m, Color c)
        {
            if (m == null) return;

            SetColorIfExists(m, "_Color", c);
            SetColorIfExists(m, "_BaseColor", c);
            SetColorIfExists(m, "_TintColor", c);

            try { m.color = c; } catch { }
        }

        static void ConfigureOpaqueOrTransparent(Material m, bool transparent)
        {
            if (m == null) return;

            if (transparent)
            {
                SetIntIfExists(m, "_ZWrite", 0);
                SetIntIfExists(m, "_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                SetIntIfExists(m, "_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (m.HasProperty("_Mode")) SetFloatIfExists(m, "_Mode", 3f);
                m.renderQueue = 3000;
                try { m.EnableKeyword("_ALPHABLEND_ON"); } catch { }
            }
            else
            {
                SetIntIfExists(m, "_ZWrite", 1);
                SetIntIfExists(m, "_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                SetIntIfExists(m, "_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                if (m.HasProperty("_Mode")) SetFloatIfExists(m, "_Mode", 0f);
                m.renderQueue = -1;
                try { m.DisableKeyword("_ALPHABLEND_ON"); } catch { }
                try { m.DisableKeyword("_ALPHAPREMULTIPLY_ON"); } catch { }
                try { m.DisableKeyword("_ALPHATEST_ON"); } catch { }
            }
        }

        static void SetTextureIfExists(Material m, string name, Texture t)
        {
            try
            {
                if (m != null && t != null && m.HasProperty(name))
                    m.SetTexture(name, t);
            }
            catch { }
        }

        static void SetFloatIfExists(Material m, string name, float v)
        {
            try
            {
                if (m != null && m.HasProperty(name))
                    m.SetFloat(name, v);
            }
            catch { }
        }

        static void SetIntIfExists(Material m, string name, int v)
        {
            try
            {
                if (m != null && m.HasProperty(name))
                    m.SetInt(name, v);
            }
            catch { }
        }

        static void SetColorIfExists(Material m, string name, Color c)
        {
            try
            {
                if (m != null && m.HasProperty(name))
                    m.SetColor(name, c);
            }
            catch { }
        }
    }
}
