using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rend les murs de la chambre semi-transparents (dithering Bayer) quand un joueur est à l'intérieur.
///
/// Setup :
///   • Créer un matériau avec le shader "MonsterHotel/RoomWallDither"
///   • L'assigner comme wallMaterial dans RoomPlaceholder
///   • Ce composant est ajouté automatiquement par RoomPlacer lors de la pose
/// </summary>
[RequireComponent(typeof(RoomInstance))]
public class RoomDither : MonoBehaviour
{
    [Tooltip("Vitesse d'apparition/disparition du dithering")]
    public float fadeSpeed = 6f;

    [Tooltip("Intensité maximale du dithering (0 = opaque, >1 = effet renforcé)")]
    public float maxDitherAlpha = 0.82f;

    [Tooltip("Rayon du dithering en unités monde autour du joueur (shader Fade Radius). Doit correspondre à la taille de la chambre.")]
    public float fadeRadius = 5f;

    // ─── Privé ────────────────────────────────────────────────────

    [Tooltip("Hauteur de l'offset vertical appliqué au point cible du joueur pour le raycast")]
    public float playerHeightOffset = 0.8f;

    static readonly int          FadeStrengthID = Shader.PropertyToID("_FadeStrength");
    static readonly int          FadeRadiusID   = Shader.PropertyToID("_FadeRadius");
    static readonly RaycastHit[] HitBuffer      = new RaycastHit[32];

    readonly List<Renderer> _wallRenderers = new();
    MaterialPropertyBlock   _mpb;
    float                   _currentDither;

    // ─── Lifecycle ────────────────────────────────────────────────

    void Start()
    {
        _mpb = new MaterialPropertyBlock();
        var cfg = HotelConfig.Dither;
        if (cfg != null)
        {
            fadeSpeed          = cfg.ditherFadeSpeed;
            maxDitherAlpha     = cfg.ditherMaxAlpha;
            playerHeightOffset = cfg.ditherHeightOffset;
            fadeRadius         = cfg.ditherFadeRadius;
        }
        CollectWallRenderers();
    }

    void Update()
    {
        bool playerInside = IsAnyPlayerInside() || IsOccludingAnyPlayer();
        float target      = playerInside ? maxDitherAlpha : 0f;

        if (Mathf.Approximately(_currentDither, target) && Mathf.Approximately(target, 0f))
            return; // rien à faire — évite le SetPropertyBlock chaque frame quand opaque

        _currentDither = Mathf.MoveTowards(_currentDither, target, fadeSpeed * Time.deltaTime);

        _mpb.SetFloat(FadeStrengthID, _currentDither);
        _mpb.SetFloat(FadeRadiusID,   fadeRadius);
        foreach (var rend in _wallRenderers)
            if (rend != null) rend.SetPropertyBlock(_mpb);
    }

    // ─── Interne ──────────────────────────────────────────────────

    /// <summary>Re-scanne les renderers après un swap de visuel (upgrade).</summary>
    public void RefreshRenderers() => CollectWallRenderers();

    void CollectWallRenderers()
    {
        _wallRenderers.Clear();
        foreach (var rend in GetComponentsInChildren<Renderer>())
        {
            // Exclut le sol (pas besoin de le ditherer)
            if (rend.gameObject.name == "Floor") continue;
            _wallRenderers.Add(rend);
        }
    }

    bool IsAnyPlayerInside()
    {
        var room = GetComponent<RoomInstance>();
        foreach (var placer in RoomPlacer.All)
        {
            if (placer == null) continue;
            if (IsInsideRoom(placer.transform.position)) return true;
            // Dithering centré sur le ghost quand le joueur place un meuble dans cette chambre
            var fp = placer.GetComponent<FurniturePlacer>();
            if (fp != null && fp.IsPlacing && fp.TargetRoom == room) return true;
        }
        return false;
    }

    bool IsOccludingAnyPlayer()
    {
        foreach (var placer in RoomPlacer.All)
        {
            if (placer == null) continue;

            // RW1 : caméra propre à CE joueur (écran splitté statique) — chaque joueur teste
            // l'occlusion depuis SA propre caméra, pas une Camera.main globale partagée.
            var cam = placer.GetComponent<TopDownController>()?.PlayerCamera;
            if (cam == null) continue;
            var camPos = cam.transform.position;

            var target = placer.transform.position + Vector3.up * playerHeightOffset;
            var dir    = target - camPos;
            float dist = dir.magnitude;
            if (dist < 0.01f) continue;
            int count = Physics.RaycastNonAlloc(camPos, dir / dist, HitBuffer, dist);
            for (int i = 0; i < count; i++)
            {
                var t = HitBuffer[i].transform;
                while (t != null)
                {
                    if (t == transform) return true;
                    t = t.parent;
                }
            }
        }
        return false;
    }

    /// <summary>Teste si worldPos est à l'intérieur des murs de la chambre (espace local).</summary>
    bool IsInsideRoom(Vector3 worldPos)
    {
        var local = transform.InverseTransformPoint(worldPos);
        return Mathf.Abs(local.x) < 0.5f && Mathf.Abs(local.z) < 0.5f;
    }
}
