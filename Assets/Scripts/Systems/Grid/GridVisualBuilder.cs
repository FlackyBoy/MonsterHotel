using UnityEngine;

/// <summary>
/// Construit et dimensionne la grille visuelle affichée au sol pendant la construction (RW2).
/// Un seul Quad, retaillé/repositionné depuis GridManager plutôt que réglé à la main dans
/// l'Inspector — grandit automatiquement avec l'hôtel (voir GridManager.OnGridExpanded).
///
/// Visible uniquement côté joueur en train de construire : porté par le layer "BuildGrid",
/// masqué par défaut sur cam0/cam1 (SplitScreenManager.CreateCamera) et réactivé par
/// PlayerCamera pour la caméra du joueur concerné pendant qu'il construit.
///
/// Setup Unity requis : créer le Layer "BuildGrid" (Project Settings → Tags and Layers),
/// assigner un matériau utilisant le shader "MonsterHotel/BuildGridLines" (Assets/Materials/
/// BuildGrid_Mat.mat) sur le champ gridMaterial — dessine juste les contours de chaque case,
/// pas besoin de texture, le nombre de cases (_CellCount) est calculé automatiquement.
/// </summary>
public class GridVisualBuilder : MonoBehaviour
{
    static readonly int CellCountID = Shader.PropertyToID("_CellCount");

    [Tooltip("Matériau utilisant le shader MonsterHotel/BuildGridLines — le nombre de cases est calculé automatiquement.")]
    public Material gridMaterial;

    [Tooltip("Décalage vertical au-dessus du sol pour éviter le z-fighting")]
    public float heightOffset = 0.02f;

    Transform    _quad;
    MeshRenderer _renderer;

    void Start()
    {
        Build();
        if (GridManager.Instance != null)
            GridManager.Instance.OnGridExpanded += RefreshFromGrid;
        else
            Debug.LogWarning("[GridVisualBuilder] GridManager.Instance est null — la grille visuelle ne pourra pas se dimensionner.");
    }

    void OnDestroy()
    {
        if (GridManager.Instance != null)
            GridManager.Instance.OnGridExpanded -= RefreshFromGrid;
    }

    void Build()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "BuildGridVisual";
        go.transform.SetParent(transform, false);

        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col); // pas de collider — ne doit pas perturber les raycasts sol (DecorationPlacer/GridManager)

        _renderer = go.GetComponent<MeshRenderer>();
        if (gridMaterial != null) _renderer.material = gridMaterial; // instance dédiée (tuilage propre à cette grille)

        int buildGridLayer = LayerMask.NameToLayer("BuildGrid");
        if (buildGridLayer >= 0) go.layer = buildGridLayer;

        _quad = go.transform;
        RefreshFromGrid();
    }

    /// <summary>Repositionne/redimensionne la grille visuelle depuis les bornes actuelles de GridManager.</summary>
    public void RefreshFromGrid()
    {
        var grid = GridManager.Instance;
        if (grid == null || _quad == null) return;

        grid.GetOverallBounds(out int minX, out int minY, out int maxX, out int maxY);

        int cellsW = maxX - minX + 1;
        int cellsH = maxY - minY + 1;

        float worldW = cellsW * grid.cellSize;
        float worldH = cellsH * grid.cellSize;

        Vector3 center = grid.origin + new Vector3(
            (minX + cellsW * 0.5f) * grid.cellSize,
            heightOffset,
            (minY + cellsH * 0.5f) * grid.cellSize);

        _quad.position   = center;
        _quad.rotation   = Quaternion.Euler(90f, 0f, 0f);
        _quad.localScale = new Vector3(worldW, worldH, 1f);

        if (_renderer != null && _renderer.material != null)
            _renderer.material.SetVector(CellCountID, new Vector4(cellsW, cellsH, 0f, 0f));
    }
}
