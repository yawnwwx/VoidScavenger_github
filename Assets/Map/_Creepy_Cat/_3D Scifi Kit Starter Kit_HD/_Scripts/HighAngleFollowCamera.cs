using UnityEngine;

[RequireComponent(typeof(Camera))]
public class HighAngleFollowCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Camera Position")]
    public Vector3 localOffset = new Vector3(0f, 10f, -7f);
    public float lookHeight = 1f;
    public float lookAheadDistance = 2.5f;

    [Header("Camera Settings")]
    public float fieldOfView = 55f;

    [Header("Rotation Follow")]
    public bool smoothYaw = true;
    public float yawSmooth = 18f;

    [Header("Target Follow")]
    public bool smoothTargetPosition = false;
    public float targetPositionSmooth = 30f;

    private Camera cam;
    private float currentYaw;
    private Vector3 currentTargetPosition;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        if (target != null)
        {
            currentYaw = target.eulerAngles.y;
            currentTargetPosition = target.position;
        }

        ApplyCameraSettings();
        UpdateCameraInstant();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        UpdateTargetPosition();
        UpdateYaw();
        UpdateCameraPosition();
        ApplyCameraSettings();
    }

    private void UpdateTargetPosition()
    {
        if (smoothTargetPosition)
        {
            currentTargetPosition = Vector3.Lerp(
                currentTargetPosition,
                target.position,
                1f - Mathf.Exp(-targetPositionSmooth * Time.deltaTime)
            );
        }
        else
        {
            currentTargetPosition = target.position;
        }
    }

    private void UpdateYaw()
    {
        float targetYaw = target.eulerAngles.y;

        if (smoothYaw)
        {
            currentYaw = Mathf.LerpAngle(
                currentYaw,
                targetYaw,
                1f - Mathf.Exp(-yawSmooth * Time.deltaTime)
            );
        }
        else
        {
            currentYaw = targetYaw;
        }
    }

    private void UpdateCameraPosition()
    {
        Quaternion yawRotation = Quaternion.Euler(0f, currentYaw, 0f);

        Vector3 cameraPosition = currentTargetPosition + yawRotation * localOffset;
        Vector3 lookPoint = currentTargetPosition + yawRotation * new Vector3(0f, lookHeight, lookAheadDistance);

        transform.position = cameraPosition;
        transform.rotation = Quaternion.LookRotation((lookPoint - transform.position).normalized, Vector3.up);
    }

    private void UpdateCameraInstant()
    {
        if (target == null)
        {
            return;
        }

        currentYaw = target.eulerAngles.y;
        currentTargetPosition = target.position;

        Quaternion yawRotation = Quaternion.Euler(0f, currentYaw, 0f);

        transform.position = currentTargetPosition + yawRotation * localOffset;

        Vector3 lookPoint = currentTargetPosition + yawRotation * new Vector3(0f, lookHeight, lookAheadDistance);
        transform.rotation = Quaternion.LookRotation((lookPoint - transform.position).normalized, Vector3.up);
    }

    private void ApplyCameraSettings()
    {
        if (cam == null)
        {
            return;
        }

        cam.orthographic = false;
        cam.fieldOfView = fieldOfView;
    }
}