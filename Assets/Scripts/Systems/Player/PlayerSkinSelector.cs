using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Choisit quel modèle enfant du prefab Player afficher selon le joueur (P1/P2) — évite que P2
/// soit un clone visuel de P1. Les modèles candidats (ex: Tall, Small) coexistent déjà comme
/// enfants du root, chacun avec son propre Animator ; ce composant se contente d'activer le bon et
/// de désactiver les autres.
///
/// À appeler explicitement juste après PlayerInput.Instantiate(...) (pas depuis Awake) — playerIndex
/// n'est fiable qu'une fois Instantiate() revenu, et TopDownController.Awake() a de toute façon déjà
/// mis en cache l'Animator par défaut du prefab à ce moment-là (voir RefreshAnimator()).
/// </summary>
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(TopDownController))]
public class PlayerSkinSelector : MonoBehaviour
{
    [Tooltip("Index 0 = P1, index 1 = P2, etc. Glisser les enfants modèle du prefab dans cet ordre.")]
    public GameObject[] skins;

    public void ApplyForCurrentPlayer()
    {
        if (skins == null || skins.Length == 0) return;

        var pi    = GetComponent<PlayerInput>();
        int index = Mathf.Clamp(pi.playerIndex, 0, skins.Length - 1);

        for (int i = 0; i < skins.Length; i++)
            if (skins[i] != null) skins[i].SetActive(i == index);

        GetComponent<TopDownController>().RefreshAnimator();
    }
}
