using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyChase : MonoBehaviour
{
    [Header("추적할 플레이어")]
    public Transform target;

    [Header("적 모델의 Animator")]
    public Animator animator;

    [Header("감지 설정")]
    [Min(0.1f)]
    public float detectionRange = 10f;

    [Tooltip("일반 감지 상태에서 플레이어를 놓친 뒤 추적을 중단할 시간")]
    [Min(0.1f)]
    public float loseSightDelay = 4f;

    [Tooltip("코어 전투용 적처럼 처음부터 계속 추적할 때 체크")]
    public bool alwaysChaseTarget = false;

    [Header("시야 검사")]
    public float enemyEyeHeight = 1.2f;
    public float playerSightHeight = 1f;

    public LayerMask sightBlockingLayers =
        Physics.DefaultRaycastLayers;

    [Min(0.05f)]
    public float sightCheckInterval = 0.15f;

    [Header("경로 갱신")]
    [Min(0.05f)]
    public float pathUpdateInterval = 0.2f;

    [Header("실행 중 확인")]
    [SerializeField]
    private bool permanentlyAlerted;

    public bool IsAlerted { get; private set; }

    private NavMeshAgent agent;
    private EnemyHealth health;
    private EnemyMeleeAttack melee;
    private PlayerHealth targetHealth;

    private bool attackComponentAvailable;
    private bool hasWarnedAboutNavMesh;

    private float nextPathUpdateTime;
    private float nextSightCheckTime;
    private float lastSeenTime;

    private Vector3 lastKnownPosition;

    private bool AgentReady =>
        agent != null &&
        agent.isActiveAndEnabled &&
        agent.isOnNavMesh;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<EnemyHealth>();
        melee = GetComponent<EnemyMeleeAttack>();

        attackComponentAvailable =
            melee != null && melee.enabled;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator != null)
            animator.applyRootMotion = false;
    }

    private void Start()
    {
        ResolveTarget();

        if (target == null)
        {
            Debug.LogWarning(
                "EnemyChase: Target에 플레이어를 연결해 주세요.",
                this
            );
        }

        ApplyAttackEnabledState();
    }

    private void ResolveTarget()
    {
        if (target == null &&
            melee != null &&
            melee.targetHealth != null)
        {
            target = melee.targetHealth.transform;
        }

        if (target == null)
        {
            PlayerHealth foundPlayer =
                FindAnyObjectByType<PlayerHealth>();

            if (foundPlayer != null)
                target = foundPlayer.transform;
        }

        targetHealth = target != null
            ? target.GetComponentInParent<PlayerHealth>()
            : null;

        if (melee != null && targetHealth != null)
            melee.targetHealth = targetHealth;
    }

    private void Update()
    {
        if (Time.timeScale == 0f)
            return;

        bool cannotChase =
            (health != null && health.IsDead) ||
            target == null ||
            !target.gameObject.activeInHierarchy ||
            (targetHealth != null && targetHealth.IsDead);

        if (cannotChase)
        {
            SetAlerted(false);
            StopAgent();
            return;
        }

        // 총에 맞았거나 코어 전투용 적이면
        // 시야와 거리에 상관없이 현재 위치를 계속 추적합니다.
        if (permanentlyAlerted || alwaysChaseTarget)
        {
            lastKnownPosition = target.position;
            lastSeenTime = Time.time;
            SetAlerted(true);
        }
        else if (Time.time >= nextSightCheckTime)
        {
            nextSightCheckTime =
                Time.time + Mathf.Max(0.05f, sightCheckInterval);

            UpdateDetection();
        }

        if (!agent.isActiveAndEnabled)
            return;

        if (!agent.isOnNavMesh)
        {
            if (!hasWarnedAboutNavMesh)
            {
                Debug.LogWarning(
                    "적이 NavMesh 위에 없습니다. " +
                    "Bake된 바닥 위에 적을 놓아 주세요.",
                    this
                );

                hasWarnedAboutNavMesh = true;
            }

            return;
        }

        hasWarnedAboutNavMesh = false;

        if (!IsAlerted)
        {
            StopAgent();
            return;
        }

        // 공격 모션 중이거나 공격 가능한 거리에서는 정지합니다.
        if (melee != null &&
            melee.isActiveAndEnabled &&
            (melee.IsAttacking || melee.CanReachTarget()))
        {
            StopAgent();
            return;
        }

        bool wasStopped = agent.isStopped;
        agent.isStopped = false;

        if (!wasStopped && Time.time < nextPathUpdateTime)
            return;

        nextPathUpdateTime =
            Time.time + Mathf.Max(0.05f, pathUpdateInterval);

        agent.SetDestination(lastKnownPosition);
    }

    private void UpdateDetection()
    {
        float distance = Vector3.Distance(
            transform.position,
            target.position
        );

        bool canCheckSight =
            IsAlerted || distance <= detectionRange;

        if (canCheckSight && HasLineOfSight())
        {
            lastKnownPosition = target.position;
            lastSeenTime = Time.time;

            SetAlerted(true);
            return;
        }

        if (IsAlerted &&
            Time.time - lastSeenTime >= loseSightDelay)
        {
            SetAlerted(false);
        }
    }

    // EnemyHealth에서 피해를 받았을 때 호출합니다.
    public void AlertToPlayer()
    {
        if (!isActiveAndEnabled ||
            (health != null && health.IsDead))
        {
            return;
        }

        // 한 번 맞으면 이번 생존 중에는 추적을 포기하지 않습니다.
        permanentlyAlerted = true;

        ResolveTarget();

        if (target == null ||
            (targetHealth != null && targetHealth.IsDead))
        {
            return;
        }

        lastKnownPosition = target.position;
        lastSeenTime = Time.time;

        nextPathUpdateTime = 0f;
        nextSightCheckTime = 0f;

        SetAlerted(true);
    }

    private void SetAlerted(bool alerted)
    {
        if (IsAlerted == alerted)
            return;

        IsAlerted = alerted;
        nextPathUpdateTime = 0f;

        ApplyAttackEnabledState();

        if (!alerted)
            StopAgent();
    }

    private void ApplyAttackEnabledState()
    {
        if (melee == null || !attackComponentAvailable)
            return;

        bool shouldEnable =
            isActiveAndEnabled &&
            IsAlerted &&
            (health == null || !health.IsDead);

        if (melee.enabled != shouldEnable)
            melee.enabled = shouldEnable;
    }

    private bool HasLineOfSight()
    {
        Vector3 start =
            transform.position + Vector3.up * enemyEyeHeight;

        Vector3 end =
            target.position + Vector3.up * playerSightHeight;

        Vector3 direction = end - start;
        float distance = direction.magnitude;

        if (distance < 0.001f)
            return true;

        RaycastHit[] hits = Physics.RaycastAll(
            start,
            direction.normalized,
            distance,
            sightBlockingLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (RaycastHit hit in hits)
        {
            Transform hitTransform = hit.collider.transform;

            // 자신의 충돌체 제외.
            if (hitTransform == transform ||
                hitTransform.IsChildOf(transform))
            {
                continue;
            }

            // 플레이어의 충돌체 제외.
            if (hitTransform == target ||
                hitTransform.IsChildOf(target))
            {
                continue;
            }

            PlayerHealth hitPlayer =
                hit.collider.GetComponentInParent<PlayerHealth>();

            if (targetHealth != null && hitPlayer == targetHealth)
                continue;

            // 다른 적 때문에 시야가 차단되지 않도록 합니다.
            if (hit.collider.GetComponentInParent<EnemyHealth>() != null)
                continue;

            // 벽이나 닫힌 문 등이 있으면 보이지 않습니다.
            return false;
        }

        return true;
    }

    private void StopAgent()
    {
        if (!AgentReady)
            return;

        agent.isStopped = true;
        agent.velocity = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (Time.timeScale == 0f ||
            animator == null ||
            animator.runtimeAnimatorController == null)
        {
            return;
        }

        float movementSpeed = 0f;

        if (IsAlerted &&
            AgentReady &&
            !agent.isStopped &&
            (health == null || !health.IsDead) &&
            (melee == null || !melee.IsAttacking))
        {
            Vector3 velocity = agent.velocity;
            velocity.y = 0f;

            movementSpeed = velocity.magnitude;
        }

        animator.SetFloat("Speed", movementSpeed);
    }

    private void OnEnable()
    {
        if (agent != null)
            ApplyAttackEnabledState();
    }

    private void OnDisable()
    {
        StopAgent();

        if (melee != null && attackComponentAvailable)
            melee.enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsAlerted ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}