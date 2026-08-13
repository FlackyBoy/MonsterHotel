using UnityEngine;

/// <summary>
/// Spawne des monstres à intervalles indépendants par type et les enregistre dans le ReservationSystem.
/// Chaque MonsterData possède son propre spawnInterval et maxPending.
/// </summary>
public class SpawnScheduler : MonoBehaviour
{
    public static SpawnScheduler Instance { get; private set; }

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

    float[] _timers;
    float   _defaultSpawnInterval;
    float   _openHour;
    float   _closeHour;

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        var cfg = HotelConfig.Instance;
        _defaultSpawnInterval = cfg != null ? cfg.defaultSpawnInterval : 20f;
        _openHour             = cfg != null ? cfg.spawnOpenHour        : 10f;
        _closeHour            = cfg != null ? cfg.spawnCloseHour       : 20f;

        // HotelConfig.monsters est la source de vérité — prime sur l'assignation locale
        // de la scène pour éviter que les deux catalogues divergent.
        if (cfg != null && cfg.monsters != null && cfg.monsters.Length > 0)
            monsterPool = cfg.monsters;
    }

    void Start()
    {
        if (monsterPool == null || monsterPool.Length == 0) return;

        _timers = new float[monsterPool.Length];
        for (int i = 0; i < monsterPool.Length; i++)
        {
            float interval = IntervalFor(monsterPool[i]);
            _timers[i] = spawnOnStart ? 0f : interval;
        }
    }

    void Update()
    {
        if (monsterPool == null || ReservationSystem.Instance == null) return;

        for (int i = 0; i < monsterPool.Length; i++)
        {
            _timers[i] -= Time.deltaTime;
            if (_timers[i] <= 0f)
            {
                TrySpawn(monsterPool[i]);
                _timers[i] = IntervalFor(monsterPool[i]);
            }
        }
    }

    // ─── Spawn ────────────────────────────────────────────────────

    /// <summary>
    /// forceVisitor : null = tirage aléatoire normal (mealVisitorChance) ; true/false = impose le
    /// mode visiteur repas / client chambre, sans tirage (debug).
    /// bypassGates : ignore la fenêtre horaire, le seuil légendaire et le plafond maxPending —
    /// réservé au spawn forcé de debug, jamais utilisé par le cycle de spawn normal.
    /// </summary>
    void TrySpawn(MonsterData data, bool? forceVisitor = null, bool bypassGates = false)
    {
        if (data == null) return;

        if (!bypassGates)
        {
            float hour = TimeManager.Instance?.Hour ?? 12f;

            if (data.preferredSpawnTime == SpawnTime.Any)
            {
                // Pas de préférence : gaté par la fenêtre d'ouverture générale de la réception
                if (hour >= _closeHour || hour < _openHour)
                    return;
            }
            else
            {
                // Préférence jour/nuit explicite : prime sur la fenêtre d'ouverture générale
                // (sinon DayOnly/NightOnly peut devenir impossible à satisfaire si la fenêtre
                // d'ouverture ne chevauche pas la plage jour/nuit correspondante).
                var currentTime = TimeManager.Instance?.CurrentSpawnTime ?? SpawnTime.DayOnly;
                if (data.preferredSpawnTime != currentTime)
                    return;
            }

            // Les monstres légendaires n'apparaissent qu'à partir d'un seuil de Renommée
            if (data.isLegendary)
            {
                var cfg    = HotelConfig.Instance;
                float threshold = cfg != null ? cfg.renownLegendaryThreshold : 30f;
                float renown    = HotelStatsManager.Instance?.TotalRenown ?? 0f;
                if (renown < threshold) return;
            }

            int pending = ReservationSystem.Instance.PendingCountOfType(data);
            if (pending >= data.maxPending)
            {
                return;
            }
        }

        Vector3 spawnRot = spawnPoint != null ? spawnPoint.eulerAngles : Vector3.zero;
        Vector3 pos = spawnPoint != null
            ? spawnPoint.position + Quaternion.Euler(spawnRot) * data.spawnOffset
            : data.spawnOffset;

        GameObject go;
        if (data.prefab != null)
            go = Instantiate(data.prefab, pos, Quaternion.identity);
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
        mover.moveSpeed = data.moveSpeed;

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
        var seeker = go.GetComponent<MonsterNeedSeeker>() ?? go.AddComponent<MonsterNeedSeeker>();
        if (go.GetComponent<MonsterRoamBehavior>() == null)
            go.AddComponent<MonsterRoamBehavior>();
        if (go.GetComponent<MonsterDebugTools>() == null)
            go.AddComponent<MonsterDebugTools>();

        // ── Visiteur repas (G6, Phase 1) ──────────────────────────
        // Tirage aléatoire (ou forceVisitor imposé en debug) : un monstre avec au moins un
        // besoin peut arriver juste pour le satisfaire (ex: manger) sans réserver de chambre —
        // ne rejoint pas la file de réception, va directement chercher à satisfaire son besoin
        // puis repart.
        var visitorCfg = HotelConfig.Instance;
        bool canBeVisitor = data.needs != null && data.needs.Length > 0;
        float visitorChance = visitorCfg != null ? visitorCfg.mealVisitorChance : 0f;
        bool becomesVisitor = forceVisitor.HasValue ? (forceVisitor.Value && canBeVisitor) : (canBeVisitor && Random.value < visitorChance);
        if (becomesVisitor)
        {
            // Arrive déjà avec le besoin bas — va chercher à le satisfaire dès la 1ère vérification
            foreach (var needType in data.needs)
                if (needType != null) needs.SetNeedLevel(needType, 0.15f);

            needs.ActivateDecay();
            float maxDuration = visitorCfg != null ? visitorCfg.mealVisitorMaxDuration : 90f;
            seeker.ActivateAsVisitor(maxDuration);

            // Marqueur visuel (G6, Phase 2) — distingue le visiteur d'un client chambre
            if (go.GetComponent<MealVisitorBadge>() == null)
                go.AddComponent<MealVisitorBadge>();

            Debug.Log($"[Spawn] {data.monsterName} arrive en visiteur repas (pas de chambre).");
            return;
        }

        ReservationSystem.Instance.RegisterArrival(data, go, spawnPoint);
    }

    float IntervalFor(MonsterData data)
    {
        float baseInterval = data.spawnInterval > 0f ? data.spawnInterval : _defaultSpawnInterval;

        // La Renommée réduit progressivement l'intervalle de spawn
        var cfg = HotelConfig.Instance;
        if (cfg != null)
        {
            float renown = HotelStatsManager.Instance?.TotalRenown ?? 0f;
            if (renown >= cfg.renownSpawnSpeedupThreshold && cfg.renownMaxSpawnReduction > 0f)
            {
                float t         = Mathf.Clamp01((renown - cfg.renownSpawnSpeedupThreshold) / cfg.renownSpawnSpeedupThreshold);
                float reduction = t * cfg.renownMaxSpawnReduction;
                baseInterval   *= (1f - reduction);
            }
        }

        return Mathf.Max(1f, baseInterval);
    }

    // ─── API ──────────────────────────────────────────────────────

    /// <summary>Force le spawn d'un monstre aléatoire immédiatement (debug).</summary>
    [ContextMenu("Spawner un monstre maintenant")]
    public void ForceSpawn()
    {
        if (monsterPool == null || monsterPool.Length == 0) return;
        TrySpawn(monsterPool[Random.Range(0, monsterPool.Length)]);
    }

    /// <summary>
    /// DEBUG : force le spawn d'un monstre qui réservera directement une chambre — ignore la
    /// fenêtre horaire, le seuil légendaire et les plafonds, pour tester sans attendre.
    /// </summary>
    [ContextMenu("Debug : Spawner un client chambre")]
    public void ForceSpawnRoomGuest()
    {
        if (monsterPool == null || monsterPool.Length == 0) return;
        TrySpawn(monsterPool[Random.Range(0, monsterPool.Length)], forceVisitor: false, bypassGates: true);
    }

    /// <summary>
    /// DEBUG : force le spawn d'un visiteur repas (va directement au restaurant, pas de
    /// chambre) — ignore la fenêtre horaire, le seuil légendaire et les plafonds. Choisit un
    /// monstre du pool ayant au moins un besoin défini (sinon rien à satisfaire au restaurant).
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
        TrySpawn(candidate, forceVisitor: true, bypassGates: true);
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
        TrySpawn(debugMonsterToSpawn, forceVisitor: false, bypassGates: true);
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
        TrySpawn(debugMonsterToSpawn, forceVisitor: true, bypassGates: true);
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
