using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Composant joueur gérant le mode placement de meuble dans une chambre.
///
/// Contrôles — ajoute ces actions dans la Player Action Map de l'InputActionAsset :
///   RotatePlacement  → Q / LB (tourne de 45°)
///   ConfirmPlacement → F / A  (pose le meuble)
///   CancelPlacement  → Échap / B (annule)
/// Si les actions n'existent pas dans l'asset, les touches par défaut ci-dessus s'appliquent.
///
/// Usage : RoomManagementPanel appelle StartPlacing(furnitureData, room) pour activer le mode.
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class FurniturePlacer : MonoBehaviour
{
    [Header("Ghost")]
    [Tooltip("Matériau fantôme quand la pose est valide (vert semi-transparent)")]
    public Material ghostValidMaterial;
    [Tooltip("Matériau fantôme quand la pose est invalide (rouge semi-transparent)")]
    public Material ghostInvalidMaterial;

    [Header("Noms des actions dans la Player Action Map")]
    [Tooltip("Action de rotation du meuble (ajouter dans l'InputActionAsset → Player)")]
    public string rotateActionName  = "RotatePlacement";
    [Tooltip("Action de confirmation / pose")]
    public string confirmActionName = "ConfirmPlacement";
    [Tooltip("Action d'annulation")]
    public string cancelActionName  = "CancelPlacement";
    [Tooltip("Action secondaire de confirmation (Interact) — utilisée si ConfirmPlacement n'est pas lié")]
    public string interactActionName = "Interact";

    [Header("Lissage du ghost")]
    public float ghostMoveSmooth   = 15f;
    public float ghostRotateSmooth = 20f;

    [Header("Curseur (déplacement indépendant du joueur)")]
    [Tooltip("Vitesse de déplacement du curseur de placement")]
    public float cursorMoveSpeed = 4f;

    [Header("Snapping chaise")]
    [Tooltip("Distance max pour snapper sur un ChairSlot")]
    public float chairSlotSnapRange = 1.5f;

    [Header("Marge murs")]
    [Tooltip("Marge supplémentaire (en fraction de la taille de la pièce) pour éviter que les meubles passent dans les murs. Augmente si les murs sont épais (ex: 0.1 = 10%).")]
    public float wallMargin = 0.1f;

    // ─── Privé ────────────────────────────────────────────────────

    bool              _isPlacing;
    FurnitureData     _furnitureData;
    RoomInstance      _targetRoom;
    GameObject        _ghost;
    float             _rotationY;
    float             _visualRotY;
    bool              _currentValid;
    Vector3           _ghostWorldPos;
    Vector3           _cursorPos;         // position monde du curseur (indépendant du joueur)
    ChairSlot         _snapSlot;          // slot le plus proche quand requiresChairSlot
    FurnitureInstance _movingFurniture;  // non-null en mode déplacement d'un meuble existant
    bool              _upgradeAfterMove; // true = ghost upgrade, upgrade auto à la confirmation
    int               _placingFrameCount; // nombre de frames depuis le début du placement (0 = frame de démarrage)

    /// <summary>
    /// Quand true, le panneau contrôle le placement (preview).
    /// FurniturePlacer affiche le ghost mais ignore les inputs confirm/cancel.
    /// </summary>
    public bool PanelIsControlling { get; set; }

    /// <summary>Appelé par le panneau quand il confirme le meuble — transfère le contrôle au placer.</summary>
    public void ConfirmPreview()
    {
        PanelIsControlling = false;
        _placingFrameCount = -5; // force plusieurs frames d'attente avant de pouvoir confirmer
    }

    PlayerInput          _playerInput;
    FurniturePicker      _picker;
    TopDownController    _controller;

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
        _picker      = GetComponent<FurniturePicker>();
        _controller  = GetComponent<TopDownController>();
        var cfg = HotelConfig.Instance;
        if (cfg != null) { cursorMoveSpeed = cfg.furnitureCursorSpeed; wallMargin = cfg.furnitureWallMargin; chairSlotSnapRange = cfg.chairSnapRange; }
    }

    void Update()
    {
        if (!_isPlacing) return;

        // Auto-cancel si le joueur s'éloigne trop de la chambre
        if (_targetRoom != null)
        {
            var sc   = _targetRoom.transform.lossyScale;
            float roomExtent = Mathf.Max(sc.x, sc.z) * 0.5f;
            float dist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(_targetRoom.transform.position.x, _targetRoom.transform.position.z));
            if (dist > roomExtent + 3f)
            {
                CancelPlacing();
                return;
            }
        }

        _placingFrameCount++;
        UpdateGhostTransform();
        HandleInput();
    }

    void OnDestroy() => DestroyGhost();

    // ─── API publique ─────────────────────────────────────────────

    public bool IsPlacing           => _isPlacing;
    public RoomInstance TargetRoom  => _targetRoom;
    public Vector3 GhostWorldPos    => _ghostWorldPos;

    System.Action _onDone; // appelé quand le placement se termine

    public void SetOnDoneCallback(System.Action callback) => _onDone = callback;

    /// <summary>Démarre le mode placement du meuble donné dans la chambre cible.</summary>
    public void StartPlacing(FurnitureData data, RoomInstance room)
    {
        if (_isPlacing) CancelPlacing();

        _furnitureData = data;
        _targetRoom    = room;
        _rotationY     = 0f;
        _visualRotY    = 0f;
        _ghost         = CreateGhost(data?.prefab);

        // Positionne le ghost immédiatement à l'intérieur de la chambre avec une marge
        // conservative (0.1), car les bounds du renderer ne sont pas encore initialisées
        // sur le premier frame (avant le premier rendu).
        _cursorPos = room.transform.position; // démarre au centre de la pièce
        _ghostWorldPos = _cursorPos;
        if (_ghost != null) _ghost.transform.position = _ghostWorldPos;

        _currentValid      = true;
        _isPlacing         = true;
        _placingFrameCount = 0;
    }

    /// <summary>Démarre le mode déplacement d'un meuble déjà posé (sans coût).</summary>
    public void StartMoving(FurnitureInstance fi)
    {
        if (fi == null || fi.Room == null) return;
        if (_isPlacing) CancelPlacing();

        _movingFurniture = fi;
        _furnitureData   = fi.Data;
        _targetRoom      = fi.Room;
        _rotationY       = fi.transform.eulerAngles.y;
        _visualRotY      = _rotationY;
        _ghost           = CreateGhost(_furnitureData?.prefab);

        // Démarre à la position actuelle du meuble
        _cursorPos     = fi.transform.position;
        _ghostWorldPos = fi.transform.position;
        if (_ghost != null)
        {
            _ghost.transform.SetPositionAndRotation(_ghostWorldPos, fi.transform.rotation);
        }

        // Masque le meuble source pendant le déplacement
        foreach (var rend in fi.GetComponentsInChildren<Renderer>())
            rend.enabled = false;

        _currentValid      = true;
        _isPlacing         = true;
        _placingFrameCount = 0;
    }

    /// <summary>
    /// Démarre le mode déplacement avec le ghost de l'upgrade suivant.
    /// À la confirmation, repositionne ET upgrade automatiquement.
    /// </summary>
    public void StartMovingForUpgrade(FurnitureInstance fi)
    {
        if (fi == null || !fi.CanUpgrade || fi.Room == null) return;
        StartMoving(fi);

        // Remplace le ghost par celui de l'upgrade
        DestroyGhost();
        _ghost = CreateGhost(fi.Data.nextUpgrade.prefab);
        _ghost.transform.SetPositionAndRotation(_ghostWorldPos, Quaternion.Euler(0f, _rotationY, 0f));
        _upgradeAfterMove = true;
    }

    /// <summary>Annule le mode placement sans poser de meuble.</summary>
    public void CancelPlacing()
    {
        PanelIsControlling = false;
        var cb = _onDone;
        _onDone = null;
        // Restaure la visibilité du meuble déplacé si annulation
        if (_movingFurniture != null)
        {
            foreach (var rend in _movingFurniture.GetComponentsInChildren<Renderer>())
                rend.enabled = true;
            _movingFurniture = null;
        }

        _upgradeAfterMove = false;
        DestroyGhost();
        _isPlacing     = false;
        _furnitureData = null;
        _targetRoom    = null;
        cb?.Invoke();
    }

    // ─── Ghost ────────────────────────────────────────────────────

    void UpdateGhostTransform()
    {
        if (_ghost == null || _targetRoom == null) return;

        // ── Mode chaise : déplacement normal + snap sur ChairSlot si proche ──
        if (_furnitureData != null && _furnitureData.requiresChairSlot)
        {
            // Même mouvement curseur que les autres meubles
            if (!PanelIsControlling && _controller != null)
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

            // Y : aligne sur le sol comme les autres meubles
            ComputeClampData(_ghost, _targetRoom, out Vector3 ptc, out _, out _, out float extY);
            var ghostPos  = _cursorPos;
            ghostPos.y    = _targetRoom.transform.position.y + extY - ptc.y;

            // Snap si un slot libre est à portée du curseur
            _snapSlot     = FindNearestFreeChairSlot();
            _currentValid = _snapSlot != null;

            if (_snapSlot != null)
            {
                _ghost.SetActive(true);
                _ghost.transform.SetPositionAndRotation(
                    _snapSlot.transform.position,
                    _snapSlot.transform.rotation * Quaternion.Euler(0f, _rotationY, 0f));
                SetGhostMaterial(ghostValidMaterial);
            }
            else
            {
                _ghost.SetActive(true);
                _ghost.transform.position = ghostPos;
                SetGhostMaterial(ghostInvalidMaterial);
            }
            return;
        }

        // Déplace le curseur avec l'input du controller — uniquement hors preview (panneau fermé)
        if (!PanelIsControlling && _controller != null)
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

        Vector3 desiredPivot = _cursorPos;

        // Calcule les données de clamping basées sur le CENTRE DU MESH (pas le pivot)
        ComputeClampData(_ghost, _targetRoom, out Vector3 pivotToCenter, out float rangeX, out float rangeZ, out float extentsY);

        // Y : aligne le bas du mesh sur le sol de la chambre.
        // Le pivot de la chambre est au niveau du sol (local Y=0 = sol), pas au centre.
        float floorY = _targetRoom.transform.position.y;
        desiredPivot.y = floorY + extentsY - pivotToCenter.y;

        _ghostWorldPos = ClampPivotToRoom(desiredPivot, _targetRoom, pivotToCenter, rangeX, rangeZ);

        // Validité : le centre du mesh est dans la chambre
        var localCenter = _targetRoom.transform.InverseTransformPoint(_ghostWorldPos + pivotToCenter);
        _currentValid = Mathf.Abs(localCenter.x) <= 0.5f && Mathf.Abs(localCenter.z) <= 0.5f;

        // Validité : pas de chevauchement avec les meubles déjà posés
        if (_currentValid)
        {
            var ghostBounds = BoundsUtils.Get(_ghost);
            ghostBounds.Expand(-0.05f);
            foreach (var fi in _targetRoom.PlacedFurniture)
            {
                if (fi == null || fi == _movingFurniture) continue;
                var fb = BoundsUtils.Get(fi.CurrentVisual != null ? fi.CurrentVisual : fi.gameObject);
                fb.Expand(-0.05f);
                if (ghostBounds.Intersects(fb)) { _currentValid = false; break; }
            }
        }

        // Lissage visuel — on préserve le Y cible (sol) sans le lerper
        float t = 1f - Mathf.Exp(-ghostMoveSmooth * Time.deltaTime);
        Vector3 lerpedPivot = Vector3.Lerp(_ghost.transform.position, _ghostWorldPos, t);
        lerpedPivot.y = _ghostWorldPos.y;
        _ghost.transform.position = lerpedPivot;

        _visualRotY = Mathf.LerpAngle(_visualRotY, _rotationY, 1f - Mathf.Exp(-ghostRotateSmooth * Time.deltaTime));
        _ghost.transform.rotation = Quaternion.Euler(0f, _visualRotY, 0f);

        SetGhostMaterial(_currentValid ? ghostValidMaterial : ghostInvalidMaterial);
    }

    void PlaceOnChairSlot()
    {
        var chairContainer = new GameObject(_furnitureData.furnitureName);
        chairContainer.transform.SetPositionAndRotation(
            _snapSlot.transform.position,
            _snapSlot.transform.rotation * Quaternion.Euler(0f, _rotationY, 0f));

        var chairFi = chairContainer.AddComponent<FurnitureInstance>();
        chairFi.Init(_furnitureData, _targetRoom);

        if (_furnitureData.prefab != null)
        {
            var visual = Instantiate(_furnitureData.prefab, chairContainer.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            foreach (var col in visual.GetComponentsInChildren<Collider>())
                col.enabled = false;
            chairFi.SetVisual(visual);
        }

        var occupant = chairContainer.AddComponent<ChairSlotOccupant>();
        occupant.Slot = _snapSlot;
        _snapSlot.Occupy();
        _snapSlot = null;

        _targetRoom.AddFurniture(chairFi);

        // Recrée le ghost pour poser une autre chaise — curseur reste là où il était
        DestroyGhost();
        _ghost = CreateGhost(_furnitureData.prefab);
        _placingFrameCount = -1;
    }

    ChairSlot FindNearestFreeChairSlot()
    {
        ChairSlot best     = null;
        float     bestDist = float.MaxValue;
        foreach (var slot in ChairSlot.All)
        {
            if (slot.IsOccupied) continue;
            // Distance XZ uniquement — le curseur et les slots n'ont pas le même Y
            float dist = Vector2.Distance(
                new Vector2(_cursorPos.x, _cursorPos.z),
                new Vector2(slot.transform.position.x, slot.transform.position.z));
            if (dist < bestDist) { best = slot; bestDist = dist; }
        }
        return bestDist <= chairSlotSnapRange ? best : null;
    }

    // ─── Input ────────────────────────────────────────────────────

    void HandleInput()
    {
        // En mode preview (panneau ouvert), le panneau gère confirm/cancel
        if (PanelIsControlling) return;

        if (_playerInput.WasPressed(rotateActionName)) _rotationY = (_rotationY + 45f) % 360f;

        // Confirmation : action dédiée OU Interact (seulement à partir du 2e frame pour éviter
        // que le même appui qui sélectionne le meuble dans le Picker ne le replace immédiatement)
        bool confirm = _playerInput.WasPressed(confirmActionName)
                    || (_placingFrameCount > 1 && _playerInput.WasPressed(interactActionName));
        if (confirm) TryPlace();

        if (_playerInput.WasPressed(cancelActionName)) CancelPlacing();
    }

    // ─── Placement ────────────────────────────────────────────────

    void TryPlace()
    {
        if (_targetRoom == null || _furnitureData == null) return;
        if (!_currentValid) return;

        // ── Mode déplacement : repositionne le meuble existant ────────────
        if (_movingFurniture != null)
        {
            _movingFurniture.transform.SetPositionAndRotation(
                _ghostWorldPos, Quaternion.Euler(0f, _rotationY, 0f));

            foreach (var rend in _movingFurniture.GetComponentsInChildren<Renderer>())
                rend.enabled = true;

            bool doUpgrade   = _upgradeAfterMove;
            var  movedFurni  = _movingFurniture;
            var  savedRoom   = _targetRoom;
            _upgradeAfterMove = false;
            _movingFurniture  = null;
            DestroyGhost();
            _isPlacing     = false;
            _furnitureData = null;
            _targetRoom    = null;

            if (doUpgrade) movedFurni.Upgrade();

            // Déplacement simple : repasse en mode sélection Move pour enchaîner
            if (!doUpgrade)
                _picker?.StartPicking(savedRoom, FurniturePickMode.Move);

            return;
        }

        // ── Mode placement neuf ───────────────────────────────────────────
        if (!EconomyManager.Instance.TrySpend(_furnitureData.cost))
        {
            return;
        }

        // ── Chaise : pose sur le ChairSlot ───────────────────────────────
        if (_furnitureData.requiresChairSlot)
        {
            if (_snapSlot == null) return;
            PlaceOnChairSlot();
            return;
        }

        var rot       = Quaternion.Euler(0f, _rotationY, 0f);
        var container = new GameObject(_furnitureData.furnitureName);
        container.transform.SetPositionAndRotation(_ghostWorldPos, rot);

        var fi = container.AddComponent<FurnitureInstance>();
        fi.Init(_furnitureData, _targetRoom);

        if (_furnitureData.prefab != null)
        {
            var visual = Instantiate(_furnitureData.prefab, container.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            foreach (var col in visual.GetComponentsInChildren<Collider>())
                col.enabled = false;
            fi.SetVisual(visual);
        }
        else
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(container.transform, false);
            cube.transform.localScale = Vector3.one * 0.5f;
        }

        _targetRoom.AddFurniture(fi);

        if (_furnitureData.matchingChair != null)
        {
            // Table posée (a une chaise assortie définie) : enchaîne directement sur la pose de
            // chaises autour, sans repasser par le menu meuble — reste en mode placement (garde le
            // callback _onDone en cours), curseur laissé où était la table plutôt que recentré.
            var chairData = _furnitureData.matchingChair;
            DestroyGhost();
            _movingFurniture   = null;
            _upgradeAfterMove  = false;
            _furnitureData     = chairData;
            _rotationY         = 0f;
            _visualRotY        = 0f;
            _placingFrameCount = 0;
            _currentValid      = true;
            _ghost             = CreateGhost(chairData.prefab);
        }
        else if (_furnitureData.multiPlace)
        {
            DestroyGhost();
            _movingFurniture   = null;
            _upgradeAfterMove  = false;
            _placingFrameCount = 0;
            _ghost             = CreateGhost(_furnitureData.prefab);
            _cursorPos         = _ghostWorldPos;
        }
        else
        {
            CancelPlacing();
        }
    }

    // ─── Ghost helpers ────────────────────────────────────────────

    GameObject CreateGhost(GameObject prefab)
    {
        GameObject ghost;
        if (prefab != null)
        {
            ghost = Instantiate(prefab);
        }
        else
        {
            ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ghost.transform.localScale = Vector3.one * 0.5f;
        }
        ghost.name = "Ghost_Furniture";

        foreach (var col in ghost.GetComponentsInChildren<Collider>())
            col.enabled = false;
        foreach (var mb in ghost.GetComponentsInChildren<MonoBehaviour>())
            if (mb != null) mb.enabled = false;

        if (ghostValidMaterial != null)
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

    // ─── Helpers géométriques ─────────────────────────────────────

    /// <summary>
    /// Calcule les paramètres de clamping basés sur le CENTRE DU MESH (bounds AABB),
    /// pas le pivot du prefab.
    ///
    /// pivotToCenter : vecteur monde du pivot vers le centre des bounds.
    /// rangeX/Z      : plage locale autorisée pour le centre du mesh (0 = au centre de la chambre).
    ///
    /// Valeurs conservatives (pivotToCenter=0, range=0.1) si les bounds ne sont pas prêtes.
    /// </summary>
    void ComputeClampData(GameObject ghost, RoomInstance room,
        out Vector3 pivotToCenter, out float rangeX, out float rangeZ, out float extentsY)
    {
        pivotToCenter = Vector3.zero;
        rangeX = rangeZ = 0.1f;
        extentsY = 0.25f; // valeur conservative

        if (ghost == null || room == null) return;

        // Encapsule les bounds monde de tous les renderers (ignore les bounds à zéro du premier frame)
        Bounds? wb = null;
        foreach (var rend in ghost.GetComponentsInChildren<Renderer>())
        {
            if (rend.bounds.size.sqrMagnitude < 0.0001f) continue;
            if (!wb.HasValue) wb = rend.bounds;
            else { var b = wb.Value; b.Encapsulate(rend.bounds); wb = b; }
        }
        if (!wb.HasValue) return;

        // Offset monde : pivot → centre du mesh
        pivotToCenter = wb.Value.center - ghost.transform.position;
        extentsY      = wb.Value.extents.y;

        // Demi-extents du mesh en espace local de la chambre (axe par axe)
        var sc = room.transform.localScale;
        float halfX = sc.x > 0f ? wb.Value.extents.x / sc.x : 0f;
        float halfZ = sc.z > 0f ? wb.Value.extents.z / sc.z : 0f;

        // Plage autorisée pour le CENTRE du mesh : marge configurable par rapport aux murs
        rangeX = Mathf.Max(0f, 0.5f - halfX - wallMargin);
        rangeZ = Mathf.Max(0f, 0.5f - halfZ - wallMargin);
    }

    /// <summary>
    /// Contraint la position du pivot de sorte que le CENTRE DU MESH reste dans la chambre.
    /// pivotToCenter : vecteur monde pivot → centre mesh.
    /// rangeX/Z      : plage locale autorisée pour le centre mesh.
    /// </summary>
    static Vector3 ClampPivotToRoom(Vector3 pivotWorld, RoomInstance room,
        Vector3 pivotToCenter, float rangeX, float rangeZ)
    {
        // Centre mesh désiré
        Vector3 meshCenter = pivotWorld + pivotToCenter;

        // Clamp le centre mesh en espace local chambre
        var localCenter = room.transform.InverseTransformPoint(meshCenter);
        localCenter.x = Mathf.Clamp(localCenter.x, -rangeX, rangeX);
        localCenter.z = Mathf.Clamp(localCenter.z, -rangeZ, rangeZ);

        // Retourne la position du pivot correspondante
        return room.transform.TransformPoint(localCenter) - pivotToCenter;
    }

    /// <summary>Contraint worldPos à l'intérieur des bornes de la chambre — marge uniforme (utilisé à l'init).</summary>
    static Vector3 ClampToRoom(Vector3 worldPos, RoomInstance room, float halfExtent = 0.45f)
    {
        var localPos = room.transform.InverseTransformPoint(worldPos);
        localPos.x = Mathf.Clamp(localPos.x, -halfExtent, halfExtent);
        localPos.z = Mathf.Clamp(localPos.z, -halfExtent, halfExtent);
        return room.transform.TransformPoint(localPos);
    }

}
