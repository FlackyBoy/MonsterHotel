using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Gère tous les employés embauchés : spawn, salaires, limite, démissions.
/// Attache sur un GameObject dans _Managers.
/// </summary>
public class EmployeeManager : MonoBehaviour
{
    public static EmployeeManager Instance { get; private set; }

    [Header("Templates par rôle")]
    [Tooltip("Un EmployeeData par rôle — contient la liste des skins disponibles")]
    public EmployeeData[] employeeTemplates;

    [Header("Génération candidats")]
    [Tooltip("Nombre de candidats Réception générés dans la liste")]
    public int candidatesReception = 2;
    [Tooltip("Nombre de candidats Nettoyage générés dans la liste")]
    public int candidatesCleaning  = 2;
    [Tooltip("Nombre de candidats Cuisinier générés dans la liste")]
    public int candidatesCook      = 2;

    [Header("Spawn")]
    public Transform spawnPoint;

    // ─── Privé ────────────────────────────────────────────────────

    readonly List<EmployeeInstance>                       _hired       = new();
    readonly Dictionary<EmployeeData, Queue<GameObject>>  _prefabPools = new();

    // ─── API publique ─────────────────────────────────────────────

    public IReadOnlyList<EmployeeInstance> Hired => _hired;

    public int MaxEmployees
    {
        get
        {
            var cfg = HotelConfig.Employee;
            if (cfg != null && cfg.employeeMaxOverride > 0) return cfg.employeeMaxOverride;
            int rooms = RoomInstance.All.Count;
            float ratio = cfg != null ? cfg.employeeRoomRatio : 3f;
            return Mathf.Max(1, Mathf.FloorToInt(rooms / ratio));
        }
    }

    public bool CanHire => _hired.Count < MaxEmployees;

    public event System.Action OnRosterChanged;

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnNewDay += OnNewDay;
    }

    void OnDestroy()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnNewDay -= OnNewDay;
    }

    // ─── Fin de journée ───────────────────────────────────────────

    void OnNewDay(int day) => PaySalaries();

    void PaySalaries()
    {
        for (int i = _hired.Count - 1; i >= 0; i--)
        {
            var emp = _hired[i];
            if (emp == null) { _hired.RemoveAt(i); continue; }

            if (!EconomyManager.Instance.TrySpend(emp.Data.dailySalary))
            {
                Debug.Log($"[Employés] Impossible de payer {emp.Data.employeeName} → démission.");
                _hired.RemoveAt(i);
                emp.Resign();
            }
        }
        OnRosterChanged?.Invoke();
    }

    // ─── Embauche / Licenciement ──────────────────────────────────

    public bool Hire(EmployeeData data, GameObject assignedPrefab = null)
    {
        if (data == null) return false;
        if (!CanHire)
        {
            Debug.Log("[Employés] Limite d'employés atteinte.");
            return false;
        }
        if (!EconomyManager.Instance.TrySpend(data.hiringFee))
        {
            Debug.Log($"[Employés] Fonds insuffisants pour embaucher {data.employeeName}.");
            return false;
        }

        Vector3 basePos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        GameObject go;

        var prefab = assignedPrefab;
        if (prefab != null)
        {
            // Préserve le décalage local X/Z défini sur le prefab (ex : léger recentrage visuel),
            // mais pas le Y — la hauteur de spawn doit toujours venir du spawnPoint (le sol), pas
            // du Y enregistré sur le prefab (souvent juste un artefact de la scène où il a été
            // sauvegardé, contrairement à la rotation qui peut légitimement varier).
            Vector3 offset = prefab.transform.position;
            Vector3 pos    = new Vector3(basePos.x + offset.x, basePos.y, basePos.z + offset.z);
            go = Instantiate(prefab, pos, prefab.transform.rotation);
        }
        else
        {
            // Repli de secours si characterPrefabs[] est vide ou pointe vers un prefab manquant
            // (référence cassée) — sans NavMeshAgent, MonsterMover ne fait plus aucun pathfinding
            // et marche en ligne droite (traverse les murs), ce qui ressemble à tort à un bug
            // NavMesh. Ajouté ici pour que le symptôme reste cohérent avec le reste du roster
            // plutôt que de faire perdre du temps à chercher au mauvais endroit.
            Debug.LogWarning($"[Employés] {data.employeeName} — aucun prefab assigné (characterPrefabs vide ou référence cassée sur '{data.name}'), utilise une capsule de repli.");
            go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = basePos;
            go.AddComponent<NavMeshAgent>();
        }

        go.name = data.employeeName;

        var instance = go.GetComponent<EmployeeInstance>() ?? go.AddComponent<EmployeeInstance>();
        instance.Init(data);
        AddRoleAI(go, data.role);

        _hired.Add(instance);
        OnRosterChanged?.Invoke();

        Debug.Log($"[Employés] {data.employeeName} embauché(e) ({data.role}) — {data.dailySalary}G/jour.");
        return true;
    }

    public void Fire(EmployeeInstance emp)
    {
        if (emp == null) return;
        _hired.Remove(emp);
        emp.Resign();
        OnRosterChanged?.Invoke();
    }

    public void OnEmployeeResigned(EmployeeInstance emp)
    {
        _hired.Remove(emp);
        OnRosterChanged?.Invoke();
    }

    // ─── Pool de skins ────────────────────────────────────────────

    /// <summary>
    /// Retourne le prochain prefab visuel de la pool pour ce template.
    /// Auto-reset (shuffle) quand la pool est épuisée.
    /// </summary>
    public GameObject GetNextPrefab(EmployeeData data)
    {
        if (data?.characterPrefabs == null || data.characterPrefabs.Length == 0) return null;

        if (!_prefabPools.TryGetValue(data, out var pool) || pool.Count == 0)
            pool = RebuildPool(data);

        return pool.Dequeue();
    }

    Queue<GameObject> RebuildPool(EmployeeData data)
    {
        var list = new List<GameObject>(data.characterPrefabs);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        var pool = new Queue<GameObject>(list);
        _prefabPools[data] = pool;
        return pool;
    }

    // ─── Génération de candidats ──────────────────────────────────

    static readonly string[] FirstNames = { "Ada", "Bruno", "Clara", "Denis", "Eva", "Félix", "Gina", "Hugo", "Iris", "Jules" };
    static readonly string[] LastNames  = { "Mort", "Cendre", "Spectre", "Larve", "Croc", "Sang", "Nuit", "Brume", "Os", "Ombre" };

    public List<CandidateInfo> GenerateCandidates()
    {
        var list = new List<CandidateInfo>();

        float salaryPerRating = HotelConfig.Employee != null ? HotelConfig.Employee.employeeSalaryPerRating : 8f;
        float feeMultiplier   = HotelConfig.Employee != null ? HotelConfig.Employee.employeeFeeMultiplier   : 1.5f;

        var distribution = new (EmployeeRole role, int count)[]
        {
            (EmployeeRole.Reception, candidatesReception),
            (EmployeeRole.Cleaning,  candidatesCleaning),
            (EmployeeRole.Cook,      candidatesCook),
        };

        foreach (var (role, count) in distribution)
        {
            EmployeeData template = null;
            if (employeeTemplates != null)
            {
                var matching = System.Array.FindAll(employeeTemplates, t => t != null && t.role == role);
                if (matching.Length > 0)
                    template = matching[Random.Range(0, matching.Length)];
            }

            for (int i = 0; i < count; i++)
            {
                int    rating    = Random.Range(1, 21);
                int    salary    = Mathf.RoundToInt(rating * salaryPerRating + Random.Range(-5f, 5f));
                int    fee       = Mathf.RoundToInt(salary * feeMultiplier);
                string firstName = FirstNames[Random.Range(0, FirstNames.Length)];
                string lastName  = LastNames[Random.Range(0, LastNames.Length)];

                list.Add(new CandidateInfo
                {
                    displayName    = $"{firstName} {lastName}",
                    role           = role,
                    rating         = rating,
                    dailySalary    = salary,
                    hiringFee      = fee,
                    template       = template,
                    assignedPrefab = template != null ? GetNextPrefab(template) : null
                });
            }
        }
        return list;
    }

    // ─── Interne ──────────────────────────────────────────────────

    static void AddRoleAI(GameObject go, EmployeeRole role)
    {
        switch (role)
        {
            case EmployeeRole.Reception:
                if (go.GetComponent<ReceptionEmployeeAI>() == null) go.AddComponent<ReceptionEmployeeAI>(); break;
            case EmployeeRole.Cleaning:
                if (go.GetComponent<CleaningEmployeeAI>() == null) go.AddComponent<CleaningEmployeeAI>(); break;
            case EmployeeRole.Cook:
                if (go.GetComponent<CookEmployeeAI>() == null) go.AddComponent<CookEmployeeAI>(); break;
        }
    }

    // ─── Nested type ─────────────────────────────────────────────

    public class CandidateInfo
    {
        public string       displayName;
        public EmployeeRole role;
        public int          rating;
        public int          dailySalary;
        public int          hiringFee;
        public EmployeeData template;
        public GameObject   assignedPrefab;
    }
}
