using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Permet au joueur de séparer une bagarre entre deux monstres en s'approchant et en appuyant sur
/// Interact. Suit fidèlement le pattern de CleaningInteractor.cs (scan par distance sur un registre
/// statique, prompt WorldPrompt, action Interact) — pas d'InputConsumer, cohérent avec l'exemple
/// concret déjà en prod plutôt que la théorie.
///
/// Ce composant est passif : il ne s'active que quand aucun autre mode exclusif n'est actif
/// (FurniturePicker, FurniturePlacer, RoomPlacer).
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class MonsterFightBreaker : MonoBehaviour
{
    [Header("Détection")]
    [Tooltip("Portée de détection — un peu plus large que les interactions habituelles (1.5m) car le nuage VFX est visuellement plus grand qu'une cible ponctuelle.")]
    public float interactRange = 2f;

    [Header("Noms des actions")]
    public string interactActionName = "Interact";

    // ─── Privé ────────────────────────────────────────────────────

    PlayerInput      _playerInput;
    FurniturePicker  _picker;
    FurniturePlacer  _furniturePlacer;
    RoomPlacer       _roomPlacer;

    MonsterFightBehavior  _target;
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
            ClearTarget();
            return;
        }

        FindBestTarget();
        UpdatePrompt();

        if (_target == null) return;

        var action = _playerInput.actions.FindAction(interactActionName, throwIfNotFound: false);
        if (action != null && action.WasPressedThisFrame())
        {
            _target.ResolveByPlayer();
            _target = null;
            DestroyPrompt();
        }
    }

    void OnDestroy() => DestroyPrompt();

    // ─── Détection ────────────────────────────────────────────────

    void FindBestTarget()
    {
        MonsterFightBehavior best = null;
        float bestDist = interactRange;

        foreach (var fb in MonsterFightBehavior.All)
        {
            if (!fb.IsFighting) continue; // seulement pendant la mêlée visible, pas Locked/Approaching
            float dist = Vector3.Distance(transform.position, fb.transform.position);
            if (dist < bestDist) { bestDist = dist; best = fb; }
        }

        _target = best;
    }

    // ─── Prompt ───────────────────────────────────────────────────

    void UpdatePrompt()
    {
        if (_target == null) { DestroyPrompt(); return; }

        var b = BoundsUtils.Get(_target.gameObject);
        ShowPrompt(b.center + Vector3.up * (b.extents.y + 0.5f), $"[ {interactActionName} ] Séparer la bagarre");
    }

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

    void ClearTarget()
    {
        _target = null;
        DestroyPrompt();
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
