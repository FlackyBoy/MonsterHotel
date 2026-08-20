using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sur le joueur : détecte la proximité d'un RestaurantReceptionDesk et accueille le prochain
/// monstre côté restaurant. Même rôle que ReceptionInteractor (hôtel) — permet au joueur de
/// prendre en charge la réception du restaurant lui-même tant qu'aucun réceptionniste n'est
/// embauché (sans ce composant, la file resto ne serait jamais validée sans employé).
///
/// Contrôles :
///   Interact → CheckInNext() sur le RestaurantReservationSystem
///
/// Inactif si un autre mode exclusif est actif (FurniturePicker, Placer, RoomPlacer).
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class RestaurantReceptionInteractor : MonoBehaviour
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
        var rrs  = RestaurantReservationSystem.Instance;

        // NextServiceableUnchecked (au lieu de NextUnchecked) saute le 1er de la file si aucune
        // place n'est dispo pour lui mais qu'un autre pourrait être accepté (G6-B24).
        var nextUnchecked = rrs?.NextServiceableUnchecked;

        if (desk == null || nextUnchecked == null)
        {
            DestroyPrompt();
            return;
        }

        string label = $"[ {interactActionName} ] Accueillir (resto) : {nextUnchecked.Monster?.name}";
        ShowPrompt(desk.transform.position + Vector3.up * 0.8f, label);

        if (_playerInput.WasPressed(interactActionName))
            rrs.CheckInNext(nextUnchecked);
    }

    // ─── Helpers ──────────────────────────────────────────────────

    RestaurantReceptionDesk NearestDesk()
    {
        RestaurantReceptionDesk best = null;
        float bestDist = interactRange;
        foreach (var d in RestaurantReceptionDesk.All)
        {
            float dist = Vector3.Distance(transform.position, d.transform.position);
            if (dist < bestDist) { bestDist = dist; best = d; }
        }
        return best;
    }

    bool IsAnyOtherModeActive() =>
        (_picker     != null && _picker.IsPicking)  ||
        (_placer     != null && _placer.IsPlacing)  ||
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
