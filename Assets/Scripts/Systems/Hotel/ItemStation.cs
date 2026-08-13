using UnityEngine;

/// <summary>
/// Station monde où les joueurs récupèrent ou rendent un item.
/// Détection par distance (même pattern que ReceptionInteractor).
/// Presser Interact sans item → prend l'item.
/// Presser Interact avec l'item correspondant → le rend.
/// </summary>
public class ItemStation : MonoBehaviour
{
    [Header("Item distribué")]
    public HoldableItemData itemData;

    [Header("Visuel de l'item disponible")]
    public Transform itemDisplayPoint;

    [Header("Interaction")]
    public float interactRange = 1.8f;
    public string interactActionName = "Interact";

    GameObject _displayVisual;

    void Start() => SpawnDisplay();

    void Update()
    {
        foreach (var holder in PlayerItemHolder.All)
        {
            if (holder == null) continue;
            if (Vector3.Distance(transform.position, holder.transform.position) > interactRange) continue;

            var pi = holder.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (pi == null || !pi.WasPressed(interactActionName)) continue;

            if (holder.HeldItem == itemData)
                holder.Drop();
            else if (!holder.IsHoldingItem)
                holder.PickUp(itemData);
        }
    }

    void SpawnDisplay()
    {
        if (itemDisplayPoint == null || itemData?.heldPrefab == null) return;
        _displayVisual = Instantiate(itemData.heldPrefab, itemDisplayPoint);
        _displayVisual.transform.localPosition = Vector3.zero;
        _displayVisual.transform.localRotation = Quaternion.identity;
    }
}
