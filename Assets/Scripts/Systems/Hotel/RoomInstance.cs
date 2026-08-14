using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Composant posé sur chaque room instanciée dans le monde.
/// Tracks l'état runtime : occupant, propreté, réparation, mobilier.
/// </summary>
public class RoomInstance : MonoBehaviour
{
    // ─── Registre global ──────────────────────────────────────────

    public static readonly HashSet<RoomInstance> All = new();
    public static event System.Action OnAnyDestroyed;

    void Awake()     { All.Add(this); }
    void OnDestroy() { All.Remove(this); OnAnyDestroyed?.Invoke(); }

    // ─── Données statiques ────────────────────────────────────────

    /// <summary>Données ScriptableObject de cette chambre (type, coût, taille…).</summary>
    public RoomData Data { get; private set; }

    /// <summary>Coin bas-gauche (en cellules grille) occupé par cette chambre.</summary>
    public Vector2Int OriginCell    { get; private set; }

    /// <summary>Empreinte (en cellules) de cette chambre après rotation.</summary>
    public Vector2Int FootprintSize { get; private set; }

    /// <summary>
    /// Point d'entrée devant la porte (côté -Z local), 1.5 unités devant en world-space.
    /// Assez loin du mur pour être clairement sur le NavMesh principal.
    /// </summary>
    public Vector3 EntryPoint
    {
        get
        {
            var doorFace = transform.TransformPoint(new Vector3(0f, 0f, -0.5f));
            doorFace.y = 0f;
            return doorFace - transform.forward * 1.5f;
        }
    }

    // ─── État ─────────────────────────────────────────────────────

    public RoomState State    { get; private set; } = RoomState.Empty;
    public bool      IsDirty  { get; private set; }
    public bool      NeedsRepair { get; private set; }

    /// <summary>MonsterData du monstre actuellement dans la chambre (null si vide).</summary>
    public MonsterData CurrentMonster { get; private set; }

    /// <summary>GameObject du monstre en jeu (null si vide).</summary>
    public GameObject CurrentMonsterObject { get; private set; }

    // ─── Mobilier ─────────────────────────────────────────────────

    readonly List<FurnitureInstance> _furniture = new();
    readonly List<DebrisInstance>    _debris    = new();

    public IReadOnlyList<FurnitureInstance> PlacedFurniture => _furniture;

    /// <summary>True si tous les meubles obligatoires de RoomData sont posés.</summary>
    public bool IsFullyFurnished
    {
        get
        {
            if (Data?.requiredFurniture == null || Data.requiredFurniture.Length == 0) return true;
            foreach (var req in Data.requiredFurniture)
            {
                bool found = false;
                foreach (var fi in _furniture)
                    if (fi != null && fi.Data == req) { found = true; break; }
                if (!found) return false;
            }
            return true;
        }
    }

    /// <summary>True si ce meuble spécifique est déjà posé dans cette chambre.</summary>
    public bool HasFurniture(FurnitureData data)
    {
        foreach (var fi in _furniture)
            if (fi != null && fi.Data == data) return true;
        return false;
    }

    public void AddFurniture(FurnitureInstance fi)
    {
        if (!_furniture.Contains(fi))
        {
            _furniture.Add(fi);
        }
    }

    public void RemoveFurniture(FurnitureInstance fi)
    {
        _furniture.Remove(fi);
    }

    public void AddDebris(DebrisInstance d)
    {
        if (!_debris.Contains(d)) _debris.Add(d);
    }

    /// <summary>Appelé par DebrisInstance.PickUp() / OnDestroy().</summary>
    public void RemoveDebris(DebrisInstance d)
    {
        _debris.Remove(d);
        TryAutoClean();
    }

    /// <summary>
    /// Vérifie si tous les meubles sont propres et non-abimés et qu'il n'y a plus de détritus.
    /// Si oui, remet la chambre en état Empty.
    /// </summary>
    public void TryAutoClean()
    {
        if (State != RoomState.Dirty && State != RoomState.NeedsRepair) return;

        foreach (var fi in _furniture)
            if (fi != null && (fi.IsDirty || fi.IsDamaged)) return;

        if (_debris.Count > 0) return;

        IsDirty     = false;
        NeedsRepair = false;
        SetState(RoomState.Empty);
        Debug.Log($"[Chambre] {name} — entièrement nettoyée, redevient disponible (State=Empty, IsFullyFurnished={IsFullyFurnished}).");
    }

    // ─── Stats calculées ──────────────────────────────────────────

    /// <summary>Revenu total par nuit = base chambre + bonus de chaque meuble.</summary>
    public int TotalRevenue
    {
        get
        {
            int total = Data?.baseRevenue ?? 0;
            foreach (var fi in _furniture) if (fi?.Data != null) total += fi.Data.revenueBonus;
            return total;
        }
    }

    /// <summary>Attractivité totale = base chambre + bonus de chaque meuble.</summary>
    public int TotalAttractiveness
    {
        get
        {
            int total = Data?.attractiveness ?? 0;
            foreach (var fi in _furniture) if (fi?.Data != null) total += fi.Data.attractivenessBonus;
            return total;
        }
    }

    /// <summary>Qualité de la chambre (1–5), définie dans RoomData.</summary>
    public int Quality => Data?.quality ?? 1;

    // ─── Upgrade chambre ──────────────────────────────────────────

    public bool CanUpgrade => Data?.nextUpgrade != null;

    /// <summary>
    /// Améliore la chambre vers le niveau suivant.
    /// Met à jour la grille (libère l'ancien footprint, vérifie et occupe le nouveau).
    /// Supporte les changements de taille.
    /// </summary>
    public bool UpgradeRoom() => UpgradeRoom(out _);

    /// <summary>Variante avec raison d'échec (ex: affichage UI). Voir <see cref="UpgradeRoom()"/>.</summary>
    public bool UpgradeRoom(out string failReason)
    {
        failReason = null;

        if (!CanUpgrade)
        {
            failReason = "Pas d'amélioration disponible";
            return false;
        }

        if (!IsFullyFurnished)
        {
            failReason = "Chambre non meublée";
            return false;
        }

        var next = Data.nextUpgrade;
        var grid = GridManager.Instance;
        if (grid == null) return false;

        // ── Calcul du nouveau footprint ───────────────────────────
        float rotY       = transform.eulerAngles.y;
        var newFootprint = ComputeFootprint(next.size, rotY);

        // Déduit le nouvel origin à partir de la position monde (le GO est au centre du footprint)
        float newOxF = (transform.position.x - grid.origin.x) / grid.cellSize - newFootprint.x * 0.5f;
        float newOyF = (transform.position.z - grid.origin.z) / grid.cellSize - newFootprint.y * 0.5f;
        int   newOx  = Mathf.RoundToInt(newOxF);
        int   newOy  = Mathf.RoundToInt(newOyF);

        // ── Vérification des cellules ─────────────────────────────
        for (int x = newOx; x < newOx + newFootprint.x; x++)
        {
            for (int y = newOy; y < newOy + newFootprint.y; y++)
            {
                if (!grid.IsInBounds(x, y))
                {
                    failReason = "Pas assez de place pour agrandir la chambre";
                    return false;
                }
                // Libre si vide OU déjà occupé par cette chambre
                bool isOwnCell = x >= OriginCell.x && x < OriginCell.x + FootprintSize.x &&
                                 y >= OriginCell.y && y < OriginCell.y + FootprintSize.y;
                var cell = grid.GetCell(x, y);
                if (!isOwnCell && cell != null && cell.IsOccupied)
                {
                    failReason = "Pas assez de place pour agrandir la chambre";
                    return false;
                }
            }
        }

        // ── Dépense ───────────────────────────────────────────────
        if (!EconomyManager.Instance.TrySpend(Data.upgradeCost))
        {
            failReason = "Or insuffisant";
            return false;
        }

        // ── Mise à jour grille ────────────────────────────────────
        grid.FreeRect(OriginCell.x, OriginCell.y, FootprintSize.x, FootprintSize.y);
        grid.SetRect(newOx, newOy, newFootprint.x, newFootprint.y, CellType.Room);
        for (int x = newOx; x < newOx + newFootprint.x; x++)
            for (int y = newOy; y < newOy + newFootprint.y; y++)
                grid.PlaceOccupant(x, y, gameObject);

        InitCells(new Vector2Int(newOx, newOy), newFootprint);

        // ── Données + visuel ──────────────────────────────────────
        Data = next;
        name = next.roomName;

        // Recentre le GO exactement sur le nouveau footprint (correction d'arrondi éventuelle)
        float newCx = grid.origin.x + (newOx + newFootprint.x * 0.5f) * grid.cellSize;
        float newCz = grid.origin.z + (newOy + newFootprint.y * 0.5f) * grid.cellSize;
        transform.position = new Vector3(newCx, transform.position.y, newCz);

        transform.localScale = new Vector3(
            Data.size.x * grid.cellSize,
            Data.height,
            Data.size.y * grid.cellSize);

        SwapRoomVisual(next.prefab);

        OnStateChanged?.Invoke(this);
        return true;
    }

    /// <summary>Calcule la bounding-box en cellules d'une taille donnée à un angle de rotation.</summary>
    static Vector2Int ComputeFootprint(Vector2Int size, float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        float cos = Mathf.Abs(Mathf.Cos(rad));
        float sin = Mathf.Abs(Mathf.Sin(rad));
        int bw = Mathf.CeilToInt(size.x * cos + size.y * sin);
        int bh = Mathf.CeilToInt(size.x * sin + size.y * cos);
        return new Vector2Int(Mathf.Max(1, bw), Mathf.Max(1, bh));
    }

    // ─── Événements ───────────────────────────────────────────────

    public event System.Action<RoomInstance> OnStateChanged;
    public event System.Action<RoomInstance> OnOccupantChanged;

    // ─── Initialisation ───────────────────────────────────────────

    /// <summary>Appelé par RoomPlacer juste après l'instanciation.</summary>
    public void Init(RoomData data)
    {
        Data  = data;
        State = RoomState.Empty;

        // Ajoute automatiquement FacilityRoomInstance si c'est une salle commune
        if (data != null && data.isFacility)
        {
            if (GetComponent<FacilityRoomInstance>() == null)
                gameObject.AddComponent<FacilityRoomInstance>();
        }
    }

    /// <summary>Enregistre la position grille occupée (appelé par RoomPlacer après Init).</summary>
    public void InitCells(Vector2Int originCell, Vector2Int footprintSize)
    {
        OriginCell    = originCell;
        FootprintSize = footprintSize;
    }

    /// <summary>Supprime la chambre, rembourse le joueur et libère les cellules grille.</summary>
    public void Demolish(float refundRate = 1f)
    {
        if (State == RoomState.Occupied)
        {
            return;
        }

        // Détruit les meubles et rembourse au même taux que la chambre
        for (int i = _furniture.Count - 1; i >= 0; i--)
        {
            var fi = _furniture[i];
            if (fi != null)
            {
                int furnitureRefund = Mathf.RoundToInt((fi.Data?.cost ?? 0) * refundRate);
                if (furnitureRefund > 0) EconomyManager.Instance?.Earn(furnitureRefund);
                Destroy(fi.gameObject);
            }
        }
        _furniture.Clear();

        // Détruit les détritus au sol
        for (int i = _debris.Count - 1; i >= 0; i--)
            if (_debris[i] != null) Destroy(_debris[i].gameObject);
        _debris.Clear();

        int refund = Mathf.RoundToInt((Data?.cost ?? 0) * refundRate);
        EconomyManager.Instance?.Earn(refund);
        GridManager.Instance?.FreeRect(OriginCell.x, OriginCell.y, FootprintSize.x, FootprintSize.y);

        Destroy(gameObject);
    }

    // ─── API publique ─────────────────────────────────────────────

    /// <summary>Assigne un monstre à cette chambre. Retourne false si incompatible ou non-disponible.</summary>
    public bool Assign(MonsterData monster, GameObject monsterObject)
    {
        if (State != RoomState.Empty)
        {
            return false;
        }

        if (!IsFullyFurnished)
        {
            return false;
        }

        if (!monster.IsRoomCompatible(Data.roomType))
        {
            return false;
        }

        CurrentMonster       = monster;
        CurrentMonsterObject = monsterObject;
        SetState(RoomState.Occupied);
        OnOccupantChanged?.Invoke(this);

        return true;
    }

    /// <summary>Le monstre quitte la chambre. La chambre devient sale et les meubles peuvent être abimés.</summary>
    public void Vacate()
    {
        if (State != RoomState.Occupied) return;

        CurrentMonster       = null;
        CurrentMonsterObject = null;
        IsDirty = true;
        SetState(RoomState.Dirty);
        OnOccupantChanged?.Invoke(this);

        ApplyPostMonsterEffects();
    }

    void ApplyPostMonsterEffects()
    {
        bool forceAll  = HotelConfig.Debug != null && HotelConfig.Debug.debugForceFullDirtyOnVacate;
        bool anyDamaged = false;

        foreach (var fi in _furniture)
        {
            if (fi == null) continue;
            if (forceAll || Random.value < Data.furnitureDirtyChance)  fi.SetDirty(true);
            if (!forceAll && Random.value < Data.furnitureDamageChance) { fi.SetDamaged(true); anyDamaged = true; }
        }

        if (anyDamaged) NeedsRepair = true;

        SpawnDebris(forceAll);
    }

    void SpawnDebris(bool forceMax = false)
    {
        if (Data.debrisPrefabs == null || Data.debrisPrefabs.Length == 0) return;
        if (!forceMax && Data.maxDebris <= 0) return;

        var sc = transform.lossyScale;
        float hw = sc.x * 0.4f;
        float hd = sc.z * 0.4f;

        int maxD  = Mathf.Max(1, Data.maxDebris);
        int count = forceMax ? maxD : Random.Range(0, maxD + 1);
        for (int i = 0; i < count; i++)
        {
            var prefab = Data.debrisPrefabs[Random.Range(0, Data.debrisPrefabs.Length)];
            if (prefab == null) continue;

            // Cherche une position libre (max 8 essais)
            Vector3 pos = Vector3.zero;
            bool found = false;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                // XZ aléatoire dans la chambre, Y cherché par raycast pour se poser sur le sol
            var candidate = transform.position + new Vector3(
                    Random.Range(-hw, hw), 0f, Random.Range(-hd, hd));
            if (Physics.Raycast(new Vector3(candidate.x, candidate.y + 5f, candidate.z),
                                Vector3.down, out var floorHit, 10f))
                candidate.y = floorHit.point.y;

                bool overlaps = false;
                foreach (var fi in _furniture)
                {
                    if (fi == null) continue;
                    var fp = fi.transform.position;
                    if (Vector2.Distance(new Vector2(candidate.x, candidate.z),
                                        new Vector2(fp.x, fp.z)) < 0.6f)
                    { overlaps = true; break; }
                }

                if (!overlaps) { pos = candidate; found = true; break; }
            }
            if (!found) continue; // pas de place libre, skip ce détritus

            var go = Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            var debris = go.GetComponent<DebrisInstance>() ?? go.AddComponent<DebrisInstance>();
            debris.Init(this);
            AddDebris(debris);
        }
    }

    /// <summary>Nettoie la chambre. Redevient disponible si pas de réparation nécessaire.</summary>
    public void Clean()
    {
        if (!IsDirty) return;
        IsDirty = false;

        SetState(NeedsRepair ? RoomState.NeedsRepair : RoomState.Empty);
    }

    /// <summary>Marque la chambre comme nécessitant une réparation.</summary>
    public void MarkNeedsRepair()
    {
        NeedsRepair = true;
        if (State == RoomState.Empty || State == RoomState.Dirty)
            SetState(RoomState.NeedsRepair);
    }

    /// <summary>La réparation est terminée. Redevient disponible si propre.</summary>
    public void FinishRepair()
    {
        NeedsRepair = false;
        if (!IsDirty)
            SetState(RoomState.Empty);
    }

    // ─── Swap visuel chambre ──────────────────────────────────────

    /// <summary>
    /// Remplace les enfants visuels (__ph_*) par ceux générés depuis le nouveau prefab.
    ///
    /// Stratégie : instancie le prefab à la même position/scale que ce GO, appelle Build()
    /// dessus, puis reparente ses __ph_* enfants ici avec worldPositionStays=true.
    /// Cela évite le problème de deferred-Destroy ET de mismatch de scale.
    /// </summary>
    void SwapRoomVisual(GameObject newPrefab)
    {
        if (newPrefab == null) return;

        var srcPlaceholder = newPrefab.GetComponent<RoomPlaceholder>();

        // Instancie le prefab au même emplacement / même scale que la chambre actuelle
        var temp = Instantiate(newPrefab, transform.position, transform.rotation);
        temp.transform.localScale = transform.localScale;

        var tempPlaceholder = temp.GetComponent<RoomPlaceholder>();
        if (tempPlaceholder != null)
        {
            if (srcPlaceholder != null)
            {
                tempPlaceholder.wallThickness = srcPlaceholder.wallThickness;
                tempPlaceholder.doorWidth     = srcPlaceholder.doorWidth;
                tempPlaceholder.wallHeight    = srcPlaceholder.wallHeight;
                tempPlaceholder.floorHeight   = srcPlaceholder.floorHeight;
                tempPlaceholder.floorMaterial = srcPlaceholder.floorMaterial;
                tempPlaceholder.wallMaterial  = srcPlaceholder.wallMaterial;
                tempPlaceholder.doorMaterial  = srcPlaceholder.doorMaterial;
            }
            tempPlaceholder.Build();
        }

        // Supprime les anciens enfants visuels de ce GO
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name.StartsWith("__ph_"))
                Destroy(child.gameObject);
        }

        // Transfère les nouveaux __ph_* du temp vers ce GO
        for (int i = temp.transform.childCount - 1; i >= 0; i--)
        {
            var child = temp.transform.GetChild(i);
            if (child.name.StartsWith("__ph_"))
            {
                child.SetParent(transform, worldPositionStays: true);
                foreach (var col in child.GetComponentsInChildren<Collider>(true))
                    col.enabled = true;
            }
        }

        // Met à jour le placeholder courant pour cohérence future
        var myPlaceholder = GetComponent<RoomPlaceholder>();
        if (myPlaceholder != null && srcPlaceholder != null)
        {
            myPlaceholder.wallThickness = srcPlaceholder.wallThickness;
            myPlaceholder.doorWidth     = srcPlaceholder.doorWidth;
            myPlaceholder.wallHeight    = srcPlaceholder.wallHeight;
            myPlaceholder.floorHeight   = srcPlaceholder.floorHeight;
            myPlaceholder.floorMaterial = srcPlaceholder.floorMaterial;
            myPlaceholder.wallMaterial  = srcPlaceholder.wallMaterial;
            myPlaceholder.doorMaterial  = srcPlaceholder.doorMaterial;
        }

        Destroy(temp);
        GetComponent<RoomDither>()?.RefreshRenderers();
    }

    // ─── Privé ────────────────────────────────────────────────────

    void SetState(RoomState newState)
    {
        if (State == newState) return;
        State = newState;
        OnStateChanged?.Invoke(this);
    }
}

public enum RoomState
{
    Empty,       // Libre et propre, prête à accueillir
    Occupied,    // Monstre en séjour
    Dirty,       // Libérée mais pas encore nettoyée
    NeedsRepair, // Nécessite une réparation avant d'être réutilisable
}
