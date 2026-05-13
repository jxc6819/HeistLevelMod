using SG.Phoenix.Assets.Code.InputManagement;
using System;
using UnhollowerRuntimeLib;
using UnityEngine;
using MelonLoader;
using SG.Phoenix.Assets.Code.Interactables;

namespace IEYTD_Mod2Code
{
    public class DroneVentScript : MonoBehaviour
    {
        public DroneVentScript(IntPtr ptr) : base(ptr) { }
        public DroneVentScript() : base(ClassInjector.DerivedConstructorPointer<DroneVentScript>())
            => ClassInjector.DerivedConstructorBody(this);

        public float ShutterZ = 2.880565f;
        public float ShutterBuffer = 0.25f;
        public float EndZ = 11.0622f;

        public bool ShuttersOpen = false;

        public Vector3 VentStartPos = new Vector3(0.0571f, 8.1911f, 0.4597f);
        public bool UseFixedStartPos = true;

        public float StickSpeed = 2.2f;
        public float StickDeadzone = 0.15f;

        public float HandDriveGain = 1.0f;
        public float HandDeadzone = 0.0025f;

        public GameObject Hand;
        GameObject _RightHand;
        GameObject _LeftHand;

        public GameObject Drone;

        LineRenderer _tkLR;

        GameObject _ventBeamGO;
        LineRenderer _ventLR;
        PickUp _dronePU;
        Rigidbody _rb;
        GameObject RightReticle;
        GameObject LeftReticle;
        GameObject VRRig;

        bool _inVentMode;
        bool _beamEnabled;
        bool holding;

        float _fixedX;
        float _fixedY;

        bool _useRightController = true;

        VRHandInput v;

        public void Start()
        {
            v = GameObject.Find("RightHandRoot").GetComponent<VRHandInput>();
            Drone = gameObject;
            _rb = Drone.GetComponent<Rigidbody>();

            _RightHand = GameObject.Find("RightHandRoot");
            _LeftHand = GameObject.Find("LeftHandRoot");

            Hand = _RightHand != null ? _RightHand : _LeftHand;
            _useRightController = (Hand == _RightHand);

            _tkLR = FindTelekinesisBeamLineRenderer(Hand);

            _ventBeamGO = new GameObject("VentBeam");
            _ventBeamGO.transform.SetParent(Hand.transform, false);
            _ventLR = _ventBeamGO.AddComponent<LineRenderer>();
            _ventLR.useWorldSpace = true;
            _dronePU = Drone.GetComponent<PickUp>();
            _fixedX = VentStartPos.x;
            _fixedY = VentStartPos.y;

            VRRig = GameObject.Find("VRRig");
            LeftReticle = VRRig.transform.GetChild(5).GetChild(0).gameObject;
            RightReticle = VRRig.transform.GetChild(10).GetChild(0).gameObject;

            if (_tkLR != null)
            {
                _ventLR.sharedMaterial = _tkLR.sharedMaterial;
                _ventLR.widthMultiplier = _tkLR.widthMultiplier;
                _ventLR.widthCurve = _tkLR.widthCurve;
                _ventLR.colorGradient = _tkLR.colorGradient;
                _ventLR.textureMode = _tkLR.textureMode;
                _ventLR.numCapVertices = _tkLR.numCapVertices;
                _ventLR.numCornerVertices = _tkLR.numCornerVertices;

                _ventLR.positionCount = _tkLR.positionCount;
                if (_ventLR.positionCount < 2) _ventLR.positionCount = 2;
            }
            else
            {
                _ventLR.positionCount = 15;
                _ventLR.numCapVertices = 6;
            }

            _ventLR.enabled = false;
        }

        void Update()
        {

            v.SetTelekinesisAbilityEnabled(true);
            if (!_inVentMode)
                return;

            if (!holding && _dronePU.isHeld)
            {
                RePickUp();
            }

            if(holding && !IsHolding())
            {
                holding = false;
                _rb.isKinematic = false;
                DisableBeam();
                EnableReticles();
            }

            if (_beamEnabled)
                UpdateBeam();

        }

        void EnteredVent()
        {
            if(_dronePU.isHeld)
            {
                MelonLogger.Msg("[EnteredVent] - isHeld");
                Hand = _dronePU.heldHand.gameObject;
                _useRightController = (Hand == _RightHand);
                DisableReticle(_useRightController);
                holding = true;
                _inVentMode = true;
            }
            Hand.GetComponent<VRHandInput>().ReleaseHeldObject();
            Drone.transform.position = VentStartPos;

            _rb.isKinematic = true;
            EnableBeam();
        }

        void RePickUp()
        {
            if (!_dronePU.isHeld) return;
            Hand = _dronePU.heldHand.gameObject;
            _useRightController = (Hand == _RightHand);
            DisableReticle(_useRightController);
            holding = true;
            Hand.GetComponent<VRHandInput>().ReleaseHeldObject();
            _rb.isKinematic = true;
            Drone.transform.position = new Vector3(_fixedX, _fixedY, Drone.transform.position.z);
            EnableBeam();
        }

        void ExitedVent()
        {
            _inVentMode = false;
        }

        public void EnableBeam()
        {
            MelonLogger.Msg("EnableBeam");
            _beamEnabled = true;
            if (_ventLR != null) _ventLR.enabled = true;
        }

        public void DisableBeam()
        {
            MelonLogger.Msg("DisableBeam");
            _beamEnabled = false;
            if (_ventLR != null) _ventLR.enabled = false;

        }

        void DisableReticle(bool rightHand)
        {
            if(rightHand)
            {
                RightReticle.SetActive(false);
            }
            else LeftReticle.SetActive(false);
        }

        void EnableReticles()
        {
            RightReticle.SetActive(true);
            LeftReticle.SetActive(true);
        }

        void UpdateBeam()
        {
            if (_ventLR == null || Hand == null || Drone == null)
                return;

            int n = _ventLR.positionCount;
            if (n < 2) n = 2;

            Vector3 a = Hand.transform.position;
            Vector3 b = Drone.transform.position;

            for (int i = 0; i < n; i++)
            {
                float t = (n == 1) ? 1f : (float)i / (float)(n - 1);
                _ventLR.SetPosition(i, Vector3.Lerp(a, b, t));
            }
        }

        float GetStickY()
        {

            if (_useRightController)
                return OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch).y;
            else
                return OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch).y;
        }

        bool IsHolding()
        {

            bool grip, trig;

            if (_useRightController)
            {
                grip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch) > 0.2f;
                trig = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch) > 0.2f;
            }
            else
            {
                grip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.LTouch) > 0.2f;
                trig = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch) > 0.2f;
            }

            return grip || trig;
        }

        public void SimulateSchellGrab()
        {
            MelonLogger.Msg("Simulate Schell Grab");
            VRHandInput vhi = Hand.GetComponent<VRHandInput>();
            GameObject ReticleTarget = Hand.transform.GetChild(2).gameObject;
            GameObject ReticleVis;
            if (Hand == _RightHand) ReticleVis = RightReticle.transform.parent.gameObject;
            else ReticleVis = LeftReticle.transform.parent.gameObject;

            ReticleVis.SetActive(true);
            ReticleTarget.transform.position = Drone.transform.position;

        }

        void OnTriggerEnter(Collider other)
        {
            if (_inVentMode) return;

            if (other != null && other.name == "VentCollider")
            {
                EnteredVent();
            }
        }

        void OnTriggerExit(Collider other)
        {
            if(other != null && other.name == "VentCollider")
            {

            }
        }

        LineRenderer FindTelekinesisBeamLineRenderer(GameObject handRoot)
        {
            if (handRoot == null) return null;

            Transform beamT = FindChildRecursive(handRoot.transform, "TelekinesisBeam");
            if (beamT == null) return null;

            return beamT.GetComponent<LineRenderer>();
        }

        Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;

            int c = root.childCount;
            for (int i = 0; i < c; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
