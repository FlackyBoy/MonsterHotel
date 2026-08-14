using UnityEngine;

/// <summary>
/// Catalogues de données — raccourcis Inspector vers tous les assets de monstres, chambres,
/// besoins et recettes du jeu.
/// Asset : Resources/Config/HotelCatalog.asset (auto-chargé, voir Instance).
/// </summary>
[CreateAssetMenu(fileName = "HotelCatalog", menuName = "Hotel/Config/Catalog")]
public class HotelCatalog : ScriptableObject
{
    static HotelCatalog _instance;

    public static HotelCatalog Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<HotelCatalog>("Config/HotelCatalog");
            return _instance;
        }
    }

    [Header("── Monstres (modifier ici ou cliquer sur l'asset)")]
    [Tooltip("Tous les types de monstres du jeu. Modifier revenuePerNight, stayDuration, etc. directement sur chaque asset.")]
    public MonsterData[] monsters;

    [Header("── Chambres (modifier ici ou cliquer sur l'asset)")]
    [Tooltip("Tous les types de chambres du jeu. Modifier baseRevenue, quality, serviceTime, etc.")]
    public RoomData[] rooms;

    [Header("── Besoins (modifier ici ou cliquer sur l'asset)")]
    [Tooltip("Tous les types de besoins. Modifier decayRate, seuils, bonus satisfaction, etc.")]
    public NeedType[] needTypes;

    [Header("── Recettes (modifier ici ou cliquer sur l'asset)")]
    [Tooltip("Toutes les recettes de cuisine. Modifier workDuration, ingrédients, etc.")]
    public RecipeData[] recipes;
}
