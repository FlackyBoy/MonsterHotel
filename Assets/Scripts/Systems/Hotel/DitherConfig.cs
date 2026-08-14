using UnityEngine;

/// <summary>
/// Configuration dithering — effet de transparence des murs.
/// Asset : Resources/Config/DitherConfig.asset (auto-chargé, voir Instance).
/// </summary>
[CreateAssetMenu(fileName = "DitherConfig", menuName = "Hotel/Config/Dither")]
public class DitherConfig : ScriptableObject
{
    static DitherConfig _instance;

    public static DitherConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<DitherConfig>("Config/DitherConfig");
            return _instance;
        }
    }

    [Header("Dithering murs")]
    [Tooltip("Vitesse de transition de l'effet de transparence")]
    public float ditherFadeSpeed = 6f;
    [Tooltip("Intensité maximale du dithering (0 = opaque, 1 = semi-transparent, >1 = effet renforcé selon le shader)")]
    public float ditherMaxAlpha = 0.82f;
    [Tooltip("Décalage vertical du test d'occlusion (pour viser la tête)")]
    public float ditherHeightOffset = 0.8f;
    [Tooltip("Rayon du dithering en unités monde autour du joueur (shader Fade Radius)")]
    public float ditherFadeRadius = 5f;
}
