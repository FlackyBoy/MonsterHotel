using UnityEngine;

public enum ItemAnchor { Sol, Mur }
public enum BuildContext { Room, Hall }

[CreateAssetMenu(fileName = "ItemBlueprint", menuName = "MonsterHotel/Item Blueprint")]
public class ItemBlueprint : ScriptableObject
{
    [Header("Visuel et prefab")]
    public string displayName;
    public GameObject prefabFinal;          // prefab posé (Layer = Item, colliders non-trigger)
    public GameObject prefabGhostOptional;  // variante allégée (facultatif)

    [Header("Empreinte de placement")]
    public Vector3 size = new Vector3(1, 1, 1);    // boîte de validation (mètres)
    public Vector3 center = Vector3.zero;        // offset si pivot non centré
    public ItemAnchor anchor = ItemAnchor.Sol;   // Sol ou Mur

    [Header("Contexte d’autorisation")]
    public BuildContext allowedContext = BuildContext.Room; // Room ou Hall
    public RoomCategory[] allowedRoomCategories;            // optionnel
    public UnityEngine.Object[] allowedMonsterTypes;        // optionnel (laisse vide si tu n’as pas de types)
    public string[] tags;                                   // optionnel

    [Header("Coût")]
    public int costCents = 0;
}
