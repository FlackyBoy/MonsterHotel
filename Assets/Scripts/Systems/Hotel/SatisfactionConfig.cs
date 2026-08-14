using UnityEngine;

/// <summary>
/// Configuration satisfaction — valeurs par défaut pour tous les monstres.
/// Asset : Resources/Config/SatisfactionConfig.asset (auto-chargé, voir Instance).
/// </summary>
[CreateAssetMenu(fileName = "SatisfactionConfig", menuName = "Hotel/Config/Satisfaction")]
public class SatisfactionConfig : ScriptableObject
{
    static SatisfactionConfig _instance;

    public static SatisfactionConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<SatisfactionConfig>("Config/SatisfactionConfig");
            return _instance;
        }
    }

    [Header("Satisfaction (par défaut pour tous les monstres)")]
    [Tooltip("Satisfaction initiale de chaque monstre à l'arrivée [0-100]")]
    [Range(0f, 100f)]
    public float initialSatisfaction = 80f;

    [Tooltip("En dessous de ce seuil le monstre quitte l'hôtel (0 = jamais)")]
    [Range(0f, 100f)]
    public float leaveThreshold = 10f;

    [Header("Décorations — Confort")]
    [Tooltip("Bonus de satisfaction initiale par point de Confort total de l'hôtel")]
    public float comfortToSatisfactionBonus = 0.5f;
}
