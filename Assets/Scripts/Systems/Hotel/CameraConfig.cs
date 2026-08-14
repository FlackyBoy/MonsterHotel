using UnityEngine;

/// <summary>
/// Configuration caméra — vue construction (dézoom) et écran scindé.
/// Asset : Resources/Config/CameraConfig.asset (auto-chargé, voir Instance).
/// </summary>
[CreateAssetMenu(fileName = "CameraConfig", menuName = "Hotel/Config/Camera")]
public class CameraConfig : ScriptableObject
{
    static CameraConfig _instance;

    public static CameraConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<CameraConfig>("Config/CameraConfig");
            return _instance;
        }
    }

    [Header("Vue construction (RW2 — dézoom caméra pendant le placement)")]
    [Tooltip("Distance additionnelle de la caméra pendant la construction (dézoom). Valeur de départ — à toi d'ajuster.")]
    public float buildModeExtraDistance = 12f;
    [Tooltip("Vitesse de transition du dézoom caméra à l'entrée/sortie du mode construction")]
    public float buildModeZoomLerpSpeed = 4f;
    [Tooltip("Vitesse de recadrage de la caméra vers le curseur de placement pendant la construction")]
    public float buildModeCameraFollowSpeed = 15f;

    [Header("Écran scindé (RW1 — split statique + fusion caméra)")]
    [Tooltip("Distance caméra au-dessus des joueurs (vue top-down)")]
    public float splitScreenCameraHeight = 22f;
    [Tooltip("Distance entre les deux joueurs en dessous de laquelle l'écran ne se scinde plus (une seule caméra plein écran cadre les deux). Valeur de départ — à toi d'ajuster.")]
    public float splitScreenMergeDistance = 15f;
    [Tooltip("Marge autour du seuil pour éviter un flicker split/fusion pile à la limite")]
    public float splitScreenMergeHysteresis = 3f;
    [Tooltip("En fusion, dézoom additionnel par unité de distance entre les deux joueurs")]
    public float splitScreenMergeDistancePerUnit = 0.6f;
    [Tooltip("Vitesse de transition (lerp/seconde) entre écran scindé et fusionné — plus petit = transition plus lente/fluide, plus grand = plus rapide/franche")]
    public float splitScreenMergeTransitionSpeed = 2f;
    [Tooltip("Épaisseur du trait de séparation en pixels (visible uniquement en mode scindé)")]
    public float splitScreenDividerWidth = 4f;
    public Color splitScreenDividerColor = Color.black;
}
