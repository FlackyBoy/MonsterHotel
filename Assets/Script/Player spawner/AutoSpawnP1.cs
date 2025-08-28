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
    public bool preferGamepadStart = false;
    [Tooltip("Autoriser le basculement automatique clavier↔manette après le spawn")]
    public bool allowAutoSwitch = false;

    [Header("Spawn")]
    public Transform spawnPoint;

    void Start()
    {
        if (!playerPrefab) { Debug.LogError("AutoSpawnP1: playerPrefab non assigné"); return; }
        if (PlayerInput.all.Count > 0) return; // déjà un joueur

        // Choix des devices de départ
        var pad = Gamepad.current;
        var kb = Keyboard.current;
        var ms = Mouse.current;

        PlayerInput pi = null;

        if (preferGamepadStart && pad != null)
        {
            // P1 à la manette
            pi = PlayerInput.Instantiate(playerPrefab, 0, controlSchemeGamepad, -1, pad);
            // force le scheme (utile si l’asset a plusieurs schemes)
            pi.SwitchCurrentControlScheme(controlSchemeGamepad, pad);
        }
        else
        {
            // P1 au clavier/souris si possible
            if (kb != null && ms != null)
            {
                pi = PlayerInput.Instantiate(playerPrefab, 0, controlSchemeKeyboardMouse, -1, kb, ms);
                pi.SwitchCurrentControlScheme(controlSchemeKeyboardMouse, kb, ms);
            }
            else if (kb != null)
            {
                pi = PlayerInput.Instantiate(playerPrefab, 0, controlSchemeKeyboardMouse, -1, kb);
                pi.SwitchCurrentControlScheme(controlSchemeKeyboardMouse, kb);
            }
            else if (pad != null)
            {
                // fallback manette
                pi = PlayerInput.Instantiate(playerPrefab, 0, controlSchemeGamepad, -1, pad);
                pi.SwitchCurrentControlScheme(controlSchemeGamepad, pad);
            }
            else
            {
                // aucun device détecté → instancie quand même
                pi = PlayerInput.Instantiate(playerPrefab, 0, controlSchemeKeyboardMouse, -1);
            }
        }

        if (pi == null) { Debug.LogError("AutoSpawnP1: échec d’instanciation PlayerInput"); return; }

        // Positionne au point de spawn si fourni
        if (spawnPoint)
            pi.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        // Verrouille ou non le basculement auto
        pi.neverAutoSwitchControlSchemes = !allowAutoSwitch;

        // Assure la bonne action map
        if (pi.currentActionMap == null || pi.currentActionMap.name != "Gameplay")
            pi.SwitchCurrentActionMap("Gameplay");

        Debug.Log($"P1 prêt. scheme={pi.currentControlScheme}, devices={string.Join(",", pi.devices)}");
    }
}
