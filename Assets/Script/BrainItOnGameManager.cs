using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class BrainItOnGameManager : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private LineDrawingController lineDrawingController;
    [SerializeField] private GoalZone levelGoalZone;

    [Header("Ball Freeze While Drawing")]
    [SerializeField] private Rigidbody2D ballRigidbody;
    [SerializeField] private bool freezeBallWhileDrawing = true;

    [Header("UI (Optional)")]
    [SerializeField] private TMP_Text shapeCountTextTMP;
    [SerializeField] private TMP_Text releaseTimerTextTMP;
    [SerializeField] private Text shapeCountText;
    [SerializeField] private Text releaseTimerText;
    [SerializeField] private TMP_Text goalHoldTimerTextTMP;
    [SerializeField] private Text goalHoldTimerText;
    [SerializeField] private string shapeCountPrefix = "Shapes: ";
    [SerializeField] private string releaseTimerPrefix = "Time Left: ";
    [SerializeField] private string releaseTimerSuffix = "s";
    [SerializeField] private string goalHoldTimerPrefix = "Win In: ";
    [SerializeField] private string goalHoldTimerSuffix = "s";
    [SerializeField] private int goalHoldTimerDecimals = 1;
    [SerializeField] private bool hideGoalHoldTimerWhenIdle = true;
    [SerializeField] private int releaseTimerDecimals = 0;
    [SerializeField] private float levelTimeLimitSeconds = 20f;

    [Header("Level End Menu (Win Only)")]
    [SerializeField] private GameObject levelEndMenuRoot;
    [SerializeField] private TMP_Text levelEndShapesNeededTextTMP;
    [SerializeField] private TMP_Text levelEndTimeTakenTextTMP;
    [SerializeField] private Text levelEndShapesNeededText;
    [SerializeField] private Text levelEndTimeTakenText;
    [SerializeField] private string levelEndShapesNeededPrefix = "Shapes Needed: ";
    [SerializeField] private string levelEndTimeTakenPrefix = "Time Taken: ";
    [SerializeField] private string levelEndTimeTakenSuffix = "s";
    [SerializeField] private string homeSceneName = "Home";

    [Header("Level Progress")]
    [SerializeField] private int currentLevelNumberOverride = 0;

    private bool isLevelWon = false;
    private bool ballFrozenByDrawing;
    private bool hasReleasedInitialBall;
    private bool hasCachedBallConstraints;
    private RigidbodyConstraints2D cachedBallConstraints;
    private float releaseTimerSecondsRemaining;
    private bool isReleaseTimerRunning;
    private bool hasReleaseTimerStarted;
    private bool isLevelTimedOut;

    private void Awake()
    {
        TryResolveBallRigidbody();
        FreezeBallForDrawingState();
    }

    private void FixedUpdate()
    {
        if (!hasReleasedInitialBall)
        {
            FreezeBallForDrawingState();
        }
    }

    private void OnEnable()
    {
        if (levelGoalZone != null)
        {
            levelGoalZone.OnGoalAchieved += HandleGoalAchieved;
            levelGoalZone.OnHoldProgress += HandleHoldProgress;
        }

        if (lineDrawingController != null)
        {
            lineDrawingController.DrawingStarted += HandleDrawingStarted;
            lineDrawingController.DrawingStopped += HandleDrawingStopped;
            lineDrawingController.LineFinished += HandleLineFinished;
        }
    }

    private void OnDisable()
    {
        if (levelGoalZone != null)
        {
            levelGoalZone.OnGoalAchieved -= HandleGoalAchieved;
            levelGoalZone.OnHoldProgress -= HandleHoldProgress;
        }

        if (lineDrawingController != null)
        {
            lineDrawingController.DrawingStarted -= HandleDrawingStarted;
            lineDrawingController.DrawingStopped -= HandleDrawingStopped;
            lineDrawingController.LineFinished -= HandleLineFinished;
        }
    }

    private void Update()
    {
        if (WasEscapePressedThisFrame())
        {
            GoToHomePage();
            return;
        }

        if (!isReleaseTimerRunning || isLevelWon || isLevelTimedOut)
        {
            return;
        }

        releaseTimerSecondsRemaining = Mathf.Max(0f, releaseTimerSecondsRemaining - Time.deltaTime);
        UpdateReleaseTimerText();

        if (releaseTimerSecondsRemaining <= 0f)
        {
            HandleLevelTimedOut();
        }
    }

        private static bool WasEscapePressedThisFrame()
        {
    #if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    #else
        return Input.GetKeyDown(KeyCode.Escape);
    #endif
        }

    private void Start()
    {
        TryResolveBallRigidbody();
        FreezeBallForDrawingState();
        releaseTimerSecondsRemaining = Mathf.Max(0f, levelTimeLimitSeconds);
        isReleaseTimerRunning = false;
        hasReleaseTimerStarted = false;
        isLevelTimedOut = false;
        SetLevelEndMenuVisible(false);
        UpdateShapeCountText();
        UpdateReleaseTimerText();
        UpdateGoalHoldTimerUI(0f);

        if (lineDrawingController != null)
        {
            // Optional: reset drawing
            lineDrawingController.SetDrawingAllowed(true);
        }

        if (freezeBallWhileDrawing && ballRigidbody == null)
        {
            return;
        }
    }

    private void HandleGoalAchieved()
    {
        if (isLevelWon) return;

        isLevelWon = true;

        int resolvedLevelNumber = ResolveCurrentLevelNumber();
        if (resolvedLevelNumber > 0)
        {
            MenuManager.MarkLevelComplete(resolvedLevelNumber);
        }
        if (lineDrawingController != null)
        {
            lineDrawingController.SetDrawingAllowed(false);
        }

        StopReleaseTimer();
        UpdateGoalHoldTimerUI(1f);
        UpdateAndShowLevelEndMenu();
    }

    private void HandleDrawingStarted()
    {
        if (!hasReleasedInitialBall)
        {
            FreezeBallForDrawingState();
        }
    }

    private void HandleDrawingStopped()
    {
        if (isLevelWon || isLevelTimedOut)
        {
            return;
        }

        if (!hasReleasedInitialBall)
        {
            ForceUnfreezeBall();

            ballFrozenByDrawing = false;
            hasReleasedInitialBall = true;
        }

        if (!hasReleaseTimerStarted)
        {
            StartReleaseTimer();
        }
    }

    private void FreezeBallForDrawingState()
    {
        TryResolveBallRigidbody();

        if (!freezeBallWhileDrawing || ballRigidbody == null || isLevelWon)
        {
            return;
        }

        if (!hasCachedBallConstraints)
        {
            cachedBallConstraints = ballRigidbody.constraints;
            hasCachedBallConstraints = true;
        }

        ballRigidbody.linearVelocity = Vector2.zero;
        ballRigidbody.angularVelocity = 0f;
        ballRigidbody.constraints = RigidbodyConstraints2D.FreezeAll;
        ballRigidbody.simulated = true;
        ballFrozenByDrawing = true;
    }

    private void ForceUnfreezeBall()
    {
        if (ballRigidbody == null)
        {
            return;
        }

        if (hasCachedBallConstraints)
        {
            ballRigidbody.constraints = cachedBallConstraints;
        }
        else
        {
            ballRigidbody.constraints = RigidbodyConstraints2D.None;
        }

        ballRigidbody.simulated = true;
        ballRigidbody.WakeUp();
    }

    private void TryResolveBallRigidbody()
    {
        if (ballRigidbody != null)
        {
            return;
        }

        Rigidbody2D fallbackCandidate = null;
        Rigidbody2D[] rigidbodies = FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None);

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody2D candidate = rigidbodies[i];
            if (candidate == null || candidate.bodyType != RigidbodyType2D.Dynamic)
            {
                continue;
            }

            if (lineDrawingController != null && candidate.transform.IsChildOf(lineDrawingController.transform))
            {
                continue;
            }

            string candidateName = candidate.gameObject.name;
            string lowercaseName = candidateName.ToLowerInvariant();
            if (lowercaseName.Contains("glass"))
            {
                continue;
            }

            if (lowercaseName.Contains("ball") || candidate.tag == "TargetObject" || candidate.tag == "Water")
            {
                ballRigidbody = candidate;
                return;
            }

            if (fallbackCandidate == null)
            {
                fallbackCandidate = candidate;
            }
        }

        ballRigidbody = fallbackCandidate;
    }

    private void HandleHoldProgress(float progress)
    {
        if (isLevelWon) return;

        UpdateGoalHoldTimerUI(progress);
    }

    private void HandleLineFinished()
    {
        UpdateShapeCountText();
    }

    private void StartReleaseTimer()
    {
        if (isLevelWon || isLevelTimedOut)
        {
            return;
        }

        if (!hasReleaseTimerStarted)
        {
            releaseTimerSecondsRemaining = Mathf.Max(0f, levelTimeLimitSeconds);
            hasReleaseTimerStarted = true;
        }

        isReleaseTimerRunning = true;
        UpdateReleaseTimerText();
    }

    private void StopReleaseTimer()
    {
        isReleaseTimerRunning = false;
        UpdateReleaseTimerText();
    }

    private void UpdateShapeCountText()
    {
        int finishedShapeCount = lineDrawingController != null ? lineDrawingController.FinishedLineCount : 0;
        int totalShapeCount = lineDrawingController != null ? Mathf.Max(0, lineDrawingController.MaximumLinesPerRound) : 0;
        string textValue = finishedShapeCount + "/" + totalShapeCount;

        if (shapeCountTextTMP != null)
        {
            shapeCountTextTMP.text = textValue;
        }

        if (shapeCountText != null)
        {
            shapeCountText.text = textValue;
        }
    }

    private void UpdateReleaseTimerText()
    {
        int decimals = Mathf.Clamp(releaseTimerDecimals, 0, 3);
        string textValue = releaseTimerPrefix + releaseTimerSecondsRemaining.ToString("F" + decimals) + releaseTimerSuffix;

        if (releaseTimerTextTMP != null)
        {
            releaseTimerTextTMP.text = textValue;
        }

        if (releaseTimerText != null)
        {
            releaseTimerText.text = textValue;
        }
    }

    private void UpdateGoalHoldTimerUI(float holdProgress)
    {
        if (goalHoldTimerTextTMP == null && goalHoldTimerText == null)
        {
            return;
        }

        float requiredHoldTime = levelGoalZone != null ? Mathf.Max(0f, levelGoalZone.RequiredHoldTime) : 0f;
        float clampedProgress = Mathf.Clamp01(holdProgress);
        float remainingSeconds = Mathf.Max(0f, requiredHoldTime * (1f - clampedProgress));
        int decimals = Mathf.Clamp(goalHoldTimerDecimals, 0, 3);
        string textValue = goalHoldTimerPrefix + remainingSeconds.ToString("F" + decimals) + goalHoldTimerSuffix;

        bool isVisible = !hideGoalHoldTimerWhenIdle || clampedProgress > 0f;

        if (goalHoldTimerTextTMP != null)
        {
            goalHoldTimerTextTMP.text = textValue;
            goalHoldTimerTextTMP.gameObject.SetActive(isVisible);
        }

        if (goalHoldTimerText != null)
        {
            goalHoldTimerText.text = textValue;
            goalHoldTimerText.gameObject.SetActive(isVisible);
        }
    }

    private void HandleLevelTimedOut()
    {
        if (isLevelWon || isLevelTimedOut)
        {
            return;
        }

        isLevelTimedOut = true;
        isReleaseTimerRunning = false;

        if (lineDrawingController != null)
        {
            lineDrawingController.SetDrawingAllowed(false);
        }
        SetLevelEndMenuVisible(false);
    }

    public void RestartLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToHomePage()
    {
        if (string.IsNullOrWhiteSpace(homeSceneName))
        {
            return;
        }

        SceneManager.LoadScene(homeSceneName);
    }

    public void LoadNextLevel()
    {
        int nextBuildIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextBuildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            return;
        }

        SceneManager.LoadScene(nextBuildIndex);
    }

    private void UpdateAndShowLevelEndMenu()
    {
        int shapesNeededCount = lineDrawingController != null ? lineDrawingController.FinishedLineCount : 0;

        float timeTakenSeconds = hasReleaseTimerStarted
            ? Mathf.Max(0f, levelTimeLimitSeconds - releaseTimerSecondsRemaining)
            : 0f;


        int decimals = Mathf.Clamp(releaseTimerDecimals, 0, 3);

        string shapesNeededTextValue = levelEndShapesNeededPrefix + shapesNeededCount;

        string timeTakenTextValue = levelEndTimeTakenPrefix + timeTakenSeconds.ToString("F" + decimals) + levelEndTimeTakenSuffix;
        

        if (levelEndShapesNeededTextTMP != null)
        {

            levelEndShapesNeededTextTMP.text = shapesNeededTextValue;
        }

        if (levelEndShapesNeededText != null)
        {
           
            levelEndShapesNeededText.text = shapesNeededTextValue;
        }

        if (levelEndTimeTakenTextTMP != null)
        {
            levelEndTimeTakenTextTMP.text = timeTakenTextValue;
        }

        if (levelEndTimeTakenText != null)
        {

            levelEndTimeTakenText.text = timeTakenTextValue;
        }

        SetLevelEndMenuVisible(true);
    }

    private int ResolveCurrentLevelNumber()
    {
        if (currentLevelNumberOverride > 0)
        {
            return currentLevelNumberOverride;
        }

        string sceneName = SceneManager.GetActiveScene().name;
        int value = 0;
        bool foundDigit = false;

        for (int i = 0; i < sceneName.Length; i++)
        {
            char c = sceneName[i];
            if (c >= '0' && c <= '9')
            {
                value = (value * 10) + (c - '0');
                foundDigit = true;
            }
        }

        if (foundDigit && value > 0)
        {
            return value;
        }

        int buildIndex = SceneManager.GetActiveScene().buildIndex;
        if (buildIndex > 0)
        {
            return buildIndex;
        }

        return 0;
    }

    private void SetLevelEndMenuVisible(bool isVisible)
    {
        if (levelEndMenuRoot != null)
        {
            
            levelEndMenuRoot.SetActive(isVisible);
        }
    }
}
