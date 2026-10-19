using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class TransmissionEnemySpawner : MonoBehaviour
{
    [Header("게임 진행")]
    public MissionFlow missionFlow;
    public PlayerHealth playerHealth;

    [Header("생성할 적 프리팹")]
    public GameObject enemyPrefab;

    [Header("적 생성 위치")]
    public Transform[] spawnPoints;

    [Header("생성 설정")]
    [Min(0.5f)]
    public float spawnInterval = 4f;

    [Min(1)]
    public int maxAliveEnemies = 5;

    [Header("생성 위치 주변의 NavMesh 검색 거리")]
    [Min(0.1f)]
    public float navMeshSearchRadius = 1f;

    private readonly List<GameObject> spawnedEnemies =
        new List<GameObject>();

    private NavMeshAgent prefabAgent;
    private float spawnTimer;
    private bool transmissionStarted;

    private void Start()
    {
        if (missionFlow == null ||
            playerHealth == null ||
            enemyPrefab == null ||
            spawnPoints == null ||
            spawnPoints.Length == 0)
        {
            StopWithError(
                "Mission Flow, Player Health, Enemy Prefab, " +
                "Spawn Points를 모두 연결해 주세요."
            );
            return;
        }

        prefabAgent = enemyPrefab.GetComponent<NavMeshAgent>();

        if (prefabAgent == null ||
            enemyPrefab.GetComponent<EnemyChase>() == null ||
            enemyPrefab.GetComponent<EnemyMeleeAttack>() == null ||
            enemyPrefab.GetComponent<EnemyHealth>() == null)
        {
            StopWithError(
                "적 프리팹의 최상위 오브젝트에 NavMeshAgent, " +
                "EnemyChase, EnemyMeleeAttack, EnemyHealth가 필요합니다."
            );
            return;
        }

        if (!enemyPrefab.activeSelf || !prefabAgent.enabled)
        {
            StopWithError(
                "적 프리팹과 NavMeshAgent를 활성화해 주세요."
            );
        }
    }

    private void Update()
    {
        if (missionFlow == null ||
            playerHealth == null ||
            playerHealth.IsDead ||
            Time.timeScale == 0f)
        {
            return;
        }

        // 전송 전이나 전송 완료 후에는 생성하지 않습니다.
        if (!missionFlow.IsTransmitting)
            return;

        // 전송을 처음 시작했을 때 즉시 한 마리 생성합니다.
        if (!transmissionStarted)
        {
            transmissionStarted = true;
            TrySpawnEnemy();
            spawnTimer = spawnInterval;
            return;
        }

        spawnTimer -= Time.deltaTime;

        if (spawnTimer > 0f)
            return;

        spawnTimer = Mathf.Max(0.5f, spawnInterval);
        TrySpawnEnemy();
    }

    private void TrySpawnEnemy()
    {
        // 죽어서 파괴된 적을 목록에서 제거합니다.
        spawnedEnemies.RemoveAll(enemy => enemy == null);

        if (spawnedEnemies.Count >= maxAliveEnemies)
            return;

        // 프리팹과 같은 Agent Type의 NavMesh를 찾습니다.
        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = prefabAgent.agentTypeID,
            areaMask = prefabAgent.areaMask
        };

        // 무작위 지점부터 확인하고, 사용할 수 없으면
        // 나머지 생성 지점도 순서대로 확인합니다.
        int startIndex = Random.Range(0, spawnPoints.Length);

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            int index = (startIndex + i) % spawnPoints.Length;
            Transform point = spawnPoints[index];

            if (point == null)
                continue;

            NavMeshHit hit;

            if (!NavMesh.SamplePosition(
                point.position,
                out hit,
                navMeshSearchRadius,
                filter))
            {
                continue;
            }

            GameObject enemy = Instantiate(
                enemyPrefab,
                hit.position,
                point.rotation
            );

            EnemyChase chase = enemy.GetComponent<EnemyChase>();
            EnemyMeleeAttack attack =
                enemy.GetComponent<EnemyMeleeAttack>();

            // 생성된 적에게 실제 플레이어를 연결합니다.
            chase.target = playerHealth.transform;
            attack.targetHealth = playerHealth;

            chase.enabled = true;
            attack.enabled = true;

            spawnedEnemies.Add(enemy);
            return;
        }

        Debug.LogWarning(
            "적을 생성할 수 없습니다. Spawn Point를 " +
            "Bake된 이동 가능 바닥 가까이에 배치해 주세요.",
            this
        );
    }

    private void StopWithError(string message)
    {
        Debug.LogError(
            "TransmissionEnemySpawner: " + message,
            this
        );

        enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (spawnPoints == null)
            return;

        Gizmos.color = Color.red;

        foreach (Transform point in spawnPoints)
        {
            if (point == null)
                continue;

            Gizmos.DrawWireSphere(point.position, 0.5f);
            Gizmos.DrawLine(
                point.position,
                point.position + Vector3.up * 2f
            );
        }
    }
}