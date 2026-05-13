using MelonLoader;
using System;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class SubGear : MonoBehaviour
    {
        public SubGear(IntPtr ptr) : base(ptr) { }
        public SubGear() : base(ClassInjector.DerivedConstructorPointer<SubGear>())
            => ClassInjector.DerivedConstructorBody(this);

        public MainGear mainGear;
        public InfinitePlayerWheelTK vaultWheel;
        public Vector3 rotDirection = new Vector3(0, 0, 1);
        public float speed = 1f;
        public VaultPuzzleManager vaultPuzzleManager;

        public float gearSoundStartThreshold = 0.5f;
        public float gearSoundStopThreshold = 0.5f;
        public float wheelSoundStartThreshold = 0.035f;
        public float wheelSoundStopThreshold = 0.015f;
        public float redLightAngle;

        private bool _gearSoundOn;
        private bool _wheelSoundOn;

        void Update()
        {
            float mainGearDif = 0f;
            float vaultWheelDif = 0f;

            if (mainGear != null)
                mainGearDif = mainGear.rot - mainGear.lastRot;

            if (vaultWheel != null)
                vaultWheelDif = vaultWheel.rot - vaultWheel.lastRot;

            float dif = mainGearDif + vaultWheelDif;
            float absDif = Mathf.Abs(dif);
            float absWheelDif = Mathf.Abs(vaultWheelDif);

            bool gearSpinning = _gearSoundOn
                ? absDif >= gearSoundStopThreshold
                : absDif >= gearSoundStartThreshold;

            bool wheelSpinning = _wheelSoundOn
                ? absWheelDif >= wheelSoundStopThreshold
                : absWheelDif >= wheelSoundStartThreshold;

            if (gearSpinning)
            {
                if (!_gearSoundOn)
                {
                    _gearSoundOn = true;
                    TurnOnGearSound();
                }
            }
            else
            {
                if (_gearSoundOn)
                {
                    _gearSoundOn = false;
                    TurnOffGearSound();
                }
            }

            if (wheelSpinning)
            {
                if (!_wheelSoundOn)
                {
                    _wheelSoundOn = true;
                    TurnOnWheelSound();
                }
            }
            else
            {
                if (_wheelSoundOn)
                {
                    _wheelSoundOn = false;
                    TurnOffWheelSound();
                }
            }

            if (absDif < 0.000001f) return;

            dif *= (speed * 0.4f);

            transform.Rotate(
                rotDirection.x * -dif,
                rotDirection.y * -dif,
                rotDirection.z * -dif
            );
        }

        public void TurnOnGearSound()
        {
            vaultPuzzleManager.TurnOnGearSound();
        }

        public void TurnOffGearSound()
        {
            vaultPuzzleManager.TurnOffGearSound();
        }

        public void TurnOnWheelSound()
        {
            vaultPuzzleManager.TurnOnWheelSound();
        }

        public void TurnOffWheelSound()
        {
            vaultPuzzleManager.TurnOffWheelSound();
        }

        public void RotateTo(Quaternion targetLocalRot, bool green)
        {
            MelonCoroutines.Start(RotateRoutine(targetLocalRot, 0.5f, green));
            AudioUtil.PlayAt("DialAdjustment.ogg", transform.position);
        }

        private System.Collections.IEnumerator RotateRoutine(Quaternion targetLocalRot, float duration, bool green)
        {
            Quaternion startRot = transform.localRotation;
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                float progress = t / duration;

                transform.localRotation = Quaternion.Slerp(startRot, targetLocalRot, progress);

                yield return null;
            }

            transform.localRotation = targetLocalRot;
            if (green) AudioUtil.PlayAt("DialCorrect.ogg", transform.position, 1);
        }
    }
}
