using System;
using MelonLoader;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class ThumbstickTest : MonoBehaviour
    {
        public ThumbstickTest(IntPtr p) : base(p) { }
        public ThumbstickTest() : base(ClassInjector.DerivedConstructorPointer<ThumbstickTest>())
            => ClassInjector.DerivedConstructorBody(this);

        Vector2 lastLeft = new Vector2(999f, 999f);
        Vector2 lastRight = new Vector2(999f, 999f);
        float nextLogTime;

        void Update()
        {
            if (Time.unscaledTime < nextLogTime)
                return;

            nextLogTime = Time.unscaledTime + 0.1f;

            Vector2 left = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
            Vector2 right = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);

            if ((left - lastLeft).sqrMagnitude > 0.0004f || (right - lastRight).sqrMagnitude > 0.0004f)
            {
                lastLeft = left;
                lastRight = right;
                MelonLogger.Msg($"[Thumbstick] L=({left.x:0.00}, {left.y:0.00})  R=({right.x:0.00}, {right.y:0.00})");
            }

            if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstickUp)) MelonLogger.Msg("[Thumbstick] L UP down");
            if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstickDown)) MelonLogger.Msg("[Thumbstick] L DOWN down");
            if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstickLeft)) MelonLogger.Msg("[Thumbstick] L LEFT down");
            if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstickRight)) MelonLogger.Msg("[Thumbstick] L RIGHT down");
        }
    }
}
