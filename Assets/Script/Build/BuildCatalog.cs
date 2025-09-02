using UnityEngine;

[CreateAssetMenu(fileName = "BuildCatalog", menuName = "MonsterHotel/Build Catalog")]
public class BuildCatalog : ScriptableObject
{
    public RoomTypeDef[] roomTypes;
    public ItemBlueprint[] items;
}
