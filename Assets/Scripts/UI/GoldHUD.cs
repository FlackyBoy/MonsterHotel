using TMPro;
using UnityEngine;

/// <summary>
/// Affiche le solde du joueur en temps réel.
/// Attache-le sur un TextMeshProUGUI dans le Canvas HUD.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class GoldHUD : MonoBehaviour
{
    TextMeshProUGUI _label;

    void Awake() => _label = GetComponent<TextMeshProUGUI>();

    void OnEnable()
    {
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.OnGoldChanged += Refresh;
            Refresh(EconomyManager.Instance.Gold);
        }
    }

    void OnDisable()
    {
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnGoldChanged -= Refresh;
    }

    void Start()
    {
        if (EconomyManager.Instance != null)
            Refresh(EconomyManager.Instance.Gold);
    }

    void Refresh(int gold) => _label.text = $"{gold} G";
}
