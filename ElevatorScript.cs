using System;
using System.Collections;
using MelonLoader;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD_Mod2Code
{
    public class ElevatorScript : MonoBehaviour
    {
        public ElevatorScript(IntPtr p) : base(p) { }
        public ElevatorScript() : base(ClassInjector.DerivedConstructorPointer<ElevatorScript>()) =>
            ClassInjector.DerivedConstructorBody(this);

        public string ElevatorFloorName = "ElevatorFloor";
        public string Beam1PivotName = "Beam1Pivot";
        public string Beam2PivotName = "Beam2Pivot";
        public string VRRigName = "VRRig";

        public float GroundY = 0f;
        public float BasementY = -3.443f;

        public float Beam1GroundZ = -70f;
        public float BeamBasementZ = 0f;

        public float MoveDuration = 8.0f;
        public AnimationCurve FloorCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private Transform _floor, _beam1, _beam2, _vrRig;
        private object _routineToken;

        private void Start()
        {
            _floor = GameObject.Find(ElevatorFloorName)?.transform;
            _beam1 = GameObject.Find(Beam1PivotName)?.transform;
            _beam2 = GameObject.Find(Beam2PivotName)?.transform;
            _vrRig = GameObject.Find(VRRigName)?.transform;
        }

        public void Descend()
        {
            if (!Valid()) return;
            Restart(MoveElevator(BasementY));
        }

        public void Ascend()
        {
            if (!Valid()) return;
            Restart(MoveElevator(GroundY));
        }

        private bool Valid() => _floor != null && _beam1 != null && _beam2 != null;

        private void Restart(IEnumerator r)
        {
            if (_routineToken != null) MelonCoroutines.Stop(_routineToken);
            _routineToken = MelonCoroutines.Start(r);
        }

        private IEnumerator MoveElevator(float targetFloorY)
        {
            Vector3 floorStart = _floor.localPosition;
            Vector3 floorEnd = new Vector3(floorStart.x, targetFloorY, floorStart.z);

            float elapsed = 0f;
            float lastFloorY = floorStart.y;

            while (elapsed < MoveDuration)
            {
                float u = (MoveDuration <= 0.0001f) ? 1f : Mathf.Clamp01(elapsed / MoveDuration);
                float fu = (FloorCurve != null) ? FloorCurve.Evaluate(u) : u;

                Vector3 newFloor = Vector3.LerpUnclamped(floorStart, floorEnd, fu);
                _floor.localPosition = newFloor;

                float beam1Z = Beam1ZFromFloorY(newFloor.y);
                float beam2Z = -beam1Z;

                SetLocalZ(_beam1, beam1Z);
                SetLocalZ(_beam2, beam2Z);

                if (_vrRig != null)
                {
                    float dy = newFloor.y - lastFloorY;
                    _vrRig.position += _floor.up * dy;
                    lastFloorY = newFloor.y;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            _floor.localPosition = floorEnd;

            float finalBeam1Z = Beam1ZFromFloorY(targetFloorY);
            SetLocalZ(_beam1, finalBeam1Z);
            SetLocalZ(_beam2, -finalBeam1Z);

            _routineToken = null;
        }

        private float Beam1ZFromFloorY(float y)
        {

            float yClamped = Mathf.Clamp(y, BasementY, GroundY);

            if (yClamped >= -0.44f)
                return LerpByY(yClamped, 0f, Beam1GroundZ, -0.44f, -45f);

            if (yClamped >= -1.19f)
                return LerpByY(yClamped, -0.44f, -45f, -1.19f, -22f);

            if (yClamped >= -2.17f)
                return LerpByY(yClamped, -1.19f, -22f, -2.17f, -7f);

            return LerpByY(yClamped, -2.17f, -7f, BasementY, BeamBasementZ);
        }

        private static float LerpByY(float y, float yA, float zA, float yB, float zB)
        {

            float t = Mathf.InverseLerp(yA, yB, y);
            return Mathf.Lerp(zA, zB, t);
        }

        private static void SetLocalZ(Transform t, float z)
        {
            var e = t.localEulerAngles;
            e.z = z;
            t.localEulerAngles = e;
        }
    }
}
