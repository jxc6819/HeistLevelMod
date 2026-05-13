using System;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class MainGear : MonoBehaviour
    {
        public MainGear(IntPtr ptr) : base(ptr) { }
        public MainGear() : base(ClassInjector.DerivedConstructorPointer<MainGear>())
            => ClassInjector.DerivedConstructorBody(this);

        public InfiniteDroneRotationalMotion source;
        public float lastRot;
        public float rot;

        void Start()
        {
            rot = source != null ? source.continuousAngle : 0f;
            lastRot = rot;
        }

        void Update()
        {
            lastRot = rot;

            if (source != null)
                rot = source.continuousAngle;
        }
    }
}
