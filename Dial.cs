using MelonLoader;
using SG.Phoenix.Assets.Code.VignetteNodes.Phoenix;
using System;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class Dial : MonoBehaviour
    {
        public Dial(IntPtr ptr) : base(ptr) { }
        public Dial() : base(ClassInjector.DerivedConstructorPointer<Dial>())
            => ClassInjector.DerivedConstructorBody(this);

        public int currentLight = -1;
        public bool primed;
        public VaultPuzzleManager manager;

        GameObject activeLight;
        bool redTriggered;

        public void OnTriggerEnter(Collider other)
        {
            int lightColor = GetLightColor(other.gameObject);

            if (lightColor == -1)
                return;

            activeLight = other.gameObject;
            currentLight = lightColor;
        }

        public void OnTriggerExit(Collider other)
        {
            if (activeLight != other.gameObject)
                return;

            activeLight = null;
            currentLight = -1;
        }

        public int GetLightColor(GameObject obj)
        {
            string objName = obj.name.ToLower();

            if (objName.Contains("redlight")) return 0;
            if (objName.Contains("greenlight")) return 1;
            return -1;
        }

        public void Released()
        {
            if (primed)
                return;

            SubGear subGear = transform.GetChild(0).GetComponent<SubGear>();

            if (currentLight == 0)
            {
                subGear.RotateTo(Quaternion.Euler(subGear.redLightAngle, 0f, 0f), false);
                RedLight();
            }
            else if (currentLight == 1)
            {
                subGear.RotateTo(Quaternion.Euler(180f, 0f, 0f), true);
                GreenLight();
            }
        }

        void GreenLight()
        {
            MelonLogger.Msg($"[Dial] - {gameObject.name} - GreenLight");

            transform.GetChild(0).GetComponent<SubGear>().enabled = false;
            primed = true;
            manager.GreenLightTriggered();
        }

        void RedLight()
        {
            if (redTriggered)
                return;

            redTriggered = true;

            MelonLogger.Msg($"[Dial] - {gameObject.name} - RedLight");

            SubGear subGear = transform.GetChild(0).GetComponent<SubGear>();
            subGear.mainGear.gameObject.GetComponent<InfiniteDroneRotationalMotion>().enabled = false;

            manager.RedLightTriggered();
            enabled = false;
        }
    }
}
