using UnityEngine;
using UnityEngine.InputSystem;

public sealed class OffRoadFollowCamera : MonoBehaviour
{
    private Transform target;
    private int viewIndex;
    private Vector3 velocity;
    private float finishCinematicRemaining;
    private float finishCinematicElapsed;

    public string ViewName => viewIndex == 0 ? "CHASE CAM" : viewIndex == 1 ? "BONNET CAM" : "COCKPIT CAM";

    public void SetTarget(Transform targetTransform)
    {
        target = targetTransform;
        transform.SetPositionAndRotation(target.position - target.forward * 8f + Vector3.up * 4f,
            Quaternion.LookRotation(target.position - transform.position));
        Camera camera = GetComponent<Camera>();
        camera.fieldOfView = 65f;
        camera.nearClipPlane = 0.08f;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;
        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            CycleView();

        if (finishCinematicRemaining > 0f)
        {
            finishCinematicElapsed += Time.unscaledDeltaTime;
            finishCinematicRemaining -= Time.unscaledDeltaTime;
            float angle = finishCinematicElapsed * 34f;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 4.2f, -8.5f);
            Vector3 desiredPosition = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition,
                Time.unscaledDeltaTime * 2.5f);
            Vector3 lookPoint = target.position + Vector3.up * 1.15f;
            Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation,
                Time.unscaledDeltaTime * 3f);
            return;
        }

        if (viewIndex == 0)
        {
            Vector3 desiredPosition = target.position - target.forward * 8.2f + Vector3.up * 3.4f;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, 0.2f);
            Vector3 lookPoint = target.position + target.forward * 2f + Vector3.up * 1.05f;
            Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * 6f);
        }
        else
        {
            Vector3 localPosition = viewIndex == 1
                ? new Vector3(0f, 1.65f, 1.5f)
                : new Vector3(0f, 1.82f, 0.65f);
            Vector3 desiredPosition = target.TransformPoint(localPosition);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * 12f);
            transform.rotation = Quaternion.Slerp(transform.rotation, target.rotation, Time.deltaTime * 10f);
        }
    }

    public void CycleView()
    {
        if (finishCinematicRemaining > 0f)
            return;
        viewIndex = (viewIndex + 1) % 3;
    }

    public void BeginFinishCinematic()
    {
        viewIndex = 0;
        finishCinematicElapsed = 0f;
        finishCinematicRemaining = 5f;
    }
}
