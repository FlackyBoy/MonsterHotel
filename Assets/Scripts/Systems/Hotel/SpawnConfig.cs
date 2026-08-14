using UnityEngine;

/// <summary>
/// Configuration spawn — fenêtre d'ouverture, visiteurs repas et effet de la Renommée sur le spawn.
/// Asset : Resources/Config/SpawnConfig.asset (auto-chargé, voir Instance).
/// </summary>
[CreateAssetMenu(fileName = "SpawnConfig", menuName = "Hotel/Config/Spawn")]
public class SpawnConfig : ScriptableObject
{
    static SpawnConfig _instance;

    public static SpawnConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<SpawnConfig>("Config/SpawnConfig");
            return _instance;
        }
    }

    [Header("Spawn monstres")]
    [Tooltip("Intervalle en secondes entre deux spawns (si MonsterData.spawnInterval = 0)")]
    public float defaultSpawnInterval = 20f;
    [Tooltip("Heure à partir de laquelle les nouveaux monstres arrivent")]
    public float spawnOpenHour = 10f;
    [Tooltip("Heure à partir de laquelle plus aucun monstre n'arrive")]
    public float spawnCloseHour = 20f;

    [Header("Visiteurs repas (G6, Phase 1)")]
    [Tooltip("Probabilité qu'un monstre qui spawn soit un visiteur \"repas seul\" (pas de chambre) plutôt qu'un client chambre")]
    [Range(0f, 1f)]
    public float mealVisitorChance = 0.6f;
    [Tooltip("Durée max (secondes) qu'un visiteur repas reste avant de repartir de force, même sans avoir été servi (garde-fou)")]
    public float mealVisitorMaxDuration = 90f;

    [Header("Décorations — Renommée")]
    [Tooltip("Renommée minimale pour commencer à réduire l'intervalle de spawn")]
    public float renownSpawnSpeedupThreshold = 10f;
    [Tooltip("Réduction maximale de l'intervalle de spawn au maximum de renommée (0.4 = 40% plus rapide)")]
    [Range(0f, 0.8f)]
    public float renownMaxSpawnReduction = 0.4f;
    [Tooltip("Renommée totale à partir de laquelle les monstres légendaires peuvent apparaître")]
    public float renownLegendaryThreshold = 30f;
}
