using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Roue d'interaction affichée quand le joueur est proche d'un employé et appuie sur Interact.
/// Options : Licencier / Envoyer en pause / Reprendre le travail.
/// Setup : placer ce composant sur un Canvas WorldSpace enfant de l'employé.
/// </summary>
public class EmployeeInteractionWheel : MonoBehaviour
{
    [Header("Références UI")]
    [Tooltip("Panel racine de la roue (activé/désactivé)")]
    public GameObject wheelRoot;
    [Tooltip("Bouton Licencier")]
    public Button fireButton;
    [Tooltip("Bouton Pause")]
    public Button breakButton;
    [Tooltip("Bouton Reprendre")]
    public Button workButton;
    [Tooltip("Label état de l'employé")]
    public TextMeshProUGUI stateLabel;

    [Header("Détection joueur")]
    [Tooltip("Distance maximale pour ouvrir la roue")]
    public float interactRange = 2f;

    EmployeeInstance _employee;
    bool             _open;

    void Awake()
    {
        _employee = GetComponentInParent<EmployeeInstance>();

        if (fireButton  != null) fireButton.onClick.AddListener(OnFire);
        if (breakButton != null) breakButton.onClick.AddListener(OnBreak);
        if (workButton  != null) workButton.onClick.AddListener(OnWork);

        if (wheelRoot != null) wheelRoot.SetActive(false);
    }

    void Update()
    {
        if (_employee == null) return;

        // Ferme si le joueur s'éloigne
        if (_open && !IsAnyPlayerNear())
            Close();

        // Oriente le canvas vers la caméra
        if (_open && Camera.main != null)
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
    }

    // ─── API publique ─────────────────────────────────────────────

    public void Toggle()
    {
        if (_open) Close();
        else Open();
    }

    public void Open()
    {
        if (wheelRoot == null) return;
        _open = true;
        wheelRoot.SetActive(true);
        RefreshButtons();
    }

    public void Close()
    {
        if (wheelRoot == null) return;
        _open = false;
        wheelRoot.SetActive(false);
    }

    // ─── Boutons ──────────────────────────────────────────────────

    void OnFire()
    {
        Close();
        EmployeeManager.Instance?.Fire(_employee);
    }

    void OnBreak()
    {
        _employee.GoOnBreak();
        RefreshButtons();
    }

    void OnWork()
    {
        _employee.ForceWork();
        RefreshButtons();
        Close();
    }

    void RefreshButtons()
    {
        if (stateLabel != null)
            stateLabel.text = StateLabel(_employee.State);

        bool onBreak   = _employee.State == EmployeeState.OnBreak || _employee.State == EmployeeState.GoingToBreak;
        bool canWork   = onBreak;
        bool canBreak  = !onBreak && _employee.State != EmployeeState.Resigning;

        if (breakButton != null) breakButton.interactable = canBreak;
        if (workButton  != null) workButton.interactable  = canWork;
    }

    bool IsAnyPlayerNear()
    {
        foreach (var placer in RoomPlacer.All)
        {
            if (placer == null) continue;
            if (Vector3.Distance(transform.position, placer.transform.position) <= interactRange)
                return true;
        }
        return false;
    }

    static string StateLabel(EmployeeState state) => state switch
    {
        EmployeeState.Working      => "Au travail",
        EmployeeState.GoingToWork  => "Rejoint le poste",
        EmployeeState.OnBreak      => "En pause",
        EmployeeState.GoingToBreak => "Part en pause",
        EmployeeState.Idle         => "En attente",
        EmployeeState.Resigning    => "Démissionne...",
        _                          => ""
    };
}
