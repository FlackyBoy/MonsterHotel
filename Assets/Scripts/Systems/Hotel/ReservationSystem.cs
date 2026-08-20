using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gère la file d'attente des monstres arrivés à l'hôtel.
/// Attache-le sur le GameObject ReservationSystem dans _Managers.
/// </summary>
public class ReservationSystem : MonoBehaviour
{
    public static ReservationSystem Instance { get; private set; }

    [Header("Réception")]
    [Tooltip("ReceptionQueueManager de la file d'attente devant le comptoir de l'hôtel")]
    public ReceptionQueueManager receptionQueue;

    // ─── Données ──────────────────────────────────────────────────

    public class PendingGuest
    {
        public MonsterData Data;
        public GameObject  MonsterObject;
        public float       TimeRemaining;
        /// <summary>True après que le joueur a interagi au comptoir.</summary>
        public bool        IsCheckedIn;
        /// <summary>True quand le monstre a physiquement atteint son slot de file.</summary>
        public bool        HasArrived;
        /// <summary>Slot dans la ReceptionQueue (-1 si non assigné).</summary>
        public int         QueueSlotIndex;
        /// <summary>True tant qu'un réceptionniste s'est déjà engagé à traiter ce monstre — évite
        /// que deux réceptionnistes convergent sur le même (voir ReceptionEmployeeAI).</summary>
        public bool        IsClaimed;
        /// <summary>Point de spawn du monstre (pour le retour en cas de timeout).</summary>
        public Transform   SpawnPoint;
        /// <summary>Temps (Time.time) auquel le monstre est arrivé à la réception.</summary>
        public float       ArrivalTime;

        public float WaitedSeconds => Time.time - ArrivalTime;

        public PendingGuest(MonsterData data, GameObject go, Transform spawnPoint)
        {
            Data           = data;
            MonsterObject  = go;
            TimeRemaining  = data.maxWaitTime;
            IsCheckedIn    = false;
            QueueSlotIndex = -1;
            SpawnPoint     = spawnPoint;
            ArrivalTime    = Time.time;
        }
    }

    // ─── Privé ────────────────────────────────────────────────────

    readonly List<PendingGuest> _pending = new();

    // ─── API publique ─────────────────────────────────────────────

    /// <summary>Nombre de monstres en attente (toutes phases confondues).</summary>
    public int PendingCount => _pending.Count;

    /// <summary>Nombre de monstres d'un type donné encore en attente.</summary>
    public int PendingCountOfType(MonsterData data)
    {
        int count = 0;
        foreach (var g in _pending)
            if (g.Data == data) count++;
        return count;
    }

    /// <summary>
    /// Prochain monstre à prendre en charge : le premier arrivé (ordre FIFO d'insertion dans _pending),
    /// parmi ceux physiquement arrivés à la réception.
    /// </summary>
    public PendingGuest NextUnchecked
    {
        get
        {
            foreach (var g in _pending)
                if (g.MonsterObject != null && !g.IsCheckedIn && g.HasArrived) return g;
            return null;
        }
    }

    /// <summary>
    /// Comme NextUnchecked, mais ignore ceux déjà réclamés par un autre réceptionniste — utilisé
    /// par ReceptionEmployeeAI pour choisir une tâche sans faire converger plusieurs employés sur
    /// le même monstre. Le joueur (ReceptionInteractor) continue d'utiliser NextUnchecked (il peut
    /// toujours accueillir lui-même un monstre déjà "réclamé" par un employé en chemin).
    /// </summary>
    public PendingGuest NextUnclaimed
    {
        get
        {
            foreach (var g in _pending)
                if (g.MonsterObject != null && !g.IsCheckedIn && g.HasArrived && !g.IsClaimed) return g;
            return null;
        }
    }

    /// <summary>
    /// Comme NextUnclaimed, mais ignore en plus ceux pour qui aucune chambre compatible n'est
    /// disponible actuellement — utilisé par ReceptionEmployeeAI pour choisir une tâche : inutile
    /// de s'engager à marcher jusqu'au comptoir hôtel pour un monstre qu'on ne pourra de toute
    /// façon pas accepter tout de suite, alors que le restaurant a peut-être quelqu'un à servir.
    /// </summary>
    public PendingGuest NextServiceableUnclaimed
    {
        get
        {
            foreach (var g in _pending)
                if (g.MonsterObject != null && !g.IsCheckedIn && g.HasArrived && !g.IsClaimed &&
                    GetCompatibleRooms(g.Data).Count > 0)
                    return g;
            return null;
        }
    }

    /// <summary>
    /// Comme NextUnchecked, mais saute ceux pour qui aucune chambre compatible n'est disponible —
    /// utilisé par ReceptionInteractor (joueur) pour ne pas rester bloqué sur le 1er de la file si
    /// un autre pourrait être accepté immédiatement (voir TODO G6-B24). Contrairement à
    /// NextServiceableUnclaimed (employé), ignore volontairement IsClaimed : le joueur peut
    /// toujours accueillir lui-même un monstre déjà réclamé par un employé en chemin.
    /// </summary>
    public PendingGuest NextServiceableUnchecked
    {
        get
        {
            foreach (var g in _pending)
                if (g.MonsterObject != null && !g.IsCheckedIn && g.HasArrived &&
                    GetCompatibleRooms(g.Data).Count > 0)
                    return g;
            return null;
        }
    }

    /// <summary>Prochain monstre checked-in mais sans chambre assignée.</summary>
    public PendingGuest NextCheckedInWaiting
    {
        get { foreach (var g in _pending) if (g.IsCheckedIn) return g; return null; }
    }

    /// <summary>Lecture seule de la file (pour l'UI).</summary>
    public IReadOnlyList<PendingGuest> Pending => _pending;

    /// <summary>Déclenché quand la file change (arrivée, attribution, départ).</summary>
    public event System.Action OnQueueChanged;

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (receptionQueue != null)
            receptionQueue.OnSlotIndexChanged += HandleSlotIndexChanged;
    }

    void OnDisable()
    {
        if (receptionQueue != null)
            receptionQueue.OnSlotIndexChanged -= HandleSlotIndexChanged;
    }

    void HandleSlotIndexChanged(GameObject monster, int newIndex)
    {
        foreach (var guest in _pending)
        {
            if (guest.MonsterObject == monster)
            {
                guest.QueueSlotIndex = newIndex;
                return;
            }
        }
    }

    void Update()
    {
        if (_pending.Count == 0) return;

        float dt = Time.deltaTime;

        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            // Le monstre peut disparaître sans passer par ce système — nettoie la référence morte
            // plutôt que de la laisser planter un accès ultérieur (ex : NextServiceableUnclaimed).
            if (_pending[i].MonsterObject == null)
            {
                receptionQueue?.ReleaseSlot(_pending[i].QueueSlotIndex);
                _pending.RemoveAt(i);
                OnQueueChanged?.Invoke();
                continue;
            }

            _pending[i].TimeRemaining -= dt;

            if (_pending[i].TimeRemaining <= 0f)
                RemoveGuest(i, angry: true);
        }
    }

    // ─── Arrivée ──────────────────────────────────────────────────

    /// <summary>
    /// Enregistre l'arrivée d'un monstre, lui assigne un slot de file
    /// et le fait naviguer vers ce slot.
    /// </summary>
    public void RegisterArrival(MonsterData data, GameObject monsterObject, Transform spawnPoint = null)
    {
        var guest = new PendingGuest(data, monsterObject, spawnPoint);
        _pending.Add(guest);
        OnQueueChanged?.Invoke();

        if (monsterObject == null) { guest.HasArrived = true; return; }

        var mover = monsterObject.GetComponent<MonsterMover>()
                 ?? monsterObject.AddComponent<MonsterMover>();
        var qm = receptionQueue;

        if (qm != null && qm.RequestSlot(out int slotIndex, out Vector3 slotPos, monsterObject))
        {
            guest.QueueSlotIndex = slotIndex;
            mover.OnArrived += () => guest.HasArrived = true;
            mover.MoveTo(slotPos);
            return;
        }

        // Pas de gestionnaire de file (comptoir pas encore construit) ou file pleine :
        // à défaut de slot, le monstre marche au moins vers le comptoir le plus proche
        // s'il en existe un, plutôt que de rester figé à son point de spawn pour toujours.
        guest.HasArrived = true;
        ReceptionDesk desk = null;
        foreach (var d in ReceptionDesk.All) { desk = d; break; }
        if (desk != null)
            mover.MoveTo(desk.StandPoint);
    }

    /// <summary>
    /// Prend en charge le monstre en tête de file (appelé par ReceptionInteractor ou ReceptionEmployeeAI).
    /// N'accepte que si une chambre compatible est déjà disponible — sinon le monstre reste
    /// en tête de file, en attente, sans jamais bloquer ou dupliquer sa position.
    /// </summary>
    public void CheckInNext(PendingGuest specific = null)
    {
        var guest = specific ?? NextUnchecked;
        if (guest == null) return;
        if (guest.IsCheckedIn) return; // déjà traité entre-temps (ex : joueur a validé avant l'employé)

        // La condition d'acceptation inclut la disponibilité d'une chambre.
        // Tant qu'aucune chambre compatible n'existe, le monstre continue d'attendre en tête de file.
        if (GetCompatibleRooms(guest.Data).Count == 0)
        {
            LogNoCompatibleRoom(guest.Data);
            return;
        }

        guest.IsCheckedIn = true;

        // Libère le slot physique — le monstre est accepté, la file avance derrière lui.
        receptionQueue?.ReleaseSlot(guest.QueueSlotIndex);
        guest.QueueSlotIndex = -1;

        // Impact satisfaction selon le temps d'attente
        var satisfaction = guest.MonsterObject?.GetComponent<SatisfactionComponent>();
        if (satisfaction != null)
        {
            var cfg = HotelConfig.Reception;
            float goodWait  = cfg != null ? cfg.receptionGoodWaitTime : 20f;
            float bonus     = cfg != null ? cfg.receptionWaitBonus    : 15f;
            float penalty   = cfg != null ? cfg.receptionWaitPenalty  : 10f;

            float waited = guest.WaitedSeconds;
            if (waited <= goodWait)
                satisfaction.ApplyBonus(bonus);
            else
                satisfaction.ApplyDecay(penalty);
        }

        OnQueueChanged?.Invoke();

        // Une chambre compatible existait à l'instant du check — rien n'a pu changer
        // entre-temps (exécution synchrone) donc l'assignation réussit toujours ici.
        TryAssignRoom(guest);
    }

    // ─── Attribution ──────────────────────────────────────────────

    /// <summary>
    /// Cherche une chambre compatible libre et l'attribue au guest.
    /// Retourne true si une chambre a été trouvée.
    /// </summary>
    public bool TryAssignRoom(PendingGuest guest)
    {
        var rooms = GetCompatibleRooms(guest.Data);
        if (rooms.Count == 0) return false;

        var room     = rooms[0];
        bool assigned = room.Assign(guest.Data, guest.MonsterObject);
        if (!assigned) return false;

        // Libère le slot de file
        receptionQueue?.ReleaseSlot(guest.QueueSlotIndex);

        // Déplace le monstre vers la chambre
        if (guest.MonsterObject != null)
        {
            var mover = guest.MonsterObject.GetComponent<MonsterMover>()
                     ?? guest.MonsterObject.AddComponent<MonsterMover>();
            mover.MoveTo(room.EntryPoint, room.transform.position);
        }

        // Retire de la file
        int idx = _pending.IndexOf(guest);
        if (idx >= 0)
        {
            _pending.RemoveAt(idx);
            OnQueueChanged?.Invoke();
        }

        int   nights         = Random.Range(guest.Data.stayMinNights, guest.Data.stayMaxNights + 1);
        float dayDuration    = TimeManager.Instance != null ? TimeManager.Instance.dayDuration : 300f;
        float actualDuration = nights * dayDuration;

        // Référence chambre + activation de la recherche de besoins
        if (guest.MonsterObject != null)
        {
            var roomRef = guest.MonsterObject.GetComponent<GuestRoomReference>()
                       ?? guest.MonsterObject.AddComponent<GuestRoomReference>();
            roomRef.Room = room;
            roomRef.StartStayTimer(actualDuration);
            guest.MonsterObject.GetComponent<MonsterNeedsComponent>()?.ActivateDecay();
            guest.MonsterObject.GetComponent<MonsterNeedSeeker>()?.Activate();
            guest.MonsterObject.GetComponent<MonsterRoamBehavior>()?.Activate();
            guest.MonsterObject.GetComponent<MonsterSocialBehavior>()?.Activate();
            guest.MonsterObject.GetComponent<MonsterFightBehavior>()?.Activate();
        }

        guest.MonsterObject?.GetComponent<GuestBubble>()?.Hide();
        int stayRevenue = guest.Data.revenuePerNight * nights;
        EconomyManager.Instance?.Earn(stayRevenue);
        Debug.Log($"[Paiement] {guest.Data.monsterName} check-in chambre '{room.Data?.roomName}' — " +
                   $"{nights} nuit(s) × {guest.Data.revenuePerNight}G = +{stayRevenue}G (solde: {EconomyManager.Instance?.Gold}G)");

        StartCoroutine(CheckoutAfter(room, actualDuration, guest.Data.checkoutWindowStart, guest.Data.checkoutWindowEnd));

        // Départ anticipé si satisfaction tombe sous leaveThreshold
        var satisfaction = guest.MonsterObject?.GetComponent<SatisfactionComponent>();
        if (satisfaction != null)
        {
            var capturedMonster = guest.MonsterObject;
            var capturedSpawn   = guest.SpawnPoint;
            satisfaction.OnWantsToLeave += () => CheckoutEarly(room, capturedMonster, capturedSpawn);
        }

        return true;
    }

    /// <summary>Retourne toutes les RoomInstance libres compatibles avec ce monstre.</summary>
    public List<RoomInstance> GetCompatibleRooms(MonsterData data)
    {
        var result = new List<RoomInstance>();
        foreach (var room in RoomInstance.All)
        {
            if (room.Data == null) continue;
            if (room.Data.isFacility) continue;
            if (room.State == RoomState.Empty && room.IsFullyFurnished && data.IsRoomCompatible(room.Data.roomType))
                result.Add(room);
        }
        return result;
    }

    /// <summary>Diagnostic : explique pourquoi aucune chambre compatible n'a été trouvée pour ce
    /// monstre — utile pour distinguer "aucune chambre de ce type" de "chambre existante mais bloquée
    /// dans un état inattendu (sale, pas meublée...)".</summary>
    static void LogNoCompatibleRoom(MonsterData data)
    {
        bool anyOfType = false;
        foreach (var room in RoomInstance.All)
        {
            if (room.Data == null || room.Data.isFacility) continue;
            if (!data.IsRoomCompatible(room.Data.roomType)) continue;
            anyOfType = true;
            Debug.Log($"[Réception] {data.monsterName} — chambre '{room.name}' de type compatible mais indisponible : State={room.State}, IsFullyFurnished={room.IsFullyFurnished}.");
        }
        if (!anyOfType)
            Debug.Log($"[Réception] {data.monsterName} — aucune chambre du bon type dans l'hôtel.");
    }

    // ─── Départ ───────────────────────────────────────────────────

    System.Collections.IEnumerator CheckoutAfter(RoomInstance room, float duration, float windowStart, float windowEnd)
    {
        yield return new WaitForSeconds(duration);

        if (room == null || room.State != RoomState.Occupied) yield break;

        yield return WaitForCheckoutWindow(windowStart, windowEnd);

        if (room == null || room.State != RoomState.Occupied) yield break;

        CheckoutNow(room);
    }

    /// <summary>
    /// Effectue le départ immédiat du monstre (départ naturel ou anticipé).
    /// Capture la satisfaction finale et verse le pourboire.
    /// </summary>
    void CheckoutNow(RoomInstance room)
    {
        if (room == null || room.State != RoomState.Occupied) return;

        var monsterObj  = room.CurrentMonsterObject;
        var monsterData = room.CurrentMonster;

        // ── Satisfaction finale ──
        float satisfaction = monsterObj?.GetComponent<SatisfactionComponent>()?.Value ?? 50f;

        // ── Pourboire ──
        if (monsterData != null)
        {
            var cfg = HotelConfig.Economy;
            float tipGoodThreshold   = cfg != null ? cfg.tipGoodThreshold    : 70f;
            float tipNormalThreshold = cfg != null ? cfg.tipNormalThreshold   : 40f;
            float tipGoodMultiplier  = cfg != null ? cfg.tipGoodMultiplier    : 0.3f;
            float tipNormalMultiplier= cfg != null ? cfg.tipNormalMultiplier  : 0.1f;

            int tip = 0;
            if (satisfaction >= tipGoodThreshold)
                tip = Mathf.RoundToInt(monsterData.revenuePerNight * tipGoodMultiplier);
            else if (satisfaction >= tipNormalThreshold)
                tip = Mathf.RoundToInt(monsterData.revenuePerNight * tipNormalMultiplier);

            if (tip > 0)
                EconomyManager.Instance?.Earn(tip);

            Debug.Log($"[Paiement] {monsterData.monsterName} check-out — satisfaction {satisfaction:F0}/100 → " +
                       $"pourboire +{tip}G (solde: {EconomyManager.Instance?.Gold}G)");

            // Canal chambre, toujours "servi" : occuper la chambre EST le service (contrairement au
            // visiteur resto qui peut repartir sans avoir été servi — voir MonsterNeedSeeker).
            HotelStatsManager.Instance?.ReportGuestDeparture(monsterData.monsterType, satisfaction, angry: false, GuestChannel.Room, served: true);
        }

        room.Vacate();

        if (monsterObj != null)
        {
            monsterObj.GetComponent<MonsterSocialBehavior>()?.Interrupt();
            monsterObj.GetComponent<MonsterFightBehavior>()?.Interrupt();
            Destroy(monsterObj);
        }
    }

    /// <summary>
    /// Attend que l'heure soit dans la fenêtre de checkout du monstre.
    /// Choisit une heure aléatoire dans la fenêtre pour simuler des départs échelonnés.
    /// </summary>
    System.Collections.IEnumerator WaitForCheckoutWindow(float windowStart, float windowEnd)
    {
        if (TimeManager.Instance == null) yield break;

        // Choisit une heure de départ aléatoire dans la fenêtre
        float departHour = Random.Range(windowStart, windowEnd);
        bool  waitingForNextDay = false;

        while (true)
        {
            float hour = TimeManager.Instance != null ? TimeManager.Instance.Hour : windowStart;

            // Hors fenêtre — passé la fin : prépare le lendemain
            if (hour >= windowEnd)
            {
                if (!waitingForNextDay)
                {
                    departHour       = Random.Range(windowStart, windowEnd);
                    waitingForNextDay = true;
                }
                yield return new WaitForSeconds(1f);
                continue;
            }

            // Hors fenêtre — avant le début
            if (hour < windowStart)
            {
                waitingForNextDay = false;
                yield return new WaitForSeconds(1f);
                continue;
            }

            // Dans la fenêtre
            waitingForNextDay = false;
            if (hour >= departHour)
                yield break;

            yield return new WaitForSeconds(0.5f);
        }
    }

    /// <summary>
    /// Départ anticipé par insatisfaction : libère la chambre et fait marcher le monstre vers la sortie.
    /// </summary>
    void CheckoutEarly(RoomInstance room, GameObject monsterObj, Transform spawnPoint)
    {
        if (room == null || room.State != RoomState.Occupied) return;

        var monsterData = room.CurrentMonster;
        float satisfaction = monsterObj?.GetComponent<SatisfactionComponent>()?.Value ?? 0f;

        Debug.Log($"[Paiement] {monsterData?.monsterName} départ anticipé (insatisfaction) — " +
                   $"satisfaction {satisfaction:F0}/100 → pas de pourboire (solde: {EconomyManager.Instance?.Gold}G)");

        if (monsterData != null)
            HotelStatsManager.Instance?.ReportGuestDeparture(monsterData.monsterType, satisfaction, angry: true, GuestChannel.Room, served: true);

        room.Vacate();

        if (monsterObj != null)
        {
            monsterObj.GetComponent<MonsterSocialBehavior>()?.Interrupt();
            monsterObj.GetComponent<MonsterFightBehavior>()?.Interrupt();
            Vector3 exitPos = spawnPoint != null ? spawnPoint.position : monsterObj.transform.position;
            var mover = monsterObj.GetComponent<MonsterMover>() ?? monsterObj.AddComponent<MonsterMover>();
            mover.WalkToAndDestroy(exitPos);
        }
    }

    // ─── Debug ────────────────────────────────────────────────────

    /// <summary>
    /// DEBUG : termine immédiatement le séjour d'un client chambre, comme si sa durée de séjour
    /// était naturellement arrivée à son terme (pourboire calculé sur sa satisfaction actuelle,
    /// contrairement à DebugForceLeave qui traite ça comme un départ en colère sans pourboire).
    /// Retourne false si ce monstre n'occupe pas de chambre.
    /// </summary>
    public bool DebugFinishStay(GameObject monster)
    {
        var roomRef = monster?.GetComponent<GuestRoomReference>();
        if (roomRef == null || roomRef.Room == null || roomRef.Room.State != RoomState.Occupied)
            return false;

        CheckoutNow(roomRef.Room);
        return true;
    }

    /// <summary>
    /// DEBUG : force le départ immédiat d'un monstre, quel que soit son état actuel
    /// (en file d'attente, en chambre, ou ailleurs dans l'hôtel).
    /// </summary>
    public void DebugForceLeave(GameObject monster)
    {
        if (monster == null) return;

        // Cas 1 : encore dans la file d'attente (pas encore de chambre assignée)
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            if (_pending[i].MonsterObject == monster)
            {
                RemoveGuest(i, angry: true);
                return;
            }
        }

        // Cas 2 : occupe une chambre
        var roomRef = monster.GetComponent<GuestRoomReference>();
        if (roomRef != null && roomRef.Room != null)
        {
            var spawn = FindAnyObjectByType<SpawnScheduler>();
            CheckoutEarly(roomRef.Room, monster, spawn != null ? spawn.spawnPoint : null);
            return;
        }

        // Cas 3 : ni en file ni en chambre (ex : en train de manger, en balade) — sortie directe
        monster.GetComponent<MonsterSocialBehavior>()?.Interrupt();
        monster.GetComponent<MonsterFightBehavior>()?.Interrupt();
        var mover = monster.GetComponent<MonsterMover>() ?? monster.AddComponent<MonsterMover>();
        var fallbackSpawn = FindAnyObjectByType<SpawnScheduler>();
        Vector3 exit = fallbackSpawn != null ? fallbackSpawn.spawnPoint.position : monster.transform.position;
        mover.WalkToAndDestroy(exit);
    }

    // ─── Privé ────────────────────────────────────────────────────

    void RemoveGuest(int index, bool angry)
    {
        var guest = _pending[index];

        // Libère le slot de file
        receptionQueue?.ReleaseSlot(guest.QueueSlotIndex);

        _pending.RemoveAt(index);
        OnQueueChanged?.Invoke();

        if (angry && guest.MonsterObject != null)
        {
            Debug.Log($"[Paiement] {guest.Data?.monsterName} quitte la file (attente trop longue) — " +
                       "jamais check-in, aucun revenu généré.");

            // Marche vers le point de spawn puis se détruit
            var mover = guest.MonsterObject.GetComponent<MonsterMover>()
                     ?? guest.MonsterObject.AddComponent<MonsterMover>();
            Vector3 exitPos = guest.SpawnPoint != null
                ? guest.SpawnPoint.position
                : guest.MonsterObject.transform.position;
            mover.WalkToAndDestroy(exitPos);
        }
    }
}
