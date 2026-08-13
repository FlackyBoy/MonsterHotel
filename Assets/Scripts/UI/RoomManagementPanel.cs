using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Panneau UI de gestion d'une chambre — deux onglets :
///   0 · Chambre : Supprimer, Nettoyer, Déplacer, Améliorer
///   1 · Meuble  : Déplacer / Améliorer / Retirer un meuble (FurniturePicker), Poser meuble neuf
///
/// Contrôles :
///   Navigate ←→  → changer d'onglet
///   Navigate ↑↓  → changer de bouton dans l'onglet
///   Submit       → valider le bouton sélectionné
/// </summary>
public class RoomManagementPanel : MonoBehaviour
{
    [Header("Config joueur")]
    [Tooltip("0 = Joueur 1, 1 = Joueur 2")]
    public int playerIndex = 0;

    [Header("Références UI")]
    public TMP_Text roomNameText;
    public TMP_Text roomStateText;
    public Button   deleteButton;
    public Button   cleanButton;

    [Header("Remboursement")]
    [Range(0f, 1f)]
    public float refundRate = 1f;

    [Header("Typographie des boutons")]
    [Tooltip("0 = hérite du prefab deleteButton, sinon override")]
    public float buttonFontSize = 0f;

    [Header("Noms des actions dans la Player Action Map")]
    public string submitActionName   = "Submit";
    public string navigateActionName = "Navigate";
    public string nextTabActionName  = "NextTab";   // ex : Tab / RB
    public string prevTabActionName  = "PrevTab";   // ex : Shift+Tab / LB
    public string cancelActionName   = "Cancel";

    // ─── Privé ────────────────────────────────────────────────────

    RoomInstance          _room;
    RoomPlacer            _placer;
    readonly List<Button>        _navButtons     = new();
    readonly List<Button>        _dynamicButtons = new();
    readonly List<FurnitureData> _navFurniture   = new(); // null pour les boutons non-meuble
    int _selectedIndex = 0;
    int _activeTab     = 0;
    int _frameShown    = -1;

    // Barre d'onglets auto-générée
    GameObject _tabBar;
    Image[]             _tabBg;
    TextMeshProUGUI[]   _tabLabels;

    const int TabCount = 2;
    static readonly string[] TabNames = { "Meuble", "Chambre" };

    static readonly Color ColSelected    = new(1f, 0.85f, 0f);
    static readonly Color ColNormal      = Color.white;
    static readonly Color ColDisabled    = new(0.5f, 0.5f, 0.5f);
    static readonly Color ColTabActive   = new(1f, 0.75f, 0f);      // fond onglet actif
    static readonly Color ColTabInactive = new(0.25f, 0.25f, 0.25f);

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake()
    {
        gameObject.SetActive(false);
        deleteButton?.onClick.AddListener(OnDeleteClicked);
        cleanButton?.onClick.AddListener(OnCleanClicked);
        DisableEventSystemNav(deleteButton);
        DisableEventSystemNav(cleanButton);

        if (GetComponent<VerticalLayoutGroup>() == null)
        {
            var vlg = gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight     = true;
            vlg.childControlWidth      = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 5f;
            vlg.padding = new RectOffset(10, 10, 10, 10);
        }
        if (GetComponent<ContentSizeFitter>() == null)
        {
            var csf = gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        CreateTabBar();
    }

    void Update()
    {
        if (!IsVisible || _placer == null || Time.frameCount == _frameShown) return;

        var pi = _placer.GetComponent<PlayerInput>();
        if (pi == null) return;

        // Changement d'onglet (actions dédiées)
        if (WasActionPressed(pi, nextTabActionName))
            SwitchTab((_activeTab + 1) % TabCount);
        else if (WasActionPressed(pi, prevTabActionName))
            SwitchTab((_activeTab - 1 + TabCount) % TabCount);

        // Navigation verticale ↑↓
        if (_navButtons.Count > 1)
        {
            if (WasNavPressed(pi, down: true))
                SelectButton((_selectedIndex + 1) % _navButtons.Count);
            else if (WasNavPressed(pi, down: false))
                SelectButton((_selectedIndex - 1 + _navButtons.Count) % _navButtons.Count);
        }

        // Confirmation
        if (WasConfirmPressed(pi))
        {
            if (_selectedIndex >= 0 && _selectedIndex < _navButtons.Count)
            {
                var btn = _navButtons[_selectedIndex];
                if (btn != null && btn.interactable)
                    btn.onClick.Invoke();
            }
            return;
        }

        // Fermeture
        if (WasActionPressed(pi, cancelActionName))
            Hide();
    }

    // ─── API ──────────────────────────────────────────────────────

    public bool IsVisible => gameObject.activeSelf;

    public void Show(RoomInstance room, RoomPlacer placer, int idx = 0)
    {
        _room       = room;
        _placer     = placer;
        _activeTab  = 0;
        _frameShown = Time.frameCount;
        RefreshUI();
        RebuildNavButtons();
        SelectButton(0);
        RefreshTabBar();
        PositionForPlayer(idx);
        gameObject.SetActive(true);
        // Empêche l'EventSystem de sélectionner/highlight des boutons automatiquement
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    public void Hide()
    {
        CancelFurniturePreview();
        foreach (var b in _dynamicButtons)
            if (b != null) Destroy(b.gameObject);
        _dynamicButtons.Clear();
        _navButtons.Clear();
        _navFurniture.Clear();

        gameObject.SetActive(false);
        _room   = null;
        _placer = null;
    }

    void CancelFurniturePreview()
    {
        var fp = _placer?.GetComponent<FurniturePlacer>();
        if (fp != null && fp.PanelIsControlling)
            fp.CancelPlacing();
    }

    // ─── Barre d'onglets ──────────────────────────────────────────

    void CreateTabBar()
    {
        _tabBar = new GameObject("TabBar");
        _tabBar.transform.SetParent(transform, false);

        var hlg = _tabBar.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = true;
        hlg.childForceExpandHeight = false;
        hlg.spacing = 2f;

        var le = _tabBar.AddComponent<LayoutElement>();
        le.preferredHeight = 22f;

        _tabBg     = new Image[TabCount];
        _tabLabels = new TextMeshProUGUI[TabCount];

        for (int i = 0; i < TabCount; i++)
        {
            var tab = new GameObject($"Tab_{TabNames[i]}");
            tab.transform.SetParent(_tabBar.transform, false);

            var bg = tab.AddComponent<Image>();
            _tabBg[i] = bg;

            var tgo = new GameObject("Label");
            tgo.transform.SetParent(tab.transform, false);
            var txt = tgo.AddComponent<TextMeshProUGUI>();
            txt.text      = TabNames[i];
            txt.fontSize  = 11f;
            txt.alignment = TextAlignmentOptions.Center;
            txt.enableWordWrapping = false;
            var trt = tgo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            _tabLabels[i] = txt;
        }

        // Place la barre juste après les textes d'info (roomNameText / roomStateText)
        int insertAt = 0;
        if (roomStateText != null) insertAt = roomStateText.transform.GetSiblingIndex() + 1;
        else if (roomNameText != null) insertAt = roomNameText.transform.GetSiblingIndex() + 1;
        _tabBar.transform.SetSiblingIndex(insertAt);

        RefreshTabBar();
    }

    void SwitchTab(int tab)
    {
        _activeTab = tab;
        RebuildNavButtons();
        SelectButton(0);
        RefreshTabBar();
    }

    void RefreshTabBar()
    {
        if (_tabBg == null) return;
        for (int i = 0; i < TabCount; i++)
        {
            bool active = i == _activeTab;
            if (_tabBg[i]     != null) _tabBg[i].color     = active ? ColTabActive   : ColTabInactive;
            if (_tabLabels[i] != null) _tabLabels[i].color = active ? Color.black     : new Color(0.75f, 0.75f, 0.75f);

            // Hint discret sur les onglets inactifs
            if (_tabLabels[i] != null && !active)
                _tabLabels[i].text = $"< {TabNames[i]} >";
            else if (_tabLabels[i] != null)
                _tabLabels[i].text = TabNames[i];
        }
    }

    // ─── Interne ──────────────────────────────────────────────────

    void RefreshUI()
    {
        if (_room == null) return;
        if (roomNameText  != null) roomNameText.text  = _room.Data?.roomName ?? "Chambre";
        if (roomStateText != null)
        {
            string s = LocalizeState(_room.State);
            if (!_room.IsFullyFurnished) s += " ⚠ Non meublee";
            s += $"\n{_room.TotalRevenue} G/nuit  Q:{_room.Quality}/5  Att:{_room.TotalAttractiveness}";
            roomStateText.text = s;
        }
        if (deleteButton != null) deleteButton.interactable = _room.State != RoomState.Occupied;
        // Visibilité gérée par RebuildNavButtons selon l'onglet actif
    }

    void RebuildNavButtons()
    {
        CancelFurniturePreview();
        foreach (var b in _dynamicButtons)
            if (b != null) Destroy(b.gameObject);
        _dynamicButtons.Clear();
        _navButtons.Clear();
        _navFurniture.Clear();

        // Les boutons permanents ne sont visibles que sur l'onglet Chambre (tab 1)
        deleteButton?.gameObject.SetActive(_activeTab == 1);
        cleanButton?.gameObject.SetActive(_activeTab == 1 && _room?.State == RoomState.Dirty);

        if (_activeTab == 0) RebuildTabMeuble();
        else                 RebuildTabChambre();
    }

    void RebuildTabChambre()
    {
        if (deleteButton != null) { _navButtons.Add(deleteButton); _navFurniture.Add(null); }
        if (cleanButton  != null && _room?.State == RoomState.Dirty) { _navButtons.Add(cleanButton); _navFurniture.Add(null); }
        if (_room?.Data == null) return;

        if (_room.State != RoomState.Occupied && !RoomHasMonsterInside(_room))
        { _navButtons.Add(CreateDynamicButton("Deplacer chambre", OnMoveClicked)); _navFurniture.Add(null); }

        if (_room.CanUpgrade)
        {
            if (_room.IsFullyFurnished)
            { _navButtons.Add(CreateDynamicButton($"Ameliorer chambre ({_room.Data.upgradeCost} G)", OnUpgradeRoomClicked)); _navFurniture.Add(null); }
            else
            { _navButtons.Add(CreateDynamicButton("Ameliorer chambre — posez d'abord les meubles requis", null)); _navFurniture.Add(null); }
        }
    }

    void RebuildTabMeuble()
    {
        if (_room?.Data == null || deleteButton == null) return;

        foreach (var fd in _room.Data.requiredFurniture)
        {
            if (fd == null) continue;
            if (!fd.multiPlace && _room.HasFurniture(fd)) continue;
            _navButtons.Add(CreateFurnitureButton($"Poser : {fd.furnitureName} ({fd.cost} G)", fd));
        }
        foreach (var fd in _room.Data.optionalFurniture)
        {
            if (fd == null) continue;
            if (!fd.multiPlace && _room.HasFurniture(fd)) continue;
            _navButtons.Add(CreateFurnitureButton($"Decorer : {fd.furnitureName} ({fd.cost} G)", fd));
        }

        bool hasFurniture   = _room.PlacedFurniture.Count > 0;
        bool hasUpgradeable = false;
        foreach (var fi in _room.PlacedFurniture)
            if (fi != null && fi.CanUpgrade) { hasUpgradeable = true; break; }

        if (hasFurniture)
        {
            _navButtons.Add(CreateDynamicButton("Deplacer un meuble", () => OnEnterPickerMode(FurniturePickMode.Move)));
            _navFurniture.Add(null);
            if (hasUpgradeable)
            { _navButtons.Add(CreateDynamicButton("Ameliorer un meuble", () => OnEnterPickerMode(FurniturePickMode.Upgrade))); _navFurniture.Add(null); }
            _navButtons.Add(CreateDynamicButton("Retirer un meuble", () => OnEnterPickerMode(FurniturePickMode.Remove)));
            _navFurniture.Add(null);
        }
    }

    // ─── Boutons dynamiques ───────────────────────────────────────

    Button CreateDynamicButton(string label, System.Action onClick)
    {
        var go  = Instantiate(deleteButton.gameObject, transform);
        go.SetActive(true);
        go.name = $"DynBtn_{label}";
        var btn = go.GetComponent<Button>();
        var txt = go.GetComponentInChildren<TMP_Text>();
        if (txt != null) { if (buttonFontSize > 0f) txt.fontSize = buttonFontSize; txt.text = label; }
        btn.onClick.RemoveAllListeners();
        if (onClick != null)
            btn.onClick.AddListener(() => onClick());
        else
            btn.interactable = false;
        DisableEventSystemNav(btn);
        _dynamicButtons.Add(btn);
        return btn;
    }

    Button CreateFurnitureButton(string label, FurnitureData fd)
    {
        var go  = Instantiate(deleteButton.gameObject, transform);
        go.SetActive(true);
        go.name = $"FurnitureBtn_{fd.furnitureName}";
        var btn = go.GetComponent<Button>();
        var txt = go.GetComponentInChildren<TMP_Text>();
        if (txt != null) { if (buttonFontSize > 0f) txt.fontSize = buttonFontSize; txt.text = label; }
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => OnFurnitureClicked(fd));
        DisableEventSystemNav(btn);
        _dynamicButtons.Add(btn);
        _navFurniture.Add(fd); // associe le fd à ce bouton pour la preview
        return btn;
    }

    static void DisableEventSystemNav(Button btn)
    {
        if (btn == null) return;
        var nav = btn.navigation;
        nav.mode = Navigation.Mode.None;
        btn.navigation = nav;
    }

    void SelectButton(int index)
    {
        if (_selectedIndex >= 0 && _selectedIndex < _navButtons.Count)
            ApplyButtonColor(_navButtons[_selectedIndex], selected: false);

        _selectedIndex = index;

        if (_selectedIndex >= 0 && _selectedIndex < _navButtons.Count)
            ApplyButtonColor(_navButtons[_selectedIndex], selected: true);

        // Preview ghost : affiche le ghost du meuble survolé, annule si c'est un bouton non-meuble
        if (_room != null && _placer != null && _navFurniture.Count == _navButtons.Count)
        {
            var fp = _placer.GetComponent<FurniturePlacer>();
            if (fp != null)
            {
                var fd = (index >= 0 && index < _navFurniture.Count) ? _navFurniture[index] : null;
                if (fd != null)
                {
                    fp.StartPlacing(fd, _room);
                    fp.PanelIsControlling = true; // APRÈS StartPlacing (qui appelle CancelPlacing en interne)
                }
                else
                {
                    CancelFurniturePreview();
                }
            }
        }
    }

    void ApplyButtonColor(Button btn, bool selected)
    {
        if (btn == null) return;
        var c = btn.colors;
        c.normalColor   = selected ? ColSelected : ColNormal;
        c.disabledColor = ColDisabled;
        btn.colors = c;
    }

    // ─── Handlers ─────────────────────────────────────────────────

    void OnDeleteClicked()
    {
        if (_room == null) return;
        var room = _room;
        Hide();
        room.Demolish(refundRate);
    }

    void OnCleanClicked()
    {
        if (_room == null) return;
        _room.Clean();
        RefreshUI(); RebuildNavButtons(); SelectButton(0);
    }

    void OnUpgradeRoomClicked()
    {
        if (_room == null) return;
        if (_room.UpgradeRoom(out string failReason))
        {
            _room.GetComponent<RoomSign>()?.RefreshInteractRange();
            RefreshUI(); RebuildNavButtons(); SelectButton(0);
        }
        else if (failReason != null)
        {
            ShowTemporaryMessage(failReason);
        }
    }

    Coroutine _messageCoroutine;

    /// <summary>Affiche un message d'erreur temporaire dans roomStateText, puis restaure l'état normal.</summary>
    void ShowTemporaryMessage(string message, float duration = 2f)
    {
        if (roomStateText == null) return;
        if (_messageCoroutine != null) StopCoroutine(_messageCoroutine);
        _messageCoroutine = StartCoroutine(ShowTemporaryMessageRoutine(message, duration));
    }

    System.Collections.IEnumerator ShowTemporaryMessageRoutine(string message, float duration)
    {
        roomStateText.text = $"<color=#FF6666>⚠ {message}</color>";
        yield return new WaitForSeconds(duration);
        RefreshUI();
        _messageCoroutine = null;
    }

    void OnMoveClicked()
    {
        if (_placer == null || _room == null) return;
        // Double-check défensif — le bouton est déjà masqué dans ce cas (RebuildTabChambre), mais
        // au cas où l'état aurait changé entre l'ouverture du panneau et le clic (ex: un monstre
        // vient d'entrer dans une pièce commune pile à ce moment). StartMoving() ne repositionne
        // que la chambre et son mobilier, jamais les monstres présents — sans ce garde-fou ils
        // restaient plantés à l'ancien emplacement après un déplacement.
        if (RoomHasMonsterInside(_room))
        {
            ShowTemporaryMessage("Impossible : un monstre est dans la pièce");
            return;
        }
        var room = _room; var placer = _placer;
        Hide();
        placer.StartMoving(room);
    }

    /// <summary>
    /// True si au moins un monstre se trouve physiquement dans les limites de la pièce — distinct
    /// de RoomState.Occupied, qui ne concerne que les chambres privées réservées (les pièces
    /// communes comme la cuisine ou le salon ne passent jamais à Occupied même quand des monstres
    /// y sont présents, voir RoomInstance.Init).
    /// </summary>
    static bool RoomHasMonsterInside(RoomInstance room)
    {
        if (room == null) return false;
        var bounds = BoundsUtils.Get(room.gameObject);
        foreach (var dataRef in FindObjectsByType<MonsterDataReference>(FindObjectsSortMode.None))
        {
            if (dataRef == null) continue;
            if (bounds.Contains(dataRef.transform.position)) return true;
        }
        return false;
    }

    void OnFurnitureClicked(FurnitureData fd)
    {
        if (_placer == null || _room == null) return;
        var fp     = _placer.GetComponent<FurniturePlacer>();
        if (fp == null) return;

        // Capture pour la closure
        var capturedRoom   = _room;
        var capturedPlacer = _placer;
        var capturedIdx    = playerIndex;

        // Quand le placement se termine (ESC pour multiPlace, après pose pour les autres)
        // → réouvre le panneau
        fp.SetOnDoneCallback(() =>
        {
            if (capturedPlacer != null && capturedRoom != null)
                Show(capturedRoom, capturedPlacer, capturedIdx);
        });

        fp.ConfirmPreview();
        _placer = null; // empêche CancelFurniturePreview dans Hide()
        Hide();
    }

    void OnEnterPickerMode(FurniturePickMode mode)
    {
        if (_placer == null || _room == null) return;
        var picker = _placer.GetComponent<FurniturePicker>();
        if (picker == null) { return; }
        var room = _room;
        Hide();
        picker.StartPicking(room, mode);
    }

    // ─── Positionnement ───────────────────────────────────────────

    void PositionForPlayer(int idx)
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) return;
        if (idx == 0)
        { rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f); rt.anchoredPosition = new Vector2(20f, 20f); }
        else
        { rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f); rt.anchoredPosition = new Vector2(-20f, 20f); }
    }

    // ─── Input helpers ────────────────────────────────────────────

    bool WasConfirmPressed(PlayerInput pi)
    {
        var action = pi.actions.FindAction(submitActionName, throwIfNotFound: false);
        return action?.WasPressedThisFrame() ?? false;
    }

    bool WasNavPressed(PlayerInput pi, bool down)
    {
        var action = pi.actions.FindAction(navigateActionName, throwIfNotFound: false);
        if (action == null || !action.WasPressedThisFrame()) return false;
        var val = action.ReadValue<Vector2>();
        return down ? val.y < -0.3f : val.y > 0.3f;
    }

    bool WasActionPressed(PlayerInput pi, string actionName)
    {
        var action = pi.actions.FindAction(actionName, throwIfNotFound: false);
        return action?.WasPressedThisFrame() ?? false;
    }

    // ─── Localisation ─────────────────────────────────────────────

    static string LocalizeState(RoomState state) => state switch
    {
        RoomState.Empty       => "Disponible",
        RoomState.Occupied    => "Occupee",
        RoomState.Dirty       => "Sale",
        RoomState.NeedsRepair => "A reparer",
        _                     => state.ToString(),
    };
}
