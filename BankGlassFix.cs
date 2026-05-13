using System;
using MelonLoader;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public static class BankGlassFix
    {
        static Texture2D _sharedWhiteTex;
        static Material _sharedBankGlassMat;

        static readonly Color BankGlassColor = new Color(
            42f / 255f,
            68f / 255f,
            84f / 255f,
            138f / 255f
        );

        public static void Apply()
        {
            MeshRenderer[] renderers = Resources.FindObjectsOfTypeAll<MeshRenderer>();
            if (renderers == null || renderers.Length == 0)
            {
                MelonLogger.Warning("[BankGlassFix] No MeshRenderers found.");
                return;
            }

            Material runtimeMat = GetOrCreateSharedBankGlassMat();
            if (runtimeMat == null)
            {
                MelonLogger.Warning("[BankGlassFix] Runtime material creation failed.");
                return;
            }

            int rendererCount = 0;
            int slotCount = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer r = renderers[i];
                if (r == null) continue;

                Material[] mats = null;
                try { mats = r.sharedMaterials; } catch { }
                if (mats == null || mats.Length == 0) continue;

                bool changed = false;

                for (int m = 0; m < mats.Length; m++)
                {
                    Material src = mats[m];
                    if (src == null) continue;

                    string matName = src.name ?? "";
                    if (!matName.StartsWith("MAT_bank_glass_01", StringComparison.OrdinalIgnoreCase))
                        continue;

                    mats[m] = runtimeMat;
                    changed = true;
                    slotCount++;
                }

                if (!changed) continue;

                try { r.sharedMaterials = mats; } catch { }
                try { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; } catch { }
                try { r.receiveShadows = false; } catch { }
                try { r.enabled = true; } catch { }

                rendererCount++;
            }

            MelonLogger.Msg("[BankGlassFix] Applied bank glass mat to " + slotCount + " slot(s) across " + rendererCount + " renderer(s).");
        }

        static Material GetOrCreateSharedBankGlassMat()
        {
            if (_sharedBankGlassMat != null)
                return _sharedBankGlassMat;

            Shader glassShader = Shader.Find("Phoenix/SH_Shared_GUIUnlitAlpha_01");
            if (glassShader == null)
            {
                MelonLogger.Warning("[BankGlassFix] Shader not found: Phoenix/SH_Shared_GUIUnlitAlpha_01");
                return null;
            }

            _sharedBankGlassMat = new Material(glassShader);
            _sharedBankGlassMat.name = "BankGlass_RuntimeMat";

            Texture2D glassTex = GetOrCreateWhiteTex();
            _sharedBankGlassMat.SetTexture("_MainTex", glassTex);

            _sharedBankGlassMat.color = BankGlassColor;
            if (_sharedBankGlassMat.HasProperty("_Color"))
                _sharedBankGlassMat.SetColor("_Color", BankGlassColor);

            if (_sharedBankGlassMat.HasProperty("_TintColor"))
                _sharedBankGlassMat.SetColor("_TintColor", BankGlassColor);

            _sharedBankGlassMat.renderQueue = 3000;
            _sharedBankGlassMat.hideFlags = HideFlags.DontUnloadUnusedAsset;

            return _sharedBankGlassMat;
        }

        static Texture2D GetOrCreateWhiteTex()
        {
            if (_sharedWhiteTex != null)
                return _sharedWhiteTex;

            int w = 64;
            int h = 64;

            _sharedWhiteTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            _sharedWhiteTex.name = "BankGlass_FakeGlassTex";

            for (int y = 0; y < h; y++)
            {
                float fy = y / 63f;

                for (int x = 0; x < w; x++)
                {
                    float fx = x / 63f;

                    float baseV = 0.84f;
                    float grad = Mathf.Lerp(0.05f, -0.03f, fy);

                    float band1 = Mathf.Exp(-Mathf.Pow((fx - 0.22f) / 0.08f, 2f)) * 0.16f;
                    float band2 = Mathf.Exp(-Mathf.Pow((fx - 0.73f) / 0.06f, 2f)) * 0.10f;

                    float diag = Mathf.Sin((fx * 1.35f + fy * 0.85f) * 6.2831853f) * 0.015f;

                    float v = Mathf.Clamp01(baseV + grad + band1 + band2 + diag);

                    Color c = new Color(
                        Mathf.Clamp01(v * 0.93f),
                        Mathf.Clamp01(v * 0.97f),
                        Mathf.Clamp01(v * 1.02f),
                        1f
                    );

                    _sharedWhiteTex.SetPixel(x, y, c);
                }
            }

            _sharedWhiteTex.Apply(false, false);
            _sharedWhiteTex.wrapMode = TextureWrapMode.Clamp;
            _sharedWhiteTex.filterMode = FilterMode.Bilinear;
            _sharedWhiteTex.hideFlags = HideFlags.DontUnloadUnusedAsset;

            return _sharedWhiteTex;
        }
    }
}
