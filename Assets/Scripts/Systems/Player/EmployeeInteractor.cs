using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Composant joueur : détecte les employés proches et ouvre la roue d'interaction sur Interact.
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class EmployeeInteractor : MonoBehaviour
{
    [Tooltip("Distance d'interaction avec un employé")]
    public float interactRange = 2f;

    PlayerInput _input;

    void Awake() => _input = GetComponent<PlayerInput>();

    void Update()
    {
        if (!_input.WasPressed("Interact")) return;

        var nearest = FindNearestEmployee();
        if (nearest == null) return;

        var wheel = nearest.GetComponentInChildren<EmployeeInteractionWheel>(true);
        wheel?.Toggle();
    }

    EmployeeInstance FindNearestEmployee()
    {
        EmployeeInstance best  = null;
        float            bestD = interactRange;

        foreach (var emp in EmployeeInstance.All)
        {
            if (emp == null) continue;
            float d = Vector3.Distance(transform.position, emp.transform.position);
            if (d < bestD) { bestD = d; best = emp; }
        }
        return best;
    }
}
