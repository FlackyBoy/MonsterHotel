using UnityEngine;

/// <summary>
/// Configuration placement — curseurs de placement meubles et chambres.
/// Asset : Resources/Config/PlacementConfig.asset (auto-chargé, voir Instance).
/// </summary>
[CreateAssetMenu(fileName = "PlacementConfig", menuName = "Hotel/Config/Placement")]
public class PlacementConfig : ScriptableObject
{
    static PlacementConfig _instance;

    public static PlacementConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<PlacementConfig>("Config/PlacementConfig");
            return _instance;
        }
    }

    [Header("Placement meubles")]
    [Tooltip("Distance de snap de la chaise sur un ChairSlot")]
    public float chairSnapRange = 1.5f;
    [Tooltip("Vitesse de déplacement du curseur de placement de meuble (unités/seconde)")]
    public float furnitureCursorSpeed = 4f;
    [Tooltip("Marge murs pour les meubles (fraction de la taille de la pièce, ex: 0.1 = 10%)")]
    public float furnitureWallMargin = 0.1f;

    [Header("Placement chambres")]
    [Tooltip("Vitesse de déplacement du curseur de placement de chambre (unités/seconde)")]
    public float roomCursorSpeed = 6f;
    [Tooltip("Distance devant le joueur où le ghost de chambre apparaît")]
    public float roomPlacementOffset = 2f;
}
