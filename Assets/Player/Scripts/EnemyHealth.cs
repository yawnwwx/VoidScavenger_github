using UnityEngine;
using UnityEngine.AI;

public class EnemyHealth : MonoBehaviour
{
    [Header("체력")]
    public int hp = 3;

    [Header("적 모델의 Animator")]
    public Animator animator;

    [Header("사망 후 삭제까지 걸리는 시간")]
    [Min(0.1f)]
    public float destroyDelay = 5f;

    public bool IsDead { get; private set; }

    private NavMeshAgent agent;
    private EnemyChase chase;
    private EnemyMeleeAttack melee;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        chase = GetComponent<EnemyChase>();
        melee = GetComponent<EnemyMeleeAttack>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator != null)
            animator.applyRootMotion = false;
    }

    public void TakeDamage(int damageAmount)
    {
        if (IsDead || damageAmount <= 0)
            return;

        hp = Mathf.Max(0, hp - damageAmount);

        if (hp <= 0)
        {
            Die();
            return;
        }

        // 감지 거리 밖이어도 맞으면 플레이어를 추적합니다.
        if (chase != null && chase.isActiveAndEnabled)
            chase.AlertToPlayer();
    }

    private void Die()
    {
        IsDead = true;

        if (melee != null)
        {
            melee.CancelAttack();
            melee.enabled = false;
        }

        if (chase != null)
            chase.enabled = false;

        if (agent != null && agent.isActiveAndEnabled)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }

            agent.enabled = false;
        }

        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }

        foreach (Rigidbody body in GetComponentsInChildren<Rigidbody>())
        {
            body.isKinematic = true;
        }

        if (animator != null &&
            animator.runtimeAnimatorController != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.ResetTrigger("Attack");
            animator.SetBool("Dead", true);
        }

        Destroy(gameObject, destroyDelay);
    }
}