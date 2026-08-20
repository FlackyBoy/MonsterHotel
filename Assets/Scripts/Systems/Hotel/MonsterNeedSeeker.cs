using System.Collections;
using UnityEngine;

/// <summary>
/// IA de recherche de besoins sur un monstre.
/// Vérifie périodiquement si un besoin est insatisfait,
/// trouve la facilité appropriée et demande le service.
/// Actif uniquement après que le monstre a été assigné à une chambre.
/// </summary>
public class MonsterNeedSeeker : MonoBehaviour
{
    [Header("Comportement")]
    [Tooltip("Intervalle en secondes entre deux vérifications des besoins")]
    public float checkInterval = 5f;

    [Tooltip("Distance max pour chercher une facilité")]
    public float searchRadius = 50f;

    [Header("Conversion visiteur → client (après un repas réussi, G6 Phase 4)")]
    [Tooltip("Chance qu'un visiteur repas décide de réserver une chambre après avoir mangé (0 = jamais). Valeur de départ — à toi de l'ajuster.")]
    [Range(0f, 1f)] public float stayAfterMealChance = 0.25f;

    [Header("Abandon repas (file resto pleine trop longtemps, ou jamais servi à table)")]
    [Tooltip("Pénalité de satisfaction pour un client chambre qui abandonne sans être servi (garde sa réservation, peut réessayer plus tard)")]
    public float giveUpMealPenalty = 10f;

    // ─── État ──────────────────────────────────────────────────────

    MonsterNeedsComponent _needs;
    MonsterMover          _mover;

    bool     _active;
    bool     _seekingNeed;
    float    _waitStartTime;
    bool     _isVisitor;
    Coroutine _visitTimeoutCoroutine;

    public bool IsSeeking => _seekingNeed;

    public float WaitStartTime => _waitStartTime;

    // ─── Lifecycle ─────────────────────────────────────────────────

    void Awake()
    {
        _needs = GetComponent<MonsterNeedsComponent>();
        _mover = GetComponent<MonsterMover>();
    }

    public void Activate()
    {
        if (_active) return; // évite un 2e NeedCheckLoop en parallèle (ex : conversion visiteur→client)
        _active = true;
        StartCoroutine(NeedCheckLoop());
    }

    /// <summary>
    /// Active en mode "visiteur sans chambre" (G6, Phase 1) : pas de retour en chambre après
    /// service — repart directement de l'hôtel. `maxDuration` est un garde-fou : le visiteur
    /// repart de force si jamais servi dans ce délai (0 = pas de limite).
    /// </summary>
    public void ActivateAsVisitor(float maxDuration)
    {
        _isVisitor = true;
        Activate();
        if (maxDuration > 0f)
            _visitTimeoutCoroutine = StartCoroutine(VisitTimeoutRoutine(maxDuration));
    }

    IEnumerator VisitTimeoutRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (this == null) yield break;
        ExitDissatisfied("jamais servi dans le délai imparti");
    }

    /// <summary>Appelé après un repas réussi (à table) ou un retour normal (mode auto).</summary>
    public void OnServiceComplete()
    {
        _seekingNeed = false;
        var room = GetComponent<GuestRoomReference>()?.Room;
        if (room != null && _mover != null)
        {
            Debug.Log($"[Cuisine] {name} — servi, retourne en chambre.");
            _mover.MoveTo(room.EntryPoint, room.transform.position);
        }
        else if (_isVisitor && _mover != null)
        {
            TryStayOrExit();
        }
    }

    /// <summary>
    /// Après un repas réussi, un visiteur sans chambre peut décider de rester et réserver une
    /// chambre (tirage sur stayAfterMealChance) plutôt que de systématiquement repartir.
    /// </summary>
    void TryStayOrExit()
    {
        var dataRef = GetComponent<MonsterDataReference>();

        if (stayAfterMealChance > 0f && dataRef?.Data != null && Random.value < stayAfterMealChance)
        {
            _isVisitor = false;
            if (_visitTimeoutCoroutine != null) { StopCoroutine(_visitTimeoutCoroutine); _visitTimeoutCoroutine = null; }

            // N'est plus un simple visiteur — retire le badge "Repas" pour éviter la confusion
            // visuelle une fois qu'il devient un vrai client en attente de chambre.
            var badge = GetComponent<MealVisitorBadge>();
            if (badge != null) Destroy(badge);

            Debug.Log($"[Restaurant] {name} a apprécié son repas — tente de réserver une chambre.");
            var spawn = SpawnScheduler.Instance;
            ReservationSystem.Instance?.RegisterArrival(dataRef.Data, gameObject, spawn != null ? spawn.spawnPoint : null);
        }
        else
        {
            Debug.Log($"[Cuisine] {name} — visiteur servi, repart de l'hôtel.");
            ReportDeparture(angry: false);
            ExitHotel();
        }
    }

    void ExitHotel()
    {
        if (_mover == null) return;
        var spawn = SpawnScheduler.Instance;
        Vector3 exit = spawn != null && spawn.spawnPoint != null ? spawn.spawnPoint.position : transform.position;
        _mover.WalkToAndDestroy(exit);
    }

    /// <summary>
    /// Quitte l'hôtel en laissant une note d'insatisfaction — utilisé quand un visiteur sans
    /// chambre abandonne (ex : jamais accueilli au restaurant à temps).
    /// </summary>
    public void ExitDissatisfied(string reason)
    {
        _seekingNeed = false;
        Debug.Log($"[Réputation] {name} quitte l'hôtel insatisfait — {reason}.");
        ReportDeparture(angry: true);
        ExitHotel();
    }

    /// <summary>
    /// G1 : reporte la satisfaction finale de ce visiteur (sans chambre) à son départ réel de
    /// l'hôtel — toujours canal Resto (cette méthode n'est appelée que pour un visiteur sans
    /// chambre, voir TryStayOrExit/ExitDissatisfied). served = !angry : ce chemin n'est atteint
    /// qu'après un repas réussi (angry: false, OnServiceComplete → TryStayOrExit) ou un abandon/délai
    /// dépassé sans avoir été servi (angry: true, ExitDissatisfied) — les deux cas restent
    /// mutuellement exclusifs aujourd'hui, donc angry capture directement le "non servi".
    /// </summary>
    void ReportDeparture(bool angry)
    {
        var type = GetComponent<MonsterDataReference>()?.Data?.monsterType;
        if (type == null) return;
        float satisfaction = GetComponent<SatisfactionComponent>()?.Value ?? 50f;
        HotelStatsManager.Instance?.ReportGuestDeparture(type.Value, satisfaction, angry, GuestChannel.Restaurant, served: !angry);
    }

    /// <summary>
    /// Abandon d'une tentative de repas (file resto pleine trop longtemps, ou jamais servi à
    /// table). Client chambre : pénalité de satisfaction + retour en chambre (garde sa
    /// réservation, peut réessayer plus tard). Visiteur sans chambre : quitte l'hôtel avec une
    /// note d'insatisfaction (voir ExitDissatisfied).
    /// </summary>
    public void GiveUpMeal(string visitorExitReason)
    {
        _seekingNeed = false;
        var room = GetComponent<GuestRoomReference>()?.Room;
        if (room != null && _mover != null)
        {
            GetComponent<SatisfactionComponent>()?.ApplyDecay(giveUpMealPenalty);
            Debug.Log($"[Cuisine] {name} — pas servi à temps, retourne en chambre déçu (-{giveUpMealPenalty} satisfaction).");
            _mover.MoveTo(room.EntryPoint, room.transform.position);
        }
        else if (_mover != null)
        {
            ExitDissatisfied(visitorExitReason);
        }
    }

    // ─── Boucle ────────────────────────────────────────────────────

    IEnumerator NeedCheckLoop()
    {
        while (_active)
        {
            yield return new WaitForSeconds(checkInterval);

            if (_seekingNeed || _needs == null) continue;

            var urgentNeed = _needs.GetMostUrgentNeed();
            if (urgentNeed == null || !urgentNeed.IsUnsatisfied) continue;

            var facility = FindFacilityFor(urgentNeed.type);
            if (facility == null)
            {
                Debug.LogWarning($"[Cuisine] {name} — besoin '{urgentNeed.type.needName}' insatisfait mais aucune facilité trouvée.");
                continue;
            }

            // Le besoin est urgent ET satisfiable : priorité sur une conversation ou une bagarre en
            // cours (fetch paresseux plutôt qu'un champ caché en Awake — évite tout souci d'ordre
            // d'AddComponent dans SpawnScheduler).
            GetComponent<MonsterSocialBehavior>()?.Interrupt();
            GetComponent<MonsterFightBehavior>()?.Interrupt();

            _waitStartTime = Time.time;

            // Les facilités "service joueur" (restaurant) passent par une réception dédiée —
            // vérifie la disponibilité et gère la file d'attente avant d'y envoyer le monstre.
            if (facility.playerServiceOnly && RestaurantReservationSystem.Instance != null)
            {
                bool registered = RestaurantReservationSystem.Instance.RegisterArrival(gameObject);
                if (!registered)
                {
                    // Comptoir resto pas encore posé (ou plus dans la scène) : _seekingNeed reste
                    // false, le prochain cycle (checkInterval) réessaiera automatiquement — sans
                    // ça le monstre restait bloqué indéfiniment, même une fois le comptoir construit.
                    continue;
                }
                _seekingNeed = true;
            }
            else
            {
                _seekingNeed = true;
                facility.RequestService(gameObject, _waitStartTime);
            }

            Debug.Log($"[Cuisine] {name} — besoin '{urgentNeed.type.needName}' insatisfait, se dirige vers '{facility.name}'.");
        }
    }

    // ─── Recherche ─────────────────────────────────────────────────

    FacilityRoomInstance FindFacilityFor(NeedType needType)
    {
        FacilityRoomInstance best     = null;
        float                bestDist = float.MaxValue;

        foreach (var facility in FacilityRoomInstance.All)
        {
            if (facility.needFulfilled != needType) continue;
            if (!facility.HasRoom)                  continue;

            float dist = Vector3.Distance(transform.position, facility.transform.position);
            if (dist > searchRadius || dist >= bestDist) continue;

            best     = facility;
            bestDist = dist;
        }

        return best;
    }
}
