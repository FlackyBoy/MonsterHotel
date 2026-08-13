/// <summary>
/// Référence vers la chambre assignée au monstre.
/// Ajouté par ReservationSystem au moment de l'attribution.
/// </summary>
public class GuestRoomReference : UnityEngine.MonoBehaviour
{
    public RoomInstance Room { get; set; }

    // ─── Décompte séjour (visible dans l'Inspector) ───────────────

    [UnityEngine.SerializeField, UnityEngine.Tooltip("Durée totale du séjour (secondes)")]
    float _stayTotal;

    [UnityEngine.SerializeField, UnityEngine.Tooltip("Temps restant avant le départ (secondes)")]
    float _stayRemaining;

    public float StayRemaining => _stayRemaining;

    /// <summary>Appelé par ReservationSystem à l'attribution de la chambre.</summary>
    public void StartStayTimer(float totalSeconds)
    {
        _stayTotal     = totalSeconds;
        _stayRemaining = totalSeconds;
    }

    void Update()
    {
        if (_stayRemaining > 0f)
            _stayRemaining -= UnityEngine.Time.deltaTime;
    }
}
