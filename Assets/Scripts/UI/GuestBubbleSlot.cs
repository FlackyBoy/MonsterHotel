using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using MoreMountains.Tools;

/// <summary>
/// Composant sur le prefab GuestBubbleSlot (et RestaurantQueueHUD, qui réutilise le même prefab).
/// Expose les références UI pour que GuestQueueHUD/RestaurantQueueHUD puissent les piloter.
///
/// Deux préoccupations volontairement séparées, pour ne pas se marcher dessus :
///   - Le REMPLISSAGE doit suivre le temps réel en continu (la donnée source est déjà continue,
///     pas besoin d'interpolation) → SetBar01() à chaque appel, qui fixe la valeur instantanément.
///   - La PULSATION (attire l'œil en urgence) est un punch d'échelle géré ici directement (pas
///     MMProgressBar.Bump() : sa détection "la valeur diminue" se base sur un état interne
///     [_newPercent] que seul UpdateBar01() met à jour — jamais appelée ici puisqu'on utilise
///     SetBar01() pour un remplissage fluide, donc Bump() ne se déclenchait jamais). Amplitude ET
///     fréquence augmentent progressivement à partir de pulseStartRatio jusqu'à ratio = 0.
/// </summary>
public class GuestBubbleSlot : MonoBehaviour
{
    [Header("Pulsation d'urgence")]
    [Tooltip("En dessous de ce ratio, la jauge se met à pulser — au-dessus, aucune pulsation")]
    [Range(0f, 1f)] public float pulseStartRatio = 0.5f;
    [Tooltip("Intervalle entre deux pulsations juste sous le seuil de départ (encore calme)")]
    public float pulseIntervalAtStart = 1.5f;
    [Tooltip("Intervalle entre deux pulsations quand le ratio approche 0 (urgent)")]
    public float pulseIntervalAtEnd = 0.2f;
    [Tooltip("Amplitude du punch (multiplicateur d'échelle) juste sous le seuil de départ — léger")]
    public float pulseScaleAtStart = 1.05f;
    [Tooltip("Amplitude du punch quand le ratio approche 0 — marqué")]
    public float pulseScaleAtEnd = 1.4f;
    [Tooltip("Durée d'un aller-retour de pulsation")]
    public float pulseDuration = 0.25f;

    public MMProgressBar            progressBar;
    public Image                    icon;
    public TMPro.TextMeshProUGUI    nameLabel;

    float     _currentRatio = 1f;
    float     _pulseTimer;
    Vector3   _progressBarBaseScale = Vector3.one;
    Coroutine _pulseCoroutine;

    public void SetGuest(string monsterName, Sprite monsterIcon)
    {
        if (nameLabel != null) nameLabel.text = monsterName;
        if (icon      != null) icon.sprite    = monsterIcon;
        if (icon      != null) icon.enabled   = monsterIcon != null;
        _currentRatio = 1f;
        _pulseTimer   = 0f;
        if (progressBar != null) _progressBarBaseScale = progressBar.transform.localScale;
    }

    public void SetRatio(float ratio)
    {
        _currentRatio = ratio;
        progressBar?.SetBar01(ratio);
    }

    void Update()
    {
        if (progressBar == null || _currentRatio >= pulseStartRatio)
        {
            _pulseTimer = 0f;
            return;
        }

        _pulseTimer -= Time.deltaTime;
        if (_pulseTimer <= 0f)
        {
            float urgency = 1f - Mathf.InverseLerp(0f, pulseStartRatio, _currentRatio); // 0 au seuil, 1 à ratio=0
            TriggerPulse(urgency);
            _pulseTimer = Mathf.Lerp(pulseIntervalAtStart, pulseIntervalAtEnd, urgency);
        }
    }

    void TriggerPulse(float urgency)
    {
        float targetScale = Mathf.Lerp(pulseScaleAtStart, pulseScaleAtEnd, urgency);
        if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
        _pulseCoroutine = StartCoroutine(PulseCo(targetScale));
    }

    IEnumerator PulseCo(float targetScale)
    {
        var t    = progressBar.transform;
        float half = pulseDuration * 0.5f;

        for (float elapsed = 0f; elapsed < half; elapsed += Time.deltaTime)
        {
            t.localScale = Vector3.LerpUnclamped(_progressBarBaseScale, _progressBarBaseScale * targetScale, elapsed / half);
            yield return null;
        }
        for (float elapsed = 0f; elapsed < half; elapsed += Time.deltaTime)
        {
            t.localScale = Vector3.LerpUnclamped(_progressBarBaseScale * targetScale, _progressBarBaseScale, elapsed / half);
            yield return null;
        }
        t.localScale = _progressBarBaseScale;
    }
}
