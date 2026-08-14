using UnityEngine;

/// <summary>
/// Configuration cuisine — attente repas, qualité employé et jauge d'attente visuelle.
/// Asset : Resources/Config/KitchenConfig.asset (auto-chargé, voir Instance).
/// </summary>
[CreateAssetMenu(fileName = "KitchenConfig", menuName = "Hotel/Config/Kitchen")]
public class KitchenConfig : ScriptableObject
{
    static KitchenConfig _instance;

    public static KitchenConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<KitchenConfig>("Config/KitchenConfig");
            return _instance;
        }
    }

    [Header("Cuisine — attente repas")]
    [Tooltip("Satisfaction perdue par seconde pendant que le monstre attend son repas à table")]
    public float waitFoodSatisfactionDecay = 0.5f;
    [Tooltip("Durée maximale de la jauge d'attente à table (secondes)")]
    public float waitGaugeMaxTime = 40f;
    [Tooltip("Distance max pour livrer un plat à table")]
    public float deliveryRange = 2f;

    [Header("Cuisine — qualité employé (note 1 → min, note 20 → max)")]
    [Tooltip("Bonus de satisfaction livré par un cuisinier note 1")]
    public float cookDeliveryBonusMin = 5f;
    [Tooltip("Bonus de satisfaction livré par un cuisinier note 20")]
    public float cookDeliveryBonusMax = 25f;

    [Header("Jauge d'attente — visuel")]
    public float waitGaugeWidth    = 14f;
    public float waitGaugeHeight   = 80f;
    public float waitGaugeOffsetY  = 3.2f;
    public Color waitGaugeColor    = Color.white;
}
