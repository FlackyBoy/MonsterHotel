using UnityEngine;

/// <summary>
/// Pilote la lumière directionnelle et la lumière ambiante selon l'heure du TimeManager.
/// Attache-le sur un GameObject dans _Managers et assigne la Directional Light de la scène.
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("La Directional Light principale de la scène (soleil/lune)")]
    public Light directionalLight;

    [Header("Couleur lumière selon l'heure")]
    [Tooltip("Gradient 24h : position 0 = minuit, position 1 = minuit+1. Couleur de la lumière directionnelle.")]
    public Gradient lightColorGradient;

    [Header("Intensité lumière selon l'heure")]
    [Tooltip("Courbe 24h (axe X = heure normalisée 0-1, axe Y = intensité)")]
    public AnimationCurve lightIntensityCurve = DefaultIntensityCurve();

    [Header("Lumière ambiante")]
    [Tooltip("Gradient 24h pour la lumière ambiante (même logique que lightColorGradient)")]
    public Gradient ambientColorGradient;

    [Header("Rotation du soleil")]
    [Tooltip("Angle de départ de la rotation (correspond à minuit)")]
    public float sunriseAngle  = -90f;
    [Tooltip("Rotation sur l'axe X — 180° = un tour complet sur la journée")]
    public float sunTotalAngle = 180f;

    // ─── Lifecycle ────────────────────────────────────────────────

    void Reset()
    {
        lightIntensityCurve = DefaultIntensityCurve();
    }

    void Update()
    {
        if (TimeManager.Instance == null) return;

        float hour       = TimeManager.Instance.Hour;
        float normalized = hour / 24f; // 0 → 1

        ApplyLight(normalized);
    }

    // ─── Privé ────────────────────────────────────────────────────

    void ApplyLight(float t)
    {
        if (directionalLight != null)
        {
            // Couleur
            if (lightColorGradient != null)
                directionalLight.color = lightColorGradient.Evaluate(t);

            // Intensité
            directionalLight.intensity = lightIntensityCurve.Evaluate(t);

            // Rotation (X = altitude du soleil)
            float xAngle = sunriseAngle + t * sunTotalAngle * 2f; // *2 pour 360°/jour
            directionalLight.transform.rotation = Quaternion.Euler(xAngle, -30f, 0f);
        }

        // Lumière ambiante
        if (ambientColorGradient != null)
            RenderSettings.ambientLight = ambientColorGradient.Evaluate(t);
    }

    // ─── Helpers ──────────────────────────────────────────────────

    static AnimationCurve DefaultIntensityCurve()
    {
        // Nuit faible → aube montante → plein jour → crépuscule → nuit
        return new AnimationCurve(
            new Keyframe(0f,    0.05f),  // minuit
            new Keyframe(0.25f, 0.1f),   // 6h — aube
            new Keyframe(0.35f, 1.0f),   // 8h30 — journée
            new Keyframe(0.65f, 1.0f),   // 15h30 — journée
            new Keyframe(0.80f, 0.15f),  // 19h — crépuscule
            new Keyframe(1f,    0.05f)   // minuit
        );
    }
}
