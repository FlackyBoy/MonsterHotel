using UnityEngine;

/// <summary>
/// Configuration cycle jour/nuit et durée de séjour.
/// Asset : Resources/Config/DayNightConfig.asset (auto-chargé, voir Instance).
/// </summary>
[CreateAssetMenu(fileName = "DayNightConfig", menuName = "Hotel/Config/DayNight")]
public class DayNightConfig : ScriptableObject
{
    static DayNightConfig _instance;

    public static DayNightConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<DayNightConfig>("Config/DayNightConfig");
            return _instance;
        }
    }

    [Header("Cycle jour/nuit")]
    [Tooltip("Durée d'une journée complète en secondes réels")]
    public float dayDuration = 300f;
    [Tooltip("Heure de départ (0-24)")]
    public float startHour = 8f;
}
