// Assets/Script/Build/UI/ItemButtonUI.cs
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemButtonUI : MonoBehaviour
{
    [Header("Refs (optionnel, auto si laiss� vide)")]
    [SerializeField] TextMeshProUGUI tmpLabel;   // <- TMP
    [SerializeField] Text legacyLabel;           // <- Legacy (fallback)
    [SerializeField] Image icon;
    [SerializeField] Button btn;

    ItemBlueprint item;
    Action<ItemBlueprint> onClick;

    public void Setup(ItemBlueprint item, Action<ItemBlueprint> onClick)
    {
        this.item = item;
        this.onClick = onClick;

        // Auto-r�solution si non c�bl� dans l�inspector
        if (!btn) btn = GetComponent<Button>();
        if (!tmpLabel) tmpLabel = GetComponentInChildren<TextMeshProUGUI>(true);
        if (!legacyLabel && !tmpLabel) legacyLabel = GetComponentInChildren<Text>(true);
        if (!icon) icon = GetComponentInChildren<Image>(true);

        var nameToShow = !string.IsNullOrEmpty(item.displayName) ? item.displayName : item.name;

        if (tmpLabel) tmpLabel.text = nameToShow;
        else if (legacyLabel) legacyLabel.text = nameToShow;

        // On n�impose pas d�ic�ne. Si le prefab du bouton a d�j� un sprite, on le garde.
        if (icon) icon.enabled = icon.sprite != null;

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => this.onClick?.Invoke(this.item));
    }
}
