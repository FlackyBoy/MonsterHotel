using UnityEngine;

/// <summary>
/// Données statiques d'un type de monstre.
/// Crée via clic-droit → Create → MonsterHotel → Monster Data.
/// </summary>
[CreateAssetMenu(fileName = "MonsterData", menuName = "MonsterHotel/Monster Data")]
public class MonsterData : ScriptableObject
{
    [Header("Identité")]
    public string monsterName = "Monstre";
    public MonsterType monsterType;
    public Sprite icon;
    public GameObject prefab;

    [Header("Séjour")]
    [Tooltip("Revenus générés par nuit de séjour")]
    public int revenuePerNight = 50;
    [Tooltip("Revenus générés par repas servi au restaurant (client chambre ou visiteur)")]
    public int mealRevenue = 15;
    [Tooltip("Durée minimale du séjour en nuits")]
    [Min(1)]
    public int stayMinNights = 1;
    [Tooltip("Durée maximale du séjour en nuits (tirage aléatoire entre min et max)")]
    [Min(1)]
    public int stayMaxNights = 2;
    [Tooltip("Patience au comptoir avant de repartir (secondes)")]
    public float maxWaitTime = 30f;

    [Header("Rooms compatibles")]
    [Tooltip("Types de chambres que ce monstre accepte (RoomTypeData assets).")]
    public RoomTypeData[] compatibleRoomTypes;

    [Header("Déplacement")]
    [Tooltip("Vitesse de déplacement de ce monstre")]
    public float moveSpeed = 3f;
    [Tooltip("Décalage local appliqué à la position de spawn (relatif au forward du SpawnPoint)")]
    public Vector3 spawnOffset;

    [Header("Spawn")]
    [Tooltip("Poids de sélection aléatoire — 1 = normal, 2 = deux fois plus fréquent, 0.5 = rare")]
    public float spawnWeight = 1f;
    [Tooltip("Intervalle en secondes entre deux apparitions de ce type EN CLIENT CHAMBRE (0 = utilise l'intervalle par défaut du SpawnScheduler)")]
    public float spawnInterval = 0f;
    [Tooltip("Nombre max de ce type de monstre en attente simultanément (client chambre uniquement — pas de plafond équivalent pour les visiteurs repas, limitation connue)")]
    public int maxPending = 3;
    [Tooltip("Intervalle en secondes entre deux apparitions de ce type EN VISITEUR REPAS uniquement " +
             "(sans chambre) — flux de spawn indépendant de spawnInterval, tourne en parallèle (pas " +
             "un ratio partagé entre les deux). 0 = utilise l'intervalle par défaut " +
             "(HotelConfig.Spawn.defaultMealVisitorSpawnInterval). Sans effet si ce monstre n'a aucun " +
             "besoin assigné (section Besoins ci-dessous) — il ne peut de toute façon jamais devenir " +
             "visiteur repas dans ce cas.")]
    public float mealVisitorSpawnInterval = 0f;

    [Header("Besoins")]
    [Tooltip("Types de besoins que ce monstre a (NeedType assets). Laisse vide = aucun besoin.")]
    public NeedType[] needs;

    [Tooltip("Valeurs personnalisées par besoin — permet de surcharger decayRate et seuils du NeedType pour ce monstre spécifiquement.")]
    public NeedOverride[] needOverrides;

    [Header("Arrivée")]
    public SpawnTime preferredSpawnTime = SpawnTime.Any;

    [Header("Sommeil (reste en chambre)")]
    [Tooltip("Période pendant laquelle ce monstre DOIT rester dans sa chambre (dort) — n'en sort " +
             "pas se balader. Distinct de preferredSpawnTime (qui contrôle seulement l'heure " +
             "d'ARRIVÉE, à ne pas confondre). Any = jamais confiné. DayOnly = reste en chambre le " +
             "jour (monstre nocturne, ex: vampire). NightOnly = reste en chambre la nuit pour " +
             "dormir (ex: zombie).")]
    public SpawnTime sleepTime = SpawnTime.Any;

    [Header("Départ")]
    [Tooltip("Heure minimale à laquelle ce monstre peut quitter l'hôtel (0-24)")]
    [Range(0f, 24f)]
    public float checkoutWindowStart = 8f;
    [Tooltip("Heure maximale à laquelle ce monstre peut quitter l'hôtel (0-24)")]
    [Range(0f, 24f)]
    public float checkoutWindowEnd = 10f;

    [Header("Légendaire")]
    [Tooltip("Si coché, ce monstre n'apparaît que si la Renommée de l'hôtel dépasse le seuil défini dans HotelConfig.")]
    public bool isLegendary;

    [Header("Détritus en balade")]
    [Tooltip("Chance à chaque étape de balade que ce monstre laisse un détritus au sol (0 = jamais). Valeur de départ — à toi de l'ajuster.")]
    [Range(0f, 1f)] public float roamLitterChance = 0.1f;
    [Tooltip("Prefabs de détritus pouvant être laissés en balade par ce monstre. Vide = ce monstre n'en laisse jamais, quel que soit roamLitterChance.")]
    public GameObject[] litterPrefabs;

    [Header("Compatibilité sociale")]
    [Tooltip("Types de monstres avec lesquels ce monstre peut engager une conversation. Laissé vide par défaut — à toi de définir les affinités. La compatibilité doit être mutuelle (l'autre monstre doit aussi lister ce type) pour qu'une conversation démarre.")]
    public MonsterType[] compatibleSocialTypes;

    [Tooltip("Types de monstres avec lesquels ce monstre entre en conflit s'ils se croisent en balade. Laissé vide par défaut — à toi de définir les animosités (ex: rivalité classique). L'incompatibilité doit être mutuelle (l'autre monstre doit aussi lister ce type) pour qu'une bagarre démarre. Ne pas pré-remplir de paires.")]
    public MonsterType[] incompatibleTypes;

    /// <summary>Retourne true si ce monstre peut séjourner dans une chambre de ce type.</summary>
    public bool IsRoomCompatible(RoomTypeData roomType)
    {
        if (compatibleRoomTypes == null) return false;
        foreach (var t in compatibleRoomTypes)
            if (t == roomType) return true;
        return false;
    }

    /// <summary>Retourne true si ce monstre accepte de discuter avec ce type de monstre.</summary>
    public bool IsSociallyCompatible(MonsterType otherType)
    {
        if (compatibleSocialTypes == null) return false;
        foreach (var t in compatibleSocialTypes)
            if (t == otherType) return true;
        return false;
    }

    /// <summary>Retourne true si ce monstre entre en conflit avec ce type de monstre.</summary>
    public bool IsSociallyIncompatible(MonsterType otherType)
    {
        if (incompatibleTypes == null) return false;
        foreach (var t in incompatibleTypes)
            if (t == otherType) return true;
        return false;
    }
}

public enum MonsterType  { Zombie, Vampire, Werewolf }
public enum SpawnTime    { Any, DayOnly, NightOnly }

[System.Serializable]
public class NeedOverride
{
    public NeedType needType;

    [Tooltip("0 = utilise la valeur du NeedType")]
    public float decayRate = 0f;

    [Tooltip("0 = utilise la valeur du NeedType")]
    public float unsatisfiedThreshold = 0f;

    [Tooltip("0 = utilise la valeur du NeedType")]
    public float criticalThreshold = 0f;
}
