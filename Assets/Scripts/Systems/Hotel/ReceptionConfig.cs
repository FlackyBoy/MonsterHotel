using UnityEngine;

/// <summary>
/// Configuration réception — attente au comptoir.
/// Asset : Resources/Config/ReceptionConfig.asset (auto-chargé, voir Instance).
/// </summary>
[CreateAssetMenu(fileName = "ReceptionConfig", menuName = "Hotel/Config/Reception")]
public class ReceptionConfig : ScriptableObject
{
    static ReceptionConfig _instance;

    public static ReceptionConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<ReceptionConfig>("Config/ReceptionConfig");
            return _instance;
        }
    }

    [Header("Réception — attente au comptoir")]
    [Tooltip("Attente (secondes) considérée comme bonne → bonus satisfaction")]
    public float receptionGoodWaitTime = 20f;
    [Tooltip("Bonus satisfaction si pris en charge dans les temps")]
    public float receptionWaitBonus = 15f;
    [Tooltip("Malus satisfaction si l'attente est trop longue")]
    public float receptionWaitPenalty = 10f;
}
