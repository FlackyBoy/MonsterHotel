using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Calcule les stats du récap de fin de journée (U2) — journalières (score, or, satisfaction,
/// arrivées/départs, renommée gagnée) et générales (jour, or total, monstres présents), plus le
/// détail par type de monstre. S'appuie sur TimeManager.OnNewDay, HotelStatsManager.OnGuestDeparted
/// (G1) et SpawnScheduler.OnMonsterArrived.
///
/// Setup : ajouter ce composant sur un objet persistant de la scène, assigner le champ Panel.
/// </summary>
public class DayScoreManager : MonoBehaviour
{
    public static DayScoreManager Instance { get; private set; }

    [Header("UI")]
    public DayEndPanel panel;

    // Poids/seuils de score (journalier + global) déplacés dans HotelConfig.Score (ScoreConfig,
    // asset Resources/Config/ScoreConfig.asset) — cohérent avec le reste du jeu (Économie, Spawn,
    // Satisfaction, etc.), voir HotelConfig.cs. Plus de champs ici, valeurs par défaut identiques
    // à ce qui existait avant cette migration.

    // ─── Structures passées à DayEndPanel ──────────────────────────

    public struct DailyRecapData
    {
        public int   day, score, stars, goldDelta;
        public float avgSatisfaction, renownGained;

        // Arrivées et clients servis, distingués par canal (G12) — un client resto peut repartir
        // sans avoir été servi (abandon/délai dépassé), contrairement à un client chambre pour qui
        // occuper la chambre EST le service.
        public int arrivalCountRoom, arrivalCountRestaurant;
        public int servedCountRoom, servedCountRestaurant, unservedCountRestaurant;
        public int mealRevenueToday;
    }

    public struct GeneralRecapData
    {
        public int   currentDay, totalGold, presentCount;
        public int   globalScore, globalStars;
        public float avgSatisfactionAllTime, totalRenown, totalComfort;

        // Équivalents cumulés (jamais remis à zéro) des stats journalières ci-dessus.
        public int totalArrivalsRoom, totalArrivalsRestaurant;
        public int totalServedRoom, totalServedRestaurant, totalUnservedRestaurant;
        public int totalMealRevenue;
    }

    public struct MonsterTypeStats
    {
        public float avgSatisfaction;
        public int   departureCount, presentCount;
        public float globalRenown, renownGainedToday, renownLostToday;
        public int   mealRevenueToday, mealRevenueTotal;
    }

    // ─── Privé — accumulateurs de la journée en cours ──────────────

    int   _goldAtDayStart;
    float _satisfactionSum;
    int   _departureCount;
    float _renownGainedToday;

    // Arrivées et clients servis du jour, par canal (G12).
    int _arrivalCountRoom, _arrivalCountRestaurant;
    int _servedCountRoom, _servedCountRestaurant, _unservedCountRestaurant;
    int _mealRevenueToday;

    // ─── Cumulés depuis le début de la partie — jamais remis à zéro (onglet Général) ─
    int   _totalDeparturesAllTime;
    float _satisfactionSumAllTime;
    int   _totalArrivalsRoomAllTime, _totalArrivalsRestaurantAllTime;
    int   _totalServedRoomAllTime, _totalServedRestaurantAllTime, _totalUnservedRestaurantAllTime;
    int   _totalMealRevenueAllTime;

    // Agrégation par type de monstre (pour l'onglet Monstres du récap).
    readonly Dictionary<MonsterType, float> _satisfactionSumByType   = new();
    readonly Dictionary<MonsterType, int>   _departureCountByType    = new();
    readonly Dictionary<MonsterType, float> _renownGainedByType      = new();
    readonly Dictionary<MonsterType, float> _renownLostByType        = new();
    readonly Dictionary<MonsterType, int>   _mealRevenueTodayByType  = new();
    readonly Dictionary<MonsterType, int>   _mealRevenueTotalByType  = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        _goldAtDayStart = EconomyManager.Instance != null ? EconomyManager.Instance.Gold : 0;

        if (HotelStatsManager.Instance != null)
        {
            HotelStatsManager.Instance.OnGuestDeparted += HandleGuestDeparted;
            HotelStatsManager.Instance.OnMealRevenue   += HandleMealRevenue;
        }
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnNewDay += HandleNewDay;
        if (SpawnScheduler.Instance != null)
            SpawnScheduler.Instance.OnMonsterArrived += HandleMonsterArrived;
    }

    void OnDestroy()
    {
        if (HotelStatsManager.Instance != null)
        {
            HotelStatsManager.Instance.OnGuestDeparted -= HandleGuestDeparted;
            HotelStatsManager.Instance.OnMealRevenue   -= HandleMealRevenue;
        }
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnNewDay -= HandleNewDay;
        if (SpawnScheduler.Instance != null)
            SpawnScheduler.Instance.OnMonsterArrived -= HandleMonsterArrived;
    }

    void HandleMonsterArrived(MonsterType type, GuestChannel channel)
    {
        if (channel == GuestChannel.Room)
        {
            _arrivalCountRoom += 1;
            _totalArrivalsRoomAllTime += 1;
        }
        else
        {
            _arrivalCountRestaurant += 1;
            _totalArrivalsRestaurantAllTime += 1;
        }
    }

    void HandleGuestDeparted(MonsterType type, float satisfaction, bool angry, float renownDelta, GuestChannel channel, bool served)
    {
        _satisfactionSum   += satisfaction;
        _departureCount    += 1;
        _renownGainedToday += renownDelta;

        _totalDeparturesAllTime += 1;
        _satisfactionSumAllTime += satisfaction;

        // Client chambre : toujours servi (occuper la chambre EST le service). Client resto :
        // servi seulement s'il a effectivement mangé — sinon reparti sans avoir été servi.
        if (channel == GuestChannel.Room)
        {
            _servedCountRoom += 1;
            _totalServedRoomAllTime += 1;
        }
        else if (served)
        {
            _servedCountRestaurant += 1;
            _totalServedRestaurantAllTime += 1;
        }
        else
        {
            _unservedCountRestaurant += 1;
            _totalUnservedRestaurantAllTime += 1;
        }

        _satisfactionSumByType.TryGetValue(type, out float sum);
        _satisfactionSumByType[type] = sum + satisfaction;
        _departureCountByType.TryGetValue(type, out int count);
        _departureCountByType[type] = count + 1;

        // Répartit le delta signé (positif = gain, négatif = perte) dans deux accumulateurs
        // séparés — même donnée que le total net déjà utilisé pour le score, juste éclatée.
        if (renownDelta >= 0f)
        {
            _renownGainedByType.TryGetValue(type, out float gained);
            _renownGainedByType[type] = gained + renownDelta;
        }
        else
        {
            _renownLostByType.TryGetValue(type, out float lost);
            _renownLostByType[type] = lost - renownDelta; // stocké en positif
        }
    }

    void HandleMealRevenue(MonsterType type, int amount)
    {
        _mealRevenueToday += amount;
        _totalMealRevenueAllTime += amount;

        _mealRevenueTodayByType.TryGetValue(type, out int today);
        _mealRevenueTodayByType[type] = today + amount;
        _mealRevenueTotalByType.TryGetValue(type, out int total);
        _mealRevenueTotalByType[type] = total + amount;
    }

    void HandleNewDay(int newDay)
    {
        var scoreConfig = HotelConfig.Score;

        // Attractivité des chambres → renommée (voir HotelStatsManager.RecalcRoomAttractiveness) —
        // recalculée une fois par jour, ici, avant tout calcul de renommée plus bas.
        HotelStatsManager.Instance?.RecalcRoomAttractiveness();

        int   currentGold      = EconomyManager.Instance != null ? EconomyManager.Instance.Gold : 0;
        int   goldDelta        = currentGold - _goldAtDayStart;
        float avgSatisfaction  = _departureCount > 0 ? _satisfactionSum / _departureCount : 0f;

        int score = Mathf.RoundToInt(
            goldDelta * scoreConfig.goldWeight +
            avgSatisfaction * scoreConfig.satisfactionWeight +
            _renownGainedToday * scoreConfig.renownWeight);

        int stars = ComputeStars(score, scoreConfig.oneStarScore, scoreConfig.twoStarScore,
            scoreConfig.threeStarScore, scoreConfig.fourStarScore, scoreConfig.fiveStarScore);

        Debug.Log($"[Score] Jour {newDay - 1} terminé — {score} pts ({stars}★) | " +
                   $"Or: {(goldDelta >= 0 ? "+" : "")}{goldDelta}G | " +
                   $"Arrivées: {_arrivalCountRoom} chambre / {_arrivalCountRestaurant} resto | " +
                   $"Servis: {_servedCountRoom} chambre / {_servedCountRestaurant} resto (+{_unservedCountRestaurant} non servis) | " +
                   $"Satisfaction moy: {avgSatisfaction:F0}/100 | " +
                   $"Renommée: {(_renownGainedToday >= 0 ? "+" : "")}{_renownGainedToday:F1} | " +
                   $"Gain resto: +{_mealRevenueToday}G");

        // Scan live des monstres actuellement en scène — pas un accumulateur journalier, une
        // photo à l'instant présent (même pattern que RoomManagementPanel.cs pour recenser les
        // monstres). Sert au total du Général et au détail par type de l'onglet Monstres.
        var presentRefs = FindObjectsByType<MonsterDataReference>(FindObjectsSortMode.None);
        var presentCountByType = new Dictionary<MonsterType, int>();
        foreach (var r in presentRefs)
        {
            if (r?.Data == null) continue;
            presentCountByType.TryGetValue(r.Data.monsterType, out int c);
            presentCountByType[r.Data.monsterType] = c + 1;
        }

        var daily = new DailyRecapData
        {
            day = newDay - 1, score = score, stars = stars, goldDelta = goldDelta,
            avgSatisfaction = avgSatisfaction, renownGained = _renownGainedToday,
            arrivalCountRoom = _arrivalCountRoom, arrivalCountRestaurant = _arrivalCountRestaurant,
            servedCountRoom = _servedCountRoom, servedCountRestaurant = _servedCountRestaurant,
            unservedCountRestaurant = _unservedCountRestaurant, mealRevenueToday = _mealRevenueToday,
        };

        // Renommée totale toutes catégories confondues, et satisfaction moyenne sur tous les
        // départs depuis le début — équivalents "globaux" des stats journalières.
        float totalRenown = 0f;
        if (HotelStatsManager.Instance != null)
            foreach (MonsterType type in System.Enum.GetValues(typeof(MonsterType)))
                totalRenown += HotelStatsManager.Instance.RenownForCategory(type);

        float avgSatisfactionAllTime = _totalDeparturesAllTime > 0
            ? _satisfactionSumAllTime / _totalDeparturesAllTime : 0f;

        // Confort de l'hôtel (décorations posées) — pas d'équivalent "gagné aujourd'hui" (ne se
        // gagne pas au jour le jour, c'est un instantané), donc ajouté seulement au score global,
        // pas au journalier — comme l'Or total/la Satisfaction globale.
        float totalComfort = HotelStatsManager.Instance != null ? HotelStatsManager.Instance.TotalComfort : 0f;

        int globalScore = Mathf.RoundToInt(
            currentGold * scoreConfig.globalGoldWeight +
            avgSatisfactionAllTime * scoreConfig.globalSatisfactionWeight +
            totalRenown * scoreConfig.globalRenownWeight +
            totalComfort * scoreConfig.globalComfortWeight);

        int globalStars = ComputeStars(globalScore, scoreConfig.globalOneStarScore, scoreConfig.globalTwoStarScore,
            scoreConfig.globalThreeStarScore, scoreConfig.globalFourStarScore, scoreConfig.globalFiveStarScore);

        var general = new GeneralRecapData
        {
            currentDay = newDay, totalGold = currentGold, presentCount = presentRefs.Length,
            globalScore = globalScore, globalStars = globalStars,
            avgSatisfactionAllTime = avgSatisfactionAllTime, totalRenown = totalRenown, totalComfort = totalComfort,
            totalArrivalsRoom = _totalArrivalsRoomAllTime, totalArrivalsRestaurant = _totalArrivalsRestaurantAllTime,
            totalServedRoom = _totalServedRoomAllTime, totalServedRestaurant = _totalServedRestaurantAllTime,
            totalUnservedRestaurant = _totalUnservedRestaurantAllTime, totalMealRevenue = _totalMealRevenueAllTime,
        };

        // Union de toutes les catégories concernées (départs, arrivées, présence ou gain resto ce
        // jour) — un type qui n'a rien fait mais a des monstres présents doit quand même apparaître.
        var allTypes = new HashSet<MonsterType>();
        foreach (var k in _satisfactionSumByType.Keys)  allTypes.Add(k);
        foreach (var k in presentCountByType.Keys)      allTypes.Add(k);
        foreach (var k in _mealRevenueTodayByType.Keys) allTypes.Add(k);

        var statsByType = new Dictionary<MonsterType, MonsterTypeStats>();
        foreach (var type in allTypes)
        {
            int   depCount     = _departureCountByType.TryGetValue(type, out int dc) ? dc : 0;
            float satSum       = _satisfactionSumByType.TryGetValue(type, out float ss) ? ss : 0f;
            int   presCount    = presentCountByType.TryGetValue(type, out int pc) ? pc : 0;
            float gained       = _renownGainedByType.TryGetValue(type, out float g) ? g : 0f;
            float lost         = _renownLostByType.TryGetValue(type, out float l) ? l : 0f;
            int   mealToday    = _mealRevenueTodayByType.TryGetValue(type, out int mt) ? mt : 0;
            int   mealTotal    = _mealRevenueTotalByType.TryGetValue(type, out int mtt) ? mtt : 0;

            statsByType[type] = new MonsterTypeStats
            {
                avgSatisfaction   = depCount > 0 ? satSum / depCount : 0f,
                departureCount    = depCount,
                presentCount      = presCount,
                globalRenown      = HotelStatsManager.Instance != null ? HotelStatsManager.Instance.RenownForCategory(type) : 0f,
                renownGainedToday = gained,
                renownLostToday   = lost,
                mealRevenueToday  = mealToday,
                mealRevenueTotal  = mealTotal,
            };
        }

        // Sans panel assigné (UI pas encore construite), on s'arrête au log ci-dessus — pas de
        // pause sans écran pour la lever, le jeu continuerait de tourner figé sans explication.
        if (panel != null)
        {
            panel.Show(daily, general, statsByType);
            GameManager.Instance?.PauseGame();
        }

        // Réinitialise pour la journée qui commence — pas les accumulateurs "AllTime"/Total, qui ne
        // sont jamais remis à zéro (voir champ correspondant dans GeneralRecapData).
        _goldAtDayStart     = currentGold;
        _satisfactionSum    = 0f;
        _departureCount     = 0;
        _renownGainedToday  = 0f;
        _arrivalCountRoom          = 0;
        _arrivalCountRestaurant    = 0;
        _servedCountRoom           = 0;
        _servedCountRestaurant     = 0;
        _unservedCountRestaurant   = 0;
        _mealRevenueToday          = 0;
        _satisfactionSumByType.Clear();
        _departureCountByType.Clear();
        _renownGainedByType.Clear();
        _renownLostByType.Clear();
        _mealRevenueTodayByType.Clear();
    }

    static int ComputeStars(int score, int oneStar, int twoStar, int threeStar, int fourStar, int fiveStar) =>
        score >= fiveStar  ? 5 :
        score >= fourStar  ? 4 :
        score >= threeStar ? 3 :
        score >= twoStar   ? 2 :
        score >= oneStar   ? 1 : 0;
}
