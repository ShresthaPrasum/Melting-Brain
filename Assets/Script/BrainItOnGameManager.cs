using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;

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
    [SerializeField] private string shapeCountPrefix = "Shapes: ";
    [SerializeField] private string releaseTimerPrefix = "Time Left: ";
    [SerializeField] private string releaseTimerSuffix = "s";
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

        if (lineDrawingController != null)
        {
            // Optional: reset drawing
            lineDrawingController.SetDrawingAllowed(true);
        }

        if (freezeBallWhileDrawing && ballRigidbody == null)
        {
            Debug.LogWarning("BrainItOnGameManager: Ball Rigidbody2D is not assigned and could not be auto-detected.");
        }
    }

    private void HandleGoalAchieved()
    {
        if (isLevelWon) return;

        isLevelWon = true;

        if (lineDrawingController != null)
        {
            lineDrawingController.SetDrawingAllowed(false);
        }

        StopReleaseTimer();
        UpdateAndShowLevelEndMenu();

        Debug.Log("LEVEL PASSED! You achieved the goal.");
        // Trigger Win UI here
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
            if (ballRigidbody != null && ballFrozenByDrawing && hasCachedBallConstraints)
            {
                ballRigidbody.constraints = cachedBallConstraints;
            }

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
        ballFrozenByDrawing = true;
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

        // Progress goes from 0.0 to 1.0. Display this on a UI bar to show player they are almost there
        if (progress > 0f)
        {
            Debug.Log($"Holding Target... {(progress * 100).ToString("F0")}%");
        }
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
        int remainingShapeCount = Mathf.Max(0, totalShapeCount - finishedShapeCount);
        string textValue = shapeCountPrefix + remainingShapeCount + "/" + totalShapeCount;

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

        Debug.Log("TIME UP! Level failed.");
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
            Debug.LogWarning("BrainItOnGameManager: Home Scene Name is empty.");
            return;
        }

        SceneManager.LoadScene(homeSceneName);
    }

    public void LoadNextLevel()
    {
        int nextBuildIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextBuildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogWarning("BrainItOnGameManager: No next level found in Build Settings.");
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

    private void SetLevelEndMenuVisible(bool isVisible)
    {
        if (levelEndMenuRoot != null)
        {
            levelEndMenuRoot.SetActive(isVisible);
        }
    }
}
