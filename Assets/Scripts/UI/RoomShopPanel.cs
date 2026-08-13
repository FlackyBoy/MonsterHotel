using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Panneau UI affiché au comptoir permettant de choisir un type de chambre.
///
/// Hiérarchie attendue :
///   RoomShopPanel (ce script)
///   └── Content (HorizontalLayoutGroup ou GridLayoutGroup)
///       └── [slots générés dynamiquement]
///
/// SlotPrefab attendu :
///   Button
///   ├── RoomName  (TMP_Text)
///   └── RoomCost  (TMP_Text)
/// </summary>
public class RoomShopPanel : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Parent des slots (Layout Group recommandé)")]
    public Transform content;
    [Tooltip("Prefab d'un slot : Button + enfants RoomName et RoomCost (TMP_Text)")]
    public GameObject slotPrefab;

    [Header("Typographie des slots")]
    [Tooltip("0 = hérite du prefab, sinon override")]
    public float slotNameFontSize = 0f;
    public float slotCostFontSize = 0f;

    [Header("Dimensions")]
    [Tooltip("Hauteur de chaque slot en pixels")]
    public float slotHeight = 48f;
    [Tooltip("Padding interne du panneau : x = gauche+droite, y = haut+bas")]
    public Vector2 panelPadding = new(16f, 16f);

    [Header("Décorations (optionnel)")]
    [Tooltip("Si assigné, un onglet Décorations apparaît dans le panneau. " +
             "Navigue gauche/droite pour changer d'onglet.")]
    public DecorationData[] decorationCatalog;

    [Header("Noms des actions dans la Player Action Map")]
    public string submitActionName   = "Submit";
    public string navigateActionName = "Navigate";
    public string cancelActionName   = "Cancel";

    // ─── Privé ────────────────────────────────────────────────────

    enum ShopTab { Rooms, Decorations }

    RoomPlacer                _placer;
    DecorationPlacer          _decoplacer;
    RoomData[]                _catalog;
    PlayerInput               _playerInput;
    int                       _selectedIndex;
    int                       _frameShown = -1; // frame sur laquelle Show() a été appelé
    ShopTab                   _currentTab = ShopTab.Rooms;
    readonly List<GameObject> _slots = new();

    bool HasDecorations => decorationCatalog != null && decorationCatalog.Length > 0;

    static readonly Color ColSelected = new(1f, 0.85f, 0f);
    static readonly Color ColNormal   = Color.white;
    static readonly Color ColDisabled = new(0.5f, 0.5f, 0.5f);

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake() => gameObject.SetActive(false);

    void Update()
    {
        if (_playerInput == null || _slots.Count == 0 || Time.frameCount == _frameShown) return;

        // Navigation
        var navAction = _playerInput.actions.FindAction(navigateActionName, throwIfNotFound: false);
        if (navAction != null && navAction.WasPressedThisFrame())
        {
            var v = navAction.ReadValue<Vector2>();

            // Gauche/Droite : change d'onglet (si décorations disponibles)
            if (HasDecorations && Mathf.Abs(v.x) > Mathf.Abs(v.y))
            {
                SwitchTab();
                return;
            }

            // Haut/Bas : navigue dans les slots
            int dir = 0;
            if (v.y < -0.3f) dir =  1;
            if (v.y >  0.3f) dir = -1;
            if (dir != 0)
                SelectSlot((_selectedIndex + dir + _slots.Count) % _slots.Count);
        }

        // Confirmation
        var submitAction = _playerInput.actions.FindAction(submitActionName, throwIfNotFound: false);
        if (submitAction != null && submitAction.WasPressedThisFrame())
        {
            ConfirmSelected();
            return;
        }

        // Fermeture (manette : bouton Cancel / clavier : Échap)
        var cancelAction = _playerInput.actions.FindAction(cancelActionName, throwIfNotFound: false);
        if (cancelAction != null && cancelAction.WasPressedThisFrame())
            Hide();
    }

    // ─── API ──────────────────────────────────────────────────────

    public void Show(RoomData[] catalog, RoomPlacer placer, int playerIndex = 0)
    {
        _placer      = placer;
        _decoplacer  = placer.GetComponent<DecorationPlacer>();
        _catalog     = catalog;
        _playerInput = placer.GetComponent<PlayerInput>();
        _frameShown  = Time.frameCount;
        _currentTab  = ShopTab.Rooms;
        BuildSlots();
        SelectSlot(0);
        ResizePanel();
        PositionForPlayer(playerIndex);
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        ClearSlots();
        _placer      = null;
        _decoplacer  = null;
        _catalog     = null;
        _playerInput = null;
    }

    // ─── Interne ──────────────────────────────────────────────────

    void BuildSlots()
    {
        ClearSlots();

        if (_currentTab == ShopTab.Rooms)
            BuildRoomSlots();
        else
            BuildDecoSlots();

        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnGoldChanged += OnGoldChanged;
    }

    void BuildRoomSlots()
    {
        if (_catalog == null) return;

        // En-tête d'onglet (si décorations disponibles)
        if (HasDecorations) BuildTabHeader("CHAMBRES", "◄►  DÉCORATIONS");

        for (int i = 0; i < _catalog.Length; i++)
        {
            var data = _catalog[i];
            if (data == null) continue;

            var slot = CreateSlot();
            var nameText = slot.transform.Find("RoomName")?.GetComponent<TMP_Text>();
            var costText = slot.transform.Find("RoomCost")?.GetComponent<TMP_Text>();

            if (nameText != null)
            {
                if (slotNameFontSize > 0f) nameText.fontSize = slotNameFontSize;
                string typeName = data.roomType != null ? data.roomType.typeName : "";
                nameText.text = $"{data.roomName}\n<size=75%><color=#AAAAAA>{typeName}</color></size>";
            }
            if (costText != null)
            {
                if (slotCostFontSize > 0f) costText.fontSize = slotCostFontSize;
                costText.text = $"{data.cost} G";
            }

            var btn = slot.GetComponent<Button>();
            if (btn != null)
            {
                var captured = data;
                btn.onClick.AddListener(() => OnRoomSlotClicked(captured));
                RefreshRoomSlot(btn, data);
            }
        }
    }

    void BuildDecoSlots()
    {
        if (decorationCatalog == null) return;

        // En-tête d'onglet
        BuildTabHeader("DÉCORATIONS", "◄►  CHAMBRES");

        for (int i = 0; i < decorationCatalog.Length; i++)
        {
            var data = decorationCatalog[i];
            if (data == null) continue;

            var slot = CreateSlot();
            var nameText = slot.transform.Find("RoomName")?.GetComponent<TMP_Text>();
            var costText = slot.transform.Find("RoomCost")?.GetComponent<TMP_Text>();

            if (nameText != null)
            {
                if (slotNameFontSize > 0f) nameText.fontSize = slotNameFontSize;
                string bonuses = "";
                if (data.comfortBonus > 0f) bonuses += $" +{data.comfortBonus:0.#}c";
                if (data.renownBonus  > 0f) bonuses += $" +{data.renownBonus:0.#}r";
                nameText.text = $"{data.decorationName}" +
                    (bonuses.Length > 0 ? $"\n<size=75%><color=#AAFFAA>{bonuses.Trim()}</color></size>" : "");
            }
            if (costText != null)
            {
                if (slotCostFontSize > 0f) costText.fontSize = slotCostFontSize;
                costText.text = $"{data.cost} G";
            }

            var btn = slot.GetComponent<Button>();
            if (btn != null)
            {
                var captured = data;
                btn.onClick.AddListener(() => OnDecoSlotClicked(captured));
                RefreshDecoSlot(btn, data);
            }
        }
    }

    void BuildTabHeader(string activeTab, string hint)
    {
        var slot = CreateSlot(selectable: false);
        var nameText = slot.transform.Find("RoomName")?.GetComponent<TMP_Text>();
        var costText = slot.transform.Find("RoomCost")?.GetComponent<TMP_Text>();
        if (nameText != null) nameText.text = $"<b>{activeTab}</b>";
        if (costText != null) costText.text = $"<size=75%><color=#AAAAAA>{hint}</color></size>";
    }

    GameObject CreateSlot(bool selectable = true)
    {
        var slot = Instantiate(slotPrefab, content);
        _slots.Add(slot);
        var le = slot.GetComponent<LayoutElement>() ?? slot.AddComponent<LayoutElement>();
        le.preferredHeight = slotHeight;
        le.flexibleHeight  = 0f;
        if (!selectable)
        {
            var btn = slot.GetComponent<Button>();
            if (btn != null) btn.interactable = false;
        }
        return slot;
    }

    void SwitchTab()
    {
        _currentTab = _currentTab == ShopTab.Rooms ? ShopTab.Decorations : ShopTab.Rooms;
        BuildSlots();
        SelectSlot(_slots.Count > 1 ? 1 : 0); // saute l'en-tête
        ResizePanel();
    }

    void ClearSlots()
    {
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnGoldChanged -= OnGoldChanged;

        foreach (var s in _slots) Destroy(s);
        _slots.Clear();
    }

    void SelectSlot(int index)
    {
        if (_selectedIndex >= 0 && _selectedIndex < _slots.Count)
            ApplySlotColor(_slots[_selectedIndex]?.GetComponent<Button>(), selected: false);

        _selectedIndex = index;

        if (_selectedIndex >= 0 && _selectedIndex < _slots.Count)
            ApplySlotColor(_slots[_selectedIndex]?.GetComponent<Button>(), selected: true);
    }

    void ApplySlotColor(Button btn, bool selected)
    {
        if (btn == null) return;
        var colors = btn.colors;
        colors.normalColor   = selected ? ColSelected : ColNormal;
        colors.disabledColor = ColDisabled;
        btn.colors = colors;
    }

    void ConfirmSelected()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _slots.Count) return;
        var btn = _slots[_selectedIndex]?.GetComponent<Button>();
        if (btn != null && btn.interactable) btn.onClick.Invoke();
    }

    void OnRoomSlotClicked(RoomData data)
    {
        if (_placer == null || _placer.IsPlacing) return;
        if (!EconomyManager.Instance.CanAfford(data.cost)) return;
        var placer = _placer;
        Hide();
        placer.StartPlacing(data);
    }

    void OnDecoSlotClicked(DecorationData data)
    {
        if (_decoplacer == null || _decoplacer.IsPlacing) return;
        if (!EconomyManager.Instance.CanAfford(data.cost)) return;
        var decoplacer = _decoplacer;
        Hide();
        decoplacer.StartPlacing(data);
    }

    void OnGoldChanged(int _)
    {
        if (_currentTab == ShopTab.Rooms && _catalog != null)
        {
            // Les slots de rooms commencent après l'éventuel header (index 1 si HasDecorations, sinon 0)
            int offset = HasDecorations ? 1 : 0;
            for (int i = 0; i < _catalog.Length && offset + i < _slots.Count; i++)
            {
                var btn = _slots[offset + i].GetComponent<Button>();
                if (btn != null) RefreshRoomSlot(btn, _catalog[i]);
            }
        }
        else if (_currentTab == ShopTab.Decorations && decorationCatalog != null)
        {
            int offset = 1; // toujours un header en mode déco
            for (int i = 0; i < decorationCatalog.Length && offset + i < _slots.Count; i++)
            {
                var btn = _slots[offset + i].GetComponent<Button>();
                if (btn != null) RefreshDecoSlot(btn, decorationCatalog[i]);
            }
        }
    }

    void ResizePanel()
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) return;

        float h = _slots.Count * slotHeight + panelPadding.y;
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);

        if (content is RectTransform crt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(crt);
    }

    void PositionForPlayer(int playerIndex)
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) return;

        if (playerIndex == 0)
        {
            // Joueur 1 — bas gauche
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(20f, 20f);
        }
        else
        {
            // Joueur 2 — bas droite
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-20f, 20f);
        }
    }

    void RefreshRoomSlot(Button btn, RoomData data)
    {
        btn.interactable = EconomyManager.Instance == null || EconomyManager.Instance.CanAfford(data.cost);
    }

    void RefreshDecoSlot(Button btn, DecorationData data)
    {
        btn.interactable = EconomyManager.Instance == null || EconomyManager.Instance.CanAfford(data.cost);
    }
}
