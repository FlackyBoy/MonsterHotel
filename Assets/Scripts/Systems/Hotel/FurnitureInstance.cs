using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Composant-container posé sur chaque meuble instancié dans une chambre.
/// Référence ses données et la chambre propriétaire.
/// Le visuel (prefab) est instancié comme enfant de ce container.
/// </summary>
public class FurnitureInstance : MonoBehaviour
{
    // ─── Registre global ──────────────────────────────────────────

    public static readonly HashSet<FurnitureInstance> All = new();

    // MaterialPropertyBlock réutilisable — initialisé dans Awake (interdit dans le constructeur MonoBehaviour)
    MaterialPropertyBlock _mpb;

    void Awake()     { All.Add(this); _mpb = new MaterialPropertyBlock(); }
    void OnDestroy() { All.Remove(this); Room?.RemoveFurniture(this); }

    // ─────────────────────────────────────────────────────────────

    public FurnitureData Data          { get; private set; }
    public RoomInstance  Room          { get; private set; }
    /// <summary>GO enfant contenant le visuel actuel (mis à jour à chaque SwapVisual).</summary>
    public GameObject    CurrentVisual { get; private set; }

    /// <summary>Appelé par FurniturePlacer juste après la création du container.</summary>
    public void Init(FurnitureData data, RoomInstance room)
    {
        Data = data;
        Room = room;
    }

    /// <summary>Enregistre le visuel initial (appelé par FurniturePlacer après Instantiate).</summary>
    public void SetVisual(GameObject visual)
    {
        CurrentVisual = visual;
        RefreshStateVisual(); // applique la teinture si déjà sale/abimé
    }

    // ─── État propreté / réparation ───────────────────────────────

    public bool IsDirty   { get; private set; }
    public bool IsDamaged { get; private set; }

    /// <summary>True tant qu'un employé de nettoyage a réservé ce meuble — empêche un doublon.</summary>
    public bool IsBeingCleaned { get; private set; }

    public void ReserveCleaning() => IsBeingCleaned = true;
    public void ReleaseCleaning() => IsBeingCleaned = false;

    public void SetDirty(bool dirty)
    {
        if (IsDirty == dirty) return;
        IsDirty = dirty;
        RefreshStateVisual();
        if (!dirty) Room?.TryAutoClean();
    }

    public void SetDamaged(bool damaged)
    {
        if (IsDamaged == damaged) return;
        IsDamaged = damaged;
        RefreshStateVisual();
        if (!damaged) Room?.TryAutoClean();
    }

    /// <summary>Applique une teinte selon l'état (sale = jaune, abimé = rouge, propre = blanc).
    /// Compatible URP Lit (_BaseColor) et Standard shader (_Color).</summary>
    void RefreshStateVisual()
    {
        if (CurrentVisual == null) return;

        bool hasState = IsDamaged || IsDirty;

        if (!hasState)
        {
            foreach (var r in CurrentVisual.GetComponentsInChildren<Renderer>())
                r.SetPropertyBlock(null);
            return;
        }

        Color tint = IsDamaged ? new Color(0.9f, 0.3f, 0.3f) : new Color(0.75f, 0.68f, 0.2f);

        _mpb.Clear();
        _mpb.SetColor("_BaseColor", tint); // URP Lit
        _mpb.SetColor("_Color",     tint); // Standard / shaders importés

        foreach (var r in CurrentVisual.GetComponentsInChildren<Renderer>())
            r.SetPropertyBlock(_mpb);
    }

    // ─── Upgrade ──────────────────────────────────────────────────

    public bool CanUpgrade => Data?.nextUpgrade != null;

    /// <summary>
    /// Améliore ce meuble vers le niveau suivant.
    /// Dépense le coût, swap le prefab visuel, met à jour les données.
    /// Retourne false si fonds insuffisants ou niveau max.
    /// </summary>
    public bool Upgrade()
    {
        if (!CanUpgrade)
        {
            return false;
        }

        if (!EconomyManager.Instance.TrySpend(Data.upgradeCost))
        {
            return false;
        }

        var next = Data.nextUpgrade;

        // Swap le prefab visuel si différent
        if (next.prefab != Data.prefab)
            SwapVisual(next.prefab);

        Data = next;
        name = next.furnitureName;

        return true;
    }

    // ─── Suppression ──────────────────────────────────────────────

    /// <summary>Retire et détruit ce meuble, rembourse son coût.</summary>
    public void Remove()
    {
        Room?.RemoveFurniture(this);
        if (Data != null)
            EconomyManager.Instance?.Earn(Data.cost);
        Destroy(gameObject);
    }

    // ─── Interne ──────────────────────────────────────────────────

    /// <summary>Détruit le visuel enfant actuel et instancie le nouveau prefab comme enfant.</summary>
    void SwapVisual(GameObject newPrefab)
    {
        // Détruit les enfants actuels (le visuel)
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        // Instancie le nouveau visuel comme enfant
        if (newPrefab != null)
        {
            var visual = Instantiate(newPrefab, transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            // Désactive les colliders et scripts du visuel (le container gère tout)
            foreach (var col in visual.GetComponentsInChildren<Collider>())
                col.enabled = false;

            CurrentVisual = visual;
        }
        else
        {
            // Fallback cube si pas de prefab
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(transform, false);
            cube.transform.localScale = Vector3.one * 0.5f;
        }
    }
}
