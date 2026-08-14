using UnityEngine;

/// <summary>
/// Squelette commun aux IA employé (Cuisinier, Nettoyeur, Réceptionniste) :
/// gère le cycle Working/GoingToWork, le flag `_busy`, et le nettoyage des
/// réservations/BlockBreak en fin de tâche ou d'interruption externe
/// (démission, fin d'heures de travail).
///
/// Les sous-classes implémentent :
///   - GoToPost()      : déplacement initial vers le poste (peut être vide si pas de poste fixe)
///   - TryStartTask()  : cherche une tâche disponible et la démarre si trouvée
///   - ReleaseReservations() (optionnel) : libère les cibles de tâche réservées
/// </summary>
[RequireComponent(typeof(EmployeeInstance))]
public abstract class EmployeeTaskAI : MonoBehaviour
{
    protected EmployeeInstance _employee;
    protected MonsterMover     _mover;
    protected bool             _busy;

    bool _wentToPost;

    protected virtual void Awake()
    {
        _employee = GetComponent<EmployeeInstance>();
        _mover    = GetComponent<MonsterMover>() ?? gameObject.AddComponent<MonsterMover>();
    }

    protected virtual void Update()
    {
        if (_employee.State != EmployeeState.Working && _employee.State != EmployeeState.GoingToWork)
        {
            // Interruption externe (démission, fin d'heures de travail...) : la routine en
            // cours doit être stoppée explicitement, sinon elle continue en arrière-plan
            // pendant qu'une nouvelle tâche pourrait démarrer en parallèle une fois _busy reset.
            if (_busy)
            {
                StopAllCoroutines();
                EndTask();
            }
            _wentToPost = false;
            return;
        }

        if (_employee.State == EmployeeState.GoingToWork)
        {
            if (!_wentToPost)
            {
                GoToPost();
                _wentToPost = true;
                _employee.SetState(EmployeeState.Working);
            }
            return;
        }

        if (_busy) return;

        TryStartTask();
    }

    /// <summary>Déplacement initial vers le poste de travail (une fois, en GoingToWork). Vide si pas de poste fixe.</summary>
    protected abstract void GoToPost();

    /// <summary>Cherche une tâche disponible et la démarre (coroutine qui met `_busy = true`) si trouvée.</summary>
    protected abstract void TryStartTask();

    /// <summary>Libère les cibles de tâche réservées par la sous-classe. No-op par défaut.</summary>
    protected virtual void ReleaseReservations() { }

    /// <summary>À appeler en fin de routine (naturelle ou interrompue) : reset busy/BlockBreak + réservations.</summary>
    protected void EndTask()
    {
        _busy                = false;
        _employee.BlockBreak = false;
        ReleaseReservations();
    }

    /// <summary>
    /// Multiplicateur de vitesse d'action selon la note de l'employé (note 1 → 1.0, note 20 → réduit
    /// selon HotelConfig.employeeRatingCurveDivisor). Utilisé pour scaler la durée des tâches.
    /// </summary>
    protected float RatingSpeedMultiplier()
    {
        int   rating  = _employee.Data != null ? _employee.Data.rating : 10;
        float divisor = HotelConfig.Employee != null ? HotelConfig.Employee.employeeRatingCurveDivisor : 38f;
        return 1f - (rating - 1f) / divisor;
    }
}
