using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Comportement du fantôme APRÈS son passage à la réception (G8) — le fantôme rejoint la file
/// normale comme n'importe quel client (ReservationSystem.RegisterArrival), mais
/// ReservationSystem.CheckInGhost() ne lui assigne jamais de chambre : c'est ce composant qu'elle
/// active à la place, une fois le fantôme accueilli. Se balade (traverse les murs, voir
/// MonsterMover.ignoresWalls) à la recherche d'un humain disponible (PossessableHuman.All) à
/// posséder. Une fois la possession réussie, ce GameObject est détruit — tout se poursuit sur
/// l'humain possédé (voir PossessableHuman.Possess()).
/// </summary>
[RequireComponent(typeof(MonsterMover))]
public class GhostPossessionBehavior : MonoBehaviour
{
    [Tooltip("Distance à laquelle le fantôme considère avoir atteint l'humain ciblé")]
    public float possessRange = 1f;
    [Tooltip("Intervalle entre deux recherches/recalages de destination")]
    public float searchInterval = 1f;

    public static readonly List<GhostPossessionBehavior> All = new();

    MonsterMover         _mover;
    MonsterDataReference _dataRef;
    PossessableHuman     _target;
    float                _searchTimer;
    bool                 _active;

    void Awake()
    {
        _mover   = GetComponent<MonsterMover>();
        _dataRef = GetComponent<MonsterDataReference>();
        All.Add(this);
    }

    void OnDestroy() => All.Remove(this);

    /// <summary>
    /// Démarre la recherche — appelé par ReservationSystem.CheckInGhost() une fois le fantôme accueilli
    /// à la réception. Garde _active indispensable : contrairement aux autres comportements
    /// (Roam/Social/Fight/Seeker), ce composant peut être pré-attaché sur le prefab du fantôme — sans
    /// cette garde, Update() chasserait un humain dès l'instanciation, avant même le passage par la
    /// réception (bug observé : le fantôme n'attendait jamais).
    /// </summary>
    public void Activate()
    {
        if (_active) return;
        // Rafraîchit — pas seulement Awake() : si ce composant est pré-attaché sur le prefab, son
        // Awake() s'exécute pendant Instantiate(), avant que SpawnScheduler.SpawnMonsterObject()
        // n'ajoute dynamiquement MonsterDataReference juste après. Sans ce rafraîchissement, _dataRef
        // restait null à vie → ghostData toujours null dans Possess() → échec silencieux systématique
        // (bug confirmé en jeu : Possess() appelé mais avec ghostData=null).
        _mover   = GetComponent<MonsterMover>();
        _dataRef = GetComponent<MonsterDataReference>();
        _active = true;
        GetComponent<MonsterRoamBehavior>()?.Activate();
    }

    void Update()
    {
        if (!_active) return;

        _searchTimer -= Time.deltaTime;
        bool shouldRefresh = _searchTimer <= 0f;
        if (shouldRefresh) _searchTimer = searchInterval;

        if (_target == null || _target.IsPossessed)
        {
            if (_target != null)
            {
                // La cible vient d'être possédée par un autre fantôme entre-temps — stoppe
                // immédiatement plutôt que d'attendre le prochain searchInterval, sans quoi ce
                // fantôme continue de marcher jusqu'à la position exacte d'un humain déjà occupé
                // (donne l'impression qu'il "entre" dans un humain déjà possédé).
                _target = null;
                _mover.Stop();
            }
            if (!shouldRefresh) return;
            _target = FindNearestAvailableHuman();
            if (_target != null) _mover.MoveTo(_target.transform.position);
            return;
        }

        // Distance horizontale seulement (ignore Y) — si le fantôme et l'humain n'ont pas exactement
        // le même pivot en hauteur (ex : fantôme qui flotte), une distance 3D pouvait rester
        // supérieure à possessRange indéfiniment malgré un recouvrement visuel parfait vu du dessus
        // (jeu top-down), empêchant Possess() de se déclencher sans jamais rien logger.
        Vector3 diff = transform.position - _target.transform.position;
        diff.y = 0f;
        float dist = diff.magnitude;

        if (dist <= possessRange)
        {
            _target.Possess(_dataRef != null ? _dataRef.Data : null);
            Destroy(gameObject);
            return;
        }

        // Recale la destination périodiquement au cas où l'humain continue de se balader.
        if (shouldRefresh) _mover.MoveTo(_target.transform.position);
    }

    PossessableHuman FindNearestAvailableHuman()
    {
        PossessableHuman best = null;
        float bestDist = float.MaxValue;
        foreach (var human in PossessableHuman.All)
        {
            // IsPanicking couvre à la fois la course erratique libre et la phase où il est porté par
            // le joueur (IsBeingCarried) — non repossédable dans les deux cas, voir PossessableHuman.
            if (human == null || human.IsPossessed || human.IsPanicking) continue;
            float d = Vector3.Distance(transform.position, human.transform.position);
            if (d < bestDist) { bestDist = d; best = human; }
        }
        return best;
    }
}
