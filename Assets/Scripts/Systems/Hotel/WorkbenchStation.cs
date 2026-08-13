using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Plan de travail de cuisine.
/// Idle → joueur dépose ingrédient → jauge qualité oscille → joueur capture → plat prêt → joueur récupère
/// </summary>
public class WorkbenchStation : MonoBehaviour
{
    [Header("Recettes")]
    public RecipeData[] recipes;

    [Header("UI")]
    public QualityBarUI qualityBar;

    [Header("Point de dépôt du plat produit")]
    public Transform outputPoint;

    [Header("Input")]
    public string interactActionName = "Interact";

    [Header("Interaction")]
    public float interactRange = 1.8f;

    // ─── État ──────────────────────────────────────────────────────

    enum State { Idle, WaitingQualityCapture, ReadyToPickup }

    State            _state = State.Idle;
    HoldableItemData _pendingResult;
    float            _capturedQuality;

    // ─── Update ────────────────────────────────────────────────────

    void Update()
    {
        foreach (var holder in PlayerItemHolder.All)
        {
            if (holder == null) continue;
            if (Vector3.Distance(transform.position, holder.transform.position) > interactRange) continue;

            var pi = holder.GetComponent<PlayerInput>();
            if (pi == null || !pi.WasPressed(interactActionName)) continue;

            HandleInteract(holder);
            break;
        }
    }

    // ─── Logique ───────────────────────────────────────────────────

    void HandleInteract(PlayerItemHolder holder)
    {
        switch (_state)
        {
            case State.Idle:                  TryStartCrafting(holder); break;
            case State.WaitingQualityCapture: CaptureQuality();         break;
            case State.ReadyToPickup:         GiveResultToPlayer(holder); break;
        }
    }

    void TryStartCrafting(PlayerItemHolder holder)
    {
        if (!holder.IsHoldingItem)
        {
            Debug.Log("[Cuisine] Plan de travail — le joueur n'a pas d'ingrédient en main.");
            return;
        }

        var recipe = FindRecipe(holder.HeldItem);
        if (recipe == null)
        {
            Debug.Log($"[Cuisine] Plan de travail — aucune recette pour '{holder.HeldItem.itemName}'.");
            return;
        }

        holder.Drop();
        _pendingResult = recipe.result;
        _state = State.WaitingQualityCapture;

        if (qualityBar == null)
            Debug.LogWarning("[Cuisine] qualityBar non assigné sur WorkbenchStation !");
        qualityBar?.Show();
        StartCoroutine(AutoCaptureAfter(recipe.workDuration));

        Debug.Log($"[Cuisine] Préparation lancée : '{recipe.recipeName}' ({recipe.workDuration}s). Appuie sur Interact pour capturer la qualité.");
    }

    void CaptureQuality()
    {
        _capturedQuality = qualityBar != null ? qualityBar.Capture() : 1f;
        _state = State.ReadyToPickup;
        qualityBar?.ShowFeedback(_capturedQuality);  // gère la fermeture du canvas en interne

        Debug.Log($"[Cuisine] Qualité capturée : {_capturedQuality:P0}. Appuie sur Interact pour récupérer '{_pendingResult?.itemName}'.");
    }

    void GiveResultToPlayer(PlayerItemHolder holder)
    {
        if (holder.IsHoldingItem)
        {
            Debug.Log("[Cuisine] Plan de travail — mains pleines, impossible de récupérer le plat.");
            return;
        }

        holder.PickUp(_pendingResult);

        var carrier = holder.GetComponent<FoodQualityCarrier>() ?? holder.gameObject.AddComponent<FoodQualityCarrier>();
        carrier.Quality = _capturedQuality;

        Debug.Log($"[Cuisine] Joueur récupère '{_pendingResult.itemName}' (qualité {_capturedQuality:P0}). Porte-le au comptoir de livraison.");

        _pendingResult = null;
        _state = State.Idle;
    }

    // ─── Helpers ───────────────────────────────────────────────────

    RecipeData FindRecipe(HoldableItemData ingredient)
    {
        foreach (var r in recipes)
            if (r != null && r.ingredient == ingredient) return r;
        return null;
    }

    IEnumerator AutoCaptureAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (_state == State.WaitingQualityCapture)
        {
            Debug.Log("[Cuisine] Temps écoulé — capture automatique de la qualité.");
            CaptureQuality();
        }
    }
}
