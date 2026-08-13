using UnityEngine;

/// <summary>
/// Données d'un meuble achetable et plaçable dans une chambre.
/// Crée via clic-droit → Create → MonsterHotel → Furniture Data.
/// </summary>
[CreateAssetMenu(fileName = "FurnitureData", menuName = "MonsterHotel/Furniture Data")]
public class FurnitureData : ScriptableObject
{
    public string furnitureName = "Meuble";

    [Tooltip("Required = obligatoire pour activer la chambre / Decorative = améliore les bonus")]
    public FurnitureType furnitureType = FurnitureType.Required;

    public int cost = 50;

    [Tooltip("Prefab instancié quand le meuble est posé")]
    public GameObject prefab;

    [Tooltip("Décalage local appliqué lors de la pose (utile si le pivot du prefab n'est pas centré)")]
    public Vector3 placementOffset;

    [Tooltip("Bonus de revenu par nuit (s'applique quand la chambre est occupée)")]
    public int revenueBonus = 0;

    [Tooltip("Bonus d'attractivité (influence la satisfaction des monstres)")]
    public int attractivenessBonus = 0;

    [Header("Placement spécial")]
    [Tooltip("Si coché : ce meuble doit être posé sur un ChairSlot (emplacement de table)")]
    public bool requiresChairSlot;
    [Tooltip("Si coché : reste en mode placement après chaque pose (pour en placer plusieurs d'affilée)")]
    public bool multiPlace;
    [Tooltip("Si ce meuble est une table (avec des ChairSlot enfants dans son prefab) : chaise à proposer automatiquement juste après l'avoir posée. Laisser vide si ce meuble n'est pas une table.")]
    public FurnitureData matchingChair;

    [Header("Upgrade")]
    [Tooltip("Coût pour améliorer CE meuble vers le niveau suivant")]
    public int upgradeCost = 0;

    [Tooltip("Données du niveau suivant (null = niveau max)")]
    public FurnitureData nextUpgrade;
}

public enum FurnitureType
{
    Required,   // Obligatoire — la chambre n'est pas assignable sans lui
    Decorative, // Optionnel — améliore revenu et attractivité
}
