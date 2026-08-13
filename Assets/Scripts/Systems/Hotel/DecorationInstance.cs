using UnityEngine;

/// <summary>
/// Composant posé sur chaque décoration placée dans l'hôtel.
/// S'enregistre automatiquement dans HotelStatsManager pour contribuer aux stats globales.
/// </summary>
public class DecorationInstance : MonoBehaviour
{
    public DecorationData Data { get; private set; }

    public void Init(DecorationData data)
    {
        Data = data;
        HotelStatsManager.Instance?.Register(this);
    }

    void OnDestroy()
    {
        HotelStatsManager.Instance?.Unregister(this);

        // Libère la cellule de grille occupée par cette décoration
        var grid = GridManager.Instance;
        if (grid != null)
        {
            var cell = grid.WorldToCell(transform.position);
            if (cell != null && cell.occupant == gameObject)
                grid.ClearOccupant(cell.coords.x, cell.coords.y);
        }
    }
}
