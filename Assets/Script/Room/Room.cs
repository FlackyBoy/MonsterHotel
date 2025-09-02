using UnityEngine;

public class Room : MonoBehaviour
{
    public RoomTypeDef type;
    public RoomVolume volume; // enfant avec BoxCollider isTrigger

    void Awake()
    {
        if (!volume) volume = GetComponentInChildren<RoomVolume>();
        if (volume) volume.ownerRoom = this;
    }
}
