using UnityEngine;

public enum BuildContext { Room, Hall, Any }
public enum ItemAnchor { Sol, Mur }

[CreateAssetMenu(fileName = "ItemBlueprint", menuName = "MonsterHotel/Item Blueprint")]
public class ItemBlueprint : ScriptableObject
{
    [Header("Identité")]
    public string displayName;
    public string[] tags;                 // "Decor","Confort","Cuisine", etc.

    [Header("Contexte")]
    public BuildContext allowedContext = BuildContext.Room;
    public RoomCategory[] allowedRoomCategories;   // pour filtrer
    public string[] allowedMonsterTypes;           // pour chambres dédiées

    [Header("Coût (centimes)")]
    public int costCents = 1500;

    [Header("Pose")]
    public ItemAnchor anchor = ItemAnchor.Sol;
    public Vector3 size = new Vector3(1f, 1f, 1f);
    public Vector3 center = Vector3.zero;

    [Header("Prefabs")]
    public GameObject prefabFinal;
    public GameObject prefabGhostOptional;

    [Header("Effets simples")]
    public int priceDeltaPerNightCents;   // +€/nuit
    public int stylePoints;               // score décor
}
