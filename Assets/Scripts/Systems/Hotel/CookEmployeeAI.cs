using System.Collections;
using UnityEngine;

/// <summary>
/// IA du cuisinier employé.
/// Flux : attente au comptoir → frigo (ingrédient du monstre) → plan de travail (cuisson) → livraison à table → retour au comptoir.
/// </summary>
public class CookEmployeeAI : EmployeeTaskAI
{
    [Tooltip("Durée de cuisson si aucune recette n'est trouvée (secondes). Réduite par la note.")]
    public float baseCookDuration = 10f;
    [Tooltip("Bonus de satisfaction accordé au monstre après livraison (si HotelConfig absent).")]
    public float deliverySatisfactionBonus = 15f;
    [Tooltip("Distance considérée comme 'arrivé' à une destination.")]
    public float arrivalDistance = 1.5f;
    [Tooltip("Timeout pour rejoindre une destination (secondes).")]
    public float moveTimeout = 15f;

    WorkbenchStation _bench;
    FridgeStation    _fridge;
    EatingSpot       _reservedSpot;

    protected override void GoToPost()
    {
        CacheStations();
        if (_bench != null) _mover.MoveTo(BenchPosition());
    }

    protected override void TryStartTask()
    {
        var spot = FindWaitingSpot();
        if (spot != null)
        {
            spot.ReserveForService();
            _reservedSpot = spot;
            StartCoroutine(CookRoutine(spot));
        }
        else
            WaitAtCounter();
    }

    protected override void ReleaseReservations()
    {
        if (_reservedSpot != null) { _reservedSpot.ReleaseServiceReservation(); _reservedSpot = null; }
    }

    // ─── Initialisation ───────────────────────────────────────────

    void WaitAtCounter()
    {
        if (_bench != null && Vector3.Distance(transform.position, BenchPosition()) > arrivalDistance)
            _mover.MoveTo(BenchPosition());
    }

    void CacheStations()
    {
        if (_bench  == null) _bench  = FindAnyObjectByType<WorkbenchStation>();
        if (_fridge == null) _fridge = FindAnyObjectByType<FridgeStation>();
    }

    /// <summary>
    /// Décalage stable par employé (basé sur son InstanceID) — évite que plusieurs cuisiniers
    /// ne se superposent exactement au même point autour d'un plan de travail/frigo partagé,
    /// ce qui donnerait l'impression qu'ils travaillent en alternance plutôt qu'en parallèle.
    /// </summary>
    Vector3 PersonalOffset(float radius = 0.6f)
    {
        float angle = (GetInstanceID() * 137) % 360;
        float rad   = angle * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * radius;
    }

    Vector3 BenchPosition()  => _bench  != null ? _bench.transform.position  + PersonalOffset() : transform.position;
    Vector3 FridgePosition() => _fridge != null ? _fridge.transform.position + PersonalOffset() : transform.position;

    // ─── Recherche ────────────────────────────────────────────────

    EatingSpot FindWaitingSpot()
    {
        EatingSpot best  = null;
        float      bestD = float.MaxValue;
        foreach (var spot in EatingSpot.All)
        {
            if (spot == null || !spot.IsWaitingForFood || spot.IsReservedForService) continue;
            float d = Vector3.Distance(transform.position, spot.transform.position);
            if (d < bestD) { bestD = d; best = spot; }
        }
        return best;
    }

    RecipeData FindRecipeForMonster(MonsterData monsterData)
    {
        var recipes = HotelConfig.Instance?.recipes;
        if (recipes == null || recipes.Length == 0) return null;

        RecipeData fallback = null;

        foreach (var recipe in recipes)
        {
            if (recipe == null || recipe.result == null) continue;

            // Garder une recette générique en fallback (utilisée si aucune ne correspond au monstre)
            if (fallback == null) fallback = recipe;

            var  dish       = recipe.result;
            bool compatible = dish.compatibleMonsters == null || dish.compatibleMonsters.Length == 0;
            if (!compatible && monsterData != null)
            {
                foreach (var m in dish.compatibleMonsters)
                    if (m == monsterData) { compatible = true; break; }
            }
            if (!compatible) continue;

            if (_fridge?.ingredients != null && recipe.ingredient != null)
            {
                foreach (var ing in _fridge.ingredients)
                    if (ing == recipe.ingredient) return recipe;
            }
            else return recipe;
        }

        return fallback; // Aucune recette spécifique — utilise la première disponible
    }

    float GetCookDuration(RecipeData recipe)
    {
        float baseD = recipe != null ? recipe.workDuration : baseCookDuration;
        return baseD * RatingSpeedMultiplier();
    }

    float GetDeliveryBonus()
    {
        int   rating = _employee.Data != null ? _employee.Data.rating : 10;
        float t      = (rating - 1f) / 19f;
        var   cfg    = HotelConfig.Instance;
        float min    = cfg != null ? cfg.cookDeliveryBonusMin : deliverySatisfactionBonus * 0.4f;
        float max    = cfg != null ? cfg.cookDeliveryBonusMax : deliverySatisfactionBonus * 1.6f;
        return Mathf.Lerp(min, max, t);
    }

    // ─── Routine principale ───────────────────────────────────────

    IEnumerator CookRoutine(EatingSpot spot)
    {
        _busy                = true;
        _employee.BlockBreak = true;
        CacheStations();

        var   recipe       = FindRecipeForMonster(spot.WaitingMonsterData);
        float cookDuration = GetCookDuration(recipe);

        // 1. Aller au frigo
        if (_fridge != null)
        {
            _mover.MoveTo(FridgePosition());
            yield return MoveToTarget(FridgePosition(), moveTimeout);
        }

        // 2. Aller au plan de travail et cuisiner
        if (_bench != null)
        {
            _mover.MoveTo(BenchPosition());
            yield return MoveToTarget(BenchPosition(), moveTimeout);
        }

        yield return new WaitForSeconds(cookDuration);

        // 3. Livraison — aller à table si le monstre attend toujours
        if (spot == null || !spot.IsWaitingForFood) { EndTask(); yield break; }

        _mover.MoveTo(spot.transform.position);
        yield return MoveToSpot(spot, moveTimeout);

        // 4. Livrer (passe le plat pour remplir le besoin de faim)
        if (spot != null && spot.IsWaitingForFood)
            spot.CompleteDelivery(GetDeliveryBonus(), recipe?.result);

        // 5. Retour au comptoir
        if (_bench != null) _mover.MoveTo(BenchPosition());
        EndTask();
    }

    // ─── Helpers déplacement ──────────────────────────────────────

    IEnumerator MoveToTarget(Vector3 target, float timeout)
    {
        float t = timeout;
        while (t > 0f && Vector3.Distance(transform.position, target) > arrivalDistance)
        {
            t -= Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator MoveToSpot(EatingSpot spot, float timeout)
    {
        float t = timeout;
        while (t > 0f && spot != null && spot.IsWaitingForFood &&
               Vector3.Distance(transform.position, spot.transform.position) > arrivalDistance)
        {
            t -= Time.deltaTime;
            yield return null;
        }
    }
}
