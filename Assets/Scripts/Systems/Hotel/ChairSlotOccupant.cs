using UnityEngine;

/// <summary>
/// Ajouté automatiquement par FurniturePlacer sur une chaise posée sur un ChairSlot.
/// Libère le slot quand la chaise est détruite ou retirée.
/// </summary>
public class ChairSlotOccupant : MonoBehaviour
{
    public ChairSlot Slot { get; set; }

    void OnDestroy()
    {
        Slot?.Release();
    }
}
