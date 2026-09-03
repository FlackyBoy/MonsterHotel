using UnityEngine;

/// <summary>
/// Comportement de l'humain une fois possédé par un fantôme (G8) : vomit aléatoirement, déclenche des
/// quirks visuels aléatoires (lévitation / cogne la tête contre un mur), et gère le départ du fantôme —
/// checkout normal (fenêtre horaire) ou fuite anticipée "fatiguée" (chance aléatoire indépendante).
/// Comme il n'a jamais de RoomInstance/ReservationSystem, ce composant appelle directement
/// HotelStatsManager.ReportGuestDeparture() pour le checkout normal (même méthode qu'un checkout
/// classique en coulisses). Aucun des deux départs ne détruit l'humain : le fantôme quitte le corps
/// (PossessableHuman.Unpossess()), l'humain reste dans l'hôtel (calme après un checkout normal, en
/// panique après une fuite anticipée — voir PossessableHuman.Catch/PlaceInCage).
/// Ajouté par PossessableHuman.Possess() — jamais directement.
/// </summary>
public class PossessedHumanQuirks : MonoBehaviour
{
    MonsterData _data;
    Animator    _animator;

    float _vomitTimer;
    float _quirkTimer;
    float _earlyLeaveTimer;
    bool  _departing;

    /// <summary>Time.time à partir duquel le départ (normal) devient possible — voir Init() (même
    /// calcul de durée de séjour que ReservationSystem.TryAssignRoom() pour un client chambre
    /// classique, stayMinNights/stayMaxNights de MonsterData). Sans effet sur la fuite anticipée, qui
    /// peut survenir à tout moment de la possession.</summary>
    float _eligibleForDepartureAt;

    /// <summary>ghostData fournit tout (intervalles de vomi/quirks/fuite anticipée, débris, fenêtre de
    /// checkout, durée de séjour) — voir MonsterData section "Fantôme — humain possédé" + section
    /// séjour commune.</summary>
    public void Init(MonsterData data)
    {
        _data     = data;
        _animator = GetComponentInChildren<Animator>();

        ResetVomitTimer();
        _quirkTimer      = RandomQuirkInterval();
        _earlyLeaveTimer = data.possessionEarlyLeaveCheckInterval;

        int nights = Random.Range(data.stayMinNights, data.stayMaxNights + 1);
        float dayDuration = TimeManager.Instance != null ? TimeManager.Instance.dayDuration : 300f;
        _eligibleForDepartureAt = Time.time + nights * dayDuration;
    }

    void Update()
    {
        if (_data == null || _departing) return;

        _vomitTimer -= Time.deltaTime;
        if (_vomitTimer <= 0f)
        {
            Vomit();
            ResetVomitTimer();
        }

        _quirkTimer -= Time.deltaTime;
        if (_quirkTimer <= 0f)
        {
            TriggerRandomQuirk();
            _quirkTimer = RandomQuirkInterval();
        }

        CheckEarlyLeave();
        if (_departing) return; // la fuite anticipée ci-dessus a pu déclencher ce frame — n'enchaîne pas sur un checkout normal
        CheckDeparture();
    }

    // ─── Vomi ─────────────────────────────────────────────────────

    void ResetVomitTimer() => _vomitTimer = Random.Range(_data.possessionVomitIntervalMin, _data.possessionVomitIntervalMax);

    void Vomit()
    {
        Debug.Log($"[Fantôme] {name} (possédé) vomit.");
        _animator?.SetTrigger("Vomit");
        if (_data.possessionVomitDebrisPrefab != null)
            Instantiate(_data.possessionVomitDebrisPrefab, transform.position, Quaternion.identity);
    }

    // ─── Quirks visuels aléatoires (lévitation / cogne la tête) ────

    float RandomQuirkInterval() => Random.Range(_data.possessionQuirkIntervalMin, _data.possessionQuirkIntervalMax);

    /// <summary>
    /// Tirage uniforme parmi les quirks actuellement éligibles — HeadBump seulement si un mur est à
    /// proximité (IsNearWall), Levitate toujours éligible. Le CHAÎNAGE de chaque quirk vers sa suite
    /// définie (ex: HeadSlam Start→Loop→End) se fait entièrement dans l'Animator Controller (transitions
    /// à Exit Time) — ce composant ne fait que déclencher le Trigger de départ.
    /// </summary>
    void TriggerRandomQuirk()
    {
        if (_animator == null) return;

        bool nearWall = IsNearWall();
        int options = nearWall ? 2 : 1;
        int pick = Random.Range(0, options);

        if (pick == 0) _animator.SetTrigger("Levitate");
        else           _animator.SetTrigger("HeadBump");
    }

    /// <summary>Scan des 4 cellules voisines dans la grille — vrai si l'une d'elles est un mur.</summary>
    bool IsNearWall()
    {
        var grid = GridManager.Instance;
        if (grid == null) return false;

        var here = grid.WorldToCell(transform.position);
        if (here == null) return false;

        Vector2Int[] offsets = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };
        foreach (var o in offsets)
        {
            var cell = grid.GetCell(here.coords + o);
            if (cell != null && cell.type == CellType.Wall) return true;
        }
        return false;
    }

    // ─── Fuite anticipée du fantôme ("fatigué") ────────────────────

    /// <summary>
    /// Tirage périodique indépendant du checkout normal — le fantôme peut quitter le corps avant
    /// l'heure. Contrairement au checkout normal, aucune stat de départ n'est reportée (ce n'est pas un
    /// vrai départ de l'hôtel, l'humain reste et panique) et Unpossess(panicked: true) est appelé au
    /// lieu du chemin calme.
    /// </summary>
    void CheckEarlyLeave()
    {
        _earlyLeaveTimer -= Time.deltaTime;
        if (_earlyLeaveTimer > 0f) return;
        _earlyLeaveTimer = _data.possessionEarlyLeaveCheckInterval;

        if (Random.value >= _data.possessionEarlyLeaveChance) return;

        _departing = true;
        Debug.Log($"[Fantôme] {name} — fatigué, le fantôme quitte le corps avant l'heure. L'humain panique.");
        GetComponent<PossessableHuman>()?.Unpossess(panicked: true);
    }

    // ─── Checkout normal (fenêtre horaire) ─────────────────────────

    void CheckDeparture()
    {
        if (Time.time < _eligibleForDepartureAt) return;

        float hour = TimeManager.Instance != null ? TimeManager.Instance.Hour : 0f;
        bool inWindow = _data.checkoutWindowStart <= _data.checkoutWindowEnd
            ? (hour >= _data.checkoutWindowStart && hour < _data.checkoutWindowEnd)
            : (hour >= _data.checkoutWindowStart || hour < _data.checkoutWindowEnd);
        if (!inWindow) return;

        _departing = true;
        float satisfaction = GetComponent<SatisfactionComponent>()?.Value ?? 50f;
        HotelStatsManager.Instance?.ReportGuestDeparture(
            _data.monsterType, satisfaction, angry: false, GuestChannel.Room, served: true);

        Debug.Log($"[Fantôme] {name} — le fantôme quitte le corps possédé (checkout), l'humain reste.");
        GetComponent<PossessableHuman>()?.Unpossess();
    }
}
