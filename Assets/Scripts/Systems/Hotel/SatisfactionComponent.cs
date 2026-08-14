using UnityEngine;

/// <summary>
/// Score de satisfaction global du monstre [0-100].
/// Décroit quand des besoins sont critiques (via MonsterNeedsComponent).
/// Un score à 0 déclenche le départ du monstre.
/// </summary>
public class SatisfactionComponent : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Score de satisfaction initial")]
    [Range(0f, 100f)]
    public float initialSatisfaction = 80f;

    [Tooltip("En dessous de ce seuil le monstre part (0 = jamais)")]
    [Range(0f, 100f)]
    public float leaveThreshold = 10f;

    [Header("État courant")]
    [SerializeField, Range(0f, 100f)] float _satisfaction;

    public float Value => _satisfaction;
    public float Normalized => _satisfaction / 100f;

    public bool WantsToLeave => _satisfaction <= leaveThreshold;

    /// <summary>Fired quand le monstre décide de partir (satisfaction <= leaveThreshold).</summary>
    public event System.Action OnWantsToLeave;

    bool _leaveFired;

    void Awake()
    {
        var cfg = HotelConfig.Satisfaction;
        if (cfg != null)
        {
            initialSatisfaction = cfg.initialSatisfaction;
            leaveThreshold      = cfg.leaveThreshold;

            // Bonus de Confort : chaque point de confort améliore la satisfaction initiale
            float comfort = HotelStatsManager.Instance?.TotalComfort ?? 0f;
            if (comfort > 0f)
                initialSatisfaction = Mathf.Min(100f, initialSatisfaction + comfort * cfg.comfortToSatisfactionBonus);
        }
        _satisfaction = initialSatisfaction;
    }

    /// <summary>
    /// Applique une perte de satisfaction (valeur positive = perte).
    /// Appelé par MonsterNeedsComponent pour chaque besoin critique.
    /// </summary>
    public void ApplyDecay(float amount)
    {
        _satisfaction = Mathf.Clamp(_satisfaction - amount, 0f, 100f);

        if (!_leaveFired && WantsToLeave)
        {
            _leaveFired = true;
            OnWantsToLeave?.Invoke();
        }
    }

    /// <summary>Augmente la satisfaction (ex: service rendu, besoin satisfait).</summary>
    public void ApplyBonus(float amount)
    {
        _satisfaction = Mathf.Clamp(_satisfaction + amount, 0f, 100f);
        _leaveFired = _satisfaction > leaveThreshold ? false : _leaveFired;
    }
}
