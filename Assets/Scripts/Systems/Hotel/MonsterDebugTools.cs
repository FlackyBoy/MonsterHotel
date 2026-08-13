using UnityEngine;

/// <summary>
/// Outils de debug pour un monstre en jeu.
/// Ajouté automatiquement à chaque spawn (SpawnScheduler).
/// En Play Mode : sélectionner le monstre dans la hiérarchie, clic droit sur ce composant
/// dans l'Inspector → "Debug : Forcer le départ" / "Debug : Terminer le séjour".
/// </summary>
public class MonsterDebugTools : MonoBehaviour
{
    [ContextMenu("Debug : Forcer le départ")]
    public void ForceLeave()
    {
        // Vérifie d'abord la file du restaurant (libère son slot proprement) avant de
        // retomber sur la logique hôtel (file/chambre/sortie directe).
        if (RestaurantReservationSystem.Instance != null &&
            RestaurantReservationSystem.Instance.DebugForceLeave(gameObject))
            return;

        ReservationSystem.Instance?.DebugForceLeave(gameObject);
    }

    /// <summary>
    /// Termine immédiatement le séjour d'un client chambre, comme si sa durée était naturellement
    /// écoulée (check-out normal, pourboire selon satisfaction) — contrairement à "Forcer le
    /// départ" qui traite ça comme un départ en colère (aucun pourboire). Sans effet si ce
    /// monstre n'occupe pas de chambre (visiteur, en file d'attente...).
    /// </summary>
    [ContextMenu("Debug : Terminer le séjour")]
    public void FinishStay()
    {
        bool handled = ReservationSystem.Instance != null && ReservationSystem.Instance.DebugFinishStay(gameObject);
        if (!handled)
            Debug.Log($"[Debug] {name} n'occupe pas de chambre — rien à terminer (utilise \"Forcer le départ\" pour les autres cas).");
    }
}
