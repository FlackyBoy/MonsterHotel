using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Outil éditeur : construit la hiérarchie de scène Monster Hotel en un clic.
/// Menu : Monster Hotel → Setup Scene Hierarchy
/// </summary>
public static class SceneSetup
{
    [MenuItem("Monster Hotel/Setup Scene Hierarchy")]
    static void SetupHierarchy()
    {
        if (!EditorUtility.DisplayDialog(
            "Setup Scene Hierarchy",
            "Cela va créer la hiérarchie de base dans la scène active.\nContinuer ?",
            "Oui", "Annuler"))
            return;

        // ── _Managers ────────────────────────────────────────────
        var managers = GetOrCreate("_Managers");
        GetOrCreate("GameManager",     managers);
        GetOrCreate("GridManager",     managers);
        GetOrCreate("EconomyManager",  managers);
        GetOrCreate("SpawnScheduler",  managers);

        // ── _Camera ──────────────────────────────────────────────
        var cameraRoot = GetOrCreate("_Camera");

        // Cherche la MainCamera existante ou en crée une
        var mainCamGO = GameObject.FindWithTag("MainCamera");
        if (mainCamGO == null)
        {
            mainCamGO = new GameObject("MainCamera");
            mainCamGO.AddComponent<Camera>();
            mainCamGO.tag = "MainCamera";
            mainCamGO.AddComponent<AudioListener>();
        }
        mainCamGO.transform.SetParent(cameraRoot.transform, true);
        // CinemachineBrain à ajouter manuellement sur la MainCamera

        // ── World ────────────────────────────────────────────────
        var world = GetOrCreate("World");
        GetOrCreate("Grid", world);

        // ── UI ───────────────────────────────────────────────────
        var ui = GetOrCreate("UI");
        GetOrCreate("HUD", ui);

        // ── Nettoyage : supprime la Directional Light par défaut si seule dans scène ──
        // (optionnel, laissée intacte pour l'éclairage)

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[SceneSetup] Hiérarchie Monster Hotel créée. Pense à sauvegarder la scène (Ctrl+S).");
    }

    // ─── Utilitaire ───────────────────────────────────────────────────────────

    static GameObject GetOrCreate(string name, GameObject parent = null)
    {
        // Cherche d'abord un objet existant avec ce nom exact au bon niveau
        Transform parentT = parent != null ? parent.transform : null;

        if (parentT != null)
        {
            var existing = parentT.Find(name);
            if (existing != null) return existing.gameObject;
        }
        else
        {
            var found = GameObject.Find(name);
            if (found != null && found.transform.parent == null) return found;
        }

        var go = new GameObject(name);
        if (parentT != null) go.transform.SetParent(parentT, false);
        return go;
    }
}
