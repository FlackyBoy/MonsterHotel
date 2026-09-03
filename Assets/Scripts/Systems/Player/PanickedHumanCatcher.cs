using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Permet au joueur de rattraper un humain en pleine panique (fuite anticipée du fantôme, voir
/// PossessedHumanQuirks/PossessableHuman) et de le ramener dans une cage (Cage), ainsi que d'invoquer
/// un nouvel humain depuis une cage — remplace HumanSummonTrigger. Suit fidèlement le pattern de
/// MonsterFightBreaker.cs (scan par distance sur un registre statique, prompt WorldPrompt, action
/// Interact) — un seul composant gère les trois interactions (attraper / déposer / invoquer) pour
/// éviter toute ambiguïté sur ce que fait une pression d'Interact près d'une cage.
///
/// Ce composant est passif : il ne s'active que quand aucun autre mode exclusif n'est actif
/// (FurniturePicker, FurniturePlacer, RoomPlacer).
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class PanickedHumanCatcher : MonoBehaviour
{
    [Header("Détection")]
    public float catchRange = 2f;
    public float cageRange  = 2.5f;

    [Header("Noms des actions")]
    public string interactActionName = "Interact";

    // ─── Privé ────────────────────────────────────────────────────

    PlayerInput      _playerInput;
    FurniturePicker  _picker;
    FurniturePlacer  _furniturePlacer;
    RoomPlacer       _roomPlacer;

    PossessableHuman _carried; // non-null tant que ce joueur porte un humain attrapé

    GameObject            _prompt;
    TMPro.TextMeshProUGUI _promptLabel;

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake()
    {
        _playerInput     = GetComponent<PlayerInput>();
        _picker          = GetComponent<FurniturePicker>();
        _furniturePlacer = GetComponent<FurniturePlacer>();
        _roomPlacer      = GetComponent<RoomPlacer>();
    }

    void Update()
    {
        if (IsAnyOtherModeActive())
        {
            DestroyPrompt();
            return;
        }

        if (_carried != null && !_carried.IsBeingCarried)
            _carried = null; // détaché autrement (détruit, etc.)

        if (_carried != null)
            UpdateWhileCarrying();
        else
            UpdateWhileFree();
    }

    void OnDestroy() => DestroyPrompt();

    // ─── Porte un humain → cherche une cage pour le déposer ────────

    void UpdateWhileCarrying()
    {
        var cage = FindNearestCage(cageRange);
        if (cage == null) { DestroyPrompt(); return; }

        ShowPrompt(cage.SpawnPosition + Vector3.up * 1.5f, $"[ {interactActionName} ] Déposer dans la cage");

        if (_playerInput.WasPressed(interactActionName))
        {
            _carried.PlaceInCage(cage);
            _carried = null;
            DestroyPrompt();
        }
    }

    // ─── Ne porte rien → cherche un humain paniqué, sinon une cage à invoquer ───

    void UpdateWhileFree()
    {
        var human = FindNearestPanickingHuman(catchRange);
        if (human != null)
        {
            var b = BoundsUtils.Get(human.gameObject);
            ShowPrompt(b.center + Vector3.up * (b.extents.y + 0.5f), $"[ {interactActionName} ] Attraper");

            if (_playerInput.WasPressed(interactActionName))
            {
                human.Catch(transform);
                _carried = human;
                DestroyPrompt();
            }
            return;
        }

        var cage = FindNearestCage(cageRange);
        if (cage != null)
        {
            ShowPrompt(cage.SpawnPosition + Vector3.up * 1.5f,
                cage.cost > 0 ? $"[ {interactActionName} ] Invoquer un humain ({cage.cost}G)"
                              : $"[ {interactActionName} ] Invoquer un humain");

            if (_playerInput.WasPressed(interactActionName))
                TrySummon(cage);
            return;
        }

        DestroyPrompt();
    }

    void TrySummon(Cage cage)
    {
        if (cage.humanPrefab == null) return;
        if (cage.cost > 0 && (EconomyManager.Instance == null || !EconomyManager.Instance.TrySpend(cage.cost))) return;

        Instantiate(cage.humanPrefab, cage.SpawnPosition, Quaternion.identity);
    }

    // ─── Détection ────────────────────────────────────────────────

    PossessableHuman FindNearestPanickingHuman(float range)
    {
        PossessableHuman best = null;
        float bestDist = range;

        foreach (var h in PossessableHuman.All)
        {
            if (h == null || !h.IsPanicking || h.IsBeingCarried) continue;
            float dist = Vector3.Distance(transform.position, h.transform.position);
            if (dist < bestDist) { bestDist = dist; best = h; }
        }
        return best;
    }

    Cage FindNearestCage(float range)
    {
        Cage best = null;
        float bestDist = range;

        foreach (var c in Cage.All)
        {
            if (c == null) continue;
            float dist = Vector3.Distance(transform.position, c.transform.position);
            if (dist < bestDist) { bestDist = dist; best = c; }
        }
        return best;
    }

    // ─── Prompt ───────────────────────────────────────────────────

    void ShowPrompt(Vector3 worldPos, string text)
    {
        if (_prompt == null)
            (_prompt, _promptLabel) = WorldPrompt.Create(260f, 32f);

        _prompt.transform.position = worldPos;
        WorldPrompt.FaceCamera(_prompt);
        if (_promptLabel != null) _promptLabel.text = text;
    }

    void DestroyPrompt()
    {
        if (_prompt != null) { Destroy(_prompt); _prompt = null; }
    }

    // ─── Helpers ──────────────────────────────────────────────────

    bool IsAnyOtherModeActive()
    {
        if (_picker != null && _picker.IsPicking) return true;
        if (_furniturePlacer != null && _furniturePlacer.IsPlacing) return true;
        if (_roomPlacer != null && _roomPlacer.IsPlacing) return true;
        return false;
    }
}
