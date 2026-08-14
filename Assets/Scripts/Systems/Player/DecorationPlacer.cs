using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gère le mode placement de décorations dans le hall de l'hôtel (hors chambres).
///
/// Contrôles (mêmes que FurniturePlacer) :
///   RotatePlacement  → tourne de 45°
///   ConfirmPlacement → pose la décoration
///   CancelPlacement  → annule
///
/// Usage : RoomShopPanel appelle StartPlacing(decorationData) pour activer le mode.
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class DecorationPlacer : MonoBehaviour
{
    [Header("Ghost")]
    [Tooltip("Matériau fantôme quand la pose est valide (vert semi-transparent)")]
    public Material ghostValidMaterial;
    [Tooltip("Matériau fantôme quand la pose est invalide (rouge semi-transparent)")]
    public Material ghostInvalidMaterial;

    [Header("Actions")]
    public string rotateActionName  = "RotatePlacement";
    public string confirmActionName = "ConfirmPlacement";
    public string cancelActionName  = "CancelPlacement";
    public string interactActionName = "Interact";

    [Header("Lissage")]
    public float ghostMoveSmooth   = 15f;
    public float ghostRotateSmooth = 20f;
    public float cursorMoveSpeed   = 4f;

    [Header("Raycast sol")]
    [Tooltip("Layers considérés comme sol. Exclure le layer Room si les colliders de chambre gênent.")]
    public LayerMask floorLayerMask = ~0;

    [Header("Déplacement")]
    [Tooltip("Distance pour reprendre une décoration posée")]
    public float pickupRange = 1.5f;

    // ─── Privé ────────────────────────────────────────────────────

    bool               _isPlacing;
    DecorationData     _data;
    GameObject         _ghost;
    Vector3            _cursorPos;
    Vector3            _ghostWorldPos;
    bool               _currentValid;
    float              _rotationY;
    float              _visualRotY;
    int                _placingFrameCount;
    DecorationInstance _movingDeco; // non-null en mode déplacement d'une déco existante

    PlayerInput       _playerInput;
    TopDownController _controller;

    public bool IsPlacing => _isPlacing;

    /// <summary>Position monde du curseur de placement, indépendante du joueur.</summary>
    public Vector3 CursorWorldPos => _cursorPos;

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
        _controller  = GetComponent<TopDownController>();
        var cfg = HotelConfig.Placement;
        if (cfg != null) cursorMoveSpeed = cfg.furnitureCursorSpeed;
    }

    void Update()
    {
        if (_isPlacing)
        {
            _placingFrameCount++;
            UpdateGhost();
            HandleInput();
        }
        else
        {
            // Hors placement : Interact pour reprendre une déco proche
            if (_playerInput.WasPressed(interactActionName))
                TryPickupNearest();
        }
    }

    void OnDestroy() => DestroyGhost();

    // ─── API publique ─────────────────────────────────────────────

    public void StartPlacing(DecorationData data)
    {
        if (_isPlacing) CancelPlacing();

        _data              = data;
        _rotationY         = 0f;
        _visualRotY        = 0f;
        _cursorPos         = transform.position;
        _ghostWorldPos     = _cursorPos;
        _ghost             = CreateGhost(data?.prefab);
        _currentValid      = false;
        _isPlacing         = true;
        _placingFrameCount = 0;
    }

    public void CancelPlacing()
    {
        // Restaure la déco si on annule un déplacement
        if (_movingDeco != null)
        {
            foreach (var r in _movingDeco.GetComponentsInChildren<Renderer>())
                r.enabled = true;
            _movingDeco = null;
        }
        DestroyGhost();
        _isPlacing = false;
        _data      = null;
    }

    /// <summary>Reprend une décoration déjà posée pour la repositionner (gratuit).</summary>
    public void StartMoving(DecorationInstance di)
    {
        if (di == null) return;
        if (_isPlacing) CancelPlacing();

        _movingDeco    = di;
        _data          = di.Data;
        _rotationY     = di.transform.eulerAngles.y;
        _visualRotY    = _rotationY;
        _cursorPos     = di.transform.position;
        _ghostWorldPos = di.transform.position;
        _ghost         = CreateGhost(_data?.prefab);
        if (_ghost != null)
            _ghost.transform.SetPositionAndRotation(_ghostWorldPos, di.transform.rotation);

        // Masque la déco source pendant le déplacement
        foreach (var r in di.GetComponentsInChildren<Renderer>())
            r.enabled = false;

        _currentValid      = true;
        _isPlacing         = true;
        _placingFrameCount = 0;
    }

    void TryPickupNearest()
    {
        DecorationInstance nearest = null;
        float bestDist = pickupRange;
        // Cherche la déco la plus proche du joueur
        foreach (var di in Object.FindObjectsByType<DecorationInstance>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(transform.position, di.transform.position);
            if (d < bestDist) { bestDist = d; nearest = di; }
        }
        if (nearest != null) StartMoving(nearest);
    }

    // ─── Ghost ────────────────────────────────────────────────────

    void UpdateGhost()
    {
        if (_ghost == null) return;

        // Déplace le curseur via l'input du controller
        if (_controller != null)
        {
            Vector2 inp = _controller.CursorInput;
            // RW1 : caméra propre à CE joueur — Camera.main en repli défensif seulement.
            var cam = _controller.PlayerCamera != null ? _controller.PlayerCamera : Camera.main;
            if (inp.sqrMagnitude > 0.001f && cam != null)
            {
                var right = Vector3.ProjectOnPlane(cam.transform.right,   Vector3.up).normalized;
                var fwd   = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                _cursorPos += (right * inp.x + fwd * inp.y) * (cursorMoveSpeed * Time.deltaTime);
            }
        }

        // Pose le ghost sur le sol
        float floorY = GetFloorY(_cursorPos);
        float halfH  = GetGhostHalfHeight();
        _ghostWorldPos = new Vector3(_cursorPos.x, floorY + halfH, _cursorPos.z);

        _currentValid = IsValidPosition(_cursorPos);

        // Lissage visuel — Y fixé au sol, pas interpolé
        float t = 1f - Mathf.Exp(-ghostMoveSmooth * Time.deltaTime);
        Vector3 lerped = Vector3.Lerp(_ghost.transform.position, _ghostWorldPos, t);
        lerped.y = _ghostWorldPos.y;
        _ghost.transform.position = lerped;

        _visualRotY = Mathf.LerpAngle(_visualRotY, _rotationY,
            1f - Mathf.Exp(-ghostRotateSmooth * Time.deltaTime));
        _ghost.transform.rotation = Quaternion.Euler(0f, _visualRotY, 0f);

        SetGhostMaterial(_currentValid ? ghostValidMaterial : ghostInvalidMaterial);
    }

    // ─── Input ────────────────────────────────────────────────────

    void HandleInput()
    {
        if (_playerInput.WasPressed(rotateActionName))
            _rotationY = (_rotationY + 45f) % 360f;

        bool confirm = _playerInput.WasPressed(confirmActionName)
                    || (_placingFrameCount > 1 && _playerInput.WasPressed(interactActionName));
        if (confirm) TryPlace();

        if (_playerInput.WasPressed(cancelActionName)) CancelPlacing();
    }

    // ─── Placement ────────────────────────────────────────────────

    void TryPlace()
    {
        if (_data == null || !_currentValid) return;

        var rot = Quaternion.Euler(0f, _rotationY, 0f);

        // ── Mode déplacement : repositionne la déco existante ─────────────
        if (_movingDeco != null)
        {
            // Libère l'ancienne cellule de grille
            UnregisterFromGrid(_movingDeco.gameObject);

            _movingDeco.transform.SetPositionAndRotation(_ghostWorldPos, rot);
            foreach (var r in _movingDeco.GetComponentsInChildren<Renderer>())
                r.enabled = true;

            RegisterInGrid(_movingDeco.gameObject, _ghostWorldPos);
            _movingDeco = null;
            CancelPlacing();
            return;
        }

        // ── Mode placement neuf ───────────────────────────────────────────
        if (EconomyManager.Instance == null || !EconomyManager.Instance.TrySpend(_data.cost)) return;

        var container = new GameObject(_data.decorationName);
        container.transform.SetPositionAndRotation(_ghostWorldPos, rot);

        if (_data.prefab != null)
        {
            var visual = Instantiate(_data.prefab, container.transform);
            visual.transform.localPosition = _data.placementOffset;
            visual.transform.localRotation = Quaternion.identity;
            foreach (var col in visual.GetComponentsInChildren<Collider>())
                col.enabled = false;
        }
        else
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(container.transform, false);
            cube.transform.localScale = Vector3.one * 0.5f;
        }

        var di = container.AddComponent<DecorationInstance>();
        di.Init(_data);

        // Enregistre dans la grille pour bloquer le placement de chambres
        RegisterInGrid(container, _ghostWorldPos);

        FeedbackManager.Instance?.furniturePlaced?.PlayFeedbacks(_ghostWorldPos);

        if (_data.multiPlace)
        {
            DestroyGhost();
            _ghost             = CreateGhost(_data.prefab);
            _placingFrameCount = 0;
        }
        else
        {
            CancelPlacing();
        }
    }

    static void RegisterInGrid(GameObject go, Vector3 worldPos)
    {
        var grid = GridManager.Instance;
        if (grid == null) return;
        var cell = grid.WorldToCell(worldPos);
        if (cell != null) grid.PlaceOccupant(cell.coords.x, cell.coords.y, go);
    }

    static void UnregisterFromGrid(GameObject go)
    {
        var grid = GridManager.Instance;
        if (grid == null) return;
        // Cherche la cellule qui a cet occupant et la libère
        var cell = grid.WorldToCell(go.transform.position);
        if (cell != null && cell.occupant == go)
            grid.ClearOccupant(cell.coords.x, cell.coords.y);
    }

    // ─── Validation ───────────────────────────────────────────────

    bool IsValidPosition(Vector3 worldPos)
    {
        // Doit y avoir un sol solide en dessous
        bool hasFloor = Physics.Raycast(
            new Vector3(worldPos.x, worldPos.y + 5f, worldPos.z),
            Vector3.down, 10f, floorLayerMask, QueryTriggerInteraction.Ignore);
        if (!hasFloor) return false;

        // Pas dans une chambre, pas sur un bloc intact
        var grid = GridManager.Instance;
        if (grid != null)
        {
            var cell = grid.WorldToCell(worldPos);
            if (cell != null && cell.type == CellType.Room)      return false;
            if (cell != null && cell.IsOccupied)                  return false;
        }

        return true;
    }

    float GetFloorY(Vector3 pos)
    {
        if (Physics.Raycast(new Vector3(pos.x, pos.y + 10f, pos.z), Vector3.down,
                            out var hit, 20f, floorLayerMask, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return 0f;
    }

    float GetGhostHalfHeight()
    {
        if (_ghost == null) return 0f;
        Bounds? wb = null;
        foreach (var r in _ghost.GetComponentsInChildren<Renderer>())
        {
            if (r.bounds.size.sqrMagnitude < 0.0001f) continue;
            if (!wb.HasValue) wb = r.bounds;
            else { var b = wb.Value; b.Encapsulate(r.bounds); wb = b; }
        }
        return wb.HasValue ? wb.Value.extents.y : 0.25f;
    }

    // ─── Ghost helpers ────────────────────────────────────────────

    GameObject CreateGhost(GameObject prefab)
    {
        GameObject ghost;
        if (prefab != null)
            ghost = Instantiate(prefab);
        else
        {
            ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ghost.transform.localScale = Vector3.one * 0.5f;
        }
        ghost.name = "Ghost_Decoration";
        foreach (var col in ghost.GetComponentsInChildren<Collider>())
            col.enabled = false;
        foreach (var mb in ghost.GetComponentsInChildren<MonoBehaviour>())
            mb.enabled = false;
        if (ghostValidMaterial != null) ApplyMat(ghost, ghostValidMaterial);
        return ghost;
    }

    void SetGhostMaterial(Material mat)
    {
        if (mat != null && _ghost != null) ApplyMat(_ghost, mat);
    }

    void ApplyMat(GameObject go, Material mat)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var mats = new Material[r.materials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.materials = mats;
        }
    }

    void DestroyGhost()
    {
        if (_ghost != null) { Destroy(_ghost); _ghost = null; }
    }
}
