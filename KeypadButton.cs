using MelonLoader;
using System.Collections;
using UnhollowerBaseLib.Attributes;
using UnhollowerRuntimeLib;
using UnityEngine;
using System;

namespace IEYTD_Mod2Code
{
    public class KeypadButton : MonoBehaviour
    {
        public KeypadButton(IntPtr p) : base(p) { }
        public KeypadButton() : base(ClassInjector.DerivedConstructorPointer<KeypadButton>())
            => ClassInjector.DerivedConstructorBody(this);

        public string value;
        private float buttonSpeed = 0.1f;
        private float pressDistance = 0.0025f;
        private float buttonHoldTime = 0.1f;

        float pressCooldown = 0.18f;

        float maxPressTime = 0.35f;

        public Keypad keypad;
        public static bool headsetOn;

        bool pressed = false;
        GameObject pressingObject;
        bool moving;
        float releaseAt;
        float nextPressTime;

        void Awake()
        {
            if (keypad == null)
            {
                keypad = GetComponentInParent<Keypad>();
            }
        }

        void Update()
        {

            if (pressed && Time.time >= releaseAt)
            {
                pressed = false;
                pressingObject = null;
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (!headsetOn) return;
            if (other == null) return;

            if (!IsDroneHandButtonPresser(other)) return;

            if (Time.time < nextPressTime) return;
            if (pressed) return;

            pressed = true;
            pressingObject = other.gameObject;
            releaseAt = Time.time + maxPressTime;
            nextPressTime = Time.time + pressCooldown;

            PressButton();
        }

        void OnTriggerExit(Collider other)
        {
            if (!pressed) return;
            if (other == null) return;
            if (other.gameObject != pressingObject) return;

            pressed = false;
            pressingObject = null;
        }

        private bool IsDroneHandButtonPresser(Collider other)
        {
            GameObject go = other.gameObject;
            if (go == null) return false;

            string n = go.name;
            if (n == "SM_handdrone_R_low") return true;
            if (n == "SM_handdrone_L_low") return true;

            return false;
        }

        public void PressButton()
        {
            if (moving) return;

            if (keypad == null)
            {
                keypad = GetComponentInParent<Keypad>();
                if (keypad == null)
                {
                    MelonLogger.Warning("[KeypadButton] No Keypad found for button '" + gameObject.name + "' value='" + value + "'");
                    return;
                }
            }

            AudioUtil.PlayAt("ButtonPress.ogg", transform.position);
            keypad.AddInput(value);
            MelonCoroutines.Start(MoveSmooth());
        }

        [HideFromIl2Cpp]
        private IEnumerator MoveSmooth()
        {
            moving = true;

            Vector3 restPos = transform.localPosition;
            Vector3 pressedPos = restPos + new Vector3(0, 0, pressDistance);

            float elapsedTime = 0f;
            while (elapsedTime < buttonSpeed)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / buttonSpeed);

                transform.localPosition = Vector3.Lerp(restPos, pressedPos, t);

                yield return null;
            }

            transform.localPosition = pressedPos;

            yield return new WaitForSeconds(buttonHoldTime);

            elapsedTime = 0f;
            while (elapsedTime < buttonSpeed)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / buttonSpeed);

                transform.localPosition = Vector3.Lerp(pressedPos, restPos, t);

                yield return null;
            }

            transform.localPosition = restPos;
            moving = false;
        }
    }
}
