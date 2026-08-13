using UnityEngine;

/// <summary>
/// Définit un type d'employé par rôle.
/// Crée via clic-droit → Create → MonsterHotel → Employee Data.
/// Contient la liste des skins disponibles pour ce rôle.
/// Les stats (nom, note, salaire) sont générées aléatoirement à la création du candidat.
/// </summary>
[CreateAssetMenu(fileName = "EmployeeData_New", menuName = "MonsterHotel/Employee Data")]
public class EmployeeData : ScriptableObject
{
    [Header("Identité")]
    public string employeeName = "Employé";
    public EmployeeRole role   = EmployeeRole.Reception;
    public Sprite portrait;

    [Header("Skins disponibles")]
    [Tooltip("Prefabs visuels pour ce rôle — piochés en pool, auto-reset quand épuisés")]
    public GameObject[] characterPrefabs;
    public float moveSpeed = 3.5f;

    [Header("Économie (runtime — ne pas éditer manuellement)")]
    [Tooltip("Note sur 20 — générée aléatoirement à l'embauche")]
    [Range(1, 20)]
    public int rating = 10;
    [Tooltip("Salaire journalier — calculé depuis la note")]
    public int dailySalary = 30;
    [Tooltip("Frais de recrutement — calculés depuis la note")]
    public int hiringFee = 50;

    [Header("Horaires")]
    [Range(0f, 24f)] public float workStartHour = 8f;
    [Range(0f, 24f)] public float workEndHour   = 20f;

    [Header("Pauses")]
    public float breakInterval = 120f;
    public float breakDuration = 30f;
}
