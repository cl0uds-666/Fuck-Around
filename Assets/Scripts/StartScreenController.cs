using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StartScreenController : MonoBehaviour
{
    private enum StartFlowState
    {
        MainMenu,
        Options,
        Intro,
        InGame
    }

    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject optionsPanel;
    public GameObject controlsPanel;
    public GameObject gameplayHudRoot;

    [Header("Buttons")]
    public Button startButton;
    public Button optionsButton;
    public Button quitButton;
    public Button backButton;
    public Button controlsButton;
    public Button controlsBackButton;

    [Header("Gameplay Scripts To Toggle")]
    public MonoBehaviour[] gameplaySystems;

    [Header("Camera Setup")]
    public Transform cameraTransform;
    public Transform menuCameraAnchor;
    public Transform gameplayCameraAnchor;
    public Transform gameplayCameraParent;
    public float cameraLerpDuration = 2f;

    [Header("Intro Actor")]
    public PassengerWalker introPassengerPrefab;
    public Transform introPassengerSpawnPoint;
    public Transform introPassengerBoardPoint;
    public float introMoveSpeed = 2.5f;
    public float fallbackIntroDuration = 4f;

    private StartFlowState currentState;
    private Coroutine introRoutine;

    private void Awake()
    {
        WireButtons();
    }

    private void Start()
    {
        EnterMainMenuState();
    }

    private void WireButtons()
    {
        if (startButton != null) startButton.onClick.AddListener(OnStartPressed);
        if (optionsButton != null) optionsButton.onClick.AddListener(OnOptionsPressed);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitPressed);
        if (backButton != null) backButton.onClick.AddListener(OnBackPressed);
        if (controlsButton != null) controlsButton.onClick.AddListener(OnControlsPressed);
        if (controlsBackButton != null) controlsBackButton.onClick.AddListener(OnControlsBackPressed);
    }

    private void EnterMainMenuState()
    {
        currentState = StartFlowState.MainMenu;

        SetGameplayEnabled(false);
        SetGameplayHudVisible(false);

        SetActiveSafe(mainMenuPanel, true);
        SetActiveSafe(optionsPanel, false);
        SetActiveSafe(controlsPanel, false);

        SetMenuButtonsInteractable(true);

        if (cameraTransform != null && menuCameraAnchor != null)
        {
            SnapCameraToAnchor(menuCameraAnchor);
        }
    }

    private void OnStartPressed()
    {
        if (currentState != StartFlowState.MainMenu)
        {
            return;
        }

        SetMenuButtonsInteractable(false);
        currentState = StartFlowState.Intro;
        SetActiveSafe(mainMenuPanel, false);

        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
        }

        introRoutine = StartCoroutine(RunIntroSequence());
    }

    private IEnumerator RunIntroSequence()
    {
        PassengerWalker introPassenger = null;
        bool introWalkerFinished = false;

        if (introPassengerPrefab != null && introPassengerSpawnPoint != null && introPassengerBoardPoint != null)
        {
            introPassenger = Instantiate(introPassengerPrefab, introPassengerSpawnPoint.position, introPassengerSpawnPoint.rotation);
            introPassenger.moveSpeed = introMoveSpeed;
            introPassenger.Setup(
                introPassengerBoardPoint.position,
                PassengerWalker.PassengerFlow.Boarding,
                _ => introWalkerFinished = true);
        }

        float fallbackTimer = 0f;
        while (introPassenger != null && !introWalkerFinished && fallbackTimer < fallbackIntroDuration)
        {
            fallbackTimer += Time.deltaTime;
            yield return null;
        }

        if (introPassenger != null)
        {
            Destroy(introPassenger.gameObject);
        }

        yield return StartCoroutine(BlendCameraToGameplay());

        EnterInGameState();
    }

    private IEnumerator BlendCameraToGameplay()
    {
        if (cameraTransform == null || gameplayCameraAnchor == null)
        {
            yield break;
        }

        float duration = Mathf.Max(0.01f, cameraLerpDuration);
        float elapsed = 0f;

        if (menuCameraAnchor != null)
        {
            SnapCameraToAnchor(menuCameraAnchor);
        }
        else
        {
            cameraTransform.SetParent(null, true);
        }

        Vector3 fromPos = cameraTransform.position;
        Quaternion fromRot = cameraTransform.rotation;
        Vector3 toPos = gameplayCameraAnchor.position;
        Quaternion toRot = gameplayCameraAnchor.rotation;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            cameraTransform.position = Vector3.Lerp(fromPos, toPos, t);
            cameraTransform.rotation = Quaternion.Slerp(fromRot, toRot, t);

            yield return null;
        }

        Transform gameplayParent = gameplayCameraParent != null ? gameplayCameraParent : gameplayCameraAnchor;
        SnapCameraToAnchor(gameplayParent);
    }

    private void EnterInGameState()
    {
        currentState = StartFlowState.InGame;
        SetGameplayEnabled(true);
        SetGameplayHudVisible(true);

        SetActiveSafe(mainMenuPanel, false);
        SetActiveSafe(optionsPanel, false);
        SetActiveSafe(controlsPanel, false);
    }

    private void OnOptionsPressed()
    {
        if (currentState != StartFlowState.MainMenu)
        {
            return;
        }

        currentState = StartFlowState.Options;
        SetActiveSafe(mainMenuPanel, false);
        SetActiveSafe(optionsPanel, true);
        SetActiveSafe(controlsPanel, false);
    }

    private void OnBackPressed()
    {
        if (currentState != StartFlowState.Options)
        {
            return;
        }

        currentState = StartFlowState.MainMenu;
        SetActiveSafe(mainMenuPanel, true);
        SetActiveSafe(optionsPanel, false);
        SetActiveSafe(controlsPanel, false);
    }

    private void OnControlsPressed()
    {
        if (currentState != StartFlowState.Options)
        {
            return;
        }

        SetActiveSafe(controlsPanel, true);
    }

    private void OnControlsBackPressed()
    {
        if (currentState != StartFlowState.Options)
        {
            return;
        }

        SetActiveSafe(controlsPanel, false);
    }

    private void OnQuitPressed()
    {
#if UNITY_EDITOR
        Debug.Log("Quit requested from start menu.");
#else
        Application.Quit();
#endif
    }


    private void SnapCameraToAnchor(Transform anchor)
    {
        if (cameraTransform == null || anchor == null)
        {
            return;
        }

        cameraTransform.SetParent(anchor, false);
        cameraTransform.localPosition = Vector3.zero;
        cameraTransform.localRotation = Quaternion.identity;
    }
    private void SetGameplayEnabled(bool enabled)
    {
        if (gameplaySystems == null)
        {
            return;
        }

        for (int i = 0; i < gameplaySystems.Length; i++)
        {
            MonoBehaviour system = gameplaySystems[i];
            if (system != null)
            {
                system.enabled = enabled;
            }
        }
    }

    private void SetGameplayHudVisible(bool visible)
    {
        SetActiveSafe(gameplayHudRoot, visible);
    }

    private void SetMenuButtonsInteractable(bool interactable)
    {
        if (startButton != null) startButton.interactable = interactable;
        if (optionsButton != null) optionsButton.interactable = interactable;
        if (quitButton != null) quitButton.interactable = interactable;
    }

    private static void SetActiveSafe(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }
}
