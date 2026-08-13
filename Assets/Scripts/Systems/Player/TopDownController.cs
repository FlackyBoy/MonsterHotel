using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Déplacement top-down sur le plan XZ via Rigidbody.
/// Reçoit l'input depuis PlayerInputBinder.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class TopDownController : MonoBehaviour
{
    [Header("Déplacement")]
    public float moveSpeed    = 5f;
    public float sprintSpeed  = 9f;
    [Tooltip("Vitesse de rotation en degrés/seconde — valeur de départ, à ajuster. Une rotation trop abrupte fait que le personnage fait quasi instantanément face à sa direction de déplacement, ce qui coince MoveX/MoveZ près de 0 (zone instable du Blend Tree directionnel) et fait 'sauter' le blend entre clips.")]
    public float rotationSpeed = 480f; // degrés/seconde

    [Header("Animation")]
    [Tooltip("Lissage (Animator.SetFloat dampTime) des paramètres Speed/MoveX/MoveZ — valeur de départ, à ajuster.")]
    public float moveBlendDampTime = 0.1f;

    Rigidbody _rb;
    Animator  _animator;
    Vector2   _moveInput;
    bool      _sprinting;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int MoveXHash = Animator.StringToHash("MoveX");
    static readonly int MoveZHash = Animator.StringToHash("MoveZ");

    void Awake()
    {
        _rb       = GetComponent<Rigidbody>();
        _animator = GetComponentInChildren<Animator>();
        _rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        var cfg = HotelConfig.Instance;
        if (cfg != null) { moveSpeed = cfg.playerMoveSpeed; sprintSpeed = cfg.playerSprintSpeed; }
    }

    RoomPlacer       _roomPlacer;
    FurniturePlacer  _furniturePlacer;
    DecorationPlacer _decorationPlacer;
    PlayerInput      _playerInput;

    void Start()
    {
        _roomPlacer       = GetComponent<RoomPlacer>();
        _furniturePlacer  = GetComponent<FurniturePlacer>();
        _decorationPlacer = GetComponent<DecorationPlacer>();
        _playerInput      = GetComponent<PlayerInput>();
    }

    /// <summary>
    /// Caméra qui suit CE joueur (RW1 — écran splitté statique, une caméra indépendante par
    /// joueur). Résolue à la demande via SplitScreenManager plutôt que mise en cache une fois,
    /// pour éviter une valeur périmée si ce Start() tourne avant que SplitScreenManager n'ait
    /// fini d'assigner les caméras. Null tant qu'aucune caméra n'est encore assignée à ce joueur.
    /// </summary>
    public Camera PlayerCamera =>
        SplitScreenManager.Instance != null ? SplitScreenManager.Instance.GetCameraForPlayer(_playerInput) : null;

    /// <summary>
    /// True si un des 3 placers est activement en train de placer quelque chose — piloté par le
    /// joueur avec un curseur libre (vue construction). Pour FurniturePlacer, exclut le mode
    /// "aperçu pendant navigation du menu" (PanelIsControlling) : RoomManagementPanel.SelectButton()
    /// appelle StartPlacing() dès qu'on survole un item de la liste, avant toute confirmation —
    /// sans cette exclusion, naviguer dans le menu ferait clignoter le zoom/la grille caméra.
    /// </summary>
    public bool IsBuilding =>
        (_roomPlacer != null && _roomPlacer.IsPlacing) ||
        (_furniturePlacer != null && _furniturePlacer.IsPlacing && !_furniturePlacer.PanelIsControlling) ||
        (_decorationPlacer != null && _decorationPlacer.IsPlacing);

    /// <summary>Position monde du curseur du placer actuellement actif (voir IsBuilding), sinon la position du joueur.</summary>
    public Vector3 BuildCursorWorldPos
    {
        get
        {
            if (_roomPlacer != null && _roomPlacer.IsPlacing) return _roomPlacer.CursorWorldPos;
            if (_furniturePlacer != null && _furniturePlacer.IsPlacing && !_furniturePlacer.PanelIsControlling) return _furniturePlacer.GhostWorldPos;
            if (_decorationPlacer != null && _decorationPlacer.IsPlacing) return _decorationPlacer.CursorWorldPos;
            return transform.position;
        }
    }

    /// <summary>Appelé par PlayerInputBinder chaque frame.</summary>
    public void SetMoveInput(Vector2 input) => _moveInput = input;

    /// <summary>Input de mouvement courant (utilisé par RoomPlacer pour le curseur).</summary>
    public Vector2 MoveInput => _moveInput;

    Vector2 _cursorInput;
    /// <summary>Input curseur (stick droit / flèches) — utilisé par RoomPlacer.</summary>
    public void SetCursorInput(Vector2 input) => _cursorInput = input;
    public Vector2 CursorInput => _cursorInput;

    /// <summary>Appelé par PlayerInputBinder pour activer/désactiver le sprint.</summary>
    public void SetSprintInput(bool sprinting) => _sprinting = sprinting;

    void FixedUpdate()
    {
        // Déplacement verrouillé pendant la construction (mode curseur libre) — sur clavier P2,
        // les flèches servent à la fois au déplacement ET au curseur de placement, donc sans ce
        // verrou bouger le curseur ferait aussi marcher le personnage.
        if (IsBuilding)
        {
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
            SetAnimFloat(SpeedHash, 0f);
            SetAnimFloat(MoveXHash, 0f);
            SetAnimFloat(MoveZHash, 0f);
            return;
        }

        Vector3 dir = new Vector3(_moveInput.x, 0f, _moveInput.y);

        // Déplacement
        float speed = _sprinting ? sprintSpeed : moveSpeed;
        Vector3 velocity = dir * speed;
        _rb.linearVelocity = new Vector3(velocity.x, _rb.linearVelocity.y, velocity.z);

        // Rotation vers la direction de déplacement — vitesse angulaire réelle (RotateTowards,
        // pas un facteur de blend Slerp) : avec Slerp, rotationSpeed * fixedDeltaTime dépassait 1
        // dès ~50°/s, donc le personnage faisait face à sa direction de déplacement en un seul
        // tick physique, quel que soit rotationSpeed. Voir tooltip du champ.
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
            _rb.rotation = Quaternion.RotateTowards(_rb.rotation, target, rotationSpeed * Time.fixedDeltaTime);
        }

        // Direction de déplacement en espace local (relative à l'orientation actuelle du
        // personnage, déjà mise à jour ci-dessus) — alimente le Blend Tree directionnel de
        // l'Animator (avant/arrière/côtés/diagonales) au lieu d'un simple Idle/Marche uniforme.
        Vector3 localDir = transform.InverseTransformDirection(dir);

        SetAnimFloat(SpeedHash, dir.magnitude);
        SetAnimFloat(MoveXHash, localDir.x);
        SetAnimFloat(MoveZHash, localDir.z);
    }

    /// <summary>
    /// SetFloat amorti (Speed/MoveX/MoveZ) — snap à la cible une fois l'écart négligeable,
    /// sinon l'amortissement exponentiel de SetFloat(dampTime) ne l'atteint jamais pile et la
    /// valeur continue de dériver indéfiniment (visible en Idle dans le panneau Parameters).
    /// </summary>
    void SetAnimFloat(int hash, float target)
    {
        if (_animator == null) return;
        if (Mathf.Abs(_animator.GetFloat(hash) - target) < 0.01f)
            _animator.SetFloat(hash, target);
        else
            _animator.SetFloat(hash, target, moveBlendDampTime, Time.fixedDeltaTime);
    }
}
