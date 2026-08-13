using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Chaise/place assise dans la salle à manger.
/// Le monstre s'y dirige, attend le joueur, reçoit son plat et mange.
/// Le joueur livre en s'approchant et en appuyant sur Interact.
/// </summary>
public class EatingSpot : MonoBehaviour
{
    public static readonly HashSet<EatingSpot> All = new();

    [Header("Interaction (livraison joueur)")]
    public string interactActionName = "Interact";
    public float  deliveryRange = 2f;

    [Header("Déplacement")]
    public float arrivalDistance = 1.2f;

    [Header("Attente nourriture")]
    [Tooltip("Satisfaction perdue par seconde pendant que le monstre attend son repas à table")]
    public float waitFoodSatisfactionDecay = 0.5f;
    [Tooltip("Durée maximale de la jauge d'attente (en secondes) — la barre se vide sur cette durée")]
    public float waitGaugeMaxTime = 40f;

    [Header("Saleté après repas")]
    [Tooltip("Prefabs de débris à spawner sur la table après qu'un monstre a mangé")]
    public GameObject[] mealDebrisPrefabs;
    [Tooltip("Nombre max de débris générés après un repas")]
    [Range(0, 5)]
    public int maxMealDebris = 2;

    // ─── État ──────────────────────────────────────────────────────

    enum SpotState { Empty, MovingToSeat, WaitingForFood, Eating }
    SpotState  _state      = SpotState.Empty;
    GameObject _monster;
    float      _waitStartTime;
    int        _dirtyCount;
    bool       _reservedByCook;

    /// <summary>True si des débris post-repas n'ont pas encore été ramassés.</summary>
    public bool IsDirty          => _dirtyCount > 0;
    public bool IsOccupied       => _state != SpotState.Empty || IsDirty;
    public bool IsWaitingForFood => _state == SpotState.WaitingForFood;

    /// <summary>True tant qu'un cuisinier a réservé ce service — empêche un doublon.</summary>
    public bool IsReservedForService => _reservedByCook;
    public void ReserveForService()          => _reservedByCook = true;
    public void ReleaseServiceReservation()  => _reservedByCook = false;

    /// <summary>MonsterData du monstre en attente (null si aucun ou état différent).</summary>
    public MonsterData WaitingMonsterData =>
        _state == SpotState.WaitingForFood && _monster != null
            ? _monster.GetComponent<MonsterDataReference>()?.Data
            : null;

    void Awake()
    {
        var cfg = HotelConfig.Instance;
        if (cfg != null)
        {
            waitFoodSatisfactionDecay = cfg.waitFoodSatisfactionDecay;
            waitGaugeMaxTime          = cfg.waitGaugeMaxTime;
            deliveryRange             = cfg.deliveryRange;
        }
    }

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

    // ─── API publique ──────────────────────────────────────────────

    /// <summary>Assigne ce siège à un monstre. Retourne false si déjà occupé.</summary>
    public bool TrySit(GameObject monster)
    {
        if (IsOccupied || monster == null) return false;
        _monster = monster;
        _state   = SpotState.MovingToSeat;
        StartCoroutine(SitRoutine(monster));
        return true;
    }

    // ─── Update : livraison joueur ──────────────────────────────────

    void Update()
    {
        if (_state != SpotState.WaitingForFood) return;

        if (_monster == null)
        {
            // Le monstre a disparu pendant qu'il attendait (départ forcé, timeout hôtel qui le
            // détruit ailleurs...) sans jamais repasser par ce composant — sans ce filet, _state
            // restait bloqué sur WaitingForFood pour toujours, rendant cette place "occupée" à vie
            // (IsOccupied) même si elle est en fait vide. D'où l'impression que les tables ne se
            // libèrent jamais alors que de nouvelles tables fonctionnent (elles, jamais bloquées).
            Debug.LogWarning($"[Cuisine] {name} — le monstre en attente a disparu sans libérer la place, libération forcée.");
            Release();
            return;
        }

        // Décroissance satisfaction + tick jauge
        _monster.GetComponent<SatisfactionComponent>()
                ?.ApplyDecay(waitFoodSatisfactionDecay * Time.deltaTime);
        _monster.GetComponent<WaitingGaugeUI>()?.Tick(Time.deltaTime);

        // Timeout : le monstre repart si la jauge est écoulée
        if (Time.time - _waitStartTime >= waitGaugeMaxTime)
        {
            // Client chambre : pénalité de satisfaction + retour en chambre (garde sa réservation).
            // Visiteur sans chambre : quitte l'hôtel avec une note d'insatisfaction.
            _monster.GetComponent<MonsterNeedSeeker>()?.GiveUpMeal("jamais servi à table (attente écoulée)");
            Release();
            return;
        }

        // Détection livraison joueur
        foreach (var holder in PlayerItemHolder.All)
        {
            if (!holder.IsHoldingItem) continue;
            if (holder.HeldItem.itemType != HoldableItemType.CookedDish) continue;
            if (!IsDishCompatibleWithMonster(holder.HeldItem, _monster)) continue;
            if (Vector3.Distance(transform.position, holder.transform.position) > deliveryRange) continue;

            var pi = holder.GetComponent<PlayerInput>();
            if (pi == null || !pi.WasPressed(interactActionName)) continue;

            var   carrier = holder.GetComponent<FoodQualityCarrier>();
            float quality = carrier != null ? carrier.Quality : 1f;
            if (carrier != null) carrier.Quality = 1f;

            var dish = holder.HeldItem;
            holder.Drop();

            float waitTime = Time.time - _waitStartTime;
            _state = SpotState.Eating;
            _monster.GetComponent<WaitingGaugeUI>()?.Hide();

            Debug.Log($"[Cuisine] Joueur livre '{dish.itemName}' à {_monster.name}.");
            StartCoroutine(EatRoutine(_monster, dish, quality, waitTime));
            break;
        }
    }

    // ─── API employé cuisinier ─────────────────────────────────────

    /// <summary>
    /// Complète la livraison automatiquement (utilisé par CookEmployeeAI).
    /// Donne un bonus de satisfaction, libère la place et prévient le MonsterNeedSeeker.
    /// </summary>
    /// <summary>
    /// Livraison par l'employé cuisinier.
    /// Quand dish est fourni, déclenche EatRoutine (même expérience que la livraison joueur).
    /// Sinon, remplit le besoin le plus urgent en fallback.
    /// </summary>
    public void CompleteDelivery(float satisfactionBonus = 15f, HoldableItemData dish = null)
    {
        if (!IsWaitingForFood || _monster == null) return;

        var   monster  = _monster;
        float waitTime = _waitStartTime > 0f ? Time.time - _waitStartTime : 0f;

        monster.GetComponent<WaitingGaugeUI>()?.Hide();
        monster.GetComponent<SatisfactionComponent>()?.ApplyBonus(satisfactionBonus);

        SpawnMealDebris();

        if (dish != null)
        {
            // Même chemin que la livraison joueur : EatRoutine gère le remplissage progressif
            // et appelle OnServiceComplete + Release à la fin
            _state   = SpotState.Eating;
            _monster = null;
            StartCoroutine(EatRoutine(monster, dish, 1f, waitTime));
        }
        else
        {
            // Fallback sans plat : remplit le besoin, délai avant de libérer la table
            _state   = SpotState.Eating;
            _monster = null;
            var needs  = monster.GetComponent<MonsterNeedsComponent>();
            var urgent = needs?.GetMostUrgentNeed();
            if (urgent != null)
                needs.FulfillNeed(urgent.type, 1f, waitTime, 1f);
            StartCoroutine(DelayedServiceComplete(monster, 3f));
        }
    }

    // ─── Routines ──────────────────────────────────────────────────

    IEnumerator SitRoutine(GameObject monster)
    {
        var mover = monster.GetComponent<MonsterMover>();
        if (mover != null)
        {
            mover.MoveTo(transform.position);
            yield return new WaitUntil(() =>
                monster == null ||
                Vector3.Distance(monster.transform.position, transform.position) < arrivalDistance);
        }

        if (monster == null) { Release(); yield break; }

        _state         = SpotState.WaitingForFood;
        _waitStartTime = Time.time;
        var gauge = monster.GetComponent<WaitingGaugeUI>() ?? monster.AddComponent<WaitingGaugeUI>();
        gauge.Show(waitGaugeMaxTime);
    }

    IEnumerator EatRoutine(GameObject monster, HoldableItemData dish, float quality, float waitTime)
    {
        // Visuel du plat sur la table
        GameObject dishVisual = null;
        if (dish?.heldPrefab != null)
            dishVisual = Instantiate(dish.heldPrefab,
                                     transform.position + Vector3.up * 0.3f,
                                     Quaternion.identity);

        float duration = dish != null && dish.eatDuration > 0f ? dish.eatDuration : 5f;
        float fillRate = dish != null ? dish.fillAmount / duration : 0f;
        float elapsed  = 0f;
        var   needs    = monster.GetComponent<MonsterNeedsComponent>();

        while (elapsed < duration && monster != null)
        {
            if (needs != null && dish?.needFulfilled != null)
            {
                var state = needs.GetNeedState(dish.needFulfilled);
                if (state != null)
                    state.level = Mathf.Clamp01(state.level + fillRate * Time.deltaTime);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (dishVisual != null) Destroy(dishVisual);

        if (monster != null)
        {
            monster.GetComponent<MonsterNeedsComponent>()
                   ?.FulfillNeed(dish?.needFulfilled, 0f, waitTime, quality);
            monster.GetComponent<MonsterNeedSeeker>()?.OnServiceComplete();
            PayForMeal(monster);
            Debug.Log($"[Cuisine] {monster.name} a terminé son repas — retour en chambre.");
        }

        SpawnMealDebris();
        Release();
    }

    void Release()
    {
        if (_monster != null) _monster.GetComponent<WaitingGaugeUI>()?.Hide();
        _state          = SpotState.Empty;
        _monster        = null;
        _reservedByCook = false;
    }

    /// <summary>
    /// Trouve le point TableSurface de la table à laquelle cette chaise est snappée,
    /// via ChairSlotOccupant → ChairSlot.parent → enfant nommé "TableSurface".
    /// </summary>
    Transform FindTableSurface()
    {
        var occupant = GetComponentInParent<ChairSlotOccupant>(true);
        if (occupant?.Slot == null) return null;
        return occupant.Slot.transform.parent?.Find("TableSurface");
    }

    IEnumerator DelayedServiceComplete(GameObject monster, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (monster != null)
        {
            monster.GetComponent<MonsterNeedSeeker>()?.OnServiceComplete();
            PayForMeal(monster);
        }
        Release();
    }

    /// <summary>Revenu du repas servi — s'applique aussi bien à un client chambre qu'à un visiteur
    /// sans chambre (les deux payent leur repas séparément de la nuitée).</summary>
    static void PayForMeal(GameObject monster)
    {
        var data = monster.GetComponent<MonsterDataReference>()?.Data;
        if (data == null || data.mealRevenue <= 0) return;

        EconomyManager.Instance?.Earn(data.mealRevenue);
        Debug.Log($"[Paiement] {data.monsterName} repas servi — +{data.mealRevenue}G (solde: {EconomyManager.Instance?.Gold}G)");
    }

    void SpawnMealDebris()
    {
        if (mealDebrisPrefabs == null || mealDebrisPrefabs.Length == 0 || maxMealDebris <= 0) return;

        var surface = FindTableSurface();
        if (surface == null) return;

        int count = Random.Range(0, maxMealDebris + 1);
        for (int i = 0; i < count; i++)
        {
            var prefab = mealDebrisPrefabs[Random.Range(0, mealDebrisPrefabs.Length)];
            if (prefab == null) continue;

            var pos = surface.position + new Vector3(
                Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f));
            var go = Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

            var d = go.GetComponent<DebrisInstance>() ?? go.AddComponent<DebrisInstance>();
            d.Init(null); // pas de chambre — trouvé via DebrisInstance.All par le nettoyeur
            _dirtyCount++;
            d.OnRemoved += () => _dirtyCount--;
        }
    }

    // ─── Helpers ───────────────────────────────────────────────────

    static bool IsDishCompatibleWithMonster(HoldableItemData dish, GameObject monster)
    {
        if (dish.compatibleMonsters == null || dish.compatibleMonsters.Length == 0) return true;
        var dataRef = monster.GetComponent<MonsterDataReference>();
        if (dataRef == null) return false;
        foreach (var m in dish.compatibleMonsters)
            if (m == dataRef.Data) return true;
        return false;
    }
}
