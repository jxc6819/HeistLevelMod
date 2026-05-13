namespace IEYTD_Mod2Code
{
    public interface IDroneGrabbable
    {
        void OnDroneGrabBegin(DroneHand hand);
        void OnDroneGrabUpdate(DroneHand hand);
        void OnDroneGrabEnd(DroneHand hand);
    }
}
