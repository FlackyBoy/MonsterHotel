using UnityEngine;

public enum HoldableItemType { Mop, Wrench, RawIngredient, CookedDish }

/// <summary>
/// ScriptableObject définissant un item que le joueur peut tenir.
///
/// Créer via : Assets → Create → MonsterHotel → Holdable Item
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "MonsterHotel/Holdable Item")]
public class HoldableItemData : ScriptableObject
{
    public string           itemName = "Item";
    public HoldableItemType itemType;

    [Tooltip("Prefab visuel affiché quand le joueur tient l'objet")]
    public GameObject heldPrefab;

    [Header("Aliment (CookedDish uniquement)")]
    [Tooltip("Besoin que ce plat satisfait")]
    public NeedType needFulfilled;

    [Tooltip("Quantité de besoin remplie [0-1]")]
    [Range(0f, 1f)]
    public float fillAmount = 0.8f;

    [Tooltip("Monstres qui peuvent consommer ce plat (vide = tous)")]
    public MonsterData[] compatibleMonsters;

    [Tooltip("Durée du repas à table (secondes) — 0 utilise la valeur par défaut de l'EatingSpot")]
    public float eatDuration = 5f;
}
