using TMPro;
using UnityEngine;

/// <summary>
/// Affiche l'heure in-game en haut de l'écran.
/// Attache-le sur un TextMeshProUGUI dans le Canvas HUD.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class TimeHUD : MonoBehaviour
{
    TextMeshProUGUI _label;

    void Awake() => _label = GetComponent<TextMeshProUGUI>();

    void Update()
    {
        if (TimeManager.Instance != null)
            _label.text = TimeManager.Instance.TimeString;
    }
}
