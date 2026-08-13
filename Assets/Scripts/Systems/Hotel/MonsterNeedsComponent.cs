using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Suivi des besoins d'un monstre en temps réel.
/// Ajouté automatiquement par SpawnScheduler sur chaque monstre spawné.
/// Les niveaux décroissent selon NeedType.decayRate (valeur par seconde).
/// </summary>
public class MonsterNeedsComponent : MonoBehaviour
{
    [System.Serializable]
    public class NeedState
    {
        public NeedType type;
        [Range(0f, 1f)] public float level = 1f;

        // Valeurs overridées par MonsterData (0 = utilise le NeedType)
        [HideInInspector] public float decayRateOverride;
        [HideInInspector] public float unsatisfiedThresholdOverride;
        [HideInInspector] public float criticalThresholdOverride;

        public float ActiveDecayRate             => decayRateOverride > 0f            ? decayRateOverride            : type.decayRate;
        public float ActiveUnsatisfiedThreshold  => unsatisfiedThresholdOverride > 0f ? unsatisfiedThresholdOverride : type.unsatisfiedThreshold;
        public float ActiveCriticalThreshold     => criticalThresholdOverride > 0f    ? criticalThresholdOverride    : type.criticalThreshold;

        public bool IsUnsatisfied => level < ActiveUnsatisfiedThreshold;
        public bool IsCritical    => level < ActiveCriticalThreshold;
    }

    [Header("État courant (lecture seule en jeu)")]
    [SerializeField] List<NeedState> _needs = new();

    public IReadOnlyList<NeedState> Needs => _needs;

    SatisfactionComponent _satisfaction;
    bool _decayActive;

    void Awake()
    {
        _satisfaction = GetComponent<SatisfactionComponent>();
    }

    /// <summary>
    /// Initialise les besoins à partir du MonsterData.
    /// Appelé par SpawnScheduler après l'instantiation.
    /// </summary>
    public void Initialize(MonsterData data)
    {
        _needs.Clear();
        if (data.needs == null) return;

        foreach (var needType in data.needs)
        {
            if (needType == null) continue;

            var state = new NeedState { type = needType, level = 1f };

            // Applique les overrides du MonsterData si présents
            if (data.needOverrides != null)
            {
                foreach (var o in data.needOverrides)
                {
                    if (o.needType != needType) continue;
                    if (o.decayRate > 0f)             state.decayRateOverride             = o.decayRate;
                    if (o.unsatisfiedThreshold > 0f)  state.unsatisfiedThresholdOverride  = o.unsatisfiedThreshold;
                    if (o.criticalThreshold > 0f)     state.criticalThresholdOverride     = o.criticalThreshold;
                    break;
                }
            }

            _needs.Add(state);
        }
    }

    /// <summary>Active la dégradation des besoins. Appelé quand le monstre a une chambre assignée.</summary>
    public void ActivateDecay() => _decayActive = true;

    /// <summary>Force le niveau d'un besoin (ex: visiteur repas qui arrive déjà affamé).</summary>
    public void SetNeedLevel(NeedType type, float level)
    {
        foreach (var need in _needs)
            if (need.type == type) { need.level = Mathf.Clamp01(level); return; }
    }

    void Update()
    {
        if (debugTriggerHunger && debugNeedType != null)
        {
            debugTriggerHunger = false;
            foreach (var need in _needs)
                if (need.type == debugNeedType) { need.level = 0f; break; }
        }

        if (!_decayActive) return;

        foreach (var need in _needs)
        {
            need.level = Mathf.Clamp01(need.level - need.ActiveDecayRate * Time.deltaTime);

            if (need.IsCritical && _satisfaction != null)
                _satisfaction.ApplyDecay(need.type.satisfactionDecayPerSecond * Time.deltaTime);
        }
    }

    /// <summary>
    /// Remplit un besoin du type donné d'une quantité [0-1].
    /// waitTime  : temps écoulé depuis que le monstre attendait ce service (secondes).
    /// quality   : qualité du service [0-1] (ex. résultat de la jauge cuisine).
    /// Bonus de satisfaction si waitTime ≤ goodServiceWaitTime.
    /// Bonus de satisfaction si quality ≥ goodQualityThreshold.
    /// Pénalité de satisfaction si waitTime > goodServiceWaitTime.
    /// Pénalité de satisfaction si quality < goodQualityThreshold.
    /// Retourne false si ce monstre n'a pas ce besoin.
    /// </summary>
    public bool FulfillNeed(NeedType needType, float amount, float waitTime = 0f, float quality = 1f)
    {
        foreach (var need in _needs)
        {
            if (need.type != needType) continue;

            need.level = Mathf.Clamp01(need.level + amount);

            if (_satisfaction == null) return true;

            // Temps d'attente
            if (waitTime <= needType.goodServiceWaitTime)
                _satisfaction.ApplyBonus(needType.satisfactionBonus);
            else
                _satisfaction.ApplyDecay(needType.satisfactionPenalty);

            // Qualité du service
            if (quality >= needType.goodQualityThreshold)
                _satisfaction.ApplyBonus(needType.satisfactionBonus);
            else
                _satisfaction.ApplyDecay(needType.satisfactionPenalty);

            return true;
        }
        return false;
    }

    /// <summary>
    /// Retourne le besoin le plus urgent (niveau le plus bas), ou null.
    /// </summary>
    public NeedState GetMostUrgentNeed()
    {
        NeedState worst = null;
        foreach (var need in _needs)
        {
            if (worst == null || need.level < worst.level)
                worst = need;
        }
        return worst;
    }

    /// <summary>Retourne l'état d'un besoin par type, ou null si le monstre n'a pas ce besoin.</summary>
    public NeedState GetNeedState(NeedType needType)
    {
        foreach (var need in _needs)
            if (need.type == needType) return need;
        return null;
    }

    /// <summary>Retourne true si un besoin AUTRE que celui passé est critique.</summary>
    public bool HasOtherCriticalNeed(NeedType except)
    {
        foreach (var need in _needs)
            if (need.type != except && need.IsCritical) return true;
        return false;
    }

    /// <summary>Retourne true si au moins un besoin est insatisfait.</summary>
    public bool HasUnsatisfiedNeed()
    {
        foreach (var need in _needs)
            if (need.IsUnsatisfied) return true;
        return false;
    }

    // ─── Debug ────────────────────────────────────────────────────

    [Header("Debug (Play Mode)")]
    [Tooltip("NeedType à utiliser pour les tests ci-dessous")]
    public NeedType debugNeedType;

    [Tooltip("Coche cette case en Play Mode pour vider instantanément le besoin sélectionné")]
    public bool debugTriggerHunger;

    [ContextMenu("Debug — Remplir besoin (bon service)")]
    void Debug_FulfillGood()
    {
        if (debugNeedType == null) { return; }
        FulfillNeed(debugNeedType, 0.9f, waitTime: 10f, quality: 0.8f);
    }

    [ContextMenu("Debug — Remplir besoin (mauvais service)")]
    void Debug_FulfillBad()
    {
        if (debugNeedType == null) { return; }
        FulfillNeed(debugNeedType, 0.5f, waitTime: 120f, quality: 0.2f);
    }

    [ContextMenu("Debug — Vider tous les besoins")]
    void Debug_DrainAll()
    {
        foreach (var need in _needs) need.level = 0f;
    }

    [ContextMenu("Debug — Afficher état")]
    void Debug_PrintState()
    {
    }
}
