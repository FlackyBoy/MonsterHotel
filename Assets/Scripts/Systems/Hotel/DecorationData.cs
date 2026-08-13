using UnityEngine;

/// <summary>
/// Données d'une décoration achetable et plaçable dans le hall de l'hôtel.
/// Crée via clic-droit → Create → MonsterHotel → Decoration Data.
/// </summary>
[CreateAssetMenu(fileName = "DecorationData", menuName = "MonsterHotel/Decoration Data")]
public class DecorationData : ScriptableObject
{
    [Header("Identité")]
    public string decorationName = "Décoration";
    public Sprite icon;

    [Header("Placement")]
    public int     cost = 100;
    public GameObject prefab;
    public Vector3 placementOffset;
    [Tooltip("Reste en mode placement après chaque pose (pour en placer plusieurs d'affilée)")]
    public bool multiPlace;

    [Header("Bonus hôtel")]
    [Tooltip("Points de Confort ajoutés au total. Chaque point améliore la satisfaction initiale des monstres.")]
    public float comfortBonus;
    [Tooltip("Points de Renommée ajoutés au total. Réduit l'intervalle de spawn et débloque les monstres légendaires.")]
    public float renownBonus;
}
