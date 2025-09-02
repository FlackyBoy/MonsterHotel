// Assets/Script/Build/PlacementValidator.cs
using UnityEngine;

public static class PlacementValidator
{
    public struct Result { public bool ok; public string reason; }

    // ── Pièce : collisions + support sol (PAS de couloir)
    public static Result ValidateRoomPose(RoomTypeDef def, BuildRules rules, Vector3 pos, Quaternion rot)
    {
        // 1) collisions contre Room/Item/NoBuild
        Vector3 half = def.footprintSize * 0.5f;
        if (Physics.CheckBox(pos + rot * def.footprintCenter, half, rot, rules.blockMask, QueryTriggerInteraction.Ignore))
            return new Result { ok = false, reason = "Collision" };

        // 2) support sol + pente
        if (!RaycastSupport(rules, pos, rot, half))
            return new Result { ok = false, reason = "Sol invalide" };

        return new Result { ok = true };
    }

    // ── Objet : contexte + collisions + ancrage + containment (dans la chambre si besoin)
    public static Result ValidateItemPose(ItemBlueprint def, BuildRules rules, Vector3 pos, Quaternion rot, RoomVolume roomOrNull, bool inHall)
    {
        // Contexte
        if (def.allowedContext == BuildContext.Room && roomOrNull == null)
            return new Result { ok = false, reason = "Hors chambre" };
        if (def.allowedContext == BuildContext.Hall && !inHall)
            return new Result { ok = false, reason = "Hors hall" };

        // Collision
        Vector3 half = def.size * 0.5f;
        if (Physics.CheckBox(pos + rot * def.center, half, rot, rules.blockMask, QueryTriggerInteraction.Ignore))
            return new Result { ok = false, reason = "Collision" };

        // Ancrage
        if (def.anchor == ItemAnchor.Sol)
        {
            if (!RaycastSupport(rules, pos, rot, half))
                return new Result { ok = false, reason = "Sol invalide" };
        }
        else // Mur
        {
            Vector3 dir = rot * Vector3.forward;
            if (!Physics.Raycast(pos, dir, out _, 0.6f, rules.blockMask, QueryTriggerInteraction.Ignore))
                return new Result { ok = false, reason = "Pas de mur" };
        }

        // Doit rester dans le volume de la chambre (si pas Hall)
        if (def.allowedContext != BuildContext.Hall && roomOrNull != null)
        {
            if (!roomOrNull.ContainsBox(pos + rot * def.center, half, rot))
                return new Result { ok = false, reason = "Hors volume chambre" };
        }

        return new Result { ok = true };
    }

    // Rayon long et pente tolérée
    static bool RaycastSupport(BuildRules rules, Vector3 pos, Quaternion rot, Vector3 half)
    {
        float up = half.y + 5f;
        Vector3 start = pos + Vector3.up * up;
        float maxDist = up + 20f;

        // Debug visuel (optionnel) :
        // Debug.DrawLine(start, start + Vector3.down * maxDist, Color.yellow, 0f, false);

        if (!Physics.Raycast(start, Vector3.down, out var hit, maxDist, rules.groundMask, QueryTriggerInteraction.Ignore))
            return false;

        float angle = Vector3.Angle(hit.normal, Vector3.up);
        return angle <= rules.maxSlopeDeg;
    }
}
