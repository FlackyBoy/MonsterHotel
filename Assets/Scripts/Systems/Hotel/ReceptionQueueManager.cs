using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gère les slots de la file d'attente devant un comptoir (réception hôtel, réception restaurant...).
/// Les monstres se positionnent en file indienne à partir de queueStart,
/// dans la direction -forward (le slot 0 est le plus proche du comptoir).
/// Quand un slot se libère, tous les monstres derrière avancent d'un cran.
/// Pas de singleton : chaque comptoir a sa propre instance, référencée directement par le
/// système qui le possède (ex : ReservationSystem.receptionQueue, RestaurantReservationSystem.queueManager).
/// </summary>
public class ReceptionQueueManager : MonoBehaviour
{
    [Header("File d'attente")]
    [Tooltip("Transform placé juste devant le comptoir — premier slot de la file")]
    public Transform queueStart;
    [Tooltip("Nombre max de slots dans la file")]
    public int maxSlots = 5;
    [Tooltip("Espacement entre chaque monstre (unités monde)")]
    public float slotSpacing = 1.5f;

    // ─── Privé ────────────────────────────────────────────────────

    /// <summary>Slot i → GameObject du monstre qui l'occupe (null = libre).</summary>
    GameObject[] _slots;

    /// <summary>
    /// Fired après une compaction. Paramètres : monstre, nouvel index de slot.
    /// Permet à ReservationSystem de mettre à jour QueueSlotIndex.
    /// </summary>
    public event System.Action<GameObject, int> OnSlotIndexChanged;

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake()
    {
        _slots = new GameObject[maxSlots];
    }

    // ─── API publique ─────────────────────────────────────────────

    /// <summary>
    /// Réserve le premier slot libre et y associe le monstre.
    /// Retourne false si la file est pleine.
    /// </summary>
    public bool RequestSlot(out int index, out Vector3 position, GameObject monster)
    {
        for (int i = 0; i < maxSlots; i++)
        {
            if (_slots[i] == null)
            {
                _slots[i] = monster;
                index      = i;
                position   = SlotPosition(i);
                return true;
            }
        }
        index    = -1;
        position = Vector3.zero;
        return false;
    }

    /// <summary>
    /// Libère le slot, puis compacte la file :
    /// chaque monstre derrière avance d'un cran et marche vers sa nouvelle position.
    /// </summary>
    public void ReleaseSlot(int index)
    {
        if (index < 0 || index >= maxSlots) return;

        _slots[index] = null;
        Compact(index);
    }

    // ─── Compaction ───────────────────────────────────────────────

    void Compact(int freedIndex)
    {
        for (int i = freedIndex; i < maxSlots - 1; i++)
        {
            // Cherche le prochain slot occupé (gère les éventuels trous dans la file)
            int next = i + 1;
            while (next < maxSlots && _slots[next] == null) next++;
            if (next >= maxSlots) break;

            _slots[i]    = _slots[next];
            _slots[next] = null;

            var monster = _slots[i];
            if (monster != null)
            {
                monster.GetComponent<MonsterMover>()?.MoveTo(SlotPosition(i));
                OnSlotIndexChanged?.Invoke(monster, i);
            }
        }
    }

    // ─── Calcul de position ───────────────────────────────────────

    Vector3 SlotPosition(int i)
    {
        if (queueStart == null)
        {
            // Repli sur la position du comptoir lui-même plutôt que l'origine du monde (0,0,0) —
            // sans ce repli, un queueStart oublié envoyait silencieusement tous les monstres vers
            // l'origine, qui peut coïncider avec un autre point de l'hôtel (ex : la réception),
            // donnant l'impression qu'ils se dirigent vers le mauvais comptoir.
            Debug.LogWarning($"[{name}] ReceptionQueueManager.queueStart non assigné — repli sur la position du comptoir. Assigne un Transform enfant dans l'Inspector.");
            return transform.position;
        }
        return queueStart.position - queueStart.forward * (i * slotSpacing);
    }

    // ─── Gizmos ───────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        if (queueStart == null) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < maxSlots; i++)
        {
            var pos = SlotPosition(i);
            Gizmos.DrawWireSphere(pos, 0.3f);
            if (_slots != null && i < _slots.Length && _slots[i] != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(pos, _slots[i].transform.position);
                Gizmos.color = Color.cyan;
            }
        }
    }
}
