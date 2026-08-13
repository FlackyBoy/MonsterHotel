using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// HUD screen-space : instancie un GuestBubbleSlot par monstre en attente
/// et met à jour le fill (vert→rouge) chaque frame.
///
/// Setup :
///   - Assigner Container (le RectTransform avec HorizontalLayoutGroup dans Canvas_GuestHUD)
///   - Assigner BubblePrefab (le prefab GuestBubbleSlot)
/// </summary>
public class GuestQueueHUD : MonoBehaviour
{
    [Header("Références")]
    public RectTransform  container;
    public GameObject bubblePrefab;

    // ─── Privé ────────────────────────────────────────────────────

    readonly Dictionary<ReservationSystem.PendingGuest, GuestBubbleSlot> _slots = new();

    // ─── Lifecycle ────────────────────────────────────────────────

    void Start()
    {
        if (ReservationSystem.Instance != null)
            ReservationSystem.Instance.OnQueueChanged += RefreshSlots;

        RefreshSlots();
    }

    void OnDestroy()
    {
        if (ReservationSystem.Instance != null)
            ReservationSystem.Instance.OnQueueChanged -= RefreshSlots;
    }

    void Update()
    {
        foreach (var kv in _slots)
        {
            float ratio = kv.Key.Data.maxWaitTime > 0f
                ? Mathf.Clamp01(kv.Key.TimeRemaining / kv.Key.Data.maxWaitTime)
                : 1f;

            kv.Value.SetRatio(ratio);
        }
    }

    // ─── Gestion des slots ────────────────────────────────────────

    void RefreshSlots()
    {
        var pending = ReservationSystem.Instance?.Pending;
        if (pending == null) return;

        // Retire les slots obsolètes
        var toRemove = new List<ReservationSystem.PendingGuest>();
        foreach (var kv in _slots)
        {
            bool found = false;
            foreach (var g in pending) { if (g == kv.Key) { found = true; break; } }
            if (!found) toRemove.Add(kv.Key);
        }
        foreach (var g in toRemove)
        {
            if (_slots[g] != null) Destroy(_slots[g].gameObject);
            _slots.Remove(g);
        }

        // Ajoute les nouveaux
        foreach (var guest in pending)
        {
            if (_slots.ContainsKey(guest)) continue;

            var go   = Instantiate(bubblePrefab, container);
            var slot = go.GetComponent<GuestBubbleSlot>();
            slot.SetGuest(guest.Data.monsterName, GetMonsterIcon(guest.Data));
            slot.SetRatio(1f);
            _slots[guest] = slot;
        }
    }

    // ─── Icône monstre ────────────────────────────────────────────

    static Sprite GetMonsterIcon(MonsterData data)
    {
        var field = data.GetType().GetField("hudIcon",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(data) as Sprite;
    }
}
