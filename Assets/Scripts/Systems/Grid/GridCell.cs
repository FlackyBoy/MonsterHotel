using UnityEngine;

/// <summary>
/// Données d'une cellule de la grille.
/// </summary>
[System.Serializable]
public class GridCell
{
    public Vector2Int coords;
    public CellType type;

    // Objet posé sur cette cellule (meuble, station de service, etc.)
    public GameObject occupant;

    public bool IsOccupied => occupant != null;
    public bool IsWalkable => type == CellType.Floor || type == CellType.Room;

    public GridCell(int x, int y)
    {
        coords = new Vector2Int(x, y);
        type = CellType.Empty;
        occupant = null;
    }
}
