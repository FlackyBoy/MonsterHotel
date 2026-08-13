using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Permet à P2 de rejoindre en appuyant sur Start/A d'une manette non utilisée,
/// ou sur Entrée si un clavier secondaire est configuré.
///
/// Placer sur le même GO que AutoSpawnP1 (ou n'importe quel Manager).
/// </summary>
public class LocalJoinManager : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject playerPrefab;

    [Header("Spawn")]
    public Transform spawnPoint;

    [Header("Schemes")]
    public string controlSchemeGamepad = "Gamepad";

    [Header("Debug — rejoindre P2 avec clavier (touche J)")]
    [Tooltip("Active une touche clavier pour spawner P2 (tests éditeur sans manette)")]
    public bool allowKeyboardDebugJoin = true;

    void Update()
    {
        // Déjà 2 joueurs → rien à faire
        if (PlayerInput.all.Count >= 2) return;
        if (playerPrefab == null) return;

        // ── Manette : n'importe quel gamepad non encore utilisé ──
        foreach (var gamepad in Gamepad.all)
        {
            if (IsDeviceUsed(gamepad)) continue;

            if (gamepad.startButton.wasPressedThisFrame ||
                gamepad.buttonSouth.wasPressedThisFrame)
            {
                SpawnP2(controlSchemeGamepad, gamepad);
                return;
            }
        }

        // ── Debug clavier : touche J ──────────────────────────────
        if (allowKeyboardDebugJoin && Keyboard.current != null)
        {
            if (Keyboard.current.jKey.wasPressedThisFrame)
                SpawnP2(controlSchemeGamepad, Gamepad.all.Count > 0 ? Gamepad.all[0] : null);
        }
    }

    void SpawnP2(string scheme, InputDevice device)
    {
        PlayerInput pi;

        if (device != null)
            pi = PlayerInput.Instantiate(playerPrefab, 1, scheme, -1, device);
        else
            pi = PlayerInput.Instantiate(playerPrefab, 1, scheme, -1);

        if (spawnPoint)
            pi.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        // Verrouillé comme P1 (AutoSpawnP1) — évite qu'Unity ré-assigne automatiquement
        // les devices de P2 (ex: si le clavier partagé génère un input qui ressemble à
        // une intention de switch de contrôle).
        pi.neverAutoSwitchControlSchemes = true;

        if (pi.currentActionMap == null || pi.currentActionMap.name != "Player")
            pi.SwitchCurrentActionMap("Player");

        Debug.Log($"[LocalJoinManager] P2 spawné — scheme={pi.currentControlScheme}, device={device?.name}");
    }

    static bool IsDeviceUsed(InputDevice device)
    {
        foreach (var pi in PlayerInput.all)
            foreach (var d in pi.devices)
                if (d == device) return true;
        return false;
    }
}
