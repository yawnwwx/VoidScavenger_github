using UnityEngine;
using UnityEngine.SceneManagement;

public class MissionFlow : MonoBehaviour
{
    [Header("플레이어")]
    public PlayerHealth playerHealth;

    [Header("상호작용 위치")]
    public Transform corePoint;
    public Transform extractionPoint;

    [Header("코어방 입구")]
    public DoorController coreEntranceDoor;

    [Header("전송 완료 비상벨 - 선택 사항")]
    public AudioSource emergencyAlarm;

    [Header("진행 설정")]
    [Min(0.1f)]
    public float interactionDistance = 2.5f;

    [Min(1f)]
    public float transmissionDuration = 10f;

    [Header("메인 메뉴 씬")]
    public string mainMenuSceneName = "MainMenu";

    [Header("UI 디자인")]
    public Font uiFont;

    public Color accentColor =
        new Color(0.2f, 0.9f, 0.92f, 1f);

    public Color panelColor =
        new Color(0.025f, 0.045f, 0.075f, 0.94f);

    public Color warningColor =
        new Color(1f, 0.68f, 0.3f, 1f);

    [Header("왼쪽 위 목표 안내")]
    public Vector2 objectivePosition = new Vector2(24f, 24f);

    [Min(300f)]
    public float objectiveWidth = 360f;

    [Header("맨 위 중앙 전송 게이지")]
    [Tooltip("화면 위에서 패널까지의 거리")]
    [Min(0f)]
    public float transferTopMargin = 18f;

    [Tooltip("화면 너비 대비 게이지 패널 너비")]
    [Range(0.3f, 0.9f)]
    public float transferWidthRatio = 0.6f;

    [Range(20f, 50f)]
    public float transferBarHeight = 30f;

    [Tooltip("완료 후 100% 게이지를 표시하는 시간")]
    [Min(0f)]
    public float completionDisplayDuration = 4f;

    private enum MissionState
    {
        FindCore,
        SealingEntrance,
        Transmitting,
        ReturnToStart,
        Cleared,
        Failed
    }

    private MissionState state = MissionState.FindCore;

    private float transmissionTimer;
    private float completionDisplayUntil;

    private bool isLoading;
    private bool initialized;
    private string loadError = "";

    private GUIStyle smallStyle;
    private GUIStyle bodyStyle;
    private GUIStyle titleStyle;
    private GUIStyle centerStyle;
    private GUIStyle transferTitleStyle;
    private GUIStyle percentStyle;
    private GUIStyle buttonStyle;

    public bool IsTransmitting =>
        state == MissionState.Transmitting;

    public bool IsTransmissionComplete =>
        state == MissionState.ReturnToStart ||
        state == MissionState.Cleared;

    private bool HasEnded =>
        state == MissionState.Cleared ||
        state == MissionState.Failed;

    private bool ShowCompletedTransfer =>
        state == MissionState.ReturnToStart &&
        Time.time < completionDisplayUntil;

    private bool ShowTransferPanel =>
        state == MissionState.SealingEntrance ||
        state == MissionState.Transmitting ||
        ShowCompletedTransfer;

    private float TransferDuration =>
        Mathf.Max(1f, transmissionDuration);

    private float TransferProgress =>
        Mathf.Clamp01(transmissionTimer / TransferDuration);

    private void Awake()
    {
        Time.timeScale = 1f;

        if (emergencyAlarm != null)
        {
            emergencyAlarm.playOnAwake = false;
            emergencyAlarm.loop = true;
            emergencyAlarm.Stop();
        }
    }

    private void Start()
    {
        if (playerHealth == null ||
            corePoint == null ||
            extractionPoint == null ||
            coreEntranceDoor == null)
        {
            Debug.LogError(
                "MissionFlow: Player Health, Core Point, " +
                "Extraction Point, Core Entrance Door를 연결해 주세요.",
                this
            );

            enabled = false;
            return;
        }

        if (!coreEntranceDoor.IsReady)
        {
            Debug.LogError(
                "MissionFlow: 코어 입구 DoorController의 " +
                "연결 상태와 활성화 여부를 확인해 주세요.",
                this
            );

            enabled = false;
            return;
        }

        initialized = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (!initialized || isLoading || HasEnded)
            return;

        if (playerHealth.IsDead)
        {
            EndMission(false);
            return;
        }

        if (Time.timeScale == 0f)
            return;

        if (state == MissionState.SealingEntrance)
        {
            if (coreEntranceDoor.IsClosed)
            {
                transmissionTimer = 0f;
                state = MissionState.Transmitting;
            }

            return;
        }

        if (state == MissionState.Transmitting)
        {
            transmissionTimer = Mathf.Min(
                transmissionTimer + Time.deltaTime,
                TransferDuration
            );

            if (transmissionTimer >= TransferDuration)
                CompleteTransmission();

            return;
        }

        if (Cursor.lockState != CursorLockMode.Locked ||
            !Input.GetKeyDown(KeyCode.F))
        {
            return;
        }

        if (state == MissionState.FindCore && IsNear(corePoint))
        {
            if (coreEntranceDoor.CloseAndLock())
            {
                transmissionTimer = 0f;
                state = MissionState.SealingEntrance;
            }
            else
            {
                Debug.LogError(
                    "MissionFlow: 코어 입구 문을 제어할 수 없습니다.",
                    this
                );
            }
        }
        else if (state == MissionState.ReturnToStart &&
                 IsNear(extractionPoint))
        {
            EndMission(true);
        }
    }

    private void CompleteTransmission()
    {
        transmissionTimer = TransferDuration;
        state = MissionState.ReturnToStart;

        completionDisplayUntil =
            Time.time + Mathf.Max(0f, completionDisplayDuration);

        if (!coreEntranceDoor.UnlockAndOpen())
        {
            Debug.LogError(
                "MissionFlow: 코어 입구를 열 수 없습니다.",
                this
            );
        }

        if (emergencyAlarm != null && emergencyAlarm.clip != null)
            emergencyAlarm.Play();
    }

    private bool IsNear(Transform point)
    {
        if (playerHealth == null || point == null)
            return false;

        return Vector3.Distance(
            playerHealth.transform.position,
            point.position
        ) <= interactionDistance;
    }

    private void StopAlarm()
    {
        if (emergencyAlarm != null)
            emergencyAlarm.Stop();
    }

    private void EndMission(bool success)
    {
        state = success
            ? MissionState.Cleared
            : MissionState.Failed;

        StopAlarm();

        if (playerHealth.controlsToDisable != null)
        {
            foreach (Behaviour control in playerHealth.controlsToDisable)
            {
                if (control != null)
                    control.enabled = false;
            }
        }

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void LoadScene(string sceneName)
    {
        if (isLoading)
            return;

        if (string.IsNullOrWhiteSpace(sceneName) ||
            !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            loadError = "Scene missing from Build Profiles.";

            Debug.LogError(
                $"씬을 불러올 수 없습니다: {sceneName}. " +
                "Build Profiles의 Scene List를 확인해 주세요.",
                this
            );

            return;
        }

        isLoading = true;
        StopAlarm();
        Time.timeScale = 1f;

        SceneManager.LoadScene(sceneName);
    }

    private string GetObjective()
    {
        return state == MissionState.ReturnToStart
            ? "Return to your ship"
            : "Find the core";
    }

    private string GetObjectiveDetail()
    {
        return state == MissionState.ReturnToStart
            ? "Data secured. Extract from the starting room."
            : "Activate the terminal to start the transfer.";
    }

    private string GetPrompt(out bool requiresKey)
    {
        requiresKey = false;

        if (state == MissionState.FindCore && IsNear(corePoint))
        {
            requiresKey = true;
            return "START TRANSFER  /  ENTRANCE WILL LOCK";
        }

        if (state == MissionState.ReturnToStart &&
            IsNear(extractionPoint))
        {
            requiresKey = true;
            return "EXTRACT TO YOUR SHIP";
        }

        if (state == MissionState.FindCore &&
            IsNear(extractionPoint))
        {
            return "SECURE THE CORE DATA BEFORE EXTRACTION";
        }

        return "";
    }

    private void EnsureStyles()
    {
        if (bodyStyle != null)
            return;

        smallStyle = CreateStyle(
            13, TextAnchor.MiddleLeft, false
        );

        bodyStyle = CreateStyle(
            19, TextAnchor.MiddleLeft, true
        );

        titleStyle = CreateStyle(
            32, TextAnchor.MiddleCenter, true
        );

        centerStyle = CreateStyle(
            15, TextAnchor.MiddleCenter, true
        );

        transferTitleStyle = CreateStyle(
            21, TextAnchor.MiddleCenter, true
        );

        percentStyle = CreateStyle(
            20, TextAnchor.MiddleCenter, true
        );

        buttonStyle = new GUIStyle(centerStyle);
    }

    private GUIStyle CreateStyle(
        int size,
        TextAnchor alignment,
        bool bold)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            alignment = alignment,
            fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
            wordWrap = true,
            padding = new RectOffset(0, 0, 0, 0)
        };

        style.normal.textColor = Color.white;

        if (uiFont != null)
            style.font = uiFont;

        return style;
    }

    private void DrawRect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;

        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        GUI.color = previous;
    }

    private void DrawText(
        Rect rect,
        string text,
        GUIStyle style,
        Color color)
    {
        Color previous = GUI.contentColor;
        GUI.contentColor = color;

        GUI.Label(rect, text, style);

        GUI.contentColor = previous;
    }

    private void DrawPanel(Rect rect, Color accent)
    {
        DrawRect(
            new Rect(
                rect.x + 3f,
                rect.y + 4f,
                rect.width,
                rect.height
            ),
            new Color(0f, 0f, 0f, 0.18f)
        );

        DrawRect(rect, panelColor);

        DrawRect(
            new Rect(rect.x, rect.y, 3f, rect.height),
            accent
        );

        DrawRect(
            new Rect(rect.x, rect.yMax - 1f, rect.width, 1f),
            new Color(accent.r, accent.g, accent.b, 0.25f)
        );
    }

    private void DrawObjectivePanel()
    {
        // 전송 게이지 표시 중에는 왼쪽 위 목표 안내를 숨깁니다.
        if (ShowTransferPanel)
            return;

        float x = objectivePosition.x;
        float y = objectivePosition.y;
        float width = Mathf.Max(300f, objectiveWidth);

        DrawPanel(
            new Rect(x, y, width, 80f),
            accentColor
        );

        DrawText(
            new Rect(x + 17f, y + 10f, width - 34f, 27f),
            GetObjective(),
            bodyStyle,
            Color.white
        );

        DrawText(
            new Rect(x + 17f, y + 40f, width - 34f, 30f),
            GetObjectiveDetail(),
            smallStyle,
            new Color(0.67f, 0.76f, 0.82f)
        );
    }

    private void DrawTransferPanel(
        float logicalWidth,
        float logicalHeight)
    {
        if (!ShowTransferPanel)
            return;

        bool sealing = state == MissionState.SealingEntrance;
        bool completed = ShowCompletedTransfer;

        float width = logicalWidth *
            Mathf.Clamp(transferWidthRatio, 0.3f, 0.9f);

        float barHeight = Mathf.Clamp(
            transferBarHeight, 20f, 50f
        );

        // 아래 설명을 없앤 만큼 패널 높이도 줄입니다.
        float panelHeight = barHeight + 60f;

        float x = (logicalWidth - width) * 0.5f;

        float y = Mathf.Clamp(
            transferTopMargin,
            0f,
            Mathf.Max(0f, logicalHeight - panelHeight)
        );

        Color accent = sealing ? warningColor : accentColor;

        DrawPanel(
            new Rect(x, y, width, panelHeight),
            accent
        );

        string heading;

        if (sealing)
            heading = "PREPARING DATA TRANSFER";
        else if (completed)
            heading = "TRANSFER COMPLETE";
        else
            heading = "TRANSFERRING CORE DATA";

        DrawText(
            new Rect(x + 20f, y + 9f, width - 40f, 30f),
            heading,
            transferTitleStyle,
            Color.white
        );

        Rect track = new Rect(
            x + 20f,
            y + 45f,
            width - 40f,
            barHeight
        );

        // 게이지 테두리
        DrawRect(
            new Rect(
                track.x - 1f,
                track.y - 1f,
                track.width + 2f,
                track.height + 2f
            ),
            new Color(accent.r, accent.g, accent.b, 0.45f)
        );

        // 게이지 배경
        DrawRect(
            track,
            new Color(0.035f, 0.08f, 0.12f, 1f)
        );

        float progress = sealing ? 0f : TransferProgress;
        float fillWidth = track.width * progress;

        if (fillWidth > 0f)
        {
            // 진행률에 따라 왼쪽부터 채워집니다.
            DrawRect(
                new Rect(
                    track.x,
                    track.y,
                    fillWidth,
                    track.height
                ),
                accent
            );

            DrawRect(
                new Rect(track.x, track.y, fillWidth, 3f),
                new Color(1f, 1f, 1f, 0.3f)
            );

            if (progress < 1f)
            {
                float edgeWidth = Mathf.Min(3f, fillWidth);

                DrawRect(
                    new Rect(
                        track.x + fillWidth - edgeWidth,
                        track.y,
                        edgeWidth,
                        track.height
                    ),
                    new Color(1f, 1f, 1f, 0.85f)
                );
            }
        }

        string percent = sealing
            ? "--"
            : $"{Mathf.FloorToInt(progress * 100f)}%";

        // 숫자 그림자
        DrawText(
            new Rect(
                track.x + 1f,
                track.y + 1f,
                track.width,
                track.height
            ),
            percent,
            percentStyle,
            new Color(0f, 0f, 0f, 0.8f)
        );

        // 막대 가운데 퍼센트
        DrawText(
            track,
            percent,
            percentStyle,
            Color.white
        );
    }

    private void DrawPrompt()
    {
        string prompt = GetPrompt(out bool requiresKey);

        if (string.IsNullOrEmpty(prompt))
            return;

        DrawPanel(
            new Rect(340f, 610f, 600f, 58f),
            accentColor
        );

        if (requiresKey)
        {
            Rect keyRect = new Rect(356f, 621f, 36f, 36f);

            DrawRect(keyRect, accentColor);

            DrawText(
                keyRect,
                "F",
                centerStyle,
                new Color(0.02f, 0.06f, 0.09f)
            );

            DrawText(
                new Rect(408f, 620f, 510f, 38f),
                prompt,
                centerStyle,
                Color.white
            );
        }
        else
        {
            DrawText(
                new Rect(356f, 620f, 568f, 38f),
                prompt,
                centerStyle,
                new Color(0.7f, 0.78f, 0.84f)
            );
        }
    }

    private bool DrawButton(Rect rect, string text, Color accent)
    {
        bool hover =
            GUI.enabled && rect.Contains(Event.current.mousePosition);

        DrawRect(
            rect,
            hover
                ? new Color(accent.r, accent.g, accent.b, 0.25f)
                : new Color(0.06f, 0.1f, 0.15f, 1f)
        );

        DrawRect(
            new Rect(rect.x, rect.y, 3f, rect.height),
            accent
        );

        return GUI.Button(rect, text, buttonStyle);
    }

    private void DrawEndScreen()
    {
        bool success = state == MissionState.Cleared;
        Color color = success ? accentColor : warningColor;

        Matrix4x4 scaledMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.identity;

        DrawRect(
            new Rect(0f, 0f, Screen.width, Screen.height),
            new Color(0f, 0.015f, 0.03f, 0.75f)
        );

        GUI.matrix = scaledMatrix;

        DrawPanel(
            new Rect(340f, 190f, 600f, 350f),
            color
        );

        DrawText(
            new Rect(370f, 220f, 540f, 52f),
            success ? "MISSION COMPLETE" : "MISSION FAILED",
            titleStyle,
            Color.white
        );

        DrawText(
            new Rect(370f, 280f, 540f, 40f),
            success
                ? "Core data recovered. Extraction successful."
                : "Operator signal lost. Return and try again.",
            centerStyle,
            new Color(0.7f, 0.78f, 0.84f)
        );

        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && !isLoading;

        if (DrawButton(
            new Rect(400f, 340f, 480f, 58f),
            "RETRY MISSION",
            color))
        {
            LoadScene(SceneManager.GetActiveScene().path);
        }

        if (DrawButton(
            new Rect(400f, 412f, 480f, 58f),
            "MAIN MENU",
            color))
        {
            LoadScene(mainMenuSceneName);
        }

        GUI.enabled = previousEnabled;

        DrawText(
            new Rect(370f, 482f, 540f, 42f),
            loadError,
            centerStyle,
            warningColor
        );
    }

    private void OnGUI()
    {
        if (!enabled || !initialized ||
            Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        EnsureStyles();

        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;
        Color previousContentColor = GUI.contentColor;
        bool previousEnabled = GUI.enabled;

        float scale = Mathf.Min(
            Screen.width / 1280f,
            Screen.height / 720f
        );

        float logicalWidth = Screen.width / scale;
        float logicalHeight = Screen.height / scale;

        GUI.matrix = Matrix4x4.TRS(
            Vector3.zero,
            Quaternion.identity,
            Vector3.one * scale
        );

        GUI.color = Color.white;
        GUI.contentColor = Color.white;

        if (!HasEnded)
        {
            DrawObjectivePanel();
            DrawTransferPanel(logicalWidth, logicalHeight);
        }

        Vector3 centerOffset = new Vector3(
            (logicalWidth - 1280f) * 0.5f,
            (logicalHeight - 720f) * 0.5f,
            0f
        );

        GUI.matrix = Matrix4x4.TRS(
            centerOffset * scale,
            Quaternion.identity,
            Vector3.one * scale
        );

        if (HasEnded)
            DrawEndScreen();
        else
            DrawPrompt();

        GUI.enabled = previousEnabled;
        GUI.contentColor = previousContentColor;
        GUI.color = previousColor;
        GUI.matrix = previousMatrix;
    }

    private void OnDestroy()
    {
        StopAlarm();
    }
}