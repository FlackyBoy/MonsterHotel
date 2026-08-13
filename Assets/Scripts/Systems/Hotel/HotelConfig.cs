using UnityEngine;

/// <summary>
/// Configuration centralisée du jeu — toutes les valeurs de gameplay en un seul endroit.
///
/// SETUP (une seule fois) :
///   1. Clic-droit dans le Project → Create → Hotel → Config
///   2. Nommer l'asset exactement "HotelConfig"
///   3. Le placer dans un dossier "Resources/" (Assets/Resources/HotelConfig.asset)
///   4. Tous les composants lisent automatiquement cet asset au démarrage.
///      Si l'asset est absent, les valeurs saisies dans les Inspectors s'appliquent.
/// </summary>
[CreateAssetMenu(fileName = "HotelConfig", menuName = "Hotel/Config")]
public class HotelConfig : ScriptableObject
{
    // ─── Singleton ────────────────────────────────────────────────

    static HotelConfig _instance;

    /// <summary>Charge l'asset depuis Resources/HotelConfig (null si absent).</summary>
    public static HotelConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<HotelConfig>("HotelConfig");
            return _instance;
        }
    }

    // ─── Économie ─────────────────────────────────────────────────

    [Header("Économie")]
    [Tooltip("Or de départ du joueur")]
    public int startingGold = 350;

    // ─── Satisfaction & Départ ────────────────────────────────────

    [Header("Satisfaction (par défaut pour tous les monstres)")]
    [Tooltip("Satisfaction initiale de chaque monstre à l'arrivée [0-100]")]
    [Range(0f, 100f)]
    public float initialSatisfaction = 80f;

    [Tooltip("En dessous de ce seuil le monstre quitte l'hôtel (0 = jamais)")]
    [Range(0f, 100f)]
    public float leaveThreshold = 10f;

    // ─── Réception ────────────────────────────────────────────────

    [Header("Réception — attente au comptoir")]
    [Tooltip("Attente (secondes) considérée comme bonne → bonus satisfaction")]
    public float receptionGoodWaitTime = 20f;
    [Tooltip("Bonus satisfaction si pris en charge dans les temps")]
    public float receptionWaitBonus = 15f;
    [Tooltip("Malus satisfaction si l'attente est trop longue")]
    public float receptionWaitPenalty = 10f;

    // ─── Pourboires ───────────────────────────────────────────────

    [Header("Pourboires au départ")]
    [Tooltip("Satisfaction >= seuil → bon pourboire")]
    [Range(0f, 100f)]
    public float tipGoodThreshold = 70f;
    [Tooltip("Satisfaction >= seuil → pourboire normal")]
    [Range(0f, 100f)]
    public float tipNormalThreshold = 40f;
    [Tooltip("Bon pourboire = revenuePerNight × ce multiplicateur")]
    public float tipGoodMultiplier = 0.3f;
    [Tooltip("Pourboire normal = revenuePerNight × ce multiplicateur")]
    public float tipNormalMultiplier = 0.1f;

    // ─── Cuisine ──────────────────────────────────────────────────

    [Header("Cuisine — attente repas")]
    [Tooltip("Satisfaction perdue par seconde pendant que le monstre attend son repas à table")]
    public float waitFoodSatisfactionDecay = 0.5f;
    [Tooltip("Durée maximale de la jauge d'attente à table (secondes)")]
    public float waitGaugeMaxTime = 40f;
    [Tooltip("Distance max pour livrer un plat à table")]
    public float deliveryRange = 2f;

    [Header("Cuisine — qualité employé (note 1 → min, note 20 → max)")]
    [Tooltip("Bonus de satisfaction livré par un cuisinier note 1")]
    public float cookDeliveryBonusMin = 5f;
    [Tooltip("Bonus de satisfaction livré par un cuisinier note 20")]
    public float cookDeliveryBonusMax = 25f;

    [Header("Jauge d'attente — visuel")]
    public float waitGaugeWidth    = 14f;
    public float waitGaugeHeight   = 80f;
    public float waitGaugeOffsetY  = 3.2f;
    public Color waitGaugeColor    = Color.white;

    // ─── Cycle jour/nuit ──────────────────────────────────────────

    [Header("Cycle jour/nuit")]
    [Tooltip("Durée d'une journée complète en secondes réels")]
    public float dayDuration = 300f;
    [Tooltip("Heure de départ (0-24)")]
    public float startHour = 8f;

    [Header("Séjour")]
    [Tooltip("Durée réelle (secondes) d'une \"nuit\" de séjour — découplée de dayDuration pour " +
             "permettre plusieurs séjours par chambre et par jour (sinon 1 nuit = 1 jour entier).")]
    public float nightStayDuration = 60f;

    [Header("Visiteurs repas (G6, Phase 1)")]
    [Tooltip("Probabilité qu'un monstre qui spawn soit un visiteur \"repas seul\" (pas de chambre) plutôt qu'un client chambre")]
    [Range(0f, 1f)]
    public float mealVisitorChance = 0.6f;
    [Tooltip("Durée max (secondes) qu'un visiteur repas reste avant de repartir de force, même sans avoir été servi (garde-fou)")]
    public float mealVisitorMaxDuration = 90f;

    // ─── Spawn ────────────────────────────────────────────────────

    [Header("Spawn monstres")]
    [Tooltip("Intervalle en secondes entre deux spawns (si MonsterData.spawnInterval = 0)")]
    public float defaultSpawnInterval = 20f;
    [Tooltip("Heure à partir de laquelle les nouveaux monstres arrivent")]
    public float spawnOpenHour = 10f;
    [Tooltip("Heure à partir de laquelle plus aucun monstre n'arrive")]
    public float spawnCloseHour = 20f;

    // ─── Joueur ───────────────────────────────────────────────────

    [Header("Joueur — déplacement")]
    [Tooltip("Vitesse de marche du joueur (unités/seconde)")]
    public float playerMoveSpeed = 5f;
    [Tooltip("Vitesse de sprint du joueur (unités/seconde)")]
    public float playerSprintSpeed = 9f;

    // ─── Placement ────────────────────────────────────────────────

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

    [Header("Vue construction (RW2 — dézoom caméra pendant le placement)")]
    [Tooltip("Distance additionnelle de la caméra pendant la construction (dézoom). Valeur de départ — à toi d'ajuster.")]
    public float buildModeExtraDistance = 12f;
    [Tooltip("Vitesse de transition du dézoom caméra à l'entrée/sortie du mode construction")]
    public float buildModeZoomLerpSpeed = 4f;
    [Tooltip("Vitesse de recadrage de la caméra vers le curseur de placement pendant la construction")]
    public float buildModeCameraFollowSpeed = 15f;

    [Header("Écran scindé (RW1 — split statique + fusion caméra)")]
    [Tooltip("Distance caméra au-dessus des joueurs (vue top-down)")]
    public float splitScreenCameraHeight = 22f;
    [Tooltip("Distance entre les deux joueurs en dessous de laquelle l'écran ne se scinde plus (une seule caméra plein écran cadre les deux). Valeur de départ — à toi d'ajuster.")]
    public float splitScreenMergeDistance = 15f;
    [Tooltip("Marge autour du seuil pour éviter un flicker split/fusion pile à la limite")]
    public float splitScreenMergeHysteresis = 3f;
    [Tooltip("En fusion, dézoom additionnel par unité de distance entre les deux joueurs")]
    public float splitScreenMergeDistancePerUnit = 0.6f;
    [Tooltip("Vitesse de transition (lerp/seconde) entre écran scindé et fusionné — plus petit = transition plus lente/fluide, plus grand = plus rapide/franche")]
    public float splitScreenMergeTransitionSpeed = 2f;
    [Tooltip("Épaisseur du trait de séparation en pixels (visible uniquement en mode scindé)")]
    public float splitScreenDividerWidth = 4f;
    public Color splitScreenDividerColor = Color.black;

    [Header("Dithering murs")]
    [Tooltip("Vitesse de transition de l'effet de transparence")]
    public float ditherFadeSpeed = 6f;
    [Tooltip("Intensité maximale du dithering (0 = opaque, 1 = semi-transparent, >1 = effet renforcé selon le shader)")]
    public float ditherMaxAlpha = 0.82f;
    [Tooltip("Décalage vertical du test d'occlusion (pour viser la tête)")]
    public float ditherHeightOffset = 0.8f;
    [Tooltip("Rayon du dithering en unités monde autour du joueur (shader Fade Radius)")]
    public float ditherFadeRadius = 5f;

    // ─── Blocs destructibles ─────────────────────────────────────

    [Header("Blocs destructibles")]
    [Tooltip("Distance d'interaction avec un bloc")]
    public float blockInteractRange = 3f;
    [Tooltip("Hauteur visuelle des blocs (unités monde)")]
    public float blockHeight        = 3f;
    [Tooltip("Hysterèse de ciblage : le bloc déjà ciblé conserve un avantage. " +
             "0.75 = 25% d'avantage (stable). Vers 1.0 = aucune hysterèse (plus réactif).")]
    [Range(0.5f, 1f)]
    public float blockTargetHysteresis = 0.75f;

    // ─── Free roam ────────────────────────────────────────────────

    [Header("Free Roam des monstres")]
    [Tooltip("Temps minimum (sec) qu'un monstre attend en chambre avant de sortir se balader")]
    public float roamMinWait          = 15f;
    [Tooltip("Temps maximum (sec) qu'un monstre attend en chambre avant de sortir se balader")]
    public float roamMaxWait          = 40f;
    [Tooltip("Durée totale (sec) d'une balade avant de rentrer en chambre")]
    public float roamDuration         = 20f;
    [Tooltip("Intervalle (sec) entre chaque nouveau waypoint pendant la balade")]
    public float roamWaypointInterval =  6f;
    [Tooltip("Temps d'attente approximatif pour le trajet retour en chambre")]
    public float roamReturnWait       =  8f;

    // ─── Décorations & Stats hôtel ───────────────────────────────

    [Header("Décorations — Confort")]
    [Tooltip("Bonus de satisfaction initiale par point de Confort total de l'hôtel")]
    public float comfortToSatisfactionBonus = 0.5f;

    [Header("Décorations — Renommée")]
    [Tooltip("Renommée minimale pour commencer à réduire l'intervalle de spawn")]
    public float renownSpawnSpeedupThreshold = 10f;
    [Tooltip("Réduction maximale de l'intervalle de spawn au maximum de renommée (0.4 = 40% plus rapide)")]
    [Range(0f, 0.8f)]
    public float renownMaxSpawnReduction = 0.4f;
    [Tooltip("Renommée totale à partir de laquelle les monstres légendaires peuvent apparaître")]
    public float renownLegendaryThreshold = 30f;

    [Header("Debug")]
    [Tooltip("Force tous les meubles sales + débris max au départ d'un monstre, " +
             "quelle que soit la chance définie dans RoomData. Désactiver en production.")]
    public bool debugForceFullDirtyOnVacate = false;

    [Header("── Blocs (modifier ici ou cliquer sur l'asset)")]
    [Tooltip("Tous les types de blocs destructibles (Terre, Pierre...).")]
    public BlockData[] blocks;

    // ─── Employés ─────────────────────────────────────────────────

    [Header("Employés")]
    [Tooltip("1 employé autorisé par X chambres (ex: 3 = 1 employé pour 3 chambres)")]
    public float employeeRoomRatio = 3f;
    [Tooltip("Override debug : si > 0, remplace le calcul par chambres. Remettre à 0 en production.")]
    public int employeeMaxOverride = 0;
    [Tooltip("Taux de fatigue par minute de travail (points/minute)")]
    public float employeeFatigueRate = 2f;
    [Tooltip("Taux de récupération par minute de pause (points/minute)")]
    public float employeeRecoveryRate = 5f;
    [Tooltip("En dessous de ce seuil de bien-être, l'employé démissionne")]
    [Range(0f, 100f)]
    public float employeeResignThreshold = 10f;
    [Tooltip("Perte de bien-être quand le joueur force un employé à travailler pendant sa pause")]
    public float employeeForceWorkPenalty = 20f;
    [Tooltip("Salaire journalier = note × ce multiplicateur")]
    public float employeeSalaryPerRating = 6f;
    [Tooltip("Frais d'embauche = salaire × ce multiplicateur")]
    public float employeeFeeMultiplier = 1.5f;

    [Header("Employés — efficacité par note (note 1 → min, note 20 → max)")]
    [Tooltip("Multiplicateur de breakInterval à note 1 (ex: 0.5 = pause 2× plus souvent)")]
    public float employeeBreakIntervalMinMult = 0.5f;
    [Tooltip("Multiplicateur de breakInterval à note 20 (ex: 1.5 = pause 50% moins souvent)")]
    public float employeeBreakIntervalMaxMult = 1.5f;
    [Tooltip("Multiplicateur de vitesse de déplacement à note 1")]
    public float employeeSpeedMinMult = 0.75f;
    [Tooltip("Multiplicateur de vitesse de déplacement à note 20")]
    public float employeeSpeedMaxMult = 1.25f;
    [Tooltip("Multiplicateur de taux de fatigue à note 1 (ex: 1.5 = se fatigue 50% plus vite)")]
    public float employeeFatigueRateMinMult = 1.5f;
    [Tooltip("Multiplicateur de taux de fatigue à note 20 (ex: 0.5 = se fatigue 2× moins vite)")]
    public float employeeFatigueRateMaxMult = 0.5f;
    [Tooltip("Multiplicateur de taux de récupération à note 1")]
    public float employeeRecoveryRateMinMult = 0.75f;
    [Tooltip("Multiplicateur de taux de récupération à note 20")]
    public float employeeRecoveryRateMaxMult = 1.25f;
    [Tooltip("Durée de base d'une action de nettoyage (secondes). Réduite par la note.")]
    public float employeeCleanBaseDuration = 4f;
    [Tooltip("Délai de base pour accueillir un monstre à la réception (secondes). Réduit par la note.")]
    public float employeeCheckInBaseDelay = 2f;
    [Tooltip("Diviseur de la courbe d'efficacité des AIs (cuisine, nettoyage, réception). " +
             "note 20 → multiplicateur = 1 - 19/diviseur. Ex: 38 = 50% du temps à note 20.")]
    public float employeeRatingCurveDivisor = 38f;

    // ─── Références données ───────────────────────────────────────

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
