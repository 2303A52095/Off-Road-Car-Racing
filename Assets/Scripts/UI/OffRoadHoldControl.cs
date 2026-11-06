using UnityEngine;
using UnityEngine.EventSystems;

public sealed class OffRoadHoldControl : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private OffRoadVehicleController vehicle;
    private OffRoadVehicleControl control;

    public void Initialize(OffRoadVehicleController target, OffRoadVehicleControl vehicleControl)
    {
        vehicle = target;
        control = vehicleControl;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        vehicle.SetControlHeld(control, true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        vehicle.SetControlHeld(control, false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        vehicle.SetControlHeld(control, false);
    }

    private void OnDisable()
    {
        if (vehicle != null)
            vehicle.SetControlHeld(control, false);
    }
}
