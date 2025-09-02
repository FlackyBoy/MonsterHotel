using System.Collections.Generic;
using UnityEngine;

public enum RoomCategory { Chambre, Restaurant, Divertissement }

[CreateAssetMenu(fileName = "RoomType", menuName = "MonsterHotel/Room Type")]
public class RoomTypeDef : ScriptableObject
{
    [Header("Identité")]
    public string displayName;
    public RoomCategory category;
    [Tooltip("Optionnel pour chambre par type de monstre")]
    public string monsterType; // vide si non applicable

    [Header("Coût (centimes)")]
    public int costCents = 6000;

    [Header("Prefabs")]
    public GameObject prefabFinal;
    public GameObject prefabGhostOptional; // sinon on génère un cube footprint

    [Header("Empreinte (local)")]
    public Vector3 footprintSize = new Vector3(3f, 2.8f, 3f); // largeur, hauteur, profondeur
    public Vector3 footprintCenter = Vector3.zero;

    [Header("Portes (points locaux)")]
    public List<Vector3> doorsLocal = new List<Vector3>(); // ex: (0,0, +1.5f) pour porte sur face Z+

    [Header("Tags de filtrage items autorisés")]
    public List<string> allowedItemTags = new List<string>();
}
