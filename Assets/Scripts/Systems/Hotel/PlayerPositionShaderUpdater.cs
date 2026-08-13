using UnityEngine;

/// <summary>
/// Met à jour chaque frame les positions des joueurs comme propriétés globales du shader.
/// S'instancie automatiquement au démarrage — aucun setup manuel requis.
/// Utilisé par RoomWallFade pour le fondu radial autour des joueurs.
/// </summary>
public class PlayerPositionShaderUpdater : MonoBehaviour
{
    static readonly int PlayerPos0ID = Shader.PropertyToID("_PlayerPos0");
    static readonly int PlayerPos1ID = Shader.PropertyToID("_PlayerPos1");

    static readonly Vector4 FarAway = new(99999f, 0f, 99999f, 0f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        var go = new GameObject("[PlayerPositionShaderUpdater]");
        go.AddComponent<PlayerPositionShaderUpdater>();
        DontDestroyOnLoad(go);
    }

    void LateUpdate()
    {
        var placers = RoomPlacer.All;

        Shader.SetGlobalVector(PlayerPos0ID, placers.Count > 0 && placers[0] != null
            ? ToVec4(GetEffectivePos(placers[0])) : FarAway);

        Shader.SetGlobalVector(PlayerPos1ID, placers.Count > 1 && placers[1] != null
            ? ToVec4(GetEffectivePos(placers[1])) : FarAway);
    }

    // Si le joueur est en train de placer un meuble, utilise la position du ghost
    // pour que le dithering soit centré sur le curseur dans la pièce
    static Vector3 GetEffectivePos(RoomPlacer placer)
    {
        var fp = placer.GetComponent<FurniturePlacer>();
        if (fp != null && fp.IsPlacing && fp.GhostWorldPos != Vector3.zero)
            return fp.GhostWorldPos;
        return placer.transform.position;
    }

    static Vector4 ToVec4(Vector3 v) => new(v.x, v.y, v.z, 0f);
}
