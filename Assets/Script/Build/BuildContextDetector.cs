using UnityEngine;

// Détecte où se trouve le joueur : dans un RoomVolume ? dans Hall ?
public class BuildContextDetector : MonoBehaviour
{
    public RoomVolume currentRoom;   // null si hors chambre
    public bool inHall;              // true si dans HallArea

    void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out RoomVolume room)) currentRoom = room;
        if (other.CompareTag("HallArea")) inHall = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out RoomVolume room) && currentRoom == room) currentRoom = null;
        if (other.CompareTag("HallArea")) inHall = false;
    }
}
