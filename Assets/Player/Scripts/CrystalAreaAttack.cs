using UnityEngine;

public class CrystalAreaAttack : MonoBehaviour
{
    [Header("필수 연결")]
    public MissionFlow missionFlow;
    public PlayerHealth playerHealth;
    public Transform firePoint;
    public Material effectMaterial;

    [Header("공격 설정")]
    [Min(0.1f)] public float detectionRange = 12f;
    [Min(0.1f)] public float attackRadius = 1.5f;
    [Min(0.1f)] public float chargeTime = 1.5f;
    [Min(0.1f)] public float cooldown = 3f;
    [Min(0f)] public float firstAttackDelay = 0f;
    [Min(1)] public int damage = 10;

    [Header("바닥 감지")]
    public LayerMask groundLayers = ~0;
    public float groundRayHeight = 1f;
    public float groundRayDistance = 5f;
    public float groundOffset = 0.04f;

    [Header("피해 판정 높이")]
    [Min(0.1f)] public float damageHeight = 2f;

    [Header("번개 효과")]
    [Min(0.05f)] public float lightningDuration = 0.25f;
    public float lightningWidth = 0.12f;
    public float lightningJitter = 0.2f;

    private enum AttackState
    {
        Waiting,
        Charging,
        Lightning
    }

    private AttackState state;
    private bool wasActive;
    private float timer;
    private float nextAttackTime;

    private Vector3 lockedGroundPoint;

    private GameObject effectRoot;
    private Transform marker;
    private Transform fill;
    private LineRenderer ring;
    private LineRenderer bolt;
    private Mesh diskMesh;

    private const int CircleSegments = 64;
    private readonly Vector3[] boltPoints = new Vector3[14];

    private bool IsActive =>
        missionFlow != null &&
        playerHealth != null &&
        missionFlow.IsTransmitting &&
        !playerHealth.IsDead;

    private void Start()
    {
        if (missionFlow == null ||
            playerHealth == null ||
            firePoint == null ||
            effectMaterial == null)
        {
            Debug.LogError(
                "CrystalAreaAttack: 필수 연결 4개를 모두 넣어 주세요.",
                this
            );
            enabled = false;
            return;
        }

        CreateEffects();
        ResetAttack();
    }

    private void Update()
    {
        if (!IsActive)
        {
            ResetAttack();
            wasActive = false;
            return;
        }

        // 일시정지 중에는 진행하지 않습니다.
        if (Time.timeScale == 0f)
            return;

        if (!wasActive)
        {
            wasActive = true;
            nextAttackTime = Time.time + firstAttackDelay;
        }

        switch (state)
        {
            case AttackState.Waiting:
                if (Time.time < nextAttackTime)
                    return;

                Vector3 difference =
                    playerHealth.transform.position - firePoint.position;

                if (difference.sqrMagnitude >
                    detectionRange * detectionRange)
                {
                    return;
                }

                Vector3 groundPoint;
                if (TryFindGround(out groundPoint))
                {
                    BeginCharge(groundPoint);
                }
                else
                {
                    // 바닥을 못 찾으면 잠시 후 다시 확인합니다.
                    nextAttackTime = Time.time + 0.5f;
                }
                break;

            case AttackState.Charging:
                timer += Time.deltaTime;

                float progress = Mathf.Clamp01(timer / chargeTime);

                // 부채꼴이 아니라 중심에서 반지름이 커집니다.
                float radius = attackRadius * progress;
                fill.localScale = new Vector3(radius, 1f, radius);

                if (progress >= 1f)
                    Strike();
                break;

            case AttackState.Lightning:
                timer += Time.deltaTime;
                DrawLightning();

                if (timer >= lightningDuration)
                {
                    ResetAttack();
                    nextAttackTime = Time.time + cooldown;
                }
                break;
        }
    }

    private bool TryFindGround(out Vector3 point)
    {
        Vector3 origin = playerHealth.transform.position +
                         Vector3.up * groundRayHeight;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            groundRayDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );

        point = Vector3.zero;
        float nearest = float.PositiveInfinity;
        bool found = false;

        foreach (RaycastHit hit in hits)
        {
            // 플레이어나 적의 몸을 바닥으로 선택하지 않습니다.
            if (hit.collider.GetComponentInParent<PlayerHealth>() != null)
                continue;

            if (hit.collider.GetComponentInParent<EnemyHealth>() != null)
                continue;

            if (hit.transform == transform ||
                hit.transform.IsChildOf(transform))
                continue;

            // 벽처럼 가파른 면은 제외합니다.
            if (hit.normal.y < 0.7f)
                continue;

            if (hit.distance < nearest)
            {
                nearest = hit.distance;
                point = hit.point;
                found = true;
            }
        }

        return found;
    }

    private void BeginCharge(Vector3 groundPoint)
    {
        // 공격이 끝날 때까지 이 위치를 유지합니다.
        lockedGroundPoint = groundPoint;

        marker.position = groundPoint + Vector3.up * groundOffset;
        marker.gameObject.SetActive(true);

        fill.localScale = new Vector3(0.001f, 1f, 0.001f);

        // 최종 공격 반지름의 테두리를 먼저 표시합니다.
        for (int i = 0; i < CircleSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / CircleSegments;
            ring.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * attackRadius,
                0.01f,
                Mathf.Sin(angle) * attackRadius
            ));
        }

        timer = 0f;
        state = AttackState.Charging;
    }

    private void Strike()
    {
        fill.localScale = new Vector3(
            attackRadius, 1f, attackRadius
        );

        // 번개 선 자체가 아니라 바닥 원 안에 있는지 판정합니다.
        Vector3 offset =
            playerHealth.transform.position - lockedGroundPoint;

        float horizontalDistanceSquared =
            offset.x * offset.x + offset.z * offset.z;

        bool insideCircle =
            horizontalDistanceSquared <= attackRadius * attackRadius;

        bool sameHeight = Mathf.Abs(offset.y) <= damageHeight;

        if (insideCircle && sameHeight)
        {
            // 한 번의 번개당 피해는 한 번만 적용합니다.
            playerHealth.TakeDamage(damage);
        }

        bolt.enabled = true;
        DrawLightning();

        timer = 0f;
        state = AttackState.Lightning;
    }

    private void DrawLightning()
    {
        Vector3 end = lockedGroundPoint +
                      Vector3.up * groundOffset;

        for (int i = 0; i < boltPoints.Length; i++)
        {
            float t = i / (float)(boltPoints.Length - 1);
            boltPoints[i] = Vector3.Lerp(firePoint.position, end, t);

            if (i > 0 && i < boltPoints.Length - 1)
            {
                boltPoints[i] +=
                    Random.insideUnitSphere * lightningJitter;
            }
        }

        bolt.SetPositions(boltPoints);
    }

    private void CreateEffects()
    {
        // 수정 모델의 확대/축소에 영향을 받지 않게 따로 생성합니다.
        effectRoot = new GameObject(name + "_AttackEffects");

        marker = new GameObject("GroundWarning").transform;
        marker.SetParent(effectRoot.transform, false);

        GameObject fillObject = new GameObject("RedFill");
        fill = fillObject.transform;
        fill.SetParent(marker, false);

        diskMesh = CreateDiskMesh();

        MeshFilter filter = fillObject.AddComponent<MeshFilter>();
        filter.sharedMesh = diskMesh;

        MeshRenderer renderer = fillObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = effectMaterial;
        renderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        GameObject ringObject = new GameObject("RedRing");
        ringObject.transform.SetParent(marker, false);

        ring = ringObject.AddComponent<LineRenderer>();
        ring.sharedMaterial = effectMaterial;
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = CircleSegments;
        ring.startWidth = 0.06f;
        ring.endWidth = 0.06f;
        ring.startColor = Color.red;
        ring.endColor = Color.red;

        // 바닥と平行な線にします。
        ring.alignment = LineAlignment.TransformZ;
        ringObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        // 円の座標は後で設定するため、回転は使わず
        // カメラに向く細い線として表示します。
        ringObject.transform.localRotation = Quaternion.identity;
        ring.alignment = LineAlignment.View;

        GameObject boltObject = new GameObject("Lightning");
        boltObject.transform.SetParent(effectRoot.transform, false);

        bolt = boltObject.AddComponent<LineRenderer>();
        bolt.sharedMaterial = effectMaterial;
        bolt.useWorldSpace = true;
        bolt.positionCount = boltPoints.Length;
        bolt.startWidth = lightningWidth;
        bolt.endWidth = lightningWidth;
        bolt.startColor = Color.cyan;
        bolt.endColor = Color.white;
        bolt.numCapVertices = 4;
        bolt.enabled = false;
    }

    private Mesh CreateDiskMesh()
    {
        Vector3[] vertices = new Vector3[CircleSegments + 1];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[CircleSegments * 3];

        vertices[0] = Vector3.zero;

        for (int i = 0; i < vertices.Length; i++)
            colors[i] = new Color(1f, 0f, 0f, 0.4f);

        for (int i = 0; i < CircleSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / CircleSegments;

            vertices[i + 1] = new Vector3(
                Mathf.Cos(angle), 0f, Mathf.Sin(angle)
            );

            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = (i + 1) % CircleSegments + 1;
            triangles[i * 3 + 2] = i + 1;
        }

        Mesh mesh = new Mesh();
        mesh.name = "CrystalAttackDisk";
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void ResetAttack()
    {
        state = AttackState.Waiting;
        timer = 0f;

        if (marker != null)
            marker.gameObject.SetActive(false);

        if (bolt != null)
            bolt.enabled = false;
    }

    private void OnDisable()
    {
        ResetAttack();
        wasActive = false;
    }

    private void OnDestroy()
    {
        if (effectRoot != null)
            Destroy(effectRoot);

        if (diskMesh != null)
            Destroy(diskMesh);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 center = firePoint != null
            ? firePoint.position
            : transform.position;

        Gizmos.DrawWireSphere(center, detectionRange);
    }
}