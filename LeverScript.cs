using System;
using SG.Phoenix.Assets.Code.Interactables;
using UnhollowerRuntimeLib;
using UnityEngine;
using UnityEngine.Events;

namespace IEYTD_Mod2Code
{
    class LeverScript : MonoBehaviour
    {
        public LeverScript(IntPtr ptr) : base(ptr) { }
        public LeverScript() : base(ClassInjector.DerivedConstructorPointer<LeverScript>())
            => ClassInjector.DerivedConstructorBody(this);

        RotationalMotion rotationalMotion;
        GameObject[] shutters;
        DroneVentRailTK ventRail;
        bool shuttersOpen;

        void Start()
        {
            rotationalMotion = GetComponent<RotationalMotion>();

            Transform shutterRoot = GameObject.Find("Shutters").transform;
            shutters = new GameObject[6];
            for (int i = 0; i < shutters.Length; i++)
                shutters[i] = shutterRoot.GetChild(i).gameObject;

            rotationalMotion._OnInteractEvent.AddListener((UnityAction)OnInteracted);
            ventRail = GameObject.Find("PickUp_HOST_Drone").GetComponent<DroneVentRailTK>();
        }

        void OnInteracted()
        {
            rotationalMotion.heldHand.ReleaseHeldObject();
            shuttersOpen = !shuttersOpen;

            rotationalMotion.SetRotation(shuttersOpen ? 80f : 0f);
            ToggleShutters(shuttersOpen);
            ventRail.ShuttersOpen = shuttersOpen;
        }

        void ToggleShutters(bool open)
        {
            AudioUtil.PlayAt("LeverFlipped.ogg", transform.position);

            for (int i = 0; i < shutters.Length; i++)
            {
                shutters[i].transform.rotation = Quaternion.Euler(0f, open ? 90f : 15f, 0f);
                shutters[i].GetComponent<BoxCollider>().enabled = !open;
            }
        }
    }
}
