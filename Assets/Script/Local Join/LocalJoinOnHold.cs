using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class LocalJoinOnHold : MonoBehaviour
{
    [Tooltip("Prefab joueur avec PlayerInput à la racine")]
    public GameObject playerPrefab;

    [Tooltip("Noms EXACTS des control schemes dans movementActions")]
    public string controlSchemeGamepad = "Gamepad";
    public string controlSchemeKeyboardMouse = "Keyboard&Mouse";

    [Tooltip("Durée de maintien pour rejoindre")]
    public float holdSeconds = 0.6f;

    [Tooltip("Nombre max de joueurs")]
    public int maxPlayers = 2;

    private readonly Dictionary<InputDevice, float> hold = new();

    void Update()
    {
        if (playerPrefab == null) return;

        // Limite joueurs
        if (PlayerInput.all.Count >= maxPlayers) return;

        // 1) Manettes: Start/Options/Bouton Sud
        foreach (var pad in Gamepad.all)
        {
            bool pressing = pad.startButton.isPressed || pad.selectButton.isPressed || pad.buttonSouth.isPressed;
            if (pressing)
            {
                hold.TryGetValue(pad, out float t);
                t += Time.unscaledDeltaTime;
                hold[pad] = t;

                if (t >= holdSeconds)
                {
                    var pi = PlayerInput.Instantiate(playerPrefab, -1, controlSchemeGamepad, -1, pad);
                    Debug.Log($"[Join] Gamepad {pad.displayName} → Player {pi.playerIndex}");
                    hold[pad] = 0f;
                    return; // un join par frame
                }
            }
            else hold.Remove(pad);
        }

        // 2) Clavier (optionnel): Enter pour rejoindre
        var kb = Keyboard.current;
        if (kb != null)
        {
            bool pressing = kb.enterKey.isPressed || kb.numpadEnterKey.isPressed;
            if (pressing)
            {
                hold.TryGetValue(kb, out float t);
                t += Time.unscaledDeltaTime;
                hold[kb] = t;

                if (t >= holdSeconds)
                {
                    if (Mouse.current != null)
                        PlayerInput.Instantiate(playerPrefab, -1, controlSchemeKeyboardMouse, -1, kb, Mouse.current);
                    else
                        PlayerInput.Instantiate(playerPrefab, -1, controlSchemeKeyboardMouse, -1, kb);

                    Debug.Log("[Join] Keyboard&Mouse");
                    hold[kb] = 0f;
                }
            }
            else hold.Remove(kb);
        }
    }
}
