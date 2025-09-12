using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class BuildPickerUI : MonoBehaviour
{
    [Header("Wiring")]
    public RectTransform content;
    public ItemButtonUI itemButtonPrefab;

    readonly List<ItemBlueprint> _items = new();
    Action<ItemBlueprint> _onSelect;
    int _index = 0;

    public void Show(ItemBlueprint[] items, Action<ItemBlueprint> onSelect)
    {
        _onSelect = onSelect;
        Clear();
        _items.Clear();
        if (items != null) _items.AddRange(items);

        for (int i = 0; i < _items.Count; i++)
        {
            var b = Instantiate(itemButtonPrefab, content);
            b.gameObject.name = $"ItemButton_{i}_{_items[i].name}";
            b.Setup(_items[i], Select); // clic souris reste supporté
        }

        gameObject.SetActive(true);
        FocusIndex(0);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        Clear();
        _items.Clear();
        _onSelect = null;
        _index = 0;
    }

    // ---- Navigation au pad/clavier ----
    public void Next()
    {
        if (_items.Count == 0) return;
        _index = (_index + 1) % _items.Count;
        FocusIndex(_index);
    }
    public void Prev()
    {
        if (_items.Count == 0) return;
        _index = (_index - 1 + _items.Count) % _items.Count;
        FocusIndex(_index);
    }
    public void Confirm()
    {
        if (_items.Count == 0) return;
        Select(_items[_index]);
    }

    void FocusIndex(int i)
    {
        _index = Mathf.Clamp(i, 0, Mathf.Max(0, content.childCount - 1));
        if (content.childCount == 0) return;
        var go = content.GetChild(_index).gameObject;
        EventSystem.current?.SetSelectedGameObject(go);
    }

    void Select(ItemBlueprint it)
    {
        _onSelect?.Invoke(it);
        Hide();
    }

    void Clear()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
    }
}
