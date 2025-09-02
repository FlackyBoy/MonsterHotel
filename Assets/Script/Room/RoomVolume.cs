using UnityEngine;

// Mettre ce composant sur l’enfant "Volume" de la chambre (BoxCollider isTrigger)
[RequireComponent(typeof(BoxCollider))]
public class RoomVolume : MonoBehaviour
{
    public Room ownerRoom;
    public BoxCollider box; // auto-assign

    void Awake()
    {
        if (!box) box = GetComponent<BoxCollider>();
        if (box && !box.isTrigger) box.isTrigger = true;
    }

    public bool ContainsBox(Vector3 worldCenter, Vector3 worldHalfExtents, Quaternion worldRot)
    {
        // Test approximatif : tous les sommets de l’AABB locale dans le volume
        // Simplifié: on vérifie l’intersection OverlapBox contre ce trigger uniquement.
        Collider[] hits = Physics.OverlapBox(worldCenter, worldHalfExtents, worldRot, 1 << gameObject.layer, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
            if (hits[i] == box) return true;
        return false;
    }
}
