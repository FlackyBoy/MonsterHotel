using UnityEngine;

public enum EmployeeState { Idle, GoingToWork, Working, GoingToBreak, OnBreak, Resigning }

/// <summary>
/// Composant posé sur chaque employé instancié.
/// Gère la machine d'état, la fatigue, le bien-être et les interactions joueur.
/// </summary>
public class EmployeeInstance : MonoBehaviour
{
    public static readonly System.Collections.Generic.HashSet<EmployeeInstance> All = new();

    // ─── Données ──────────────────────────────────────────────────

    public EmployeeData Data { get; private set; }

    // ─── État ─────────────────────────────────────────────────────

    public EmployeeState State      { get; private set; } = EmployeeState.Idle;

    /// <summary>
    /// Quand true, la pause automatique ne se déclenche pas.
    /// À activer par l'IA pendant une tâche critique (ex : cycle de service du cuisinier).
    /// </summary>
    public bool BlockBreak { get; set; }

    [SerializeField, Range(0f, 100f)] float _fatigue   = 0f;
    [SerializeField, Range(0f, 100f)] float _wellbeing = 100f;

    public float Fatigue   => _fatigue;
    public float Wellbeing => _wellbeing;

    // ─── Timers internes ──────────────────────────────────────────

    float _workTimer;   // temps travaillé depuis la dernière pause
    float _breakTimer;  // temps de pause restant

    // ─── Config (depuis HotelConfig) ──────────────────────────────

    float _fatigueRate;
    float _recoveryRate;
    float _resignThreshold;
    float _forceWorkPenalty;

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake()
    {
        All.Add(this);
        var cfg = HotelConfig.Instance;
        if (cfg != null)
        {
            _fatigueRate      = cfg.employeeFatigueRate;
            _recoveryRate     = cfg.employeeRecoveryRate;
            _resignThreshold  = cfg.employeeResignThreshold;
            _forceWorkPenalty = cfg.employeeForceWorkPenalty;
        }
        else
        {
            _fatigueRate = 2f; _recoveryRate = 5f; _resignThreshold = 10f; _forceWorkPenalty = 20f;
        }
    }

    void OnDestroy() => All.Remove(this);

    // ─── Init ─────────────────────────────────────────────────────

    public void Init(EmployeeData data)
    {
        Data        = data;
        name        = data.employeeName;
        _workTimer  = 0f;
        _breakTimer = 0f;

        ApplyRatingScaling();

        var mover = GetComponent<MonsterMover>() ?? gameObject.AddComponent<MonsterMover>();
        mover.moveSpeed = data.moveSpeed;

        SetState(EmployeeState.Idle);
    }

    void ApplyRatingScaling()
    {
        if (Data == null) return;

        var   cfg = HotelConfig.Instance;
        float t   = (Data.rating - 1f) / 19f; // 0 = note 1, 1 = note 20

        float breakMin      = cfg != null ? cfg.employeeBreakIntervalMinMult  : 0.5f;
        float breakMax      = cfg != null ? cfg.employeeBreakIntervalMaxMult  : 1.5f;
        float speedMin      = cfg != null ? cfg.employeeSpeedMinMult          : 0.75f;
        float speedMax      = cfg != null ? cfg.employeeSpeedMaxMult          : 1.25f;
        float fatigueMin    = cfg != null ? cfg.employeeFatigueRateMinMult    : 1.5f;
        float fatigueMax    = cfg != null ? cfg.employeeFatigueRateMaxMult    : 0.5f;
        float recoveryMin   = cfg != null ? cfg.employeeRecoveryRateMinMult   : 0.75f;
        float recoveryMax   = cfg != null ? cfg.employeeRecoveryRateMaxMult   : 1.25f;

        Data.breakInterval *= Mathf.Lerp(breakMin,   breakMax,   t);
        Data.moveSpeed     *= Mathf.Lerp(speedMin,   speedMax,   t);
        _fatigueRate       *= Mathf.Lerp(fatigueMin, fatigueMax, t);
        _recoveryRate      *= Mathf.Lerp(recoveryMin,recoveryMax,t);
    }

    // ─── Update ───────────────────────────────────────────────────

    void Update()
    {
        if (State == EmployeeState.Resigning) return;

        // UpdateNeeds() et CheckResignation() sont désactivés avec le reste du système de
        // pause (pas de break room en scène). UpdateSchedule() reste actif : il gère aussi
        // le passage Idle → GoingToWork selon les horaires, indépendant de la pause.
        // UpdateNeeds();
        UpdateSchedule();
        // CheckResignation();
    }

    // DÉSACTIVÉ avec le reste du système de pause — cf. commentaire dans Update().
    // void UpdateNeeds()
    // {
    //     float dt = Time.deltaTime;
    //
    //     if (State == EmployeeState.Working || State == EmployeeState.GoingToWork)
    //     {
    //         _fatigue   = Mathf.Min(100f, _fatigue + _fatigueRate * dt / 60f);
    //     }
    //     else if (State == EmployeeState.OnBreak || State == EmployeeState.GoingToBreak)
    //     {
    //         _fatigue   = Mathf.Max(0f, _fatigue   - _recoveryRate * dt / 60f);
    //         _wellbeing = Mathf.Min(100f, _wellbeing + _recoveryRate * 0.5f * dt / 60f);
    //     }
    // }

    void UpdateSchedule()
    {
        if (Data == null) return;

        float hour = TimeManager.Instance?.Hour ?? 12f;
        bool inWorkHours = hour >= Data.workStartHour && hour < Data.workEndHour;

        if (!inWorkHours && State != EmployeeState.OnBreak && State != EmployeeState.GoingToBreak && State != EmployeeState.Idle)
        {
            SetState(EmployeeState.Idle);
            return;
        }

        if (inWorkHours && State == EmployeeState.Idle)
        {
            SetState(EmployeeState.GoingToWork);
            return;
        }

        // DÉSACTIVÉ avec le reste du système de pause — cf. commentaire dans Update().
        // // Pause auto (différée si l'IA est en pleine tâche critique)
        // if (State == EmployeeState.Working)
        // {
        //     _workTimer += Time.deltaTime;
        //     if (_workTimer >= Data.breakInterval && !BlockBreak)
        //     {
        //         _workTimer = 0f;
        //         SetState(EmployeeState.GoingToBreak);
        //         return;
        //     }
        // }
        //
        // // Pause : décompte du timer, que l'employé soit arrivé à la salle de repos ou encore
        // // en chemin (GoingToBreak bascule immédiatement en OnBreak — le déplacement physique
        // // vers la salle de pause se poursuit en tâche de fond via le mover, indépendamment
        // // de la machine à état. Sans ça, rien ne fait jamais transiter GoingToBreak → OnBreak
        // // et l'employé reste bloqué indéfiniment).
        // if (State == EmployeeState.GoingToBreak)
        //     SetState(EmployeeState.OnBreak);
        //
        // if (State == EmployeeState.OnBreak)
        // {
        //     _breakTimer -= Time.deltaTime;
        //     if (_breakTimer <= 0f)
        //         SetState(inWorkHours ? EmployeeState.GoingToWork : EmployeeState.Idle);
        // }
    }

    // DÉSACTIVÉ avec le reste du système de pause — _wellbeing ne change plus jamais tant que
    // UpdateNeeds()/ForceWork() sont désactivés, donc ce seuil ne serait de toute façon jamais atteint.
    // void CheckResignation()
    // {
    //     if (_wellbeing <= _resignThreshold)
    //         Resign();
    // }

    // ─── API publique ─────────────────────────────────────────────

    // DÉSACTIVÉ avec le reste du système de pause — cf. commentaire dans Update().
    // Rendu no-op (plutôt que supprimé) pour que la roue d'interaction (EmployeeInteractionWheel)
    // continue de compiler sans mettre un employé dans un état bloqué (GoingToBreak sans promotion).
    /// <summary>Force l'employé à retourner au travail pendant sa pause → pénalité bien-être.</summary>
    public void ForceWork()
    {
        // if (State != EmployeeState.OnBreak && State != EmployeeState.GoingToBreak) return;
        // _wellbeing = Mathf.Max(0f, _wellbeing - _forceWorkPenalty);
        // _workTimer = 0f;
        // SetState(EmployeeState.GoingToWork);
    }

    /// <summary>Envoie l'employé en pause immédiatement.</summary>
    public void GoOnBreak()
    {
        // DÉSACTIVÉ avec le reste du système de pause — cf. commentaire dans Update().
        // if (State == EmployeeState.OnBreak || State == EmployeeState.GoingToBreak) return;
        // _workTimer  = 0f;
        // SetState(EmployeeState.GoingToBreak);
    }

    /// <summary>Démission : l'employé marche vers la sortie puis se détruit.</summary>
    public void Resign()
    {
        SetState(EmployeeState.Resigning);
        EmployeeManager.Instance?.OnEmployeeResigned(this);

        var mover = GetComponent<MonsterMover>();
        if (mover != null)
        {
            var spawn = FindAnyObjectByType<SpawnScheduler>();
            Vector3 exit = spawn != null ? spawn.spawnPoint.position : transform.position;
            mover.WalkToAndDestroy(exit);
        }
        else
        {
            Destroy(gameObject, 2f);
        }
    }

    // ─── Interne ──────────────────────────────────────────────────

    public void SetState(EmployeeState newState)
    {
        State = newState;

        // Branches GoingToBreak/OnBreak désactivées avec le reste du système de pause —
        // plus rien ne transite vers ces états tant qu'elles sont commentées ici, mais on les
        // laisse en commentaire (plutôt que supprimées) pour réactivation facile.
        // if (newState == EmployeeState.GoingToBreak)
        // {
        //     _breakTimer = Data != null ? Data.breakDuration : 30f;
        //     GoToBreakRoom();
        // }
        // else if (newState == EmployeeState.OnBreak)
        // {
        //     _breakTimer = Data != null ? Data.breakDuration : 30f;
        // }
    }

    // DÉSACTIVÉ avec le reste du système de pause — cf. commentaire dans Update().
    // void GoToBreakRoom()
    // {
    //     var mover = GetComponent<MonsterMover>();
    //     if (mover == null) return;
    //
    //     // Cherche dans les RoomInstance (chambre avec isBreakRoom)
    //     foreach (var room in RoomInstance.All)
    //     {
    //         if (room.Data != null && room.Data.isBreakRoom)
    //         {
    //             mover.MoveTo(room.EntryPoint, room.transform.position);
    //             return;
    //         }
    //     }
    //
    //     // Cherche dans les FacilityRoomInstance (salle commune marquée isBreakRoom)
    //     foreach (var facility in FacilityRoomInstance.All)
    //     {
    //         if (facility != null && facility.isBreakRoom)
    //         {
    //             mover.MoveTo(facility.transform.position);
    //             return;
    //         }
    //     }
    //     // Pas de salle de repos → l'employé reste sur place (récupération partielle)
    // }
}
