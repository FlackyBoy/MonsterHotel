using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[DefaultExecutionOrder(1100)]
public class SplitScreenManager : MonoBehaviour
{
    [Header("Cameras")]
    public Camera mainCam;          // garde Cinemachine plein écran
    public Camera camA;             // split A (Base, SolidColor, Everything)
    public Camera camB;             // split B (Base, SolidColor, Everything)

    [Header("Follow (sur CamA/CamB)")]
    public FollowTargetSmooth followA; // offset top-down (0,22,0)
    public FollowTargetSmooth followB; // offset top-down (0,22,0)

    [Header("Cinemachine plein écran")]
    public CinemachineTargetGroup targetGroup;
    public CinemachineCamera vcamGroup;
    public float groupPadding = 2f;

    [Header("Seuils + lissage")]
    public float splitDistance = 25f;
    public float mergeDistance = 18f;
    [Range(0f, 0.5f)] public float switchDeadband = 0.15f;
    public float angleLerp = 12f;
    public float sideFeatherPx = 2f;

    [Header("RT Overlay UI")]
    public Canvas splitRTOverlay;   // Canvas Screen Space - Overlay
    public RawImage rtA;            // RawImage plein écran (Mat_SplitA)
    public RawImage rtB;            // RawImage plein écran (Mat_SplitB)
    public Material matA;           // TEMPLATE Mat_SplitA (shader UI/SplitClip)
    public Material matB;           // TEMPLATE Mat_SplitB

    [Header("Debug")]
    public bool forceSplit = false;
    public KeyCode toggleSplitKey = KeyCode.F9;

    // Instances runtime
    Material matAInst, matBInst;
    RenderTexture rta, rtb;

    // État
    bool split;
    bool verticalState = true;
    float smoothedAngleDeg;
    Transform lastA, lastB;
    Quaternion refRotation;

    static readonly Rect Full = new Rect(0, 0, 1, 1);

    void Start()
    {
        if (!mainCam) mainCam = Camera.main;

        // CamA/CamB sécurisées pour RT (aucune Brain, Base, SolidColor, Everything)
        ConfigureRTCam(camA);
        ConfigureRTCam(camB);

        // Instancie les mats pour éviter les erreurs de copie
        if (matA) { matAInst = new Material(matA); matAInst.SetColor("_Color", Color.white); }
        if (matB) { matBInst = new Material(matB); matBInst.SetColor("_Color", Color.white); }
        if (rtA && matAInst) rtA.material = matAInst;
        if (rtB && matBInst) rtB.material = matBInst;

        EnsureRTs();                    // crée RT et assigne aux RawImage
        SetSplit(false, true);          // plein écran initial
        if (vcamGroup) vcamGroup.Priority = 100;
        if (splitRTOverlay) splitRTOverlay.gameObject.SetActive(false);

        Debug.Log("[Split] Init OK");
    }

    void OnDestroy()
    {
        if (matAInst) Destroy(matAInst);
        if (matBInst) Destroy(matBInst);
        ReleaseRTs();
    }

    void ConfigureRTCam(Camera c)
    {
        if (!c) return;
        // URP: Render Type = Base (pas Overlay)
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = Color.black;
        c.cullingMask = ~0;     // Everything
        c.depth = 1;
        c.targetTexture = null; // sera branché au split
        c.enabled = false;
    }

    void EnsureRTs()
    {
        int w = Mathf.Max(1, Screen.width);
        int h = Mathf.Max(1, Screen.height);

        if (rta && (rta.width != w || rta.height != h)) { rta.Release(); Destroy(rta); rta = null; }
        if (rtb && (rtb.width != w || rtb.height != h)) { rtb.Release(); Destroy(rtb); rtb = null; }

        if (!rta) { rta = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "SplitA_RT" }; rta.Create(); }
        if (!rtb) { rtb = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "SplitB_RT" }; rtb.Create(); }

        if (rtA) { rtA.texture = rta; if (rtA.material) rtA.material.SetColor("_Color", Color.white); }
        if (rtB) { rtB.texture = rtb; if (rtB.material) rtB.material.SetColor("_Color", Color.white); }
    }

    void ReleaseRTs()
    {
        if (rta) { rta.Release(); Destroy(rta); }
        if (rtb) { rtb.Release(); Destroy(rtb); }
    }

    void LateUpdate()
    {



        if (UnityEngine.InputSystem.Keyboard.current != null &&
    UnityEngine.InputSystem.Keyboard.current.f9Key.wasPressedThisFrame)
            forceSplit = !forceSplit;

        if (rta && (rta.width != Screen.width || rta.height != Screen.height))
            EnsureRTs();

        var players = PlayerInput.all
            .OrderBy(p => p.playerIndex)
            .Select(p => p.transform)
            .Take(2)
            .ToArray();

        if (players.Length == 0) return;

        if (players.Length == 1)
        {
            UpdateGroup(players[0], players[0]);
            SetSplit(forceSplit ? true : false);

            // Angle vers 0 hors split
            float k0 = 1f - Mathf.Exp(-angleLerp * Time.deltaTime);
            smoothedAngleDeg = Mathf.LerpAngle(smoothedAngleDeg, 0f, k0);

            // Pas de découpe utile hors split, mais on garde des valeurs cohérentes
            UpdateClipMaterials(Vector2.right, 0.5f, 0.5f, +1f, -1f);
            return;
        }

        var a = players[0];
        var b = players[1];

        // Assigne les cibles de suivi en continu
        if (followA) followA.target = a;
        if (followB) followB.target = b;

        UpdateGroup(a, b);

        float dist = Vector3.Distance(a.position, b.position);
        bool wantSplit = forceSplit || dist > splitDistance;
        bool wantMerge = !forceSplit && dist < mergeDistance;

        if (!split && wantSplit) { CacheRefRotation(); SetSplit(true); }
        if (split && wantMerge) { SetSplit(false); }

        // Référence écran = rotation mémorisée au split (stable), sinon vcam/mainCam
        Quaternion basis = split
            ? refRotation
            : (vcamGroup ? vcamGroup.transform.rotation : mainCam.transform.rotation);

        Vector3 right3 = basis * Vector3.right;
        Vector3 up3 = basis * Vector3.up;
        Vector3 d = b.position - a.position;

        // Angle A→B en écran, lissé
        float rawAngle = Mathf.Atan2(Vector3.Dot(d, up3), Vector3.Dot(d, right3)) * Mathf.Rad2Deg;
        float k = 1f - Mathf.Exp(-angleLerp * Time.deltaTime);
        smoothedAngleDeg = Mathf.LerpAngle(smoothedAngleDeg, rawAngle, k);

        // Dir uv de la ligne
        float rad = smoothedAngleDeg * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;

        // Côté pour chaque image
        Vector3 n3 = basis * new Vector3(-dir.y, 0f, dir.x); // normal à la ligne
        Vector3 mid = (a.position + b.position) * 0.5f;
        float sA = Vector3.Dot(a.position - mid, n3);
        float sideA = (sA < 0f) ? +1f : -1f;

        UpdateClipMaterials(dir, 0.5f, 0.5f, sideA, -sideA);

        if (!split) return;

        // Les deux cams rendent plein écran vers leurs RT.
        if (camA) camA.rect = Full;
        if (camB) camB.rect = Full;
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
            lastA = a; lastB = b;
        }
    }

    void CacheRefRotation()
    {
        if (vcamGroup) refRotation = vcamGroup.transform.rotation;
        else if (mainCam) refRotation = mainCam.transform.rotation;
        else refRotation = Quaternion.identity;

        // branche RT au moment du split et active les cams
        if (camA) { camA.targetTexture = rta; camA.rect = Full; camA.enabled = true; }
        if (camB) { camB.targetTexture = rtb; camB.rect = Full; camB.enabled = true; }
    }

    void SetSplit(bool enable, bool immediate = false)
    {
        if (split == enable && !immediate)
        {
            if (splitRTOverlay && splitRTOverlay.gameObject.activeSelf != enable)
                splitRTOverlay.gameObject.SetActive(enable);
            return;
        }
        split = enable;

        // Active/branche les cams RT et l’UI
        if (enable)
        {
            EnsureRTs(); // s’assure que RTs existent et assignées aux RawImages

            if (camA) { camA.enabled = true; camA.targetTexture = rta; camA.rect = new Rect(0, 0, 1, 1); }
            if (camB) { camB.enabled = true; camB.targetTexture = rtb; camB.rect = new Rect(0, 0, 1, 1); }

            if (splitRTOverlay) splitRTOverlay.gameObject.SetActive(true);
        }
        else
        {
            if (camA) { camA.enabled = false; camA.targetTexture = null; }
            if (camB) { camB.enabled = false; camB.targetTexture = null; }

            if (splitRTOverlay) splitRTOverlay.gameObject.SetActive(false);
        }

        DumpState("[SetSplit]");
    }

    void DumpState(string tag)
    {
        Debug.Log($"{tag} split={split} " +
                  $"CamA en={camA && camA.enabled} ttA={(camA && camA.targetTexture ? camA.targetTexture.name : "null")} " +
                  $"CamB en={camB && camB.enabled} ttB={(camB && camB.targetTexture ? camB.targetTexture.name : "null")} " +
                  $"Overlay={(splitRTOverlay ? splitRTOverlay.gameObject.activeSelf.ToString() : "null")} " +
                  $"RT_A.tex={(rtA && rtA.texture ? rtA.texture.name : "null")} RT_B.tex={(rtB && rtB.texture ? rtB.texture.name : "null")}");
    }

    void UpdateClipMaterials(Vector2 dir, float cx, float cy, float sideForA, float sideForB)
    {
        float featherUV = sideFeatherPx / Mathf.Max(1f, Screen.height);

        if (matAInst)
        {
            matAInst.SetVector("_Dir", new Vector4(dir.x, dir.y, 0, 0));
            matAInst.SetVector("_Center", new Vector4(cx, cy, 0, 0));
            matAInst.SetFloat("_Side", sideForA);
            matAInst.SetFloat("_Feather", featherUV);
        }
        if (matBInst)
        {
            matBInst.SetVector("_Dir", new Vector4(dir.x, dir.y, 0, 0));
            matBInst.SetVector("_Center", new Vector4(cx, cy, 0, 0));
            matBInst.SetFloat("_Side", sideForB);
            matBInst.SetFloat("_Feather", featherUV);
        }
    }
}
