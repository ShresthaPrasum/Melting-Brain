using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class GoalZone : MonoBehaviour
{
    [Header("Goal Settings")]
    [SerializeField] private string targetTagName = "TargetObject";
    [SerializeField] private float requiredHoldTime = 3f;

    private Coroutine holdRoutine;
    private bool goalAchieved;

    public event Action OnGoalAchieved;
    public event Action<float> OnHoldProgress; // Sends 0.0 to 1.0 progress

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (goalAchieved) return;

        if (other.CompareTag(targetTagName))
        {
            if (holdRoutine == null)
            {
                holdRoutine = StartCoroutine(HoldTimerRoutine());
            }
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (goalAchieved) return;

        // Failsafe in case trigger enter was missed
        if (other.CompareTag(targetTagName) && holdRoutine == null)
        {
            holdRoutine = StartCoroutine(HoldTimerRoutine());
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (goalAchieved) return;

        if (other.CompareTag(targetTagName))
        {
            if (holdRoutine != null)
            {
                StopCoroutine(holdRoutine);
                holdRoutine = null;
                OnHoldProgress?.Invoke(0f); // Reset progress
            }
        }
    }

    private IEnumerator HoldTimerRoutine()
    {
        float timer = 0f;

        while (timer < requiredHoldTime)
        {
            timer += Time.deltaTime;
            OnHoldProgress?.Invoke(timer / requiredHoldTime);
            yield return null;
        }

        goalAchieved = true;
        OnGoalAchieved?.Invoke();
    }

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }
}
