using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marque un GameObject comme comptoir d'accueil.
/// L'interaction joueur est gérée par ReceptionInteractor (composant sur le joueur).
/// L'employé réceptionniste utilise ce composant pour localiser le comptoir (ReceptionDesk.All).
/// </summary>
public class ReceptionDesk : MonoBehaviour
{
    public static readonly HashSet<ReceptionDesk> All = new();

    // OnEnable (pas Awake) : s'enregistre aussi si le GameObject était inactif au chargement de la
    // scène et activé plus tard — Awake ne se déclenche jamais sur un objet resté inactif depuis le
    // début, ce qui aurait laissé ReceptionDesk.All vide silencieusement dans ce cas.
    void OnEnable() => All.Add(this);

    void OnDisable() => All.Remove(this);

    /// <summary>
    /// Point où marcher pour interagir avec ce comptoir — utilise un enfant nommé "StandPoint" s'il
    /// existe (à positionner à la main dans l'éditeur, au sol devant le comptoir), sinon retombe sur
    /// le pivot du comptoir lui-même (comportement historique). Cible le pivot directement pouvait
    /// amener les employés/monstres à viser un point en hauteur ou au milieu du mesh du comptoir
    /// plutôt qu'une position au sol atteignable.
    /// </summary>
    public Vector3 StandPoint
    {
        get
        {
            var t = transform.Find("StandPoint");
            return t != null ? t.position : transform.position;
        }
    }
}
