using UnityEngine;

public static class PlacementValidator
{
    public struct Result { public bool ok; public string reason; }

    static readonly Collider[] tmp = new Collider[32];

    // Dans PlacementValidator.cs
    public static Result ValidateRoomPose(RoomTypeDef def, BuildRules rules, Vector3 pos, Quaternion rot)
    {
        Vector3 center = pos + rot * def.footprintCenter;
        Vector3 half = def.footprintSize * 0.5f;

        // 1) collisions solides (murs, items, no-build)
        if (Physics.CheckBox(center, half, rot, rules.blockMask, QueryTriggerInteraction.Ignore))
            return new Result { ok = false, reason = "Collision (murs/objets)" };

        // 2) interdit d'être DANS une autre pièce (chevauchement des RoomVolume) avec marge
        const float eps = 0.1f; // marge tolérante pour poser "mur à mur"
        Vector3 halfInterior = new Vector3(
            Mathf.Max(half.x - eps, 0.01f),
            Mathf.Max(half.y - eps, 0.01f),
            Mathf.Max(half.z - eps, 0.01f)
        );

        int n = Physics.OverlapBoxNonAlloc(center, halfInterior, tmp, rot, rules.roomVolumeMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            var rv = tmp[i] ? tmp[i].GetComponent<RoomVolume>() : null;
            if (rv != null)
                return new Result { ok = false, reason = "À l’intérieur d’une autre pièce" };
        }

        // 3) support sol + pente
        if (!RaycastSupport(rules, pos, rot, half))
            return new Result { ok = false, reason = "Sol invalide" };

        return new Result { ok = true };
    }


    public static Result ValidateItemPose(
    ItemBlueprint def, BuildRules rules,
    Vector3 pos, Quaternion rot,
    RoomVolume currentRoom, bool inHall)
    {
        Vector3 half = def.size * 0.5f;
        const float contactEps = 0.03f;
        Vector3 halfCol = new Vector3(half.x + contactEps, half.y + contactEps, half.z + contactEps);
        Vector3 center = pos + rot * def.center;

        // 1) collisions solides (murs, items, no-build) — le sol n'est PAS dans blockMask
        int blockMaskForItems = rules.blockMask & ~LayerMask.GetMask("BuildBounds");

        if (Physics.CheckBox(center, halfCol, rot, blockMaskForItems, QueryTriggerInteraction.Ignore))
            return new Result { ok = false, reason = "Collision (murs/objets)" };
        // Point de test pour appartenance à une pièce (ignore Y)
        Vector3 testPoint = center + Vector3.up * 0.2f;

        // 2) Hall: interdit d'entrer dans une RoomVolume
        if (inHall)
        {
            int nHall = Physics.OverlapSphereNonAlloc(testPoint, 0.05f, tmp, rules.roomVolumeMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < nHall; i++)
                if (tmp[i] && tmp[i].GetComponent<RoomVolume>() != null)
                    return new Result { ok = false, reason = "Hors hall" };
            return new Result { ok = true };
        }

        // 3) Chambre: doit être DANS la chambre courante et pas dans une autre
        if (!currentRoom)
            return new Result { ok = false, reason = "Pas de chambre" };

        // a) dans cette chambre via ClosestPoint (stable)
        var thisCol = currentRoom.GetComponent<Collider>();
        if (!thisCol)
            return new Result { ok = false, reason = "RoomVolume sans collider" };

        bool insideThis =
            (thisCol.ClosestPoint(testPoint) - testPoint).sqrMagnitude < 1e-6f;
        if (!insideThis)
            return new Result { ok = false, reason = "Hors de la chambre" };

        // b) pas dans une autre chambre
        int n = Physics.OverlapSphereNonAlloc(testPoint, 0.05f, tmp, rules.roomVolumeMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            var rv = tmp[i] ? tmp[i].GetComponent<RoomVolume>() : null;
            if (rv && rv != currentRoom)
                return new Result { ok = false, reason = "Dans une autre chambre" };
        }
        // DEBUG: qui collisionne ?
        int nDbg = Physics.OverlapBoxNonAlloc(center, halfCol, tmp, rot, blockMaskForItems, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < nDbg; i++)
            Debug.Log($"[ItemGhostDBG] hit {tmp[i].name} on layer {LayerMask.LayerToName(tmp[i].gameObject.layer)}");

        return new Result { ok = true };
    }




    static bool RaycastSupport(BuildRules rules, Vector3 pos, Quaternion rot, Vector3 half)
    {
        float up = half.y + 5f;
        Vector3 start = pos + Vector3.up * up;
        float maxDist = up + 20f;
        if (!Physics.Raycast(start, Vector3.down, out var hit, maxDist, rules.groundMask, QueryTriggerInteraction.Ignore))
            return false;
        float angle = Vector3.Angle(hit.normal, Vector3.up);
        return angle <= rules.maxSlopeDeg;
    }
}
