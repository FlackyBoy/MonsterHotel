using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PhysicsCharacterController.CharacterManager))]
public class PlayerInputBinder : MonoBehaviour
{
    PlayerInput pi;
    PhysicsCharacterController.CharacterManager cm;
    InputAction move, jump, sprint, crouch;

    void Awake()
    {
        pi = GetComponent<PlayerInput>();
        cm = GetComponent<PhysicsCharacterController.CharacterManager>();
    }

    void OnEnable()
    {
        var a = pi.actions;
        move = a.FindAction("Movement", throwIfNotFound: false);
        jump = a.FindAction("Jump", throwIfNotFound: false);
        sprint = a.FindAction("Sprint", throwIfNotFound: false);
        crouch = a.FindAction("Crouch", throwIfNotFound: false);

        if (move == null || jump == null || sprint == null || crouch == null)
        { Debug.LogError("Actions manquantes (Movement/Jump/Sprint/Crouch)."); return; }

        move.Enable(); jump.Enable(); sprint.Enable(); crouch.Enable();

        move.performed += cm.Input_Movement;
        move.canceled += cm.Input_Movement;

        jump.performed += cm.Input_Jump;
        jump.canceled += cm.Input_Jump;

        sprint.performed += cm.Input_Sprint;
        sprint.canceled += cm.Input_Sprint;

        crouch.performed += cm.Input_Crouch;
        crouch.canceled += cm.Input_Crouch;

    }

    void OnDisable()
    {
        if (move != null) { move.performed -= cm.Input_Movement; move.canceled -= cm.Input_Movement; }
        if (jump != null) { jump.performed -= cm.Input_Jump; jump.canceled -= cm.Input_Jump; }
        if (sprint != null) { sprint.performed -= cm.Input_Sprint; sprint.canceled -= cm.Input_Sprint; }
        if (crouch != null) { crouch.performed -= cm.Input_Crouch; crouch.canceled -= cm.Input_Crouch; }
    }
}
