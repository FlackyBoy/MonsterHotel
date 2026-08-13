using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bulle world-space affichée au-dessus d'un monstre en attente d'une chambre.
/// Affiche le nom du monstre et une jauge de patience (vert → rouge).
///
/// Ce composant est ajouté dynamiquement par ReservationSystem.RegisterArrival().
/// Il gère son propre canvas séparé (_root) détruit quand le monstre est pris en charge.
/// </summary>
public class GuestBubble : MonoBehaviour
{
    [Header("Position")]
    public Vector3 bubbleOffset = new Vector3(0f, 1.8f, 0f);

    ReservationSystem.PendingGuest _guest;
    GameObject _root;
    Image      _bar;

    // ─── API ──────────────────────────────────────────────────────

    public void Init(ReservationSystem.PendingGuest guest)
    {
        _guest = guest;
        BuildCanvas();
    }

    /// <summary>Cache la bulle (appelé quand le monstre obtient une chambre).</summary>
    public void Hide()
    {
        if (_root != null) { Destroy(_root); _root = null; }
        enabled = false;
    }

    // ─── Lifecycle ────────────────────────────────────────────────

    void Update()
    {
        if (_root == null || _guest == null) return;

        _root.transform.position = transform.position + bubbleOffset;
        WorldPrompt.FaceCamera(_root);

        float ratio = _guest.Data.maxWaitTime > 0f
            ? Mathf.Clamp01(_guest.TimeRemaining / _guest.Data.maxWaitTime)
            : 1f;

        if (_bar != null)
        {
            _bar.fillAmount = ratio;
            _bar.color      = Color.Lerp(Color.red, Color.green, ratio);
        }
    }

    void OnDestroy() => Hide();

    // ─── Construction du canvas ───────────────────────────────────

    void BuildCanvas()
    {
        _root = new GameObject("_GuestBubble");
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        if (Camera.main != null) canvas.worldCamera = Camera.main;

        var rt = _root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(120f, 44f);
        _root.transform.localScale = Vector3.one * 0.007f;

        // Fond global
        MakeImage(_root.transform, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.75f));

        // Nom du monstre
        var tgo = new GameObject("Name");
        tgo.transform.SetParent(_root.transform, false);
        var txt = tgo.AddComponent<TMPro.TextMeshProUGUI>();
        txt.text      = _guest.Data.monsterName;
        txt.fontSize  = 11f;
        txt.color     = Color.white;
        txt.alignment = TMPro.TextAlignmentOptions.Center;
        var trt = tgo.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0.32f);
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        // Fond de la barre
        MakeImage(_root.transform,
            new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.30f),
            new Color(0.15f, 0.15f, 0.15f, 1f));

        // Barre de patience (fill horizontal)
        var barGO = new GameObject("Bar");
        barGO.transform.SetParent(_root.transform, false);
        _bar            = barGO.AddComponent<Image>();
        _bar.type       = Image.Type.Filled;
        _bar.fillMethod = Image.FillMethod.Horizontal;
        _bar.fillAmount = 1f;
        _bar.color      = Color.green;
        var brt = barGO.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.04f, 0.06f);
        brt.anchorMax = new Vector2(0.96f, 0.30f);
        brt.offsetMin = brt.offsetMax = Vector2.zero;
    }

    static void MakeImage(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        var go = new GameObject("Bg");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}
