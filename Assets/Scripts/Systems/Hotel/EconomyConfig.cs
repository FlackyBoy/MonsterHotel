using UnityEngine;

/// <summary>
/// Configuration économie — or de départ et pourboires au départ des monstres.
/// Asset : Resources/Config/EconomyConfig.asset (auto-chargé, voir Instance).
/// </summary>
[CreateAssetMenu(fileName = "EconomyConfig", menuName = "Hotel/Config/Economy")]
public class EconomyConfig : ScriptableObject
{
    static EconomyConfig _instance;

    public static EconomyConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<EconomyConfig>("Config/EconomyConfig");
            return _instance;
        }
    }

    [Header("Économie")]
    [Tooltip("Or de départ du joueur")]
    public int startingGold = 350;

    [Header("Pourboires au départ")]
    [Tooltip("Satisfaction >= seuil → bon pourboire")]
    [Range(0f, 100f)]
    public float tipGoodThreshold = 70f;
    [Tooltip("Satisfaction >= seuil → pourboire normal")]
    [Range(0f, 100f)]
    public float tipNormalThreshold = 40f;
    [Tooltip("Bon pourboire = revenuePerNight × ce multiplicateur")]
    public float tipGoodMultiplier = 0.3f;
    [Tooltip("Pourboire normal = revenuePerNight × ce multiplicateur")]
    public float tipNormalMultiplier = 0.1f;
}
