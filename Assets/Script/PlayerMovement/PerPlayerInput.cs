using UnityEngine;
using UnityEngine.InputSystem;


public class PerPlayerInput : MonoBehaviour
{
    [Header("Noms d'actions dans la map active")]
    public string moveAction = "Movement"; // Vector2
    public string jumpAction = "Jump"; // Button


    PlayerInput pi;
    InputAction move, jump;


    public Vector2 Move { get; private set; }
    public bool JumpPressed { get; private set; }


    void Awake()
    {
        pi = GetComponent<PlayerInput>();
        var a = pi.actions;
        move = a.FindAction(moveAction, throwIfNotFound: false);
        jump = a.FindAction(jumpAction, throwIfNotFound: false);
        if (move == null) Debug.LogError($"Action introuvable: {moveAction}");
        if (jump == null) Debug.LogError($"Action introuvable: {jumpAction}");
    }


    void OnEnable()
    {
        move?.Enable();
        jump?.Enable();
        if (jump != null) { jump.performed += OnJump; jump.canceled += OnJump; }
    }


    void OnDisable()
    {
        if (jump != null) { jump.performed -= OnJump; jump.canceled -= OnJump; }
        move?.Disable();
        jump?.Disable();
    }


    void Update()
    {
        if (move != null) Move = move.ReadValue<Vector2>();
    }


    void OnJump(InputAction.CallbackContext ctx) => JumpPressed = ctx.ReadValueAsButton();
}