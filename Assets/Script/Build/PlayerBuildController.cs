using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;
using System.Collections.Generic;


// Par joueur : gère le ghost et la pose. Menu UI minimal viendra après.
[DefaultExecutionOrder(90)]
public class PlayerBuildController : MonoBehaviour
{
    [Header("Refs")]
    public PlayerInput playerInput;         // assigne le PlayerInput de ce joueur
    public BuildRules rules;                // asset BuildRules
    public BuildCatalog catalog;            // asset BuildCatalog
    public BuildContextDetector context;    // sur le joueur (trigger)

    [Header("Ghost follow")]
    public float ghostDistance = 2.0f;
    public float rotateStepDeg = 15f;
    [SerializeField] bool debugLogs = true;
    // état courant
    enum Mode { None, RoomGhost, ItemGhost }
    Mode mode;
    RoomTypeDef currRoom;
    ItemBlueprint currItem;
    PlacementGhost ghost;
    Quaternion ghostRot = Quaternion.identity;

    // actions
    InputAction actBuild, actPlace, actCancel, actRotCW, actRotCCW, actNext, actPrev, actSwitch;

    int selIndex = 0;

    void Awake()
    {
        if (!playerInput) playerInput = GetComponent<PlayerInput>();
        if (!context) context = GetComponent<BuildContextDetector>();

        var a = playerInput ? playerInput.actions : null;
        if (a == null)
        {
            Debug.LogError("[Build] PlayerInput.actions est NULL. Assigne MovementActions sur PlayerInput.");
            return;
        }

        // Log les maps et actions trouvées
        if (debugLogs)
        {
            var maps = string.Join(", ", a.actionMaps.Select(m => m.name));
            Debug.Log($"[Build] Maps actives dans l’asset: {maps}");
            var gameplay = a.FindActionMap("Gameplay", true);
            var acts = string.Join(", ", gameplay.actions.Select(x => x.name));
            Debug.Log($"[Build] Actions dans Gameplay: {acts}");
            Debug.Log($"[Build] DefaultActionMap PlayerInput: {playerInput.defaultActionMap}");
        }

        actBuild = a["Build"];
        actPlace = a["Place"];
        actCancel = a["Cancel"];
        actRotCW = a["RotateCW"];
        actRotCCW = a["RotateCCW"];
        actNext = a["Next"];
        actPrev = a["Prev"];
        actSwitch = a["Switch"];

        // Garde-fou: si Build est introuvable, on le dit clairement
        if (actBuild == null)
            Debug.LogError("[Build] Action 'Build' introuvable dans la map 'Gameplay'. Vérifie le NOM EXACT et sauvegarde l’asset.");
    }


    void Update()
    {
        // Debug sans UI : appuie Build -> démarre un ghost selon contexte
        if (actBuild != null && actBuild.WasPressedThisFrame())
        {
            StartGhostAuto();
        }
        if (actBuild != null && actBuild.WasPressedThisFrame())
            Debug.Log("[Build] Input Action 'Build' fired");
        if (mode != Mode.None)
        {
            if (actRotCW != null && actRotCW.WasPressedThisFrame()) ghostRot = Quaternion.Euler(0, rotateStepDeg, 0) * ghostRot;
            if (actRotCCW != null && actRotCCW.WasPressedThisFrame()) ghostRot = Quaternion.Euler(0, -rotateStepDeg, 0) * ghostRot;

            // déplacement autour du joueur + snap
            Vector3 target = transform.position + transform.forward * ghostDistance;
            float grid = mode == Mode.RoomGhost ? rules.gridSizeRoom : rules.gridSizeItem;
            target = Snap(target, grid);
            ghost.transform.SetPositionAndRotation(target, ghostRot);

            // validation
            bool ok = false; string reason = "";
            if (mode == Mode.RoomGhost)
            {
                var res = PlacementValidator.ValidateRoomPose(currRoom, rules, target, ghostRot);
                ok = res.ok; reason = res.reason;
            }
            else if (mode == Mode.ItemGhost)
            {
                var res = PlacementValidator.ValidateItemPose(currItem, rules, target, ghostRot, context.currentRoom, context.inHall);
                ok = res.ok; reason = res.reason;
            }
            ghost.SetOK(ok);

            // place
            if (ok && actPlace != null && actPlace.WasPressedThisFrame())
            {
                Place(target, ghostRot);
                StopGhost();
            }

            // cancel
            if (actCancel != null && actCancel.WasPressedThisFrame())
            {
                StopGhost();
            }
        }
    }

    void StartGhostAuto()
    {
        // Forcer une chambre pour debug, index 0 du catalog
        if (catalog == null || catalog.roomTypes == null || catalog.roomTypes.Length == 0)
        {
            Debug.LogError("[Build] Catalog.roomTypes vide");
            return;
        }
        StartRoomGhost(catalog.roomTypes[0]);
    }


    void StartRoomGhost(RoomTypeDef def)
    {
        StopGhost();
        currRoom = def;
        mode = Mode.RoomGhost;
        ghostRot = Quaternion.identity;
        ghost = new GameObject($"Ghost_{def.displayName}").AddComponent<PlacementGhost>();
        ghost.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast"); // couche neutre
        ghost.InitFromPrefab(def.prefabGhostOptional, def.footprintSize, rules.ghostOK, rules.ghostNOK);
    }

    void StartItemGhost(ItemBlueprint def)
    {
        StopGhost();
        currItem = def;
        mode = Mode.ItemGhost;
        ghostRot = Quaternion.identity;
        ghost = new GameObject($"Ghost_{def.displayName}").AddComponent<PlacementGhost>();
        ghost.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        ghost.InitFromPrefab(def.prefabGhostOptional, def.size, rules.ghostOK, rules.ghostNOK);
    }

    void StopGhost()
    {
        if (ghost) Destroy(ghost.gameObject);
        ghost = null;
        currRoom = null;
        currItem = null;
        mode = Mode.None;
    }

    void Place(Vector3 pos, Quaternion rot)
    {
        if (mode == Mode.RoomGhost)
        {
            var go = Instantiate(currRoom.prefabFinal, pos, rot);
            var room = go.GetComponent<Room>();
            if (!room) room = go.AddComponent<Room>();
            room.type = currRoom;

            // Assure un volume enfant
            if (!room.volume)
            {
                var volGO = new GameObject("Volume");
                volGO.transform.SetParent(go.transform, false);
                var box = volGO.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = currRoom.footprintCenter;
                box.size = new Vector3(currRoom.footprintSize.x, Mathf.Max(currRoom.footprintSize.y, 2f), currRoom.footprintSize.z);
                var rv = volGO.AddComponent<RoomVolume>();
                rv.ownerRoom = room;
            }

            // TODO: Budget.TrySpend(currRoom.costCents) avant d'instancier
            // TODO: GameplayEventBus.Raise(new RoomBuilt(...))
        }
        else if (mode == Mode.ItemGhost)
        {
            Transform parent = context.inHall ? FindOrCreate("Hall/Content") :
                               (context.currentRoom ? context.currentRoom.ownerRoom.transform : null);
            var go = Instantiate(currItem.prefabFinal, pos, rot, parent);
            // TODO: Budget.TrySpend(currItem.costCents)
            // TODO: GameplayEventBus.Raise(new DecorPlaced(...))
        }
    }

    Transform FindOrCreate(string path)
    {
        var root = GameObject.Find(path);
        if (root) return root.transform;
        var parts = path.Split('/');
        Transform current = null;
        for (int i = 0; i < parts.Length; i++)
        {
            var name = parts[i];
            Transform t = (current ? current.Find(name) : GameObject.Find(name)?.transform);
            if (!t) { var go = new GameObject(name); t = go.transform; if (current) t.SetParent(current); }
            current = t;
        }
        return current;
    }

    static Vector3 Snap(Vector3 p, float g)
    {
        return new Vector3(
            Mathf.Round(p.x / g) * g,
            p.y,
            Mathf.Round(p.z / g) * g
        );
    }
    ItemBlueprint[] FilterItemsForRoomContext()
    {
        // Sécurité
        if (catalog == null || catalog.items == null || catalog.items.Length == 0)
            return System.Array.Empty<ItemBlueprint>();

        // Dans une chambre → items autorisés pour cette chambre
        if (context != null && context.currentRoom != null)
        {
            var room = context.currentRoom.ownerRoom;
            var hasRoom = room != null && room.type != null;

            var roomCat = hasRoom ? room.type.category : RoomCategory.Chambre;
            var roomMonster = hasRoom ? room.type.monsterType : null;
            var allowedTags = hasRoom ? room.type.allowedItemTags : null;

            var list = new List<ItemBlueprint>(catalog.items.Length);
            foreach (var it in catalog.items)
            {
                // Contexte: pas d’items Hall-only dans une chambre
                if (it.allowedContext == BuildContext.Hall)
                    continue;

                // Filtre catégorie de pièce si défini sur l’item
                if (it.allowedRoomCategories != null && it.allowedRoomCategories.Length > 0)
                {
                    bool okCat = false;
                    for (int i = 0; i < it.allowedRoomCategories.Length; i++)
                        if (it.allowedRoomCategories[i] == roomCat) { okCat = true; break; }
                    if (!okCat) continue;
                }

                // Filtre type de monstre si défini sur l’item
                if (it.allowedMonsterTypes != null && it.allowedMonsterTypes.Length > 0)
                {
                    bool okMon = false;
                    for (int i = 0; i < it.allowedMonsterTypes.Length; i++)
                        if (roomMonster == it.allowedMonsterTypes[i]) { okMon = true; break; }
                    if (!okMon) continue;
                }

                // Filtre tags (optionnel) selon allowedItemTags de la RoomType
                if (allowedTags != null && allowedTags.Count > 0)
                {
                    bool okTag = false;
                    if (it.tags != null && it.tags.Length > 0)
                    {
                        for (int i = 0; i < it.tags.Length; i++)
                            if (allowedTags.Contains(it.tags[i])) { okTag = true; break; }
                    }
                    else
                    {
                        okTag = true; // item sans tag accepté
                    }
                    if (!okTag) continue;
                }

                list.Add(it);
            }
            return list.ToArray();
        }

        // Dans le hall → items Hall ou Any
        if (context != null && context.inHall)
        {
            var list = new List<ItemBlueprint>(catalog.items.Length);
            foreach (var it in catalog.items)
            {
                if (it.allowedContext == BuildContext.Room) continue; // exclut Room-only
                list.Add(it);
            }
            return list.ToArray();
        }

        // Zone neutre → pas d’items (on proposera des pièces)
        return System.Array.Empty<ItemBlueprint>();
    }

}
