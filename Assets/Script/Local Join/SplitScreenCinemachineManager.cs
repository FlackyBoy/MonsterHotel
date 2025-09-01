using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class SplitScreenCinemachineManager : MonoBehaviour
{
    [Header("Output cameras")]
    public Camera mainCam;   // plein écran (mode groupé)
    public Camera camA;      // split A
    public Camera camB;      // split B

    [Header("Cinemachine")]
    public CinemachineTargetGroup targetGroup;
    public CinemachineCamera vcamGroup; // suit TargetGroup
    public CinemachineCamera vcamA;     // suit P1
    public CinemachineCamera vcamB;     // suit P2

    [Header("Seuils split (hystérésis)")]
    public float splitDistance = 25f;   // split si dist > seuil
    public float mergeDistance = 18f;   // fusion si dist < seuil
    public float rectLerpSpeed = 8f;    // animation viewport

    [Header("Group padding")]
    public float groupPadding = 2f;     // rayon par joueur dans TargetGroup

    Rect targetRectA = new Rect(0, 0, 1, 1);
    Rect targetRectB = new Rect(0, 0, 1, 1);
    bool split;

    Transform lastA, lastB;

    void Start()
    {
        if (!mainCam) mainCam = Camera.main;
        SetSplit(false, immediate: true);

        // Vcam group suit le TargetGroup
        if (vcamGroup && targetGroup)
        {
            vcamGroup.Follow = targetGroup.transform;
            vcamGroup.LookAt = targetGroup.transform;
        }
    }

    void LateUpdate()
    {
        var players = PlayerInput.all
            .OrderBy(p => p.playerIndex)
            .Select(p => p.transform)
            .Take(2)
            .ToArray();

        if (players.Length == 0) return;

        if (players.Length == 1)
        {
            UpdateGroup(players[0], players[0]);
            SetSplit(false);
            return;
        }

        var a = players[0];
        var b = players[1];

        UpdateGroup(a, b);

        float dist = Vector3.Distance(a.position, b.position);
        if (!split && dist > splitDistance) SetSplit(true);
        if (split && dist < mergeDistance) SetSplit(false);

        if (split)
        {
            // Choix auto: split vertical si séparation X dominante, sinon horizontal
            Vector3 d = b.position - a.position;
            bool vertical = Mathf.Abs(d.x) >= Mathf.Abs(d.z);

            targetRectA = vertical ? new Rect(0, 0, 0.5f, 1f)
                                   : new Rect(0, 0.5f, 1f, 0.5f);
            targetRectB = vertical ? new Rect(0.5f, 0, 0.5f, 1f)
                                   : new Rect(0, 0, 1f, 0.5f);

            if (vcamA) { vcamA.Follow = a; vcamA.LookAt = a; }
            if (vcamB) { vcamB.Follow = b; vcamB.LookAt = b; }

            float t = 1f - Mathf.Exp(-rectLerpSpeed * Time.deltaTime);
            camA.rect = LerpRect(camA.rect, targetRectA, t);
            camB.rect = LerpRect(camB.rect, targetRectB, t);
        }
    }

    void UpdateGroup(Transform a, Transform b)
    {
        if (!targetGroup) return;

        if (lastA != a || lastB != b)
        {
            if (lastA) targetGroup.RemoveMember(lastA);
            if (lastB && lastB != lastA) targetGroup.RemoveMember(lastB);

            targetGroup.AddMember(a, 1f, groupPadding);
            targetGroup.AddMember(b, 1f, groupPadding);

            lastA = a;
            lastB = b;
        }
    }

    void SetSplit(bool enable, bool immediate = false)
    {
        if (split == enable && !immediate) return;
        split = enable;

        mainCam.enabled = !enable;
        camA.enabled = enable;
        camB.enabled = enable;

        if (immediate)
        {
            camA.rect = enable ? targetRectA : new Rect(0, 0, 1, 1);
            camB.rect = enable ? targetRectB : new Rect(0, 0, 1, 1);
        }
    }

    static Rect LerpRect(Rect a, Rect b, float t)
    {
        return new Rect(
            Mathf.Lerp(a.x, b.x, t),
            Mathf.Lerp(a.y, b.y, t),
            Mathf.Lerp(a.width, b.width, t),
            Mathf.Lerp(a.height, b.height, t)
        );
    }
}
