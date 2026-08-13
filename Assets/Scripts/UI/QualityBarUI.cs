using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Jauge oscillante pour la qualité de préparation d'un plat.
/// Place ce composant sur un GameObject enfant du Canvas (WorldSpace ou ScreenSpace).
/// Nécessite un Slider UI enfant pour l'affichage.
/// </summary>
public class QualityBarUI : MonoBehaviour
{
    [Header("Références")]
    public Slider slider;
    [Tooltip("Le Canvas enfant à afficher/cacher (glisse le Canvas ici)")]
    public GameObject canvasRoot;

    [Header("Oscillation")]
    [Tooltip("Vitesse d'oscillation (aller-retour par seconde)")]
    public float speed = 1.2f;

    [Header("Zone de bonne qualité")]
    [Tooltip("Valeur minimale pour obtenir un bonus de qualité")]
    [Range(0f, 1f)] public float goodZoneMin = 0.6f;
    [Range(0f, 1f)] public float goodZoneMax = 1f;

    [Header("Feedback qualité")]
    [Tooltip("Texte TMP affiché brièvement après la capture (optionnel)")]
    public TMP_Text feedbackText;
    public float feedbackDuration = 2f;
    [Range(0f, 1f)] public float goodThreshold   = 0.65f;
    [Range(0f, 1f)] public float normalThreshold = 0.35f;
    public Color colorGood   = Color.green;
    public Color colorNormal = Color.yellow;
    public Color colorBad    = Color.red;

    bool  _active;
    float _value;

    public bool  IsActive     => _active;
    public float Value        => _value;
    public bool  IsInGoodZone => _value >= goodZoneMin && _value <= goodZoneMax;

    void Awake()
    {
        canvasRoot?.SetActive(false);
    }

    public void Show()
    {
        _value  = 0f;
        _active = true;
        Debug.Log($"[QualityBar] Show() — canvasRoot={(canvasRoot != null ? canvasRoot.name : "NULL")} slider={(slider != null ? slider.name : "NULL")}");
        if (canvasRoot != null)
            canvasRoot.SetActive(true);
        else
            Debug.LogWarning("[QualityBar] canvasRoot non assigné ! Glisse le Canvas enfant dans ce champ.");
    }

    public void Hide()
    {
        _active = false;
        canvasRoot?.SetActive(false);
    }

    /// <summary>
    /// Stoppe l'oscillation et retourne la qualité capturée [0-1].
    /// </summary>
    public float Capture()
    {
        _active = false;
        return _value;
    }

    /// <summary>
    /// Affiche brièvement un feedback textuel coloré selon la qualité capturée.
    /// </summary>
    public void ShowFeedback(float quality)
    {
        if (feedbackText == null) return;
        StopCoroutine(nameof(FeedbackFade));

        if (quality >= goodThreshold)
        {
            feedbackText.text  = "Excellent !";
            feedbackText.color = colorGood;
        }
        else if (quality >= normalThreshold)
        {
            feedbackText.text  = "Correct";
            feedbackText.color = colorNormal;
        }
        else
        {
            feedbackText.text  = "Médiocre...";
            feedbackText.color = colorBad;
        }

        feedbackText.gameObject.SetActive(true);
        StartCoroutine(FeedbackFade());
    }

    IEnumerator FeedbackFade()
    {
        // Cache le slider mais garde le canvas visible pour le texte
        if (slider != null) slider.gameObject.SetActive(false);
        yield return new WaitForSeconds(feedbackDuration);
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
        if (slider != null) slider.gameObject.SetActive(true);
        canvasRoot?.SetActive(false);
    }

    void Update()
    {
        if (!_active) return;

        _value = Mathf.PingPong(Time.time * speed, 1f);

        if (slider != null)
            slider.value = _value;
    }
}
