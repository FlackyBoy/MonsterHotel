using System.Collections;
using UnityEngine;

/// <summary>
/// IA de l'employé de nettoyage : nettoie les meubles sales/abîmés et ramasse les débris au sol.
/// Pas de poste fixe — reste où il se trouve tant qu'aucune tâche n'est disponible.
/// </summary>
public class CleaningEmployeeAI : EmployeeTaskAI
{
    [Tooltip("Durée de base pour nettoyer/réparer un meuble (remplacée par HotelConfig si disponible)")]
    public float cleanDuration = 4f;

    FurnitureInstance _reservedFurniture;
    DebrisInstance    _reservedDebris;

    protected override void Awake()
    {
        base.Awake();
        var cfg = HotelConfig.Instance;
        if (cfg != null) cleanDuration = cfg.employeeCleanBaseDuration;
    }

    protected override void GoToPost() { /* pas de poste fixe pour le nettoyeur */ }

    protected override void TryStartTask()
    {
        var furniture = FindDirtyFurniture();
        if (furniture != null)
        {
            furniture.ReserveCleaning();
            _reservedFurniture = furniture;
            StartCoroutine(CleanRoutine(furniture));
            return;
        }

        var debris = FindDebris();
        if (debris != null)
        {
            debris.Reserve();
            _reservedDebris = debris;
            StartCoroutine(PickupDebrisRoutine(debris));
        }
    }

    protected override void ReleaseReservations()
    {
        if (_reservedFurniture != null) { _reservedFurniture.ReleaseCleaning(); _reservedFurniture = null; }
        if (_reservedDebris    != null) { _reservedDebris.ReleaseReservation(); _reservedDebris    = null; }
    }

    // ─── Recherche ────────────────────────────────────────────────

    FurnitureInstance FindDirtyFurniture()
    {
        FurnitureInstance best  = null;
        float             bestD = float.MaxValue;

        foreach (var fi in FurnitureInstance.All)
        {
            if (fi == null) continue;
            if (!fi.IsDirty && !fi.IsDamaged) continue;
            if (fi.IsBeingCleaned) continue;
            float d = Vector3.Distance(transform.position, fi.transform.position);
            if (d < bestD) { bestD = d; best = fi; }
        }
        return best;
    }

    DebrisInstance FindDebris()
    {
        DebrisInstance best  = null;
        float          bestD = float.MaxValue;

        foreach (var d in DebrisInstance.All)
        {
            if (d == null) continue;
            if (d.IsReserved) continue;
            float dist = Vector3.Distance(transform.position, d.transform.position);
            if (dist < bestD) { bestD = dist; best = d; }
        }
        return best;
    }

    // ─── Routines ─────────────────────────────────────────────────

    IEnumerator CleanRoutine(FurnitureInstance target)
    {
        _busy                = true;
        _employee.BlockBreak = true;

        _mover.MoveTo(target.transform.position);

        float timeout = 10f;
        while (timeout > 0f && target != null &&
               HorizontalDistance(transform.position, target.transform.position) > 1.5f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (target != null)
        {
            float actualDuration = cleanDuration * RatingSpeedMultiplier();
            yield return new WaitForSeconds(actualDuration);

            target.SetDirty(false);
            target.SetDamaged(false);
        }

        EndTask();
    }

    IEnumerator PickupDebrisRoutine(DebrisInstance debris)
    {
        _busy                = true;
        _employee.BlockBreak = true;

        _mover.MoveTo(debris.transform.position);

        float timeout = 10f;
        while (timeout > 0f && debris != null &&
               HorizontalDistance(transform.position, debris.transform.position) > 1.2f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (debris != null)
            debris.PickUp();

        EndTask();
    }

    /// <summary>
    /// Distance horizontale (XZ) uniquement — le mover verrouille toujours le Y du nettoyeur à la
    /// hauteur du sol (jamais celle d'une table), donc comparer la distance 3D complète empêchait
    /// de considérer un débris posé sur table comme "atteint" (l'écart de hauteur ne se referme
    /// jamais), déclenchant systématiquement le timeout de secours au lieu d'une vraie arrivée.
    /// </summary>
    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
