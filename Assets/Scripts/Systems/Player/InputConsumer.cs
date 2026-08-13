using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Permet à un système de "consommer" l'action Interact pour un joueur donné ce frame,
/// empêchant d'autres systèmes de réagir au même appui.
/// Appeler Consume(playerIndex) pour réserver l'input.
/// Appeler IsConsumed(playerIndex) pour vérifier avant d'agir.
/// Se réinitialise automatiquement à chaque frame.
/// </summary>
public class InputConsumer : MonoBehaviour
{
    public static InputConsumer Instance { get; private set; }

    readonly HashSet<int> _consumed = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void LateUpdate() => _consumed.Clear();

    public static void Consume(int playerIndex)
    {
        if (Instance != null) Instance._consumed.Add(playerIndex);
    }

    public static bool IsConsumed(int playerIndex) =>
        Instance != null && Instance._consumed.Contains(playerIndex);
}
