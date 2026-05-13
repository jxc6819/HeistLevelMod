using MelonLoader;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using SG.Phoenix.Assets.Code.Interactables;
using UnhollowerBaseLib;
using UnhollowerBaseLib.Attributes;
using UnhollowerRuntimeLib;

namespace IEYTD_Mod2Code
{
    public static class VanSceneManager
    {

        static Texture _defaultObjectiveTexture;
        static Vector2 _defaultObjectiveOffset;
        static Vector2 _defaultObjectiveScale;
        static bool _defaultObjectiveCached = false;

        static Sprite _defaultSouvenirSprite;
        static Sprite _defaultSouvenirOverrideSprite;
        static bool _defaultSouvenirCached = false;

        static Type _vanPickUpManaged;
        static Type _vanShakeManaged;
        static Il2CppSystem.Type _vanPickUpIl2;
        static Il2CppSystem.Type _vanShakeIl2;

        static readonly string[] VanDonorExactNames =
        {
            "P_Van_INT_FastFoodCup_01",
            "P_Van_INT_FastFoodCup_01 (1)",
            "P_Van_INT_FastFoodCup_01 (2)",
            "P_Van_INT_FastFoodCup_01 (3)",
            "P_Van_INT_FastFoodCup_01 (4)",
            "P_Van_INT_FastFoodCup_01 (5)",
            "P_Van_INT_FastFoodCup_01 (6)",
            "P_Van_INT_FastFoodCup_01 (7)",
            "P_Van_INT_FastFoodCup_01 (8)",
            "P_Van_INT_FastFoodCup_01 (9)"
        };

        public static void OnFreshVanLoaded()
        {
            MelonLogger.Msg("[VanSceneManager] Fresh Van Loaded");
            CacheDefaultVanScreenState();
            UpdateSouvenirNames();
            UpdateSouvenirIcons();
            UpdateSpeedRunDisplay();

            createCassetteTape();
            UpdateSouvenirObjects();
        }
        public static void ReplaceScreenTexture()
        {
            SetObjectiveScreenTextureFromBundle("OperationBlindspotBriefing.png");
        }

        public static void SetSouvenirScreen()
        {
            if (!HasCompletedCustomLevel())
            {
                SetSouvenirBackgroundFromBundle("UI_Van_Souvenir_BeforeCompletion.png");
                SetSouvenirStaticOverlay("UI_Van_Souvenir_BeforeCompletion.png", true);
                FireSouvenirScreenShowEvents();
                MelonLogger.Msg("[VanSceneManager] - Souvenir screen set to before-completion static PNG.");
                return;
            }

            SetSouvenirStaticOverlay(null, false);

            SetSouvenirBackgroundFromBundle("UI_Van_SouvenirScreen.png");
            MelonLogger.Msg("[VanSceneManager] - Souvenir Screen Set");

            RefreshSouvenirNames();
            UpdateSpeedRunDisplay();

            FireSouvenirScreenShowEvents();

            Texture2D souvenirUnlocked = HeistBundle2Manager.GetTexture("Souvenir_Unlocked.png");
            Texture2D souvenirLocked = HeistBundle2Manager.GetTexture("Souvenir_Locked.png");
        }

        static bool HasCompletedCustomLevel()
        {
            try
            {
                if (SaveManager.Current == null)
                    SaveManager.Load();

                return SaveManager.Current != null && SaveManager.Current.LevelComplete;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[VanSceneManager] Could not read LevelComplete. Treating level as incomplete: " + ex.Message);
                return false;
            }
        }

        static void SetObjectiveScreenTextureFromBundle(string textureName)
        {
            Texture2D tex = HeistBundle2Manager.GetTexture(textureName);
            if (tex == null)
            {
                MelonLogger.Warning("[VanSceneManager] Missing objective screen texture in bundle: " + textureName);
                return;
            }

            GameObject screen = GameObject.Find("SM_Van_INT_ObjectiveMonitorScreen_02");
            if (screen == null)
            {
                MelonLogger.Warning("[VanSceneManager] Objective monitor object not found.");
                return;
            }

            MeshRenderer mr = screen.GetComponent<MeshRenderer>();
            if (mr == null || mr.material == null)
            {
                MelonLogger.Warning("[VanSceneManager] Objective monitor MeshRenderer/material not found.");
                return;
            }

            mr.material.mainTexture = tex;
            mr.material.mainTextureOffset = new Vector2(0, 0);
            mr.material.mainTextureScale = new Vector2(1, 1);
        }

        static void SetSouvenirBackgroundFromBundle(string textureName)
        {
            Texture2D tex = HeistBundle2Manager.GetTexture(textureName);
            if (tex == null)
            {
                MelonLogger.Warning("[VanSceneManager] Missing souvenir screen texture in bundle: " + textureName);
                return;
            }

            GameObject bgObj = GameObject.Find("SouvenirScreen Background");
            if (bgObj == null)
            {
                MelonLogger.Warning("[VanSceneManager] SouvenirScreen Background not found.");
                return;
            }

            Image img = bgObj.GetComponent<Image>();
            if (img == null)
            {
                MelonLogger.Warning("[VanSceneManager] SouvenirScreen Background has no Image component.");
                return;
            }

            Sprite sp = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f
            );

            img.sprite = sp;
            img.overrideSprite = sp;
        }

        static void SetSouvenirStaticOverlay(string textureName, bool visible)
        {
            GameObject bgObj = GameObject.Find("SouvenirScreen Background");
            if (bgObj == null)
            {
                MelonLogger.Warning("[VanSceneManager] Cannot set souvenir static overlay because SouvenirScreen Background was not found.");
                return;
            }

            Transform parent = bgObj.transform.parent;
            if (parent == null)
            {
                MelonLogger.Warning("[VanSceneManager] Cannot set souvenir static overlay because background has no parent.");
                return;
            }

            Transform existing = parent.Find("Mod_StaticSouvenirScreenOverlay");
            GameObject overlay = existing != null ? existing.gameObject : null;

            if (!visible)
            {
                if (overlay != null) overlay.SetActive(false);
                return;
            }

            Texture2D tex = HeistBundle2Manager.GetTexture(textureName);
            if (tex == null)
            {
                MelonLogger.Warning("[VanSceneManager] Missing souvenir static overlay texture in bundle: " + textureName);
                return;
            }

            if (overlay == null)
            {
                overlay = GameObject.Instantiate(bgObj);
                overlay.name = "Mod_StaticSouvenirScreenOverlay";
                overlay.transform.SetParent(parent, false);

                RectTransform bgRt = bgObj.GetComponent<RectTransform>();
                RectTransform rt = overlay.GetComponent<RectTransform>();
                if (bgRt != null && rt != null)
                {
                    rt.anchorMin = bgRt.anchorMin;
                    rt.anchorMax = bgRt.anchorMax;
                    rt.anchoredPosition = bgRt.anchoredPosition;
                    rt.sizeDelta = bgRt.sizeDelta;
                    rt.pivot = bgRt.pivot;
                    rt.localRotation = bgRt.localRotation;
                    rt.localScale = bgRt.localScale;
                    rt.offsetMin = bgRt.offsetMin;
                    rt.offsetMax = bgRt.offsetMax;
                }
                else
                {
                    overlay.transform.localPosition = bgObj.transform.localPosition;
                    overlay.transform.localRotation = bgObj.transform.localRotation;
                    overlay.transform.localScale = bgObj.transform.localScale;
                }

                Image overlayImage = overlay.GetComponent<Image>();
                if (overlayImage == null) overlayImage = overlay.AddComponent<Image>();
                overlayImage.raycastTarget = false;
            }

            Image img = overlay.GetComponent<Image>();
            if (img == null) img = overlay.AddComponent<Image>();

            Sprite sp = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f
            );

            img.sprite = sp;
            img.overrideSprite = sp;
            img.raycastTarget = false;

            overlay.transform.SetAsLastSibling();
            overlay.SetActive(true);
        }

        static void FireSouvenirScreenShowEvents()
        {

            GameObject srs = GameObject.Find("SouvenirRenderSource");
            if (srs != null)
            {
                SG.GlobalEvents.GlobalEventListener[] events = srs.GetComponents<SG.GlobalEvents.GlobalEventListener>();
                if (events != null && events.Length > 1 && events[1] != null)
                    events[1].HandleEvent();
                else
                    MelonLogger.Warning("[VanSceneManager] SouvenirRenderSource show event missing.");
            }
            else
            {
                MelonLogger.Warning("[VanSceneManager] SouvenirRenderSource not found.");
            }

            GameObject src = GameObject.Find("ScreenRenderCamera");
            if (src != null)
            {
                SG.GlobalEvents.GlobalEventListener[] listeners = src.GetComponents<SG.GlobalEvents.GlobalEventListener>();
                if (listeners != null && listeners.Length > 0 && listeners[0] != null)
                    listeners[0].HandleEvent();
                else
                    MelonLogger.Warning("[VanSceneManager] ScreenRenderCamera show listener missing.");
            }
            else
            {
                MelonLogger.Warning("[VanSceneManager] ScreenRenderCamera not found.");
            }
        }

        static void UpdateSouvenirIcons()
        {
            Transform srs = GameObject.Find("SouvenirRenderSource").transform;
            GameObject lockedObj = srs.GetChild(0).gameObject;
            GameObject unlockedObj = srs.GetChild(1).gameObject;

            Transform SA = srs.GetChild(4);
            Transform SB = srs.GetChild(5);
            Transform SC = srs.GetChild(6);
            Transform SD = srs.GetChild(7);
            Transform SE = srs.GetChild(8);
            Transform SF = srs.GetChild(9);
            Transform[] iconSockets = new Transform[] { SA, SB, SC, SD, SE, SF };

            bool[] iconValues = SaveManager.Current.SouvenirsFound;

            for (int i = 0; i < iconSockets.Length; i++)
            {
                GameObject icon;
                if (iconValues[i] == true) icon = GameObject.Instantiate(unlockedObj);
                else icon = GameObject.Instantiate(lockedObj);

                icon.name = $"Souvenir{i} Icon";
                icon.transform.parent = srs;
                icon.transform.localScale = new Vector3(1, 1, 1);
                icon.transform.localPosition = iconSockets[i].transform.localPosition;
                icon.gameObject.SetActive(true);
            }
        }

        static GameObject heistLvlSouvenirs;

        static void UpdateSouvenirNames()
        {
            Transform srs = GameObject.Find("SouvenirRenderSource").transform;
            string[] names = new string[] { "Hidden Trophy", "Bear Down", "Housekeeping", "Red Tape", "Smash N Grab", "Friendly Fire" };

            GameObject lvl1Souvenirs = srs.GetChild(10).gameObject;
            heistLvlSouvenirs = GameObject.Instantiate(lvl1Souvenirs);
            lvl1Souvenirs.SetActive(false);
            heistLvlSouvenirs.SetActive(true);
            heistLvlSouvenirs.name = "HeistLvl Souvenirs";
            heistLvlSouvenirs.transform.parent = lvl1Souvenirs.transform.parent;
            heistLvlSouvenirs.transform.localPosition = lvl1Souvenirs.transform.localPosition;
            heistLvlSouvenirs.transform.localScale = new Vector3(1, 1, 1);

            TextMeshProUGUI txt1 = heistLvlSouvenirs.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI txt2 = heistLvlSouvenirs.transform.GetChild(1).GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI txt3 = heistLvlSouvenirs.transform.GetChild(2).GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI txt4 = heistLvlSouvenirs.transform.GetChild(3).GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI txt5 = heistLvlSouvenirs.transform.GetChild(4).GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI txt6 = heistLvlSouvenirs.transform.GetChild(5).GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI[] texts = new TextMeshProUGUI[] { txt1, txt2, txt3, txt4, txt5, txt6 };

            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].text = names[i];
            }

        }

        const float SpeedRunTargetSeconds = 125f;

        static void UpdateSpeedRunDisplay()
        {
            float bestSeconds = -1f;
            try
            {
                if (SaveManager.Current != null)
                    bestSeconds = SaveManager.Current.BestTimeSeconds;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[VanSceneManager] Could not read BestTimeSeconds: " + ex.Message);
            }

            string targetText = FormatSpeedRunTime(SpeedRunTargetSeconds);
            string bestText = (bestSeconds > 0f) ? FormatSpeedRunTime(bestSeconds) : "--:--";
            bool speedRunUnlocked = bestSeconds > 0f && bestSeconds <= SpeedRunTargetSeconds + 0.0001f;

            SetTMPTextByObjectName("SouvenirSpeedRunTarget TMP", targetText);
            SetTMPTextByObjectName("BestTime Text", bestText);

            GameObject locked = FindSceneObjectByName("SouvenirIcon_SpeedRun_Locked");
            GameObject unlocked = FindSceneObjectByName("SouvenirIcon_SpeedRun_Unlocked");

            if (locked != null) locked.SetActive(!speedRunUnlocked);
            if (unlocked != null) unlocked.SetActive(speedRunUnlocked);

            MelonLogger.Msg("[VanSceneManager] Speedrun display updated. Target=" + targetText +
                            " Best=" + bestText + " Unlocked=" + speedRunUnlocked);
        }

        static string FormatSpeedRunTime(float seconds)
        {
            if (seconds <= 0f) return "--:--";

            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds + 0.5f));
            int mins = totalSeconds / 60;
            int secs = totalSeconds % 60;
            return mins.ToString() + ":" + secs.ToString("00");
        }

        static GameObject FindSceneObjectByName(string exactName)
        {
            GameObject go = GameObject.Find(exactName);
            if (go != null) return go;

            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                GameObject candidate = all[i];
                if (candidate == null) continue;
                if (!candidate.scene.IsValid()) continue;
                if (candidate.name == exactName) return candidate;
            }

            return null;
        }

        static void SetTMPTextByObjectName(string objectName, string value)
        {
            GameObject go = FindSceneObjectByName(objectName);
            if (go == null)
            {
                MelonLogger.Warning("[VanSceneManager] Could not find speedrun TMP object: " + objectName);
                return;
            }

            DisableTextDriversThatOverwriteTMP(go);

            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = go.GetComponentInChildren<TextMeshProUGUI>(true);

            if (tmp == null)
            {
                MelonLogger.Warning("[VanSceneManager] Could not find TextMeshProUGUI on: " + objectName);
                return;
            }

            tmp.text = value;
            tmp.SetText(value);
            tmp.ForceMeshUpdate();
        }

        static void DisableTextDriversThatOverwriteTMP(GameObject go)
        {
            if (go == null) return;

            Component[] comps = go.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                Component c = comps[i];
                if (c == null) continue;

                string typeName = c.GetType().FullName ?? c.GetType().Name;
                bool overwritesTMP = typeName.Contains("DigitalTimeDisplay") || typeName.Contains("LocalizedTMPProUGUIText");
                if (!overwritesTMP) continue;

                Behaviour b = c as Behaviour;
                if (b != null)
                {
                    b.enabled = false;
                    MelonLogger.Msg("[VanSceneManager] Disabled text driver on " + go.name + ": " + typeName);
                }
            }
        }

        static void UpdateSouvenirObjects()
        {

            GameObject table = GameObject.Instantiate(HeistBundle2Manager.GetGameObject("table"));
            LevelUtil.ConvertToPhoenix(table);
            table.name = "HeistTable";
            table.transform.position = new Vector3(0.2f, 0.5f, -1.2f);
            table.transform.rotation = Quaternion.Euler(new Vector3(270, 90, 0));

            for (int i = 1; i < 6; i++)
            {
                GameObject souv = SpawnSouvenir(i);
                if (souv == null) continue;

            }

            SetSouvenirTransform();

            bool completedSpeedrun = SaveManager.Current.BestTimeSeconds > 0f && SaveManager.Current.BestTimeSeconds <= 125f;
             if (completedSpeedrun) SpawnWatch();

        }

        static GameObject SpawnSouvenir(int id)
        {
            if (id <= 0 || id > 5) return null;

            if (SaveManager.HasSouvenir(id) == false) return null;

            string[] names = new string[] { "", "BankBearSouvenir", "HousekeepingSouvenir", "", "GoldBarSouvenir", "DroneHand" };
            if (id == 3)
            {
                GameObject flash = GameObject.Find("P_Van_INT_Souvenier_3c").transform.GetChild(0).GetChild(0).gameObject;
                GameObject souv = GameObject.Instantiate(flash);
                souv.name = "LaserPointer";
                souv.transform.GetChild(1).GetChild(0).gameObject.GetComponent<MeshRenderer>().material.mainTexture = HeistBundle2Manager.GetTexture("LaserPointer.png");
                souv.transform.GetChild(1).GetChild(1).gameObject.GetComponent<MeshRenderer>().material.mainTexture = HeistBundle2Manager.GetTexture("LaserPointer.png");
                GameObject child1 = souv.transform.GetChild(0).gameObject;
                GameObject child2 = souv.transform.GetChild(3).gameObject;
                GameObject child3 = souv.transform.GetChild(4).gameObject;
                souv.AddComponent<LaserPointer>();
                GameObject.Destroy(child1); GameObject.Destroy(child2); GameObject.Destroy(child3);
                return souv;
            }
            else
            {

                GameObject souv = GameObject.Instantiate(HeistBundle2Manager.GetGameObject(names[id]));
                bool doMetallic = (id == 4);
                PhoenixMaterialUtil.ConvertToPhoenix(souv, false);
                MakeGrabbableFreshVan(souv);
                if (id == 4) PhoenixMaterialUtil.ForceGoldBarPhoenix(souv);
                if (id == 5) souv.AddComponent<DroneHandVan>();
                return souv;
            }

        }

        static void SetSouvenirTransform()
        {
            GameObject bearDownObj = GameObject.Find("PickUp_HOST_BankBearSouvenir(Clone)");
            if (bearDownObj != null)
            {
                Transform BearDownT = bearDownObj.transform;
                BearDownT.position = new Vector3(0.5983f, 0.8214f, -1.3942f);
                BearDownT.rotation = Quaternion.Euler(new Vector3(270.8727f, 249.8096f, 179.8515f));
                BearDownT.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            }
            else
            {
                MelonLogger.Msg("[VanSceneManager] Bear Down souvenir not spawned; skipping transform.");
            }

            GameObject houseKeepingObj = GameObject.Find("PickUp_HOST_HousekeepingSouvenir(Clone)");
            if (houseKeepingObj != null)
            {
                Transform HouseKeepingT = houseKeepingObj.transform;
                HouseKeepingT.position = new Vector3(-0.1717f, 0.7957f, -1.3878f);
                HouseKeepingT.rotation = Quaternion.Euler(new Vector3(0, 114.4706f, 0));
                HouseKeepingT.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            }
            else
            {
                MelonLogger.Msg("[VanSceneManager] Housekeeping souvenir not spawned; skipping transform.");
            }

            GameObject droneHandObj = GameObject.Find("PickUp_HOST_DroneHand(Clone)");
            if (droneHandObj != null)
            {
                Transform DroneHandT = droneHandObj.transform;
                DroneHandT.position = new Vector3(0.0363f, 0.7389f, -1.4385f);
                DroneHandT.rotation = Quaternion.Euler(new Vector3(83.2118f, 358.8469f, 89.9999f));
                DroneHandT.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            }
            else
            {
                MelonLogger.Msg("[VanSceneManager] DroneHand souvenir not spawned; skipping transform.");
            }

            GameObject goldObj = GameObject.Find("PickUp_HOST_GoldBarSouvenir(Clone)");
            if (goldObj != null)
            {
                Transform GoldT = goldObj.transform;
                GoldT.position = new Vector3(0.6076f, 0.7016f, -1.1019f);
                GoldT.rotation = Quaternion.Euler(new Vector3(-0.0001f, 357.8404f, 0.0003f));
                if (GoldT.childCount > 0) GoldT.GetChild(0).localScale = new Vector3(11.374f, 24.609f, 24.609f);
                GoldT.localScale = new Vector3(1, 1, 1);
            }
            else
            {
                MelonLogger.Msg("[VanSceneManager] GoldBar souvenir not spawned; skipping transform.");
            }

            GameObject laserObj = GameObject.Find("LaserPointer");
            if (laserObj != null)
            {
                Transform LaserT = laserObj.transform;
                LaserT.position = new Vector3(-0.1503f, 0.6845f, -1.218f);
                LaserT.rotation = Quaternion.Euler(5.0462f, 91.4432f, 90);
                LaserT.localScale = new Vector3(1, 1, 1);
            }
            else
            {
                MelonLogger.Msg("[VanSceneManager] LaserPointer souvenir not spawned; skipping transform.");
            }
        }
        static void RefreshSouvenirNames()
        {
            string[] names = new string[] { "Hidden Trophy", "Bear Down", "Housekeeping", "Red Tape", "Smash N Grab", "Friendly Fire" };
            TextMeshProUGUI txt1 = heistLvlSouvenirs.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI txt2 = heistLvlSouvenirs.transform.GetChild(1).GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI txt3 = heistLvlSouvenirs.transform.GetChild(2).GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI txt4 = heistLvlSouvenirs.transform.GetChild(3).GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI txt5 = heistLvlSouvenirs.transform.GetChild(4).GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI txt6 = heistLvlSouvenirs.transform.GetChild(5).GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI[] texts = new TextMeshProUGUI[] { txt1, txt2, txt3, txt4, txt5, txt6 };

            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].text = names[i];
            }
        }

        public static GameObject SubTapeInstance;
        static void createCassetteTape()
        {

            var old = GameObject.Find("SubmarineCassette");
            if (old != null) UnityEngine.Object.Destroy(old);

            GameObject tutTape = GameObject.Find("P_Van_INT_CassetteTutorial");
            Texture2D subTapeTexture = HeistBundle2Manager.GetTexture("HeistTapeTexture");
            GameObject subTape = UnityEngine.Object.Instantiate(tutTape);
            subTape.name = "HeistCassette";
            GameObject subArt = subTape.transform.GetChild(0).GetChild(0).GetChild(0).gameObject;
            subArt.GetComponent<MeshRenderer>().material.mainTexture = subTapeTexture;
            Vector3 tapePos = new Vector3(0.2092f, 0.6874f, -1.148f);
            Vector3 tapeRot = new Vector3(0.0002f, 0.2486f, 0.0008f);
            subTape.SetActive(false);
            subTape.transform.position = tapePos;
            subTape.transform.rotation = Quaternion.Euler(tapeRot);
            subTape.SetActive(true);
            SubTapeInstance = subTape;

            if (GameObject.Find("VanStartButtonHook_GO") == null)
            {
                var hookGO = new GameObject("VanStartButtonHook_GO");
                UnityEngine.Object.DontDestroyOnLoad(hookGO);
                hookGO.AddComponent<VanStartButtonHook>();
                MelonLogger.Msg("[Van] Created VanStartButtonHook.");
            }

        }

        static void CacheDefaultVanScreenState()
        {
            if (!_defaultObjectiveCached)
            {
                GameObject objectiveScreen = GameObject.Find("SM_Van_INT_ObjectiveMonitorScreen_02");
                if (objectiveScreen != null)
                {
                    MeshRenderer mr = objectiveScreen.GetComponent<MeshRenderer>();
                    if (mr != null && mr.material != null)
                    {
                        _defaultObjectiveTexture = mr.material.mainTexture;
                        _defaultObjectiveOffset = mr.material.mainTextureOffset;
                        _defaultObjectiveScale = mr.material.mainTextureScale;
                        _defaultObjectiveCached = true;
                    }
                }
            }

            if (!_defaultSouvenirCached)
            {
                GameObject bgObj = GameObject.Find("SouvenirScreen Background");
                if (bgObj != null)
                {
                    Image img = bgObj.GetComponent<Image>();
                    if (img != null)
                    {
                        _defaultSouvenirSprite = img.sprite;
                        _defaultSouvenirOverrideSprite = img.overrideSprite;
                        _defaultSouvenirCached = true;
                    }
                }
            }
        }

        public static void RestoreDefaultVanScreens()
        {
            SetObjectiveScreenTextureFromBundle("UI_Van_ObjectiveScreen_Standby.png");
            SetSouvenirBackgroundFromBundle("UI_Van_ObjectiveScreen_Standby.png");
            SetSouvenirStaticOverlay("UI_Van_ObjectiveScreen_Standby.png", true);
            MelonLogger.Msg("[VanSceneManager] Set both van screens to standby PNG.");
        }

        public static void SpawnWatch()
        {
            GameObject donor = GameObject.Find("P_Shared_INT_SpdWatch_MovieSet");

            if (donor == null)
            {
                GameObject fallback = GameObject.Find("P_Van_INT_Souvenier_5speedrun");
                if (fallback != null && fallback.transform.childCount > 0)
                {
                    Transform a = fallback.transform.GetChild(0);
                    if (a != null && a.childCount > 0)
                        donor = a.GetChild(0).gameObject;
                }
            }

            if (donor == null)
            {
                MelonLogger.Warning("[VanSceneManager] SpawnWatch failed: donor not found.");
                return;
            }

            GameObject heistWatch = GameObject.Instantiate(donor);
            heistWatch.name = "HeistWatch";

            Texture2D watchTex = HeistBundle2Manager.GetTexture("DroneWatch.png");
            if (watchTex == null)
                MelonLogger.Warning("[VanSceneManager] SpawnWatch: DroneWatch.png texture not found.");

            MeshRenderer faceRenderer = FindWatchFaceRenderer(heistWatch);
            if (faceRenderer != null && watchTex != null)
                ApplyWatchAlbedo(faceRenderer, watchTex);
            else if (faceRenderer == null)
                MelonLogger.Warning("[VanSceneManager] SpawnWatch: watch face renderer not found.");

            StripWatchToAlbedoOnlyAndMakeMetallic(heistWatch, faceRenderer, watchTex);

            heistWatch.SetActive(true);
            heistWatch.transform.position = new Vector3(-0.2407f, 0.6891f, - 1.2412f);
            heistWatch.transform.rotation = Quaternion.Euler(new Vector3(-0.0001f, 305.9389f, 282.3544f));
        }

        static MeshRenderer FindWatchFaceRenderer(GameObject watchRoot)
        {
            if (watchRoot == null) return null;

            try
            {
                Transform t = watchRoot.transform;
                if (t.childCount > 0)
                {
                    t = t.GetChild(0);
                    if (t != null && t.childCount > 0)
                    {
                        t = t.GetChild(0);
                        if (t != null && t.childCount > 3)
                        {
                            GameObject faceObj = t.GetChild(3).gameObject;
                            MeshRenderer mr = faceObj.GetComponent<MeshRenderer>();
                            if (mr != null) return mr;
                        }
                    }
                }
            }
            catch { }

            MeshRenderer[] renderers = watchRoot.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers == null) return null;

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer r = renderers[i];
                if (r == null) continue;

                string n = (r.gameObject.name ?? "").ToLowerInvariant();
                if (n.Contains("face") || n.Contains("screen") || n.Contains("watch"))
                    return r;
            }

            return renderers.Length > 0 ? renderers[0] : null;
        }

        static void ApplyWatchAlbedo(MeshRenderer renderer, Texture2D albedo)
        {
            if (renderer == null || albedo == null) return;

            Material[] mats = renderer.materials;
            if (mats == null) return;

            for (int i = 0; i < mats.Length; i++)
            {
                Material mat = mats[i];
                if (mat == null) continue;

                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
                if (mat.HasProperty("_BaseColorMap")) mat.SetTexture("_BaseColorMap", albedo);
                if (mat.HasProperty("_AlbedoMap")) mat.SetTexture("_AlbedoMap", albedo);

                try { mat.mainTexture = albedo; } catch { }
            }

            renderer.materials = mats;
        }

        static void StripWatchToAlbedoOnlyAndMakeMetallic(GameObject watchRoot, MeshRenderer forcedAlbedoRenderer, Texture2D forcedAlbedo)
        {
            if (watchRoot == null) return;

            Renderer[] renderers = watchRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                MelonLogger.Warning("[VanSceneManager] SpawnWatch: no renderers found to clean.");
                return;
            }

            int materialCount = 0;
            int clearedTextures = 0;

            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null) continue;

                Material[] mats = renderer.materials;
                if (mats == null) continue;

                bool rendererChanged = false;

                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    if (mat == null) continue;

                    materialCount++;
                    Texture2D albedoForThisRenderer = (renderer == forcedAlbedoRenderer) ? forcedAlbedo : null;
                    clearedTextures += StripMaterialToAlbedoOnlyAndMakeMetallic(mat, albedoForThisRenderer);
                    rendererChanged = true;
                }

                if (rendererChanged)
                    renderer.materials = mats;
            }

            MelonLogger.Msg("[VanSceneManager] SpawnWatch kept albedo only, cleared " + clearedTextures +
                            " texture slot(s), and applied generic metallic values on " + materialCount + " material(s).");
        }

        static int StripMaterialToAlbedoOnlyAndMakeMetallic(Material mat, Texture2D forcedAlbedo)
        {
            if (mat == null) return 0;

            Texture keepMain = forcedAlbedo;

            if (keepMain == null)
            {
                try { if (mat.HasProperty("_MainTex")) keepMain = mat.GetTexture("_MainTex"); } catch { }
                if (keepMain == null) { try { keepMain = mat.mainTexture; } catch { } }
                if (keepMain == null) { try { if (mat.HasProperty("_BaseMap")) keepMain = mat.GetTexture("_BaseMap"); } catch { } }
                if (keepMain == null) { try { if (mat.HasProperty("_BaseColorMap")) keepMain = mat.GetTexture("_BaseColorMap"); } catch { } }
                if (keepMain == null) { try { if (mat.HasProperty("_AlbedoMap")) keepMain = mat.GetTexture("_AlbedoMap"); } catch { } }
            }

            int cleared = 0;

            cleared += ClearTexture(mat, "_BumpMap") ? 1 : 0;
            cleared += ClearTexture(mat, "_NormalMap") ? 1 : 0;
            cleared += ClearTexture(mat, "_DetailNormalMap") ? 1 : 0;
            cleared += ClearTexture(mat, "_DetailBumpMap") ? 1 : 0;

            cleared += ClearTexture(mat, "_ParallaxMap") ? 1 : 0;
            cleared += ClearTexture(mat, "_HeightMap") ? 1 : 0;
            cleared += ClearTexture(mat, "_DisplacementMap") ? 1 : 0;

            cleared += ClearTexture(mat, "_MetallicGlossMap") ? 1 : 0;
            cleared += ClearTexture(mat, "_SpecGlossMap") ? 1 : 0;
            cleared += ClearTexture(mat, "_SpecularMap") ? 1 : 0;
            cleared += ClearTexture(mat, "_GlossMap") ? 1 : 0;
            cleared += ClearTexture(mat, "_MaskMap") ? 1 : 0;
            cleared += ClearTexture(mat, "_PackedMap") ? 1 : 0;
            cleared += ClearTexture(mat, "_MSEMap") ? 1 : 0;
            cleared += ClearTexture(mat, "_MseMap") ? 1 : 0;
            cleared += ClearTexture(mat, "_mse") ? 1 : 0;
            cleared += ClearTexture(mat, "mse") ? 1 : 0;
            cleared += ClearTexture(mat, "_ORMMap") ? 1 : 0;
            cleared += ClearTexture(mat, "_MRAOMap") ? 1 : 0;

            cleared += ClearTexture(mat, "_OcclusionMap") ? 1 : 0;
            cleared += ClearTexture(mat, "_DetailMask") ? 1 : 0;
            cleared += ClearTexture(mat, "_DetailAlbedoMap") ? 1 : 0;
            cleared += ClearTexture(mat, "_EmissionMap") ? 1 : 0;

            if (keepMain != null)
            {
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", keepMain);
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", keepMain);
                if (mat.HasProperty("_BaseColorMap")) mat.SetTexture("_BaseColorMap", keepMain);
                if (mat.HasProperty("_AlbedoMap")) mat.SetTexture("_AlbedoMap", keepMain);
                try { mat.mainTexture = keepMain; } catch { }
            }

            SetFloatIfExists(mat, "_Metallic", 1f);
            SetFloatIfExists(mat, "_MetallicAmount", 1f);
            SetFloatIfExists(mat, "_MetallicStrength", 1f);
            SetFloatIfExists(mat, "_Metalness", 1f);

            SetFloatIfExists(mat, "_Glossiness", 0.92f);
            SetFloatIfExists(mat, "_Smoothness", 0.92f);
            SetFloatIfExists(mat, "_SmoothnessAmount", 0.92f);
            SetFloatIfExists(mat, "_Roughness", 0.08f);
            SetFloatIfExists(mat, "_SpecularHighlights", 1f);
            SetFloatIfExists(mat, "_GlossyReflections", 1f);

            DisableKeywordSafe(mat, "_NORMALMAP");
            DisableKeywordSafe(mat, "_PARALLAXMAP");
            DisableKeywordSafe(mat, "_METALLICGLOSSMAP");
            DisableKeywordSafe(mat, "_SPECGLOSSMAP");
            DisableKeywordSafe(mat, "_OCCLUSIONMAP");
            DisableKeywordSafe(mat, "_EMISSION");
            DisableKeywordSafe(mat, "_DETAIL_MULX2");

            SetFloatIfExists(mat, "_BumpScale", 0f);
            SetFloatIfExists(mat, "_NormalScale", 0f);
            SetFloatIfExists(mat, "_Parallax", 0f);
            SetFloatIfExists(mat, "_HeightScale", 0f);
            SetFloatIfExists(mat, "_OcclusionStrength", 0f);

            if (mat.HasProperty("_SpecColor"))
            {
                try { mat.SetColor("_SpecColor", Color.white); } catch { }
            }

            if (mat.HasProperty("_EmissionColor"))
            {
                try { mat.SetColor("_EmissionColor", Color.black); } catch { }
            }

            return cleared;
        }

        static bool ClearTexture(Material mat, string prop)
        {
            if (mat == null) return false;
            if (!mat.HasProperty(prop)) return false;

            try
            {
                if (mat.GetTexture(prop) == null) return false;
                mat.SetTexture(prop, null);
                return true;
            }
            catch { return false; }
        }

        static void DisableKeywordSafe(Material mat, string keyword)
        {
            if (mat == null) return;
            try { mat.DisableKeyword(keyword); } catch { }
        }

        static void SetFloatIfExists(Material mat, string prop, float value)
        {
            if (mat == null) return;
            if (!mat.HasProperty(prop)) return;
            try { mat.SetFloat(prop, value); } catch { }
        }

        static bool MakeGrabbableFreshVan(GameObject target)
        {
            if (!target)
            {
                MelonLogger.Warning("[VanSceneManager] MakeGrabbableFreshVan called with null target.");
                return false;
            }

            ResolveVanPhoenixTypes();
            if (_vanPickUpIl2 == null)
            {
                MelonLogger.Error("[VanSceneManager] Phoenix PickUp type not found. Can't make van souvenir grabbable.");
                return false;
            }

            Component donor = FindFreshVanDonorPickUp();
            if (donor != null && TryCloneFreshVanDonorAsHost(donor, target))
                return true;

            MelonLogger.Warning("[VanSceneManager] Fresh donor host failed for " + target.name + ". Trying direct fallback.");
            return TryVanFallbackAddComponents(target);
        }

        static void ResolveVanPhoenixTypes()
        {
            if (_vanPickUpManaged == null) _vanPickUpManaged = FindTypeBySuffix(".Interactables.PickUp");
            if (_vanShakeManaged == null) _vanShakeManaged = FindTypeBySuffix(".Gestures.PickUpShakeGesture");

            if (_vanPickUpManaged != null && _vanPickUpIl2 == null) _vanPickUpIl2 = ToIl2(_vanPickUpManaged);
            if (_vanShakeManaged != null && _vanShakeIl2 == null) _vanShakeIl2 = ToIl2(_vanShakeManaged);
        }

        static Component FindFreshVanDonorPickUp()
        {
            if (_vanPickUpIl2 == null) return null;

            for (int i = 0; i < VanDonorExactNames.Length; i++)
            {
                GameObject g = GameObject.Find(VanDonorExactNames[i]);
                if (!g) continue;

                Component c = g.GetComponent(_vanPickUpIl2) as Component;
                if (c != null)
                {
                    MelonLogger.Msg("[VanSceneManager] Fresh van donor PickUp found by GameObject.Find: " + g.name);
                    return c;
                }
            }

            Component best = null;
            var allGos = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < allGos.Length; i++)
            {
                var g = allGos[i];
                if (!g || !g.scene.IsValid()) continue;

                Component c = g.GetComponent(_vanPickUpIl2) as Component;
                if (c == null) continue;

                string nm = (g.name ?? "").ToLowerInvariant();
                if (nm.Contains("fastfoodcup") || nm.Contains("cup"))
                {
                    MelonLogger.Msg("[VanSceneManager] Fresh van donor PickUp found by fresh scan: " + g.name);
                    return c;
                }

                if (best == null || g.GetComponent<Rigidbody>() != null)
                    best = c;
            }

            if (best != null)
            {
                MelonLogger.Msg("[VanSceneManager] Fresh van donor PickUp fallback: " + best.gameObject.name);
                return best;
            }

            MelonLogger.Warning("[VanSceneManager] No fresh van donor PickUp found.");
            return null;
        }

        static bool TryCloneFreshVanDonorAsHost(Component donorPickUp, GameObject target)
        {
            try
            {
                var donorGO = donorPickUp.gameObject;
                if (!donorGO || !target) return false;

                var host = UnityEngine.Object.Instantiate(donorGO);
                host.SetActive(true);
                host.name = "PickUp_HOST_" + target.name;

                host.transform.position = target.transform.position;
                host.transform.rotation = target.transform.rotation;
                host.transform.localScale = target.transform.lossyScale;

                var rb = host.GetComponent<Rigidbody>() ?? host.AddComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.useGravity = true;
                if (rb.mass <= 0f) rb.mass = 1f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                if (_vanPickUpIl2 != null && host.GetComponent(_vanPickUpIl2) == null)
                    host.AddComponent(_vanPickUpIl2);

                if (_vanShakeIl2 != null && host.GetComponent(_vanShakeIl2) == null)
                    host.AddComponent(_vanShakeIl2);

                var donorRends = host.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < donorRends.Length; i++)
                {
                    var r = donorRends[i];
                    if (r) r.enabled = false;
                }

                target.transform.SetParent(host.transform, true);

                var targetRbs = target.GetComponentsInChildren<Rigidbody>(true);
                for (int i = 0; i < targetRbs.Length; i++)
                    if (targetRbs[i]) UnityEngine.Object.Destroy(targetRbs[i]);

                StripVanHost(host, target, _vanPickUpIl2, _vanShakeIl2);

                var il2Cols = target.GetComponentsInChildren<Collider>(true);
                var list = new List<Collider>();
                if (il2Cols != null)
                {
                    for (int i = 0; i < il2Cols.Length; i++)
                    {
                        var c = il2Cols[i];
                        if (!c) continue;

                        var mc = c as MeshCollider;
                        if (mc != null) { try { mc.convex = true; } catch { } }

                        list.Add(c);
                    }
                }

                if (list.Count == 0)
                {
                    var mf = target.GetComponentInChildren<MeshFilter>(true);
                    if (mf && mf.sharedMesh)
                    {
                        var mc = target.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                        mc.convex = true;
                        list.Add(mc);
                    }
                    else
                    {
                        list.Add(target.AddComponent<BoxCollider>());
                    }
                }

                var pickUpComp = (_vanPickUpIl2 != null) ? host.GetComponent(_vanPickUpIl2) as Component : null;
                if (pickUpComp != null)
                {
                    TryEnableBehaviour(pickUpComp);
                    KickPickUpEnableGuardian(pickUpComp, 4f);
                    BindCollidersToInteractable(pickUpComp, list.ToArray());
                }

                EnsureTargetVisualsOn(target);

                int interact = LayerMask.NameToLayer("Interactable");
                if (interact < 0) interact = 8;
                LevelUtil.SetLayerRecursive(target, interact);
                EnsureLayerVisibleToCamera(target, FindHMDCamera());

                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[VanSceneManager] Fresh van grab host failed: " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        static bool TryVanFallbackAddComponents(GameObject target)
        {
            try
            {
                if (_vanPickUpIl2 == null) return false;

                BreakAllJoints(target);

                var cols = target.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < cols.Length; i++)
                {
                    var c = cols[i];
                    if (!c) continue;

                    try { c.enabled = true; } catch { }
                    var mc = c as MeshCollider;
                    if (mc != null) { try { mc.convex = true; } catch { } }
                }

                if (target.GetComponent(_vanPickUpIl2) == null) target.AddComponent(_vanPickUpIl2);
                if (_vanShakeIl2 != null && target.GetComponent(_vanShakeIl2) == null) target.AddComponent(_vanShakeIl2);

                var rb = target.GetComponent<Rigidbody>() ?? target.AddComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.mass = (rb.mass <= 0f) ? 1f : rb.mass;
                rb.drag = Mathf.Max(rb.drag, 0.05f);
                rb.angularDrag = Mathf.Max(rb.angularDrag, 0.05f);
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.constraints = RigidbodyConstraints.None;
                rb.detectCollisions = true;

                bool hadCollider = false;
                for (int i = 0; i < cols.Length; i++) { if (cols[i]) { hadCollider = true; break; } }
                if (!hadCollider)
                {
                    var mf = target.GetComponentInChildren<MeshFilter>(true);
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var mc = target.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                        mc.convex = true;
                    }
                    else
                    {
                        target.AddComponent<BoxCollider>();
                    }
                }

                int interact = LayerMask.NameToLayer("Interactable");
                if (interact < 0) interact = 8;
                LevelUtil.SetLayerRecursive(target, interact);
                EnsureLayerVisibleToCamera(target, FindHMDCamera());

                var pu = target.GetComponent(_vanPickUpIl2) as Component;
                if (pu != null)
                {
                    TryEnableBehaviour(pu);
                    KickPickUpEnableGuardian(pu, 3.5f);

                    var bindCols = target.GetComponentsInChildren<Collider>(true);
                    if (bindCols != null && bindCols.Length > 0)
                        BindCollidersToInteractable(pu, bindCols);
                }

                EnsureTargetVisualsOn(target);
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[VanSceneManager] Fresh van fallback attach failed: " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        static void StripVanHost(GameObject host, GameObject target, Il2CppSystem.Type pickUpIl2, Il2CppSystem.Type shakeIl2)
        {
            if (!host) return;

            var rb = host.GetComponent<Rigidbody>();
            var pick = (pickUpIl2 != null) ? (host.GetComponent(pickUpIl2) as Component) : null;
            var shake = (shakeIl2 != null) ? (host.GetComponent(shakeIl2) as Component) : null;

            IntPtr rbPtr = PtrOf(rb);
            IntPtr pickPtr = PtrOf(pick);
            IntPtr shakePtr = PtrOf(shake);

            var tr = host.transform;
            var killChildren = new List<GameObject>();
            for (int i = 0; i < tr.childCount; i++)
            {
                var child = tr.GetChild(i);
                if (!child) continue;
                if (target != null && (child == target.transform || child.IsChildOf(target.transform))) continue;
                killChildren.Add(child.gameObject);
            }
            for (int i = 0; i < killChildren.Count; i++) { try { UnityEngine.Object.DestroyImmediate(killChildren[i]); } catch { } }

            var comps = host.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i];
                if (!c) continue;
                if (c is Transform) continue;
                if (SameIl2CppObject(c, rbPtr)) continue;
                if (SameIl2CppObject(c, pickPtr)) continue;
                if (SameIl2CppObject(c, shakePtr)) continue;
                try { UnityEngine.Object.DestroyImmediate(c); } catch { }
            }

            var cols = host.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (!c) continue;
                if (target != null && (c.transform == target.transform || c.transform.IsChildOf(target.transform))) continue;
                try { UnityEngine.Object.DestroyImmediate(c); } catch { }
            }

            var rends = host.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (!r) continue;
                if (target != null && (r.transform == target.transform || r.transform.IsChildOf(target.transform))) continue;
                try { UnityEngine.Object.DestroyImmediate(r); } catch { }
            }

            var aud = host.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < aud.Length; i++)
            {
                var a = aud[i];
                if (!a) continue;
                if (target != null && (a.transform == target.transform || a.transform.IsChildOf(target.transform))) continue;
                try { UnityEngine.Object.DestroyImmediate(a); } catch { }
            }

            if (host.GetComponent<Rigidbody>() == null) host.AddComponent<Rigidbody>();
            if (pickUpIl2 != null && host.GetComponent(pickUpIl2) == null) host.AddComponent(pickUpIl2);
            if (shakeIl2 != null && host.GetComponent(shakeIl2) == null) host.AddComponent(shakeIl2);
        }

        static bool SameIl2CppObject(UnityEngine.Object a, IntPtr bPtr)
        {
            if (a == null || bPtr == IntPtr.Zero) return false;
            try
            {
                var ib = a as Il2CppObjectBase;
                if (ib == null) return false;
                return ib.Pointer == bPtr;
            }
            catch { return false; }
        }

        static IntPtr PtrOf(UnityEngine.Object o)
        {
            if (o == null) return IntPtr.Zero;
            try
            {
                var ib = o as Il2CppObjectBase;
                return ib != null ? ib.Pointer : IntPtr.Zero;
            }
            catch { return IntPtr.Zero; }
        }

        static void KickPickUpEnableGuardian(Component pickUp, float seconds)
        {
            if (pickUp == null) return;
            MelonCoroutines.Start(Co_PickUpEnableGuardian(pickUp, seconds));
        }

        static IEnumerator Co_PickUpEnableGuardian(Component pickUp, float seconds)
        {
            float end = Time.time + Mathf.Max(0.25f, seconds);
            while (pickUp != null && Time.time < end)
            {
                try
                {
                    TryEnableBehaviour(pickUp);
                    var go = pickUp.gameObject;
                    if (go != null && !go.activeSelf) go.SetActive(true);
                    TrySetBoolProperty(pickUp, "IsEnabled", true);
                    TrySetBoolProperty(pickUp, "Enabled", true);
                    TrySetBoolField(pickUp, "isEnabled", true);
                    var rb = (go != null) ? go.GetComponent<Rigidbody>() : null;
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        rb.detectCollisions = true;
                    }
                }
                catch { }
                yield return null;
            }
        }

        static void TryEnableBehaviour(object comp)
        {
            if (comp == null) return;
            try
            {
                var b = comp as Behaviour;
                if (b != null) b.enabled = true;
            }
            catch { }
        }

        static bool TrySetBoolProperty(object o, string name, bool val)
        {
            try
            {
                var p = o.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.CanWrite && p.PropertyType == typeof(bool)) { p.SetValue(o, val, null); return true; }
            }
            catch { }
            return false;
        }

        static bool TrySetBoolField(object o, string name, bool val)
        {
            try
            {
                var f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null && f.FieldType == typeof(bool)) { f.SetValue(o, val); return true; }
            }
            catch { }
            return false;
        }

        static void BindCollidersToInteractable(Component interactable, Collider[] cols)
        {
            if (!interactable || cols == null) return;
            try
            {
                var arr = new Il2CppReferenceArray<Collider>(cols.Length);
                for (int i = 0; i < cols.Length; i++) arr[i] = cols[i];
                if (TrySetFirstColliderArrayField(interactable, arr)) return;
            }
            catch { }
            TrySetFirstColliderArrayField(interactable, cols);
        }

        static bool TrySetFirstColliderArrayField(object obj, object value)
        {
            var t = obj.GetType();
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
            var fields = t.GetFields(F);
            for (int i = 0; i < fields.Length; i++)
            {
                var f = fields[i];
                try
                {
                    var ft = f.FieldType;
                    if (ft.FullName != null && ft.FullName.Contains("Collider") && ft.IsArray == value.GetType().IsArray) { f.SetValue(obj, value); return true; }
                    if (ft.FullName != null && ft.FullName.Contains("Il2CppReferenceArray") && value.GetType().FullName != null && value.GetType().FullName.Contains("Il2CppReferenceArray")) { f.SetValue(obj, value); return true; }
                }
                catch { }
            }
            return false;
        }

        static void EnsureTargetVisualsOn(GameObject target)
        {
            if (!target) return;
            target.SetActive(true);
            var rends = target.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (!r) continue;
                try
                {
                    r.enabled = true;
#if UNITY_2019_4_OR_NEWER
                    try { r.forceRenderingOff = false; } catch { }
#endif
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows = true;
                }
                catch { }
            }
        }

        static void BreakAllJoints(GameObject go)
        {
            if (!go) return;
            var j0 = go.GetComponents<Joint>();
            for (int i = 0; i < j0.Length; i++) { try { UnityEngine.Object.Destroy(j0[i]); } catch { } }
            var j1 = go.GetComponentsInChildren<Joint>(true);
            for (int i = 0; i < j1.Length; i++) { try { UnityEngine.Object.Destroy(j1[i]); } catch { } }
        }

        static Camera FindHMDCamera()
        {
            var cams = Resources.FindObjectsOfTypeAll<Camera>();
            for (int i = 0; i < cams.Length; i++)
            {
                var c = cams[i];
                if (c == null) continue;
                if (!c.gameObject.scene.IsValid()) continue;
                if (!c.gameObject.activeInHierarchy) continue;
                if (c.name == "HMD") return c;
            }
            return null;
        }

        static void EnsureLayerVisibleToCamera(GameObject go, Camera cam)
        {
            if (go == null || cam == null) return;
            int layer = go.layer;
            int mask = cam.cullingMask;
            if (((mask >> layer) & 1) == 0)
            {
                for (int i = 0; i < 32; i++)
                {
                    if (((mask >> i) & 1) != 0)
                    {
                        LevelUtil.SetLayerRecursive(go, i);
                        break;
                    }
                }
            }
        }

        static Type FindTypeBySuffix(string suffix)
        {
            if (string.IsNullOrEmpty(suffix)) return null;
            string[] guesses = { "SG.Phoenix.Assets.Code" + suffix, "Phoenix.Assets.Code" + suffix, suffix.TrimStart('.') };
            for (int i = 0; i < guesses.Length; i++)
            {
                try
                {
                    var exact = Type.GetType(guesses[i], false);
                    if (exact != null) return exact;
                }
                catch { }
            }
            try
            {
                var asms = AppDomain.CurrentDomain.GetAssemblies();
                for (int a = 0; a < asms.Length; a++)
                {
                    var asm = asms[a];
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                    catch { continue; }
                    if (types == null) continue;
                    for (int t = 0; t < types.Length; t++)
                    {
                        var ty = types[t];
                        if (ty == null) continue;
                        var fn = ty.FullName ?? ty.Name;
                        if (fn != null && fn.EndsWith(suffix, StringComparison.Ordinal)) return ty;
                    }
                }
            }
            catch { }
            return null;
        }

        static Il2CppSystem.Type ToIl2(Type t)
        {
            if (t == null) return null;
            try { return Il2CppType.From(t); }
            catch { return null; }
        }

    }
}
