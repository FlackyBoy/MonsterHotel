using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Comportement de flânerie des monstres entre leur chambre et les zones communes.
/// Actif uniquement après assignation d'une chambre. Cède la priorité au MonsterNeedSeeker et au
/// MonsterSocialBehavior (conversation en cours).
///
/// Setup : ajouté automatiquement par SpawnScheduler sur chaque monstre.
/// Activé par ReservationSystem quand la chambre est assignée.
/// </summary>
public class MonsterRoamBehavior : MonoBehaviour
{
    [Header("Roam (valeurs par défaut — écrasées par HotelConfig si présent)")]
    [Tooltip("Temps minimum (sec) à rester en chambre avant de sortir")]
    public float minWaitInRoom       = 15f;
    [Tooltip("Temps maximum (sec) à rester en chambre avant de sortir")]
    public float maxWaitInRoom       = 40f;
    [Tooltip("Durée totale de la balade (sec) avant de rentrer en chambre")]
    public float roamDuration        = 20f;
    [Tooltip("Intervalle (sec) entre chaque nouveau waypoint pendant la balade")]
    public float waypointInterval    =  6f;
    [Tooltip("Temps d'attente approximatif pour rentrer en chambre (marge pour le trajet retour)")]
    public float returnWaitTime      =  8f;

    // ─── Privé ────────────────────────────────────────────────────

    MonsterMover        _mover;
    MonsterNeedSeeker   _seeker;
    MonsterSocialBehavior _social;
    MonsterFightBehavior _fight;
    GuestRoomReference  _roomRef;
    MonsterDataReference _dataRef;
    bool _active;

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake()
    {
        _mover   = GetComponent<MonsterMover>();
        _seeker  = GetComponent<MonsterNeedSeeker>();
        _social  = GetComponent<MonsterSocialBehavior>();
        _fight   = GetComponent<MonsterFightBehavior>();
        _roomRef = GetComponent<GuestRoomReference>();
        _dataRef = GetComponent<MonsterDataReference>();
    }

    /// <summary>Cède la priorité au NeedSeeker (besoin en cours), au comportement social (conversation en cours) et au comportement de bagarre (mêlée en cours).</summary>
    bool IsBlocked => (_seeker != null && _seeker.IsSeeking) || (_social != null && _social.IsBusy) || (_fight != null && _fight.IsBusy);

    /// <summary>Appelé par ReservationSystem quand la chambre est assignée.</summary>
    public void Activate()
    {
        if (_active) return; // évite un 2e RoamLoop en parallèle (ex : conversion visiteur→client)
        ApplyConfig();
        _roomRef = GetComponent<GuestRoomReference>();
        _active  = true;
        StartCoroutine(RoamLoop());
    }

    void OnDestroy() => _active = false;

    // ─── Boucle principale ────────────────────────────────────────

    IEnumerator RoamLoop()
    {
        while (_active)
        {
            // Attente en chambre avant de sortir
            float wait = Random.Range(minWaitInRoom, maxWaitInRoom);
            yield return new WaitForSeconds(wait);

            // Ne sort pas si un besoin est en cours de satisfaction ou une conversation en cours
            if (IsBlocked) continue;

            // ── Phase de roam ────────────────────────────────────
            float roamEnd = Time.time + roamDuration;
            while (Time.time < roamEnd)
            {
                // Cède la priorité au NeedSeeker / comportement social
                if (IsBlocked)
                {
                    yield return new WaitForSeconds(waypointInterval);
                    continue;
                }

                var pt = GetRandomWalkablePoint();
                if (pt.HasValue && _mover != null)
                    _mover.MoveTo(pt.Value);

                TrySpawnLitter();

                yield return new WaitForSeconds(waypointInterval);
            }

            // ── Retour en chambre ────────────────────────────────
            if (!IsBlocked)
            {
                var room = _roomRef?.Room;
                if (room != null && _mover != null)
                    _mover.MoveTo(room.EntryPoint, room.transform.position);

                yield return new WaitForSeconds(returnWaitTime);
            }
        }
    }

    // ─── Détritus en balade ───────────────────────────────────────

    /// <summary>
    /// Tirage indépendant à chaque étape de balade (voir MonsterData.roamLitterChance) : le monstre
    /// peut laisser un détritus au sol là où il se trouve, pas seulement en chambre/à table (G5).
    /// Détritus non rattaché à une chambre (Init(null)) — ramassable par le joueur ou un nettoyeur
    /// via DebrisInstance.All, même pattern que EatingSpot.SpawnMealDebris.
    /// </summary>
    void TrySpawnLitter()
    {
        var data = _dataRef?.Data;
        if (data == null || data.litterPrefabs == null || data.litterPrefabs.Length == 0) return;
        if (Random.value >= data.roamLitterChance) return;

        var prefab = data.litterPrefabs[Random.Range(0, data.litterPrefabs.Length)];
        if (prefab == null) return;

        var pos = transform.position + new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f));
        var go  = Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

        var debris = go.GetComponent<DebrisInstance>() ?? go.AddComponent<DebrisInstance>();
        debris.Init(null);
    }

    // ─── Helpers ──────────────────────────────────────────────────

    void ApplyConfig()
    {
        var cfg = HotelConfig.Roam;
        if (cfg == null) return;

        minWaitInRoom    = cfg.roamMinWait;
        maxWaitInRoom    = cfg.roamMaxWait;
        roamDuration     = cfg.roamDuration;
        waypointInterval = cfg.roamWaypointInterval;
        returnWaitTime   = cfg.roamReturnWait;
    }

    /// <summary>
    /// Choisit un point walkable aléatoire dans la grille.
    /// Accepte les cellules Empty, Floor et Room (hors murs et blocs).
    /// Vérifie qu'un point NavMesh existe à proximité.
    /// </summary>
    Vector3? GetRandomWalkablePoint()
    {
        var grid = GridManager.Instance;
        if (grid == null) return null;

        // Collecte les cellules dans les bornes de la grille, hors murs et blocs intacts
        var candidates = new List<Vector3>(64);
        for (int x = 0; x < grid.width; x++)
        {
            for (int y = 0; y < grid.height; y++)
            {
                var cell = grid.GetCell(x, y);
                if (cell == null)                      continue;
                if (cell.type == CellType.Wall)        continue;
                if (cell.IsOccupied)                   continue; // bloc intact ou déco
                candidates.Add(grid.CellToWorld(x, y));
            }
        }

        if (candidates.Count == 0) return null;

        // Essaie plusieurs candidats pour trouver un point NavMesh valide
        for (int attempt = 0; attempt < 10; attempt++)
        {
            var pos = candidates[Random.Range(0, candidates.Count)];
            if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                return hit.position;
        }

        return null;
    }
}
