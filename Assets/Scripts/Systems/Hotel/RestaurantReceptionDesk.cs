using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marque un GameObject comme comptoir d'accueil du restaurant.
/// Même rôle que ReceptionDesk (hôtel), mais pour la file d'attente du restaurant.
///
/// Conçu pour vivre sur un prefab de MEUBLE (FurnitureData.prefab), posable dans la cuisine
/// comme n'importe quel autre meuble (frigo, plan de travail...) via FurniturePlacer — pas un
/// GameObject à câbler manuellement dans la scène. Le meuble emporte avec lui son propre
/// ReceptionQueueManager (sibling component) + un enfant "QueueStart" pour orienter la file.
/// </summary>
public class RestaurantReceptionDesk : MonoBehaviour
{
    public static readonly HashSet<RestaurantReceptionDesk> All = new();

    [Tooltip("File d'attente de ce comptoir — laissé vide, récupéré automatiquement sur ce GameObject au Awake")]
    public ReceptionQueueManager queueManager;

    void Awake()
    {
        if (queueManager == null) queueManager = GetComponent<ReceptionQueueManager>();
        All.Add(this);
    }

    // S'abonne en Start (pas Awake) pour garantir que RestaurantReservationSystem.Instance est déjà
    // initialisé (son propre Awake), que ce comptoir soit pré-placé en scène ou posé en jeu plus tard.
    void Start()
    {
        if (queueManager != null && RestaurantReservationSystem.Instance != null)
            queueManager.OnSlotIndexChanged += RestaurantReservationSystem.Instance.HandleSlotIndexChanged;
    }

    void OnDestroy()
    {
        All.Remove(this);
        if (queueManager != null && RestaurantReservationSystem.Instance != null)
            queueManager.OnSlotIndexChanged -= RestaurantReservationSystem.Instance.HandleSlotIndexChanged;
    }

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
