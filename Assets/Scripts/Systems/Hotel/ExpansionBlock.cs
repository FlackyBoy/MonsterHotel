using MoreMountains.Feedbacks;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Bloc destructible entourant l'hôtel (style Dungeon Keeper).
/// Maintenir Interact pour creuser — progression sauvegardée entre sessions.
/// Feedback visuel : assombrissement progressif + flash jaune pendant le creusage.
/// </summary>
public class ExpansionBlock : MonoBehaviour
{
    [Header("Data")]
    public BlockData blockData;

    [Header("Taille en cellules de grille")]
    public Vector2Int sizeInCells = Vector2Int.one;

    [Header("Debug")]
    public bool debugNoDestroy = false;

    [Header("Interaction")]
    public string interactActionName = "Interact";

    [Header("Feedbacks")]
    public MMF_Player feedbackDestruction;
    public MMF_Player feedbackDigging;

    [Header("Highlight")]
    public Color highlightEmission = new Color(0.4f, 0.2f, 0f);
    public Color diggingEmission   = new Color(0.5f, 0.4f, 0f);
    [Range(0f, 1f)]
    public float damageDarkening   = 0.85f;


    // ── Registre statique ──────────────────────────────────────────
    public static readonly System.Collections.Generic.List<ExpansionBlock> All = new();

    // Cache : bloc le plus proche par joueur, recalculé une fois par frame
    static readonly System.Collections.Generic.Dictionary<RoomPlacer, ExpansionBlock> _nearestCache = new();
    static int _nearestCacheFrame = -1;

    // Joueurs qui doivent relâcher Interact avant de pouvoir creuser ce bloc
    readonly System.Collections.Generic.HashSet<RoomPlacer> _requireRelease = new();

    static void RefreshNearestCache(float range, float hysteresis = 0.75f)
    {
        if (Time.frameCount == _nearestCacheFrame) return;
        _nearestCacheFrame = Time.frameCount;

        foreach (var placer in RoomPlacer.All)
        {
            if (placer == null) continue;
            Vector3 forward = placer.transform.forward;
            Vector3 pos     = placer.transform.position;

            // Conserve la cible précédente pour l'hysterèse
            _nearestCache.TryGetValue(placer, out var prev);

            ExpansionBlock best = null;
            float bestScore = float.MaxValue;

            foreach (var block in All)
            {
                if (block == null || block._completed) continue;
                var b = block._renderer != null ? block._renderer.bounds : new Bounds(block.transform.position, Vector3.one);
                Vector3 closest = b.ClosestPoint(pos);
                float d = Vector3.Distance(closest, pos);
                if (d > range) continue;

                float dot   = Vector3.Dot(forward, (closest - pos).normalized);
                float score = d * (2f - dot);

                // Hysterèse : le bloc déjà ciblé garde un avantage pour éviter les alternances
                if (block == prev && !prev._completed) score *= hysteresis;

                if (score < bestScore) { best = block; bestScore = score; }
            }

            if (best != null)
                _nearestCache[placer] = best;
            else
                _nearestCache.Remove(placer);
        }
    }

    // ── Progression ────────────────────────────────────────────────
    [Range(0f, 1f)]
    [SerializeField] float _progress;
    bool _completed;

    // ── UI — plus de prompt, le feedback est visuel (couleur) ──────

    // ── Visuel dégâts ──────────────────────────────────────────────
    Renderer              _renderer;
    MaterialPropertyBlock _mpb;
    Color                 _baseColor;
    static readonly int   ColorID = Shader.PropertyToID("_BaseColor");

    // ── Grille ─────────────────────────────────────────────────────
    Vector2Int _gridOrigin;
    bool       _gridRegistered;

    // ─── Lifecycle ──────────────────────────────────────────────────

    void Awake()
    {
        All.Add(this);
        _renderer = GetComponentInChildren<MeshRenderer>();
        _mpb      = new MaterialPropertyBlock();
    }

    void Start()
    {
        ApplyBlockColor();
        RegisterInGrid();
    }

    void OnDestroy() => All.Remove(this);

    void Update()
    {
        if (_completed) return;

        var   cfg        = HotelConfig.Instance;
        float range      = cfg != null ? cfg.blockInteractRange      : 3f;
        float hysteresis = cfg != null ? cfg.blockTargetHysteresis   : 0.75f;

        RefreshNearestCache(range, hysteresis);

        // Règle : on ne peut creuser que si ce bloc est highlighted (= nearest pour ce joueur)
        _highlighted = false;
        foreach (var placer in RoomPlacer.All)
        {
            if (placer == null) continue;
            if (_nearestCache.TryGetValue(placer, out var nearest) && nearest == this)
                _highlighted = true;
        }

        if (!_highlighted)
        {
            UpdateDamageColor(false, false);
            return;
        }

        // Creusage
        bool digging = false;
        foreach (var placer in RoomPlacer.All)
        {
            if (placer == null) continue;
            if (!(_nearestCache.TryGetValue(placer, out var nearest) && nearest == this)) continue;

            var pi = placer.GetComponent<PlayerInput>();
            if (pi == null) continue;
            var action = pi.actions.FindAction(interactActionName, false);
            if (action == null) continue;

            // Si la touche est relâchée, ce joueur peut de nouveau creuser ce bloc
            if (!action.IsPressed()) { _requireRelease.Remove(placer); continue; }

            // Si le joueur tenait déjà la touche quand ce bloc est devenu le nearest, on attend qu'il relâche
            if (_requireRelease.Contains(placer)) continue;

            if (InputConsumer.IsConsumed(pi.playerIndex)) continue;
            InputConsumer.Consume(pi.playerIndex);
            if (!CanAffordDig()) continue;

            if (!digging) feedbackDigging?.PlayFeedbacks(transform.position);
            digging = true;
            float duration = blockData != null ? blockData.digDuration : 5f;
            _progress += Time.deltaTime / duration;

            if (_progress >= 1f)
            {
                // Tous les blocs devront attendre un relâché avant d'accepter l'input
                foreach (var p in RoomPlacer.All)
                    if (p != null) foreach (var blk in All) if (blk != null) blk._requireRelease.Add(p);
                CompleteDigging();
                return;
            }
        }

        UpdateDamageColor(_highlighted, digging);
    }
    bool _highlighted;


    // ─── Logique ────────────────────────────────────────────────────

    bool IsNearestBlockFor(RoomPlacer placer, Bounds myBounds)
    {
        float myDist = Vector3.Distance(myBounds.ClosestPoint(placer.transform.position), placer.transform.position);
        foreach (var other in All)
        {
            if (other == this || other == null || other._completed) continue;
            var otherBounds = other._renderer != null ? other._renderer.bounds : new Bounds(other.transform.position, other.transform.lossyScale);
            float otherDist = Vector3.Distance(otherBounds.ClosestPoint(placer.transform.position), placer.transform.position);
            if (otherDist < myDist) return false;
            // Tiebreaker : à distance égale, seul le bloc avec le plus petit instanceID gagne
            if (Mathf.Approximately(otherDist, myDist) && other.GetInstanceID() < GetInstanceID()) return false;
        }
        return true;
    }

    int ActualCost() => blockData != null ? blockData.cost : 0;

    bool CanAffordDig()
    {
        if (blockData == null || EconomyManager.Instance == null) return true;
        return EconomyManager.Instance.CanAfford(ActualCost());
    }

    void CompleteDigging()
    {
        if (_completed) return;
        _completed = true;
        _progress  = 1f;

        feedbackDestruction?.PlayFeedbacks(transform.position);

        if (blockData != null && EconomyManager.Instance != null)
            EconomyManager.Instance.TrySpend(ActualCost());

        UnregisterFromGrid();

        var surface = Object.FindFirstObjectByType<NavMeshSurface>();
        if (surface != null)
            StartCoroutine(RebakeAndDestroy(surface));
        else
            StartCoroutine(DestroyAfterFeedback());
    }

    System.Collections.IEnumerator RebakeAndDestroy(NavMeshSurface surface)
    {
        // UpdateNavMesh() (incrémental) — voir RoomPlacer.RebakeNavMeshAsync() pour le détail
        // (BuildNavMesh() complet causait un gros ralentissement en jeu, reverté).
        yield return surface.UpdateNavMesh(surface.navMeshData);
        yield return DestroyAfterFeedback();
    }

    System.Collections.IEnumerator DestroyAfterFeedback()
    {
        float duration = feedbackDestruction != null ? feedbackDestruction.TotalDuration : 0f;
        yield return new WaitForSeconds(duration);
        if (debugNoDestroy)
        {
            FeedbackManager.Instance?.ResetBlockAfterDelay(this, 0f);
            yield break;
        }
        Destroy(gameObject);
    }

    public void DebugReset()
    {
        var blink = GetComponent<MoreMountains.Feedbacks.MMBlink>();
        if (blink != null) blink.StopBlinking();
        _completed = false;
        _progress  = 0f;
        gameObject.SetActive(true);
        if (_renderer != null) _renderer.enabled = true;
        ApplyBlockColor();
    }

    // ─── Grille ─────────────────────────────────────────────────────

    void RegisterInGrid()
    {
        var grid = GridManager.Instance;
        if (grid == null) return;

        float cs = grid.cellSize;
        int ox = Mathf.FloorToInt((transform.position.x - grid.origin.x) / cs) - sizeInCells.x / 2;
        int oy = Mathf.FloorToInt((transform.position.z - grid.origin.z) / cs) - sizeInCells.y / 2;

        _gridOrigin     = new Vector2Int(ox, oy);
        _gridRegistered = true;

        for (int x = ox; x < ox + sizeInCells.x; x++)
            for (int y = oy; y < oy + sizeInCells.y; y++)
                grid.RegisterExpandedOccupant(x, y, gameObject);
    }

    void UnregisterFromGrid()
    {
        if (!_gridRegistered) return;
        var grid = GridManager.Instance;
        if (grid == null) return;
        for (int x = _gridOrigin.x; x < _gridOrigin.x + sizeInCells.x; x++)
            for (int y = _gridOrigin.y; y < _gridOrigin.y + sizeInCells.y; y++)
            {
                grid.ClearOccupant(x, y);
                grid.OpenExpandedCell(x, y);
            }
    }

    // ─── Visuel dégâts ───────────────────────────────────────────────

    public void ApplyBlockColor()
    {
        if (blockData == null || _renderer == null) return;
        _baseColor = blockData.blockColor;
        // Active le keyword _EMISSION une fois sur l'instance material — obligatoire pour URP Lit
        _renderer.material.EnableKeyword("_EMISSION");
        // MPB sans lecture préalable (évite de conserver un _BaseColor injecté par un feedback)
        _mpb.SetColor("_BaseColor",     _baseColor);
        _mpb.SetColor("_EmissionColor", Color.black);
        // Optionnelle — le matériau du bloc doit exposer _BaseMap (URP Lit standard, ou
        // MonsterHotel/RoomWallDither|RoomWallFade pour cumuler avec le dithering). Réglée une
        // seule fois ici : UpdateDamageColor() ne touche jamais _BaseMap, donc elle reste posée
        // sur le MPB réutilisé entre les deux appels.
        if (blockData.blockTexture != null)
            _mpb.SetTexture("_BaseMap", blockData.blockTexture);
        _renderer.SetPropertyBlock(_mpb);
    }

    void UpdateDamageColor(bool inRange, bool digging)
    {
        if (_renderer == null) return;

        // Assombrissement progressif quelle que soit l'état
        Color baseCol = Color.Lerp(_baseColor, Color.black, _progress * damageDarkening);

        Color emissCol;
        if (inRange && !digging)
            emissCol = highlightEmission;
        else if (inRange && digging)
            emissCol = diggingEmission;
        else
            emissCol = Color.black;

        _mpb.SetColor("_BaseColor",     baseCol);
        _mpb.SetColor("_EmissionColor", emissCol);
        _renderer.SetPropertyBlock(_mpb);
    }
}
