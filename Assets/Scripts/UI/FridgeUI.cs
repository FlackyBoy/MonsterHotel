using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Panneau de sélection d'ingrédients du frigo.
/// Navigation identique au RoomManagementPanel : touches Navigate ↑↓ + Submit.
/// S'auto-instancie au démarrage — aucun setup manuel requis.
/// </summary>
public class FridgeUI : MonoBehaviour
{


    [Header("Typographie")]
    public float buttonFontSize = 14f;

    [Header("Noms des actions (Player Action Map)")]
    public string navigateActionName = "Navigate";
    public string submitActionName   = "Submit";
    public string cancelActionName   = "Cancel";

    // ─── État ──────────────────────────────────────────────────────

    PlayerItemHolder        _holder;
    PlayerInput             _playerInput;
    readonly List<Button>   _buttons = new();
    int                     _selectedIndex;
    int                     _frameOpened;

    // Bouton template auto-créé
    Button      _templateButton;
    GameObject  _panel; // enfant visible — le root reste toujours actif

    static readonly Color ColSelected = new(1f, 0.85f, 0f);
    static readonly Color ColNormal   = Color.white;
    static readonly Color ColDisabled = new(0.5f, 0.5f, 0.5f);

    // ─── Lifecycle ─────────────────────────────────────────────────

    void Awake()
    {
        // Crée un panel enfant (contenu visible) — le root ne contient aucun visuel
        _panel = new GameObject("Panel");
        _panel.transform.SetParent(transform, false);

        var rt = _panel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var bg = _panel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.92f);

        var vlg = _panel.AddComponent<VerticalLayoutGroup>();
        vlg.childControlHeight = true; vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 4f;
        vlg.padding = new RectOffset(8, 8, 8, 8);

        var csf = _panel.AddComponent<ContentSizeFitter>();
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        _panel.SetActive(false);

        // Crée un bouton template invisible pour cloner
        _templateButton = CreateRawButton("__template__", null);
        _templateButton.gameObject.SetActive(false);

        // Titre
        var titleGo  = new GameObject("Title");
        titleGo.transform.SetParent(_panel.transform, false);
        titleGo.transform.SetSiblingIndex(0);
        var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
        titleTxt.text      = "Choisir un ingrédient";
        titleTxt.fontSize  = buttonFontSize + 2f;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color     = Color.white;
        var le = titleGo.AddComponent<LayoutElement>();
        le.preferredHeight = 24f;
    }

    void Update()
    {
        if (_playerInput == null || !_panel.activeSelf || Time.frameCount == _frameOpened) return;

        var nav = _playerInput.actions.FindAction(navigateActionName, false);
        if (nav != null && nav.WasPressedThisFrame())
        {
            float y = nav.ReadValue<Vector2>().y;
            if (y < -0.3f) Select((_selectedIndex + 1) % _buttons.Count);
            else if (y > 0.3f) Select((_selectedIndex - 1 + _buttons.Count) % _buttons.Count);
        }

        var submit = _playerInput.actions.FindAction(submitActionName, false);
        if (submit != null && submit.WasPressedThisFrame())
        {
            if (_selectedIndex >= 0 && _selectedIndex < _buttons.Count)
                _buttons[_selectedIndex].onClick.Invoke();
            return; // Close() peut avoir mis _playerInput à null
        }

        var cancel = _playerInput.actions.FindAction(cancelActionName, false);
        if (cancel != null && cancel.WasPressedThisFrame())
            Close();
    }

    // ─── API ───────────────────────────────────────────────────────

    public void Open(PlayerItemHolder holder, HoldableItemData[] ingredients)
    {
        Debug.Log($"[FridgeUI] Open appelé — ingredients: {ingredients?.Length ?? 0}");
        if (ingredients == null || ingredients.Length == 0) { Debug.LogWarning("[FridgeUI] Aucun ingrédient !"); return; }

        _holder      = holder;
        _playerInput = holder.GetComponent<PlayerInput>();
        _frameOpened = Time.frameCount;

        // Vide les boutons précédents
        foreach (var b in _buttons)
            if (b != null) Destroy(b.gameObject);
        _buttons.Clear();

        // Génère un bouton par ingrédient
        foreach (var item in ingredients)
        {
            if (item == null) continue;
            var captured = item;
            var btn = CreateRawButton(item.itemName, () => SelectIngredient(captured));
            _buttons.Add(btn);
        }

        // Bouton Fermer
        _buttons.Add(CreateRawButton("Fermer", Close));

        _panel.SetActive(true);
        Select(0);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    public void Close()
    {
        foreach (var b in _buttons)
            if (b != null) Destroy(b.gameObject);
        _buttons.Clear();

        _panel.SetActive(false);
        _holder      = null;
        _playerInput = null;
    }

    // ─── Privé ─────────────────────────────────────────────────────

    void SelectIngredient(HoldableItemData item)
    {
        _holder?.PickUp(item);
        Close();
    }

    void Select(int index)
    {
        if (_selectedIndex >= 0 && _selectedIndex < _buttons.Count)
            ApplyColor(_buttons[_selectedIndex], ColNormal);
        _selectedIndex = index;
        if (_selectedIndex >= 0 && _selectedIndex < _buttons.Count)
            ApplyColor(_buttons[_selectedIndex], ColSelected);
    }

    void ApplyColor(Button btn, Color col)
    {
        if (btn == null) return;
        var c = btn.colors;
        c.normalColor = col;
        btn.colors = c;
    }

    Button CreateRawButton(string label, System.Action onClick)
    {
        GameObject go;
        if (_templateButton != null)
        {
            go = Instantiate(_templateButton.gameObject, _panel.transform);
            go.SetActive(true);
        }
        else
        {
            go = new GameObject(label);
            go.transform.SetParent(_panel.transform, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
        }

        go.name = $"Btn_{label}";

        // Assure qu'un Image de fond existe
        if (go.GetComponent<Image>() == null)
        {
            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
        }

        // LayoutElement pour la hauteur fixe
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.preferredHeight = 28f;

        // Texte
        var txt = go.GetComponentInChildren<TextMeshProUGUI>();
        if (txt == null)
        {
            var tgo = new GameObject("Label");
            tgo.transform.SetParent(go.transform, false);
            txt = tgo.AddComponent<TextMeshProUGUI>();
            var rt = tgo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(6f, 0f);
            rt.offsetMax = new Vector2(-6f, 0f);
        }
        txt.text      = label;
        txt.fontSize  = buttonFontSize;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color     = Color.white;

        // Bouton
        var btn = go.GetComponent<Button>() ?? go.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        if (onClick != null) btn.onClick.AddListener(() => onClick());

        // Désactive la navigation EventSystem
        var nav = btn.navigation;
        nav.mode     = Navigation.Mode.None;
        btn.navigation = nav;

        return btn;
    }
}
