using System.Collections.Generic;
using UnityEngine;

/// <summary>Distingue un client chambre (réservation, occupe une chambre) d'un visiteur repas
/// (arrivé directement pour le restaurant, sans chambre — voir SpawnScheduler).</summary>
public enum GuestChannel { Room, Restaurant }

/// <summary>
/// Agrège les stats globales de l'hôtel (Confort, Renommée) en fonction des décorations posées,
/// plus la renommée "gagnée" par catégorie de monstre via la satisfaction des clients au départ
/// (G1). Placer dans _Managers en scène.
/// </summary>
public class HotelStatsManager : MonoBehaviour
{
    public static HotelStatsManager Instance { get; private set; }

    readonly List<DecorationInstance> _decorations = new();
    readonly Dictionary<MonsterType, float> _categoryRenown = new();

    [Header("Stats (lecture seule — mis à jour en Play)")]
    [SerializeField] float _totalComfort;
    [SerializeField] float _totalRenown;
    [SerializeField] float _totalRoomAttractiveness;

    [Header("Renommée par satisfaction (G1) — valeurs de départ, à ajuster")]
    [Tooltip("Satisfaction en dessous de laquelle un départ ne rapporte aucune renommée (mais n'en retire pas non plus, sauf si en colère)")]
    public float satisfactionRenownThreshold = 50f;
    [Tooltip("Renommée gagnée par point de satisfaction au-dessus du seuil, à chaque départ")]
    public float renownPerSatisfactionPoint = 0.05f;
    [Tooltip("Renommée perdue à chaque départ en colère (insatisfaction), indépendamment de la satisfaction exacte")]
    public float angryDepartureRenownPenalty = 1f;

    /// <summary>Somme des comfortBonus de toutes les décorations posées.</summary>
    public float TotalComfort => _totalComfort;

    /// <summary>Somme des renownBonus de toutes les décorations posées + de l'attractivité de
    /// toutes les chambres (base chambre + bonus meubles, voir RecalcRoomAttractiveness) — base
    /// partagée par toutes les catégories.</summary>
    public float TotalRenown  => _totalRenown;

    /// <summary>Déclenché chaque fois que Confort ou Renommée change.</summary>
    public event System.Action OnStatsChanged;

    /// <summary>Déclenché à chaque départ (check-out ou visiteur repas) — satisfaction finale, si en
    /// colère, le delta de renommée appliqué, le canal (chambre/resto) et si le monstre a
    /// effectivement été servi (côté resto : a mangé ; côté chambre : toujours vrai, la chambre est
    /// le service).</summary>
    public event System.Action<MonsterType, float, bool, float, GuestChannel, bool> OnGuestDeparted;

    /// <summary>Déclenché à chaque paiement d'un repas au restaurant (client chambre ou visiteur) — pour le gain resto par type de monstre.</summary>
    public event System.Action<MonsterType, int> OnMealRevenue;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Register(DecorationInstance deco)
    {
        if (!_decorations.Contains(deco))
            _decorations.Add(deco);
        RecalcStats();
    }

    public void Unregister(DecorationInstance deco)
    {
        _decorations.Remove(deco);
        RecalcStats();
    }

    void RecalcStats()
    {
        float comfort = 0f, renown = 0f;
        foreach (var d in _decorations)
        {
            if (d?.Data == null) continue;
            comfort += d.Data.comfortBonus;
            renown  += d.Data.renownBonus;
        }
        _totalComfort = comfort;
        _totalRenown  = renown + _totalRoomAttractiveness;
        OnStatsChanged?.Invoke();
    }

    /// <summary>
    /// Recalcule l'attractivité totale (somme de RoomInstance.TotalAttractiveness sur toutes les
    /// chambres — base chambre + bonus de chaque meuble) et la refond dans la renommée globale
    /// (RecalcStats), au même titre que les décorations : une base partagée par toutes les
    /// catégories, pas spécifique à un type de monstre. Scan live (comme
    /// RoomManagementPanel/DayScoreManager ailleurs dans le projet) plutôt qu'un système
    /// d'événements — aucun événement de changement de meuble n'existe aujourd'hui sur
    /// FurnitureInstance/RoomInstance. Appelé une fois par jour depuis DayScoreManager.HandleNewDay,
    /// pas en continu (la renommée est lue très souvent ailleurs — jauge légendaire, réduction du
    /// délai de spawn — un scan à chaque frame serait coûteux pour un gain de fraîcheur minime).
    /// </summary>
    public void RecalcRoomAttractiveness()
    {
        int total = 0;
        foreach (var room in FindObjectsByType<RoomInstance>(FindObjectsSortMode.None))
            total += room.TotalAttractiveness;
        _totalRoomAttractiveness = total;
        RecalcStats();
    }

    /// <summary>
    /// Renommée totale disponible pour débloquer les légendaires de cette catégorie — base
    /// décorations (partagée) + renommée gagnée spécifiquement par cette catégorie (G1).
    /// </summary>
    public float RenownForCategory(MonsterType type)
    {
        _categoryRenown.TryGetValue(type, out float earned);
        return _totalRenown + earned;
    }

    /// <summary>
    /// À appeler à chaque départ réel d'un monstre (check-out chambre ou visiteur repas qui repart)
    /// — convertit sa satisfaction finale en delta de renommée pour sa catégorie (G1).
    /// channel : chambre ou resto (voir GuestChannel). served : a effectivement reçu le service
    /// attendu (côté resto : a mangé avant de partir ; côté chambre : toujours vrai, occuper la
    /// chambre EST le service, contrairement à un visiteur resto qui peut repartir sans avoir été
    /// servi — abandon ou délai dépassé).
    /// </summary>
    public void ReportGuestDeparture(MonsterType type, float satisfaction, bool angry, GuestChannel channel, bool served)
    {
        float delta = angry
            ? -angryDepartureRenownPenalty
            : Mathf.Max(0f, (satisfaction - satisfactionRenownThreshold) * renownPerSatisfactionPoint);

        _categoryRenown.TryGetValue(type, out float current);
        _categoryRenown[type] = current + delta;

        Debug.Log($"[Renommée] {type} départ ({channel}, {(served ? "servi" : "non servi")}, satisfaction {satisfaction:F0}/100{(angry ? ", en colère" : "")}) → " +
                   $"{(delta >= 0 ? "+" : "")}{delta:F2} renommée (total catégorie: {_categoryRenown[type]:F2})");

        OnStatsChanged?.Invoke();
        OnGuestDeparted?.Invoke(type, satisfaction, angry, delta, channel, served);
    }

    /// <summary>À appeler à chaque paiement d'un repas au restaurant (voir EatingSpot.PayForMeal) — pour le gain resto par type de monstre.</summary>
    public void ReportMealRevenue(MonsterType type, int amount)
    {
        OnMealRevenue?.Invoke(type, amount);
    }
}
