using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(VideoPlayer))]
public class IntroCutsceneController : MonoBehaviour
{
    private VideoPlayer videoPlayer;
    private bool isLoading;

    private const string GameScene = "Stage01_CargoDeck_AssetBuild";

    private void Awake()
    {
        videoPlayer = GetComponent<VideoPlayer>();

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
    }

    private void Start()
    {
        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;

        videoPlayer.prepareCompleted += OnPrepared;
        videoPlayer.loopPointReached += OnVideoFinished;
        videoPlayer.errorReceived += OnVideoError;

        videoPlayer.Prepare();
    }

    private void Update()
    {
        if (isLoading)
            return;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            (Keyboard.current.spaceKey.wasPressedThisFrame ||
             Keyboard.current.escapeKey.wasPressedThisFrame))
        {
            LoadGame();
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.Escape))
        {
            LoadGame();
        }
#endif
    }

    private void OnPrepared(VideoPlayer player)
    {
        if (!isLoading)
        {
            player.Play();
        }
    }

    private void OnVideoFinished(VideoPlayer player)
    {
        LoadGame();
    }

    private void OnVideoError(VideoPlayer player, string message)
    {
        Debug.LogError("인트로 영상 재생 오류: " + message);
        LoadGame();
    }

    private void LoadGame()
    {
        if (isLoading)
            return;

        if (!Application.CanStreamedLevelBeLoaded(GameScene))
        {
            Debug.LogError(
                "Scene List에 게임 Scene을 등록해 주세요: " + GameScene);
            return;
        }

        isLoading = true;
        StartCoroutine(StopVideoAndLoadGame());
    }

    private IEnumerator StopVideoAndLoadGame()
    {
        // 영상 종료 이벤트 처리가 끝난 다음 프레임에 정리합니다.
        yield return null;

        UnsubscribeVideoEvents();

        // 영상 재생을 중지하고 내부 버퍼 등의 자원을 정리합니다.
        videoPlayer.Stop();

        // 영상 정리와 Scene 로드를 서로 다른 프레임에 진행합니다.
        yield return null;

        SceneManager.LoadSceneAsync(GameScene);
    }

    private void UnsubscribeVideoEvents()
    {
        if (videoPlayer == null)
            return;

        videoPlayer.prepareCompleted -= OnPrepared;
        videoPlayer.loopPointReached -= OnVideoFinished;
        videoPlayer.errorReceived -= OnVideoError;
    }

    private void OnDestroy()
    {
        UnsubscribeVideoEvents();
    }
}