using UnityEngine;

/// <summary>
/// Configuration free roam — balades des monstres hors de leur chambre.
/// Asset : Resources/Config/RoamConfig.asset (auto-chargé, voir Instance).
/// </summary>
[CreateAssetMenu(fileName = "RoamConfig", menuName = "Hotel/Config/Roam")]
public class RoamConfig : ScriptableObject
{
    static RoamConfig _instance;

    public static RoamConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<RoamConfig>("Config/RoamConfig");
            return _instance;
        }
    }

    [Header("Free Roam des monstres")]
    [Tooltip("Temps minimum (sec) qu'un monstre attend en chambre avant de sortir se balader")]
    public float roamMinWait          = 15f;
    [Tooltip("Temps maximum (sec) qu'un monstre attend en chambre avant de sortir se balader")]
    public float roamMaxWait          = 40f;
    [Tooltip("Durée totale (sec) d'une balade avant de rentrer en chambre")]
    public float roamDuration         = 20f;
    [Tooltip("Intervalle (sec) entre chaque nouveau waypoint pendant la balade")]
    public float roamWaypointInterval =  6f;
    [Tooltip("Temps d'attente approximatif pour le trajet retour en chambre")]
    public float roamReturnWait       =  8f;
}
