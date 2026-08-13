using UnityEngine;

/// <summary>
/// Données d'une chambre achetable au comptoir.
/// Crée via clic-droit → Create → MonsterHotel → Room Data.
/// </summary>
[CreateAssetMenu(fileName = "RoomData", menuName = "MonsterHotel/Room Data")]
public class RoomData : ScriptableObject
{
    public string roomName = "Chambre";
    public int cost = 100;

    [Tooltip("Catégorie de la chambre — détermine la compatibilité avec les monstres")]
    public RoomTypeData roomType;

    [Header("Stats")]
    [Tooltip("Revenu de base par nuit (hors bonus meubles)")]
    public int baseRevenue = 20;

    [Tooltip("Attractivité de base — influence la probabilité qu'un monstre choisisse cette chambre")]
    public int attractiveness = 1;

    [Tooltip("Qualité de base (1–5 étoiles)")]
    [Range(1, 5)]
    public int quality = 1;

    [Header("Upgrade chambre")]
    [Tooltip("Coût pour améliorer cette chambre vers le niveau suivant")]
    public int upgradeCost = 0;

    [Tooltip("RoomData du niveau suivant (null = niveau max)")]
    public RoomData nextUpgrade;

    [Tooltip("Décalage appliqué à la recherche de cellule (relatif au forward du joueur). Z=1 pousse d'une unité devant le joueur.")]
    public Vector3 placementOffset;

    [Tooltip("Empreinte en cellules (X = largeur, Z monde = profondeur)")]
    public Vector2Int size = new Vector2Int(2, 2);

    [Tooltip("Hauteur monde de la chambre (axe Y). Indépendant de la grille.")]
    public float height = 1f;

    [Tooltip("Prefab instancié lors de la pose")]
    public GameObject prefab;

    [Header("Mobilier")]
    [Tooltip("Meubles obligatoires — la chambre n'est pas assignable tant qu'ils ne sont pas tous posés")]
    public FurnitureData[] requiredFurniture = System.Array.Empty<FurnitureData>();

    [Tooltip("Meubles optionnels — améliorent le revenu et l'attractivité")]
    public FurnitureData[] optionalFurniture = System.Array.Empty<FurnitureData>();

    [Header("Facilité (salle commune)")]
    [Tooltip("Si coché, cette salle est une facilité publique — les monstres viennent satisfaire un besoin")]
    public bool isFacility = false;

    [Tooltip("Si coché, cette pièce est la salle de repos des employés — ils s'y rendent pendant leurs pauses")]
    public bool isBreakRoom = false;

    [Tooltip("Besoin que cette salle satisfait")]
    public NeedType needFulfilled;

    [Tooltip("Quantité de besoin remplie après un service complet [0-1]")]
    [Range(0f, 1f)]
    public float fillAmount = 0.8f;

    [Tooltip("Durée du service en secondes")]
    public float serviceTime = 10f;

    [Tooltip("Nombre de monstres pouvant être servis simultanément")]
    [Range(1, 8)]
    public int capacity = 2;

    [Tooltip("Si coché, les monstres attendent mais c'est le joueur qui déclenche le service (ex: cuisine)")]
    public bool playerServiceOnly = false;

    [Tooltip("Qualité fixe du service [0-1] (en attendant la jauge cuisine)")]
    [Range(0f, 1f)]
    public float serviceQuality = 0.8f;

    [Header("Post-séjour")]
    [Tooltip("Probabilité (0–1) qu'un meuble soit sale après le départ du monstre")]
    [Range(0f, 1f)] public float furnitureDirtyChance  = 0.7f;

    [Tooltip("Probabilité (0–1) qu'un meuble soit abimé après le départ du monstre")]
    [Range(0f, 1f)] public float furnitureDamageChance = 0.15f;

    [Tooltip("Prefabs de détritus à spawner au sol (laisser vide = pas de détritus)")]
    public GameObject[] debrisPrefabs = System.Array.Empty<GameObject>();

    [Tooltip("Nombre maximum de détritus spawnés à chaque départ")]
    [Range(0, 5)] public int maxDebris = 2;
}
