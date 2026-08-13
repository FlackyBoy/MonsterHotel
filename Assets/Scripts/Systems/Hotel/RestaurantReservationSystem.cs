using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gère la file d'attente des monstres qui veulent manger au restaurant — même principe que
/// ReservationSystem (hôtel) : arrivée → file physique (ReceptionQueueManager du comptoir posé) →
/// validation par le réceptionniste → n'accepte que si une place est synchroniquement disponible
/// à cet instant. Élimine l'ancien système d'attente ad-hoc devant la cuisine (plus de limbo "en
/// attente mais sans place" — même leçon que la réception hôtel).
///
/// Le comptoir (RestaurantReceptionDesk) est un MEUBLE posable dans la cuisine comme n'importe
/// quel autre (FurniturePlacer) — ce système ne référence donc jamais un ReceptionQueueManager
/// fixe, il retrouve dynamiquement le comptoir actuellement posé via RestaurantReceptionDesk.All.
/// </summary>
public class RestaurantReservationSystem : MonoBehaviour
{
    public static RestaurantReservationSystem Instance { get; private set; }

    public class PendingVisitor
    {
        public GameObject Monster;
        public float      TimeRemaining;
        public bool       IsCheckedIn;
        public bool       HasArrived;
        public int        QueueSlotIndex;
        /// <summary>True tant qu'un réceptionniste s'est déjà engagé à traiter ce monstre — évite
        /// que deux réceptionnistes convergent sur le même (voir ReceptionEmployeeAI).</summary>
        public bool        IsClaimed;
        public float      ArrivalTime;

        public float WaitedSeconds => Time.time - ArrivalTime;

        public PendingVisitor(GameObject go, float maxWaitTime)
        {
            Monster        = go;
            TimeRemaining  = maxWaitTime;
            QueueSlotIndex = -1;
            ArrivalTime    = Time.time;
        }
    }

    [Header("Réception restaurant")]
    [Tooltip("Durée max d'attente en file avant abandon (secondes)")]
    public float maxWaitTime = 60f;

    // ─── Privé ────────────────────────────────────────────────────

    readonly List<PendingVisitor> _pending = new();

    // ─── API publique ─────────────────────────────────────────────

    public int PendingCount => _pending.Count;

    /// <summary>Prochain monstre à prendre en charge : le premier arrivé, physiquement présent.</summary>
    public PendingVisitor NextUnchecked
    {
        get
        {
            foreach (var g in _pending)
                if (g.Monster != null && !g.IsCheckedIn && g.HasArrived) return g;
            return null;
        }
    }

    /// <summary>Comme NextUnchecked, mais ignore ceux déjà réclamés par un autre réceptionniste —
    /// voir ReservationSystem.NextUnclaimed pour la même logique côté hôtel.</summary>
    public PendingVisitor NextUnclaimed
    {
        get
        {
            foreach (var g in _pending)
                if (g.Monster != null && !g.IsCheckedIn && g.HasArrived && !g.IsClaimed) return g;
            return null;
        }
    }

    /// <summary>
    /// Comme NextUnclaimed, mais ignore en plus ceux pour qui aucune place n'est disponible
    /// actuellement — voir ReservationSystem.NextServiceableUnclaimed pour la même logique côté
    /// hôtel. Évite qu'un réceptionniste s'engage à marcher jusqu'au comptoir resto pour un
    /// monstre qu'il ne pourra pas asseoir tout de suite, alors que l'hôtel a peut-être quelqu'un
    /// à accueillir.
    /// </summary>
    public PendingVisitor NextServiceableUnclaimed
    {
        get
        {
            foreach (var g in _pending)
                if (g.Monster != null && !g.IsCheckedIn && g.HasArrived && !g.IsClaimed &&
                    FacilityRoomInstance.FindNearestFreeSpot(g.Monster.transform.position) != null)
                    return g;
            return null;
        }
    }

    public IReadOnlyList<PendingVisitor> Pending => _pending;

    /// <summary>Déclenché quand la file change (arrivée, prise en charge, départ).</summary>
    public event System.Action OnQueueChanged;

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        if (_pending.Count == 0) return;

        float dt = Time.deltaTime;

        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            // Le monstre peut disparaître sans passer par ce système (ex : timeout de sécurité du
            // visiteur — MonsterNeedSeeker.VisitTimeoutRoutine — qui le détruit directement même
            // s'il est encore en file ici) : nettoie la référence morte plutôt que de la laisser
            // planter les prochains accès (ex : NextServiceableUnclaimed touchant .transform).
            if (_pending[i].Monster == null)
            {
                ActiveDesk()?.queueManager?.ReleaseSlot(_pending[i].QueueSlotIndex);
                _pending.RemoveAt(i);
                OnQueueChanged?.Invoke();
                continue;
            }

            _pending[i].TimeRemaining -= dt;

            if (_pending[i].TimeRemaining <= 0f)
                RemoveVisitor(i);
        }
    }

    /// <summary>
    /// Appelé par RestaurantReceptionDesk (le meuble comptoir) sur son propre ReceptionQueueManager
    /// à sa création/destruction — pas un abonnement fixe puisque le comptoir peut être posé,
    /// déplacé ou retiré comme n'importe quel meuble.
    /// </summary>
    public void HandleSlotIndexChanged(GameObject monster, int newIndex)
    {
        foreach (var g in _pending)
        {
            if (g.Monster == monster)
            {
                g.QueueSlotIndex = newIndex;
                return;
            }
        }
    }

    // ─── Arrivée ──────────────────────────────────────────────────

    /// <summary>
    /// Enregistre l'arrivée d'un monstre affamé — file d'attente devant la réception du
    /// restaurant. Appelé par MonsterNeedSeeker à la place d'un accès direct à la cuisine.
    /// Retourne false si aucun comptoir n'existe encore (rien n'est enregistré dans ce cas —
    /// l'appelant doit réessayer plus tard, plutôt que de laisser le monstre bloqué pour toujours
    /// avec un besoin marqué "en cours" qui ne se relance jamais).
    /// </summary>
    public bool RegisterArrival(GameObject monster)
    {
        if (monster == null) return false;

        var desk = ActiveDesk();
        if (desk == null)
        {
            Debug.LogWarning($"[Restaurant] {monster.name} veut manger mais aucun RestaurantReceptionDesk trouvé (meuble comptoir pas encore posé dans la cuisine) — retentera plus tard.");
            return false;
        }

        var guest = new PendingVisitor(monster, maxWaitTime);
        _pending.Add(guest);
        OnQueueChanged?.Invoke();

        var mover = monster.GetComponent<MonsterMover>() ?? monster.AddComponent<MonsterMover>();

        if (desk.queueManager != null &&
            desk.queueManager.RequestSlot(out int slotIndex, out Vector3 slotPos, monster))
        {
            guest.QueueSlotIndex = slotIndex;
            mover.OnArrived += () => guest.HasArrived = true;
            mover.MoveTo(slotPos);
            return true;
        }

        // Pas de ReceptionQueueManager sur le comptoir, ou file pleine : à défaut de slot, le
        // monstre marche au moins vers le comptoir lui-même, plutôt que de rester figé pour toujours.
        Debug.LogWarning($"[Restaurant] {monster.name} — aucun slot disponible sur '{desk.name}' (queueManager={(desk.queueManager != null ? "présent" : "absent")}), marche directement vers le comptoir.");
        guest.HasArrived = true;
        mover.MoveTo(desk.StandPoint);
        return true;
    }

    /// <summary>
    /// Prend en charge le monstre en tête de file (appelé par ReceptionEmployeeAI).
    /// N'accepte que si une place est disponible tout de suite — sinon le monstre reste
    /// en tête de file, en attente, sans jamais bloquer ou dupliquer sa position.
    /// </summary>
    public void CheckInNext(PendingVisitor specific = null)
    {
        var guest = specific ?? NextUnchecked;
        if (guest == null) return;
        if (guest.IsCheckedIn) return; // déjà traité entre-temps (ex : joueur a validé avant l'employé)
        if (guest.Monster == null) return; // disparu entre-temps (ex : timeout de sécurité du visiteur) — Update() le nettoiera

        var spot = FacilityRoomInstance.FindNearestFreeSpot(guest.Monster.transform.position);
        if (spot == null) return; // aucune place libre — reste en tête de file

        guest.IsCheckedIn = true;
        ActiveDesk()?.queueManager?.ReleaseSlot(guest.QueueSlotIndex);
        guest.QueueSlotIndex = -1;

        _pending.Remove(guest);
        OnQueueChanged?.Invoke();

        // Une place libre existait à l'instant du check — exécution synchrone, rien n'a pu
        // changer entre-temps, donc l'assignation réussit toujours ici.
        spot.TrySit(guest.Monster);
    }

    // ─── Départ (abandon) ───────────────────────────────────────────

    void RemoveVisitor(int index)
    {
        var guest = _pending[index];

        ActiveDesk()?.queueManager?.ReleaseSlot(guest.QueueSlotIndex);
        _pending.RemoveAt(index);
        OnQueueChanged?.Invoke();

        if (guest.Monster == null) return;

        // Client chambre : pénalité de satisfaction + retour en chambre (garde sa réservation).
        // Visiteur sans chambre : quitte l'hôtel avec une note d'insatisfaction.
        guest.Monster.GetComponent<MonsterNeedSeeker>()?.GiveUpMeal("jamais accueilli au restaurant à temps");
    }

    // ─── Helpers ──────────────────────────────────────────────────

    static RestaurantReceptionDesk ActiveDesk()
    {
        foreach (var d in RestaurantReceptionDesk.All) return d;
        return null;
    }

    // ─── Debug ────────────────────────────────────────────────────

    /// <summary>DEBUG : force le départ immédiat d'un monstre encore en file d'attente resto.</summary>
    public bool DebugForceLeave(GameObject monster)
    {
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            if (_pending[i].Monster == monster)
            {
                RemoveVisitor(i);
                return true;
            }
        }
        return false;
    }
}
