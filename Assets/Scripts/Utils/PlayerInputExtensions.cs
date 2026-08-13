using UnityEngine.InputSystem;

/// <summary>
/// Extensions sur PlayerInput pour éviter la duplication de FindAction + WasPressedThisFrame.
/// </summary>
public static class PlayerInputExtensions
{
    public static bool WasPressed(this PlayerInput pi, string actionName)
    {
        var action = pi.actions.FindAction(actionName, throwIfNotFound: false);
        return action?.WasPressedThisFrame() ?? false;
    }
}
