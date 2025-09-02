// Assets/Scripts/Build/PlacementValidator.cs
using UnityEngine;

public static class PlacementValidator
{
    public struct Result { public bool ok; public string reason; }

    // PIECE : support + collisions (PAS de porte/couloir)
    public static Result ValidateRoomPose(RoomTypeDef def, BuildRules rules, Vector3 pos, Quaternion rot)
    {
        // 1) collisions avec Room/Item/NoBuild
        var half = def.footprintSize * 0.5f;
        if (Physics.CheckBox(pos + rot * def.footprintCenter, half, rot, rules.blockMask, QueryTriggerInteraction.Ignore))
            return new Result { ok = false, reason = "Collision" };

        // 2) support sol + pente
        if (!RaycastSupport(rules, pos, rot, half))
            return new Result { ok = false, reason = "Sol invalide" };

        // Pas de test de porte/couloir
        return new Result { ok = true };
    }

    // OBJET : inchangé (contexte chambre/hall, collisions, ancrage, containment)
    public static Result ValidateItemPose(ItemBlueprint def, BuildRules rules, Vector3 pos, Quaternion rot, RoomVolume roomOrNull, bool inHall)
    {
        if (def.allowedContext == BuildContext.Room && roomOrNull == null)
            return new Result { ok = false, reason = "Hors chambre" };
        if (def.allowedContext == BuildContext.Hall && !inHall)
            return new Result { ok = false, reason = "Hors hall" };

        var half = def.size * 0.5f;
        if (Physics.CheckBox(pos + rot * def.center, half, rot, rules.blockMask, QueryTriggerInteraction.Ignore))
            return new Result { ok = false, reason = "Collision" };

        if (def.anchor == ItemAnchor.Sol)
        {
            if (!RaycastSupport(rules, pos, rot, half))
                return new Result { ok = false, reason = "Sol invalide" };
        }
        else
        {
            // Ancrage mur simple
            Vector3 dir = rot * Vector3.forward;
            if (!Physics.Raycast(pos, dir, out _, 0.6f, rules.blockMask, QueryTriggerInteraction.Ignore))
                return new Result { ok = false, reason = "Pas de mur" };
        }

        if (def.allowedContext != BuildContext.Hall && roomOrNull != null)
        {
            if (!roomOrNull.ContainsBox(pos + rot * def.center, half, rot))
                return new Result { ok = false, reason = "Hors volume chambre" };
        }

        return new Result { ok = true };
    }

    static bool RaycastSupport(BuildRules rules, Vector3 pos, Quaternion rot, Vector3 half)
    {
        Vector3 start = pos + rot * new Vector3(0, half.y + 0.1f, 0);
        if (!Physics.Raycast(start, Vector3.down, out var hit, rules.supportRay, rules.groundMask, QueryTriggerInteraction.Ignore))
            return false;
        float angle = Vector3.Angle(hit.normal, Vector3.up);
        return angle <= rules.maxSlopeDeg;
    }
}
