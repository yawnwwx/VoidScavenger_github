using System.Collections;
using UnityEngine;

public class DoorController : MonoBehaviour
{
    [Header("첫 번째 문짝")]
    public GameObject doorModel;

    [Header("첫 번째 문짝이 열릴 위치 - 비우면 Open Offset 사용")]
    public Transform openPoint;

    [Header("두 번째 문짝 - 양쪽 문일 때만 연결")]
    public GameObject secondDoorModel;
    public Transform secondOpenPoint;

    [Header("플레이어와 상호작용 위치")]
    public PlayerInteraction player;
    public Transform interactionPoint;

    [Header("문 설정")]
    public bool requiresSecurityCard = true;

    [Min(0.1f)]
    public float interactionDistance = 2.5f;

    public Vector3 openOffset = new Vector3(0f, 4f, 0f);

    [Min(0.1f)]
    public float openSpeed = 2f;

    public bool showLockedMessage = true;

    [Header("문 소리")]
    public AudioClip openClip;
    public AudioClip closeClip;

    [Range(0f, 1f)]
    public float doorVolume = 0.7f;

    [Min(0.1f)]
    public float soundMinDistance = 2f;

    [Min(0.2f)]
    public float soundMaxDistance = 15f;

    private bool initialized;
    private bool isOpen;
    private bool isMoving;
    private bool missionLocked;
    private bool movingToOpen;
    private bool audioPaused;

    private Vector3 firstClosedPosition;
    private Vector3 firstOpenPosition;
    private Vector3 secondClosedPosition;
    private Vector3 secondOpenPosition;

    private Coroutine moveRoutine;
    private PlayerHealth playerHealth;
    private AudioSource doorSource;
    private GUIStyle promptStyle;

    public bool IsReady =>
        initialized && isActiveAndEnabled;

    public bool IsClosed =>
        initialized && !isOpen && !isMoving;

    public bool IsOpen =>
        initialized && isOpen && !isMoving;

    private void Awake()
    {
        if (doorModel == null ||
            player == null ||
            interactionPoint == null)
        {
            Debug.LogError(
                "DoorController: Door Model, Player, " +
                "Interaction Point를 연결해 주세요.",
                this
            );

            enabled = false;
            return;
        }

        if (secondDoorModel != null && secondOpenPoint == null)
        {
            Debug.LogError(
                "DoorController: Second Open Point를 연결해 주세요.",
                this
            );

            enabled = false;
            return;
        }

        firstClosedPosition = doorModel.transform.position;

        firstOpenPosition = openPoint != null
            ? openPoint.position
            : firstClosedPosition + openOffset;

        if (secondDoorModel != null)
        {
            secondClosedPosition = secondDoorModel.transform.position;
            secondOpenPosition = secondOpenPoint.position;
        }

        playerHealth = player.GetComponent<PlayerHealth>();

        // 문짝이 움직여도 소리는 출입구 위치에서 들립니다.
        GameObject soundObject = new GameObject("DoorAudio");
        soundObject.transform.SetParent(transform);
        soundObject.transform.position = interactionPoint.position;

        doorSource = soundObject.AddComponent<AudioSource>();
        doorSource.playOnAwake = false;
        doorSource.loop = false;
        doorSource.spatialBlend = 1f;
        doorSource.rolloffMode = AudioRolloffMode.Linear;
        doorSource.dopplerLevel = 0f;

        doorSource.minDistance =
            Mathf.Max(0.1f, soundMinDistance);

        doorSource.maxDistance = Mathf.Max(
            doorSource.minDistance + 0.1f,
            soundMaxDistance
        );

        initialized = true;
    }

    private bool CanInteract()
    {
        if (!IsReady ||
            player == null ||
            interactionPoint == null)
        {
            return false;
        }

        if (!player.isActiveAndEnabled || Time.timeScale == 0f)
            return false;

        if (playerHealth != null && playerHealth.IsDead)
            return false;

        return Vector3.Distance(
            player.transform.position,
            interactionPoint.position
        ) <= interactionDistance;
    }

    private void Update()
    {
        UpdateAudioPause();

        if (!CanInteract() ||
            missionLocked ||
            isOpen ||
            isMoving)
        {
            return;
        }

        if (requiresSecurityCard && !player.hasSecurityCard)
            return;

        // F키 없이 가까이 가면 자동으로 열립니다.
        MoveDoor(true);
    }

    public bool CloseAndLock()
    {
        if (!IsReady)
            return false;

        missionLocked = true;
        MoveDoor(false);
        return true;
    }

    public bool UnlockAndOpen()
    {
        if (!IsReady)
            return false;

        missionLocked = false;
        MoveDoor(true);
        return true;
    }

    private void MoveDoor(bool open)
    {
        if (isMoving && movingToOpen == open)
            return;

        if (!isMoving && isOpen == open)
            return;

        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        isMoving = true;
        movingToOpen = open;

        PlayDoorSound(open);
        moveRoutine = StartCoroutine(MoveDoorRoutine(open));
    }

    private void PlayDoorSound(bool open)
    {
        if (doorSource == null)
            return;

        // 이동 방향이 바뀌면 이전 소리는 끊습니다.
        doorSource.Stop();

        AudioClip clip = open ? openClip : closeClip;

        if (clip == null)
            return;

        doorSource.clip = clip;
        doorSource.volume = doorVolume;
        doorSource.Play();

        audioPaused = Time.timeScale == 0f;

        if (audioPaused)
            doorSource.Pause();
    }

    private IEnumerator MoveDoorRoutine(bool open)
    {
        Vector3 firstStart = doorModel.transform.position;

        Vector3 firstEnd = open
            ? firstOpenPosition
            : firstClosedPosition;

        bool hasSecondDoor = secondDoorModel != null;

        Vector3 secondStart = hasSecondDoor
            ? secondDoorModel.transform.position
            : Vector3.zero;

        Vector3 secondEnd = open
            ? secondOpenPosition
            : secondClosedPosition;

        float progress = 0f;

        while (progress < 1f)
        {
            progress += Time.deltaTime * Mathf.Max(0.1f, openSpeed);

            float t = Mathf.SmoothStep(
                0f, 1f, Mathf.Clamp01(progress)
            );

            doorModel.transform.position =
                Vector3.Lerp(firstStart, firstEnd, t);

            if (hasSecondDoor)
            {
                secondDoorModel.transform.position =
                    Vector3.Lerp(secondStart, secondEnd, t);
            }

            yield return null;
        }

        doorModel.transform.position = firstEnd;

        if (hasSecondDoor)
            secondDoorModel.transform.position = secondEnd;

        isOpen = open;
        isMoving = false;
        moveRoutine = null;

        // 문이 멈춘 뒤에도 음원의 마지막 잔향은 재생합니다.
    }

    private void UpdateAudioPause()
    {
        if (doorSource == null)
            return;

        bool shouldPause = Time.timeScale == 0f;

        if (audioPaused == shouldPause)
            return;

        audioPaused = shouldPause;

        if (audioPaused)
            doorSource.Pause();
        else
            doorSource.UnPause();
    }

    private void OnGUI()
    {
        if (!showLockedMessage ||
            !CanInteract() ||
            isMoving ||
            isOpen)
        {
            return;
        }

        string message;

        if (missionLocked)
        {
            message = "LOCKED - Data transfer in progress";
        }
        else if (requiresSecurityCard && !player.hasSecurityCard)
        {
            message = "SECURITY CARD REQUIRED";
        }
        else
        {
            return;
        }

        if (promptStyle == null)
        {
            promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };

            promptStyle.normal.textColor =
                new Color(1f, 0.75f, 0.35f);
        }

        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;

        float scale = Mathf.Min(
            Screen.width / 1280f,
            Screen.height / 720f
        );

        Vector3 offset = new Vector3(
            (Screen.width - 1280f * scale) * 0.5f,
            (Screen.height - 720f * scale) * 0.5f,
            0f
        );

        GUI.matrix = Matrix4x4.TRS(
            offset,
            Quaternion.identity,
            Vector3.one * scale
        );

        Rect rect = new Rect(380f, 510f, 520f, 44f);

        GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.9f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        GUI.color = Color.white;
        GUI.Label(rect, message, promptStyle);

        GUI.color = previousColor;
        GUI.matrix = previousMatrix;
    }
}