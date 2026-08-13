using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Salle commune (facilité) indépendante du système de chambres.
/// Place ce composant sur le prefab de la pièce (cuisine, salle de bains, etc.).
/// Mode auto        : remplit le besoin progressivement dès que le monstre arrive.
/// Mode playerServiceOnly : les monstres cherchent une chaise, le joueur/cuisinier apporte les plats.
///   L'accès à ce mode passe désormais par RestaurantReservationSystem (réception dédiée) —
///   un monstre n'est envoyé ici que lorsqu'une place est déjà garantie, plus de file d'attente
///   ad-hoc devant la porte (ancien système retiré, voir CHANGELOG).
/// </summary>
public class FacilityRoomInstance : MonoBehaviour
{
    public static readonly HashSet<FacilityRoomInstance> All = new();

    [Header("Configuration")]
    [Tooltip("Si coché, cette salle est la salle de repos des employés (pauses)")]
    public bool isBreakRoom = false;
    [Tooltip("Besoin satisfait par cette facilité")]
    public NeedType needFulfilled;

    [Tooltip("Capacité max de la file (mode auto uniquement)")]
    public int capacity = 4;

    [Header("Mode joueur (playerServiceOnly)")]
    [Tooltip("Si coché : les monstres cherchent une chaise, le joueur apporte les plats. Accès géré par RestaurantReservationSystem.")]
    public bool playerServiceOnly;

    [Header("Mode auto uniquement")]
    public float serviceTime    = 10f;
    [Range(0f, 1f)] public float fillAmount     = 1f;
    [Range(0f, 1f)] public float serviceQuality = 0.7f;

    // ─── État ──────────────────────────────────────────────────────

    readonly Queue<ServiceRequest> _autoQueue = new();
    int _activeSlots;

    public bool HasRoom => playerServiceOnly
        ? true // capacité gérée par la file de RestaurantReservationSystem, pas ici
        : (_autoQueue.Count + _activeSlots) < capacity * 2;

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

    // ─── API publique ──────────────────────────────────────────────

    /// <summary>
    /// Mode auto uniquement. Pour playerServiceOnly, passer par
    /// RestaurantReservationSystem.RegisterArrival() à la place (réception dédiée).
    /// </summary>
    public void RequestService(GameObject monster, float waitStartTime)
    {
        if (playerServiceOnly)
        {
            Debug.LogWarning($"[Cuisine] RequestService() appelé directement sur une facilité " +
                              "playerServiceOnly — devrait passer par RestaurantReservationSystem.RegisterArrival().");
            return;
        }

        _autoQueue.Enqueue(new ServiceRequest(monster, waitStartTime));
        TryStartNextService();
    }

    // ─── Mode auto ─────────────────────────────────────────────────

    void TryStartNextService()
    {
        if (_activeSlots >= capacity || _autoQueue.Count == 0 || playerServiceOnly) return;

        var request = _autoQueue.Dequeue();
        if (request.Monster == null) { TryStartNextService(); return; }

        _activeSlots++;
        StartCoroutine(ServeRoutine(request));
    }

    IEnumerator ServeRoutine(ServiceRequest request)
    {
        var monster = request.Monster;
        var mover   = monster?.GetComponent<MonsterMover>();

        if (mover != null)
        {
            mover.MoveTo(transform.position);
            yield return new WaitUntil(() =>
                monster == null ||
                Vector3.Distance(monster.transform.position, transform.position) < 1.5f);
        }

        if (monster == null) { FinishSlot(); yield break; }

        var needs = monster.GetComponent<MonsterNeedsComponent>();
        if (needs == null || needFulfilled == null) { FinishSlot(); yield break; }

        float fillRate = fillAmount / Mathf.Max(serviceTime, 0.1f);
        float elapsed  = 0f;

        while (elapsed < serviceTime * 3f)
        {
            if (monster == null) break;
            var state = needs.GetNeedState(needFulfilled);
            if (state == null) break;
            state.level = Mathf.Clamp01(state.level + fillRate * Time.deltaTime);
            elapsed    += Time.deltaTime;
            if (state.level >= 1f || needs.HasOtherCriticalNeed(needFulfilled)) break;
            yield return null;
        }

        if (monster != null)
        {
            needs.FulfillNeed(needFulfilled, 0f, Time.time - request.WaitStartTime, serviceQuality);
            monster.GetComponent<MonsterNeedSeeker>()?.OnServiceComplete();
        }

        FinishSlot();
    }

    void FinishSlot()
    {
        _activeSlots = Mathf.Max(0, _activeSlots - 1);
        TryStartNextService();
    }

    // ─── Helpers ───────────────────────────────────────────────────

    /// <summary>Cherche la place assise libre la plus proche, tous EatingSpot confondus.</summary>
    public static EatingSpot FindNearestFreeSpot(Vector3 from)
    {
        EatingSpot best     = null;
        float      bestDist = float.MaxValue;
        foreach (var spot in EatingSpot.All)
        {
            if (spot.IsOccupied) continue;
            float dist = Vector3.Distance(from, spot.transform.position);
            if (dist < bestDist) { best = spot; bestDist = dist; }
        }
        return best;
    }

    class ServiceRequest
    {
        public GameObject Monster;
        public float      WaitStartTime;
        public ServiceRequest(GameObject m, float t) { Monster = m; WaitStartTime = t; }
    }
}
