using UnityEngine;
using UnityEngine.AI;

public class CrewRoomEscapeWave : MonoBehaviour
{
    [Header("기존 오브젝트 연결")]
    public MissionFlow missionFlow;
    public DoorController crewRoomDoor;
    public PlayerHealth playerHealth;

    [Header("생성할 적 프리팹")]
    public GameObject enemyPrefab;

    [Header("선원방 안쪽 스폰 위치")]
    public Transform[] spawnPoints;

    [Header("생성 설정")]
    [Min(1)]
    public int enemyCount = 5;

    [Min(0.1f)]
    public float spawnInterval = 0.8f;

    [Min(0.1f)]
    public float navMeshSearchRadius = 1.5f;

    [Tooltip("문 개방 명령 후 첫 적을 생성하기까지의 시간")]
    [Min(0f)]
    public float spawnStartDelay = 1.5f;

    [Header("실행 중 확인 - 직접 수정할 필요 없음")]
    [SerializeField]
    private string currentStatus = "아직 실행되지 않음";

    [SerializeField]
    private bool transmissionCompleteDetected;

    [SerializeField]
    private bool doorOpenCommandAccepted;

    [SerializeField]
    private bool doorFullyOpen;

    [SerializeField]
    private int spawnedCount;

    [SerializeField]
    private int spawnAttempts;

    private bool eventStarted;
    private bool spawnFinished;
    private float nextSpawnTime;
    private bool reportedDoorUnavailable;

    private void Start()
    {
        if (missionFlow == null ||
            crewRoomDoor == null ||
            playerHealth == null)
        {
            currentStatus = "필수 연결 누락";

            Debug.LogError(
                "CrewRoomEscapeWave: Mission Flow, Crew Room Door, " +
                "Player Health를 연결해 주세요.",
                this
            );

            enabled = false;
            return;
        }

        // 적 생성 설정과 관계없이 문 잠금부터 처리합니다.
        if (!crewRoomDoor.CloseAndLock())
        {
            currentStatus = "선원방 문 초기화 실패";

            Debug.LogError(
                "CrewRoomEscapeWave: 선원방 문을 잠글 수 없습니다. " +
                "DoorController의 활성 상태와 연결을 확인해 주세요.",
                crewRoomDoor
            );

            enabled = false;
            return;
        }

        currentStatus = "코어 전송 완료 대기 중";
    }

    private void Update()
    {
        if (missionFlow == null ||
            crewRoomDoor == null ||
            playerHealth == null)
        {
            return;
        }

        if (Time.timeScale == 0f || playerHealth.IsDead)
            return;

        transmissionCompleteDetected =
            missionFlow.IsTransmissionComplete;

        if (!eventStarted && transmissionCompleteDetected)
            BeginEscapeEvent();

        if (!eventStarted)
            return;

        doorFullyOpen = crewRoomDoor.IsOpen;

        // 문이 나중에 준비되는 경우 개방 명령을 다시 시도합니다.
        if (!doorOpenCommandAccepted)
            TryOpenDoor();

        if (spawnFinished)
        {
            currentStatus = doorFullyOpen
                ? $"완료: 문 열림 / 적 {spawnedCount}마리 생성"
                : $"적 {spawnedCount}마리 생성 / 문 상태 확인 필요";

            return;
        }

        if (Time.time < nextSpawnTime)
        {
            currentStatus = doorFullyOpen
                ? "문 열림 / 다음 적 생성 대기"
                : "문 개방 명령 전송 / 적 생성 대기";

            return;
        }

        // 문이 열린 상태인지에 묶이지 않고 독립적으로 생성합니다.
        // 문 이동에 문제가 있어도 적 생성 여부는 확인할 수 있습니다.
        bool success = TrySpawnEnemy(spawnAttempts);

        spawnAttempts++;

        if (success)
            spawnedCount++;

        if (spawnAttempts >= Mathf.Max(1, enemyCount))
        {
            spawnFinished = true;

            currentStatus =
                $"생성 시도 완료: {spawnedCount}/{spawnAttempts}마리";
        }
        else
        {
            nextSpawnTime =
                Time.time + Mathf.Max(0.1f, spawnInterval);

            currentStatus =
                $"적 생성 진행 중: {spawnedCount}마리";
        }
    }

    private void BeginEscapeEvent()
    {
        if (eventStarted)
            return;

        eventStarted = true;
        spawnFinished = false;
        spawnedCount = 0;
        spawnAttempts = 0;

        currentStatus = "탈출 이벤트 시작";

        Debug.Log(
            "CrewRoomEscapeWave: 탈출 이벤트 시작. 선원방 문을 엽니다.",
            this
        );

        TryOpenDoor();

        nextSpawnTime =
            Time.time + Mathf.Max(0f, spawnStartDelay);
    }

    private void TryOpenDoor()
    {
        if (crewRoomDoor == null)
            return;

        if (!crewRoomDoor.IsReady)
        {
            currentStatus = "선원방 DoorController가 준비되지 않음";

            if (!reportedDoorUnavailable)
            {
                reportedDoorUnavailable = true;

                Debug.LogError(
                    "CrewRoomEscapeWave: 연결된 선원방 DoorController가 " +
                    "비활성 상태이거나 초기화되지 않았습니다.",
                    crewRoomDoor
                );
            }

            return;
        }

        doorOpenCommandAccepted = crewRoomDoor.UnlockAndOpen();

        if (doorOpenCommandAccepted)
        {
            currentStatus = "선원방 문 개방 명령 전달 완료";

            Debug.Log(
                $"CrewRoomEscapeWave: '{crewRoomDoor.name}'에 " +
                "문 개방 명령을 전달했습니다.",
                crewRoomDoor
            );
        }
    }

    private bool TrySpawnEnemy(int index)
    {
        if (enemyPrefab == null)
        {
            ReportSpawnError("Enemy Prefab이 비어 있습니다.");
            return false;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            ReportSpawnError("Spawn Points가 비어 있습니다.");
            return false;
        }

        NavMeshAgent prefabAgent =
            enemyPrefab.GetComponent<NavMeshAgent>();

        EnemyChase prefabChase =
            enemyPrefab.GetComponent<EnemyChase>();

        EnemyMeleeAttack prefabMelee =
            enemyPrefab.GetComponent<EnemyMeleeAttack>();

        EnemyHealth prefabHealth =
            enemyPrefab.GetComponent<EnemyHealth>();

        if (prefabAgent == null ||
            prefabChase == null ||
            prefabMelee == null ||
            prefabHealth == null)
        {
            ReportSpawnError(
                "적 프리팹의 최상위 오브젝트에 NavMeshAgent, " +
                "EnemyChase, EnemyMeleeAttack, EnemyHealth가 필요합니다."
            );
            return false;
        }

        if (!prefabAgent.enabled ||
            !prefabChase.enabled ||
            !prefabMelee.enabled ||
            !prefabHealth.enabled)
        {
            ReportSpawnError(
                "Project 창의 적 프리팹에서 NavMeshAgent, EnemyChase, " +
                "EnemyMeleeAttack, EnemyHealth를 모두 켜 주세요."
            );
            return false;
        }

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = prefabAgent.agentTypeID,
            areaMask = prefabAgent.areaMask
        };

        for (int offset = 0; offset < spawnPoints.Length; offset++)
        {
            int pointIndex =
                (index + offset) % spawnPoints.Length;

            Transform point = spawnPoints[pointIndex];

            if (point == null)
                continue;

            if (!NavMesh.SamplePosition(
                point.position,
                out NavMeshHit hit,
                Mathf.Max(0.1f, navMeshSearchRadius),
                filter))
            {
                continue;
            }

            GameObject enemy = Instantiate(
                enemyPrefab,
                hit.position,
                point.rotation
            );

            EnemyMeleeAttack melee =
                enemy.GetComponent<EnemyMeleeAttack>();

            EnemyChase chase =
                enemy.GetComponent<EnemyChase>();

            melee.targetHealth = playerHealth;

            chase.target = playerHealth.transform;
            chase.alwaysChaseTarget = true;

            if (!enemy.activeSelf)
                enemy.SetActive(true);

            chase.AlertToPlayer();

            Debug.Log(
                $"CrewRoomEscapeWave: 적 생성 성공 - {point.name}",
                enemy
            );

            return true;
        }

        ReportSpawnError(
            "스폰 포인트 주변에 해당 적이 사용할 NavMesh가 없습니다."
        );

        return false;
    }

    private void ReportSpawnError(string message)
    {
        currentStatus = "적 생성 실패: " + message;

        Debug.LogError(
            "CrewRoomEscapeWave: " + message,
            this
        );
    }

    // Inspector 컴포넌트 메뉴에서 실행할 수 있습니다.
    [ContextMenu("TEST - Open Door And Spawn")]
    private void TestOpenDoorAndSpawn()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "게임 실행 중에 TEST를 눌러 주세요.",
                this
            );
            return;
        }

        if (!isActiveAndEnabled)
        {
            Debug.LogWarning(
                "CrewRoomEscapeWave 오브젝트와 컴포넌트를 켜 주세요.",
                this
            );
            return;
        }

        if (crewRoomDoor == null || playerHealth == null)
        {
            Debug.LogError(
                "Crew Room Door와 Player Health를 연결해 주세요.",
                this
            );
            return;
        }

        if (Time.timeScale == 0f || playerHealth.IsDead)
        {
            Debug.LogWarning(
                "일시정지 또는 사망 상태에서는 테스트할 수 없습니다.",
                this
            );
            return;
        }

        BeginEscapeEvent();
    }
}