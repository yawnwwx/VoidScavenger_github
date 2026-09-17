using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class TempThirdPersonController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float sprintSpeed = 9f;
    public float gravity = -20f;
    public float jumpHeight = 1.2f;

    [Header("Camera Look")]
    public Transform cameraPivot;
    public Transform mainCamera;
    public float mouseSensitivity = 2f;
    public float minLookX = -35f;
    public float maxLookX = 65f;

    [Header("Shoulder Camera")]
    public float cameraDistance = 4f;
    public float cameraSideOffset = 0.8f;
    public float cameraHeightOffset = 0.4f;
    public float cameraLocalPitch = 10f;

    [Header("Camera Collision")]
    public LayerMask cameraCollisionMask = Physics.DefaultRaycastLayers;
    public float cameraCollisionRadius = 0.25f;
    public float cameraCollisionPadding = 0.15f;
    public float cameraMinDistance = 0.7f;
    public float cameraSmooth = 20f;

    private CharacterController controller;
    private Vector3 velocity;
    private float cameraPitch;
    private bool cursorLocked = true;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cameraPivot == null)
        {
            Transform foundPivot = transform.Find("CameraPivot");
            if (foundPivot != null)
            {
                cameraPivot = foundPivot;
            }
        }

        if (mainCamera == null && cameraPivot != null)
        {
            Camera foundCamera = cameraPivot.GetComponentInChildren<Camera>();
            if (foundCamera != null)
            {
                mainCamera = foundCamera.transform;
            }
        }
    }

    private void Start()
    {
        LockCursor(true);
    }

    private void Update()
    {
        HandleCursorToggle();

        if (cursorLocked)
        {
            Look();
        }

        Move();
    }

    private void LateUpdate()
    {
        UpdateCameraCollision();
    }

    private void HandleCursorToggle()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            LockCursor(!cursorLocked);
        }
    }

    private void LockCursor(bool locked)
    {
        cursorLocked = locked;

        if (locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void Look()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, minLookX, maxLookX);

        if (cameraPivot != null)
        {
            cameraPivot.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        }
    }

    private void Move()
    {
        bool isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        float inputX = Input.GetAxisRaw("Horizontal");
        float inputZ = Input.GetAxisRaw("Vertical");

        Vector3 moveDirection = transform.right * inputX + transform.forward * inputZ;
        moveDirection = Vector3.ClampMagnitude(moveDirection, 1f);

        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : moveSpeed;

        controller.Move(moveDirection * currentSpeed * Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void UpdateCameraCollision()
    {
        if (cameraPivot == null || mainCamera == null)
        {
            return;
        }

        Vector3 desiredLocalPosition = new Vector3(
            cameraSideOffset,
            cameraHeightOffset,
            -cameraDistance
        );

        Vector3 pivotPosition = cameraPivot.position;
        Vector3 desiredWorldPosition = cameraPivot.TransformPoint(desiredLocalPosition);

        Vector3 direction = desiredWorldPosition - pivotPosition;
        float desiredDistance = direction.magnitude;

        if (desiredDistance <= 0.01f)
        {
            return;
        }

        direction.Normalize();

        float finalDistance = desiredDistance;

        if (Physics.SphereCast(
            pivotPosition,
            cameraCollisionRadius,
            direction,
            out RaycastHit hit,
            desiredDistance,
            cameraCollisionMask,
            QueryTriggerInteraction.Ignore))
        {
            finalDistance = Mathf.Clamp(
                hit.distance - cameraCollisionPadding,
                cameraMinDistance,
                desiredDistance
            );
        }

        Vector3 finalCameraPosition = pivotPosition + direction * finalDistance;

        if (cameraSmooth <= 0f)
        {
            mainCamera.position = finalCameraPosition;
        }
        else
        {
            mainCamera.position = Vector3.Lerp(
                mainCamera.position,
                finalCameraPosition,
                1f - Mathf.Exp(-cameraSmooth * Time.deltaTime)
            );
        }

        mainCamera.localRotation = Quaternion.Euler(cameraLocalPitch, 0f, 0f);
    }
}