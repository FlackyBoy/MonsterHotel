using UnityEngine;

/// <summary>
/// Gère l'heure in-game (cycle 24h).
/// Attache-le sur un GameObject dans _Managers.
/// </summary>
public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [Header("Cycle jour/nuit")]
    [Tooltip("Durée d'une journée complète en secondes réels")]
    public float dayDuration = 300f; // 5 minutes = 1 journée

    [Tooltip("Heure de départ (0-24)")]
    public float startHour = 8f;

    // ─── Privé ────────────────────────────────────────────────────

    float _elapsed;
    float _prevElapsed;

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        var cfg = HotelConfig.Instance;
        if (cfg != null)
        {
            dayDuration = cfg.dayDuration;
            startHour   = cfg.startHour;
        }

        _elapsed = _prevElapsed = startHour / 24f * dayDuration;
    }

    void Update()
    {
        _prevElapsed = _elapsed;
        _elapsed    += Time.deltaTime;

        if (_elapsed >= dayDuration)
        {
            _elapsed -= dayDuration;
            Day++;
            OnNewDay?.Invoke(Day);
        }
    }

    // ─── API publique ─────────────────────────────────────────────

    /// <summary>Heure actuelle en float (0.0 – 24.0).</summary>
    public float Hour => _elapsed / dayDuration * 24f;

    /// <summary>Heure actuelle sous forme "08h30".</summary>
    public string TimeString
    {
        get
        {
            float h = Hour;
            int hours   = (int)h;
            int minutes = (int)((h - hours) * 60f);
            return $"{hours:D2}h{minutes:D2}";
        }
    }

    /// <summary>Heure actuelle statique pour les logs (retourne "--h--" si pas d'instance).</summary>
    public static string Now => Instance != null ? Instance.TimeString : "--h--";

    /// <summary>Numéro du jour actuel (commence à 1).</summary>
    public int Day { get; private set; } = 1;

    /// <summary>Déclenché à chaque nouveau jour. Paramètre = numéro du jour.</summary>
    public event System.Action<int> OnNewDay;

    /// <summary>Jour ou nuit selon l'heure actuelle.</summary>
    public SpawnTime CurrentSpawnTime =>
        Hour >= 6f && Hour < 20f ? SpawnTime.DayOnly : SpawnTime.NightOnly;
}
