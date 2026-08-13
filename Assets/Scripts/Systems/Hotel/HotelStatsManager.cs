using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Agrège les stats globales de l'hôtel (Confort, Renommée) en fonction des décorations posées.
/// Placer dans _Managers en scène.
/// </summary>
public class HotelStatsManager : MonoBehaviour
{
    public static HotelStatsManager Instance { get; private set; }

    readonly List<DecorationInstance> _decorations = new();

    [Header("Stats (lecture seule — mis à jour en Play)")]
    [SerializeField] float _totalComfort;
    [SerializeField] float _totalRenown;

    /// <summary>Somme des comfortBonus de toutes les décorations posées.</summary>
    public float TotalComfort => _totalComfort;

    /// <summary>Somme des renownBonus de toutes les décorations posées.</summary>
    public float TotalRenown  => _totalRenown;

    /// <summary>Déclenché chaque fois que Confort ou Renommée change.</summary>
    public event System.Action OnStatsChanged;

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
        _totalRenown  = renown;
        OnStatsChanged?.Invoke();
    }
}
