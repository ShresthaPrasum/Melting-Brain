using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public class BallDestroyAndRestart : MonoBehaviour
{
    [Header("Ball Detection")]
    [SerializeField] private Rigidbody2D specificBallRigidbody;
    [SerializeField] private bool matchByTagWhenSpecificBallMissing = true;
    [SerializeField] private string ballTag = "TargetObject";

    [Header("Restart")]
    [SerializeField] private bool destroyBallOnTouch = true;
    [SerializeField] private float restartDelaySeconds = 0.5f;
    [SerializeField] private bool useUnscaledTimeForDelay = false;

    private bool hasTriggered;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered)
        {
            return;
        }

        if (!IsBallCollider(other))
        {
            return;
        }

        hasTriggered = true;

        GameObject ballObject = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : other.gameObject;

        if (destroyBallOnTouch)
        {
            Destroy(ballObject);
        }

        StartCoroutine(RestartLevelRoutine());
    }

    private bool IsBallCollider(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        if (specificBallRigidbody != null)
        {
            return other.attachedRigidbody == specificBallRigidbody || other.gameObject == specificBallRigidbody.gameObject;
        }

        if (!matchByTagWhenSpecificBallMissing || string.IsNullOrWhiteSpace(ballTag))
        {
            return false;
        }

        if (other.CompareTag(ballTag))
        {
            return true;
        }

        return other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(ballTag);
    }

    private IEnumerator RestartLevelRoutine()
    {
        float waitTime = Mathf.Max(0f, restartDelaySeconds);
        if (waitTime > 0f)
        {
            if (useUnscaledTimeForDelay)
            {
                yield return new WaitForSecondsRealtime(waitTime);
            }
            else
            {
                yield return new WaitForSeconds(waitTime);
            }
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
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
