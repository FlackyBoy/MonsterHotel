using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Cheats clavier pour tester rapidement en jeu, sans passer par l'Inspector.
///
/// Setup : ajouter ce composant sur un objet persistant de la scène (ex: à côté d'EconomyManager).
/// </summary>
public class DebugCheats : MonoBehaviour
{
    [Header("Cheat : ajouter de l'or (C+M)")]
    public bool enableGoldCheat  = true;
    public int  goldCheatAmount  = 1000;

    void Update()
    {
        if (!enableGoldCheat) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        // kb.mKey = position physique QWERTY du M, qui sur AZERTY est labellée ";" — la touche
        // labellée "M" sur un clavier AZERTY correspond à kb.semicolonKey (voir notes AZERTY :
        // Input System utilise toujours les positions physiques QWERTY, jamais le label imprimé).
        var mPhysicalKey = kb.semicolonKey;

        // Peu importe l'ordre d'appui : se déclenche dès que la 2e touche est pressée pendant
        // que l'autre est déjà tenue.
        bool triggered =
            (kb.cKey.isPressed && mPhysicalKey.wasPressedThisFrame) ||
            (mPhysicalKey.isPressed && kb.cKey.wasPressedThisFrame);

        if (!triggered) return;

        EconomyManager.Instance?.Earn(goldCheatAmount);
        Debug.Log($"[Cheat] +{goldCheatAmount}G (solde: {EconomyManager.Instance?.Gold}G)");
    }
}
