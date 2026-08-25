using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HUD écran-space : instancie un GuestBubbleSlot par monstre en attente au comptoir resto —
/// même principe que GuestQueueHUD (réception hôtel), réutilise le même prefab (G6-B23 : la file
/// resto n'avait jusqu'ici aucune indication visuelle, contrairement à la file hôtel).
///
/// Setup :
///   - Assigner Container (RectTransform avec HorizontalLayoutGroup, ex: dans un Canvas_GuestHUD dédié)
///   - Assigner BubblePrefab (le prefab GuestBubbleSlot, le même que GuestQueueHUD)
/// </summary>
public class RestaurantQueueHUD : MonoBehaviour
{
    [Header("Références")]
    public RectTransform container;
    public GameObject    bubblePrefab;

    // ─── Privé ────────────────────────────────────────────────────

    readonly Dictionary<RestaurantReservationSystem.PendingVisitor, GuestBubbleSlot> _slots = new();

    // ─── Lifecycle ────────────────────────────────────────────────

    void Start()
    {
        if (RestaurantReservationSystem.Instance != null)
            RestaurantReservationSystem.Instance.OnQueueChanged += RefreshSlots;

        RefreshSlots();
    }

    void OnDestroy()
    {
        if (RestaurantReservationSystem.Instance != null)
            RestaurantReservationSystem.Instance.OnQueueChanged -= RefreshSlots;
    }

    void Update()
    {
        var system = RestaurantReservationSystem.Instance;
        if (system == null) return;

        foreach (var kv in _slots)
        {
            float ratio = system.maxWaitTime > 0f
                ? Mathf.Clamp01(kv.Key.TimeRemaining / system.maxWaitTime)
                : 1f;

            kv.Value.SetRatio(ratio);
        }
    }

    // ─── Gestion des slots ────────────────────────────────────────

    void RefreshSlots()
    {
        var pending = RestaurantReservationSystem.Instance?.Pending;
        if (pending == null) return;

        // Retire les slots obsolètes
        var toRemove = new List<RestaurantReservationSystem.PendingVisitor>();
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
            if (guest.Monster == null) continue;

            var data = guest.Monster.GetComponent<MonsterDataReference>()?.Data;

            var go   = Instantiate(bubblePrefab, container);
            var slot = go.GetComponent<GuestBubbleSlot>();
            slot.SetGuest(data != null ? data.monsterName : guest.Monster.name, GetMonsterIcon(data));
            slot.SetRatio(1f);
            _slots[guest] = slot;
        }
    }

    // ─── Icône monstre ────────────────────────────────────────────

    static Sprite GetMonsterIcon(MonsterData data)
    {
        if (data == null) return null;
        var field = data.GetType().GetField("hudIcon",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(data) as Sprite;
    }
}
