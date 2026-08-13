using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fait discuter deux monstres compatibles quand ils se croisent en balade. Comportement
/// coopératif au même titre que MonsterRoamBehavior/MonsterNeedSeeker (pas de state machine
/// centrale) : cède la priorité à MonsterNeedSeeker, et MonsterRoamBehavior cède la sienne à
/// celui-ci via IsBusy.
///
/// Setup : ajouté automatiquement par SpawnScheduler (avant les autres comportements — voir note
/// d'ordre dans SpawnScheduler.TrySpawn). Activé par ReservationSystem quand la chambre est
/// assignée. Pas actif pour les visiteurs repas sans chambre (cohérent avec MonsterRoamBehavior).
///
/// Une conversation menée à son terme (pas interrompue) applique un bonus de satisfaction aux deux
/// participants, puis un cooldown démarre avant qu'ils puissent en rechercher une nouvelle — évite
/// d'enchaîner en boucle avec le même partenaire.
/// </summary>
public class MonsterSocialBehavior : MonoBehaviour
{
    [Header("Recherche partenaire")]
    [Tooltip("Intervalle moyen (sec) entre deux tentatives de recherche de partenaire — un jitter aléatoire est appliqué pour désynchroniser les monstres. Valeur de départ, à ajuster.")]
    public float searchInterval = 4f;
    [Tooltip("Rayon (m) dans lequel chercher un partenaire — volontairement opportuniste : ne cherche que parmi les monstres déjà proches, ne traverse pas la carte. Valeur de départ, à ajuster.")]
    public float searchRadius = 5f;
    [Tooltip("Délai (sec) après la fin d'une conversation (normale ou interrompue) avant de pouvoir en rechercher une nouvelle — évite d'enchaîner en boucle avec le même partenaire. Valeur de départ, à ajuster.")]
    public float cooldownAfterChat = 20f;

    [Header("Approche")]
    [Tooltip("Distance (m) cible entre les deux monstres pour se faire face et parler. 1.3 donnait un chevauchement visuel une fois l'orientation corrigée, vu l'échelle ×3 des modèles de monstres — remonté à 2.5. Valeur de départ, à ajuster.")]
    public float approachArrivalDistance = 2.5f;
    [Tooltip("Marge (m) tolérée autour de la distance cible avant de considérer l'approche terminée.")]
    public float approachTolerance = 0.4f;
    [Tooltip("Fréquence (sec) de réémission de la marche vers le partenaire — celui-ci peut aussi être en mouvement, donc c'est une cible mobile.")]
    public float approachRepathInterval = 0.4f;

    [Header("Conversation")]
    [Tooltip("Durée min/max (sec) d'une conversation — tirée aléatoirement par le monstre qui a initié la recherche.")]
    public float talkDurationMin = 6f;
    public float talkDurationMax = 15f;

    [Header("Impact")]
    [Tooltip("Bonus de satisfaction appliqué aux deux monstres à la fin d'une conversation menée à son terme (pas si interrompue par un besoin urgent ou un checkout). Valeur de départ, à ajuster.")]
    public float satisfactionBonus = 5f;

    public static readonly HashSet<MonsterSocialBehavior> All = new();

    enum SocialState { Idle, Locked, Approaching, Talking }
    SocialState _state = SocialState.Idle;

    MonsterSocialBehavior _partner;
    bool _isLeader;
    bool _hasArrived;
    bool _active;
    float _cooldownUntil;
    Vector3 _approachTarget;
    Vector3 _approachAxis;

    Coroutine _searchCoroutine;
    Coroutine _conversationCoroutine;

    MonsterMover _mover;
    MonsterNeedSeeker _seeker;
    MonsterDataReference _dataRef;
    MonsterFightBehavior _fightSibling;
    Animator _animator;

    static readonly int IsTalkingHash   = Animator.StringToHash("IsTalking");
    static readonly int IsListeningHash = Animator.StringToHash("IsListening");

    /// <summary>True dès qu'une recherche de partenaire est verrouillée ou qu'une conversation est en cours — consulté par MonsterRoamBehavior.</summary>
    public bool IsBusy => _state != SocialState.Idle;

    // Start() plutôt qu'Awake() : si ce composant est ajouté directement sur le prefab (au lieu
    // d'être ajouté dynamiquement par SpawnScheduler), son Awake() se déclenche PENDANT
    // Instantiate(), avant que SpawnScheduler ait ajouté MonsterDataReference/autres composants
    // dynamiques juste après — un GetComponent en Awake() dans ce cas retournerait null. Start()
    // se déclenche après la fin de l'appel synchrone à SpawnScheduler.TrySpawn() (tous ses
    // AddComponent), donc robuste peu importe si ce composant est bake sur le prefab ou ajouté au
    // runtime.
    void Start()
    {
        _mover        = GetComponent<MonsterMover>();
        _seeker       = GetComponent<MonsterNeedSeeker>();
        _dataRef      = GetComponent<MonsterDataReference>();
        _fightSibling = GetComponent<MonsterFightBehavior>();
        _animator     = GetComponentInChildren<Animator>();
        // SatisfactionComponent n'est pas mis en cache ici : fetch paresseux dans
        // ApplySatisfactionBonus() (même raison, par cohérence — pas de risque à ce stade mais
        // évite de dépendre implicitement de l'ordre d'ajout de SpawnScheduler).

        Debug.Log($"[Social] {name} — Start(). dataRef={(_dataRef != null)}, data={_dataRef?.Data?.monsterName ?? "null"}, mover={(_mover != null)}, animator={(_animator != null)}, active(déjà appelé Activate avant Start ?)={_active}");
    }

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

    /// <summary>Appelé par ReservationSystem quand la chambre est assignée.</summary>
    public void Activate()
    {
        if (_active) return;
        _active = true;
        Debug.Log($"[Social] {name} — comportement social activé (recherche toutes les ~{searchInterval}s, rayon {searchRadius}m).");
        _searchCoroutine = StartCoroutine(SearchLoop());
    }

    void OnDestroy() => Interrupt();

    // ─── Recherche ────────────────────────────────────────────────

    IEnumerator SearchLoop()
    {
        while (_active)
        {
            yield return new WaitForSeconds(searchInterval + Random.Range(-1f, 1f));

            if (_state != SocialState.Idle) continue;
            if (Time.time < _cooldownUntil) continue;
            if (_seeker != null && _seeker.IsSeeking) continue;
            if (_fightSibling != null && _fightSibling.IsBusy) continue; // occupé à se battre

            TryFindPartner();
        }
    }

    /// <summary>
    /// Scan + verrouillage des deux côtés — doit rester 100% synchrone (aucun yield) : c'est cette
    /// synchronicité qui tient lieu de mutex et empêche un 3e monstre de s'incruster entre le scan
    /// et l'écriture des états.
    /// </summary>
    bool TryFindPartner()
    {
        var myData = _dataRef != null ? _dataRef.Data : null;
        if (myData == null) return false;

        MonsterSocialBehavior best = null;
        float bestDist = float.MaxValue;

        foreach (var other in All)
        {
            if (other == this || other == null) continue;
            if (!other._active) continue;
            if (other._state != SocialState.Idle) continue;
            if (Time.time < other._cooldownUntil) continue;
            if (other._seeker != null && other._seeker.IsSeeking) continue;
            if (other._fightSibling != null && other._fightSibling.IsBusy) continue;

            var otherData = other._dataRef != null ? other._dataRef.Data : null;
            if (otherData == null) continue;
            if (!myData.IsSociallyCompatible(otherData.monsterType)) continue;
            if (!otherData.IsSociallyCompatible(myData.monsterType)) continue;

            float dist = Vector3.Distance(transform.position, other.transform.position);
            if (dist > searchRadius || dist >= bestDist) continue;

            best = other;
            bestDist = dist;
        }

        if (best == null) return false;

        Debug.Log($"[Social] {name} ↔ {best.name} — partenaire trouvé (dist {bestDist:F1}m), début approche.");
        BeginPairing(best, asLeader: true);
        best.BeginPairing(this, asLeader: false);
        return true;
    }

    void BeginPairing(MonsterSocialBehavior partner, bool asLeader)
    {
        _partner    = partner;
        _isLeader   = asLeader;
        _state      = SocialState.Locked;
        _hasArrived = false;

        // Point de rendez-vous figé une seule fois ici (pas recalculé à chaque tick à partir de la
        // position mobile du partenaire) : viser une cible qui bouge en continu pendant que les
        // deux monstres se rapprochent simultanément créait une dynamique de poursuite instable
        // (ils tournaient l'un autour de l'autre au lieu de converger). Symétrique par construction
        // : les deux BeginPairing (self + partner) calculent le même point milieu, avec des axes
        // opposés — chacun s'arrête de son côté, à approachArrivalDistance l'un de l'autre.
        Vector3 axis = transform.position - partner.transform.position;
        axis.y = 0f;
        if (axis.sqrMagnitude < 0.01f)
            axis = Quaternion.Euler(0f, asLeader ? 0f : 180f, 0f) * Vector3.forward;
        _approachAxis = axis.normalized;

        Vector3 midpoint = (transform.position + partner.transform.position) * 0.5f;
        _approachTarget = midpoint + _approachAxis * (approachArrivalDistance * 0.5f);

        _conversationCoroutine = StartCoroutine(ConversationRoutine());
    }

    // ─── Approche → face-à-face → parole ─────────────────────────

    IEnumerator ConversationRoutine()
    {
        _state = SocialState.Approaching;

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

        // Direction vers la position RÉELLE du partenaire (pas l'axe figé à BeginPairing) : le
        // chemin NavMesh emprunté pour rejoindre le point de rendez-vous n'est pas forcément une
        // ligne droite (contournement d'obstacles), donc la position finale réelle peut dévier de
        // l'axe calculé avant tout déplacement. Sans risque de valeur obsolète ici : on vient de
        // confirmer via le WaitUntil ci-dessus que le partenaire est arrivé (donc immobile, Stop()
        // déjà appelé de son côté). Snap instantané (pas de rotation progressive sur plusieurs
        // frames) — supprime tout risque que la coroutine de rotation ne se termine pas comme prévu.
        FaceDirectionInstant(_partner.transform.position - transform.position);
        Debug.Log($"[Social] {name} — orienté vers {_partner?.name} (euler Y = {transform.eulerAngles.y:F0}°).");

        // Léger décalage avant de déclencher l'anim — les deux monstres ne démarrent plus leur clip
        // exactement à la même frame.
        yield return new WaitForSeconds(Random.Range(0f, 0.4f));
        if (_partner == null) yield break;

        _state = SocialState.Talking;
        // Rôle différent selon leader/follower — même clip synchronisé sur les deux donnait
        // l'impression qu'ils "font exactement pareil" (effet miroir). Le leader parle, le
        // follower écoute/hoche la tête : deux clips différents, plus aucune synchronie visible.
        if (_isLeader) SetTalking(true);
        else           SetListening(true);
        Debug.Log($"[Social] {name} — {(_isLeader ? "parle" : "écoute")} face à {_partner?.name}.");

        if (_isLeader)
        {
            float duration = Random.Range(talkDurationMin, talkDurationMax);
            yield return new WaitForSeconds(duration);
            if (_partner != null)
            {
                // Bonus uniquement en fin normale (conversation menée à son terme) — pas si
                // Interrupt() est appelé de l'extérieur (besoin urgent, checkout).
                ApplySatisfactionBonus();
                _partner.ApplySatisfactionBonus();
                Interrupt(); // fin normale = même chemin que l'interruption forcée
            }
        }
        else
        {
            yield return new WaitUntil(() => _partner == null);
        }
    }

    void FaceDirectionInstant(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    void SetTalking(bool talking)     => _animator?.SetBool(IsTalkingHash, talking);
    void SetListening(bool listening) => _animator?.SetBool(IsListeningHash, listening);

    void ApplySatisfactionBonus() => GetComponent<SatisfactionComponent>()?.ApplyBonus(satisfactionBonus);

    // ─── Interruption / libération ───────────────────────────────

    /// <summary>
    /// Interrompt une conversation en cours ou en cours de verrouillage (besoin urgent, checkout,
    /// despawn...). Sans effet si déjà libre. Propage au partenaire (référence nullée avant
    /// notification pour éviter un aller-retour infini).
    /// </summary>
    public void Interrupt()
    {
        if (_state == SocialState.Idle) return;

        Debug.Log($"[Social] {name} — conversation terminée (état: {_state}), cooldown {cooldownAfterChat}s.");

        if (_conversationCoroutine != null)
        {
            StopCoroutine(_conversationCoroutine);
            _conversationCoroutine = null;
        }

        var partner = _partner;
        _partner       = null;
        _state         = SocialState.Idle;
        _hasArrived    = false;
        _cooldownUntil = Time.time + cooldownAfterChat;
        SetTalking(false);
        SetListening(false);
        _mover?.Stop();

        if (partner != null) partner.Interrupt();
    }
}
