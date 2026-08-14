using System.Collections;
using UnityEngine;

/// <summary>
/// IA de l'employé de réception : se rend au comptoir et prend en charge les monstres en attente.
/// Gère à la fois la réception de l'hôtel (ReservationSystem, comptoir ReceptionDesk) et celle du
/// restaurant (RestaurantReservationSystem, comptoir RestaurantReceptionDesk) — un seul employé
/// suffit pour les deux.
///
/// Arbitrage entre les deux files : sert toujours le monstre qui attend depuis le plus longtemps,
/// tous postes confondus (comparaison directe des temps d'attente) — pas de "poste attitré" avec
/// priorité absolue. Avec un flux d'arrivées continu d'un côté, une priorité de poste fixe pouvait
/// affamer l'autre file indéfiniment (l'employé ne l'aidait que quand son propre poste était
/// *complètement* vide à l'instant de la vérification) ; comparer les temps d'attente garantit
/// qu'aucune des deux files n'est jamais ignorée trop longtemps.
///
/// Chaque monstre est "réclamé" (IsClaimed) dès qu'un employé s'engage à le traiter, pour qu'un
/// second réceptionniste ne converge pas sur le même monstre précis — avec plusieurs employés,
/// cette exclusivité seule suffit à les répartir naturellement entre les deux files (le suivant à
/// devenir libre choisit simplement le plus ancien parmi ceux qui restent non réclamés).
///
/// Ne s'engage jamais sur un monstre qu'il ne peut pas encore servir (pas de chambre compatible
/// côté hôtel, pas de place libre côté resto — NextServiceableUnclaimed) : dans ce cas il regarde
/// l'autre file à la place plutôt que d'attendre inutilement devant un comptoir.
/// </summary>
public class ReceptionEmployeeAI : EmployeeTaskAI
{
    float _checkInBaseDelay;

    ReservationSystem.PendingGuest             _claimedHotelGuest;
    RestaurantReservationSystem.PendingVisitor _claimedRestaurantGuest;

    protected override void Awake()
    {
        base.Awake();
        var cfg           = HotelConfig.Employee;
        _checkInBaseDelay = cfg != null ? cfg.employeeCheckInBaseDelay : 2f;
    }

    protected override void GoToPost()
    {
        ReceptionDesk desk = null;
        foreach (var d in ReceptionDesk.All) { desk = d; break; }
        if (desk != null)
            _mover.MoveTo(desk.StandPoint);
    }

    protected override void TryStartTask()
    {
        var rs  = ReservationSystem.Instance;
        var rrs = RestaurantReservationSystem.Instance;

        // NextServiceableUnclaimed (pas juste NextUnclaimed) : inutile de s'engager à marcher
        // jusqu'à un comptoir pour un monstre qu'on ne peut de toute façon pas encore servir
        // (pas de chambre compatible / pas de place au resto) — mieux vaut regarder l'autre
        // file dans ce cas plutôt que d'attendre bêtement devant un comptoir sans rien y faire.
        var hotelGuest = rs?.NextServiceableUnclaimed;
        var restoGuest = rrs?.NextServiceableUnclaimed;

        if (hotelGuest == null && restoGuest == null) return;

        // Sert le plus ancien en attente, tous postes confondus.
        bool serveHotel = hotelGuest != null &&
                           (restoGuest == null || hotelGuest.WaitedSeconds >= restoGuest.WaitedSeconds);

        Debug.Log($"[Réception] {name} — TryStartTask : hotelGuest={(hotelGuest != null ? $"{hotelGuest.WaitedSeconds:F1}s" : "null")}, restoGuest={(restoGuest != null ? $"{restoGuest.WaitedSeconds:F1}s" : "null")} → sert {(serveHotel ? "hôtel" : "resto")}.");

        if (serveHotel) StartHotelTask(hotelGuest);
        else            StartRestaurantTask(restoGuest);
    }

    void StartHotelTask(ReservationSystem.PendingGuest guest)
    {
        guest.IsClaimed   = true;
        _claimedHotelGuest = guest;
        StartCoroutine(CheckInRoutine(isRestaurant: false));
    }

    void StartRestaurantTask(RestaurantReservationSystem.PendingVisitor guest)
    {
        guest.IsClaimed        = true;
        _claimedRestaurantGuest = guest;
        StartCoroutine(CheckInRoutine(isRestaurant: true));
    }

    /// <summary>Libère la réclamation si la tâche est interrompue avant d'aboutir (fin d'heures,
    /// démission...) — sinon le monstre resterait "réclamé" indéfiniment sans jamais être traité.</summary>
    protected override void ReleaseReservations()
    {
        if (_claimedHotelGuest      != null) { _claimedHotelGuest.IsClaimed      = false; _claimedHotelGuest      = null; }
        if (_claimedRestaurantGuest != null) { _claimedRestaurantGuest.IsClaimed = false; _claimedRestaurantGuest = null; }
    }

    IEnumerator CheckInRoutine(bool isRestaurant)
    {
        _busy = true;
        _employee.BlockBreak = true;

        Vector3? deskPos = null;
        if (isRestaurant)
        {
            RestaurantReceptionDesk desk = null;
            foreach (var d in RestaurantReceptionDesk.All) { desk = d; break; }
            if (desk != null) deskPos = desk.StandPoint;
        }
        else
        {
            ReceptionDesk desk = null;
            foreach (var d in ReceptionDesk.All) { desk = d; break; }
            if (desk != null) deskPos = desk.StandPoint;
        }

        // true si aucun comptoir à atteindre (rien à attendre) ou si l'arrivée physique est confirmée.
        bool arrived = !deskPos.HasValue;

        if (deskPos.HasValue)
        {
            _mover.MoveTo(deskPos.Value);

            // Attend l'arrivée physique au comptoir avant de valider quoi que ce soit — sinon un
            // employé qui vient d'être embauché (donc encore loin du comptoir) validait les
            // monstres déjà en attente instantanément, avant même d'avoir commencé à marcher.
            float timeout = 15f;
            while (timeout > 0f && Vector3.Distance(transform.position, deskPos.Value) > 1.5f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            arrived = Vector3.Distance(transform.position, deskPos.Value) <= 1.5f;
        }

        // Ne valide QUE si l'employé est réellement arrivé — sinon le monstre était accepté "à
        // distance" après le timeout de 15s, même si l'employé n'avait jamais atteint le comptoir
        // (ex : bloqué en chemin), donnant l'impression qu'il était validé "tout seul".
        if (arrived)
        {
            float delay = Mathf.Max(0f, _checkInBaseDelay * RatingSpeedMultiplier());
            yield return new WaitForSeconds(delay);

            if (_employee.State == EmployeeState.Working)
            {
                if (isRestaurant) RestaurantReservationSystem.Instance?.CheckInNext(_claimedRestaurantGuest);
                else              ReservationSystem.Instance?.CheckInNext(_claimedHotelGuest);
            }
        }
        else
        {
            Debug.LogWarning($"[Réception] {name} — n'a jamais atteint le comptoir {(isRestaurant ? "resto" : "hôtel")} (bloqué en chemin ?), abandon de cette tentative — le monstre reste en attente pour un prochain essai.");
        }

        yield return new WaitForSeconds(0.5f);
        EndTask(); // appelle ReleaseReservations() — libère la réclamation dans tous les cas
    }
}
