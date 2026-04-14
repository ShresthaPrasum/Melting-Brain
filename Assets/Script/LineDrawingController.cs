using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class LineDrawingController : MonoBehaviour
{
    [Header("Line Look")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private float lineWidth = 0.14f;
    [SerializeField] private Color lineColor = Color.black;
    [SerializeField] private int lineSortingOrder = 5;

    [Header("Line Shape")]
    [SerializeField] private float minimumDistanceBetweenPoints = 0.08f;
    [SerializeField] private float maximumLineLength = 8f;
    [SerializeField] private float colliderThickness = 0.14f;

    [Header("Line Rigidbody After Release")]
    [SerializeField] private RigidbodyType2D lineBodyTypeAfterRelease = RigidbodyType2D.Dynamic;
    [SerializeField] private float lineGravityAfterRelease = 1f;
    [SerializeField] private bool freezeLineRotationAfterRelease = false;
    [SerializeField] private float massPerUnitLength = 0.5f;

    [Header("Drawing Rules")]
    [SerializeField] private int maximumLinesPerRound = 3;
    [SerializeField] private bool lockDrawingAfterLineLimit = false;

    [Header("Collision Blocking")]
    [SerializeField] private bool stopLineAtColliders = true;
    [SerializeField] private bool lineCanCrossItself = true;
    [SerializeField] private bool stopLineAtOtherDrawnLines = true;
    [SerializeField] private bool preventStartingInsideBlocker = true;
    [SerializeField] private LayerMask drawingBlockerLayers = ~0;
    [SerializeField] private bool blockersIncludeTriggers = false;
    [SerializeField] private float blockerSurfaceOffset = 0.01f;

    private readonly List<Vector2> lineWorldPoints = new List<Vector2>();

    private Camera cachedMainCamera;
    private Material runtimeLineMaterial;

    private GameObject currentLineObject;
    private LineRenderer currentLineRenderer;
    private PolygonCollider2D currentLinePolygonCollider;
    private Rigidbody2D currentLineRigidbody;

    private bool drawingAllowed = true;
    private bool isCurrentlyDrawing;
    private int finishedLineCount;
    private float currentLineLength;

    public event Action LineFinished;
    public event Action FirstLineFinished;
    public event Action DrawingStarted;
    public event Action DrawingStopped;

    public int FinishedLineCount => finishedLineCount;
    public int MaximumLinesPerRound => maximumLinesPerRound;

    private void Awake()
    {
        cachedMainCamera = Camera.main;
    }

    private void Update()
    {
        if (!drawingAllowed)
        {
            return;
        }

        if (TryGetPointerDown(out Vector2 downScreenPosition))
        {
            if (!IsPointerOnUI())
            {
                Vector2 startWorldPoint = ConvertScreenToWorld(downScreenPosition);
                StartNewLine(startWorldPoint);
            }
        }

        if (!isCurrentlyDrawing)
        {
            return;
        }

        if (TryGetPointerHeld(out Vector2 holdScreenPosition))
        {
            Vector2 nextWorldPoint = ConvertScreenToWorld(holdScreenPosition);
            AddPointIfNeeded(nextWorldPoint);
        }

        if (TryGetPointerUp(out _))
        {
            FinishCurrentLine();
        }
    }

    public void SetDrawingAllowed(bool canDraw)
    {
        drawingAllowed = canDraw;

        if (!drawingAllowed && isCurrentlyDrawing)
        {
            FinishCurrentLine();
        }
    }
    // /kill @e drawnlines iykyk
    public void ClearAllDrawnLines()
    {
        bool wasDrawing = isCurrentlyDrawing;

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }

        lineWorldPoints.Clear();
        isCurrentlyDrawing = false;
        drawingAllowed = true;
        finishedLineCount = 0;
        currentLineLength = 0f;

        currentLineObject = null;
        currentLineRenderer = null;
        currentLinePolygonCollider = null;
        currentLineRigidbody = null;

        if (wasDrawing)
        {
            DrawingStopped?.Invoke();
        }
    }

    private void StartNewLine(Vector2 startWorldPoint)
    {
        if (isCurrentlyDrawing)
        {
            return;
        }

        if (maximumLinesPerRound > 0 && finishedLineCount >= maximumLinesPerRound)
        {
            return;
        }

        if (cachedMainCamera == null)
        {
            cachedMainCamera = Camera.main;
            if (cachedMainCamera == null)
            {
                return;
            }
        }

        if (preventStartingInsideBlocker && IsInsideBlockingCollider(startWorldPoint))
        {
            return;
        }

        currentLineObject = new GameObject("PlayerLine_" + (finishedLineCount + 1));
        currentLineObject.transform.SetParent(transform, true);

    // RIgidibody this is my favourite part
        currentLineRigidbody = currentLineObject.AddComponent<Rigidbody2D>();
        currentLineRigidbody.bodyType = RigidbodyType2D.Kinematic;
        currentLineRigidbody.gravityScale = 0f;
        currentLineRigidbody.simulated = false;
        currentLineRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
        currentLineRigidbody.constraints = freezeLineRotationAfterRelease
            ? RigidbodyConstraints2D.FreezeRotation
            : RigidbodyConstraints2D.None;

        currentLineRenderer = currentLineObject.AddComponent<LineRenderer>();
        currentLineRenderer.useWorldSpace = false;
        currentLineRenderer.startWidth = lineWidth;
        currentLineRenderer.endWidth = lineWidth;
        currentLineRenderer.positionCount = 0;
        currentLineRenderer.material = GetLineMaterial();
        currentLineRenderer.startColor = lineColor;
        currentLineRenderer.endColor = lineColor;
        currentLineRenderer.numCapVertices = 8;
        currentLineRenderer.numCornerVertices = 6;
        currentLineRenderer.sortingOrder = lineSortingOrder;

        currentLinePolygonCollider = currentLineObject.AddComponent<PolygonCollider2D>();

        lineWorldPoints.Clear();
        currentLineLength = 0f;
        isCurrentlyDrawing = true;

        DrawingStarted?.Invoke();

        AddPointToCurrentLine(startWorldPoint);
    }

    private void AddPointIfNeeded(Vector2 worldPoint)
    {
        if (lineWorldPoints.Count == 0)
        {
            AddPointToCurrentLine(worldPoint);
            return;
        }

        Vector2 lastPoint = lineWorldPoints[lineWorldPoints.Count - 1];
        bool hitBlockingCollider = TryGetClampedPointAtBlockingCollider(lastPoint, worldPoint, out Vector2 clampedWorldPoint);
        if (hitBlockingCollider)
        {
            worldPoint = clampedWorldPoint;
        }

        float newSegmentLength = Vector2.Distance(lastPoint, worldPoint);

        if (newSegmentLength < minimumDistanceBetweenPoints)
        {
            if (hitBlockingCollider)
            {
                FinishCurrentLine();
            }

            return;
        }

        float remainingLength = maximumLineLength - currentLineLength;
        if (remainingLength <= 0f)
        {
            FinishCurrentLine();
            return;
        }

        if (newSegmentLength > remainingLength)
        {
            Vector2 direction = (worldPoint - lastPoint).normalized;
            worldPoint = lastPoint + direction * remainingLength;
            newSegmentLength = remainingLength;
        }

        currentLineLength += newSegmentLength;
        AddPointToCurrentLine(worldPoint);

        if (hitBlockingCollider)
        {
            FinishCurrentLine();
            return;
        }

        if (currentLineLength >= maximumLineLength - 0.0001f)
        {
            FinishCurrentLine();
        }
    }

    private void AddPointToCurrentLine(Vector2 worldPoint)
    {
        lineWorldPoints.Add(worldPoint);

        Vector2 localPoint = currentLineObject.transform.InverseTransformPoint(worldPoint);

        int pointIndex = lineWorldPoints.Count - 1;
        currentLineRenderer.positionCount = lineWorldPoints.Count;
        currentLineRenderer.SetPosition(pointIndex, localPoint);

        if (lineWorldPoints.Count < 2)
        {
            return;
        }

        Vector2[] localPoints = new Vector2[lineWorldPoints.Count];
        for (int i = 0; i < lineWorldPoints.Count; i++)
        {
            localPoints[i] = currentLineObject.transform.InverseTransformPoint(lineWorldPoints[i]);
        }

        UpdatePolygonColliderFromLine(localPoints);
    }

    private void UpdatePolygonColliderFromLine(Vector2[] localPoints)
    {
        if (currentLinePolygonCollider == null || localPoints == null || localPoints.Length < 2)
        {
            return;
        }

        float halfThickness = Mathf.Max(0.001f, colliderThickness * 0.5f);

        int pointCount = localPoints.Length;
        Vector2[] leftSidePoints = new Vector2[pointCount];
        Vector2[] rightSidePoints = new Vector2[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            Vector2 direction;

            if (i == 0)
            {
                direction = (localPoints[1] - localPoints[0]).normalized;
            }
            else if (i == pointCount - 1)
            {
                direction = (localPoints[pointCount - 1] - localPoints[pointCount - 2]).normalized;
            }
            else
            {
                Vector2 directionA = (localPoints[i] - localPoints[i - 1]).normalized;
                Vector2 directionB = (localPoints[i + 1] - localPoints[i]).normalized;
                direction = (directionA + directionB).normalized;

                if (direction.sqrMagnitude < 0.0001f)
                {
                    direction = directionB;
                }
            }

            Vector2 normal = new Vector2(-direction.y, direction.x).normalized;

            leftSidePoints[i] = localPoints[i] + normal * halfThickness;
            rightSidePoints[i] = localPoints[i] - normal * halfThickness;
        }

        Vector2[] polygonPath = new Vector2[pointCount * 2];
        for (int i = 0; i < pointCount; i++)
        {
            polygonPath[i] = leftSidePoints[i];
            polygonPath[pointCount + i] = rightSidePoints[pointCount - 1 - i];
        }

        currentLinePolygonCollider.pathCount = 1;
        currentLinePolygonCollider.SetPath(0, polygonPath);
    }

    private void FinishCurrentLine()
    {
        if (!isCurrentlyDrawing)
        {
            return;
        }

        isCurrentlyDrawing = false;

        DrawingStopped?.Invoke();

        if (lineWorldPoints.Count < 2)
        {
            if (currentLineObject != null)
            {
                Destroy(currentLineObject);
            }

            ResetCurrentLineReferences();
            return;
        }

        EnablePhysicsForFinishedLine();

        finishedLineCount++;
        LineFinished?.Invoke();

        if (finishedLineCount == 1)
        {
            FirstLineFinished?.Invoke();
        }

        if (lockDrawingAfterLineLimit && maximumLinesPerRound > 0 && finishedLineCount >= maximumLinesPerRound)
        {
            drawingAllowed = false;
        }

        ResetCurrentLineReferences();
    }

    private void EnablePhysicsForFinishedLine()
    {
        if (currentLineRigidbody == null)
        {
            return;
        }

        currentLineRigidbody.bodyType = lineBodyTypeAfterRelease;
        currentLineRigidbody.gravityScale = lineGravityAfterRelease;
        currentLineRigidbody.constraints = freezeLineRotationAfterRelease
            ? RigidbodyConstraints2D.FreezeRotation
            : RigidbodyConstraints2D.None;
            
        currentLineRigidbody.useAutoMass = false;
        currentLineRigidbody.mass = Mathf.Max(0.1f, currentLineLength * massPerUnitLength);

        currentLineRigidbody.simulated = true;
        currentLineRigidbody.WakeUp();
    }

    private void ResetCurrentLineReferences()
    {
        currentLineObject = null;
        currentLineRenderer = null;
        currentLinePolygonCollider = null;
        currentLineRigidbody = null;

        lineWorldPoints.Clear();
        currentLineLength = 0f;
    }

    private bool IsInsideBlockingCollider(Vector2 worldPoint)
    {
        if (!stopLineAtColliders)
        {
            return false;
        }

        Collider2D[] overlappingColliders = Physics2D.OverlapPointAll(worldPoint);
        for (int i = 0; i < overlappingColliders.Length; i++)
        {
            if (IsBlockingCollider(overlappingColliders[i]))
            {
                return true;
            }
        }

        return false;
    }

    // THIS IS TOO MUCH IG
    private bool TryGetClampedPointAtBlockingCollider(Vector2 fromWorldPoint, Vector2 toWorldPoint, out Vector2 clampedPoint)
    {
        clampedPoint = toWorldPoint;

        if (!stopLineAtColliders)
        {
            return false;
        }

        Vector2 delta = toWorldPoint - fromWorldPoint;
        float deltaMagnitude = delta.magnitude;
        if (deltaMagnitude < 0.0001f)
        {
            return false;
        }

        RaycastHit2D[] hits = Physics2D.LinecastAll(fromWorldPoint, toWorldPoint);
        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        float nearestDistance = float.MaxValue;
        bool foundBlockingHit = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (!IsBlockingCollider(hitCollider))
            {
                continue;
            }

            float hitDistance = hits[i].distance;
            if (hitDistance < nearestDistance)
            {
                nearestDistance = hitDistance;
                foundBlockingHit = true;
            }
        }

        if (!foundBlockingHit)
        {
            return false;
        }

        Vector2 direction = delta / deltaMagnitude;
        float allowedDistance = Mathf.Max(0f, nearestDistance - blockerSurfaceOffset);
        clampedPoint = fromWorldPoint + direction * allowedDistance;
        return true;
    }

    private bool IsBlockingCollider(Collider2D collider)
    {
        if (collider == null)
        {
            return false;
        }

        bool isDrawnLineCollider = TryGetLineRendererOwner(collider, out LineRenderer ownerLineRenderer);
        if (isDrawnLineCollider)
        {
            if (lineCanCrossItself && currentLineRenderer != null && ownerLineRenderer == currentLineRenderer)
            {
                return false;
            }

            return stopLineAtOtherDrawnLines;
        }

        if (!IsLayerIncludedInMask(collider.gameObject.layer, drawingBlockerLayers))
        {
            return false;
        }

        if (!blockersIncludeTriggers && collider.isTrigger)
        {
            return false;
        }

        if (currentLinePolygonCollider != null && collider == currentLinePolygonCollider)
        {
            return false;
        }

        if (currentLineObject != null && collider.transform.IsChildOf(currentLineObject.transform))
        {
            return false;
        }

        return true;
    }

    private static bool IsLayerIncludedInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private static bool TryGetLineRendererOwner(Collider2D collider, out LineRenderer ownerLineRenderer)
    {
        ownerLineRenderer = collider.GetComponent<LineRenderer>();
        if (ownerLineRenderer != null)
        {
            return true;
        }

        ownerLineRenderer = collider.GetComponentInParent<LineRenderer>();
        return ownerLineRenderer != null;
    }

    private Material GetLineMaterial()
    {
        if (lineMaterial != null)
        {
            return lineMaterial;
        }

        if (runtimeLineMaterial == null)
        {
            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader != null)
            {
                runtimeLineMaterial = new Material(spriteShader);
            }
        }

        return runtimeLineMaterial;
    }

    private Vector2 ConvertScreenToWorld(Vector2 screenPoint)
    {
        if (cachedMainCamera == null)
        {
            cachedMainCamera = Camera.main;
            if (cachedMainCamera == null)
            {
                return Vector2.zero;
            }
        }

        Vector3 worldPoint = cachedMainCamera.ScreenToWorldPoint(
            new Vector3(screenPoint.x, screenPoint.y, -cachedMainCamera.transform.position.z));

        return new Vector2(worldPoint.x, worldPoint.y);
    }

    private bool IsPointerOnUI()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            int touchId = Touchscreen.current.primaryTouch.touchId.ReadValue();
            return EventSystem.current.IsPointerOverGameObject(touchId);
        }

        return EventSystem.current.IsPointerOverGameObject();
#else
        if (Input.touchCount > 0)
        {
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        }

        return EventSystem.current.IsPointerOverGameObject();
#endif
    }

    private static bool TryGetPointerDown(out Vector2 screenPoint)
    {
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            screenPoint = touch.position.ReadValue();
            return touch.press.wasPressedThisFrame;
        }

        if (Mouse.current != null)
        {
            screenPoint = Mouse.current.position.ReadValue();
            return Mouse.current.leftButton.wasPressedThisFrame;
        }

        screenPoint = Vector2.zero;
        return false;
#else
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            screenPoint = touch.position;
            return touch.phase == TouchPhase.Began;
        }

        screenPoint = Input.mousePosition;
        return Input.GetMouseButtonDown(0);
#endif
    }

    private static bool TryGetPointerHeld(out Vector2 screenPoint)
    {
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            screenPoint = touch.position.ReadValue();
            return touch.press.isPressed;
        }

        if (Mouse.current != null)
        {
            screenPoint = Mouse.current.position.ReadValue();
            return Mouse.current.leftButton.isPressed;
        }

        screenPoint = Vector2.zero;
        return false;
#else
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            screenPoint = touch.position;
            return touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary;
        }

        screenPoint = Input.mousePosition;
        return Input.GetMouseButton(0);
#endif
    }

    private static bool TryGetPointerUp(out Vector2 screenPoint)
    {
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            screenPoint = touch.position.ReadValue();
            return touch.press.wasReleasedThisFrame;
        }

        if (Mouse.current != null)
        {
            screenPoint = Mouse.current.position.ReadValue();
            return Mouse.current.leftButton.wasReleasedThisFrame;
        }

        screenPoint = Vector2.zero;
        return false;
#else
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            screenPoint = touch.position;
            return touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
        }

        screenPoint = Input.mousePosition;
        return Input.GetMouseButtonUp(0);
#endif
    }

    private void OnDestroy()
    {
        if (runtimeLineMaterial != null)
        {
            Destroy(runtimeLineMaterial);
        }
    }
}

// BACKPAIN IS KILLING ME!!