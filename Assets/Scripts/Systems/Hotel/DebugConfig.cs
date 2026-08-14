using UnityEngine;

/// <summary>
/// Configuration debug — bascules de test à désactiver en production.
/// Asset : Resources/Config/DebugConfig.asset (auto-chargé, voir Instance).
/// </summary>
[CreateAssetMenu(fileName = "DebugConfig", menuName = "Hotel/Config/Debug")]
public class DebugConfig : ScriptableObject
{
    static DebugConfig _instance;

    public static DebugConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<DebugConfig>("Config/DebugConfig");
            return _instance;
        }
    }

    [Header("Debug")]
    [Tooltip("Force tous les meubles sales + débris max au départ d'un monstre, " +
             "quelle que soit la chance définie dans RoomData. Désactiver en production.")]
    public bool debugForceFullDirtyOnVacate = false;
}
