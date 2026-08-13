/// <summary>
/// Stocke temporairement la qualité du plat que le joueur transporte.
/// Remis à 1 quand le joueur dépose ou lâche l'item.
/// </summary>
public class FoodQualityCarrier : UnityEngine.MonoBehaviour
{
    public float Quality { get; set; } = 1f;
}
