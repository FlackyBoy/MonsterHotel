using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Unity.AI.Navigation;

/// <summary>
/// Composant joueur gérant le mode placement de chambre.
///
/// Contrôles (assigne les InputActionReference dans l'Inspector, ou laisse null pour les touches par défaut) :
///   Rotate  → Q / LB (tourne de 45°)
///   Confirm → F / A  (pose la chambre)
///   Cancel  → Échap / B (annule)
///
/// Usage : ShopCounter appelle StartPlacing(roomData) pour activer le mode placement.
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class RoomPlacer : MonoBehaviour
{
    [Header("Ghost")]
    [Tooltip("Matériau fantôme quand la pose est valide (vert semi-transparent)")]
    public Material ghostValidMaterial;
    [Tooltip("Matériau fantôme quand la pose est invalide (rouge semi-transparent)")]
    public Material ghostInvalidMaterial;

    [Header("Noms des actions dans la Player Action Map")]
    [Tooltip("Doit correspondre exactement au nom dans l'InputActionAsset")]
    public string rotateActionName  = "RotatePlacement";
    public string confirmActionName = "ConfirmPlacement";
    public string cancelActionName  = "CancelPlacement";

    [Header("Paramètres")]
    [Tooltip("Distance max devant le joueur où le ghost peut apparaître (en unités monde)")]
    public float placementOffset = 2f;

    [Header("Dithering murs")]
    public float ditherFadeSpeed    = 6f;
    public float ditherMaxAlpha     = 0.82f;
    public float ditherHeightOffset = 0.8f;

    // ─── Privé ────────────────────────────────────────────────────

    bool          _isPlacing;
    RoomData      _roomData;
    GameObject    _ghost;
    float         _rotationY;
    float         _visualRotY;       // rotation lissée pour l'affichage
    Vector2Int    _cursorCell;       // position courante du curseur (coin bas-gauche du footprint)
    Vector3       _cursorWorldPos;   // position monde continue du curseur (indépendante du joueur)
    bool          _currentValid;
    Vector3?      _exactSnapCenter;  // centre monde exact quand le snap magnétique est actif
    RoomInstance  _movingRoom;       // non-null quand on déplace une chambre existante
    Vector3[]    _furnitureSavedPos; // positions monde originales des meubles (restauration si annulation)
    Quaternion[] _furnitureSavedRot;
    Vector3[]    _furnitureLocalOff; // offsets locaux depuis le centre chambre (espace original)
    Quaternion[] _furnitureLocalRot;

    [Header("Lissage du ghost")]
    public float ghostMoveSmooth   = 15f;   // vitesse de lerp position
    public float ghostRotateSmooth = 20f;   // vitesse de lerp rotation

    PlayerInput        _playerInput;
    TopDownController  _controller;

    [Header("Curseur")]
    [Tooltip("Vitesse de déplacement monde du curseur de placement (unités/seconde)")]
    public float cursorMoveSpeed = 6f;

    // ─── Registre statique (utilisé par RoomSign pour la détection) ───

    public static readonly System.Collections.Generic.List<RoomPlacer> All = new();

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake()
    {
        All.Add(this);
        _playerInput = GetComponent<PlayerInput>();
        _controller  = GetComponent<TopDownController>();
        RoomInstance.OnAnyDestroyed += OnRoomDestroyed;
        var cfg = HotelConfig.Placement;
        if (cfg != null)
        {
            cursorMoveSpeed    = cfg.roomCursorSpeed;
            placementOffset    = cfg.roomPlacementOffset;
        }

        var ditherCfg = HotelConfig.Dither;
        if (ditherCfg != null)
        {
            ditherFadeSpeed    = ditherCfg.ditherFadeSpeed;
            ditherMaxAlpha     = ditherCfg.ditherMaxAlpha;
            ditherHeightOffset = ditherCfg.ditherHeightOffset;
        }
    }

    void Start()
    {
        // Rebake initial pour inclure les bâtiments placés en éditeur (cuisine, etc.)
        // Un seul joueur suffit — on prend le playerIndex 0
        if ((_playerInput?.playerIndex ?? 0) == 0)
            StartCoroutine(RebakeNavMeshAsync());
    }

    void Update()
    {
        if (!_isPlacing) return;

        UpdateGhostTransform();
        HandleInput();
    }

    void OnDestroy()
    {
        All.Remove(this);
        DestroyGhost();
        RoomInstance.OnAnyDestroyed -= OnRoomDestroyed;
    }

    void OnRoomDestroyed() => StartCoroutine(RebakeNavMeshAsync());

    // ─── API publique ─────────────────────────────────────────────

    public bool IsPlacing => _isPlacing;

    /// <summary>Position monde du curseur de placement, indépendante du joueur.</summary>
    public Vector3 CursorWorldPos => _cursorWorldPos;

    /// <summary>Démarre le mode placement pour la chambre donnée.</summary>
    public void StartPlacing(RoomData data)
    {
        if (_isPlacing) CancelPlacing();

        _roomData    = data;
        _rotationY   = 0f;
        _visualRotY  = 0f;
        _ghost       = CreateGhost(data.prefab);
        _isPlacing  = true;

        // Initialise le curseur devant le joueur
        _cursorWorldPos  = transform.position + transform.forward * placementOffset;
    }

    /// <summary>Démarre le mode déplacement pour une chambre existante. Pas de coût.</summary>
    public void StartMoving(RoomInstance room)
    {
        if (_isPlacing) CancelPlacing();

        _movingRoom  = room;
        _roomData    = room.Data;
        _rotationY   = room.transform.eulerAngles.y;
        _visualRotY  = _rotationY;
        _ghost       = CreateGhost(_roomData.prefab);
        _isPlacing   = true;

        // Initialise le curseur à la position actuelle de la chambre
        _cursorWorldPos = room.transform.position;

        // Masque la chambre source et désactive ses colliders pendant le déplacement
        foreach (var rend in room.GetComponentsInChildren<Renderer>())
            rend.enabled = false;
        foreach (var col in room.GetComponentsInChildren<Collider>())
            col.enabled = false;
        room.GetComponent<RoomSign>()?.HidePrompt();

        // Sauvegarde les positions/rotations des meubles et leurs offsets locaux
        var flist = room.PlacedFurniture;
        _furnitureSavedPos = new Vector3[flist.Count];
        _furnitureSavedRot = new Quaternion[flist.Count];
        _furnitureLocalOff = new Vector3[flist.Count];
        _furnitureLocalRot = new Quaternion[flist.Count];
        var invRot = Quaternion.Inverse(room.transform.rotation);
        for (int i = 0; i < flist.Count; i++)
        {
            if (flist[i] == null) continue;
            _furnitureSavedPos[i] = flist[i].transform.position;
            _furnitureSavedRot[i] = flist[i].transform.rotation;
            _furnitureLocalOff[i] = invRot * (flist[i].transform.position - room.transform.position);
            _furnitureLocalRot[i] = invRot * flist[i].transform.rotation;
        }
    }

    /// <summary>Annule le mode placement sans poser de chambre.</summary>
    public void CancelPlacing()
    {
        // Restaure la visibilité, les colliders et le panneau de la chambre si on était en mode déplacement
        if (_movingRoom != null)
        {
            // Restaure les positions des meubles si annulation
            if (_furnitureSavedPos != null)
            {
                var flist = _movingRoom.PlacedFurniture;
                for (int i = 0; i < flist.Count && i < _furnitureSavedPos.Length; i++)
                {
                    if (flist[i] == null) continue;
                    flist[i].transform.SetPositionAndRotation(_furnitureSavedPos[i], _furnitureSavedRot[i]);
                }
            }

            foreach (var rend in _movingRoom.GetComponentsInChildren<Renderer>())
                rend.enabled = true;
            foreach (var col in _movingRoom.GetComponentsInChildren<Collider>())
                col.enabled = true;
            _movingRoom.GetComponent<RoomSign>()?.RefreshPrompt();
        }

        _furnitureSavedPos = null;
        _furnitureSavedRot = null;
        _furnitureLocalOff = null;
        _furnitureLocalRot = null;
        DestroyGhost();
        _isPlacing   = false;
        _roomData    = null;
        _movingRoom  = null;
    }

    // ─── Ghost ────────────────────────────────────────────────────

    void UpdateGhostTransform()
    {
        var grid = GridManager.Instance;
        if (grid == null) return;

        var footprint = GetFootprint(_roomData.size, _rotationY);

        // ── Déplacement continu du curseur — aligné sur les axes caméra (XZ) ────
        if (_controller != null)
        {
            Vector2 inp = _controller.CursorInput;
            // RW1 : caméra propre à CE joueur (écran splitté statique) — Camera.main en repli
            // défensif seulement (résout vers une caméra quelconque, potentiellement le mauvais
            // joueur, mais évite un curseur figé si jamais PlayerCamera n'est pas encore assignée).
            var cam = _controller.PlayerCamera != null ? _controller.PlayerCamera : Camera.main;
            if (inp.sqrMagnitude > 0.001f && cam != null)
            {
                var right = Vector3.ProjectOnPlane(cam.transform.right,   Vector3.up).normalized;
                var fwd   = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                _cursorWorldPos += (right * inp.x + fwd * inp.y) * (cursorMoveSpeed * Time.deltaTime);
            }
        }

        // Convertit la position monde du curseur en cellule grille (sans clamp — le ghost peut aller hors grille)
        int rawX = Mathf.FloorToInt((_cursorWorldPos.x - grid.origin.x) / grid.cellSize);
        int rawY = Mathf.FloorToInt((_cursorWorldPos.z - grid.origin.z) / grid.cellSize);
        int ox = rawX - footprint.x / 2;
        int oy = rawY - footprint.y / 2;

        // Snap magnétique : déclenche dès qu'une room est dans le voisinage (chevauchement OU adjacence),
        // pas seulement en cas de chevauchement, sinon le grand offset empêche le déclenchement.
        _exactSnapCenter = null;
        {
            var snapped = FindAdjacentSnap(ox, oy, footprint, grid);
            if (snapped.HasValue) { ox = snapped.Value.x; oy = snapped.Value.y; }
        }

        _cursorCell   = new Vector2Int(ox, oy);
        _currentValid = CanPlace(ox, oy, footprint);

        // Position monde du ghost : exact snap center si disponible, sinon centre du footprint
        Vector3 center;
        if (_exactSnapCenter.HasValue)
        {
            center = _exactSnapCenter.Value;
        }
        else
        {
            Vector3 worldOrigin = grid.CellToWorld(ox, oy);
            float halfW = (footprint.x * grid.cellSize) * 0.5f - grid.cellSize * 0.5f;
            float halfH = (footprint.y * grid.cellSize) * 0.5f - grid.cellSize * 0.5f;
            center = worldOrigin + new Vector3(halfW, 0f, halfH);
        }

        // Lissage position et rotation (purement visuel — la logique grille utilise center/_rotationY)
        float t = 1f - Mathf.Exp(-ghostMoveSmooth * Time.deltaTime);
        _ghost.transform.position = Vector3.Lerp(_ghost.transform.position, center, t);
        _visualRotY = Mathf.LerpAngle(_visualRotY, _rotationY, 1f - Mathf.Exp(-ghostRotateSmooth * Time.deltaTime));
        _ghost.transform.rotation = Quaternion.Euler(0f, _visualRotY, 0f);
        // Scale visuel = taille ORIGINALE (pas le footprint) pour éviter le rescale visuel à la rotation.
        // Le footprint (bounding box) est uniquement utilisé pour les cellules grille.
        _ghost.transform.localScale = new Vector3(
            _roomData.size.x * grid.cellSize,
            _roomData.height,
            _roomData.size.y * grid.cellSize
        );

        SetGhostMaterial(_currentValid ? ghostValidMaterial : ghostInvalidMaterial);

        // Fait suivre les meubles au ghost en temps réel
        if (_movingRoom != null && _furnitureLocalOff != null)
        {
            var ghostPos = _ghost.transform.position;
            var ghostRot = Quaternion.Euler(0f, _visualRotY, 0f);
            var flist    = _movingRoom.PlacedFurniture;
            for (int i = 0; i < flist.Count && i < _furnitureLocalOff.Length; i++)
            {
                if (flist[i] == null) continue;
                flist[i].transform.SetPositionAndRotation(
                    ghostPos + ghostRot * _furnitureLocalOff[i],
                    ghostRot * _furnitureLocalRot[i]);
            }
        }
    }

    GameObject CreateGhost(GameObject prefab)
    {
        var ghost = Instantiate(prefab);
        ghost.name = "Ghost_Room";

        // Construit la géométrie du ghost (Start() ne sera pas appelé car les scripts sont désactivés après)
        ghost.GetComponent<RoomPlaceholder>()?.Build();

        // Désactive les colliders pour ne pas bloquer le joueur
        foreach (var col in ghost.GetComponentsInChildren<Collider>())
            col.enabled = false;

        // Désactive les scripts actifs sur le ghost
        foreach (var mb in ghost.GetComponentsInChildren<MonoBehaviour>())
            mb.enabled = false;

        ApplyMaterialToGhost(ghost, ghostValidMaterial);
        return ghost;
    }

    void SetGhostMaterial(Material mat)
    {
        if (mat == null || _ghost == null) return;
        ApplyMaterialToGhost(_ghost, mat);
    }

    void ApplyMaterialToGhost(GameObject ghost, Material mat)
    {
        if (mat == null) return;
        foreach (var rend in ghost.GetComponentsInChildren<Renderer>())
        {
            var mats = new Material[rend.materials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            rend.materials = mats;
        }
    }

    void DestroyGhost()
    {
        if (_ghost != null)
        {
            Destroy(_ghost);
            _ghost = null;
        }
    }

    // ─── Input ────────────────────────────────────────────────────

    void HandleInput()
    {
        int pIdx = _playerInput?.playerIndex ?? 0;
        if (_playerInput.WasPressed(rotateActionName))  _rotationY = (_rotationY + 45f) % 360f;
        if (_playerInput.WasPressed(confirmActionName) && !InputConsumer.IsConsumed(pIdx)) TryPlace();
        if (_playerInput.WasPressed(cancelActionName))  CancelPlacing();
    }

    // ─── Placement ────────────────────────────────────────────────

    void TryPlace()
    {
        if (!_currentValid) return;

        var grid      = GridManager.Instance;
        var footprint = GetFootprint(_roomData.size, _rotationY);
        int ox        = _cursorCell.x;
        int oy        = _cursorCell.y;

        // Double-check (le ghost peut avoir bougé ce frame)
        if (!CanPlace(ox, oy, footprint)) return;

        // Centre monde : exact snap si dispo, sinon grille
        Vector3 logicCenter;
        if (_exactSnapCenter.HasValue)
        {
            logicCenter = _exactSnapCenter.Value;
        }
        else
        {
            Vector3 worldOrigin = grid.CellToWorld(ox, oy);
            float halfW = (footprint.x * grid.cellSize) * 0.5f - grid.cellSize * 0.5f;
            float halfH = (footprint.y * grid.cellSize) * 0.5f - grid.cellSize * 0.5f;
            logicCenter = worldOrigin + new Vector3(halfW, 0f, halfH);
        }

        // ── Mode déplacement ──────────────────────────────────────
        if (_movingRoom != null)
        {
            var newRot = Quaternion.Euler(0f, _rotationY, 0f);

            // Libère les anciennes cellules
            grid.FreeRect(_movingRoom.OriginCell.x, _movingRoom.OriginCell.y,
                          _movingRoom.FootprintSize.x, _movingRoom.FootprintSize.y);

            // Repositionne la chambre existante
            _movingRoom.transform.SetPositionAndRotation(logicCenter, newRot);

            // Snappe les meubles à la position logique exacte (ils suivaient le ghost visuellement)
            if (_furnitureLocalOff != null)
            {
                var flist = _movingRoom.PlacedFurniture;
                for (int i = 0; i < flist.Count && i < _furnitureLocalOff.Length; i++)
                {
                    if (flist[i] == null) continue;
                    flist[i].transform.SetPositionAndRotation(
                        logicCenter + newRot * _furnitureLocalOff[i],
                        newRot * _furnitureLocalRot[i]);
                }
            }

            // Occupe les nouvelles cellules
            grid.SetRect(ox, oy, footprint.x, footprint.y, CellType.Room);
            for (int x = ox; x < ox + footprint.x; x++)
                for (int y = oy; y < oy + footprint.y; y++)
                    grid.PlaceOccupant(x, y, _movingRoom.gameObject);

            _movingRoom.InitCells(new Vector2Int(ox, oy), footprint);

            // Empêche CancelPlacing de restaurer les meubles à leurs positions d'avant
            _furnitureSavedPos = null;
            _furnitureSavedRot = null;
            FeedbackManager.Instance?.roomPlaced?.PlayFeedbacks(logicCenter);
            FeedbackManager.Instance?.ShakeCamera();
            CancelPlacing();
            return;
        }

        // ── Mode nouvelle chambre ─────────────────────────────────
        // Débite le coût
        if (!EconomyManager.Instance.TrySpend(_roomData.cost)) return;

        // Instancie la chambre définitive (placementOffset décale sans affecter la grille)
        var room = Instantiate(
            _roomData.prefab,
            logicCenter,
            Quaternion.Euler(0f, _rotationY, 0f)
        );
        room.name = _roomData.roomName;

        // Initialise le composant RoomInstance (ajouté automatiquement si absent du prefab)
        var instance = room.GetComponent<RoomInstance>() ?? room.AddComponent<RoomInstance>();
        instance.Init(_roomData);
        instance.InitCells(new Vector2Int(ox, oy), footprint);

        // Ajoute la pancarte d'interaction et le dithering
        room.AddComponent<RoomSign>();
        var dither = room.AddComponent<RoomDither>();
        dither.fadeSpeed          = ditherFadeSpeed;
        dither.maxDitherAlpha     = ditherMaxAlpha;
        dither.playerHeightOffset = ditherHeightOffset;

        // Scale selon la taille définie dans RoomData
        room.transform.localScale = new Vector3(
            _roomData.size.x * grid.cellSize,
            _roomData.height,
            _roomData.size.y * grid.cellSize
        );

        // Construit la géométrie puis active les colliders
        var placeholder = room.GetComponent<RoomPlaceholder>();
        if (placeholder != null) { placeholder.Build(); placeholder.EnableColliders(); placeholder.EnableNavMeshObstacles(); }

        // Marque les cellules occupées
        for (int x = ox; x < ox + footprint.x; x++)
            for (int y = oy; y < oy + footprint.y; y++)
                grid.PlaceOccupant(x, y, room);

        grid.SetRect(ox, oy, footprint.x, footprint.y, CellType.Room);

        FeedbackManager.Instance?.roomPlaced?.PlayFeedbacks(logicCenter);
        FeedbackManager.Instance?.ShakeCamera();

        // Rebake le NavMesh global de façon asynchrone (une seule surface = pas de gap)
        StartCoroutine(RebakeNavMeshAsync());

        CancelPlacing();
    }

    System.Collections.IEnumerator RebakeNavMeshAsync()
    {
        var surface = Object.FindFirstObjectByType<NavMeshSurface>();
        if (surface == null) yield break;
        // UpdateNavMesh() (incrémental, async) — testé avec BuildNavMesh() (reconstruction
        // complète) pour éviter les zones mal connectées, mais ça reconstruit TOUT le NavMesh de
        // l'hôtel à chaque pose de pièce (coût qui grossit avec la taille de l'hôtel) → gros
        // ralentissement en jeu, inacceptable. Retour à l'incrémental : le filet PathPartial/
        // PathInvalid de MonsterMover (G6-B20) rend maintenant les zones mal connectées
        // diagnosticables via un warning console plutôt que de traverser les murs en silence — un
        // Bake manuel complet reste le recours ponctuel si ce warning apparaît.
        var op = surface.UpdateNavMesh(surface.navMeshData);
        yield return op;
    }

    /// <summary>
    /// Snap magnétique bord-visuel contre bord-visuel.
    /// Teste 8 candidats : 4 faces (droite/gauche/haut/bas) + 4 coins (diagonales),
    /// ce qui permet le snap latéral ET aux angles des rooms à 45°.
    /// </summary>
    Vector2Int? FindAdjacentSnap(int ox, int oy, Vector2Int footprint, GridManager grid)
    {
        // Détecte les rooms dans le footprint + 1 cellule de marge
        const int snapMargin = 1;
        var blockingRooms = new System.Collections.Generic.HashSet<GameObject>();
        for (int x = ox - snapMargin; x < ox + footprint.x + snapMargin; x++)
            for (int y = oy - snapMargin; y < oy + footprint.y + snapMargin; y++)
            {
                var cell = grid.GetCell(x, y);
                if (cell != null && cell.IsOccupied && cell.occupant != null)
                    blockingRooms.Add(cell.occupant);
            }

        if (_movingRoom != null) blockingRooms.Remove(_movingRoom.gameObject);
        if (blockingRooms.Count == 0) return null;

        // Demi-extents visuels du ghost
        float ghostRad = _rotationY * Mathf.Deg2Rad;
        float gHx = (_roomData.size.x * Mathf.Abs(Mathf.Cos(ghostRad)) + _roomData.size.y * Mathf.Abs(Mathf.Sin(ghostRad))) * grid.cellSize * 0.5f;
        float gHz = (_roomData.size.x * Mathf.Abs(Mathf.Sin(ghostRad)) + _roomData.size.y * Mathf.Abs(Mathf.Cos(ghostRad))) * grid.cellSize * 0.5f;

        float ghostCx = grid.origin.x + (ox + footprint.x * 0.5f) * grid.cellSize;
        float ghostCz = grid.origin.z + (oy + footprint.y * 0.5f) * grid.cellSize;

        Vector2Int? best = null;
        float bestDist = float.MaxValue;

        // Calcule 8 candidats (4 faces + 4 coins) par room individuelle
        // → évite les mauvais snap causés par l'union AABB de plusieurs rooms
        foreach (var room in blockingRooms)
        {
            float rad = room.transform.eulerAngles.y * Mathf.Deg2Rad;
            Vector3 sc = room.transform.localScale;
            float hx = (sc.x * Mathf.Abs(Mathf.Cos(rad)) + sc.z * Mathf.Abs(Mathf.Sin(rad))) * 0.5f;
            float hz = (sc.x * Mathf.Abs(Mathf.Sin(rad)) + sc.z * Mathf.Abs(Mathf.Cos(rad))) * 0.5f;
            Vector3 p = room.transform.position;
            float rMinX = p.x - hx, rMaxX = p.x + hx;
            float rMinZ = p.z - hz, rMaxZ = p.z + hz;

            float[] cx = { rMaxX + gHx, rMinX - gHx, ghostCx,      ghostCx,      rMaxX + gHx, rMinX - gHx, rMaxX + gHx, rMinX - gHx };
            float[] cz = { ghostCz,      ghostCz,      rMaxZ + gHz,  rMinZ - gHz,  rMaxZ + gHz, rMaxZ + gHz, rMinZ - gHz, rMinZ - gHz };

            for (int i = 0; i < 8; i++)
            {
                int sox = Mathf.RoundToInt((cx[i] - grid.origin.x) / grid.cellSize - footprint.x * 0.5f);
                int soy = Mathf.RoundToInt((cz[i] - grid.origin.z) / grid.cellSize - footprint.y * 0.5f);
                if (!CanPlace(sox, soy, footprint)) continue;
                float dx = cx[i] - ghostCx, dz = cz[i] - ghostCz;
                float dist = dx * dx + dz * dz;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = new Vector2Int(sox, soy);
                    _exactSnapCenter = new Vector3(cx[i], 0f, cz[i]);
                }
            }
        }

        return best;
    }

    /// <summary>Vérifie que toutes les cellules du footprint sont libres et dans les bornes.
    /// En mode déplacement, les cellules de la chambre source sont considérées libres.</summary>
    bool CanPlace(int ox, int oy, Vector2Int footprint)
    {
        var grid = GridManager.Instance;
        if (grid == null) return false;

        for (int x = ox; x < ox + footprint.x; x++)
        {
            for (int y = oy; y < oy + footprint.y; y++)
            {
                // Hors grille initiale : bloqué seulement si un ExpansionBlock intact est là
                if (!grid.IsInBounds(x, y))
                {
                    if (grid.IsBlockedByExpansion(x, y)) return false;
                    continue;
                }

                var cell = grid.GetCell(x, y);
                if (!cell.IsOccupied) continue;

                if (_movingRoom != null && cell.occupant == _movingRoom.gameObject) continue;

                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Calcule la bounding box en cellules du prefab tourné.
    /// À 0°/180° → (w, h). À 90°/270° → (h, w). À 45° etc. → boite englobante arrondie.
    /// </summary>
    Vector2Int GetFootprint(Vector2Int size, float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        float cos = Mathf.Abs(Mathf.Cos(rad));
        float sin = Mathf.Abs(Mathf.Sin(rad));
        int bw = Mathf.CeilToInt(size.x * cos + size.y * sin);
        int bh = Mathf.CeilToInt(size.x * sin + size.y * cos);
        return new Vector2Int(Mathf.Max(1, bw), Mathf.Max(1, bh));
    }

}
