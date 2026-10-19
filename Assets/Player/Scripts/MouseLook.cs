using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Camera))]
public class MouseLook : MonoBehaviour
{
    public float mouseSensitivity = 200f;

    [Header("PlayerMovement가 붙은 플레이어 연결")]
    public Transform playerBody;
    public PlayerMovement movement;

    [Header("어깨너머 카메라 위치")]
    public float followHeight = 1.6f;
    public float shoulderOffset = 0.65f;

    [Min(0.1f)]
    public float distance = 3f;

    [Header("위아래 회전 제한")]
    public float minimumPitch = -55f;
    public float maximumPitch = 70f;

    [Header("우클릭 조준 줌")]
    public bool enableAimZoom = true;

    [Range(10f, 100f)]
    public float aimFieldOfView = 45f;

    [Min(0.1f)]
    public float zoomSmoothSpeed = 12f;

    [Range(0.1f, 1f)]
    public float aimSensitivityMultiplier = 0.65f;

    [Header("카메라와 벽 사이 충돌 확인")]
    public LayerMask cameraObstacleLayers = ~0;

    [Min(0.01f)]
    public float collisionRadius = 0.15f;

    [Min(0f)]
    public float wallPadding = 0.05f;

    private float yaw;
    private float pitch;
    private float currentCameraDrop;

    private Camera viewCamera;
    private float normalFieldOfView;
    private PlayerHealth health;

    public Vector3 PlanarForward =>
        Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

    public Vector3 PlanarRight =>
        Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

    private bool CanLook =>
        playerBody != null &&
        Time.timeScale > 0f &&
        (health == null || !health.IsDead) &&
        Cursor.lockState == CursorLockMode.Locked;

    private bool ShouldZoom =>
        enableAimZoom &&
        CanLook &&
        movement != null &&
        movement.isActiveAndEnabled &&
        !movement.IsDodging &&
        Input.GetMouseButton(1);

    private void Awake()
    {
        viewCamera = GetComponent<Camera>();

        // 起動時のカメラ設定を通常の視野角として保存します。
        normalFieldOfView = viewCamera.fieldOfView;

        yaw = transform.eulerAngles.y;
        pitch = Mathf.DeltaAngle(0f, transform.eulerAngles.x);
        pitch = Mathf.Clamp(pitch, minimumPitch, maximumPitch);
    }

    private void Start()
    {
        if (playerBody == null)
        {
            Debug.LogError(
                "MouseLook: Player Body에 플레이어를 연결해 주세요.",
                this
            );

            enabled = false;
            return;
        }

        if (movement == null)
            movement = playerBody.GetComponent<PlayerMovement>();

        health = playerBody.GetComponent<PlayerHealth>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (!CanLook)
            return;

        float sensitivity = mouseSensitivity;

        if (ShouldZoom)
            sensitivity *= aimSensitivityMultiplier;

        yaw += Input.GetAxis("Mouse X") *
               sensitivity * Time.deltaTime;

        pitch -= Input.GetAxis("Mouse Y") *
                 sensitivity * Time.deltaTime;

        pitch = Mathf.Clamp(
            pitch,
            minimumPitch,
            maximumPitch
        );
    }

    private void LateUpdate()
    {
        if (playerBody == null || Time.timeScale == 0f)
            return;

        UpdateZoom();
        UpdateCameraPosition();
    }

    private void UpdateZoom()
    {
        // 조준 FOV가 기본 FOV보다 커져서
        // 오히려 축소되는 것을 방지합니다.
        float zoomedFieldOfView = Mathf.Min(
            aimFieldOfView,
            normalFieldOfView
        );

        float targetFieldOfView = ShouldZoom
            ? zoomedFieldOfView
            : normalFieldOfView;

        float blend = 1f - Mathf.Exp(
            -zoomSmoothSpeed * Time.deltaTime
        );

        viewCamera.fieldOfView = Mathf.Lerp(
            viewCamera.fieldOfView,
            targetFieldOfView,
            blend
        );
    }

    private void UpdateCameraPosition()
    {
        float targetDrop =
            movement != null && movement.IsCrouching
                ? movement.cameraDrop
                : 0f;

        float smoothSpeed = movement != null
            ? movement.cameraSmoothSpeed
            : 12f;

        float blend = 1f - Mathf.Exp(
            -smoothSpeed * Time.deltaTime
        );

        currentCameraDrop = Mathf.Lerp(
            currentCameraDrop,
            targetDrop,
            blend
        );

        Quaternion rotation = Quaternion.Euler(
            pitch, yaw, 0f
        );

        Vector3 pivot = playerBody.position +
            Vector3.up * (followHeight - currentCameraDrop);

        Vector3 offset = rotation * new Vector3(
            shoulderOffset, 0f, -distance
        );

        float allowedDistance = offset.magnitude;
        Vector3 direction = offset.normalized;

        RaycastHit[] hits = Physics.SphereCastAll(
            pivot,
            collisionRadius,
            direction,
            allowedDistance,
            cameraObstacleLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == playerBody ||
                hit.transform.IsChildOf(playerBody))
            {
                continue;
            }

            allowedDistance = Mathf.Min(
                allowedDistance,
                Mathf.Max(0f, hit.distance - wallPadding)
            );
        }

        transform.SetPositionAndRotation(
            pivot + direction * allowedDistance,
            rotation
        );
    }

    private void OnDisable()
    {
        if (viewCamera != null)
            viewCamera.fieldOfView = normalFieldOfView;
    }
}