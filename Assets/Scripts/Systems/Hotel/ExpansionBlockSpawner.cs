using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Génère automatiquement des anneaux de blocs destructibles autour de la zone de départ.
///
/// Setup :
///   1. Placer un GameObject vide en scène, positionner là où tu veux le coin bas-gauche de la zone.
///   2. Attacher ce composant.
///   3. Créer un prefab cube (scale libre), y attacher ExpansionBlock.
///   4. Assigner les BlockData par anneau dans ringBlocks.
/// </summary>
public class ExpansionBlockSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [Tooltip("Cube avec le composant ExpansionBlock. Sa scale est préservée.")]
    public GameObject blockPrefab;

    [Header("Zone libre de départ (en cellules)")]
    [Tooltip("Largeur de la zone libre — doit correspondre à GridManager.width")]
    public int startWidth  = 10;
    [Tooltip("Hauteur de la zone libre — doit correspondre à GridManager.height")]
    public int startHeight = 10;
    [Tooltip("Taille d'une cellule de grille (unités monde)")]
    public float cellSize  = 1f;

    [Header("Anneaux")]
    [Tooltip("Écart en cellules entre le bord de la zone libre et le premier anneau")]
    public int ringGap   = 0;
    [Tooltip("Nombre d'anneaux à générer")]
    public int ringCount = 3;

    [Tooltip("BlockData par type, du plus proche au plus lointain.")]
    public BlockData[] ringBlocks;

    [Header("Distribution progressive")]
    [Tooltip("Force du bruit (0 = frontières nettes, 1 = très progressif)")]
    [Range(0f, 1f)]
    public float noiseStrength = 0.5f;
    [Tooltip("Échelle du bruit Perlin (valeurs plus grandes = transitions plus larges)")]
    public float noiseScale = 0.3f;

    // La difficulté progressive est gérée directement via les BlockData assignés par anneau (coût et durée sur chaque asset).

    // ─── Privé ────────────────────────────────────────────────────

    readonly List<GameObject> _spawnedBlocks = new();

    // ─── Lifecycle ────────────────────────────────────────────────

    void Start()
    {
        if (blockPrefab != null && _spawnedBlocks.Count == 0)
            GenerateBlocks();
    }

    // ─── Génération ───────────────────────────────────────────────

    public void GenerateBlocks()
    {
        ClearBlocks();

        if (blockPrefab == null) return;

        Vector3 anchor = transform.position;
        // Utilise le Renderer enfant pour obtenir la vraie taille du bloc
        var rend = blockPrefab.GetComponentInChildren<Renderer>();
        Vector3 blockSize = rend != null ? rend.bounds.size : blockPrefab.transform.localScale;
        float stepX = blockSize.x;
        float stepZ = blockSize.z;
        float halfH = blockSize.y * 0.5f;

        for (int ring = 0; ring < ringCount; ring++)
        {
            int gap = ringGap + ring;
            int ox  = -(gap + 1);
            int oy  = -(gap + 1);
            int w   = startWidth  + 2 * (gap + 1);
            int h   = startHeight + 2 * (gap + 1);

            for (int x = ox; x < ox + w; x++)
            {
                for (int y = oy; y < oy + h; y++)
                {
                    bool onEdge = x == ox || x == ox + w - 1 || y == oy || y == oy + h - 1;
                    if (!onEdge) continue;

                    Vector3 worldPos = anchor + new Vector3(
                        x * stepX + stepX * 0.5f,
                        halfH,
                        y * stepZ + stepZ * 0.5f);
                    BlockData data = GetBlockDataForPosition(x, y, ring);
                    SpawnBlock(worldPos, data);
                }
            }
        }
    }

    void SpawnBlock(Vector3 worldPos, BlockData data)
    {
        if (blockPrefab == null) return;

        var go = Instantiate(blockPrefab, worldPos, Quaternion.identity, transform);

        var block = go.GetComponentInChildren<ExpansionBlock>();
        if (block == null) block = go.AddComponent<ExpansionBlock>();

        block.blockData   = data;
        block.sizeInCells = Vector2Int.one;
        block.ApplyBlockColor();

        _spawnedBlocks.Add(go);
    }

    BlockData GetBlockDataForPosition(int x, int y, int ring)
    {
        if (ringBlocks == null || ringBlocks.Length == 0) return null;

        // Distance de Chebyshev depuis le centre de la zone libre (0 = bord intérieur, ringCount-1 = bord extérieur)
        float distNorm = ringCount > 1 ? (float)ring / (ringCount - 1) : 0f;

        // Bruit Perlin pour décaler la frontière organiquement
        float noise = (Mathf.PerlinNoise(x * noiseScale + 100f, y * noiseScale + 100f) - 0.5f) * noiseStrength;

        float t = Mathf.Clamp01(distNorm + noise);
        int index = Mathf.Clamp(Mathf.FloorToInt(t * ringBlocks.Length), 0, ringBlocks.Length - 1);
        return ringBlocks[index];
    }

    public void ClearBlocks()
    {
        foreach (var b in _spawnedBlocks)
            if (b != null) DestroyImmediate(b);
        _spawnedBlocks.Clear();
    }

    // ─── Gizmos ────────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        Vector3 anchor = transform.position;
        var rendG  = blockPrefab != null ? blockPrefab.GetComponentInChildren<Renderer>() : null;
        Vector3 ps = rendG != null ? rendG.bounds.size : (blockPrefab != null ? blockPrefab.transform.localScale : Vector3.one);
        float stepX = ps.x, stepZ = ps.z, blockH = ps.y;

        // Zone libre
        Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
        Vector3 freeCenter = anchor + new Vector3(startWidth * stepX * 0.5f, blockH * 0.5f, startHeight * stepZ * 0.5f);
        Gizmos.DrawCube(freeCenter, new Vector3(startWidth * stepX, blockH, startHeight * stepZ));

        // Anneaux
        for (int ring = 0; ring < ringCount; ring++)
        {
            float t = ringCount > 1 ? (float)ring / (ringCount - 1) : 0f;
            Gizmos.color = Color.Lerp(new Color(0.5f, 0.8f, 0.2f, 0.4f), new Color(0.8f, 0.1f, 0.1f, 0.4f), t);

            int gap = ringGap + ring;
            int ox  = -(gap + 1);
            int oy  = -(gap + 1);
            int w   = startWidth  + 2 * (gap + 1);
            int h   = startHeight + 2 * (gap + 1);

            Vector3 ringCenter = anchor + new Vector3((ox + w * 0.5f) * stepX, blockH * 0.5f, (oy + h * 0.5f) * stepZ);
            Gizmos.DrawWireCube(ringCenter, new Vector3(w * stepX, blockH, h * stepZ));
        }
    }
}
