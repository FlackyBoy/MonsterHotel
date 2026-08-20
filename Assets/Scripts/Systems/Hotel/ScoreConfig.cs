using UnityEngine;

/// <summary>
/// Configuration du score de fin de journée (U2) — poids et seuils étoiles, journaliers et
/// globaux, séparés (les échelles ne sont pas comparables : or cumulé en caisse vs or gagné dans la
/// seule journée). Valeurs par défaut identiques à celles qui étaient sur DayScoreManager avant
/// cette migration — à équilibrer ensuite.
/// Asset : Resources/Config/ScoreConfig.asset (auto-chargé, voir Instance).
/// </summary>
[CreateAssetMenu(fileName = "ScoreConfig", menuName = "Hotel/Config/Score")]
public class ScoreConfig : ScriptableObject
{
    static ScoreConfig _instance;

    public static ScoreConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<ScoreConfig>("Config/ScoreConfig");
            return _instance;
        }
    }

    [Header("Poids du score JOURNALIER")]
    [Tooltip("Points de score par pièce d'or gagnée net dans la journée")]
    public float goldWeight = 1f;
    [Tooltip("Points de score par point de satisfaction moyenne des départs de la journée")]
    public float satisfactionWeight = 2f;
    [Tooltip("Points de score par point de renommée gagnée dans la journée")]
    public float renownWeight = 10f;

    [Header("Seuils étoiles JOURNALIER (score minimum, sur 5)")]
    public int fiveStarScore  = 600;
    public int fourStarScore  = 450;
    public int threeStarScore = 300;
    public int twoStarScore   = 150;
    public int oneStarScore   = 50;

    [Header("Poids du score GLOBAL (cumulé depuis le début) — séparés du journalier")]
    [Tooltip("Points de score par pièce d'or actuellement en caisse")]
    public float globalGoldWeight = 0.05f;
    [Tooltip("Points de score par point de satisfaction moyenne sur tous les départs depuis le début")]
    public float globalSatisfactionWeight = 2f;
    [Tooltip("Points de score par point de renommée totale (toutes catégories confondues)")]
    public float globalRenownWeight = 5f;
    [Tooltip("Points de score par point de Confort total de l'hôtel (décorations posées) — pas d'équivalent journalier, le Confort ne se \"gagne\" pas au jour le jour")]
    public float globalComfortWeight = 1f;

    [Header("Seuils étoiles GLOBAL (score minimum, sur 5)")]
    public int globalFiveStarScore  = 500;
    public int globalFourStarScore  = 350;
    public int globalThreeStarScore = 200;
    public int globalTwoStarScore   = 100;
    public int globalOneStarScore   = 30;
}
