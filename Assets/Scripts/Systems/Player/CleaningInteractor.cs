using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gère le nettoyage des meubles sales, la réparation des meubles abimés, et le ramassage des détritus.
///
/// Règles :
///   Meuble sale   → nécessite un Balai (HoldableItemType.Mop)
///   Meuble abimé  → nécessite une Clé  (HoldableItemType.Wrench)
///   Détritus sol  → aucun item requis, Interact suffit
///
/// Ce composant est passif : il ne s'active que quand aucun autre mode exclusif n'est actif
/// (FurniturePicker, FurniturePlacer, RoomPlacer).
/// </summary>
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerItemHolder))]
public class CleaningInteractor : MonoBehaviour
{
    [Header("Détection")]
    public float interactRange = 1.5f;

    [Header("Noms des actions")]
    public string interactActionName = "Interact";

    // ─── Privé ────────────────────────────────────────────────────

    PlayerInput      _playerInput;
    PlayerItemHolder _holder;
    FurniturePicker  _picker;
    FurniturePlacer  _furniturePlacer;
    RoomPlacer       _roomPlacer;

    FurnitureInstance             _targetFurniture;
    DebrisInstance                _targetDebris;
    GameObject                    _prompt;
    TMPro.TextMeshProUGUI         _promptLabel;

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake()
    {
        _playerInput     = GetComponent<PlayerInput>();
        _holder          = GetComponent<PlayerItemHolder>();
        _picker          = GetComponent<FurniturePicker>();
        _furniturePlacer = GetComponent<FurniturePlacer>();
        _roomPlacer      = GetComponent<RoomPlacer>();
    }

    void Update()
    {
        if (IsAnyOtherModeActive())
        {
            ClearTargets();
            return;
        }

        FindBestTarget();
        UpdatePrompt();

        // N'agit que s'il y a une cible valide (évite d'interférer avec d'autres systèmes)
        if (_targetDebris == null && _targetFurniture == null) return;

        var action = _playerInput.actions.FindAction(interactActionName, throwIfNotFound: false);
        if (action != null && action.WasPressedThisFrame())
            TryInteract();
    }

    /// <summary>Debug : force le joueur à tenir un balai (sans station).</summary>
    [ContextMenu("DEBUG — Prendre Balai")]
    void DebugPickMop()
    {
        if (_holder == null) return;
        var fakeItem = ScriptableObject.CreateInstance<HoldableItemData>();
        fakeItem.itemName = "Balai (debug)";
        fakeItem.itemType = HoldableItemType.Mop;
        _holder.PickUp(fakeItem);
    }

    /// <summary>Debug : force le joueur à tenir une clé (sans station).</summary>
    [ContextMenu("DEBUG — Prendre Clé")]
    void DebugPickWrench()
    {
        if (_holder == null) return;
        var fakeItem = ScriptableObject.CreateInstance<HoldableItemData>();
        fakeItem.itemName = "Clé (debug)";
        fakeItem.itemType = HoldableItemType.Wrench;
        _holder.PickUp(fakeItem);
    }

    void OnDestroy() => DestroyPrompt();

    // ─── Détection ────────────────────────────────────────────────

    void FindBestTarget()
    {
        FurnitureInstance bestFurni  = null;
        DebrisInstance    bestDebris = null;
        float             bestDist   = interactRange;

        // Scan des détritus
        foreach (var debris in DebrisInstance.All)
        {
            float dist = Vector3.Distance(transform.position, debris.transform.position);
            if (dist < bestDist) { bestDist = dist; bestDebris = debris; bestFurni = null; }
        }

        // Scan des meubles sales / abimés
        foreach (var fi in FurnitureInstance.All)
        {
            if (!fi.IsDirty && !fi.IsDamaged) continue;
            float dist = Vector3.Distance(transform.position, fi.transform.position);
            if (dist < bestDist) { bestDist = dist; bestFurni = fi; bestDebris = null; }
        }

        _targetFurniture = bestFurni;
        _targetDebris    = bestDebris;
    }

    // ─── Interaction ──────────────────────────────────────────────

    void TryInteract()
    {
        if (_targetDebris != null)
        {
            _targetDebris.PickUp();
            _targetDebris = null;
            return;
        }

        if (_targetFurniture == null) return;

        if (_targetFurniture.IsDirty && _holder.HeldItem?.itemType == HoldableItemType.Mop)
        {
            _targetFurniture.SetDirty(false);
            return;
        }

        if (_targetFurniture.IsDamaged && _holder.HeldItem?.itemType == HoldableItemType.Wrench)
        {
            _targetFurniture.SetDamaged(false);
            return;
        }
    }

    // ─── Prompt ───────────────────────────────────────────────────

    void UpdatePrompt()
    {
        if (_targetDebris != null)
        {
            ShowPrompt(
                _targetDebris.transform.position + Vector3.up * 0.5f,
                $"[ {interactActionName} ] Ramasser les détritus");
            return;
        }

        if (_targetFurniture != null)
        {
            string msg = null;

            if (_targetFurniture.IsDirty)
                msg = _holder.HeldItem?.itemType == HoldableItemType.Mop
                    ? $"[ {interactActionName} ] Nettoyer : {_targetFurniture.Data?.furnitureName}"
                    : $"⚠ Besoin d'un balai pour nettoyer";

            else if (_targetFurniture.IsDamaged)
                msg = _holder.HeldItem?.itemType == HoldableItemType.Wrench
                    ? $"[ {interactActionName} ] Réparer : {_targetFurniture.Data?.furnitureName}"
                    : $"⚠ Besoin d'une clé pour réparer";

            if (msg != null)
            {
                var source = _targetFurniture.CurrentVisual != null
                    ? _targetFurniture.CurrentVisual
                    : _targetFurniture.gameObject;
                var b = BoundsUtils.Get(source);
                ShowPrompt(b.center + Vector3.up * (b.extents.y + 0.3f), msg);
                return;
            }
        }

        DestroyPrompt();
    }

    void ShowPrompt(Vector3 worldPos, string text)
    {
        if (_prompt == null)
            (_prompt, _promptLabel) = WorldPrompt.Create(260f, 32f);

        _prompt.transform.position = worldPos;
        WorldPrompt.FaceCamera(_prompt);
        if (_promptLabel != null) _promptLabel.text = text;
    }

    void DestroyPrompt()
    {
        if (_prompt != null) { Destroy(_prompt); _prompt = null; }
    }

    void ClearTargets()
    {
        _targetFurniture = null;
        _targetDebris    = null;
        DestroyPrompt();
    }

    // ─── Helpers ──────────────────────────────────────────────────

    bool IsAnyOtherModeActive()
    {
        if (_picker != null && _picker.IsPicking)           return true;
        if (_furniturePlacer != null && _furniturePlacer.IsPlacing) return true;
        if (_roomPlacer != null && _roomPlacer.IsPlacing)   return true;
        return false;
    }

}
