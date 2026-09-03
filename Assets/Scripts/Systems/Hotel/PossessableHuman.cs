using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Humain invoqué par le joueur (voir Cage, G8) — reste immobile là où il apparaît jusqu'à ce qu'un
/// fantôme le possède (GhostPossessionBehavior.FindNearestAvailableHuman). Une fois possédé, devient
/// administrativement le fantôme aux yeux de tous les systèmes existants (MonsterDataReference.Data =
/// ghostData) et active les comportements standards d'un résident — sans jamais passer par
/// ReservationSystem/RoomInstance (l'humain possédé n'a jamais de chambre).
///
/// Le fantôme peut aussi quitter le corps avant l'heure (Unpossess(panicked: true), voir
/// PossessedHumanQuirks) : l'humain panique alors (IsPanicking) jusqu'à ce que le joueur l'attrape
/// (Catch) et le ramène dans une cage (PlaceInCage) — voir PanickedHumanCatcher.
/// </summary>
public class PossessableHuman : MonoBehaviour
{
    [Header("Panique (fuite anticipée du fantôme)")]
    [Tooltip("Intervalle entre deux waypoints pendant la panique — plus court que le roam normal pour un mouvement erratique. Valeur de départ — à ajuster.")]
    public float panicWaypointInterval = 1.5f;
    [Tooltip("Vitesse de déplacement pendant la panique. Valeur de départ — à ajuster.")]
    public float panicMoveSpeed = 6f;
    [Tooltip("Distance devant le joueur porteur à laquelle l'humain porté est maintenu.")]
    public float carryOffsetDistance = 1f;
    [Tooltip("Décalage vertical de l'humain porté (évite qu'il clippe dans le sol selon le pivot du modèle).")]
    public float carryOffsetHeight = 0f;

    [Header("Possession")]
    [Tooltip("Garde-fou (secondes) : l'humain reste immobile jusqu'à la fin réelle de l'anim Possess (détectée automatiquement via l'Animator, état nommé \"Possess\"), plafonnée à cette durée pour ne jamais rester bloqué si l'état n'est pas encore configuré côté Editor. Pas besoin de la régler précisément sur la durée du clip.")]
    public float possessionAnimDuration = 3f;

    public static readonly List<PossessableHuman> All = new();

    public bool IsPossessed   { get; private set; }
    /// <summary>True depuis une fuite anticipée du fantôme jusqu'à ce que le joueur le ramène dans une
    /// cage (PlaceInCage) — couvre à la fois la course erratique libre et la phase où il est porté
    /// (IsBeingCarried). GhostPossessionBehavior exclut ces humains de la recherche de cible.</summary>
    public bool IsPanicking   { get; private set; }
    /// <summary>True le temps que le joueur porte l'humain attrapé jusqu'à une cage.</summary>
    public bool IsBeingCarried { get; private set; }

    MonsterMover _mover;
    Animator     _animator;
    Transform    _carrier;

    void Awake()
    {
        _mover    = GetComponent<MonsterMover>() ?? gameObject.AddComponent<MonsterMover>();
        _animator = GetComponentInChildren<Animator>();
        _animator?.SetTrigger("Appear");
        All.Add(this);
    }

    void OnDestroy() => All.Remove(this);

    void Update()
    {
        if (!IsBeingCarried || _carrier == null) return;
        transform.position = _carrier.position + _carrier.forward * carryOffsetDistance + Vector3.up * carryOffsetHeight;
    }

    /// <summary>Appelé par GhostPossessionBehavior à l'arrivée du fantôme.</summary>
    public void Possess(MonsterData ghostData)
    {
        if (IsPossessed || ghostData == null) return;
        IsPossessed = true;
        All.Remove(this);

        _animator?.SetTrigger("Possess"); // moment de la possession elle-même — distinct d'Appear (spawn de l'humain)

        var dataRef = GetComponent<MonsterDataReference>() ?? gameObject.AddComponent<MonsterDataReference>();
        dataRef.Data = ghostData;

        _mover.moveSpeed    = ghostData.moveSpeed;
        _mover.ignoresWalls = false; // l'humain reste soumis aux murs, contrairement au fantôme qui l'habitait

        // Même ordre d'ajout que SpawnScheduler.SpawnMonsterObject() : Social/Fight avant
        // Needs/Seeker/Roam (d'autres composants les cherchent via GetComponent() dans leur Awake()).
        if (GetComponent<MonsterSocialBehavior>() == null) gameObject.AddComponent<MonsterSocialBehavior>();
        if (GetComponent<MonsterFightBehavior>()  == null) gameObject.AddComponent<MonsterFightBehavior>();

        var needs = GetComponent<MonsterNeedsComponent>() ?? gameObject.AddComponent<MonsterNeedsComponent>();
        needs.Initialize(ghostData);
        if (GetComponent<SatisfactionComponent>() == null) gameObject.AddComponent<SatisfactionComponent>();
        var seeker = GetComponent<MonsterNeedSeeker>()   ?? gameObject.AddComponent<MonsterNeedSeeker>();
        var roam   = GetComponent<MonsterRoamBehavior>() ?? gameObject.AddComponent<MonsterRoamBehavior>();

        // Activation différée : le corps reste immobile le temps que l'anim Possess se joue
        // entièrement, plutôt que de partir marcher pendant que la transformation est encore visible.
        StartCoroutine(ActivateResidentBehaviorsAfterPossessionAnim(needs, seeker, roam));

        gameObject.AddComponent<PossessedHumanQuirks>().Init(ghostData);
    }

    IEnumerator ActivateResidentBehaviorsAfterPossessionAnim(MonsterNeedsComponent needs, MonsterNeedSeeker seeker, MonsterRoamBehavior roam)
    {
        if (_animator != null)
        {
            float elapsed = 0f;
            const string idleState = "AS_Possession_Idle";

            // Le Trigger prend effet au prochain Update de l'Animator, pas immédiatement — attend que
            // l'Animator ait bien quitté l'idle. possessionAnimDuration sert uniquement de garde-fou
            // (setup Editor pas fini) pour ne jamais rester bloqué indéfiniment.
            while (_animator.GetCurrentAnimatorStateInfo(0).IsName(idleState) && elapsed < possessionAnimDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Puis attend que l'Animator soit REVENU à l'idle — couvre toute la chaîne construite dans
            // l'Editor (Possess → anim(s) intermédiaire(s) → Idle), quel que soit le nombre d'étapes,
            // sans jamais avoir à toucher ce script si la chaîne change.
            while (!_animator.GetCurrentAnimatorStateInfo(0).IsName(idleState) && elapsed < possessionAnimDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(possessionAnimDuration);
        }

        // Même séquence d'activation que ReservationSystem au check-in classique.
        needs.ActivateDecay();
        seeker.Activate();
        roam.Activate();
        GetComponent<MonsterSocialBehavior>()?.Activate();
        GetComponent<MonsterFightBehavior>()?.Activate();
    }

    /// <summary>
    /// Appelé par PossessedHumanQuirks au départ du fantôme — checkout normal (panicked: false) ou
    /// fuite anticipée "fatiguée" (panicked: true). Retire dans tous les cas ce que Possess() avait mis
    /// en place. Checkout normal : redevient repossédable, immobile. Fuite anticipée : garde
    /// MonsterRoamBehavior actif (resserré pour un mouvement erratique, réutilise la balade en continu
    /// déjà en place pour un roamer sans chambre) et passe en panique — non repossédable tant que le
    /// joueur ne l'a pas ramené dans une cage (voir Catch/PlaceInCage).
    /// </summary>
    public void Unpossess(bool panicked = false)
    {
        if (!IsPossessed) return;
        IsPossessed = false;
        IsPanicking = panicked;
        All.Add(this);

        _mover.Stop();

        if (panicked)
        {
            var roam = GetComponent<MonsterRoamBehavior>();
            if (roam != null) roam.waypointInterval = panicWaypointInterval;
            _mover.moveSpeed = panicMoveSpeed;
        }
        else
        {
            Destroy(GetComponent<MonsterRoamBehavior>());
        }

        Destroy(GetComponent<PossessedHumanQuirks>());
        Destroy(GetComponent<MonsterNeedSeeker>());
        Destroy(GetComponent<MonsterSocialBehavior>());
        Destroy(GetComponent<MonsterFightBehavior>());
        Destroy(GetComponent<MonsterNeedsComponent>());
        Destroy(GetComponent<SatisfactionComponent>());
        Destroy(GetComponent<MonsterDataReference>());
    }

    /// <summary>Appelé par PanickedHumanCatcher quand le joueur attrape un humain en pleine panique.</summary>
    public void Catch(Transform carrier)
    {
        if (!IsPanicking || IsBeingCarried || carrier == null) return;
        IsBeingCarried = true;
        _carrier = carrier;
        Destroy(GetComponent<MonsterRoamBehavior>()); // stoppe la course erratique — position reprise par Update() ci-dessus
        _mover.Stop();
    }

    /// <summary>Appelé par PanickedHumanCatcher quand le joueur dépose l'humain porté dans une cage —
    /// redevient un humain standard disponible, comme un humain fraîchement invoqué.</summary>
    public void PlaceInCage(Cage cage)
    {
        if (!IsBeingCarried || cage == null) return;
        IsBeingCarried = false;
        IsPanicking    = false;
        _carrier       = null;

        transform.position = cage.SpawnPosition;
        _mover.Stop();
    }
}
