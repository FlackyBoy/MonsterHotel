using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectDawn.SplitScreen;

[DefaultExecutionOrder(1100)]
[RequireComponent(typeof(SplitScreenEffect))]
public class AdaptiveSplitBinderPD : MonoBehaviour
{
    [Header("Références")]
    public SplitScreenEffect splitEffect; // auto-rempli si sur la Main Camera
    public Camera subCameraPrefab;
    public Transform camerasParent;

    [Header("Sous-caméras (si pas de prefab)")]
    public LayerMask cullingMask = ~0; // Everything
    public CameraClearFlags clearFlags = CameraClearFlags.SolidColor;
    public Color backgroundColor = Color.black;
    public int depth = 1;

    [Header("Options")]
    public bool log = true;
    [Tooltip("Période de rescan en secondes")]
    public float rescanPeriod = 0.25f;

    readonly Dictionary<Transform, Camera> map = new();
    PlayerInputManager pim;
    float nextScanTime;
    int lastCount = -1;

    void Awake()
    {
        if (!splitEffect) splitEffect = GetComponent<SplitScreenEffect>();
        if (!splitEffect)
        {
            Debug.LogError("[BinderPD] SplitScreenEffect introuvable sur cette caméra.");
            enabled = false; return;
        }

        pim = FindObjectOfType<PlayerInputManager>();
        if (pim != null)
        {
            pim.onPlayerJoined += OnPlayerJoined;
            pim.onPlayerLeft += OnPlayerLeft;
        }
    }

    void OnDestroy()
    {
        if (pim != null)
        {
            pim.onPlayerJoined -= OnPlayerJoined;
            pim.onPlayerLeft -= OnPlayerLeft;
        }
    }

    void Start()
    {
        // première sync retardée pour laisser AutoSpawnP1 terminer
        Invoke(nameof(ForceResync), 0.1f);
    }

    void Update()
    {
        // rescan périodique pour capter P1/P2 créés sans événements
        if (Time.unscaledTime >= nextScanTime)
        {
            nextScanTime = Time.unscaledTime + rescanPeriod;
            ForceResync();
        }
    }

    void OnPlayerJoined(PlayerInput pi)
    {
        if (!pi || !pi.transform) return;
        EnsureScreen(pi.transform);
        if (log) Debug.Log($"[BinderPD] Join -> {pi.playerIndex} ({pi.transform.name})");
        Dump();
    }

    void OnPlayerLeft(PlayerInput pi)
    {
        if (!pi || !pi.transform) return;
        RemoveScreen(pi.transform);
        if (log) Debug.Log($"[BinderPD] Left -> {pi.playerIndex}");
        Dump();
    }

    public void ForceResync()
    {
        var found = PlayerInput.all
            .OrderBy(p => p.playerIndex)
            .Select(p => p.transform)
            .Where(t => t != null)
            .Take(4)
            .ToList();

        // retire ceux qui n’existent plus
        foreach (var t in map.Keys.Where(t => !found.Contains(t)).ToList())
            RemoveScreen(t);

        // ajoute manquants
        foreach (var t in found)
            EnsureScreen(t);

        if (found.Count != lastCount)
        {
            lastCount = found.Count;
            if (log) Debug.Log($"[BinderPD] Resync -> {found.Count} joueur(s). Screens={splitEffect.Screens.Count}");
            Dump();
        }
    }

    void EnsureScreen(Transform target)
    {
        if (map.ContainsKey(target)) return;

        var cam = subCameraPrefab ? Instantiate(subCameraPrefab)
                                  : CreateRuntimeCamera(target.name);
        if (camerasParent) cam.transform.SetParent(camerasParent, false);

        cam.enabled = true; // le plugin va lui assigner la RT et la pose

        splitEffect.AddScreen(cam, target); // <-- remplit Screens
        map[target] = cam;
    }

    void RemoveScreen(Transform target)
    {
        if (!map.TryGetValue(target, out var cam)) return;

        // enlève l’entrée correspondante dans le plugin
        int idx = splitEffect.Screens.FindIndex(s => s != null && s.Target == target);
        if (idx >= 0)
        {
            var s = splitEffect.Screens[idx];
            if (s.RenderTarget) { s.RenderTarget.Release(); s.RenderTarget = null; }
            if (s.Camera) Destroy(s.Camera.gameObject);
            splitEffect.Screens.RemoveAt(idx);
        }
        map.Remove(target);
    }

    Camera CreateRuntimeCamera(string who)
    {
        var go = new GameObject($"SplitCam_{who}");
        var cam = go.AddComponent<Camera>();
        cam.cullingMask = cullingMask;
        cam.clearFlags = clearFlags;
        cam.backgroundColor = backgroundColor;
        cam.depth = depth;
        // pas de CinemachineBrain ici
        var brain = go.GetComponent<Unity.Cinemachine.CinemachineBrain>();
        if (brain) Destroy(brain);
        return cam;
    }

    void Dump()
    {
        if (!log) return;
        var names = string.Join(",", splitEffect.Screens.Select(s => s.Target ? s.Target.name : "null"));
    }
}
