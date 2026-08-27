using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Indicateur visuel toujours visible au-dessus de la chambre selon son RoomState (U6) — pièce
/// libre, à nettoyer ou à réparer. Contrairement à RoomSign (prompt d'interaction, visible
/// seulement à proximité), cet indicateur reste visible de loin en permanence pour repérer les
/// chambres à traiter sans avoir à s'en approcher. Rien ne s'affiche pour RoomState.Occupied (le
/// client est présent, aucune action côté joueur).
///
/// Ajouté automatiquement par RoomPlacer lors de la pose (même pattern que RoomSign/RoomDither).
/// </summary>
[RequireComponent(typeof(RoomInstance))]
public class RoomStateIndicator : MonoBehaviour
{
    [Header("Position")]
    [Tooltip("Décalage additionnel (monde) appliqué après le calcul auto au-dessus du toit — X/Z pour décaler latéralement, Y pour ajuster la hauteur en plus de la marge ci-dessous.")]
    public Vector3 offset = Vector3.zero;
    [Tooltip("Marge au-dessus du toit de la chambre (en plus de sa hauteur, transform.lossyScale.y)")]
    public float heightMargin = 1f;

    [Header("Couleurs par état")]
    public Color emptyColor       = new Color(0.25f, 0.8f, 0.35f, 0.92f);
    public Color dirtyColor       = new Color(0.9f, 0.7f, 0.1f, 0.92f);
    public Color needsRepairColor = new Color(0.85f, 0.2f, 0.2f, 0.92f);

    [Header("Libellés")]
    public string emptyLabel       = "Libre";
    public string dirtyLabel       = "À nettoyer";
    public string needsRepairLabel = "À réparer";

    RoomInstance          _room;
    GameObject            _root;
    Image                 _bg;
    TMPro.TextMeshProUGUI _label;
    bool                  _hiddenExternally;

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake() => _room = GetComponent<RoomInstance>();

    void Start()
    {
        BuildIndicator();
        _room.OnStateChanged += OnRoomStateChanged;
        Refresh();
    }

    void OnDestroy()
    {
        if (_room != null) _room.OnStateChanged -= OnRoomStateChanged;
        if (_root != null) Destroy(_root);
    }

    void Update()
    {
        if (_root == null || _hiddenExternally) return;
        _root.transform.position = transform.position + Vector3.up * (transform.lossyScale.y + heightMargin) + offset;
        WorldPrompt.FaceCamera(_root);
    }

    void OnRoomStateChanged(RoomInstance room) => Refresh();

    // ─── API publique (RoomPlacer, pendant un déplacement de chambre) ──

    /// <summary>Masque l'indicateur (ex: pendant un déplacement de la chambre).</summary>
    public void Hide()
    {
        _hiddenExternally = true;
        if (_root != null) _root.SetActive(false);
    }

    /// <summary>Réaffiche l'indicateur selon l'état courant (ex: fin de déplacement).</summary>
    public void RefreshVisibility()
    {
        _hiddenExternally = false;
        Refresh();
    }

    // ─── Interne ──────────────────────────────────────────────────

    void Refresh()
    {
        if (_root == null || _hiddenExternally) return;

        bool visible = _room.State != RoomState.Occupied;
        _root.SetActive(visible);
        if (!visible) return;

        Color color;
        string text;
        switch (_room.State)
        {
            case RoomState.Dirty:       color = dirtyColor;       text = dirtyLabel;       break;
            case RoomState.NeedsRepair: color = needsRepairColor; text = needsRepairLabel; break;
            default:                    color = emptyColor;       text = emptyLabel;       break; // Empty
        }

        if (_bg    != null) _bg.color = color;
        if (_label != null) _label.text = text;
    }

    void BuildIndicator()
    {
        var (root, label) = WorldPrompt.Create(110f, 30f, 0.02f);
        _root  = root;
        _label = label;
        _bg    = root.transform.Find("BG")?.GetComponent<Image>();
        _root.SetActive(false); // Refresh() décide de la visibilité réelle juste après
    }
}
