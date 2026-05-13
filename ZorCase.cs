using SG.Phoenix.Assets.Code.Interactables;
using SG.Phoenix.Assets.Code.VignetteNodes.Phoenix;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{

    public class ZorCase : MonoBehaviour
    {
        public ZorCase(IntPtr ptr) : base(ptr) { }
        public ZorCase()
            : base(ClassInjector.DerivedConstructorPointer<ZorCase>())
            => ClassInjector.DerivedConstructorBody(this);


        public PickUp pu;
        bool triggered = false;


        public void Start()
        {
            if(transform.parent != null)
            {
                pu = transform.parent.GetComponent<PickUp>();
            }
        }

        void Update()
        {
            if(pu == null)
            {
                pu = transform.parent.GetComponent<PickUp>();
                if (pu == null) return;
            }

            if(pu.isHeld && !triggered)
            {
                triggered = true;
                HeistLevelManager.ZorCaseGrabbed();
            }
        }
    }
}
