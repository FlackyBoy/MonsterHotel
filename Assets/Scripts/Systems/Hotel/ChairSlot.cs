using UnityEngine;

/// <summary>
/// Emplacement de chaise autour d'une table.
/// Place ces GameObjects vides comme enfants du prefab Table.
/// FurniturePlacer snappera automatiquement une chaise (requiresChairSlot) sur le slot le plus proche.
/// </summary>
public class ChairSlot : MonoBehaviour
{
    public static readonly System.Collections.Generic.HashSet<ChairSlot> All = new();

    public bool IsOccupied { get; private set; }

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

    public void Occupy()  => IsOccupied = true;
    public void Release() => IsOccupied = false;
}
