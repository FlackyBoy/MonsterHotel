using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Un effet ponctuel de bagarre (voir MonsterFightBehavior.fightBurstPrefabs) avec ses propres réglages — indépendants du nuage en boucle et des autres entrées, pour pouvoir régler une catégorie d'effets (ex: les textes) différemment du reste.</summary>
[System.Serializable]
public class FightBurstEntry
{
    public GameObject prefab;
    [Tooltip("Si coché, cet effet reçoit un décalage horizontal aléatoire (rayon burstPositionJitter) — sinon il apparaît pile centré sur le point milieu des deux monstres.")]
    public bool applyJitter = true;
    [Tooltip("Décalage vertical (m) propre à cet effet, par rapport au point milieu — évite qu'il clippe dans le sol. Indépendant de vfxHeightOffset (qui ne concerne que le nuage en boucle).")]
    public float heightOffset = 0.8f;
    [Tooltip("Multiplicateur de taille propre à cet effet (1 = taille d'origine du prefab). Indépendant de vfxScale (qui ne concerne que le nuage en boucle).")]
    public float scale = 3f;
    [Tooltip("Poids de tirage aléatoire par rapport aux autres effets de la liste — 1 = normal, 2 = deux fois plus fréquent, 0.5 = deux fois plus rare. Valeur de départ, à ajuster.")]
    public float weight = 1f;
}

/// <summary>
/// Fait se battre deux monstres incompatibles quand ils se croisent en balade. Composant frère de
/// MonsterSocialBehavior (même squelette : registre statique, state machine locale, verrouillage
/// synchrone, point de rendez-vous figé, point de sortie unique idempotent) — pas une extension,
/// pour rester cohérent avec le style du projet (duplication directe plutôt qu'abstraction
/// prématurée pour seulement 2 comportements "occupants").
///
/// Setup : ajouté automatiquement par SpawnScheduler (avant Needs/Seeker/Roam — voir note d'ordre
/// dans SpawnScheduler.TrySpawn). Activé par ReservationSystem quand la chambre est assignée. Pas
/// actif pour les visiteurs repas sans chambre (cohérent avec MonsterRoamBehavior).
///
/// Deux issues possibles : le joueur sépare la bagarre à temps (ResolveByPlayer, pénalité faible)
/// ou personne n'intervient avant fightDurationBeforeAutoBreak (pénalité sévère, appliquée par le
/// leader). Interrupt() lui-même n'applique jamais de pénalité — toujours posée par l'appelant
/// avant, symétrique avec l'absence de bonus de conversation sur interruption forcée.
/// </summary>
public class MonsterFightBehavior : MonoBehaviour
{
    [Header("Recherche adversaire")]
    [Tooltip("Intervalle moyen (sec) entre deux tentatives de recherche d'adversaire — un jitter aléatoire est appliqué pour désynchroniser les monstres. Valeur de départ, à ajuster.")]
    public float searchInterval = 4f;
    [Tooltip("Rayon (m) dans lequel chercher un adversaire — volontairement opportuniste : ne cherche que parmi les monstres déjà proches, ne traverse pas la carte. Valeur de départ, à ajuster.")]
    public float searchRadius = 4f;
    [Tooltip("Délai (sec) après la fin d'une bagarre (normale ou interrompue) avant de pouvoir en rechercher une nouvelle. Valeur de départ, à ajuster.")]
    public float cooldownAfterFight = 25f;

    [Header("Approche")]
    [Tooltip("Distance (m) cible entre les deux monstres pendant la mêlée — volontairement plus rapproché que la conversation, le nuage VFX masque le chevauchement visuel. Valeur de départ, à ajuster.")]
    public float approachArrivalDistance = 1.5f;
    [Tooltip("Marge (m) tolérée autour de la distance cible avant de considérer l'approche terminée.")]
    public float approachTolerance = 0.4f;
    [Tooltip("Fréquence (sec) de réémission de la marche vers l'adversaire — celui-ci peut aussi être en mouvement, donc c'est une cible mobile.")]
    public float approachRepathInterval = 0.4f;

    [Header("Bagarre")]
    [Tooltip("Durée (sec) avant que la bagarre se termine automatiquement si le joueur n'est pas intervenu. Valeur de départ, à ajuster.")]
    public float fightDurationBeforeAutoBreak = 12f;

    [Header("Impact satisfaction")]
    [Tooltip("Pénalité appliquée aux deux monstres si le joueur sépare la bagarre à temps. Valeur de départ, à ajuster.")]
    public float satisfactionPenaltyPlayerResolved = 5f;
    [Tooltip("Pénalité appliquée aux deux monstres si la bagarre se résout par timeout — plus sévère, récompense l'intervention rapide du joueur. Valeur de départ, à ajuster.")]
    public float satisfactionPenaltyTimeout = 15f;

    [Header("VFX")]
    [Tooltip("Prefab du nuage de bagarre (en boucle), instancié entre les deux monstres au début de la mêlée et détruit à la fin. Peut rester vide (pas de VFX, le reste fonctionne quand même). Ex: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Misc/CFXR2 Cartoon Fight (Loop).prefab")]
    public GameObject fightCloudPrefab;
    [Tooltip("Effets ponctuels (one-shot) piochés UN PAR UN au hasard pendant toute la mêlée (jamais tous en même temps) — ex: les textes CFXR _BOOM_/_POW_/_WHAM_/_BOING_ (CFXR Prefabs/Texts/). Ajoute autant d'entrées que tu veux, chacune avec son propre réglage de décalage. Chaque effet se détruit tout seul (comportement standard des prefabs CFXR). Peut rester vide.")]
    public FightBurstEntry[] fightBurstPrefabs;
    [Tooltip("Intervalle min/max (sec) entre deux effets ponctuels pendant la mêlée — tiré aléatoirement à chaque fois. Valeur de départ, à ajuster.")]
    public float burstIntervalMin = 1.5f;
    public float burstIntervalMax = 4f;
    [Tooltip("Rayon (m) du décalage horizontal aléatoire appliqué aux effets qui ont \"Apply Jitter\" coché (voir chaque entrée de Fight Burst Prefabs ci-dessus). Valeur de départ, à ajuster.")]
    public float burstPositionJitter = 1f;
    [Tooltip("Décalage vertical (m) du nuage EN BOUCLE uniquement (fightCloudPrefab) par rapport au point milieu — évite qu'il clippe dans le sol. Chaque effet ponctuel a son propre décalage (voir Fight Burst Prefabs ci-dessus).")]
    public float vfxHeightOffset = 0.8f;
    [Tooltip("Multiplicateur de taille du nuage EN BOUCLE uniquement (fightCloudPrefab) — 1 = taille d'origine. Chaque effet ponctuel a son propre multiplicateur (voir Fight Burst Prefabs ci-dessus). Modifie l'instance, pas le prefab partagé.")]
    public float vfxScale = 3f;

    public static readonly HashSet<MonsterFightBehavior> All = new();

    enum FightState { Idle, Locked, Approaching, Fighting }
    FightState _state = FightState.Idle;

    MonsterFightBehavior _partner;
    bool _isLeader;
    bool _hasArrived;
    bool _active;
    float _cooldownUntil;
    Vector3 _approachTarget;
    Vector3 _approachAxis;
    GameObject _vfxInstance;

    Coroutine _searchCoroutine;
    Coroutine _fightCoroutine;
    Coroutine _burstCoroutine;

    MonsterMover _mover;
    MonsterNeedSeeker _seeker;
    MonsterDataReference _dataRef;
    MonsterSocialBehavior _socialSibling;
    Animator _animator;

    static readonly int IsFightingHash = Animator.StringToHash("IsFighting");

    /// <summary>True dès qu'un verrouillage de bagarre est en cours ou qu'une mêlée est active — consulté par MonsterRoamBehavior et MonsterSocialBehavior.</summary>
    public bool IsBusy => _state != FightState.Idle;
    /// <summary>True uniquement pendant la mêlée active (VFX + anim visibles) — distinct de IsBusy (qui inclut aussi Locked/Approaching). Utilisé par MonsterFightBreaker pour ne proposer l'interaction que quand la bagarre est visuellement en cours.</summary>
    public bool IsFighting => _state == FightState.Fighting;

    // Start() plutôt qu'Awake() — même piège d'ordre d'AddComponent que MonsterSocialBehavior : si
    // ce composant est ajouté directement sur le prefab, son Awake() se déclenche pendant
    // Instantiate(), avant que SpawnScheduler ait ajouté MonsterDataReference/autres composants
    // dynamiques juste après. Start() se déclenche après la fin de l'appel synchrone à
    // SpawnScheduler.TrySpawn(), donc robuste peu importe où ce composant est ajouté.
    //
    // Insuffisant pour l'humain possédé (G8) : voir le commentaire équivalent dans
    // MonsterSocialBehavior — Activate() rafraîchit ces références en plus, pour le cas où
    // MonsterDataReference n'existe encore qu'après Start() (possession différée dans le temps).
    void Start() => CacheReferences();

    void CacheReferences()
    {
        _mover         = GetComponent<MonsterMover>();
        _seeker        = GetComponent<MonsterNeedSeeker>();
        _dataRef       = GetComponent<MonsterDataReference>();
        _socialSibling = GetComponent<MonsterSocialBehavior>();
        _animator      = GetComponentInChildren<Animator>();
    }

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

    /// <summary>Appelé par ReservationSystem quand la chambre est assignée (ou par PossessableHuman.Possess()).</summary>
    public void Activate()
    {
        if (_active) return;
        CacheReferences();
        _active = true;
        Debug.Log($"[Fight] {name} — comportement de bagarre activé (recherche toutes les ~{searchInterval}s, rayon {searchRadius}m).");
        _searchCoroutine = StartCoroutine(SearchLoop());
    }

    void OnDestroy() => Interrupt();

    // ─── Recherche ────────────────────────────────────────────────

    IEnumerator SearchLoop()
    {
        while (_active)
        {
            yield return new WaitForSeconds(searchInterval + Random.Range(-1f, 1f));

            if (_state != FightState.Idle) continue;
            if (Time.time < _cooldownUntil) continue;
            if (_seeker != null && _seeker.IsSeeking) continue;
            if (_socialSibling != null && _socialSibling.IsBusy) continue; // occupé à discuter

            TryFindOpponent();
        }
    }

    /// <summary>
    /// Scan + verrouillage des deux côtés — doit rester 100% synchrone (aucun yield) : c'est cette
    /// synchronicité qui tient lieu de mutex et empêche un 3e monstre de s'incruster entre le scan
    /// et l'écriture des états.
    /// </summary>
    bool TryFindOpponent()
    {
        var myData = _dataRef != null ? _dataRef.Data : null;
        if (myData == null) return false;

        MonsterFightBehavior best = null;
        float bestDist = float.MaxValue;

        foreach (var other in All)
        {
            if (other == this || other == null) continue;
            if (!other._active) continue;
            if (other._state != FightState.Idle) continue;
            if (Time.time < other._cooldownUntil) continue;
            if (other._seeker != null && other._seeker.IsSeeking) continue;
            if (other._socialSibling != null && other._socialSibling.IsBusy) continue;

            var otherData = other._dataRef != null ? other._dataRef.Data : null;
            if (otherData == null) continue;
            if (!myData.IsSociallyIncompatible(otherData.monsterType)) continue;
            if (!otherData.IsSociallyIncompatible(myData.monsterType)) continue;

            float dist = Vector3.Distance(transform.position, other.transform.position);
            if (dist > searchRadius || dist >= bestDist) continue;

            best = other;
            bestDist = dist;
        }

        if (best == null) return false;

        Debug.Log($"[Fight] {name} ↔ {best.name} — adversaire trouvé (dist {bestDist:F1}m), début approche.");
        BeginPairing(best, asLeader: true);
        best.BeginPairing(this, asLeader: false);
        return true;
    }

    /// <summary>
    /// DEBUG : force une bagarre avec le monstre libre le plus proche, en ignorant compatibilité,
    /// cooldown et recherche de besoin — pour tester sans attendre une rencontre opportuniste.
    /// Clic droit sur le composant (Inspector, en Play Mode) sur un monstre pour l'utiliser.
    /// </summary>
    [ContextMenu("DEBUG — Forcer une bagarre avec le plus proche")]
    public void DebugForceFight()
    {
        if (_state != FightState.Idle)
        {
            Debug.LogWarning($"[Fight] DEBUG — {name} déjà occupé (état {_state}), impossible de forcer.");
            return;
        }

        MonsterFightBehavior best = null;
        float bestDist = float.MaxValue;

        foreach (var other in All)
        {
            if (other == this || other == null) continue;
            if (other._state != FightState.Idle) continue;

            float dist = Vector3.Distance(transform.position, other.transform.position);
            if (dist >= bestDist) continue;

            best = other;
            bestDist = dist;
        }

        if (best == null)
        {
            Debug.LogWarning($"[Fight] DEBUG — {name} : aucun autre monstre libre trouvé.");
            return;
        }

        Debug.Log($"[Fight] DEBUG — {name} force une bagarre avec {best.name} (dist {bestDist:F1}m), compatibilité/cooldown ignorés.");
        BeginPairing(best, asLeader: true);
        best.BeginPairing(this, asLeader: false);
    }

    void BeginPairing(MonsterFightBehavior partner, bool asLeader)
    {
        _partner    = partner;
        _isLeader   = asLeader;
        _state      = FightState.Locked;
        _hasArrived = false;

        // Point de rendez-vous figé une seule fois ici (pas recalculé à chaque tick à partir de la
        // position mobile du partenaire) — même précaution que MonsterSocialBehavior, évite la
        // dynamique de poursuite instable déjà rencontrée côté conversation.
        Vector3 axis = transform.position - partner.transform.position;
        axis.y = 0f;
        if (axis.sqrMagnitude < 0.01f)
            axis = Quaternion.Euler(0f, asLeader ? 0f : 180f, 0f) * Vector3.forward;
        _approachAxis = axis.normalized;

        Vector3 midpoint = (transform.position + partner.transform.position) * 0.5f;
        _approachTarget = midpoint + _approachAxis * (approachArrivalDistance * 0.5f);

        _fightCoroutine = StartCoroutine(FightRoutine());
    }

    // ─── Approche → face-à-face → mêlée ───────────────────────────

    IEnumerator FightRoutine()
    {
        _state = FightState.Approaching;

        while (_partner != null &&
               Vector3.Distance(transform.position, _approachTarget) > approachTolerance)
        {
            _mover?.MoveTo(_approachTarget); // phase 1 seule — jamais MoveTo(entry, final)
            yield return new WaitForSeconds(approachRepathInterval);
        }
        if (_partner == null) yield break;

        _mover?.Stop();
        _hasArrived = true;

        yield return new WaitUntil(() => _partner == null || _partner._hasArrived);
        if (_partner == null) yield break;

        // Direction vers la position RÉELLE de l'adversaire (pas l'axe figé à BeginPairing) — même
        // raison que MonsterSocialBehavior : le chemin NavMesh n'est pas forcément une ligne droite.
        // Snap instantané, pas de rotation progressive.
        FaceDirectionInstant(_partner.transform.position - transform.position);

        _state = FightState.Fighting;
        // Un seul état partagé (pas de distinction leader/follower côté anim, contrairement à
        // Talk/Listen) — les deux se battent, simplification assumée : le clip est un placeholder
        // que l'utilisateur remplacera de toute façon par une vraie anim de bagarre plus tard.
        SetFighting(true);
        Debug.Log($"[Fight] {name} — bagarre engagée avec {_partner?.name}.");

        if (_isLeader)
        {
            // Point milieu RÉEL (positions actuelles, les deux sont arrêtés) — pas l'axe figé.
            Vector3 mid = (transform.position + _partner.transform.position) * 0.5f + Vector3.up * vfxHeightOffset;
            if (fightCloudPrefab != null)
            {
                _vfxInstance = Instantiate(fightCloudPrefab, mid, Quaternion.identity);
                _vfxInstance.transform.localScale = Vector3.one * vfxScale;
            }
            if (fightBurstPrefabs != null && fightBurstPrefabs.Length > 0)
                _burstCoroutine = StartCoroutine(BurstLoop());

            yield return new WaitForSeconds(fightDurationBeforeAutoBreak);
            if (_partner != null)
            {
                Debug.Log($"[Fight] {name} — personne n'est intervenu, séparation par timeout (pénalité sévère).");
                ApplyPenalty(satisfactionPenaltyTimeout);
                _partner.ApplyPenalty(satisfactionPenaltyTimeout);
                Interrupt(); // résolution par timeout — même chemin de sortie que le reste
            }
        }
        else
        {
            yield return new WaitUntil(() => _partner == null);
        }
    }

    /// <summary>
    /// Pioche UN effet au hasard dans fightBurstPrefabs et le joue, à intervalles aléatoires,
    /// tant que la mêlée dure — jamais plusieurs à la fois (contrairement à l'ancien comportement
    /// "tous en même temps au début"). Fire-and-forget : pas de référence gardée par instance, les
    /// prefabs CFXR se détruisent tout seuls une fois joués. Propriété du leader uniquement, comme
    /// le nuage — stoppée explicitement dans Interrupt().
    /// </summary>
    IEnumerator BurstLoop()
    {
        while (_state == FightState.Fighting && _partner != null)
        {
            yield return new WaitForSeconds(Random.Range(burstIntervalMin, burstIntervalMax));
            if (_state != FightState.Fighting || _partner == null) yield break;

            var entry = PickWeightedBurst();
            if (entry == null) continue;

            Vector3 mid = (transform.position + _partner.transform.position) * 0.5f + Vector3.up * entry.heightOffset;
            Vector3 pos = mid;
            if (entry.applyJitter)
            {
                Vector2 jitter = Random.insideUnitCircle * burstPositionJitter;
                pos += new Vector3(jitter.x, 0f, jitter.y);
            }
            var b = Instantiate(entry.prefab, pos, Quaternion.identity);
            b.transform.localScale = Vector3.one * entry.scale;
        }
    }

    /// <summary>Tirage pondéré parmi fightBurstPrefabs (voir FightBurstEntry.weight) — ignore les entrées sans prefab ou à poids nul/négatif. Null si aucune entrée valide.</summary>
    FightBurstEntry PickWeightedBurst()
    {
        float total = 0f;
        foreach (var e in fightBurstPrefabs)
            if (e != null && e.prefab != null && e.weight > 0f) total += e.weight;
        if (total <= 0f) return null;

        float roll = Random.Range(0f, total);
        float cumulative = 0f;
        foreach (var e in fightBurstPrefabs)
        {
            if (e == null || e.prefab == null || e.weight <= 0f) continue;
            cumulative += e.weight;
            if (roll <= cumulative)
            {
                Debug.Log($"[Fight] Burst tiré : {e.prefab.name} (weight {e.weight}/{total}, roll {roll:F2}).");
                return e;
            }
        }
        return null;
    }

    void FaceDirectionInstant(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    void SetFighting(bool fighting) => _animator?.SetBool(IsFightingHash, fighting);

    void ApplyPenalty(float amount) => GetComponent<SatisfactionComponent>()?.ApplyDecay(amount);

    // ─── Résolution par le joueur ─────────────────────────────────

    /// <summary>
    /// Appelé par MonsterFightBreaker quand le joueur sépare la bagarre. Pénalité plus faible que
    /// le timeout (récompense l'intervention rapide). No-op si la bagarre n'est pas (ou plus) en
    /// cours (idempotent — protège contre une résolution simultanée par timeout le même frame).
    /// </summary>
    public void ResolveByPlayer()
    {
        if (_state != FightState.Fighting) return;

        Debug.Log($"[Fight] {name} — séparé par le joueur (pénalité faible).");
        ApplyPenalty(satisfactionPenaltyPlayerResolved);
        _partner?.ApplyPenalty(satisfactionPenaltyPlayerResolved);
        Interrupt();
    }

    // ─── Interruption / libération ───────────────────────────────

    /// <summary>
    /// Interrompt une bagarre en cours ou en cours de verrouillage (besoin urgent, checkout,
    /// despawn...). Sans effet si déjà libre. N'applique jamais de pénalité elle-même — toujours
    /// posée par l'appelant avant (ResolveByPlayer, branche timeout). Propage au partenaire
    /// (référence nullée avant notification pour éviter un aller-retour infini).
    /// </summary>
    public void Interrupt()
    {
        if (_state == FightState.Idle) return;

        if (_fightCoroutine != null)
        {
            StopCoroutine(_fightCoroutine);
            _fightCoroutine = null;
        }
        if (_burstCoroutine != null)
        {
            StopCoroutine(_burstCoroutine);
            _burstCoroutine = null;
        }

        var partner = _partner;
        _partner       = null;
        _state         = FightState.Idle;
        _hasArrived    = false;
        _cooldownUntil = Time.time + cooldownAfterFight;
        SetFighting(false);
        _mover?.Stop();

        if (_vfxInstance != null) { Destroy(_vfxInstance); _vfxInstance = null; }

        if (partner != null) partner.Interrupt();
    }
}
