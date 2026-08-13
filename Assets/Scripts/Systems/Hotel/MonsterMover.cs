using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Déplace un monstre en deux phases :
///   1. NavMeshAgent jusqu'à l'entryPoint (extérieur de la chambre)
///   2. MoveTowards jusqu'à finalTarget (intérieur de la chambre)
/// </summary>
public class MonsterMover : MonoBehaviour
{
    [Header("Mouvement")]
    public float moveSpeed    = 3f;
    public float stoppingDist = 0.15f;
    [Tooltip("Vitesse de rotation en degrés/seconde — valeur de départ, à ajuster.")]
    public float rotationSpeed = 360f;

    [Header("Filet de sécurité chemin partiel")]
    [Tooltip("Distance max (m) que le filet PathPartial peut compléter en ligne droite au-delà du dernier point NavMesh atteint (ex : bordure mal snapée, léger décalage de porte). Au-delà, mieux vaut rester bloqué au bord plutôt que de traverser un mur sur une longue distance — signale une vraie zone NavMesh déconnectée. Valeur de départ — à toi d'ajuster selon la taille de tes pièces/couloirs.")]
    public float partialPathMaxSkip = 2.5f;

    [Header("Animation")]
    [Tooltip("Lissage (Animator.SetFloat dampTime) du paramètre Speed — valeur de départ, à ajuster.")]
    public float speedBlendDampTime = 0.15f;

    NavMeshAgent _agent;
    Animator     _animator;
    Coroutine    _moveCoroutine;
    float        _targetSpeed;

    static readonly int SpeedHash = Animator.StringToHash("Speed");

    /// <summary>Déclenché une seule fois quand le monstre atteint sa première destination (slot de file).</summary>
    public event System.Action OnArrived;

    void Awake()
    {
        _agent    = GetComponent<NavMeshAgent>();
        _animator = GetComponentInChildren<Animator>();
        if (_agent != null)
        {
            _agent.speed            = moveSpeed;
            _agent.stoppingDistance = stoppingDist;
            _agent.updateRotation   = false;
            _agent.updateUpAxis     = false;
            _agent.autoBraking      = false;
            _agent.acceleration     = 20f;
            _agent.enabled          = false; // Désactivé au spawn — évite l'auto-snap NavMesh à l'instanciation
        }
    }

    /// <summary>
    /// Approche en continu le paramètre Speed vers sa cible (0 ou 1, fixée par les coroutines de
    /// déplacement ci-dessous) — évite l'à-coup d'un SetFloat instantané en début/fin de marche.
    /// </summary>
    void Update()
    {
        if (_animator == null) return;
        if (Mathf.Abs(_animator.GetFloat(SpeedHash) - _targetSpeed) < 0.01f)
            _animator.SetFloat(SpeedHash, _targetSpeed);
        else
            _animator.SetFloat(SpeedHash, _targetSpeed, speedBlendDampTime, Time.deltaTime);
    }

    /// <summary>
    /// Phase 1 seulement : NavMesh vers worldTarget.
    /// </summary>
    public void MoveTo(Vector3 worldTarget)
    {
        if (_agent != null) _agent.speed = moveSpeed;
        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
        _moveCoroutine = StartCoroutine(MovePhase1Only(worldTarget));
    }

    /// <summary>
    /// Phase 1 (NavMesh vers entryPoint) puis phase 2 (MoveTowards vers finalTarget).
    /// </summary>
    public void MoveTo(Vector3 entryPoint, Vector3 finalTarget)
    {
        if (_agent != null) _agent.speed = moveSpeed;
        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
        _moveCoroutine = StartCoroutine(MoveTwoPhase(entryPoint, finalTarget));
    }

    public void Stop()
    {
        if (_moveCoroutine != null) { StopCoroutine(_moveCoroutine); _moveCoroutine = null; }
        if (_agent != null && _agent.isActiveAndEnabled) _agent.isStopped = true;
        // Sans ça, _targetSpeed restait bloqué à 1 (mis par la coroutine de déplacement interrompue
        // ci-dessus, jamais ramené à 0 puisqu'elle n'atteint jamais sa fin naturelle) — Update()
        // continuait alors à pousser Speed vers 1 même arrêté, ce qui repartait en anim Walk dès
        // qu'un état concurrent (ex: IsTalking) relâchait la main un instant.
        _targetSpeed = 0f;
    }

    /// <summary>
    /// Marche vers destination puis détruit le GameObject.
    /// Utilisé pour le départ en timeout.
    /// </summary>
    public void WalkToAndDestroy(Vector3 destination)
    {
        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
        _moveCoroutine = StartCoroutine(WalkToAndDestroyRoutine(destination));
    }

    // ─── Phases ───────────────────────────────────────────────────

    IEnumerator MovePhase1Only(Vector3 target)
    {
        _targetSpeed = 1f;
        yield return NavigateTo(target);
        _targetSpeed = 0f;
        OnArrived?.Invoke();
        OnArrived = null;
    }

    IEnumerator MoveTwoPhase(Vector3 entryPoint, Vector3 finalTarget)
    {
        _targetSpeed = 1f;

        // Phase 1 : NavMesh jusqu'à la porte
        yield return NavigateTo(entryPoint);

        // Désactive l'agent pour la phase 2 (sinon il résiste au MoveTowards)
        if (_agent != null) _agent.enabled = false;

        // Phase 2 : ligne droite dans la chambre
        yield return DirectMove(finalTarget);

        _targetSpeed = 0f;
    }

    IEnumerator NavigateTo(Vector3 target)
    {
        // Verrouille Y : tous les déplacements restent à hauteur constante
        float floorY = transform.position.y;
        target.y = floorY;

        if (_agent == null) { yield return DirectMove(target); yield break; }

        if (!_agent.enabled || !_agent.isOnNavMesh)
        {
            _agent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out var warp, 3f, NavMesh.AllAreas))
            {
                _agent.Warp(warp.position);
                // Restaure le Y d'origine — le Warp peut déplacer le personnage sur la surface NavMesh
                var p = transform.position;
                p.y = floorY;
                transform.position = p;
            }
            else
            {
                // Aucun NavMesh trouvé : désactiver l'agent AVANT DirectMove pour éviter qu'il
                // résiste aux modifications manuelles de transform.position
                _agent.enabled = false;
                yield return DirectMove(target);
                yield break;
            }
        }

        // Snap XZ sur le NavMesh, sans toucher au Y
        if (NavMesh.SamplePosition(target, out var hit, 5f, NavMesh.AllAreas))
        {
            target.x = hit.position.x;
            target.z = hit.position.z;
            // target.y inchangé
        }

        var path = new NavMeshPath();
        _agent.CalculatePath(target, path);

        _agent.enabled = false;

        if (path.status == NavMeshPathStatus.PathComplete ||
            path.status == NavMeshPathStatus.PathPartial)
        {
            foreach (var corner in path.corners)
            {
                var c = corner;
                c.y = transform.position.y;
                yield return DirectMove(c);
            }

            // PathPartial : le chemin s'arrête au bord de la zone NavMesh atteignable (ex : deux
            // zones pas complètement reliées). Ne complète en ligne droite que sur une courte
            // distance (bordure mal snapée) — au-delà, ça revient à traverser un mur sur plusieurs
            // mètres. Dans ce cas, reste au dernier point NavMesh valide et log un warning pour
            // repérer la vraie zone déconnectée plutôt que de la masquer.
            if (path.status == NavMeshPathStatus.PathPartial)
            {
                var lastCorner = path.corners.Length > 0
                    ? path.corners[path.corners.Length - 1]
                    : transform.position;
                lastCorner.y = transform.position.y;

                float remaining = Vector3.Distance(lastCorner, target);
                if (remaining <= partialPathMaxSkip)
                {
                    yield return DirectMove(target);
                }
                else
                {
                    Debug.LogWarning($"[NavMesh] {name} — chemin partiel trop long ({remaining:F1}m restants) vers {target}, reste au dernier point atteignable plutôt que de traverser un mur.");
                }
            }
        }
        else
        {
            // PathInvalid : aucun chemin du tout (même partiel) — même plafond que PathPartial,
            // sinon ligne droite sans limite = traverse les murs en silence (aucun corner à
            // rapprocher ici, donc distance mesurée depuis la position actuelle).
            float remaining = Vector3.Distance(transform.position, target);
            if (remaining <= partialPathMaxSkip)
            {
                yield return DirectMove(target);
            }
            else
            {
                Debug.LogWarning($"[NavMesh] {name} — aucun chemin trouvé ({remaining:F1}m à vol d'oiseau) vers {target}, reste sur place plutôt que de traverser un mur.");
            }
        }
    }

    IEnumerator WalkToAndDestroyRoutine(Vector3 destination)
    {
        _targetSpeed = 1f;
        yield return NavigateTo(destination);
        _targetSpeed = 0f;
        Destroy(gameObject);
    }

    IEnumerator DirectMove(Vector3 target)
    {
        float speed = moveSpeed;
        target.y = transform.position.y;
        while (Vector3.Distance(transform.position, target) > stoppingDist)
        {
            var dir = (target - transform.position).normalized;
            transform.position = Vector3.MoveTowards(
                transform.position, target, speed * Time.deltaTime);
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    Quaternion.LookRotation(dir),
                    rotationSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = target;
    }
}
