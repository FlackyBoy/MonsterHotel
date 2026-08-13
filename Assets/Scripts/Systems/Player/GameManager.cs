using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Singleton léger : état du jeu + liste des joueurs actifs.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // ─── État ────────────────────────────────────────────────────

    public enum GameState { Menu, Playing, Paused, GameOver }

    GameState _state = GameState.Menu;
    public GameState State => _state;

    // ─── Joueurs ─────────────────────────────────────────────────

    readonly List<PlayerInput> _players = new();
    public IReadOnlyList<PlayerInput> Players => _players;

    // ─── Événements ──────────────────────────────────────────────

    public event System.Action<GameState> OnStateChanged;
    public event System.Action<PlayerInput> OnPlayerJoined;
    public event System.Action<PlayerInput> OnPlayerLeft;

    // ─── Lifecycle ───────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ─── Gestion joueurs ─────────────────────────────────────────

    /// <summary>Appelé par PlayerInput (message "OnPlayerJoined" si PlayerInputManager) ou manuellement.</summary>
    public void RegisterPlayer(PlayerInput pi)
    {
        if (_players.Contains(pi)) return;
        _players.Add(pi);
        OnPlayerJoined?.Invoke(pi);
    }

    public void UnregisterPlayer(PlayerInput pi)
    {
        if (!_players.Remove(pi)) return;
        OnPlayerLeft?.Invoke(pi);
    }

    // ─── Gestion état ────────────────────────────────────────────

    public void SetState(GameState newState)
    {
        if (_state == newState) return;
        _state = newState;
        OnStateChanged?.Invoke(_state);

        Time.timeScale = _state == GameState.Paused ? 0f : 1f;
    }

    public void StartGame() => SetState(GameState.Playing);
    public void PauseGame() => SetState(GameState.Paused);
    public void ResumeGame() => SetState(GameState.Playing);
    public void EndGame()   => SetState(GameState.GameOver);
}
