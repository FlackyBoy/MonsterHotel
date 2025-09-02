using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(90)]
public class PlayerBuildController : MonoBehaviour
{
    [Header("Refs")]
    public PlayerInput playerInput;
    public BuildRules rules;
    public BuildCatalog catalog;
    public BuildContextDetector context;

    [Header("Ghost follow")]
    [SerializeField] float ghostDistance = 2.0f;
    [SerializeField] float rotateStepDeg = 15f;

    enum Mode { None, RoomGhost, ItemGhost }
    Mode mode;
    RoomTypeDef currRoom;
    ItemBlueprint currItem;
    PlacementGhost ghost;
    Quaternion ghostRot = Quaternion.identity;

    // Input actions
    InputAction actBuild, actPlace, actCancel, actRotCW, actRotCCW;

    void OnEnable()
    {
        CacheActions();
        EnableGameplayMap();
    }

    void Awake()
    {
        if (!playerInput) playerInput = GetComponent<PlayerInput>();
        if (!context) context = GetComponent<BuildContextDetector>();
    }

    void CacheActions()
    {
        var asset = playerInput ? playerInput.actions : null;
        if (asset == null) { Debug.LogError("[Build] PlayerInput.actions NULL"); return; }

        actBuild = asset.FindAction("Build", throwIfNotFound: false);
        actPlace = asset.FindAction("Place", throwIfNotFound: false);
        actCancel = asset.FindAction("Cancel", throwIfNotFound: false);
        actRotCW = asset.FindAction("RotateCW", throwIfNotFound: false);
        actRotCCW = asset.FindAction("RotateCCW", throwIfNotFound: false);
    }

    void EnableGameplayMap()
    {
        if (playerInput == null || playerInput.actions == null) return;
        var map = playerInput.actions.FindActionMap("Gameplay", true);
        if (!map.enabled) map.Enable();
    }

    void Update()
    {
        // 1) Toggle du mode build
        if (actBuild != null && actBuild.WasPressedThisFrame())
        {
            if (mode == Mode.None) StartGhostAuto();
            else StopGhost(); // re-appuyer sur Build ferme le mode
        }

        // Si pas en mode build, rien n’est visible
        if (mode == Mode.None) return;

        // 2) Rotation du ghost
        if (actRotCW != null && actRotCW.WasPressedThisFrame())
            ghostRot = Quaternion.Euler(0, +rotateStepDeg, 0) * ghostRot;
        if (actRotCCW != null && actRotCCW.WasPressedThisFrame())
            ghostRot = Quaternion.Euler(0, -rotateStepDeg, 0) * ghostRot;

        // 3) Placement du ghost autour du joueur + snap
        Vector3 target = transform.position + transform.forward * ghostDistance;
        float grid = mode == Mode.RoomGhost ? rules.gridSizeRoom : rules.gridSizeItem;
        target = Snap(target, grid);
        ghost.transform.SetPositionAndRotation(target, ghostRot);

        // 4) Validation
        bool ok; string reason;
        if (mode == Mode.RoomGhost)
        {
            var res = PlacementValidator.ValidateRoomPose(currRoom, rules, target, ghostRot);
            ok = res.ok; reason = res.reason;
        }
        else
        {
            var res = PlacementValidator.ValidateItemPose(currItem, rules, target, ghostRot, context.currentRoom, context.inHall);
            ok = res.ok; reason = res.reason;
        }
        ghost.SetOK(ok);
        if (!ok) Debug.Log($"[Build] NOK: {reason}");

        // 5) Place uniquement sur input Place, puis on ferme le mode
        if (ok && actPlace != null && actPlace.WasPressedThisFrame())
        {
            Place(target, ghostRot);
            StopGhost(); // IMPORTANT: cache le ghost après pose
        }

        // 6) Annuler
        if (actCancel != null && actCancel.WasPressedThisFrame())
            StopGhost();
    }


    void StartGhostAuto()
    {
        // Dans une chambre -> item ; sinon -> room
        if (context != null && context.currentRoom != null)
        {
            var list = FilterItemsForRoomContext();
            if (list.Length == 0) return;
            StartItemGhost(list[0]); // simple: premier item valide
        }
        else
        {
            if (catalog == null || catalog.roomTypes == null || catalog.roomTypes.Length == 0) return;
            StartRoomGhost(catalog.roomTypes[0]); // simple: premier type
        }
    }

    void StartRoomGhost(RoomTypeDef def)
    {
        StopGhost();
        currRoom = def;
        mode = Mode.RoomGhost;
        ghostRot = Quaternion.identity;

        ghost = new GameObject($"Ghost_{def.displayName}").AddComponent<PlacementGhost>();
        ghost.gameObject.layer = LayerMask.NameToLayer("Ghost");
        var ok = rules && rules.ghostOK ? rules.ghostOK : new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Color.green };
        var nok = rules && rules.ghostNOK ? rules.ghostNOK : new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Color.red };
        ghost.InitFromPrefab(def.prefabGhostOptional, def.footprintSize, ok, nok);
    }

    void StartItemGhost(ItemBlueprint def)
    {
        StopGhost();
        currItem = def;
        mode = Mode.ItemGhost;
        ghostRot = Quaternion.identity;

        ghost = new GameObject($"Ghost_{def.displayName}").AddComponent<PlacementGhost>();
        ghost.gameObject.layer = LayerMask.NameToLayer("Ghost");
        var ok = rules && rules.ghostOK ? rules.ghostOK : new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Color.green };
        var nok = rules && rules.ghostNOK ? rules.ghostNOK : new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Color.red };
        ghost.InitFromPrefab(def.prefabGhostOptional, def.size, ok, nok);
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

            // Volume enfant si absent
            if (!room.volume)
            {
                var volGO = new GameObject("Volume");
                volGO.layer = LayerMask.NameToLayer("RoomVolume");
                volGO.transform.SetParent(go.transform, false);
                var box = volGO.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = currRoom.footprintCenter;
                box.size = new Vector3(currRoom.footprintSize.x, Mathf.Max(currRoom.footprintSize.y, 2f), currRoom.footprintSize.z);
                var rv = volGO.AddComponent<RoomVolume>();
                rv.ownerRoom = room;
            }

            // TODO: Budget.TrySpend(currRoom.costCents)
            // TODO: GameplayEventBus.Raise(new RoomBuilt(...))
        }
        else
        {
            Transform parent = context.inHall ? FindOrCreate("Hall/Content")
                               : (context.currentRoom ? context.currentRoom.ownerRoom.transform : null);
            var go = Instantiate(currItem.prefabFinal, pos, rot, parent);
            // TODO: Budget.TrySpend(currItem.costCents)
            // TODO: GameplayEventBus.Raise(new DecorPlaced(...))
        }
    }

    Transform FindOrCreate(string path)
    {
        var existing = GameObject.Find(path);
        if (existing) return existing.transform;
        var parts = path.Split('/');
        Transform current = null;
        for (int i = 0; i < parts.Length; i++)
        {
            var name = parts[i];
            Transform t = current ? current.Find(name) : GameObject.Find(name)?.transform;
            if (!t)
            {
                var go = new GameObject(name);
                t = go.transform;
                if (current) t.SetParent(current);
            }
            current = t;
        }
        return current;
    }

    static Vector3 Snap(Vector3 p, float g)
    {
        return new Vector3(Mathf.Round(p.x / g) * g, p.y, Mathf.Round(p.z / g) * g);
    }

    // ————— Filtrage items selon contexte —————
    ItemBlueprint[] FilterItemsForRoomContext()
    {
        if (catalog == null || catalog.items == null || catalog.items.Length == 0)
            return System.Array.Empty<ItemBlueprint>();

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
                if (it.allowedContext == BuildContext.Hall) continue;

                if (it.allowedRoomCategories != null && it.allowedRoomCategories.Length > 0)
                {
                    bool okCat = false;
                    for (int i = 0; i < it.allowedRoomCategories.Length; i++)
                        if (it.allowedRoomCategories[i] == roomCat) { okCat = true; break; }
                    if (!okCat) continue;
                }

                if (it.allowedMonsterTypes != null && it.allowedMonsterTypes.Length > 0)
                {
                    bool okMon = false;
                    for (int i = 0; i < it.allowedMonsterTypes.Length; i++)
                        if (roomMonster == it.allowedMonsterTypes[i]) { okMon = true; break; }
                    if (!okMon) continue;
                }

                if (allowedTags != null && allowedTags.Count > 0)
                {
                    bool okTag = false;
                    if (it.tags != null && it.tags.Length > 0)
                    {
                        for (int i = 0; i < it.tags.Length; i++)
                            if (allowedTags.Contains(it.tags[i])) { okTag = true; break; }
                    }
                    else okTag = true;
                    if (!okTag) continue;
                }
                list.Add(it);
            }
            return list.ToArray();
        }

        if (context != null && context.inHall)
        {
            var list = new List<ItemBlueprint>(catalog.items.Length);
            foreach (var it in catalog.items)
                if (it.allowedContext != BuildContext.Room) list.Add(it);
            return list.ToArray();
        }

        return System.Array.Empty<ItemBlueprint>();
    }
}
