using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Frigo : le joueur interagit pour ouvrir un panneau de sélection d'ingrédients.
/// Remplace ItemStation pour le frigo.
/// </summary>
public class FridgeStation : MonoBehaviour
{
    [Header("Ingrédients disponibles")]
    public HoldableItemData[] ingredients;

    [Header("Interaction")]
    public float  interactRange      = 1.8f;
    public string interactActionName = "Interact";

    // FridgeUI trouvé automatiquement au démarrage (pas besoin d'assigner dans le prefab)
    FridgeUI _fridgeUI;

    void Start()
    {
        _fridgeUI = Object.FindFirstObjectByType<FridgeUI>(FindObjectsInactive.Include);
    }

    void Update()
    {
        if (_fridgeUI == null)
            _fridgeUI = Object.FindFirstObjectByType<FridgeUI>(FindObjectsInactive.Include);

        foreach (var holder in PlayerItemHolder.All)
        {
            if (holder == null) continue;
            if (holder.IsHoldingItem) continue;
            if (Vector3.Distance(transform.position, holder.transform.position) > interactRange) continue;

            var pi = holder.GetComponent<PlayerInput>();
            if (pi == null || !pi.WasPressed(interactActionName)) continue;

            Debug.Log($"[FridgeStation] Interact — fridgeUI={_fridgeUI != null}, ingredients={ingredients?.Length ?? 0}");
            _fridgeUI?.Open(holder, ingredients);
            break;
        }
    }
}
