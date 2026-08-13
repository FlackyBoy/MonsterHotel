using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Pancarte d'interaction posée automatiquement sur chaque chambre par RoomPlacer.
///
/// Crée un indicateur world-space sur le mur porte, visible quand le joueur est proche.
/// Appuyer sur Interact (I / Y-Triangle) ouvre le RoomManagementPanel.
/// </summary>
[RequireComponent(typeof(RoomInstance))]
public class RoomSign : MonoBehaviour
{
    [Header("Interaction")]
    [Tooltip("Calculé automatiquement depuis la taille de la chambre si laissé à 0")]
    public float interactRange = 0f;

    [Header("Prompt world-space (optionnel — créé auto si null)")]
    public GameObject interactPrompt;

    [Header("Input")]
    public string interactActionName = "Interact";

    [Header("Panneaux UI")]
    [Tooltip("Rempli automatiquement au Start() — laisser vide en général")]
    public RoomManagementPanel[] managementPanels;

    // ─── Registre statique ────────────────────────────────────────

    public static readonly List<RoomSign> All = new();

    // ─── Privé ────────────────────────────────────────────────────

    RoomInstance              _room;
    readonly List<RoomPlacer> _playersInRange = new();
    int _frameHidden = -10;
    readonly System.Collections.Generic.Dictionary<RoomPlacer, bool> _wasPlacing = new();

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake()
    {
        All.Add(this);
        _room = GetComponent<RoomInstance>();
    }


    void Start()
    {
        // Auto-découverte des panneaux si non assignés dans l'Inspector
        if (managementPanels == null || managementPanels.Length == 0)
        {
            var found = FindObjectsByType<RoomManagementPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int maxIdx = 0;
            foreach (var p in found) maxIdx = Mathf.Max(maxIdx, p.playerIndex);
            managementPanels = new RoomManagementPanel[maxIdx + 1];
            foreach (var p in found)
                if (p.playerIndex < managementPanels.Length)
                    managementPanels[p.playerIndex] = p;
        }

        // Auto-calcul du range depuis la taille de la chambre
        if (interactRange <= 0f && _room?.Data != null)
        {
            float cs = GridManager.Instance != null ? GridManager.Instance.cellSize : 1f;
            interactRange = Mathf.Max(_room.Data.size.x, _room.Data.size.y) * cs * 0.5f + 2.5f;
        }

        // Crée l'indicateur world-space si non assigné
        if (interactPrompt == null)
            interactPrompt = CreateSignIndicator();

    }

    void OnDestroy()
    {
        All.Remove(this);

        // L'indicateur est un objet scène racine → le détruire manuellement
        if (interactPrompt != null && interactPrompt.transform.parent == null)
            Destroy(interactPrompt);

        // Ferme les panneaux ouverts
        for (int i = 0; i < _playersInRange.Count; i++)
        {
            var placer = _playersInRange[i];
            if (placer == null) continue;   // déjà détruit (ex: démolition chambre + joueur)
            int idx = GetPlayerIndex(placer);
            if (managementPanels != null && idx >= 0 && idx < managementPanels.Length
                && managementPanels[idx] != null)
                managementPanels[idx].Hide();
        }
    }

    void Update()
    {
        // Synchronise la position du panneau world-space (objet racine, ne suit pas le GO automatiquement)
        if (interactPrompt != null && interactPrompt.transform.parent == null)
        {
            float halfZ = transform.lossyScale.z * 0.5f;
            float halfX = transform.lossyScale.x * 0.25f;
            float halfY = transform.lossyScale.y * 0.3f;
            interactPrompt.transform.SetPositionAndRotation(
                transform.position - transform.forward * (halfZ + 0.05f) + transform.right * halfX + Vector3.up * halfY,
                transform.rotation * Quaternion.Euler(0f, 180f, 0f));
        }

        foreach (var placer in RoomPlacer.All)
        {
            if (placer == null) continue;

            float dist      = Vector3.Distance(transform.position, placer.transform.position);
            bool  inRange   = dist <= interactRange;
            bool  wasInRange = _playersInRange.Contains(placer);

            if (inRange && !wasInRange)
            {
                _playersInRange.Add(placer);
                if (interactPrompt != null && _playersInRange.Count == 1)
                    interactPrompt.SetActive(true);
            }
            else if (!inRange && wasInRange)
            {
                _playersInRange.Remove(placer);
                if (_playersInRange.Count == 0 && interactPrompt != null)
                    interactPrompt.SetActive(false);

                // Ferme le panel si le joueur s'éloigne
                int exitIdx = GetPlayerIndex(placer);
                if (managementPanels != null && exitIdx >= 0 && exitIdx < managementPanels.Length)
                    managementPanels[exitIdx]?.Hide();
            }

            int pIdx = GetPlayerIndex(placer);
            bool panelOpen = managementPanels != null && pIdx >= 0 && pIdx < managementPanels.Length
                          && managementPanels[pIdx] != null && managementPanels[pIdx].IsVisible;

            // Ferme automatiquement le panel si le joueur entre physiquement dans la chambre
            bool insideRoom = IsPlayerInsideRoom(placer);
            if (insideRoom && panelOpen)
                managementPanels[pIdx].Hide();

            // Ouvre le panel uniquement depuis l'extérieur, et seulement si aucun mode spécial actif
            bool isPicking          = placer.GetComponent<FurniturePicker>()?.IsPicking ?? false;
            bool isFurniturePlacing = placer.GetComponent<FurniturePlacer>()?.IsPlacing ?? false;

            // Détecte la fin d'un mode placement (chambre ou meuble) ce frame
            bool wasPlacing = _wasPlacing.TryGetValue(placer, out var wp) && wp;
            bool isPlacingNow = placer.IsPlacing || isFurniturePlacing;
            if (wasPlacing && !isPlacingNow) _frameHidden = Time.frameCount;
            _wasPlacing[placer] = isPlacingNow;

            bool recentlyHidden = Time.frameCount - _frameHidden < 3;
            int pIdx2 = GetPlayerIndex(placer);
            if (inRange && !insideRoom && !isPlacingNow && !isPicking && !panelOpen && !recentlyHidden && !InputConsumer.IsConsumed(pIdx2) && WasInteractPressed(placer) && IsNearestFor(placer))
                TogglePanel(placer);
        }
    }

    // ─── Indicateur world-space ───────────────────────────────────

    GameObject CreateSignIndicator()
    {
        var go = new GameObject("_RoomSignIndicator");

        // Position : sur le mur sud (mur porte, Z-), légèrement surélevée et décalée
        float halfZ = transform.lossyScale.z * 0.5f;
        float halfX = transform.lossyScale.x * 0.25f;
        float halfY = transform.lossyScale.y * 0.3f;
        go.transform.position = transform.position
            - transform.forward * (halfZ + 0.05f)
            + transform.right   * halfX
            + Vector3.up        * halfY;

        // Face le joueur (sens sortant du mur)
        go.transform.rotation = transform.rotation * Quaternion.Euler(0f, 180f, 0f);
        go.transform.localScale = Vector3.one * 0.015f;

        // Canvas world-space
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        if (Camera.main != null) canvas.worldCamera = Camera.main;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(90f, 28f);

        // Fond noir semi-transparent
        var bgGO = new GameObject("BG");
        bgGO.transform.SetParent(go.transform, false);
        var img = bgGO.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.8f);
        var bgRt = bgGO.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

        // Texte
        var tgo = new GameObject("Label");
        tgo.transform.SetParent(go.transform, false);
        var txt = tgo.AddComponent<TextMeshProUGUI>();
        txt.text = "[ I ] Gérer";
        txt.fontSize = 16f;
        txt.color = Color.white;
        txt.alignment = TextAlignmentOptions.Center;
        var trt = tgo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        go.SetActive(false);
        return go;
    }

    // ─── API publique ─────────────────────────────────────────────

    /// <summary>Masque l'indicateur world-space (ex: pendant un déplacement de chambre).</summary>
    public void HidePrompt() => interactPrompt?.SetActive(false);

    /// <summary>Recalcule interactRange d'après la nouvelle taille de chambre (ex: après upgrade).</summary>
    public void RefreshInteractRange()
    {
        if (_room?.Data == null) return;
        float cs = GridManager.Instance != null ? GridManager.Instance.cellSize : 1f;
        interactRange = Mathf.Max(_room.Data.size.x, _room.Data.size.y) * cs * 0.5f + 2.5f;
    }

    /// <summary>Raffraîchit la visibilité de l'indicateur selon les joueurs en portée.</summary>
    public void RefreshPrompt()
    {
        if (interactPrompt != null)
            interactPrompt.SetActive(_playersInRange.Count > 0);
    }

    // ─── Panneau ──────────────────────────────────────────────────

    void TogglePanel(RoomPlacer placer)
    {
        int idx = GetPlayerIndex(placer);
        if (managementPanels == null || idx < 0 || idx >= managementPanels.Length || managementPanels[idx] == null)
        {
            return;
        }

        var panel = managementPanels[idx];
        if (panel.IsVisible) { panel.Hide(); _frameHidden = Time.frameCount; }
        else                 panel.Show(_room, placer, idx);
    }

    // ─── Helpers ──────────────────────────────────────────────────

    /// <summary>Retourne true si cette chambre est la plus proche du joueur parmi tous les RoomSign.</summary>
    bool IsNearestFor(RoomPlacer placer)
    {
        float myDist = Vector3.Distance(transform.position, placer.transform.position);
        foreach (var other in All)
        {
            if (other == this || other == null) continue;
            if (!other._playersInRange.Contains(placer)) continue;
            if (Vector3.Distance(other.transform.position, placer.transform.position) < myDist)
                return false;
        }
        return true;
    }

    int GetPlayerIndex(RoomPlacer placer) =>
        placer.GetComponent<PlayerInput>()?.playerIndex ?? 0;

    bool WasInteractPressed(RoomPlacer placer)
    {
        var pi = placer.GetComponent<PlayerInput>();
        if (pi == null) return false;
        var action = pi.actions.FindAction(interactActionName, throwIfNotFound: false);
        return action?.WasPressedThisFrame() ?? false;
    }

    /// <summary>Retourne true si le joueur est physiquement à l'intérieur des murs de la chambre.</summary>
    bool IsPlayerInsideRoom(RoomPlacer placer)
    {
        var localPos = transform.InverseTransformPoint(placer.transform.position);
        return Mathf.Abs(localPos.x) < 0.48f && Mathf.Abs(localPos.z) < 0.48f;
    }
}
