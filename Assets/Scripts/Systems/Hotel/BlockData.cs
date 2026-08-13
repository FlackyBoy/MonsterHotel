using UnityEngine;

/// <summary>
/// Données d'un type de bloc destructible (Terre, Pierre...).
/// Crée via clic-droit → Create → Hotel → Block Data.
/// </summary>
[CreateAssetMenu(fileName = "BD_NouveauBloc", menuName = "Hotel/Block Data")]
public class BlockData : ScriptableObject
{
    [Header("Identité")]
    public string blockName    = "Terre";
    public Color  blockColor   = new Color(0.55f, 0.35f, 0.15f);
    [Tooltip("Optionnelle — nécessite un shader qui expose _BaseMap sur le matériau du bloc (URP Lit standard, ou MonsterHotel/RoomWallDither ou RoomWallFade si tu veux aussi le fondu par dithering). Laisser vide = couleur plate comme avant.")]
    public Texture2D blockTexture;

    [Header("Creusage")]
    [Tooltip("Coût en or pour détruire ce bloc")]
    public int   cost        = 50;
    [Tooltip("Durée totale de creusage en secondes (maintien de Interact)")]
    public float digDuration = 5f;
}
