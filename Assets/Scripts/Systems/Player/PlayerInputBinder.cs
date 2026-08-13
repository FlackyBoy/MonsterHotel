using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lit les actions Input System du joueur et les transmet au TopDownController.
/// En solo, gère le switch manuel clavier↔manette (sans passer par l'auto-switch Unity
/// qui ne distingue pas les deux manettes).
/// </summary>
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(TopDownController))]
public class PlayerInputBinder : MonoBehaviour
{
    [Header("Noms des control schemes (doivent correspondre à l'asset)")]
    public string schemeGamepad       = "Gamepad";
    public string schemeKeyboardMouse = "Keyboard&Mouse";

    PlayerInput       _pi;
    TopDownController _controller;
    InputAction       _move;
    float             _switchCooldown;

    void Awake()
    {
        _pi         = GetComponent<PlayerInput>();
        _controller = GetComponent<TopDownController>();
    }

    void Start()
    {
        var a = _pi.actions;
        _move = a.FindAction("Move", throwIfNotFound: false);

    }

    void Update()
    {
        if (_move == null || !_move.enabled) return;

        // Switch clavier↔manette (IsUsedByOtherPlayer empêche de voler le device de P2)
        TrySoloDeviceSwitch();

        // Lecture du mouvement selon le device actif
        var kb = Keyboard.current;
        if (kb != null && HasDevice<Keyboard>())
        {
            Vector2 input = Vector2.zero;
            if (_pi.playerIndex == 0) // P1 — ZQSD (positions physiques AZERTY)
            {
                if (kb.wKey.isPressed) input.y += 1f;
                if (kb.sKey.isPressed) input.y -= 1f;
                if (kb.aKey.isPressed) input.x -= 1f;
                if (kb.dKey.isPressed) input.x += 1f;
            }
            else // P2+ — flèches
            {
                if (kb.upArrowKey.isPressed)   input.y += 1f;
                if (kb.downArrowKey.isPressed)  input.y -= 1f;
                if (kb.leftArrowKey.isPressed)  input.x -= 1f;
                if (kb.rightArrowKey.isPressed) input.x += 1f;
            }
            _controller.SetMoveInput(input.sqrMagnitude > 1f ? input.normalized : input);
            // P1 : Left Shift / P2 : Right Shift
            bool sprint = _pi.playerIndex == 0
                ? kb.leftShiftKey.isPressed
                : kb.rightShiftKey.isPressed;
            _controller.SetSprintInput(sprint);
            // Curseur placement : touches fléchées (même joueur, indépendant du ZQSD)
            Vector2 cursor = Vector2.zero;
            if (kb.upArrowKey.isPressed)    cursor.y += 1f;
            if (kb.downArrowKey.isPressed)  cursor.y -= 1f;
            if (kb.leftArrowKey.isPressed)  cursor.x -= 1f;
            if (kb.rightArrowKey.isPressed) cursor.x += 1f;
            _controller.SetCursorInput(cursor.sqrMagnitude > 1f ? cursor.normalized : cursor);
            return;
        }

        // Gamepad : lit directement depuis le device assigné à CE joueur
        foreach (var device in _pi.devices)
        {
            if (device is Gamepad gp)
            {
                _controller.SetMoveInput(gp.leftStick.ReadValue());
                _controller.SetSprintInput(gp.leftShoulder.isPressed);
                _controller.SetCursorInput(gp.rightStick.ReadValue());
                return;
            }
        }

        _controller.SetMoveInput(_move.ReadValue<Vector2>());
    }

    /// <summary>
    /// Switch manuel clavier↔manette en solo.
    /// Utilise wasPressedThisFrame + cooldown pour éviter les boucles de switch.
    /// </summary>
    void TrySoloDeviceSwitch()
    {
        // Désactivé dès qu'un 2e joueur est présent : le clavier est un état global (pas par
        // joueur), donc les touches ZQSD de P1 (positions physiques W/A/S/D) sont vues par
        // TOUS les PlayerInputBinder, y compris celui de P2 à la manette — ce qui provoquait
        // un basculement involontaire de P2 vers le clavier, libérant sa manette que P1
        // récupérait aussitôt. Cette logique n'a de sens qu'en solo (un seul joueur qui bascule
        // lui-même entre clavier et manette).
        if (PlayerInput.all.Count > 1) return;

        if (_switchCooldown > 0f) { _switchCooldown -= Time.deltaTime; return; }

        var kb = Keyboard.current;

        if (HasDevice<Keyboard>())
        {
            // Sur clavier — switche vers manette au premier appui bouton/stick sur un pad libre
            foreach (var pad in Gamepad.all)
            {
                if (IsUsedByOtherPlayer(pad)) continue;
                bool triggered = pad.buttonSouth.wasPressedThisFrame
                              || pad.buttonNorth.wasPressedThisFrame
                              || pad.buttonEast.wasPressedThisFrame
                              || pad.buttonWest.wasPressedThisFrame
                              || pad.leftShoulder.wasPressedThisFrame
                              || pad.rightShoulder.wasPressedThisFrame
                              || pad.leftStick.ReadValue().sqrMagnitude > 0.5f;
                if (triggered)
                {
                    _pi.SwitchCurrentControlScheme(schemeGamepad, pad);
                    _switchCooldown = 0.4f;
                    return;
                }
            }
        }
        else if (HasDevice<Gamepad>() && kb != null)
        {
            // Sur manette — switche vers clavier au premier appui sur une touche de déplacement
            bool kbMove = kb.wKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame
                       || kb.sKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame
                       || kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame
                       || kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame;
            if (kbMove)
            {
                var ms = Mouse.current;
                if (ms != null) _pi.SwitchCurrentControlScheme(schemeKeyboardMouse, kb, ms);
                else            _pi.SwitchCurrentControlScheme(schemeKeyboardMouse, kb);
                _switchCooldown = 0.4f;
            }
        }
    }

    bool HasDevice<T>() where T : InputDevice
    {
        foreach (var d in _pi.devices)
            if (d is T) return true;
        return false;
    }

    bool IsUsedByOtherPlayer(InputDevice device)
    {
        foreach (var pi in PlayerInput.all)
        {
            if (pi == _pi) continue;
            foreach (var d in pi.devices)
                if (d == device) return true;
        }
        return false;
    }
}
