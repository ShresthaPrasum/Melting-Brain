using UnityEngine;

public class ObjectMotionController : MonoBehaviour
{
    public enum MotionMode
    {
        Move,
        Rotate,
        MoveAndRotate
    }

    public enum LoopMode
    {
        Once,
        Loop,
        PingPong
    }

    public enum TickMode
    {
        Update,
        FixedUpdate,
        LateUpdate
    }

    [Header("General")]
    [SerializeField] private MotionMode motionMode = MotionMode.MoveAndRotate;
    [SerializeField] private LoopMode loopMode = LoopMode.Loop;
    [SerializeField] private TickMode tickMode = TickMode.Update;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool useLocalSpace = true;
    [SerializeField] private bool useUnscaledTime = false;
    [SerializeField] private AnimationCurve interpolationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Move")]
    [SerializeField] private Vector3 moveOffset = new Vector3(2f, 0f, 0f);
    [SerializeField] private float moveDuration = 2f;

    [Header("Rotate")]
    [SerializeField] private Vector3 rotationOffsetEuler = new Vector3(0f, 0f, 90f);
    [SerializeField] private float rotateDuration = 2f;

    private Vector3 initialPosition;
    private Quaternion initialRotation;

    private float moveElapsed;
    private float rotateElapsed;
    private bool moveForward = true;
    private bool rotateForward = true;
    private bool moveCompleted;
    private bool rotateCompleted;
    private bool isPlaying;

    private void Awake()
    {
        CacheInitialTransform();
    }

    private void OnEnable()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    private void Update()
    {
        if (tickMode == TickMode.Update)
        {
            Tick(GetDeltaTime(false));
        }
    }

    private void FixedUpdate()
    {
        if (tickMode == TickMode.FixedUpdate)
        {
            Tick(GetDeltaTime(true));
        }
    }

    private void LateUpdate()
    {
        if (tickMode == TickMode.LateUpdate)
        {
            Tick(GetDeltaTime(false));
        }
    }

    public void Play()
    {
        isPlaying = true;
    }

    public void Pause()
    {
        isPlaying = false;
    }

    public void StopAndReset()
    {
        isPlaying = false;

        moveElapsed = 0f;
        rotateElapsed = 0f;
        moveForward = true;
        rotateForward = true;
        moveCompleted = false;
        rotateCompleted = false;

        ApplyTransform(0f, 0f);
    }

    public void RecacheInitialTransform()
    {
        CacheInitialTransform();
    }

    private void CacheInitialTransform()
    {
        initialPosition = useLocalSpace ? transform.localPosition : transform.position;
        initialRotation = useLocalSpace ? transform.localRotation : transform.rotation;
    }

    private float GetDeltaTime(bool isFixedStep)
    {
        if (!useUnscaledTime)
        {
            return isFixedStep ? Time.fixedDeltaTime : Time.deltaTime;
        }

        return isFixedStep ? Time.fixedUnscaledDeltaTime : Time.unscaledDeltaTime;
    }

    private void Tick(float deltaTime)
    {
        if (!isPlaying || deltaTime <= 0f)
        {
            return;
        }

        float moveT = 0f;
        float rotateT = 0f;

        if (UsesMove())
        {
            moveT = Advance(ref moveElapsed, moveDuration, ref moveForward, ref moveCompleted, deltaTime);
        }

        if (UsesRotate())
        {
            rotateT = Advance(ref rotateElapsed, rotateDuration, ref rotateForward, ref rotateCompleted, deltaTime);
        }

        ApplyTransform(moveT, rotateT);

        if (loopMode == LoopMode.Once && (!UsesMove() || moveCompleted) && (!UsesRotate() || rotateCompleted))
        {
            isPlaying = false;
        }
    }

    private bool UsesMove()
    {
        return motionMode == MotionMode.Move || motionMode == MotionMode.MoveAndRotate;
    }

    private bool UsesRotate()
    {
        return motionMode == MotionMode.Rotate || motionMode == MotionMode.MoveAndRotate;
    }

    private float Advance(ref float elapsed, float duration, ref bool forward, ref bool completed, float deltaTime)
    {
        if (duration <= 0f)
        {
            completed = true;
            return interpolationCurve.Evaluate(1f);
        }

        if (loopMode == LoopMode.Once && completed)
        {
            return interpolationCurve.Evaluate(1f);
        }

        float direction = forward ? 1f : -1f;
        elapsed += deltaTime * direction;

        if (loopMode == LoopMode.Once)
        {
            if (elapsed >= duration)
            {
                elapsed = duration;
                completed = true;
            }
            else if (elapsed < 0f)
            {
                elapsed = 0f;
            }
        }
        else if (loopMode == LoopMode.Loop)
        {
            while (elapsed >= duration)
            {
                elapsed -= duration;
            }

            while (elapsed < 0f)
            {
                elapsed += duration;
            }
        }
        else
        {
            if (elapsed >= duration)
            {
                elapsed = duration;
                forward = false;
            }
            else if (elapsed <= 0f)
            {
                elapsed = 0f;
                forward = true;
            }
        }

        float normalized = Mathf.Clamp01(elapsed / duration);
        return interpolationCurve.Evaluate(normalized);
    }

    private void ApplyTransform(float moveT, float rotateT)
    {
        if (UsesMove())
        {
            Vector3 targetPosition = initialPosition + moveOffset;
            Vector3 nextPosition = Vector3.LerpUnclamped(initialPosition, targetPosition, moveT);

            if (useLocalSpace)
            {
                transform.localPosition = nextPosition;
            }
            else
            {
                transform.position = nextPosition;
            }
        }

        if (UsesRotate())
        {
            Quaternion targetRotation = initialRotation * Quaternion.Euler(rotationOffsetEuler);
            Quaternion nextRotation = Quaternion.SlerpUnclamped(initialRotation, targetRotation, rotateT);

            if (useLocalSpace)
            {
                transform.localRotation = nextRotation;
            }
            else
            {
                transform.rotation = nextRotation;
            }
        }
    }

    private void OnValidate()
    {
        moveDuration = Mathf.Max(0f, moveDuration);
        rotateDuration = Mathf.Max(0f, rotateDuration);
    }
}
