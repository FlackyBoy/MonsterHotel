using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rend un bâtiment semi-transparent (dithering) quand il se trouve entre la caméra et un joueur.
/// Ajoute ce composant sur n'importe quel bâtiment de la scène (murs extérieurs, décors, etc.).
/// Requiert que le shader des matériaux expose la propriété "_DitherAlpha".
/// </summary>
public class BuildingDither : MonoBehaviour
{
    [Tooltip("Vitesse d'apparition/disparition du dithering")]
    public float fadeSpeed = 6f;

    [Tooltip("Intensité maximale du dithering (0 = opaque, >1 = effet renforcé)")]
    public float maxDitherAlpha = 0.82f;

    [Tooltip("Hauteur de l'offset vertical appliqué au point cible du joueur pour le raycast")]
    public float playerHeightOffset = 0.8f;

    [Tooltip("Nom du GameObject à exclure du dithering (ex: 'Floor')")]
    public string excludeByName = "Floor";

    // ─── Privé ────────────────────────────────────────────────────

    static readonly int          FadeStrengthID = Shader.PropertyToID("_FadeStrength");
    static readonly RaycastHit[] HitBuffer      = new RaycastHit[32];

    readonly List<Renderer> _renderers = new();
    MaterialPropertyBlock   _mpb;
    float                   _currentDither;

    // ─── Lifecycle ────────────────────────────────────────────────

    void Start()
    {
        _mpb = new MaterialPropertyBlock();
        var cfg = HotelConfig.Instance;
        if (cfg != null) { fadeSpeed = cfg.ditherFadeSpeed; maxDitherAlpha = cfg.ditherMaxAlpha; playerHeightOffset = cfg.ditherHeightOffset; }
        RefreshRenderers();
    }

    void Update()
    {
        float target = IsOccludingAnyPlayer() ? maxDitherAlpha : 0f;

        if (Mathf.Approximately(_currentDither, target) && Mathf.Approximately(target, 0f))
            return;

        _currentDither = Mathf.MoveTowards(_currentDither, target, fadeSpeed * Time.deltaTime);
        _mpb.SetFloat(FadeStrengthID, _currentDither);
        foreach (var rend in _renderers)
            if (rend != null) rend.SetPropertyBlock(_mpb);
    }

    // ─── API ──────────────────────────────────────────────────────

    public void RefreshRenderers()
    {
        _renderers.Clear();
        foreach (var rend in GetComponentsInChildren<Renderer>())
        {
            if (!string.IsNullOrEmpty(excludeByName) && rend.gameObject.name == excludeByName)
                continue;
            _renderers.Add(rend);
        }
    }

    // ─── Interne ──────────────────────────────────────────────────

    bool IsOccludingAnyPlayer()
    {
        foreach (var placer in RoomPlacer.All)
        {
            if (placer == null) continue;

            // RW1 : caméra propre à CE joueur (écran splitté statique) — chaque joueur teste
            // l'occlusion depuis SA propre caméra, pas une Camera.main globale partagée.
            var cam = placer.GetComponent<TopDownController>()?.PlayerCamera;
            if (cam == null) continue;

            if (IsOccluding(cam.transform.position, placer.transform.position + Vector3.up * playerHeightOffset))
                return true;
        }
        return false;
    }

    bool IsOccluding(Vector3 from, Vector3 to)
    {
        var dir   = to - from;
        float dist = dir.magnitude;
        if (dist < 0.01f) return false;

        int count = Physics.RaycastNonAlloc(from, dir / dist, HitBuffer, dist);
        for (int i = 0; i < count; i++)
        {
            var t = HitBuffer[i].transform;
            while (t != null)
            {
                if (t == transform) return true;
                t = t.parent;
            }
        }
        return false;
    }
}
