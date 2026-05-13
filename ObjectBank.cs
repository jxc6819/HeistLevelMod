using System;
using MelonLoader;
using UnityEngine;

namespace IEYTD_Mod2Code
{

    public class ObjectBank : MonoBehaviour
    {
        public ObjectBank(IntPtr ptr) : base(ptr) { }

        public static ObjectBank Instance;

        public GameObject ModLevelRoot;
        public GameObject GatheredAssetsRoot;

        public GameObject PlayerRig;
        public GameObject MainCamera;

        public GameObject SceneLoader;
        public GameObject PlayerSpawn;

        public GameObject HMD;
        public GameObject Drone;
        public GameObject Lever;
        public GameObject Manager;
        public GameObject Suitcase;

        public List<GameObject> PickUps = new List<GameObject>();
        public List<GameObject> RotMotions = new List<GameObject>();
        void Awake()
        {
            Instance = this;
        }

        public void RefreshAll()
        {

            ModLevelRoot = GameObject.Find("ModLevel_ROOT");

            if (!ModLevelRoot)
            {
                MelonLogger.Warning("[ObjectBank] ModLevel_ROOT not found.");
                return;
            }

            GatheredAssetsRoot = FindUnder(ModLevelRoot, "Gathered_ROOT") ?? GameObject.Find("Gathered_ROOT");

            PlayerRig = FindUnder(ModLevelRoot, "VRRig")
                        ?? GameObject.Find("VRRig");

            MainCamera = FindUnder(ModLevelRoot, "Main Camera")
                          ?? GameObject.Find("Main Camera");

            SceneLoader = FindUnder(ModLevelRoot, "Scene Loader")
                          ?? GameObject.Find("Scene Loader");

            PlayerSpawn = FindUnder(ModLevelRoot, "PlayerSpawn")
                          ?? GameObject.Find("PlayerSpawn");

            HMD = GameObject.Find("HMD");
            Drone = GameObject.Find("Drone");
            Lever = FindGathered("P_BSP_BannerPulleyLever(Clone)");
            Manager = FindUnder(ModLevelRoot, "Manager")
                          ?? GameObject.Find("Manager");

            Suitcase = FindGathered("ELV_NuclearFootball(Clone)");

        }

        public GameObject FindGathered(string exactName)
        {
            if (!GatheredAssetsRoot) return null;
            return FindUnder(GatheredAssetsRoot, exactName);
        }

        GameObject FindUnder(GameObject root, string exactName)
        {
            if (!root) return null;

            var trs = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < trs.Length; i++)
            {
                var t = trs[i];
                if (t && t.name == exactName)
                    return t.gameObject;
            }

            return null;
        }
    }
}
