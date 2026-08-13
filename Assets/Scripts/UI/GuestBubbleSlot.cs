using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Composant sur le prefab GuestBubbleSlot.
/// Expose les références UI pour que GuestQueueHUD puisse les piloter.
/// </summary>
public class GuestBubbleSlot : MonoBehaviour
{
    public Image                    fill;
    public Image                    icon;
    public TMPro.TextMeshProUGUI    nameLabel;

    public void SetGuest(string monsterName, Sprite monsterIcon)
    {
        if (nameLabel != null) nameLabel.text = monsterName;
        if (icon      != null) icon.sprite    = monsterIcon;
        if (icon      != null) icon.enabled   = monsterIcon != null;
    }

    public void SetRatio(float ratio)
    {
        if (fill == null) return;
        fill.fillAmount = ratio;
        fill.color      = Color.Lerp(Color.red, Color.green, ratio);
    }
}
