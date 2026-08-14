using UnityEngine;

/// <summary>
/// Configuration joueur — vitesses de déplacement.
/// Asset : Resources/Config/PlayerConfig.asset (auto-chargé, voir Instance).
/// </summary>
[CreateAssetMenu(fileName = "PlayerConfig", menuName = "Hotel/Config/Player")]
public class PlayerConfig : ScriptableObject
{
    static PlayerConfig _instance;

    public static PlayerConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<PlayerConfig>("Config/PlayerConfig");
            return _instance;
        }
    }

    [Header("Joueur — déplacement")]
    [Tooltip("Vitesse de marche du joueur (unités/seconde)")]
    public float playerMoveSpeed = 5f;
    [Tooltip("Vitesse de sprint du joueur (unités/seconde)")]
    public float playerSprintSpeed = 9f;
}
