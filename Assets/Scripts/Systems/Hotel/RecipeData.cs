using UnityEngine;

/// <summary>
/// Définit une recette : ingrédient + résultat + durée de préparation.
/// Créer via : Assets → Create → MonsterHotel → Recipe
/// </summary>
[CreateAssetMenu(fileName = "Recipe_New", menuName = "MonsterHotel/Recipe")]
public class RecipeData : ScriptableObject
{
    public string recipeName = "Recette";

    [Tooltip("Item que le joueur dépose sur le plan de travail")]
    public HoldableItemData ingredient;

    [Tooltip("Item produit après la préparation")]
    public HoldableItemData result;

    [Tooltip("Durée de préparation en secondes")]
    public float workDuration = 5f;
}
