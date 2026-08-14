using UnityEngine;

/// <summary>
/// Configuration employés — effectif, fatigue/récupération, salaires et efficacité par note.
/// Asset : Resources/Config/EmployeeConfig.asset (auto-chargé, voir Instance).
/// </summary>
[CreateAssetMenu(fileName = "EmployeeConfig", menuName = "Hotel/Config/Employee")]
public class EmployeeConfig : ScriptableObject
{
    static EmployeeConfig _instance;

    public static EmployeeConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<EmployeeConfig>("Config/EmployeeConfig");
            return _instance;
        }
    }

    [Header("Employés")]
    [Tooltip("1 employé autorisé par X chambres (ex: 3 = 1 employé pour 3 chambres)")]
    public float employeeRoomRatio = 3f;
    [Tooltip("Override debug : si > 0, remplace le calcul par chambres. Remettre à 0 en production.")]
    public int employeeMaxOverride = 0;
    [Tooltip("Taux de fatigue par minute de travail (points/minute)")]
    public float employeeFatigueRate = 2f;
    [Tooltip("Taux de récupération par minute de pause (points/minute)")]
    public float employeeRecoveryRate = 5f;
    [Tooltip("En dessous de ce seuil de bien-être, l'employé démissionne")]
    [Range(0f, 100f)]
    public float employeeResignThreshold = 10f;
    [Tooltip("Perte de bien-être quand le joueur force un employé à travailler pendant sa pause")]
    public float employeeForceWorkPenalty = 20f;
    [Tooltip("Salaire journalier = note × ce multiplicateur")]
    public float employeeSalaryPerRating = 6f;
    [Tooltip("Frais d'embauche = salaire × ce multiplicateur")]
    public float employeeFeeMultiplier = 1.5f;

    [Header("Employés — efficacité par note (note 1 → min, note 20 → max)")]
    [Tooltip("Multiplicateur de breakInterval à note 1 (ex: 0.5 = pause 2× plus souvent)")]
    public float employeeBreakIntervalMinMult = 0.5f;
    [Tooltip("Multiplicateur de breakInterval à note 20 (ex: 1.5 = pause 50% moins souvent)")]
    public float employeeBreakIntervalMaxMult = 1.5f;
    [Tooltip("Multiplicateur de vitesse de déplacement à note 1")]
    public float employeeSpeedMinMult = 0.75f;
    [Tooltip("Multiplicateur de vitesse de déplacement à note 20")]
    public float employeeSpeedMaxMult = 1.25f;
    [Tooltip("Multiplicateur de taux de fatigue à note 1 (ex: 1.5 = se fatigue 50% plus vite)")]
    public float employeeFatigueRateMinMult = 1.5f;
    [Tooltip("Multiplicateur de taux de fatigue à note 20 (ex: 0.5 = se fatigue 2× moins vite)")]
    public float employeeFatigueRateMaxMult = 0.5f;
    [Tooltip("Multiplicateur de taux de récupération à note 1")]
    public float employeeRecoveryRateMinMult = 0.75f;
    [Tooltip("Multiplicateur de taux de récupération à note 20")]
    public float employeeRecoveryRateMaxMult = 1.25f;
    [Tooltip("Durée de base d'une action de nettoyage (secondes). Réduite par la note.")]
    public float employeeCleanBaseDuration = 4f;
    [Tooltip("Délai de base pour accueillir un monstre à la réception (secondes). Réduit par la note.")]
    public float employeeCheckInBaseDelay = 2f;
    [Tooltip("Diviseur de la courbe d'efficacité des AIs (cuisine, nettoyage, réception). " +
             "note 20 → multiplicateur = 1 - 19/diviseur. Ex: 38 = 50% du temps à note 20.")]
    public float employeeRatingCurveDivisor = 38f;
}
