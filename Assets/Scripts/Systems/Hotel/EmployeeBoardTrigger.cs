using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Tableau RH interactif : le joueur s'approche et appuie sur Interact pour ouvrir le panneau d'embauche.
///
/// SETUP :
///   1. Créer un GO (ex : "EmployeeBoard") dans la scène (à la réception, salle des employés…)
///   2. Ajouter ce composant
///   3. Assigner le(s) EmployeeHiringPanel dans panels[] — ou laisser vide : auto-découverte au Start()
///   4. Le GO n'a pas besoin de Collider — la détection est par distance
/// </summary>
public class EmployeeBoardTrigger : MonoBehaviour
{
    [Tooltip("Distance d'interaction (unités monde)")]
    public float interactRange = 2.5f;

    [Tooltip("Un panneau par joueur (index 0 = P1, index 1 = P2). Laisser vide pour auto-découverte.")]
    public EmployeeHiringPanel[] panels;

    [Tooltip("Action Input à détecter")]
    public string interactActionName = "Interact";

    // ─── Lifecycle ────────────────────────────────────────────────

    void Start()
    {
        if (panels == null || panels.Length == 0)
        {
            panels = FindObjectsByType<EmployeeHiringPanel>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None);
        }
    }

    void Update()
    {
        foreach (var placer in RoomPlacer.All)
        {
            if (placer == null) continue;

            float dist = Vector3.Distance(transform.position, placer.transform.position);
            if (dist > interactRange) continue;

            var pi = placer.GetComponent<PlayerInput>();
            if (pi == null) continue;

            var action = pi.actions.FindAction(interactActionName, throwIfNotFound: false);
            if (action == null || !action.WasPressedThisFrame()) continue;

            int playerIdx = pi.playerIndex;
            TogglePanel(playerIdx, pi);
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────

    void TogglePanel(int playerIndex, PlayerInput pi)
    {
        if (panels == null || panels.Length == 0) return;

        // Un panel par joueur si disponible, sinon le panel 0 pour tous
        var panel = playerIndex < panels.Length ? panels[playerIndex] : panels[0];
        if (panel == null) return;

        if (panel.gameObject.activeSelf) panel.Hide();
        else                             panel.Show(pi);
    }
}
