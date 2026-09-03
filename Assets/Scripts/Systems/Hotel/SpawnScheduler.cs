using UnityEngine;

/// <summary>
/// Spawne des monstres à intervalles indépendants par type et les enregistre dans le ReservationSystem.
/// Chaque MonsterData possède son propre spawnInterval et maxPending.
///
/// Deux flux de spawn totalement indépendants tournent en parallèle par type de monstre (pas un
/// tirage qui partage un seul flux) : client chambre (spawnInterval) et visiteur repas sans chambre
/// (mealVisitorSpawnInterval). Un même monstre peut donc arriver comme client chambre ET, séparément,
/// comme visiteur repas selon ses deux cadences propres.
/// </summary>
public class SpawnScheduler : MonoBehaviour
{
    public static SpawnScheduler Instance { get; private set; }

    /// <summary>Déclenché à chaque arrivée réelle d'un monstre — précise le canal (chambre ou resto).</summary>
    public event System.Action<MonsterType, GuestChannel> OnMonsterArrived;

    [Header("Pool de monstres")]
    [Tooltip("Types de monstres pouvant arriver.")]
    public MonsterData[] monsterPool;

    [Header("Spawn")]
    [Tooltip("Point d'apparition des monstres (entrée de l'hôtel)")]
    public Transform spawnPoint;

    [Header("Debug")]
    [Tooltip("Spawne chaque type de monstre dès le démarrage pour tester")]
    public bool spawnOnStart = false;
    [Tooltip("Monstre utilisé par 'Debug : Spawner le monstre choisi' ci-dessous — indépendant du tirage aléatoire du pool.")]
    public MonsterData debugMonsterToSpawn;

    // ─── Privé ────────────────────────────────────────────────────

    float[] _timers;        // flux client chambre
    float[] _visitorTimers; // flux visiteur repas
    float   _defaultSpawnInterval;
    float   _openHour;
    float   _closeHour;

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        var cfg = HotelConfig.Spawn;
        _defaultSpawnInterval = cfg != null ? cfg.defaultSpawnInterval : 20f;
        _openHour             = cfg != null ? cfg.spawnOpenHour        : 10f;
        _closeHour            = cfg != null ? cfg.spawnCloseHour       : 20f;

        // HotelConfig.Catalog.monsters est la source de vérité — prime sur l'assignation locale
        // de la scène pour éviter que les deux catalogues divergent.
        var catalog = HotelConfig.Catalog;
        if (catalog != null && catalog.monsters != null && catalog.monsters.Length > 0)
            monsterPool = catalog.monsters;
    }

    void Start()
    {
        if (monsterPool == null || monsterPool.Length == 0) return;

        _timers        = new float[monsterPool.Length];
        _visitorTimers = new float[monsterPool.Length];
        for (int i = 0; i < monsterPool.Length; i++)
        {
            _timers[i]        = spawnOnStart ? 0f : IntervalFor(monsterPool[i]);
            _visitorTimers[i] = spawnOnStart ? 0f : VisitorIntervalFor(monsterPool[i]);
        }
    }

    void Update()
    {
        if (monsterPool == null || ReservationSystem.Instance == null) return;

        for (int i = 0; i < monsterPool.Length; i++)
        {
            var data = monsterPool[i];
            if (data == null) continue;

            _timers[i] -= Time.deltaTime;
            if (_timers[i] <= 0f)
            {
                // Le fantôme (G8) passe aussi par ce flux — il rejoint la même file de réception
                // que n'importe quel client, ReservationSystem sait ne jamais lui assigner de
                // chambre (voir IsServiceable/CheckInGhost).
                TrySpawnRoomGuest(data);
                _timers[i] = IntervalFor(data);
            }

            // Flux visiteur repas — indépendant, seulement si ce monstre a un besoin à satisfaire.
            // Sans objet pour le fantôme : pas de besoin propre avant possession, et une fois possédé
            // c'est PossessableHuman/MonsterNeedSeeker qui gère la faim, pas ce timer de spawn.
            if (data.monsterType != MonsterType.Ghost && data.needs != null && data.needs.Length > 0)
            {
                _visitorTimers[i] -= Time.deltaTime;
                if (_visitorTimers[i] <= 0f)
                {
                    TrySpawnMealVisitor(data);
                    _visitorTimers[i] = VisitorIntervalFor(data);
                }
            }
        }
    }

    // ─── Spawn — client chambre ─────────────────────────────────────

    /// <summary>bypassGates : ignore la fenêtre horaire, le seuil légendaire et le plafond maxPending — réservé au spawn forcé de debug.</summary>
    void TrySpawnRoomGuest(MonsterData data, bool bypassGates = false)
    {
        if (data == null) return;

        if (!bypassGates)
        {
            if (!IsWithinSpawnWindow(data)) return;
            if (!PassesLegendaryGate(data)) return;

            int pending = ReservationSystem.Instance.PendingCountOfType(data);
            if (pending >= data.maxPending) return;
        }

        var go = SpawnMonsterObject(data, GuestChannel.Room);
        ReservationSystem.Instance.RegisterArrival(data, go, spawnPoint);
    }

    // ─── Spawn — visiteur repas ──────────────────────────────────────

    /// <summary>
    /// Arrive directement sans réserver de chambre : ne rejoint pas la file de réception hôtel, va
    /// directement chercher à satisfaire son besoin (restaurant), repart une fois servi — ou peut
    /// se convertir en client chambre après coup (voir MonsterNeedSeeker.stayAfterMealChance, G6-P4).
    /// bypassGates : ignore la fenêtre horaire et le seuil légendaire — réservé au spawn forcé de
    /// debug (pas de plafond maxPending équivalent pour ce flux, limitation connue).
    /// </summary>
    void TrySpawnMealVisitor(MonsterData data, bool bypassGates = false)
    {
        if (data == null) return;
        bool canBeVisitor = data.needs != null && data.needs.Length > 0;
        if (!canBeVisitor) return;

        if (!bypassGates)
        {
            if (!IsWithinSpawnWindow(data)) return;
            if (!PassesLegendaryGate(data)) return;
        }

        var go     = SpawnMonsterObject(data, GuestChannel.Restaurant);
        var needs  = go.GetComponent<MonsterNeedsComponent>();
        var seeker = go.GetComponent<MonsterNeedSeeker>();

        // Arrive déjà avec le besoin bas — va chercher à le satisfaire dès la 1ère vérification
        foreach (var needType in data.needs)
            if (needType != null) needs.SetNeedLevel(needType, 0.15f);

        needs.ActivateDecay();
        var visitorCfg = HotelConfig.Spawn;
        float maxDuration = visitorCfg != null ? visitorCfg.mealVisitorMaxDuration : 90f;
        seeker.ActivateAsVisitor(maxDuration);

        // Marqueur visuel (G6, Phase 2) — distingue le visiteur d'un client chambre
        if (go.GetComponent<MealVisitorBadge>() == null)
            go.AddComponent<MealVisitorBadge>();

        Debug.Log($"[Spawn] {data.monsterName} arrive en visiteur repas (pas de chambre).");
    }

    // ─── Instanciation commune ───────────────────────────────────────

    /// <summary>Tire au hasard un skin parmi data.prefab + data.skinVariants (poids égal) —
    /// data.skinVariants vide = toujours data.prefab, comportement inchangé.</summary>
    static GameObject PickSkinPrefab(MonsterData data)
    {
        if (data.skinVariants == null || data.skinVariants.Length == 0) return data.prefab;

        int count = data.skinVariants.Length + 1; // +1 pour data.prefab lui-même
        int index = Random.Range(0, count);
        return index == 0 ? data.prefab : data.skinVariants[index - 1];
    }

    GameObject SpawnMonsterObject(MonsterData data, GuestChannel channel)
    {
        Vector3 spawnRot = spawnPoint != null ? spawnPoint.eulerAngles : Vector3.zero;
        Vector3 pos = spawnPoint != null
            ? spawnPoint.position + Quaternion.Euler(spawnRot) * data.spawnOffset
            : data.spawnOffset;

        GameObject go;
        var skinPrefab = PickSkinPrefab(data);
        if (skinPrefab != null)
            go = Instantiate(skinPrefab, pos, Quaternion.identity);
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = pos;
        }
        go.name = $"Monster_{data.monsterName}";

        // Référence MonsterData sur le GO
        var dataRef = go.GetComponent<MonsterDataReference>() ?? go.AddComponent<MonsterDataReference>();
        dataRef.Data = data;

        // Applique la vitesse définie dans le MonsterData
        var mover = go.GetComponent<MonsterMover>() ?? go.AddComponent<MonsterMover>();
        mover.moveSpeed    = data.moveSpeed;
        mover.ignoresWalls = data.canPhaseThroughWalls;

        // Doivent être ajoutés avant Needs/Seeker/Roam ci-dessous : AddComponent déclenche Awake()
        // immédiatement et de façon synchrone, donc un GetComponent<MonsterSocialBehavior>()/
        // <MonsterFightBehavior>() dans leur propre Awake() retournerait null si ces composants
        // étaient ajoutés après eux.
        if (go.GetComponent<MonsterSocialBehavior>() == null)
            go.AddComponent<MonsterSocialBehavior>();
        if (go.GetComponent<MonsterFightBehavior>() == null)
            go.AddComponent<MonsterFightBehavior>();

        // Initialise les besoins et la satisfaction
        var needs = go.GetComponent<MonsterNeedsComponent>() ?? go.AddComponent<MonsterNeedsComponent>();
        needs.Initialize(data);
        if (go.GetComponent<SatisfactionComponent>() == null)
            go.AddComponent<SatisfactionComponent>();
        if (go.GetComponent<MonsterNeedSeeker>() == null)
            go.AddComponent<MonsterNeedSeeker>();
        if (go.GetComponent<MonsterRoamBehavior>() == null)
            go.AddComponent<MonsterRoamBehavior>();
        if (go.GetComponent<MonsterDebugTools>() == null)
            go.AddComponent<MonsterDebugTools>();

        OnMonsterArrived?.Invoke(data.monsterType, channel);
        return go;
    }

    // ─── Gates communs ────────────────────────────────────────────

    bool IsWithinSpawnWindow(MonsterData data)
    {
        float hour = TimeManager.Instance?.Hour ?? 12f;

        if (data.preferredSpawnTime == SpawnTime.Any)
        {
            // Pas de préférence : gaté par la fenêtre d'ouverture générale de la réception
            return hour >= _openHour && hour < _closeHour;
        }

        // Préférence jour/nuit explicite : prime sur la fenêtre d'ouverture générale (sinon
        // DayOnly/NightOnly peut devenir impossible à satisfaire si la fenêtre d'ouverture ne
        // chevauche pas la plage jour/nuit correspondante).
        var currentTime = TimeManager.Instance?.CurrentSpawnTime ?? SpawnTime.DayOnly;
        return data.preferredSpawnTime == currentTime;
    }

    /// <summary>
    /// G1 : gate par la renommée GAGNÉE PAR CETTE CATÉGORIE de monstre (base décorations partagée +
    /// renommée accumulée via la satisfaction des départs de ce type précis), pas la renommée
    /// globale de l'hôtel — voir HotelStatsManager.RenownForCategory().
    /// </summary>
    bool PassesLegendaryGate(MonsterData data)
    {
        if (!data.isLegendary) return true;

        var cfg = HotelConfig.Spawn;
        float threshold = cfg != null ? cfg.renownLegendaryThreshold : 30f;
        float renown    = HotelStatsManager.Instance?.RenownForCategory(data.monsterType) ?? 0f;
        return renown >= threshold;
    }

    // ─── Intervalles ──────────────────────────────────────────────

    float IntervalFor(MonsterData data)
    {
        float baseInterval = data.spawnInterval > 0f ? data.spawnInterval : _defaultSpawnInterval;
        return Mathf.Max(1f, ApplyRenownReduction(baseInterval));
    }

    float VisitorIntervalFor(MonsterData data)
    {
        var cfg = HotelConfig.Spawn;
        float defaultInterval = cfg != null ? cfg.defaultMealVisitorSpawnInterval : 30f;
        float baseInterval = data.mealVisitorSpawnInterval > 0f ? data.mealVisitorSpawnInterval : defaultInterval;
        return Mathf.Max(1f, ApplyRenownReduction(baseInterval));
    }

    /// <summary>La Renommée réduit progressivement l'intervalle de spawn (les deux flux).</summary>
    float ApplyRenownReduction(float baseInterval)
    {
        var cfg = HotelConfig.Spawn;
        if (cfg == null) return baseInterval;

        float renown = HotelStatsManager.Instance?.TotalRenown ?? 0f;
        if (renown >= cfg.renownSpawnSpeedupThreshold && cfg.renownMaxSpawnReduction > 0f)
        {
            float t         = Mathf.Clamp01((renown - cfg.renownSpawnSpeedupThreshold) / cfg.renownSpawnSpeedupThreshold);
            float reduction = t * cfg.renownMaxSpawnReduction;
            baseInterval   *= (1f - reduction);
        }
        return baseInterval;
    }

    // ─── API ──────────────────────────────────────────────────────

    /// <summary>Force le spawn d'un monstre aléatoire immédiatement (debug) — tente les deux flux, gates respectés.</summary>
    [ContextMenu("Spawner un monstre maintenant")]
    public void ForceSpawn()
    {
        if (monsterPool == null || monsterPool.Length == 0) return;
        var data = monsterPool[Random.Range(0, monsterPool.Length)];
        TrySpawnRoomGuest(data);
        TrySpawnMealVisitor(data);
    }

    /// <summary>
    /// DEBUG : force le spawn d'un monstre qui réservera directement une chambre — ignore la
    /// fenêtre horaire, le seuil légendaire et les plafonds, pour tester sans attendre.
    /// </summary>
    [ContextMenu("Debug : Spawner un client chambre")]
    public void ForceSpawnRoomGuest()
    {
        if (monsterPool == null || monsterPool.Length == 0) return;
        TrySpawnRoomGuest(monsterPool[Random.Range(0, monsterPool.Length)], bypassGates: true);
    }

    /// <summary>
    /// DEBUG : force le spawn d'un visiteur repas (va directement au restaurant, pas de
    /// chambre) — ignore la fenêtre horaire et le seuil légendaire. Choisit un monstre du pool
    /// ayant au moins un besoin défini (sinon rien à satisfaire au restaurant).
    /// </summary>
    [ContextMenu("Debug : Spawner un visiteur repas")]
    public void ForceSpawnRestaurantVisitor()
    {
        var candidate = PickMonsterWithNeeds();
        if (candidate == null)
        {
            Debug.LogWarning("[Spawn] Aucun monstre du pool n'a de besoin défini (MonsterData.needs) — impossible de forcer un visiteur repas.");
            return;
        }
        TrySpawnMealVisitor(candidate, bypassGates: true);
    }

    /// <summary>
    /// DEBUG : force le spawn du monstre choisi dans `debugMonsterToSpawn` (pas un tirage aléatoire
    /// du pool) en tant que client chambre — réserve directement une chambre, ignore fenêtre
    /// horaire/seuil légendaire/plafonds. Utile pour tester des interactions entre types précis
    /// (ex : deux monstres compatibles pour la conversation).
    /// </summary>
    [ContextMenu("Debug : Spawner le monstre choisi (client chambre)")]
    public void ForceSpawnChosenRoomGuest()
    {
        if (debugMonsterToSpawn == null)
        {
            Debug.LogWarning("[Spawn] Aucun monstre choisi dans 'Debug Monster To Spawn'.");
            return;
        }
        TrySpawnRoomGuest(debugMonsterToSpawn, bypassGates: true);
    }

    /// <summary>
    /// DEBUG : force le spawn du monstre choisi dans `debugMonsterToSpawn` en tant que visiteur
    /// repas (pas de chambre) — même limitation que ForceSpawnRestaurantVisitor si ce monstre n'a
    /// aucun besoin défini.
    /// </summary>
    [ContextMenu("Debug : Spawner le monstre choisi (visiteur repas)")]
    public void ForceSpawnChosenVisitor()
    {
        if (debugMonsterToSpawn == null)
        {
            Debug.LogWarning("[Spawn] Aucun monstre choisi dans 'Debug Monster To Spawn'.");
            return;
        }
        if (debugMonsterToSpawn.needs == null || debugMonsterToSpawn.needs.Length == 0)
        {
            Debug.LogWarning($"[Spawn] {debugMonsterToSpawn.monsterName} n'a aucun besoin défini — impossible de le forcer en visiteur repas.");
            return;
        }
        TrySpawnMealVisitor(debugMonsterToSpawn, bypassGates: true);
    }

    MonsterData PickMonsterWithNeeds()
    {
        if (monsterPool == null) return null;
        var withNeeds = new System.Collections.Generic.List<MonsterData>();
        foreach (var m in monsterPool)
            if (m != null && m.needs != null && m.needs.Length > 0) withNeeds.Add(m);
        return withNeeds.Count > 0 ? withNeeds[Random.Range(0, withNeeds.Count)] : null;
    }
}
