using UnityEngine;
using UnityEngine.InputSystem;

public class AutoSpawnP1 : MonoBehaviour
{
    [Header("Prefab")]
    [Tooltip("Prefab joueur AVEC PlayerInput sur la racine")]
    public GameObject playerPrefab;

    [Header("Control schemes (noms EXACTS dans movementActions)")]
    public string controlSchemeGamepad = "Gamepad";
    public string controlSchemeKeyboardMouse = "Keyboard&Mouse";

    [Header("Options")]
    [Tooltip("Vrai = préfère démarrer à la manette si dispo, sinon clavier+souris")]
    public bool preferGamepadStart = true;

    [Header("Spawn")]
    public Transform spawnPoint;

    void Start()
    {
        if (!playerPrefab) { return; }
        if (PlayerInput.all.Count > 0) return; // déjà un joueur

        // Choix des devices de départ
        var pad = Gamepad.current;
        var kb = Keyboard.current;
        var ms = Mouse.current;

        PlayerInput pi = null;

        if (preferGamepadStart && pad != null)
        {
            // P1 à la manette — passe uniquement CE pad, pas de SwitchCurrentControlScheme
            // pour éviter un re-matching qui pourrait attirer d’autres devices
            pi = PlayerInput.Instantiate(playerPrefab, 0, controlSchemeGamepad, -1, pad);
        }
        else
        {
            // P1 au clavier/souris si possible
            if (kb != null && ms != null)
                pi = PlayerInput.Instantiate(playerPrefab, 0, controlSchemeKeyboardMouse, -1, kb, ms);
            else if (kb != null)
                pi = PlayerInput.Instantiate(playerPrefab, 0, controlSchemeKeyboardMouse, -1, kb);
            else if (pad != null)
                pi = PlayerInput.Instantiate(playerPrefab, 0, controlSchemeGamepad, -1, pad);
            else
                pi = PlayerInput.Instantiate(playerPrefab, 0, controlSchemeKeyboardMouse, -1);
        }

        if (pi == null) { return; }

        // Positionne au point de spawn si fourni
        if (spawnPoint)
            pi.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        // Toujours verrouillé — le switch clavier↔manette en solo est géré manuellement par PlayerInputBinder
        pi.neverAutoSwitchControlSchemes = true;

        // Assure la bonne action map
        if (pi.currentActionMap == null || pi.currentActionMap.name != "Player")
            pi.SwitchCurrentActionMap("Player");

    }
}
