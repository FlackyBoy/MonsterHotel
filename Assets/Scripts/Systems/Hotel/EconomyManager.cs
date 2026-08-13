using UnityEngine;

/// <summary>
/// Singleton gérant la monnaie du joueur.
/// Autres systèmes appellent TrySpend() pour débiter et Earn() pour créditer.
/// Abonne-toi à OnGoldChanged pour mettre à jour l'UI.
/// </summary>
[DefaultExecutionOrder(-100)]
public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance { get; private set; }

    // ─── Privé ────────────────────────────────────────────────────

    int _gold;

    // ─── API publique ─────────────────────────────────────────────

    public int Gold => _gold;

    /// <summary>Déclenché à chaque changement de solde. Paramètre = nouveau solde.</summary>
    public event System.Action<int> OnGoldChanged;

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _gold = HotelConfig.Instance != null ? HotelConfig.Instance.startingGold : 1000;
    }

    // ─── Transactions ─────────────────────────────────────────────

    /// <summary>
    /// Tente de dépenser <paramref name="amount"/> pièces.
    /// Retourne false (sans modifier le solde) si fonds insuffisants.
    /// </summary>
    public bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (_gold < amount) return false;

        _gold -= amount;
        OnGoldChanged?.Invoke(_gold);
        return true;
    }

    /// <summary>Crédite <paramref name="amount"/> pièces au solde.</summary>
    public void Earn(int amount)
    {
        if (amount <= 0) return;
        _gold += amount;
        OnGoldChanged?.Invoke(_gold);
    }

    /// <summary>Retourne true si le joueur peut se permettre cette dépense.</summary>
    public bool CanAfford(int amount) => _gold >= amount;
}
