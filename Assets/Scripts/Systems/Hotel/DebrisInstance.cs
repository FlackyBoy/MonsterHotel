using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Détritus laissé au sol par un monstre après son départ.
/// Ramassable par le joueur sans item spécifique (action Interact).
/// Notifie sa chambre d'origine quand détruit pour mettre à jour l'état de propreté.
/// </summary>
public class DebrisInstance : MonoBehaviour
{
    public static readonly HashSet<DebrisInstance> All = new();

    void Awake()     { All.Add(this); }
    /// <summary>Déclenché quand ce détritus est retiré (ramassé ou détruit par autre moyen).</summary>
    public event System.Action OnRemoved;

    void OnDestroy() { All.Remove(this); Room?.RemoveDebris(this); OnRemoved?.Invoke(); }

    [Tooltip("Distance d'interaction joueur")]
    public float  pickupRange        = 1.5f;
    public string interactActionName = "Interact";

    public RoomInstance Room { get; private set; }

    /// <summary>True tant qu'un employé de nettoyage a réservé ce détritus — empêche un doublon.</summary>
    public bool IsReserved { get; private set; }

    public void Reserve()           => IsReserved = true;
    public void ReleaseReservation() => IsReserved = false;

    public void Init(RoomInstance room) => Room = room;

    void Update()
    {
        foreach (var placer in RoomPlacer.All)
        {
            if (placer == null) continue;
            if (HorizontalDistance(transform.position, placer.transform.position) > pickupRange) continue;

            var pi = placer.GetComponent<PlayerInput>();
            if (pi == null) continue;

            var action = pi.actions.FindAction(interactActionName, throwIfNotFound: false);
            if (action == null || !action.WasPressedThisFrame()) continue;

            PickUp();
            return;
        }
    }

    /// <summary>Ramasse et détruit ce détritus (joueur ou employé).</summary>
    public void PickUp()
    {
        Room?.RemoveDebris(this);
        Destroy(gameObject);
    }

    /// <summary>
    /// Distance horizontale (XZ) uniquement — un débris posé sur une table (ex: après un repas,
    /// voir EatingSpot.SpawnMealDebris) est en hauteur par rapport au joueur au sol ; comparer la
    /// distance 3D complète empêchait de le ramasser même collé à la table (même bug déjà rencontré
    /// et corrigé côté CleaningEmployeeAI.HorizontalDistance, jamais reporté ici côté joueur).
    /// </summary>
    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
