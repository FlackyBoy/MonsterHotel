using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panneau d'embauche des employés.
/// Affiche deux onglets : Candidats disponibles / Employés actuels.
/// Navigable à la manette (Navigate ↑↓, Submit, Cancel, NextTab/PrevTab) — scopé au joueur
/// qui l'a ouvert (voir EmployeeBoardTrigger, un panel par joueur).
/// Setup : placer sur un Panel dans le Canvas, assigner les références.
/// </summary>
public class EmployeeHiringPanel : MonoBehaviour
{
    // ─── Références UI ────────────────────────────────────────────

    [Header("Onglets")]
    public Button tabCandidatesButton;
    public Button tabHiredButton;

    [Header("Conteneurs")]
    public Transform candidatesContainer;
    public Transform hiredContainer;

    [Header("Prefabs slots")]
    public GameObject candidateSlotPrefab;
    public GameObject hiredSlotPrefab;

    [Header("Infos hôtel")]
    public TextMeshProUGUI employeeCountLabel;
    public TextMeshProUGUI goldLabel;
    public Button closeButton;

    [Header("Noms des actions dans la Player Action Map")]
    public string submitActionName   = "Submit";
    public string navigateActionName = "Navigate";
    public string cancelActionName   = "Cancel";
    public string nextTabActionName  = "NextTab";
    public string prevTabActionName  = "PrevTab";

    static readonly Color ColSelected = new(1f, 0.85f, 0f);
    static readonly Color ColNormal   = Color.white;

    // ─── Privé ────────────────────────────────────────────────────

    List<EmployeeManager.CandidateInfo> _candidates = new();
    bool _showingCandidates = true;

    PlayerInput _playerInput;
    readonly List<Button> _navButtons  = new();
    int  _selectedIndex = 0;
    int  _frameShown    = -1;

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake()
    {
        if (tabCandidatesButton != null) tabCandidatesButton.onClick.AddListener(() => SwitchTab(true));
        if (tabHiredButton      != null) tabHiredButton.onClick.AddListener(() => SwitchTab(false));
        if (closeButton         != null) closeButton.onClick.AddListener(Hide);
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (_playerInput == null || Time.frameCount == _frameShown) return;

        // Changement d'onglet
        if (WasActionPressed(nextTabActionName) || WasActionPressed(prevTabActionName))
        {
            SwitchTab(!_showingCandidates);
            return;
        }

        // Navigation verticale
        if (_navButtons.Count > 1)
        {
            var nav = _playerInput.actions.FindAction(navigateActionName, throwIfNotFound: false);
            if (nav != null && nav.WasPressedThisFrame())
            {
                float y = nav.ReadValue<Vector2>().y;
                if (y < -0.3f) SelectButton((_selectedIndex + 1) % _navButtons.Count);
                else if (y > 0.3f) SelectButton((_selectedIndex - 1 + _navButtons.Count) % _navButtons.Count);
            }
        }

        // Confirmation
        if (WasActionPressed(submitActionName))
        {
            if (_selectedIndex >= 0 && _selectedIndex < _navButtons.Count)
            {
                var btn = _navButtons[_selectedIndex];
                if (btn != null && btn.interactable) btn.onClick.Invoke();
            }
            return;
        }

        // Fermeture
        if (WasActionPressed(cancelActionName))
            Hide();
    }

    // ─── API publique ─────────────────────────────────────────────

    /// <summary>Ouvre le panneau pour le joueur donné (lit ses actions Navigate/Submit/Cancel).</summary>
    public void Show(PlayerInput playerInput)
    {
        _playerInput = playerInput;
        _frameShown  = Time.frameCount;

        gameObject.SetActive(true);
        _candidates = EmployeeManager.Instance?.GenerateCandidates() ?? new List<EmployeeManager.CandidateInfo>();
        SwitchTab(true);

        if (EmployeeManager.Instance != null)
            EmployeeManager.Instance.OnRosterChanged += RefreshHired;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    /// <summary>Compat : ouverture sans tracking joueur (pas de navigation manette).</summary>
    public void Show() => Show(null);

    public void Hide()
    {
        gameObject.SetActive(false);
        _playerInput = null;
        if (EmployeeManager.Instance != null)
            EmployeeManager.Instance.OnRosterChanged -= RefreshHired;
    }

    // ─── Onglets ──────────────────────────────────────────────────

    void SwitchTab(bool candidates)
    {
        _showingCandidates = candidates;

        // Toggle les ScrollViews (Content → Viewport → ScrollView)
        if (candidatesContainer != null) candidatesContainer.parent.parent.gameObject.SetActive(candidates);
        if (hiredContainer      != null) hiredContainer.parent.parent.gameObject.SetActive(!candidates);

        if (candidates) BuildCandidates();
        else            BuildHired();

        RefreshHeader();
        SelectButton(0);
    }

    // ─── Construction candidats ───────────────────────────────────

    void BuildCandidates()
    {
        if (candidatesContainer == null) return;
        foreach (Transform t in candidatesContainer) Destroy(t.gameObject);
        _navButtons.Clear();

        foreach (var c in _candidates)
        {
            var slot = CreateSlot(candidatesContainer, candidateSlotPrefab);
            if (slot == null) continue;

            SetText(slot, "NameLabel",   c.displayName);
            SetText(slot, "RoleLabel",   RoleLabel(c.role));
            SetText(slot, "RatingLabel", $"{c.rating}/20");
            SetText(slot, "SalaryLabel", $"{c.dailySalary}G/j");
            SetText(slot, "FeeLabel",    $"Embauche : {c.hiringFee}G");

            var captured = c;
            var btn = slot.GetComponentInChildren<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => OnHire(captured));
                DisableEventSystemNav(btn);
                _navButtons.Add(btn);
            }
        }
    }

    void OnHire(EmployeeManager.CandidateInfo c)
    {
        if (EmployeeManager.Instance == null) return;

        var data = ScriptableObject.CreateInstance<EmployeeData>();
        data.employeeName  = c.displayName;
        data.role          = c.role;
        data.rating        = c.rating;
        data.dailySalary   = c.dailySalary;
        data.hiringFee     = c.hiringFee;
        data.moveSpeed     = c.template?.moveSpeed     ?? 3f;
        data.workStartHour = c.template?.workStartHour ?? 8f;
        data.workEndHour   = c.template?.workEndHour   ?? 20f;
        data.breakInterval = c.template?.breakInterval ?? 120f;
        data.breakDuration = c.template?.breakDuration ?? 30f;

        if (EmployeeManager.Instance.Hire(data, c.assignedPrefab))
        {
            _candidates.Remove(c);
            BuildCandidates();
            RefreshHeader();
            SelectButton(0);
        }
    }

    // ─── Construction employés actuels ────────────────────────────

    void BuildHired()
    {
        if (hiredContainer == null) return;
        foreach (Transform t in hiredContainer) Destroy(t.gameObject);
        _navButtons.Clear();

        if (EmployeeManager.Instance == null) return;

        foreach (var emp in EmployeeManager.Instance.Hired)
        {
            if (emp == null) continue;

            var slot = CreateSlot(hiredContainer, hiredSlotPrefab);
            if (slot == null) continue;

            SetText(slot, "NameLabel",     emp.Data.employeeName);
            SetText(slot, "RoleLabel",     RoleLabel(emp.Data.role));
            SetText(slot, "RatingLabel",   $"{emp.Data.rating}/20");
            SetText(slot, "SalaryLabel",   $"{emp.Data.dailySalary}G/j");
            SetText(slot, "FatigueLabel",  $"Fatigue : {emp.Fatigue:F0}%");
            SetText(slot, "WellbeingLabel",$"Bien-être : {emp.Wellbeing:F0}%");
            SetText(slot, "StateLabel",    emp.State.ToString());

            var captured = emp;
            Button btn = null;
            foreach (var b in slot.GetComponentsInChildren<Button>(true))
                if (b.gameObject.name.Trim() == "FireButton") { btn = b; break; }
            if (btn != null)
            {
                btn.onClick.AddListener(() => OnFire(captured));
                DisableEventSystemNav(btn);
                _navButtons.Add(btn);
            }
        }
    }

    void OnFire(EmployeeInstance emp)
    {
        EmployeeManager.Instance?.Fire(emp);
        BuildHired();
        RefreshHeader();
        SelectButton(0);
    }

    void RefreshHired()
    {
        if (!_showingCandidates) BuildHired();
        RefreshHeader();
    }

    // ─── Header ───────────────────────────────────────────────────

    void RefreshHeader()
    {
        if (employeeCountLabel != null && EmployeeManager.Instance != null)
            employeeCountLabel.text = $"Employés : {EmployeeManager.Instance.Hired.Count} / {EmployeeManager.Instance.MaxEmployees}";

        if (goldLabel != null && EconomyManager.Instance != null)
            goldLabel.text = $"{EconomyManager.Instance.Gold}G";
    }

    // ─── Sélection manette ──────────────────────────────────────────

    void SelectButton(int index)
    {
        if (_selectedIndex >= 0 && _selectedIndex < _navButtons.Count)
            ApplyButtonColor(_navButtons[_selectedIndex], selected: false);

        _selectedIndex = index;

        if (_selectedIndex >= 0 && _selectedIndex < _navButtons.Count)
            ApplyButtonColor(_navButtons[_selectedIndex], selected: true);
    }

    static void ApplyButtonColor(Button btn, bool selected)
    {
        if (btn == null) return;
        var c = btn.colors;
        c.normalColor = selected ? ColSelected : ColNormal;
        btn.colors = c;
    }

    static void DisableEventSystemNav(Button btn)
    {
        if (btn == null) return;
        var nav = btn.navigation;
        nav.mode = Navigation.Mode.None;
        btn.navigation = nav;
    }

    // ─── Helpers ──────────────────────────────────────────────────

    static GameObject CreateSlot(Transform parent, GameObject prefab)
    {
        if (prefab != null)
            return Instantiate(prefab, parent);

        // Slot générique si pas de prefab assigné
        var go = new GameObject("EmployeeSlot", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void SetText(GameObject slot, string childName, string text)
    {
        foreach (var tmp in slot.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.gameObject.name == childName)
            {
                tmp.text = text;
                return;
            }
        }
    }

    static string RoleLabel(EmployeeRole role) => role switch
    {
        EmployeeRole.Reception => "Réception",
        EmployeeRole.Cleaning  => "Nettoyage",
        EmployeeRole.Cook      => "Cuisinier",
        _                      => role.ToString()
    };

    bool WasActionPressed(string actionName)
    {
        if (_playerInput == null) return false;
        var action = _playerInput.actions.FindAction(actionName, throwIfNotFound: false);
        return action?.WasPressedThisFrame() ?? false;
    }
}
