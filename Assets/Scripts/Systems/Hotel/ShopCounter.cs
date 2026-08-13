using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Comptoir de vente de chambres.
///
/// Prérequis :
///   - Un Collider en mode Trigger sur ce GameObject (zone d'interaction)
///   - RoomPlacer sur le prefab joueur
///   - ShopPanels[0] = panel joueur 1, ShopPanels[1] = panel joueur 2
/// </summary>
public class ShopCounter : MonoBehaviour
{
    [Header("Catalogue de chambres")]
    public RoomData[] catalog;

    [Header("UI")]
    [Tooltip("Index 0 = Joueur 1, Index 1 = Joueur 2")]
    public RoomShopPanel[] shopPanels;
    public GameObject interactPrompt;

    [Header("Input")]
    [Tooltip("Nom de l'action dans la Player Action Map (doit correspondre exactement)")]
    public string interactActionName = "Interact";

    // ─── Privé ────────────────────────────────────────────────────

    // Plusieurs joueurs peuvent être dans la zone en même temps
    readonly List<RoomPlacer> _playersInRange = new();

    // ─── Lifecycle ────────────────────────────────────────────────

    void Start()
    {
        if (interactPrompt != null) interactPrompt.SetActive(false);

        // HotelConfig.rooms est la source de vérité — prime sur l'assignation locale
        // de la scène pour éviter que les deux catalogues divergent.
        var cfg = HotelConfig.Instance;
        if (cfg != null && cfg.rooms != null && cfg.rooms.Length > 0)
            catalog = cfg.rooms;
    }

    void Update()
    {
        foreach (var placer in _playersInRange)
        {
            if (placer == null || placer.IsPlacing) continue;

            // Ne réouvre pas le panneau s'il est déjà visible pour ce joueur
            // (évite que la même pression de touche ouvre le panel ET confirme un slot)
            int idx = GetPlayerIndex(placer);
            bool alreadyOpen = shopPanels != null && idx < shopPanels.Length
                            && shopPanels[idx] != null && shopPanels[idx].gameObject.activeSelf;
            if (!alreadyOpen && WasInteractPressed(placer))
                OpenShop(placer);
        }
    }

    // ─── Trigger ──────────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        var placer = other.GetComponentInParent<RoomPlacer>();
        if (placer == null || _playersInRange.Contains(placer)) return;

        _playersInRange.Add(placer);
        if (interactPrompt != null) interactPrompt.SetActive(true);
    }

    void OnTriggerExit(Collider other)
    {
        var placer = other.GetComponentInParent<RoomPlacer>();
        if (placer == null || !_playersInRange.Contains(placer)) return;

        int idx = GetPlayerIndex(placer);
        if (idx >= 0 && idx < shopPanels.Length)
            shopPanels[idx]?.Hide();

        _playersInRange.Remove(placer);
        if (_playersInRange.Count == 0 && interactPrompt != null)
            interactPrompt.SetActive(false);
    }

    // ─── Shop ─────────────────────────────────────────────────────

    void OpenShop(RoomPlacer placer)
    {
        if (catalog == null || catalog.Length == 0) return;

        int idx = GetPlayerIndex(placer);
        if (shopPanels == null || idx >= shopPanels.Length || shopPanels[idx] == null)
        {
            return;
        }

        shopPanels[idx].Show(catalog, placer, idx);
    }

    // ─── Helpers ──────────────────────────────────────────────────

    int GetPlayerIndex(RoomPlacer placer)
    {
        return placer.GetComponent<PlayerInput>()?.playerIndex ?? 0;
    }

    bool WasInteractPressed(RoomPlacer placer)
    {
        var pi = placer.GetComponent<PlayerInput>();
        if (pi == null) return false;
        var action = pi.actions.FindAction(interactActionName, throwIfNotFound: false);
        return action?.WasPressedThisFrame() ?? false;
    }
}
