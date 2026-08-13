using UnityEngine;

/// <summary>
/// Définit un type de chambre.
/// Crée de nouveaux types via clic-droit → Create → MonsterHotel → Room Type.
/// Assigne-les dans RoomData.roomType et MonsterData.compatibleRoomTypes.
/// </summary>
[CreateAssetMenu(fileName = "RoomType_New", menuName = "MonsterHotel/Room Type")]
public class RoomTypeData : ScriptableObject
{
    [Tooltip("Nom affiché dans l'UI et les logs")]
    public string typeName = "Standard";
}
