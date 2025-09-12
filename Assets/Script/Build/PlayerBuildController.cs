using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

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

    [Header("Build UI")]
    public BuildPickerUI pickerPrefab;   // drag le prefab ItemPickerPanel (avec BuildPickerUI dessus)
    public Canvas uiCanvas;              // drag BuildUI (Canvas)
    BuildPickerUI activePicker;
   
    [SerializeField] string buildUICanvasName = "BuildUI";
    [SerializeField] string pickerResourcePath = "UI/ItemPickerPanel";

    enum Mode { None, RoomGhost, ItemGhost }
    Mode mode;
    RoomTypeDef currRoom;
    ItemBlueprint currItem;
    PlacementGhost ghost;
    Quaternion ghostRot = Quaternion.identity;

    // Input actions
    InputAction actBuild, actPlace, actCancel, actRotCW, actRotCCW, actNext, actPrev;

    void OnEnable()
    {
        CacheActions();
        EnableGameplayMap();
        ResolveUIOnce();
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
        // Si le picker est ouvert, on le pilote au pad/clavier
        if (activePicker)
        {
            // === ITEM GHOST ===
            if (mode == Mode.ItemGhost && currItem != null && ghost != null)
            {
                // ---- pose devant le joueur ----
                Vector3 fwd = Camera.main
                    ? Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized
                    : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                if (fwd.sqrMagnitude < 1e-4f) fwd = transform.forward;

                const float previewDist = 2.0f;
                Vector3 previewTarget = transform.position + fwd * previewDist;

                Vector3 pos = previewTarget;
                if (Physics.Raycast(previewTarget + Vector3.up * 5f, Vector3.down,
                    out var hitG, 20f, rules.groundMask, QueryTriggerInteraction.Ignore))
                    pos = hitG.point;

                // rotation en cours
                Quaternion rot = ghost.transform.rotation;

                // entrées rotation (binds: RotateCW / RotateCCW)
                if (actRotCW != null && actRotCW.WasPressedThisFrame()) rot *= Quaternion.Euler(0f, +90f, 0f);
                if (actRotCCW != null && actRotCCW.WasPressedThisFrame()) rot *= Quaternion.Euler(0f, -90f, 0f);

                // auto-orientation
                if (currItem.anchor == ItemAnchor.Sol)
                {
                    Vector3 yaw = fwd.sqrMagnitude > 0 ? fwd : ghost.transform.forward;
                    rot = Quaternion.LookRotation(yaw, Vector3.up);
                }
                else // Mur
                {
                    const float wallDist = 2.5f;
                    if (Physics.SphereCast(transform.position + Vector3.up * 1.2f, 0.15f, fwd,
                        out var hitW, wallDist, LayerMask.GetMask("Room"), QueryTriggerInteraction.Ignore))
                    {
                        pos = hitW.point + hitW.normal * 0.02f;
                        Vector3 face = Vector3.ProjectOnPlane(-hitW.normal, Vector3.up).normalized;
                        if (face.sqrMagnitude > 1e-6f)
                            rot = Quaternion.LookRotation(face, Vector3.up);
                    }
                }

                // applique UNE FOIS (pas d’autre SetPositionAndRotation ailleurs)
                ghost.transform.SetPositionAndRotation(pos, rot);

                // validation chambre
                RoomVolume rv = (context != null) ? context.currentRoom : null;
                bool inHall = (context != null) && context.inHall;
                var res = PlacementValidator.ValidateItemPose(currItem, rules, pos, rot, rv, inHall);
                ghost.SetOK(res.ok);

                if (res.ok && actPlace != null && actPlace.WasPressedThisFrame())
                {
                    PlaceItem(pos, rot);
                    StopGhost();
                    return;
                }
                if (actCancel != null && actCancel.WasPressedThisFrame())
                {
                    StopGhost();
                    return;
                }
                if (actRotCW != null && actRotCW.WasPressedThisFrame()) Debug.Log("RotCW");

                return;
            }





            // === ITEM GHOST HANDLING END ===

            bool confirmed = false;

            if (actNext != null && actNext.WasPressedThisFrame()) activePicker.Next();
            if (actPrev != null && actPrev.WasPressedThisFrame()) activePicker.Prev();

            if (actPlace != null && actPlace.WasPressedThisFrame())
            {
                activePicker.Confirm();     // => OnItemPicked -> StartItemGhost()
                confirmed = true;
            }
            if (actCancel != null && actCancel.WasPressedThisFrame())
            {
                ClosePickerIfAny();
                return;
            }

            // Si pas de confirmation ce frame, on reste en UI-only.
            if (!confirmed) return;
            // Si confirmé, activePicker est maintenant nul, on laisse la suite de Update() gérer le ghost.
        }


        if (actBuild != null && actBuild.WasPressedThisFrame())
        {
            if (activePicker) { ClosePickerIfAny(); return; }
            if (mode != Mode.None) { StopGhost(); return; }

            // Si des items sont disponibles dans le contexte (chambre ou hall) -> ouvrir le picker
            var items = FilterItemsForRoomContext();
            if (items != null && items.Length > 0) { OpenItemPicker(); }
            else
            {
                // Sinon on passe en mode "poser une pièce" (fallback)
                if (catalog && catalog.roomTypes != null && catalog.roomTypes.Length > 0)
                    StartRoomGhost(catalog.roomTypes[0]);
                else
                    Debug.Log("[Build] Rien à construire.");
            }
        }

        // Annulation ferme tout
        if (actCancel != null && actCancel.WasPressedThisFrame())
        {
            StopGhost();
            ClosePickerIfAny();
        }

        // 2) Rotation du ghost
        if (actRotCW != null && actRotCW.WasPressedThisFrame())
            ghostRot = Quaternion.Euler(0, +rotateStepDeg, 0) * ghostRot;
        if (actRotCCW != null && actRotCCW.WasPressedThisFrame())
            ghostRot = Quaternion.Euler(0, -rotateStepDeg, 0) * ghostRot;

        // 3) Placement du ghost autour du joueur + snap
        Vector3 posY = new Vector3(transform.position.x,0, transform.position.z);
        Vector3 target = posY + transform.forward * ghostDistance;
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

    void OpenItemPicker()
    {
        ResolveUIOnce();
        if (!uiCanvas || !pickerPrefab) { Debug.LogError("[Build] UI introuvable. Canvas ou PickerPrefab manquant."); return; }

        var items = FilterItemsForRoomContext();
        if (items == null || items.Length == 0)
        {
            Debug.Log("[Build] Aucun item disponible ici.");
            return;
        }
        if (!pickerPrefab || !uiCanvas) { Debug.LogError("[Build] PickerPrefab ou Canvas manquant."); return; }

        activePicker = Instantiate(pickerPrefab, uiCanvas.transform);
        var rt = activePicker.GetComponent<RectTransform>();
        if (playerInput && rt)
        {
            if (playerInput.playerIndex % 2 == 0) { rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(20, -20); }
            else { rt.anchorMin = new Vector2(1, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 1); rt.anchoredPosition = new Vector2(-20, -20); }
        }
        activePicker.Show(items, OnItemPicked);
    }

    void OnItemPicked(ItemBlueprint it)
    {
        StartItemGhost(it);      // ghost item
    }

    void ClosePickerIfAny()
    {
        if (activePicker)
        {
            activePicker.Hide();
            Destroy(activePicker.gameObject);
            activePicker = null;
        }
    }
    void ResolveUIOnce()
    {
        // 1) Canvas par nom, incluant les objets inactifs
        if (!uiCanvas)
            uiCanvas = FindCanvasByName(buildUICanvasName);

        // 2) Template BuildPickerUI présent sous le Canvas (désactivé dans la scène)
        if (!pickerPrefab && uiCanvas)
            pickerPrefab = uiCanvas.GetComponentsInChildren<BuildPickerUI>(true).FirstOrDefault();

        EnsureEventSystem();
    }

    static Canvas FindCanvasByName(string name)
    {
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        var byName = canvases.FirstOrDefault(c => c && c.name.Equals(name, StringComparison.Ordinal));
        return byName ? byName : canvases.FirstOrDefault();
    }

    static void EnsureEventSystem()
    {
        var es = UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
        if (!es)
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }
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
            Physics.SyncTransforms();

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
            Physics.SyncTransforms();
        }
    }
    void PlaceItem(Vector3 pos, Quaternion rot)
    {
        if (!currItem || !currItem.prefabFinal)
        {
            Debug.LogError("[Build] prefabFinal manquant sur l’item.");
            return;
        }

        var go = Instantiate(currItem.prefabFinal, pos + rot * currItem.center, rot);
        go.layer = LayerMask.NameToLayer("Item");
        Physics.SyncTransforms();
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
                return Array.Empty<ItemBlueprint>();

            bool inRoom = context != null && context.currentRoom != null;
            bool inHall = context != null && context.inHall;

            // Infos de la chambre courante
            Room ownerRoom = null;
            RoomCategory roomCat = RoomCategory.Chambre;
            string roomMonster = null;
            HashSet<string> allowedTags = null;

            if (inRoom)
            {
                ownerRoom = context.currentRoom.ownerRoom;
                bool hasRoom = ownerRoom != null && ownerRoom.type != null;

                roomCat = hasRoom ? ownerRoom.type.category : RoomCategory.Chambre;
                roomMonster = hasRoom ? ownerRoom.type.monsterType : null;

                var tagsList = hasRoom ? ownerRoom.type.allowedItemTags : null;
                if (tagsList != null && tagsList.Count > 0)
                    allowedTags = new HashSet<string>(tagsList, StringComparer.OrdinalIgnoreCase);
            }

            var list = new List<ItemBlueprint>(catalog.items.Length);

            foreach (var it in catalog.items)
            {
                if (!it) continue;

                // Contexte Room / Hall
                if (inRoom && it.allowedContext != BuildContext.Room) continue;
                if (inHall && it.allowedContext != BuildContext.Hall) continue;
                if (!inRoom && !inHall) continue;

                // Catégorie de chambre
                if (inRoom && it.allowedRoomCategories != null && it.allowedRoomCategories.Length > 0)
                {
                    bool okCat = false;
                    for (int i = 0; i < it.allowedRoomCategories.Length; i++)
                        if (it.allowedRoomCategories[i] == roomCat) { okCat = true; break; }
                    if (!okCat) continue;
                }

                // Type de monstre (comparé au nom)
                if (inRoom && !string.IsNullOrEmpty(roomMonster) && it.allowedMonsterTypes != null && it.allowedMonsterTypes.Length > 0)
                {
                    bool okMon = false;
                    for (int i = 0; i < it.allowedMonsterTypes.Length; i++)
                    {
                        var obj = it.allowedMonsterTypes[i];
                        if (!obj) continue;
                        if (string.Equals(roomMonster, obj.name, StringComparison.OrdinalIgnoreCase))
                        { okMon = true; break; }
                    }
                    if (!okMon) continue;
                }

                // Tags autorisés par la chambre
                if (inRoom && allowedTags != null && allowedTags.Count > 0)
                {
                    bool okTag = false;
                    if (it.tags != null && it.tags.Length > 0)
                    {
                        for (int i = 0; i < it.tags.Length; i++)
                            if (allowedTags.Contains(it.tags[i])) { okTag = true; break; }
                    }
                    else okTag = true; // tolérant si l’item n’a pas de tags
                    if (!okTag) continue;
                }

                list.Add(it);
            }

            return list.ToArray();
        }
}
