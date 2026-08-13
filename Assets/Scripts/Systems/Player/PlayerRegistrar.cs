using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// À mettre sur le prefab joueur.
/// S'enregistre auprès du GameManager et configure la caméra principale pour P1.
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class PlayerRegistrar : MonoBehaviour
{
    PlayerInput _pi;

    void Awake() => _pi = GetComponent<PlayerInput>();

    void OnEnable()
    {
        GameManager.Instance?.RegisterPlayer(_pi);
    }

    void OnDisable()
    {
        GameManager.Instance?.UnregisterPlayer(_pi);
    }
}
