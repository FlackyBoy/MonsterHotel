using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Marqueur visuel world-space affiché au-dessus d'un "visiteur repas" (G6, Phase 2) —
/// permet de le distinguer visuellement d'un client ayant réservé une chambre.
/// Ajouté par SpawnScheduler quand un monstre est déterminé comme visiteur repas.
/// Se détruit automatiquement avec le monstre (OnDestroy) — pas besoin d'appel manuel.
/// </summary>
public class MealVisitorBadge : MonoBehaviour
{
    [Header("Position")]
    [Tooltip("1.8 place la bulle au niveau du torse sur ces modèles — 2.6 la fait passer au-dessus de la tête")]
    public Vector3 offset = new Vector3(0f, 2.6f, 0f);

    [Header("Apparence")]
    public string label = "Repas";
    [Tooltip("Orange par défaut — distinct du noir des bulles de file d'attente (GuestBubbleSlot, monstres en file pour une chambre/le resto)")]
    public Color backgroundColor = new Color(0.85f, 0.55f, 0.1f, 0.85f);

    GameObject _root;

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake() => BuildCanvas();

    void Update()
    {
        if (_root == null) return;
        _root.transform.position = transform.position + offset;
        WorldPrompt.FaceCamera(_root);
    }

    void OnDestroy()
    {
        if (_root != null) Destroy(_root);
    }

    // ─── Construction du canvas ───────────────────────────────────

    void BuildCanvas()
    {
        _root = new GameObject("_MealVisitorBadge");
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        if (Camera.main != null) canvas.worldCamera = Camera.main;

        var rt = _root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(90f, 26f);
        _root.transform.localScale = Vector3.one * 0.007f;

        // Fond
        var bgGo = new GameObject("Bg");
        bgGo.transform.SetParent(_root.transform, false);
        var bg = bgGo.AddComponent<Image>();
        bg.color = backgroundColor;
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

        // Texte
        var tgo = new GameObject("Label");
        tgo.transform.SetParent(_root.transform, false);
        var txt = tgo.AddComponent<TMPro.TextMeshProUGUI>();
        txt.text      = label;
        txt.fontSize  = 12f;
        txt.color     = Color.white;
        txt.alignment = TMPro.TextAlignmentOptions.Center;
        var trt = tgo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
    }
}
