using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Jauge d'attente world-space affichée au-dessus de la tête du monstre.
/// Barre verticale qui se vide du haut vers le bas via anchorMax (plus fiable que Image.Filled).
/// </summary>
public class WaitingGaugeUI : MonoBehaviour
{
    [Header("Références")]
    public Canvas canvas;
    public Image  fillImage;

    [Header("Position et taille")]
    public float offsetY    = 3.2f;
    public float barWidth   = 14f;
    public float barHeight  = 80f;

    [Header("Couleur")]
    public Color barColor = Color.white;

    // ─── État ──────────────────────────────────────────────────────

    float _maxTime;
    [SerializeField, Range(0f, 1f)] float _fillAmount;
    bool  _running;
    float _elapsed;

    // ─── Lifecycle ─────────────────────────────────────────────────

    void Awake()
    {
        var cfg = HotelConfig.Instance;
        if (cfg != null)
        {
            barWidth  = cfg.waitGaugeWidth;
            barHeight = cfg.waitGaugeHeight;
            offsetY   = cfg.waitGaugeOffsetY;
            barColor  = cfg.waitGaugeColor;
        }

        if (canvas == null || fillImage == null)
            BuildCanvas();
        else
            ApplyFillSettings();

        var crt = canvas?.GetComponent<RectTransform>();
        if (crt != null) crt.sizeDelta = new Vector2(barWidth, barHeight);

        canvas?.gameObject.SetActive(false);
    }

    void BuildCanvas()
    {
        var cgo = new GameObject("WaitingGaugeCanvas");
        cgo.transform.SetParent(transform, false);
        canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;
        var crt = cgo.GetComponent<RectTransform>();
        crt.sizeDelta  = new Vector2(barWidth, barHeight);
        crt.localScale = Vector3.one * 0.01f;

        // Fond sombre
        var bgGo  = new GameObject("Background");
        bgGo.transform.SetParent(cgo.transform, false);
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        var bgRt  = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

        // Barre — anchorMax.y contrôle la hauteur (0=vide, 1=plein)
        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(bgGo.transform, false);
        fillImage       = fillGo.AddComponent<Image>();
        fillImage.color = barColor;
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.pivot     = new Vector2(0.5f, 0f);
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(1f, 1f);
        fillRt.offsetMin = new Vector2(2f, 2f);
        fillRt.offsetMax = new Vector2(-2f, -2f);
    }

    /// <summary>Remet les ancres correctement si le canvas existait déjà (vieux setup).</summary>
    void ApplyFillSettings()
    {
        fillImage.color = barColor;
        fillImage.type  = Image.Type.Simple;
        var rt = fillImage.rectTransform;
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(2f, 2f);
        rt.offsetMax = new Vector2(-2f, -2f);
    }

    void LateUpdate()
    {
        if (canvas == null || !canvas.gameObject.activeSelf) return;
        canvas.transform.position = transform.position + Vector3.up * offsetY;
        if (Camera.main != null)
            canvas.transform.rotation = Camera.main.transform.rotation;
    }

    // ─── API ───────────────────────────────────────────────────────

    public void Show(float maxTime)
    {
        _maxTime = maxTime > 0f ? maxTime : 30f;
        _elapsed = 0f;
        _running = true;

        // Relit la config à chaque Show (au cas où Awake a tourné avant que HotelConfig soit dispo)
        var cfg = HotelConfig.Instance;
        if (cfg != null) { barWidth = cfg.waitGaugeWidth; barHeight = cfg.waitGaugeHeight; offsetY = cfg.waitGaugeOffsetY; barColor = cfg.waitGaugeColor; }

        var crt = canvas?.GetComponent<RectTransform>();
        if (crt != null) crt.sizeDelta = new Vector2(barWidth, barHeight);
        if (fillImage != null) fillImage.color = barColor;

        canvas?.gameObject.SetActive(true);
        UpdateFill(1f);
    }

    public void Tick(float deltaTime)
    {
        if (!_running) return;
        _elapsed += deltaTime;
        UpdateFill(Mathf.Clamp01(1f - _elapsed / _maxTime));
    }

    public void Hide()
    {
        _running = false;
        canvas?.gameObject.SetActive(false);
    }

    // ─── Interne ───────────────────────────────────────────────────

    void UpdateFill(float t)
    {
        _fillAmount = t;
        if (fillImage == null) return;

        // Contrôle la hauteur visible via anchorMax.y (0=vide en bas, 1=plein)
        var rt = fillImage.rectTransform;
        rt.anchorMax = new Vector2(1f, t);
    }
}
