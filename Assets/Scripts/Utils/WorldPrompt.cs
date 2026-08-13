using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Crée un Canvas world-space léger avec fond semi-transparent et label TMP.
/// Usage : var (root, label) = WorldPrompt.Create(width, height);
/// Détruisez root quand vous n'en avez plus besoin.
/// </summary>
public static class WorldPrompt
{
    public static (GameObject root, TMPro.TextMeshProUGUI label) Create(
        float width = 200f, float height = 32f, float scale = 0.007f)
    {
        var root   = new GameObject("_WorldPrompt");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        if (Camera.main != null) canvas.worldCamera = Camera.main;

        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, height);
        root.transform.localScale = Vector3.one * scale;

        // Fond
        var bgGO = new GameObject("BG");
        bgGO.transform.SetParent(root.transform, false);
        var img = bgGO.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.78f);
        var bgRt = bgGO.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

        // Label
        var tgo = new GameObject("Label");
        tgo.transform.SetParent(root.transform, false);
        var txt = tgo.AddComponent<TMPro.TextMeshProUGUI>();
        txt.fontSize  = 13f;
        txt.color     = Color.white;
        txt.alignment = TMPro.TextAlignmentOptions.Center;
        var trt = tgo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        return (root, txt);
    }

    /// <summary>
    /// Oriente le prompt vers la caméra principale.
    /// </summary>
    public static void FaceCamera(GameObject root)
    {
        if (root == null || Camera.main == null) return;
        var dir = root.transform.position - Camera.main.transform.position;
        if (dir.sqrMagnitude > 0.001f)
            root.transform.rotation = Quaternion.LookRotation(dir);
    }
}
