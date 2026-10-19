using UnityEngine;

[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("카메라의 MouseLook 연결")]
    public MouseLook mouseLook;

    [Header("루트 모션")]
    [Min(0.01f)]
    public float rootMotionMultiplier = 1f;

    public float gravity = -9.81f;

    [Min(1f)]
    public float turnSpeed = 720f;

    [Min(0f)]
    public float animationSmoothTime = 0.1f;

    [Header("걷기 / 전력 질주")]
    public KeyCode walkKey = KeyCode.LeftAlt;
    public KeyCode sprintKey = KeyCode.LeftShift;

    [Header("발사 후 조준 자세 유지 시간")]
    [Min(0f)]
    public float shootingAimHoldTime = 0.5f;

    [Header("웅크리기")]
    public KeyCode crouchKey = KeyCode.LeftControl;

    [Min(0.1f)]
    public float crouchSpeed = 2.5f;

    [Min(0.1f)]
    public float crouchHeight = 1.2f;

    [Min(0f)]
    public float cameraDrop = 0.8f;

    [Min(0.1f)]
    public float cameraSmoothSpeed = 12f;

    [Header("일어설 공간 확인")]
    public LayerMask standingObstacleLayers = ~0;

    [Header("Q 구르기")]
    public KeyCode dodgeKey = KeyCode.Q;

    [Min(0.1f)]
    public float dodgeDistance = 3f;

    [Min(0.1f)]
    public float dodgeDuration = 0.9f;

    [Min(0f)]
    public float dodgeCooldown = 1.5f;

    public string dodgeTrigger = "Dodge";

    [Header("Animator의 이동 상태 이름")]
    public string normalMoveState = "Grounded";
    public string aimingMoveState = "Aiming";

    private CharacterController controller;
    private Animator anim;
    private PlayerHealth health;

    private float verticalVelocity;
    private float standingHeight;
    private Vector3 standingCenter;
    private float standingStepOffset;

    private Vector3 dodgeDirection;
    private float dodgeTimeRemaining;
    private float nextDodgeTime;
    private float shootingAimUntil;

    private Vector3 frameDisplacement;
    private bool useRootMotionThisFrame;
    private bool hasDodgeTrigger;
    private int lastMovedFrame = -1;

    public bool IsCrouching { get; private set; }
    public bool IsDodging { get; private set; }
    public bool IsAiming { get; private set; }
    public bool IsSprinting { get; private set; }

    private bool CanControl =>
        isActiveAndEnabled &&
        controller != null &&
        controller.enabled &&
        Time.timeScale > 0f &&
        (health == null || !health.IsDead);

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        anim = GetComponent<Animator>();
        health = GetComponent<PlayerHealth>();

        standingHeight = controller.height;
        standingCenter = controller.center;
        standingStepOffset = controller.stepOffset;

        float minimumHeight = controller.radius * 2f + 0.01f;
        crouchHeight = Mathf.Min(
            standingHeight,
            Mathf.Max(minimumHeight, crouchHeight)
        );

        if (anim == null || anim.runtimeAnimatorController == null)
        {
            Debug.LogError(
                "PlayerMovement: 같은 오브젝트의 Animator와 " +
                "Animator Controller를 확인해 주세요.",
                this
            );
            enabled = false;
            return;
        }

        anim.applyRootMotion = true;
        anim.updateMode = AnimatorUpdateMode.Normal;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        foreach (AnimatorControllerParameter parameter in anim.parameters)
        {
            if (parameter.name == dodgeTrigger &&
                parameter.type == AnimatorControllerParameterType.Trigger)
            {
                hasDodgeTrigger = true;
                break;
            }
        }
    }

    private void Start()
    {
        if (mouseLook == null && Camera.main != null)
            mouseLook = Camera.main.GetComponent<MouseLook>();

        if (mouseLook == null)
        {
            Debug.LogError(
                "PlayerMovement: Mouse Look에 메인 카메라의 " +
                "MouseLook을 연결해 주세요.",
                this
            );
            enabled = false;
        }
    }

    private void Update()
    {
        frameDisplacement = Vector3.zero;
        useRootMotionThisFrame = false;

        if (!CanControl || mouseLook == null)
            return;

        if (IsDodging)
        {
            UpdateDodge();
        }
        else
        {
            UpdateCrouch();
            UpdateMovementInput();

            if (Input.GetKeyDown(dodgeKey) && CanDodge())
            {
                StartDodge();
                UpdateDodge();
            }
        }

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += gravity * Time.deltaTime;
        frameDisplacement.y = verticalVelocity * Time.deltaTime;

        // Animator가 꺼져 있어도 중력과 코드 이동은 처리합니다.
        if (anim == null || !anim.isActiveAndEnabled)
            MoveCharacter(frameDisplacement);
    }

    private void UpdateMovementInput()
    {
        Vector2 input = Vector2.ClampMagnitude(
            new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical")
            ),
            1f
        );

        bool hasInput = input.sqrMagnitude > 0.001f;
        bool isFiring = Input.GetMouseButton(0);

        if (isFiring)
            shootingAimUntil = Time.time + shootingAimHoldTime;

        IsAiming =
            Input.GetMouseButton(1) ||
            isFiring ||
            Time.time < shootingAimUntil ||
            IsCrouching;

        IsSprinting =
            hasInput &&
            !IsAiming &&
            !IsCrouching &&
            Input.GetKey(sprintKey);

        Vector3 worldMove =
            mouseLook.PlanarRight * input.x +
            mouseLook.PlanarForward * input.y;

        if (IsAiming)
        {
            Quaternion targetRotation = Quaternion.LookRotation(
                mouseLook.PlanarForward,
                Vector3.up
            );

            // 발사 순간에는 총과 카메라가 같은 쪽을 보게 합니다.
            transform.rotation = isFiring
                ? targetRotation
                : Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    turnSpeed * Time.deltaTime
                );
        }
        else if (hasInput)
        {
            Quaternion targetRotation = Quaternion.LookRotation(
                worldMove,
                Vector3.up
            );

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime
            );
        }

        // 조준 이동은 몸을 기준으로 앞 / 뒤 / 좌 / 우를 계산합니다.
        Vector3 localMove = transform.InverseTransformDirection(worldMove);

        float targetSpeed = 0f;

        if (hasInput)
        {
            if (IsAiming)
                targetSpeed = input.magnitude;
            else if (IsSprinting)
                targetSpeed = 2f;
            else if (Input.GetKey(walkKey))
                targetSpeed = 0.25f;
            else
                targetSpeed = 1f;
        }

        anim.SetBool("Aiming", IsAiming);

        anim.SetFloat(
            "X", IsAiming ? localMove.x : 0f,
            animationSmoothTime, Time.deltaTime
        );

        anim.SetFloat(
            "Y", IsAiming ? localMove.z : 0f,
            animationSmoothTime, Time.deltaTime
        );

        anim.SetFloat(
            "Speed", targetSpeed,
            animationSmoothTime, Time.deltaTime
        );

        if (IsCrouching)
        {
            // 환풍구 이동은 기존 속도를 유지합니다.
            frameDisplacement = worldMove * crouchSpeed * Time.deltaTime;
        }
        else
        {
            useRootMotionThisFrame = hasInput;
        }
    }

    private void OnAnimatorMove()
    {
        // 루트 모션의 자동 Transform 이동 대신 직접 충돌 처리합니다.
        if (!CanControl || anim == null || mouseLook == null)
            return;

        Vector3 displacement = frameDisplacement;

        if (useRootMotionThisFrame && IsInLocomotion())
        {
            Vector3 rootDelta = anim.deltaPosition;
            rootDelta.y = 0f;

            displacement += rootDelta * rootMotionMultiplier;
        }

        MoveCharacter(displacement);
    }

    private bool IsInLocomotion()
    {
        AnimatorStateInfo current = anim.GetCurrentAnimatorStateInfo(0);

        bool currentIsMovement =
            current.IsName(normalMoveState) ||
            current.IsName(aimingMoveState);

        if (!currentIsMovement)
            return false;

        if (anim.IsInTransition(0))
        {
            AnimatorStateInfo next = anim.GetNextAnimatorStateInfo(0);

            return next.IsName(normalMoveState) ||
                   next.IsName(aimingMoveState);
        }

        return true;
    }

    private void MoveCharacter(Vector3 displacement)
    {
        if (lastMovedFrame == Time.frameCount)
            return;

        lastMovedFrame = Time.frameCount;

        CollisionFlags flags = controller.Move(displacement);

        if ((flags & CollisionFlags.Above) != 0 &&
            verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
        }
    }

    private bool CanDodge()
    {
        return !IsCrouching &&
               controller.isGrounded &&
               Time.time >= nextDodgeTime &&
               anim != null &&
               anim.isActiveAndEnabled &&
               hasDodgeTrigger &&
               IsInLocomotion();
    }

    private void StartDodge()
    {
        IsDodging = true;
        IsSprinting = false;
        dodgeTimeRemaining = Mathf.Max(0.1f, dodgeDuration);

        dodgeDirection = Vector3.ProjectOnPlane(
            transform.forward, Vector3.up
        ).normalized;

        anim.SetFloat("X", 0f);
        anim.SetFloat("Y", 0f);
        anim.SetFloat("Speed", 0f);

        anim.ResetTrigger(dodgeTrigger);
        anim.SetTrigger(dodgeTrigger);
    }

    private void UpdateDodge()
    {
        useRootMotionThisFrame = false;

        float movementTime = Mathf.Min(
            Time.deltaTime, dodgeTimeRemaining
        );

        float dodgeSpeed =
            dodgeDistance / Mathf.Max(0.1f, dodgeDuration);

        frameDisplacement =
            dodgeDirection * dodgeSpeed * movementTime;

        dodgeTimeRemaining -= Time.deltaTime;

        if (dodgeTimeRemaining <= 0f)
        {
            IsDodging = false;
            nextDodgeTime = Time.time + dodgeCooldown;
        }
    }

    private void UpdateCrouch()
    {
        if (Input.GetKey(crouchKey))
        {
            if (!IsCrouching)
                SetCrouching(true);
        }
        else if (IsCrouching && CanStandUp())
        {
            SetCrouching(false);
        }
    }

    private void SetCrouching(bool crouching)
    {
        IsCrouching = crouching;

        float targetHeight = crouching
            ? crouchHeight
            : standingHeight;

        Vector3 targetCenter = standingCenter;
        targetCenter.y -= (standingHeight - targetHeight) * 0.5f;

        controller.stepOffset = Mathf.Min(
            standingStepOffset, targetHeight * 0.25f
        );

        controller.height = targetHeight;
        controller.center = targetCenter;

        if (!crouching)
            controller.stepOffset = standingStepOffset;
    }

    private bool CanStandUp()
    {
        Vector3 scale = transform.lossyScale;

        float radius = controller.radius *
            Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));

        float height = Mathf.Max(
            standingHeight * Mathf.Abs(scale.y),
            radius * 2f
        );

        Vector3 center = transform.TransformPoint(standingCenter);
        float halfSegment = height * 0.5f - radius;

        Vector3 bottom = center - transform.up * halfSegment;
        Vector3 top = center + transform.up * halfSegment;

        float clearance = Mathf.Min(
            radius * 0.2f,
            Mathf.Max(
                0.02f,
                controller.skinWidth * Mathf.Abs(scale.y)
            )
        );

        Collider[] obstacles = Physics.OverlapCapsule(
            bottom,
            top,
            Mathf.Max(0.01f, radius - clearance),
            standingObstacleLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider obstacle in obstacles)
        {
            if (obstacle == controller ||
                obstacle.transform == transform ||
                obstacle.transform.IsChildOf(transform))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private void OnDisable()
    {
        IsDodging = false;
        IsSprinting = false;
        IsAiming = false;
        dodgeTimeRemaining = 0f;
        frameDisplacement = Vector3.zero;
        useRootMotionThisFrame = false;

        if (anim != null && anim.runtimeAnimatorController != null)
        {
            anim.SetFloat("X", 0f);
            anim.SetFloat("Y", 0f);
            anim.SetFloat("Speed", 0f);
            anim.SetBool("Aiming", false);

            if (hasDodgeTrigger)
                anim.ResetTrigger(dodgeTrigger);
        }
    }

    public void FootStep()
    {
    }
}