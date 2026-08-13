using UnityEngine;

/// <summary>
/// Définit un type de besoin (Faim, Détente, Divertissement, Bien-être…).
/// Crée via clic-droit → Create → MonsterHotel → Need Type.
/// </summary>
[CreateAssetMenu(fileName = "NeedType_New", menuName = "MonsterHotel/Need Type")]
public class NeedType : ScriptableObject
{
    [Header("Identité")]
    public string needName = "Besoin";
    public Sprite icon;

    [Header("Dégradation")]
    [Tooltip("Vitesse de dégradation par seconde (0.01 = 100s pour vider)")]
    [Range(0.001f, 1f)]
    public float decayRate = 0.005f;

    [Header("Seuils")]
    [Tooltip("En dessous de ce niveau le monstre devient insatisfait")]
    [Range(0f, 1f)]
    public float unsatisfiedThreshold = 0.3f;

    [Tooltip("En dessous de ce niveau le monstre quitte la salle commune en colère")]
    [Range(0f, 1f)]
    public float criticalThreshold = 0.1f;

    [Header("Impact satisfaction")]
    [Tooltip("Perte de satisfaction par seconde quand le besoin est critique")]
    public float satisfactionDecayPerSecond = 2f;

    [Header("Bonus / Pénalités service")]
    [Tooltip("Bonus de satisfaction si servi dans les temps ET/OU bonne qualité")]
    public float satisfactionBonus = 10f;

    [Tooltip("Pénalité de satisfaction si trop long OU mauvaise qualité")]
    public float satisfactionPenalty = 5f;

    [Tooltip("Temps d'attente max (secondes) pour obtenir le bonus de rapidité")]
    public float goodServiceWaitTime = 30f;

    [Tooltip("Qualité minimale [0-1] pour obtenir le bonus de qualité")]
    [Range(0f, 1f)]
    public float goodQualityThreshold = 0.6f;
}
