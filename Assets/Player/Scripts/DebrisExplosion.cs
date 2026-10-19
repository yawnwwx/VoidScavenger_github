using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class DebrisExplosion : MonoBehaviour
{
    [Header("플레이어")]
    public PlayerInteraction player;

    [Header("폭탄 설치 위치")]
    public Transform plantPoint;

    [Min(0.1f)]
    public float interactionDistance = 2.5f;

    [Header("날려 버릴 잔해")]
    public GameObject[] debrisPieces;

    [Header("설치 후 보여 줄 폭탄 모델")]
    public GameObject plantedBombVisual;

    [Header("폭발 설정")]
    [Min(0.1f)]
    public float countdownDuration = 3f;

    [Min(0.1f)]
    public float debrisFlySpeed = 6f;

    [Min(0f)]
    public float debrisUpSpeed = 4f;

    [Min(0.1f)]
    public float debrisLifetime = 1.2f;

    [Header("플레이어 폭발 피해")]
    [Min(0.1f)]
    public float damageRadius = 4f;

    [Min(1)]
    public int maxExplosionDamage = 50;

    [Tooltip("체크하면 폭발 중심에서 멀어질수록 피해가 감소합니다.")]
    public bool damageFalloff = true;

    [Header("폭발 효과 - 선택 사항")]
    public GameObject explosionEffectPrefab;

    [Min(0.1f)]
    public float effectLifetime = 5f;

    [Header("폭발 소리")]
    public AudioClip explosionClip;

    [Range(0f, 1f)]
    public float explosionVolume = 0.8f;

    [Min(0.1f)]
    public float soundMinDistance = 3f;

    [Min(0.2f)]
    public float soundMaxDistance = 30f;

    [Header("잔해 구역의 NavMesh Obstacle")]
    public NavMeshObstacle navigationObstacle;

    private PlayerHealth playerHealth;
    private CharacterController playerController;
    private AudioSource audioSource;

    private bool planted;
    private bool exploded;
    private bool ready;
    private bool audioPaused;

    private float countdown;
    private GUIStyle promptStyle;

    private void Start()
    {
        if (player == null ||
            plantPoint == null ||
            debrisPieces == null ||
            debrisPieces.Length == 0)
        {
            Debug.LogError(
                "DebrisExplosion: Player, Plant Point, " +
                "Debris Pieces를 연결해 주세요.",
                this
            );

            enabled = false;
            return;
        }

        bool hasPiece = false;

        foreach (GameObject piece in debrisPieces)
        {
            if (piece == null)
                continue;

            if (piece == gameObject ||
                transform.IsChildOf(piece.transform) ||
                player.transform.IsChildOf(piece.transform) ||
                player.gameObject == piece)
            {
                Debug.LogError(
                    "Debris Pieces에는 날릴 잔해만 넣어 주세요.",
                    this
                );

                enabled = false;
                return;
            }

            hasPiece = true;
        }

        if (!hasPiece)
        {
            Debug.LogError(
                "Debris Pieces에 실제 잔해를 연결해 주세요.",
                this
            );

            enabled = false;
            return;
        }

        playerHealth = player.GetComponent<PlayerHealth>();
        playerController = player.GetComponent<CharacterController>();

        if (playerHealth == null)
        {
            Debug.LogError(
                "연결한 Player에 PlayerHealth가 필요합니다.",
                this
            );

            enabled = false;
            return;
        }

        if (plantedBombVisual != null)
            plantedBombVisual.SetActive(false);

        GameObject soundObject = new GameObject("ExplosionAudio");
        soundObject.transform.SetParent(transform);
        soundObject.transform.position = plantPoint.position;

        audioSource = soundObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = Mathf.Max(0.1f, soundMinDistance);
        audioSource.maxDistance = Mathf.Max(
            audioSource.minDistance + 0.1f,
            soundMaxDistance
        );
        audioSource.dopplerLevel = 0f;

        ready = true;
    }

    private bool PlayerCanAct()
    {
        return player != null &&
               player.isActiveAndEnabled &&
               Time.timeScale > 0f &&
               playerHealth != null &&
               !playerHealth.IsDead;
    }

    private bool IsNear()
    {
        return Vector3.Distance(
            player.transform.position,
            plantPoint.position
        ) <= interactionDistance;
    }

    private void Update()
    {
        if (!ready)
            return;

        UpdateAudioPause();

        if (exploded || !PlayerCanAct())
            return;

        if (planted)
        {
            countdown -= Time.deltaTime;

            if (countdown <= 0f)
                Explode();

            return;
        }

        if (!IsNear() || player.IsLooting)
            return;

        if (Input.GetKeyDown(KeyCode.F) && player.TryConsumeBomb())
        {
            planted = true;
            countdown = Mathf.Max(0.1f, countdownDuration);

            if (plantedBombVisual != null)
                plantedBombVisual.SetActive(true);

            Debug.Log("폭탄 설치 완료! 폭발 범위 밖으로 이동하세요.", this);
        }
    }

    private void Explode()
    {
        if (exploded)
            return;

        // 피해와 폭발이 중복 실행되지 않게 합니다.
        exploded = true;
        Vector3 origin = plantPoint.position;

        if (plantedBombVisual != null)
            plantedBombVisual.SetActive(false);

        if (navigationObstacle != null)
            navigationObstacle.enabled = false;

        if (explosionClip != null)
        {
            audioSource.transform.position = origin;
            audioSource.PlayOneShot(explosionClip, explosionVolume);
        }

        if (explosionEffectPrefab != null)
        {
            GameObject effect = Instantiate(
                explosionEffectPrefab,
                origin,
                Quaternion.identity
            );

            Destroy(effect, Mathf.Max(0.1f, effectLifetime));
        }

        StartCoroutine(FlashRoutine(origin));

        HashSet<GameObject> processed = new HashSet<GameObject>();

        foreach (GameObject piece in debrisPieces)
        {
            if (piece == null || !processed.Add(piece))
                continue;

            foreach (Collider col in piece.GetComponentsInChildren<Collider>())
                col.enabled = false;

            foreach (NavMeshObstacle obstacle in
                     piece.GetComponentsInChildren<NavMeshObstacle>())
            {
                obstacle.enabled = false;
            }

            foreach (Rigidbody body in piece.GetComponentsInChildren<Rigidbody>())
                body.isKinematic = true;

            StartCoroutine(FlyPieceRoutine(piece, origin));
        }

        ApplyExplosionDamage(origin);

        Debug.Log("잔해 제거 완료! 통로가 열렸습니다.", this);
    }

    private void ApplyExplosionDamage(Vector3 origin)
    {
        if (playerHealth == null || playerHealth.IsDead)
            return;

        // 발 위치 대신 플레이어 몸통 중심까지의 거리를 계산합니다.
        Vector3 playerCenter = playerHealth.transform.position;

        if (playerController != null && playerController.enabled)
            playerCenter = playerController.bounds.center;
        else
            playerCenter += Vector3.up;

        float radius = Mathf.Max(0.1f, damageRadius);
        float distance = Vector3.Distance(origin, playerCenter);

        if (distance >= radius)
            return;

        float multiplier = damageFalloff
            ? 1f - distance / radius
            : 1f;

        int damage = Mathf.Max(
            1,
            Mathf.CeilToInt(
                Mathf.Max(1, maxExplosionDamage) * multiplier
            )
        );

        // 기존 체력바, 피격음, 사망 처리까지 함께 실행됩니다.
        playerHealth.TakeDamage(damage);

        Debug.Log($"폭발 피해: {damage}", this);
    }

    private IEnumerator FlyPieceRoutine(
        GameObject piece,
        Vector3 origin)
    {
        Vector3 startPosition = piece.transform.position;
        Quaternion startRotation = piece.transform.rotation;
        Vector3 visualCenter = startPosition;

        Renderer[] renderers = piece.GetComponentsInChildren<Renderer>();

        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            visualCenter = bounds.center;
        }

        Vector3 direction = visualCenter - origin;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
        {
            direction = Random.onUnitSphere;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.01f)
            direction = Vector3.forward;

        Vector3 velocity =
            direction.normalized * debrisFlySpeed +
            Vector3.up * debrisUpSpeed;

        Vector3 rotationSpeed = new Vector3(
            Random.Range(-180f, 180f),
            Random.Range(-180f, 180f),
            Random.Range(-180f, 180f)
        );

        float elapsed = 0f;
        float lifetime = Mathf.Max(0.1f, debrisLifetime);

        while (elapsed < lifetime && piece != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Min(elapsed, lifetime);

            piece.transform.position =
                startPosition +
                velocity * t +
                Vector3.down * (4.9f * t * t);

            piece.transform.rotation =
                Quaternion.Euler(rotationSpeed * t) * startRotation;

            yield return null;
        }

        if (piece != null)
            Destroy(piece);
    }

    private IEnumerator FlashRoutine(Vector3 origin)
    {
        GameObject flashObject = new GameObject("ExplosionFlash");
        flashObject.transform.position = origin;

        Light flash = flashObject.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.color = new Color(1f, 0.6f, 0.2f);
        flash.range = 8f;
        flash.shadows = LightShadows.None;

        float elapsed = 0f;
        const float duration = 0.2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            flash.intensity = Mathf.Lerp(
                8f,
                0f,
                Mathf.Clamp01(elapsed / duration)
            );

            yield return null;
        }

        Destroy(flashObject);
    }

    private void UpdateAudioPause()
    {
        if (audioSource == null)
            return;

        // 사망을 일으킨 폭발음은 끝까지 들려줍니다.
        bool shouldPause =
            Time.timeScale == 0f &&
            playerHealth != null &&
            !playerHealth.IsDead;

        if (audioPaused == shouldPause)
            return;

        audioPaused = shouldPause;

        if (audioPaused)
            audioSource.Pause();
        else
            audioSource.UnPause();
    }

    private void OnGUI()
    {
        if (!ready || exploded || !PlayerCanAct())
            return;

        if (!planted && !IsNear())
            return;

        if (promptStyle == null)
        {
            promptStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };

            promptStyle.normal.textColor = Color.white;
        }

        string message;

        if (planted)
        {
            message =
                $"EXPLOSION IN {Mathf.CeilToInt(countdown)} - MOVE AWAY!";
        }
        else if (player.IsLooting)
        {
            return;
        }
        else
        {
            message = player.hasBomb
                ? "[F] Plant explosive"
                : "Find an explosive to clear the debris";
        }

        float width = Mathf.Min(600f, Screen.width - 20f);

        GUI.Box(
            new Rect(
                (Screen.width - width) * 0.5f,
                Screen.height * 0.72f,
                width,
                60f
            ),
            message,
            promptStyle
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (plantPoint == null)
            return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(
            plantPoint.position,
            Mathf.Max(0.1f, damageRadius)
        );
    }
}