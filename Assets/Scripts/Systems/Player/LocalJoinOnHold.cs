using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class LocalJoinOnHold : MonoBehaviour
{
    [Tooltip("Prefab joueur avec PlayerInput à la racine")]
    public GameObject playerPrefab;

    [Tooltip("Noms EXACTS des control schemes dans movementActions")]
    public string controlSchemeGamepad       = "Gamepad";
    public string controlSchemeKeyboardMouse = "Keyboard&Mouse";
    public string controlSchemeKeyboard1     = "Keyboard1";   // P1 en cheat (ZQSD only)
    public string controlSchemeKeyboard      = "Keyboard2";   // P2 en cheat (flèches)

    [Tooltip("Durée de maintien pour rejoindre")]
    public float holdSeconds = 0.6f;

    [Tooltip("Nombre max de joueurs")]
    public int maxPlayers = 2;

    [Header("Cheat — spawn P2 clavier")]
    [Tooltip("Maintenir cette touche + appuyer sur cheatSpawnKey pour spawner P2 (ex: C + P)")]
    public Key cheatHoldKey  = Key.C;
    public Key cheatSpawnKey = Key.P;

    private readonly Dictionary<InputDevice, float> hold = new();

    // Réutilise le même point de spawn que AutoSpawnP1 (probablement sur le même GameObject) —
    // sans ça, un joueur qui rejoint ici apparaît à l'origine du monde (0,0,0), pas dans l'hôtel.
    Transform SpawnPoint => GetComponent<AutoSpawnP1>()?.spawnPoint;

    void PositionAtSpawn(PlayerInput pi)
    {
        var sp = SpawnPoint;
        if (sp != null) pi.transform.SetPositionAndRotation(sp.position, sp.rotation);
    }

    void Update()
    {
if (playerPrefab == null) return;

        // Limite joueurs
        if (PlayerInput.all.Count >= maxPlayers) return;

        // 1) Manettes: Start/Options/Bouton Sud
        foreach (var pad in Gamepad.all)
        {
            // Ignore les manettes déjà assignées à un joueur existant
            if (IsDeviceUsed(pad)) continue;

            bool pressing = pad.startButton.isPressed || pad.selectButton.isPressed || pad.buttonSouth.isPressed;
            if (pressing)
            {
                hold.TryGetValue(pad, out float t);
                t += Time.unscaledDeltaTime;
                hold[pad] = t;

                if (t >= holdSeconds)
                {
                    var pi = PlayerInput.Instantiate(playerPrefab, -1, controlSchemeGamepad, -1, pad);
                    pi.neverAutoSwitchControlSchemes = true;
                    if (pi.currentActionMap == null || pi.currentActionMap.name != "Player")
                        pi.SwitchCurrentActionMap("Player");
                    pi.GetComponent<PlayerSkinSelector>()?.ApplyForCurrentPlayer();
                    PositionAtSpawn(pi);

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
                    PlayerInput pi2 = Mouse.current != null
                        ? PlayerInput.Instantiate(playerPrefab, -1, controlSchemeKeyboardMouse, -1, kb, Mouse.current)
                        : PlayerInput.Instantiate(playerPrefab, -1, controlSchemeKeyboardMouse, -1, kb);
                    pi2.neverAutoSwitchControlSchemes = true;
                    pi2.GetComponent<PlayerSkinSelector>()?.ApplyForCurrentPlayer();
                    PositionAtSpawn(pi2);

                    hold[kb] = 0f;
                }
            }
            else hold.Remove(kb);

            // 3) Cheat C+P : spawn P2 sur Keyboard2 (flèches) et bascule P1 sur Keyboard1 (ZQSD seulement)
            if (kb[cheatHoldKey].isPressed && kb[cheatSpawnKey].wasPressedThisFrame)
            {
                if (PlayerInput.all.Count < maxPlayers)
                {
                    // Bascule P1 vers Keyboard1 (ZQSD uniquement, plus de flèches)
                    if (PlayerInput.all.Count > 0)
                    {
                        var p1 = PlayerInput.all[0];
                        p1.SwitchCurrentControlScheme(controlSchemeKeyboard1, kb);
                        p1.neverAutoSwitchControlSchemes = true;
                    }

                    // Spawn P2 avec Keyboard2 (flèches)
                    var pi3 = PlayerInput.Instantiate(playerPrefab, -1, controlSchemeKeyboard, -1, kb);
                    pi3.neverAutoSwitchControlSchemes = true;
                    pi3.GetComponent<PlayerSkinSelector>()?.ApplyForCurrentPlayer();
                    PositionAtSpawn(pi3);
                }
            }
        }
    }

    static bool IsDeviceUsed(InputDevice device)
    {
        foreach (var pi in PlayerInput.all)
            foreach (var d in pi.devices)
                if (d == device) return true;
        return false;
    }
}
