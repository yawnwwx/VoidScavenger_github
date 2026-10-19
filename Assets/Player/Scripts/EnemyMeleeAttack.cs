using UnityEngine;
using UnityEngine.AI;

public class EnemyMeleeAttack : MonoBehaviour
{
    [Header("공격할 플레이어")]
    public PlayerHealth targetHealth;

    [Header("적 모델의 Animator")]
    public Animator animator;

    [Header("공격 설정")]
    [Min(0.1f)]
    public float attackRange = 2f;

    [Min(1)]
    public int damage = 10;

    [Tooltip("공격 모션이 끝난 뒤 다음 공격까지 기다리는 시간")]
    [Min(0.1f)]
    public float attackInterval = 1f;

    [Header("타격 시점")]
    [Tooltip("0.45이면 공격 모션의 45% 지점에서 피해를 판정합니다.")]
    [Range(0.05f, 0.95f)]
    public float hitNormalizedTime = 0.45f;

    [Tooltip("적 정면을 중심으로 한 전체 공격 각도")]
    [Range(1f, 360f)]
    public float attackAngle = 120f;

    [Header("장애물 확인 높이")]
    public float enemyCheckHeight = 1f;
    public float playerCheckHeight = 1f;

    public bool IsAttacking { get; private set; }

    private EnemyHealth health;
    private NavMeshAgent agent;

    private float nextAttackTime;
    private float attackRequestTime;
    private bool enteredAttackState;
    private bool hasAppliedHit;
    private bool originalUpdateRotation;

    private static readonly int AttackStateHash =
        Animator.StringToHash("Attack");

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator != null)
            animator.applyRootMotion = false;
    }

    private void Start()
    {
        if (targetHealth == null)
        {
            EnemyChase chase = GetComponent<EnemyChase>();

            if (chase != null && chase.target != null)
            {
                targetHealth =
                    chase.target.GetComponentInParent<PlayerHealth>();
            }
        }

        if (animator == null ||
            animator.runtimeAnimatorController == null)
        {
            Debug.LogError(
                "EnemyMeleeAttack: 적의 Animator와 " +
                "EnemyAnimator Controller를 연결해 주세요.",
                this
            );

            enabled = false;
        }
    }

    private void Update()
    {
        if (Time.timeScale == 0f)
            return;

        if ((health != null && health.IsDead) ||
            targetHealth == null ||
            targetHealth.IsDead)
        {
            CancelAttack();
            return;
        }

        if (animator == null || !animator.isActiveAndEnabled)
        {
            CancelAttack();
            return;
        }

        if (IsAttacking)
        {
            UpdateAttack();
            return;
        }

        if (Time.time < nextAttackTime || !CanReachTarget())
            return;

        // 전환 중에 공격 Trigger가 남는 것을 방지합니다.
        if (animator.IsInTransition(0))
            return;

        AnimatorStateInfo state =
            animator.GetCurrentAnimatorStateInfo(0);

        if (!state.IsName("Idle") && !state.IsName("Walk"))
            return;

        BeginAttack();
    }

    public bool CanReachTarget()
    {
        if (targetHealth == null ||
            targetHealth.IsDead ||
            (health != null && health.IsDead))
        {
            return false;
        }

        float distance = Vector3.Distance(
            transform.position,
            targetHealth.transform.position
        );

        return distance <= attackRange && !IsBlocked();
    }

    private void BeginAttack()
    {
        IsAttacking = true;
        enteredAttackState = false;
        hasAppliedHit = false;
        attackRequestTime = Time.time;

        if (agent != null)
        {
            originalUpdateRotation = agent.updateRotation;
            agent.updateRotation = false;

            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
        }

        // 공격 시작 시 방향을 정합니다.
        // 공격 도중에는 플레이어를 따라 회전하지 않습니다.
        Vector3 direction =
            targetHealth.transform.position - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction);

        animator.SetFloat("Speed", 0f);
        animator.ResetTrigger("Attack");
        animator.SetTrigger("Attack");
    }

    private void UpdateAttack()
    {
        AnimatorStateInfo current =
            animator.GetCurrentAnimatorStateInfo(0);

        bool inAttack =
            current.shortNameHash == AttackStateHash;

        AnimatorStateInfo attackState = current;

        // Attack으로 전환되는 중에도 진행률을 확인합니다.
        if (!inAttack && animator.IsInTransition(0))
        {
            AnimatorStateInfo next =
                animator.GetNextAnimatorStateInfo(0);

            if (next.shortNameHash == AttackStateHash)
            {
                inAttack = true;
                attackState = next;
            }
        }

        if (inAttack)
        {
            enteredAttackState = true;

            if (!hasAppliedHit &&
                attackState.normalizedTime >= hitNormalizedTime)
            {
                hasAppliedHit = true;
                TryApplyDamage();
            }

            // 잘못된 전환 설정으로 공격 상태에 갇히는 경우 감지.
            if (attackState.normalizedTime > 1.5f)
            {
                Debug.LogError(
                    "Attack → Idle 전환을 확인해 주세요. " +
                    "Has Exit Time 체크, Exit Time 1, " +
                    "조건 Dead=false가 필요합니다.",
                    this
                );

                CancelAttack();
                enabled = false;
            }

            return;
        }

        if (enteredAttackState)
        {
            FinishAttack();
            return;
        }

        // Trigger를 보냈지만 Attack에 진입하지 못한 경우.
        if (Time.time - attackRequestTime > 1f)
        {
            Debug.LogError(
                "Attack 상태에 진입하지 못했습니다. " +
                "Attack Trigger와 Idle/Walk → Attack " +
                "전환 조건을 확인해 주세요.",
                this
            );

            CancelAttack();
            enabled = false;
        }
    }

    private void TryApplyDamage()
    {
        // 타격 순간에 거리와 벽을 다시 검사합니다.
        if (!CanReachTarget())
            return;

        Vector3 direction =
            targetHealth.transform.position - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f &&
            Vector3.Angle(transform.forward, direction) >
            attackAngle * 0.5f)
        {
            return;
        }

        targetHealth.TakeDamage(damage);
    }

    private void FinishAttack()
    {
        CancelAttack();

        nextAttackTime =
            Time.time + Mathf.Max(0.1f, attackInterval);
    }

    public void CancelAttack()
    {
        if (!IsAttacking)
            return;

        IsAttacking = false;
        enteredAttackState = false;
        hasAppliedHit = false;

        if (agent != null)
            agent.updateRotation = originalUpdateRotation;

        if (animator != null &&
            animator.runtimeAnimatorController != null)
        {
            animator.ResetTrigger("Attack");
        }
    }

    private bool IsBlocked()
    {
        Vector3 start =
            transform.position + Vector3.up * enemyCheckHeight;

        Vector3 end =
            targetHealth.transform.position +
            Vector3.up * playerCheckHeight;

        Vector3 direction = end - start;
        float distance = direction.magnitude;

        if (distance < 0.001f)
            return false;

        RaycastHit[] hits = Physics.RaycastAll(
            start,
            direction.normalized,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == transform ||
                hit.transform.IsChildOf(transform))
            {
                continue;
            }

            PlayerHealth hitPlayer =
                hit.collider.GetComponentInParent<PlayerHealth>();

            if (hitPlayer == targetHealth)
                continue;

            return true;
        }

        return false;
    }

    private void OnDisable()
    {
        CancelAttack();
    }
}