using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Poubelle / étagère de dépôt.
/// Le joueur interagit pour jeter l'objet qu'il tient.
/// Place ce composant sur une poubelle ou un meuble de dépôt.
/// </summary>
public class TrashStation : MonoBehaviour
{
    [Header("Input")]
    public string interactActionName = "Interact";

    [Header("Interaction")]
    public float interactRange = 1.8f;

    void Update()
    {
        foreach (var holder in PlayerItemHolder.All)
        {
            if (holder == null || !holder.IsHoldingItem) continue;
            if (Vector3.Distance(transform.position, holder.transform.position) > interactRange) continue;

            var pi = holder.GetComponent<PlayerInput>();
            if (pi == null || !pi.WasPressed(interactActionName)) continue;

            Debug.Log($"[Cuisine] Plat '{holder.HeldItem.itemName}' jeté à la poubelle.");
            holder.Drop();
            break;
        }
    }
}
