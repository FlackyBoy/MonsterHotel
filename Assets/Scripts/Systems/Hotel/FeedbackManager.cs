using MoreMountains.Feedbacks;
using System.Collections;
using UnityEngine;

/// <summary>
/// Singleton centralisant les feedbacks Feel du jeu.
/// Placer dans _Managers en scène. Les prefabs y accèdent via FeedbackManager.Instance.
/// </summary>
public class FeedbackManager : MonoBehaviour
{
    public static FeedbackManager Instance { get; private set; }

    [Header("Blocs")]
    public MMF_Player blockDestruction;
    public MMF_Player blockDigging;

    [Header("Placement")]
    public MMF_Player roomPlaced;
    public MMF_Player furniturePlaced;

    [Header("Camera Shake")]
    [Tooltip("Amplitude du shake (unités monde)")]
    public float shakeAmplitude = 0.15f;
    [Tooltip("Durée du shake en secondes")]
    public float shakeDuration  = 0.3f;
    [Tooltip("Fréquence du shake (oscillations/sec)")]
    public float shakeFrequency = 20f;

    public void ShakeCamera()
    {
        var ssm = SplitScreenManager.Instance;
        if (ssm == null) return;

        if (ssm.cam0 != null)
            ssm.cam0.GetComponent<PlayerCamera>()?.Shake(shakeAmplitude, shakeDuration, shakeFrequency);
        if (ssm.cam1 != null && ssm.cam1.gameObject.activeSelf)
            ssm.cam1.GetComponent<PlayerCamera>()?.Shake(shakeAmplitude, shakeDuration, shakeFrequency);
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void ResetBlockAfterDelay(ExpansionBlock block, float delay)
    {
        StartCoroutine(ResetBlockCoroutine(block, delay));
    }

    System.Collections.IEnumerator ResetBlockCoroutine(ExpansionBlock block, float delay)
    {
        yield return new WaitForSeconds(delay);
        block.DebugReset();
    }
}
