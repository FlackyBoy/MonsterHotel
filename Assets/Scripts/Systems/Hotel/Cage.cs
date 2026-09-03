using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cage à humains (G8) — remplace HumanSummonTrigger : sert à la fois de point d'invocation d'un
/// nouvel humain ET de destination où le joueur ramène un humain attrapé après une fuite anticipée du
/// fantôme (voir PossessableHuman.Catch/PlaceInCage). Volontairement passif — toute la logique
/// d'interaction (distance, bouton Interact, quelle action proposer) vit côté joueur dans
/// PanickedHumanCatcher, pour éviter toute ambiguïté entre "invoquer" et "déposer" sur une même
/// pression d'Interact.
///
/// Placement : actuellement un objet fixe posé en scène (comme ReceptionDesk) — pas encore de mode
/// placement au curseur façon DecorationPlacer/RoomPlacer (annoncé par l'utilisateur comme souhaité à
/// terme, mais le comportement exact restait "à préciser" ; ce MVP fonctionnel n'attend pas cette
/// décision, l'upgrade vers un placement joueur est une extension possible plus tard).
/// </summary>
public class Cage : MonoBehaviour
{
    [Tooltip("Prefab de l'humain invoqué (doit porter PossessableHuman)")]
    public GameObject humanPrefab;

    [Tooltip("Point d'apparition/de dépôt des humains — laisser vide pour utiliser la position de cette cage")]
    public Transform spawnPoint;

    [Tooltip("Coût en or de l'invocation d'un nouvel humain (0 = gratuit)")]
    public int cost = 0;

    public static readonly List<Cage> All = new();

    public Vector3 SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);
}
