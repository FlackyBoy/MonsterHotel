using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gère l'item tenu par le joueur (un seul à la fois).
/// Instancie le prefab visuel de l'item comme enfant du holdPoint.
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class PlayerItemHolder : MonoBehaviour
{
    public static readonly System.Collections.Generic.HashSet<PlayerItemHolder> All = new();
    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);
    [Header("Point de tenue")]
    [Tooltip("Transform enfant du joueur où l'item visuel est attaché")]
    public Transform holdPoint;

    public HoldableItemData HeldItem      { get; private set; }
    public bool             IsHoldingItem => HeldItem != null;

    GameObject _heldVisual;

    public void PickUp(HoldableItemData data)
    {
        if (data == null) return;
        Drop();

        HeldItem = data;

        if (data.heldPrefab != null && holdPoint != null)
        {
            // Instancie sans parent pour capturer la scale monde voulue
            _heldVisual = Instantiate(data.heldPrefab);
            Vector3 desiredWorldScale = _heldVisual.transform.lossyScale;

            // Parentage sans héritage de position world
            _heldVisual.transform.SetParent(holdPoint, false);
            _heldVisual.transform.localPosition = Vector3.zero;
            _heldVisual.transform.localRotation = Quaternion.identity;

            // Compense la scale non-uniforme du parent pour éviter l'anamorphose
            Vector3 ls = holdPoint.lossyScale;
            _heldVisual.transform.localScale = new Vector3(
                desiredWorldScale.x / ls.x,
                desiredWorldScale.y / ls.y,
                desiredWorldScale.z / ls.z
            );

            // Désactive la physique et les scripts du visuel
            foreach (var col in _heldVisual.GetComponentsInChildren<Collider>())
                col.enabled = false;
        }

    }

    public void Drop()
    {
        if (HeldItem == null) return;
        HeldItem = null;
        if (_heldVisual != null) { Destroy(_heldVisual); _heldVisual = null; }
    }
}
