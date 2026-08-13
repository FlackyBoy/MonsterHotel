using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton gérant la grille de l'hôtel.
/// Gère les conversions coordonnées ↔ monde et l'état de chaque cellule.
/// Les cellules hors bornes initiales (expansion via blocs détruits) sont stockées
/// dans un dictionnaire séparé.
/// </summary>
public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Dimensions")]
    public int width = 10;
    public int height = 10;
    public float cellSize = 1f;

    [Header("Origine (coin bas-gauche)")]
    public Vector3 origin = Vector3.zero;

    [Header("Debug")]
    public bool showGizmos = true;
    public Color gizmoColorEmpty = new Color(1f, 1f, 1f, 0.1f);
    public Color gizmoColorFloor = new Color(0f, 1f, 0f, 0.2f);
    public Color gizmoColorWall  = new Color(1f, 0f, 0f, 0.4f);
    public Color gizmoColorRoom  = new Color(0f, 0.5f, 1f, 0.3f);

    GridCell[,] cells;
    readonly Dictionary<Vector2Int, GridCell> _expandedCells = new();

    // Rectangle englobant (coordonnées cellule) des cellules réellement débloquées (grille de
    // base + cellules étendues ouvertes via OpenExpandedCell) — utilisé par GridVisualBuilder
    // pour dimensionner la grille visuelle sans avoir à énumérer _expandedCells.
    int _boundsMinX, _boundsMinY, _boundsMaxX, _boundsMaxY;

    /// <summary>Déclenché quand une nouvelle cellule débloquée étend le rectangle englobant.</summary>
    public event System.Action OnGridExpanded;

    // ─── Lifecycle ───────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InitGrid();
    }

    // ─── Initialisation ──────────────────────────────────────────

    public void InitGrid()
    {
        cells = new GridCell[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                cells[x, y] = new GridCell(x, y);

        _boundsMinX = 0;
        _boundsMinY = 0;
        _boundsMaxX = width - 1;
        _boundsMaxY = height - 1;
    }

    /// <summary>Rectangle englobant (coordonnées cellule) couvrant la grille de base + toutes
    /// les cellules étendues actuellement débloquées (blocs d'expansion détruits).</summary>
    public void GetOverallBounds(out int minX, out int minY, out int maxX, out int maxY)
    {
        minX = _boundsMinX;
        minY = _boundsMinY;
        maxX = _boundsMaxX;
        maxY = _boundsMaxY;
    }

    // ─── Accès aux cellules ───────────────────────────────────────

    public GridCell GetCell(int x, int y)
    {
        // Tableau d'origine
        if (x >= 0 && x < width && y >= 0 && y < height)
            return cells[x, y];
        // Cellules étendues (blocs détruits hors bornes initiales)
        _expandedCells.TryGetValue(new Vector2Int(x, y), out var cell);
        return cell;
    }

    /// <summary>
    /// Crée une cellule étendue hors bornes et y place un occupant (ExpansionBlock).
    /// </summary>
    public void RegisterExpandedOccupant(int x, int y, GameObject obj)
    {
        if (x >= 0 && x < width && y >= 0 && y < height)
        {
            cells[x, y].occupant = obj;
            return;
        }
        var key = new Vector2Int(x, y);
        if (!_expandedCells.ContainsKey(key))
            _expandedCells[key] = new GridCell(x, y);
        _expandedCells[key].occupant = obj;
    }

    /// <summary>
    /// Libère une cellule étendue et la rend disponible au placement.
    /// </summary>
    public void OpenExpandedCell(int x, int y)
    {
        if (x >= 0 && x < width && y >= 0 && y < height)
        {
            cells[x, y].occupant = null;
            cells[x, y].type     = CellType.Empty;
            return;
        }
        var key = new Vector2Int(x, y);
        if (_expandedCells.TryGetValue(key, out var ec))
            ec.occupant = null;
        else
            _expandedCells[key] = new GridCell(x, y);

        // Étend le rectangle englobant si cette cellule débloquée sort des bornes connues —
        // c'est le vrai moment où le "territoire" jouable grandit (contrairement à
        // RegisterExpandedOccupant, qui marque juste l'empreinte d'un bloc encore intact/bloqué).
        bool grew = x < _boundsMinX || x > _boundsMaxX || y < _boundsMinY || y > _boundsMaxY;
        if (grew)
        {
            _boundsMinX = Mathf.Min(_boundsMinX, x);
            _boundsMinY = Mathf.Min(_boundsMinY, y);
            _boundsMaxX = Mathf.Max(_boundsMaxX, x);
            _boundsMaxY = Mathf.Max(_boundsMaxY, y);
            OnGridExpanded?.Invoke();
        }
    }

    public GridCell GetCell(Vector2Int coords) => GetCell(coords.x, coords.y);

    public GridCell WorldToCell(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt((worldPos.x - origin.x) / cellSize);
        int y = Mathf.FloorToInt((worldPos.z - origin.z) / cellSize); // top-down : Z = profondeur
        return GetCell(x, y);
    }

    // ─── Conversions ──────────────────────────────────────────────

    /// <summary>Centre monde de la cellule (x, y).</summary>
    public Vector3 CellToWorld(int x, int y)
    {
        return origin + new Vector3(x * cellSize + cellSize * 0.5f,
                                    0f,
                                    y * cellSize + cellSize * 0.5f);
    }

    public Vector3 CellToWorld(Vector2Int coords) => CellToWorld(coords.x, coords.y);

    // ─── Utilitaires ──────────────────────────────────────────────

    public bool IsInBounds(int x, int y) =>
        (x >= 0 && x < width && y >= 0 && y < height)
        || _expandedCells.ContainsKey(new Vector2Int(x, y));

    /// <summary>Retourne true si la cellule est bloquée par un ExpansionBlock encore intact.</summary>
    public bool IsBlockedByExpansion(int x, int y)
    {
        var key = new Vector2Int(x, y);
        return _expandedCells.TryGetValue(key, out var cell) && cell.IsOccupied;
    }

    public bool IsWalkable(int x, int y)
    {
        var cell = GetCell(x, y);
        return cell != null && cell.IsWalkable;
    }

    public bool IsFree(int x, int y)
    {
        var cell = GetCell(x, y);
        return cell != null && cell.IsWalkable && !cell.IsOccupied;
    }

    /// <summary>Pose un objet sur la cellule. Retourne false si cellule invalide ou déjà occupée.</summary>
    public bool PlaceOccupant(int x, int y, GameObject obj)
    {
        var cell = GetCell(x, y);
        if (cell == null || cell.IsOccupied) return false;
        cell.occupant = obj;
        return true;
    }

    /// <summary>Retire l'occupant de la cellule.</summary>
    public void ClearOccupant(int x, int y)
    {
        var cell = GetCell(x, y);
        if (cell != null) cell.occupant = null;
    }

    /// <summary>Libère un rectangle de cellules (type → Empty, occupant → null).</summary>
    public void FreeRect(int x, int y, int w, int h)
    {
        for (int cx = x; cx < x + w; cx++)
            for (int cy = y; cy < y + h; cy++)
            {
                var cell = GetCell(cx, cy);
                if (cell != null) { cell.type = CellType.Empty; cell.occupant = null; }
            }
    }

    /// <summary>Définit le type d'un rectangle de cellules.</summary>
    public void SetRect(int x, int y, int w, int h, CellType type)
    {
        for (int cx = x; cx < x + w; cx++)
            for (int cy = y; cy < y + h; cy++)
            {
                var cell = GetCell(cx, cy);
                if (cell != null) cell.type = type;
            }
    }

    // ─── Gizmos ───────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 center = origin + new Vector3(x * cellSize + cellSize * 0.5f,
                                                      0f,
                                                      y * cellSize + cellSize * 0.5f);
                Vector3 size   = new Vector3(cellSize * 0.98f, 0.02f, cellSize * 0.98f);

                CellType type = CellType.Empty;
                if (cells != null) type = cells[x, y].type;

                Gizmos.color = type switch
                {
                    CellType.Floor => gizmoColorFloor,
                    CellType.Wall  => gizmoColorWall,
                    CellType.Room  => gizmoColorRoom,
                    _              => gizmoColorEmpty,
                };

                Gizmos.DrawCube(center, size);

                // Contour
                Gizmos.color = new Color(0f, 0f, 0f, 0.25f);
                Gizmos.DrawWireCube(center, size);
            }
        }
    }
}
