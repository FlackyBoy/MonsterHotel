using UnityEngine;

/// <summary>
/// Configuration blocs destructibles — distance d'interaction, hauteur visuelle, hystérèse de
/// ciblage, et catalogue des types de blocs.
/// Asset : Resources/Config/BlockConfig.asset (auto-chargé, voir Instance).
/// </summary>
[CreateAssetMenu(fileName = "BlockConfig", menuName = "Hotel/Config/Block")]
public class BlockConfig : ScriptableObject
{
    static BlockConfig _instance;

    public static BlockConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<BlockConfig>("Config/BlockConfig");
            return _instance;
        }
    }

    [Header("Blocs destructibles")]
    [Tooltip("Distance d'interaction avec un bloc")]
    public float blockInteractRange = 3f;
    [Tooltip("Hauteur visuelle des blocs (unités monde)")]
    public float blockHeight        = 3f;
    [Tooltip("Hysterèse de ciblage : le bloc déjà ciblé conserve un avantage. " +
             "0.75 = 25% d'avantage (stable). Vers 1.0 = aucune hysterèse (plus réactif).")]
    [Range(0.5f, 1f)]
    public float blockTargetHysteresis = 0.75f;

    [Header("── Blocs (modifier ici ou cliquer sur l'asset)")]
    [Tooltip("Tous les types de blocs destructibles (Terre, Pierre...).")]
    public BlockData[] blocks;
}
