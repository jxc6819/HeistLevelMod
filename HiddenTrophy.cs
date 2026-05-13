using System;
using SG.Phoenix.Assets.Code.Interactables;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class HiddenTrophy : MonoBehaviour
    {
        public HiddenTrophy(IntPtr ptr) : base(ptr) { }
        public HiddenTrophy() : base(ClassInjector.DerivedConstructorPointer<HiddenTrophy>())
            => ClassInjector.DerivedConstructorBody(this);

        PickUp pickUp;
        bool grabbed;

        void Start()
        {
            pickUp = transform.parent.GetComponent<PickUp>();
        }

        void Update()
        {
            if (grabbed || !pickUp.isHeld)
                return;

            grabbed = true;
            OnGrabbed();
        }

        void OnGrabbed()
        {
            AudioUtil.PlayAt("SouvenirStinger.ogg", transform.position, 1f, true);

            if (!SaveManager.HasSouvenir(0))
                HeistLevelManager.FoundSouvenir(0);

            enabled = false;
        }
    }
}
