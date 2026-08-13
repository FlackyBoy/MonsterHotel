using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mode sélection de meuble par proximité — activé depuis RoomManagementPanel onglet Meuble.
///
/// Modes :
///   Move    → réinitie FurniturePlacer.StartMoving(fi)
///   Upgrade → affiche un ghost du meuble amélioré ; Interact confirme si ghost valide (vert)
///   Remove  → fi.Remove()
///
/// Contrôles :
///   Interact         → exécute l'action sur le meuble highlighté
///   CancelPlacement  → annule le mode
/// </summary>
public enum FurniturePickMode { Move, Upgrade, Remove }

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(FurniturePlacer))]
public class FurniturePicker : MonoBehaviour
{
    [Header("Détection")]
    public float pickRange = 1.5f;

    [Header("Highlight")]
    [Tooltip("Matériau appliqué au meuble le plus proche")]
    public Material highlightMaterial;

    [Header("Noms des actions dans la Player Action Map")]
    public string interactActionName = "Interact";
    public string cancelActionName   = "CancelPlacement";

    // ─── Privé ────────────────────────────────────────────────────

    bool              _isPicking;
    bool              _hasEnteredRoom;
    bool              _skipInputThisFrame; // ignore les inputs le frame où StartPicking est appelé
    FurniturePickMode _mode;
    RoomInstance      _targetRoom;
    FurnitureInstance             _highlighted;
    Material[][]                  _savedMaterials;
    GameObject                    _prompt;
    TMPro.TextMeshProUGUI         _promptLabel;

    // Ghost de prévisualisation upgrade
    GameObject _upgradeGhost;
    bool       _upgradeGhostValid = true;

    PlayerInput     _playerInput;
    FurniturePlacer _placer;

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
        _placer      = GetComponent<FurniturePlacer>();
    }

    void Update()
    {
        if (!_isPicking) return;

        // Auto-cancel si le joueur sort de la chambre APRÈS y être entré
        if (_targetRoom != null)
        {
            var lp = _targetRoom.transform.InverseTransformPoint(transform.position);
            bool inside = Mathf.Abs(lp.x) < 0.5f && Mathf.Abs(lp.z) < 0.5f;
            if (inside)
                _hasEnteredRoom = true;
            else if (_hasEnteredRoom)
            {
                CancelPicking();
                return;
            }
        }

        UpdateHighlight();
        UpdateUpgradeGhost();
        UpdatePromptPosition();
        HandleInput();
    }

    void OnDestroy() => CancelPicking();

    // ─── API publique ─────────────────────────────────────────────

    public bool IsPicking => _isPicking;

    public void StartPicking(RoomInstance room, FurniturePickMode mode)
    {
        if (room == null) return;
        _targetRoom          = room;
        _mode                = mode;
        _isPicking           = true;
        _hasEnteredRoom      = false;
        _skipInputThisFrame  = true; // évite que l'Interact qui a confirmé le Placer sélectionne aussitôt un meuble
    }

    public void CancelPicking()
    {
        Dehighlight();
        DestroyPrompt();
        DestroyUpgradeGhost();
        _targetRoom = null;
        _isPicking  = false;
    }

    // ─── Highlight ────────────────────────────────────────────────

    void UpdateHighlight()
    {
        if (_targetRoom == null) return;

        FurnitureInstance nearest     = null;
        float             nearestDist = pickRange;

        foreach (var fi in _targetRoom.PlacedFurniture)
        {
            if (fi == null) continue;
            if (_mode == FurniturePickMode.Upgrade && !fi.CanUpgrade) continue;
            float d = Vector3.Distance(transform.position, fi.transform.position);
            if (d < nearestDist) { nearestDist = d; nearest = fi; }
        }

        if (nearest == _highlighted) return;
        Dehighlight();
        if (nearest != null) Highlight(nearest);
    }

    void Highlight(FurnitureInstance fi)
    {
        _highlighted = fi;

        if (highlightMaterial != null)
        {
            var rends = fi.GetComponentsInChildren<Renderer>();
            _savedMaterials = new Material[rends.Length][];
            for (int i = 0; i < rends.Length; i++)
            {
                _savedMaterials[i] = rends[i].sharedMaterials;
                var mats = new Material[rends[i].materials.Length];
                for (int j = 0; j < mats.Length; j++) mats[j] = highlightMaterial;
                rends[i].materials = mats;
            }
        }

        CreatePrompt(fi);
    }

    void Dehighlight()
    {
        if (_highlighted == null) return;

        if (_savedMaterials != null)
        {
            var rends = _highlighted.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rends.Length && i < _savedMaterials.Length; i++)
                rends[i].sharedMaterials = _savedMaterials[i];
        }

        DestroyPrompt();
        DestroyUpgradeGhost();
        _highlighted    = null;
        _savedMaterials = null;
    }

    // ─── Ghost upgrade ────────────────────────────────────────────

    /// <summary>
    /// En mode Upgrade, affiche un ghost semi-transparent du meuble amélioré à la position
    /// du meuble courant. Vert = place disponible, Rouge = superposition ou débordement.
    /// </summary>
    void UpdateUpgradeGhost()
    {
        if (_mode != FurniturePickMode.Upgrade || _highlighted == null || !_highlighted.CanUpgrade)
        {
            DestroyUpgradeGhost();
            return;
        }

        var nextPrefab = _highlighted.Data?.nextUpgrade?.prefab;
        if (nextPrefab == null) { DestroyUpgradeGhost(); return; }

        // Création du ghost si nécessaire
        if (_upgradeGhost == null)
            _upgradeGhost = CreateUpgradeGhost(nextPrefab);

        // Aligne sur le meuble actuel
        _upgradeGhost.transform.SetPositionAndRotation(
            _highlighted.transform.position,
            _highlighted.transform.rotation);

        // Vérifie la validité (murs + autres meubles)
        _upgradeGhostValid = !IsGhostOverlapping();

        ApplyGhostMaterial(_upgradeGhostValid);

        // Met à jour le prompt pour refléter l'état
        RefreshUpgradePrompt();
    }

    GameObject CreateUpgradeGhost(GameObject prefab)
    {
        var ghost = Instantiate(prefab);
        ghost.name = "_UpgradeGhost";
        foreach (var col in ghost.GetComponentsInChildren<Collider>())
            col.enabled = false;
        foreach (var mb in ghost.GetComponentsInChildren<MonoBehaviour>())
            mb.enabled = false;
        return ghost;
    }

    void DestroyUpgradeGhost()
    {
        if (_upgradeGhost != null) { Destroy(_upgradeGhost); _upgradeGhost = null; }
        _upgradeGhostValid = true;
    }

    void ApplyGhostMaterial(bool valid)
    {
        if (_upgradeGhost == null) return;
        var mat = valid ? _placer.ghostValidMaterial : _placer.ghostInvalidMaterial;
        if (mat == null) return;
        foreach (var rend in _upgradeGhost.GetComponentsInChildren<Renderer>())
        {
            var mats = new Material[rend.materials.Length];
            for (int j = 0; j < mats.Length; j++) mats[j] = mat;
            rend.materials = mats;
        }
    }

    /// <summary>
    /// Retourne true si le ghost upgrade déborde de la chambre OU chevauche un autre meuble.
    /// </summary>
    bool IsGhostOverlapping()
    {
        if (_upgradeGhost == null || _targetRoom == null) return false;

        var ghostBounds = BoundsUtils.Get(_upgradeGhost);

        // Vérification des 4 coins dans le plan XZ (débordement mur)
        var e = ghostBounds.extents;
        var c = ghostBounds.center;
        Vector3[] corners =
        {
            c + new Vector3( e.x, 0f,  e.z),
            c + new Vector3(-e.x, 0f,  e.z),
            c + new Vector3( e.x, 0f, -e.z),
            c + new Vector3(-e.x, 0f, -e.z),
        };
        foreach (var corner in corners)
        {
            var lp = _targetRoom.transform.InverseTransformPoint(corner);
            if (Mathf.Abs(lp.x) > 0.48f || Mathf.Abs(lp.z) > 0.48f) return true;
        }

        // Vérification chevauchement avec les autres meubles
        ghostBounds.Expand(-0.05f);
        foreach (var other in _targetRoom.PlacedFurniture)
        {
            if (other == null || other == _highlighted) continue;
            var ob = BoundsUtils.Get(other.gameObject);
            ob.Expand(-0.05f);
            if (ghostBounds.Intersects(ob)) return true;
        }

        return false;
    }

    void RefreshUpgradePrompt()
    {
        if (_promptLabel == null || _highlighted == null) return;

        _promptLabel.text = _upgradeGhostValid
            ? $"[ {interactActionName} ] Améliorer : {_highlighted.Data?.furnitureName} ({_highlighted.Data?.upgradeCost} G)"
            : $"⚠ Place insuff. — [ {interactActionName} ] Déplacer ce meuble";
    }

    // ─── Input ────────────────────────────────────────────────────

    void HandleInput()
    {
        if (_skipInputThisFrame) { _skipInputThisFrame = false; return; }

        if (_playerInput.WasPressed(cancelActionName))
        {
            CancelPicking();
            return;
        }

        if (_highlighted == null || !_playerInput.WasPressed(interactActionName)) return;

        switch (_mode)
        {
            case FurniturePickMode.Move:
                var fi = _highlighted;
                CancelPicking();
                _placer.StartMoving(fi);
                break;

            case FurniturePickMode.Upgrade:
                if (_highlighted.CanUpgrade && _upgradeGhostValid)
                {
                    var target = _highlighted;
                    DestroyUpgradeGhost();
                    target.Upgrade();
                    Dehighlight();
                    if (target != null) Highlight(target);
                }
                else if (!_upgradeGhostValid)
                {
                    // Place insuffisante : déplace avec le ghost de l'upgrade pour voir où ça rentre
                    var toMove = _highlighted;
                    CancelPicking();
                    _placer.StartMovingForUpgrade(toMove);
                }
                break;

            case FurniturePickMode.Remove:
                _highlighted.Remove();
                if (_targetRoom == null || _targetRoom.PlacedFurniture.Count == 0)
                    CancelPicking();
                else
                {
                    _highlighted    = null;
                    _savedMaterials = null;
                    DestroyPrompt();
                }
                break;
        }
    }

    // ─── Prompt world-space ───────────────────────────────────────

    void CreatePrompt(FurnitureInstance fi)
    {
        DestroyPrompt();
        (_prompt, _promptLabel) = WorldPrompt.Create(200f, 32f);
        RefreshPromptText(fi);
        PositionPrompt(fi);
    }

    void RefreshPromptText(FurnitureInstance fi)
    {
        if (_promptLabel == null || fi == null) return;

        _promptLabel.text = _mode switch
        {
            FurniturePickMode.Move    => $"[ {interactActionName} ] Déplacer : {fi.Data?.furnitureName}",
            FurniturePickMode.Upgrade => fi.CanUpgrade
                                         ? $"[ {interactActionName} ] Améliorer : {fi.Data?.furnitureName} ({fi.Data?.upgradeCost} G)"
                                         : $"{fi.Data?.furnitureName} — niveau max",
            FurniturePickMode.Remove  => $"[ {interactActionName} ] Retirer : {fi.Data?.furnitureName}",
            _                         => "",
        };
    }

    void PositionPrompt(FurnitureInstance fi)
    {
        if (_prompt == null || fi == null) return;
        var b = BoundsUtils.Get(fi.gameObject);
        _prompt.transform.position = b.center + Vector3.up * (b.extents.y + 0.35f);
        WorldPrompt.FaceCamera(_prompt);
    }

    void UpdatePromptPosition()
    {
        if (_prompt != null && _highlighted != null)
            PositionPrompt(_highlighted);
    }

    void DestroyPrompt()
    {
        if (_prompt != null) { Destroy(_prompt); _prompt = null; _promptLabel = null; }
    }

}
