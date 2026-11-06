using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class FinishLine : MonoBehaviour
{
    private BoxCollider trigger;

    private void Awake()
    {
        ConfigureTrigger();
    }

    private void OnValidate()
    {
        ConfigureTrigger();
    }

    private void ConfigureTrigger()
    {
        if (trigger == null)
            trigger = GetComponent<BoxCollider>();
        if (trigger == null)
            return;

        trigger.isTrigger = true;
        if (trigger.size == Vector3.zero)
            trigger.size = new Vector3(13f, 5f, 3f);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryFinish(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryFinish(other);
    }

    private static void TryFinish(Collider other)
    {
        OffRoadVehicleController vehicle = other.GetComponentInParent<OffRoadVehicleController>();
        if (vehicle == null || vehicle.IsAIControlled)
            return;

        OffRoadRaceDirector.Instance?.OnFinishLine(vehicle);
    }
}
