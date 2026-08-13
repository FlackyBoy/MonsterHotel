using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Génère un placeholder visuel de chambre en primitives Unity.
///
/// Structure (toutes dans l'espace local [-0.5 .. 0.5]) :
///   Sol   — quad plat
///   Mur N — plein
///   Mur S — deux demi-murs avec ouverture (porte)
///   Mur E / Mur W — pleins
///
/// Le RoomPlacer scale le parent à (footprint × cellSize),
/// donc tout doit être conçu en espace unitaire [−0.5, 0.5].
///
/// Usage : attache ce script à un prefab vide, appuie sur Play
/// ou sur le bouton "Régénérer" dans l'Inspector pour voir le résultat.
/// Une fois satisfait, tu peux remplacer par de vrais assets graphiques.
/// </summary>
[ExecuteAlways]
public class RoomPlaceholder : MonoBehaviour
{
    [Header("Proportions")]
    [Range(0.02f, 0.2f)]
    [Tooltip("Épaisseur des murs en fraction de la taille (0.05 = 5 %)")]
    public float wallThickness = 0.05f;

    [Range(0.1f, 0.5f)]
    [Tooltip("Largeur de l'ouverture de porte en fraction (0.30 = 30 %)")]
    public float doorWidth = 0.30f;

    [Range(0.1f, 0.8f)]
    [Tooltip("Hauteur des murs en fraction de la taille")]
    public float wallHeight = 0.25f;

    [Range(0.01f, 0.1f)]
    public float floorHeight = 0.02f;

    [Header("Matériaux")]
    public Material floorMaterial;
    public Material wallMaterial;
    public Material doorMaterial;   // cadre de porte (optionnel)

    [Header("Debug")]
    public bool regenerateOnValidate = false;

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake() => HideBakedChildren();

    void HideBakedChildren()
    {
        foreach (Transform child in transform)
            child.gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (regenerateOnValidate)
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null) Build();
            };
    }
#endif

    // ─── Construction ─────────────────────────────────────────────

    [ContextMenu("Régénérer")]
    public void Build()
    {
        // Supprime les anciens enfants générés
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name.StartsWith("__ph_"))
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(child.gameObject);
                else
#endif
                    Destroy(child.gameObject);
            }
        }

        // Sol
        CreateBox("__ph_floor",
            pos:   new Vector3(0f, 0f, 0f),
            scale: new Vector3(1f, floorHeight, 1f),
            mat:   floorMaterial);

        float wt  = wallThickness;
        float wh  = wallHeight;
        float wy  = floorHeight + wh * 0.5f;   // centre Y des murs

        // Mur Nord (Z+)
        CreateBox("__ph_wallN",
            pos:   new Vector3(0f, wy, 0.5f - wt * 0.5f),
            scale: new Vector3(1f, wh, wt),
            mat:   wallMaterial);

        // Mur Sud (Z−) avec ouverture de porte centrée
        float sideW = (1f - doorWidth) * 0.5f;
        float sideX = (sideW + doorWidth) * 0.5f;

        CreateBox("__ph_wallSL",
            pos:   new Vector3(-sideX * 0.5f - doorWidth * 0.25f, wy, -0.5f + wt * 0.5f),
            scale: new Vector3(sideW, wh, wt),
            mat:   wallMaterial);

        CreateBox("__ph_wallSR",
            pos:   new Vector3(sideX * 0.5f + doorWidth * 0.25f, wy, -0.5f + wt * 0.5f),
            scale: new Vector3(sideW, wh, wt),
            mat:   wallMaterial);

        // Linteau au-dessus de la porte
        float lintelH = wh * 0.25f;
        CreateBox("__ph_lintel",
            pos:   new Vector3(0f, floorHeight + wh - lintelH * 0.5f, -0.5f + wt * 0.5f),
            scale: new Vector3(doorWidth, lintelH, wt),
            mat:   doorMaterial != null ? doorMaterial : wallMaterial);

        // Mur Est (X+)
        CreateBox("__ph_wallE",
            pos:   new Vector3(0.5f - wt * 0.5f, wy, 0f),
            scale: new Vector3(wt, wh, 1f),
            mat:   wallMaterial);

        // Mur Ouest (X−)
        CreateBox("__ph_wallW",
            pos:   new Vector3(-0.5f + wt * 0.5f, wy, 0f),
            scale: new Vector3(wt, wh, 1f),
            mat:   wallMaterial);

        // Point d'entrée devant la porte (côté Sud)
        var entry = new GameObject("__ph_entryPoint");
        entry.transform.SetParent(transform, false);
        entry.transform.localPosition = new Vector3(0f, 0f, -0.5f - wt);
    }

    // ─── API ──────────────────────────────────────────────────────

    /// <summary>Active les colliders sur tous les enfants (appelé après placement définitif).</summary>
    public void EnableColliders()
    {
        foreach (var col in GetComponentsInChildren<Collider>(includeInactive: true))
            col.enabled = true;
    }

    /// <summary>Active les NavMeshObstacles (appelé après placement définitif, pas sur le ghost).</summary>
    public void EnableNavMeshObstacles()
    {
        foreach (var obs in GetComponentsInChildren<NavMeshObstacle>(includeInactive: true))
            obs.enabled = true;
    }

    // ─── Helpers ──────────────────────────────────────────────────

    GameObject CreateBox(string partName, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = partName;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale    = scale;

        // Désactive le collider physique (pas besoin de physique sur les murs)
        var col = go.GetComponent<Collider>();
        if (col != null) col.enabled = false;


        if (mat != null)
            go.GetComponent<Renderer>().sharedMaterial = mat;

        return go;
    }
}
