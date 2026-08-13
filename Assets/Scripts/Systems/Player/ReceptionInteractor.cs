using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sur le joueur : détecte la proximité d'un ReceptionDesk et accueille le prochain monstre.
///
/// Contrôles :
///   Interact → CheckInNext() sur le ReservationSystem
///
/// Inactif si un autre mode exclusif est actif (FurniturePicker, Placer, RoomPlacer).
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class ReceptionInteractor : MonoBehaviour
{
    [Header("Détection")]
    public float interactRange = 1.8f;

    [Header("Actions")]
    public string interactActionName = "Interact";

    // ─── Privé ────────────────────────────────────────────────────

    PlayerInput     _playerInput;
    FurniturePicker _picker;
    FurniturePlacer _placer;
    RoomPlacer      _roomPlacer;

    GameObject            _prompt;
    TMPro.TextMeshProUGUI _promptLabel;

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
        _picker      = GetComponent<FurniturePicker>();
        _placer      = GetComponent<FurniturePlacer>();
        _roomPlacer  = GetComponent<RoomPlacer>();
    }

    void OnDestroy() => DestroyPrompt();

    void Update()
    {
        if (IsAnyOtherModeActive())
        {
            DestroyPrompt();
            return;
        }

        var desk = NearestDesk();
        var rs   = ReservationSystem.Instance;

        // Affiche le prompt s'il y a un monstre à accueillir OU un checked-in attendant une chambre
        var nextUnchecked = rs?.NextUnchecked;
        // N'affiche le prompt que si le monstre est physiquement arrivé au comptoir
        if (nextUnchecked != null && !nextUnchecked.HasArrived) nextUnchecked = null;
        var nextWaiting   = rs?.NextCheckedInWaiting;
        var actionGuest   = nextWaiting ?? nextUnchecked;

        if (desk == null || actionGuest == null)
        {
            DestroyPrompt();
            return;
        }

        string label = nextWaiting != null
            ? $"[ {interactActionName} ] Assigner chambre : {nextWaiting.Data.monsterName}"
            : $"[ {interactActionName} ] Accueillir : {nextUnchecked.Data.monsterName}";
        ShowPrompt(desk.transform.position + Vector3.up * 0.8f, label);

        if (_playerInput.WasPressed(interactActionName))
            ReservationSystem.Instance.CheckInNext();
    }

    // ─── Helpers ──────────────────────────────────────────────────

    ReceptionDesk NearestDesk()
    {
        ReceptionDesk best     = null;
        float         bestDist = interactRange;
        foreach (var d in ReceptionDesk.All)
        {
            float dist = Vector3.Distance(transform.position, d.transform.position);
            if (dist < bestDist) { bestDist = dist; best = d; }
        }
        return best;
    }

    bool IsAnyOtherModeActive() =>
        (_picker   != null && _picker.IsPicking)    ||
        (_placer   != null && _placer.IsPlacing)    ||
        (_roomPlacer != null && _roomPlacer.IsPlacing);

    void ShowPrompt(Vector3 worldPos, string text)
    {
        if (_prompt == null)
            (_prompt, _promptLabel) = WorldPrompt.Create(200f, 32f);
        _prompt.transform.position = worldPos;
        WorldPrompt.FaceCamera(_prompt);
        if (_promptLabel != null) _promptLabel.text = text;
    }

    void DestroyPrompt()
    {
        if (_prompt != null) { Destroy(_prompt); _prompt = null; _promptLabel = null; }
    }
}
