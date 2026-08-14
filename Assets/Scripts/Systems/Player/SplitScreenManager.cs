using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Gère le split-screen STATIQUE (RW1) : deux caméras indépendantes avec un Viewport Rect fixe
/// (P1 = moitié gauche, P2 = moitié droite), rendu natif Unity — plus de compositing dynamique.
///
/// Remplace l'ancien système adaptatif (package ProjectDawn.SplitScreen / SplitScreenEffect, qui
/// recalculait une découpe dynamique façon Voronoi selon la position des joueurs).
///
/// Transition split ↔ fusion : le cadrage 3D de PlayerCamera (position/zoom) est lissé en continu
/// (mergeBlend, 0→1), mais la mise en page écran (rects) bascule nette — animer la largeur d'un
/// Camera.rect en continu déforme le rendu d'une caméra perspective (testé, effet indésirable).
///
/// L'ancienne Main Camera est désactivée (même neutralisée par cullingMask/clearFlags, elle
/// perturbait le rendu de cam0/cam1 — confirmé empiriquement : tout remarche dès qu'on la
/// désactive). Le tag "MainCamera" est repris par la caméra de P1 (cam0, qui rend vraiment
/// quelque chose) pour que Camera.main reste valide pour les scripts UI world-space qui s'y
/// réfèrent (billboards, canvas worldCamera).
/// </summary>
public class SplitScreenManager : MonoBehaviour
{
    public static SplitScreenManager Instance { get; private set; }

    [Header("Paramètres (repris depuis HotelConfig si présent — voir ApplyConfig)")]
    [Tooltip("Distance caméra au-dessus des joueurs (vue top-down)")]
    public float cameraHeight = 22f;

    [Header("Fusion caméra (joueurs proches — écran non scindé)")]
    [Tooltip("Distance entre les deux joueurs en dessous de laquelle l'écran ne se scinde plus (une seule caméra plein écran cadre les deux). Valeur de départ — à toi d'ajuster.")]
    public float mergeDistance = 15f;
    [Tooltip("Marge autour du seuil pour éviter un flicker split/fusion pile à la limite")]
    public float mergeHysteresis = 3f;
    [Tooltip("En fusion, dézoom additionnel par unité de distance entre les deux joueurs (plus ils s'écartent, plus ça dézoome)")]
    public float mergeDistancePerUnit = 0.6f;
    [Tooltip("Vitesse de transition (lerp/seconde) entre écran scindé et fusionné")]
    public float mergeTransitionSpeed = 2f;

    [Header("Trait de séparation (visible uniquement en mode scindé)")]
    [Tooltip("Épaisseur du trait en pixels")]
    public float dividerWidth = 4f;
    public Color dividerColor = Color.black;

    [Header("Optionnel — assigne manuellement si déjà créées")]
    public Camera cam0;
    public Camera cam1;

    // ─── Privé ────────────────────────────────────────────────────

    readonly List<Transform>   _targets = new();
    readonly List<PlayerInput> _players = new();
    Camera _mainCam;
    bool   _merged;
    float  _mergeBlend; // 0 = scindé, 1 = fusionné — lissé en continu vers _merged
    Canvas _dividerCanvas;
    Image  _dividerImage;

    /// <summary>Caméra du joueur donné (celle qui le suit), ou null si ce joueur n'a pas (encore) de caméra assignée.</summary>
    public Camera GetCameraForPlayer(PlayerInput pi)
    {
        int idx = _players.IndexOf(pi);
        if (idx == 0) return cam0;
        if (idx == 1) return cam1;
        return null;
    }

    // ─── Lifecycle ────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        ApplyConfig();

        // L'ancienne Main Camera perturbait le rendu de cam0/cam1 même neutralisée (cullingMask=0
        // + clearFlags=Nothing ne suffisaient pas — confirmé en la décochant manuellement : tout
        // remarche). Désactivée complètement + retire son tag MainCamera (repris par cam0/P1 dans
        // CreateCamera, qui rend vraiment quelque chose et garde donc Camera.main valide).
        _mainCam = Camera.main;
        if (_mainCam != null)
        {
            _mainCam.enabled = false;
            _mainCam.gameObject.tag = "Untagged";
        }

        EnsureCameras();
    }

    /// <summary>Reprend les valeurs de HotelConfig si présent (paramétrage général du jeu) —
    /// sinon garde les défauts déclarés sur ce composant.</summary>
    void ApplyConfig()
    {
        var cfg = HotelConfig.Camera;
        if (cfg == null) return;

        cameraHeight         = cfg.splitScreenCameraHeight;
        mergeDistance         = cfg.splitScreenMergeDistance;
        mergeHysteresis       = cfg.splitScreenMergeHysteresis;
        mergeDistancePerUnit  = cfg.splitScreenMergeDistancePerUnit;
        mergeTransitionSpeed  = cfg.splitScreenMergeTransitionSpeed;
        dividerWidth          = cfg.splitScreenDividerWidth;
        dividerColor          = cfg.splitScreenDividerColor;
    }

    void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerJoined += HandlePlayerJoined;
            GameManager.Instance.OnPlayerLeft   += HandlePlayerLeft;

            // Rattrape les joueurs déjà enregistrés
            foreach (var pi in GameManager.Instance.Players)
                HandlePlayerJoined(pi);
        }
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerJoined -= HandlePlayerJoined;
            GameManager.Instance.OnPlayerLeft   -= HandlePlayerLeft;
        }
    }

    void Update()
    {
        UpdateSplitOrMerge();
        UpdateBlend();
    }

    // ─── Création ─────────────────────────────────────────────────

    void EnsureCameras()
    {
        if (cam0 == null) cam0 = CreateCamera("Camera_P1", -2);
        if (cam1 == null) cam1 = CreateCamera("Camera_P2", -3);

        // cam0 (P1) porte le tag MainCamera — voir Awake() pour pourquoi.
        cam0.gameObject.tag = "MainCamera";

        cam0.gameObject.SetActive(false);
        cam1.gameObject.SetActive(false);

        CreateDivider();
    }

    /// <summary>
    /// Trait vertical fin au milieu de l'écran, pour bien marquer la coupure en mode scindé —
    /// Canvas dédié (au-dessus de tout le reste). Reste actif dès que 2 joueurs sont présents,
    /// visibilité pilotée par un fondu d'alpha (UpdateBlend) plutôt qu'un SetActive discret, pour
    /// suivre la même transition fluide que les caméras.
    /// </summary>
    void CreateDivider()
    {
        var canvasGO = new GameObject("SplitScreenDividerCanvas");
        canvasGO.transform.SetParent(transform);

        _dividerCanvas = canvasGO.AddComponent<Canvas>();
        _dividerCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _dividerCanvas.sortingOrder = 100;

        var lineGO = new GameObject("DividerLine");
        lineGO.transform.SetParent(canvasGO.transform, false);
        _dividerImage = lineGO.AddComponent<Image>();
        _dividerImage.color = dividerColor;

        var rt = lineGO.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0f);
        rt.anchorMax        = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta         = new Vector2(dividerWidth, 0f);
        rt.anchoredPosition = Vector2.zero;

        canvasGO.SetActive(false);
    }

    Camera CreateCamera(string goName, int depth)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform);
        // Reprend l'angle de vue configuré sur la Main Camera (ex : 40° côté X, pas forcément une
        // vue du dessus à 90° pile) — l'ancien système copiait cette rotation depuis la Main
        // Camera à chaque frame (SplitScreenEffect.UpdateScreen), plus rien ne le fait
        // automatiquement maintenant qu'elle ne rend plus la scène.
        go.transform.rotation = _mainCam != null ? _mainCam.transform.rotation : Quaternion.Euler(40f, 0f, 0f);

        var c = go.AddComponent<Camera>();
        c.depth = depth;

        // Grille de construction (layer "BuildGrid") masquée par défaut — seule PlayerCamera la
        // réactive pour CETTE caméra pendant que SON joueur construit (voir RW2).
        int buildGridLayer = LayerMask.NameToLayer("BuildGrid");
        if (buildGridLayer >= 0)
            c.cullingMask &= ~(1 << buildGridLayer);

        var playerCam = go.AddComponent<PlayerCamera>();
        playerCam.baseDistance         = cameraHeight;
        playerCam.mergeDistancePerUnit = mergeDistancePerUnit;

        var urpData = go.AddComponent<UniversalAdditionalCameraData>();
        urpData.renderType = CameraRenderType.Base;

        return c;
    }

    // ─── Layout (rects) ───────────────────────────────────────────

    /// <summary>1 joueur actif → sa caméra en plein écran (pas de transition à faire ici, cas simple).</summary>
    void UpdateSoloLayout()
    {
        if (cam0 != null && cam0.gameObject.activeSelf) cam0.rect = new Rect(0f, 0f, 1f, 1f);
        if (cam1 != null && cam1.gameObject.activeSelf) cam1.rect = new Rect(0f, 0f, 1f, 1f);
    }

    /// <summary>
    /// À 2 joueurs, bascule entre écran scindé (loin l'un de l'autre) et une seule caméra plein
    /// écran cadrant les deux (proches) — toujours une coupure verticale nette en mode scindé,
    /// jamais orientée selon leur position relative (contrairement à l'ancien système adaptatif).
    /// Hystérésis autour de mergeDistance pour éviter un flicker pile au seuil. Force le mode
    /// scindé si un des deux joueurs construit (RW2) — la fusion ne s'applique jamais pendant la
    /// construction, pour ne pas interférer avec le dézoom/cadrage curseur de PlayerCamera.
    /// Ne fait que décider l'ÉTAT CIBLE (_merged) — UpdateBlend() gère la transition visuelle.
    /// </summary>
    void UpdateSplitOrMerge()
    {
        if (_targets.Count < 2 || cam0 == null || cam1 == null)
        {
            _merged = false;
            return;
        }

        bool eitherBuilding =
            (_players[0].GetComponent<TopDownController>()?.IsBuilding ?? false) ||
            (_players[1].GetComponent<TopDownController>()?.IsBuilding ?? false);

        float distance = Vector3.Distance(_targets[0].position, _targets[1].position);

        if (eitherBuilding)
            _merged = false;
        else if (_merged)
            _merged = distance < mergeDistance + mergeHysteresis; // reste fusionné tant que sous la borne haute
        else
            _merged = distance < mergeDistance - mergeHysteresis; // ne fusionne qu'en dessous de la borne basse
    }

    /// <summary>
    /// Anime mergeBlend vers l'état cible (_merged) — pilote le cadrage 3D de PlayerCamera en
    /// continu (position/zoom, pas de souci de déformation). La MISE EN PAGE écran (rects), elle,
    /// bascule nette sur le changement de _merged : animer la largeur d'un Camera.rect en continu
    /// déforme le rendu d'une caméra perspective pendant la transition (effet de "swipe" — testé,
    /// rendu visuellement mauvais). Le cadrage caméra déjà en cours de lissage au moment du cut
    /// limite quand même la sensation de saut.
    /// </summary>
    void UpdateBlend()
    {
        if (_targets.Count < 2 || cam0 == null || cam1 == null)
        {
            _mergeBlend = 0f;
            UpdateSoloLayout();
            if (_dividerCanvas != null) _dividerCanvas.gameObject.SetActive(false);
            return;
        }

        float target = _merged ? 1f : 0f;
        _mergeBlend = Mathf.MoveTowards(_mergeBlend, target, mergeTransitionSpeed * Time.deltaTime);

        var cam0Player = cam0.GetComponent<PlayerCamera>();
        cam0Player.mergeWithPlayer = _players[1];
        cam0Player.mergeBlend      = _mergeBlend;

        if (_merged)
        {
            cam1.gameObject.SetActive(false);
            cam0.rect = new Rect(0f, 0f, 1f, 1f);
        }
        else
        {
            if (!cam1.gameObject.activeSelf) cam1.gameObject.SetActive(true);
            cam1.GetComponent<PlayerCamera>().player = _players[1];
            cam0.rect = new Rect(0f, 0f, 0.5f, 1f);
            cam1.rect = new Rect(0.5f, 0f, 0.5f, 1f);
        }

        if (_dividerCanvas != null)
        {
            bool show = !_merged;
            if (_dividerCanvas.gameObject.activeSelf != show)
                _dividerCanvas.gameObject.SetActive(show);
            if (_dividerImage != null) _dividerImage.color = dividerColor;
        }
    }

    // ─── Gestion joueurs ──────────────────────────────────────────

    void HandlePlayerJoined(PlayerInput pi)
    {
        _targets.Add(pi.transform);
        _players.Add(pi);
        int idx = _targets.Count - 1;

        if (idx == 0)
        {
            cam0.gameObject.SetActive(true);
            cam0.GetComponent<PlayerCamera>().player = pi;
        }
        else if (idx == 1)
        {
            cam1.gameObject.SetActive(true);
            cam1.GetComponent<PlayerCamera>().player = pi;
        }

        if (_targets.Count < 2) UpdateSoloLayout();
    }

    void HandlePlayerLeft(PlayerInput pi)
    {
        _targets.Remove(pi.transform);
        _players.Remove(pi);

        // Repart d'un état propre — évite un mergeWithPlayer/mergeBlend périmé.
        _merged     = false;
        _mergeBlend = 0f;
        cam0.GetComponent<PlayerCamera>().mergeWithPlayer = null;
        cam0.GetComponent<PlayerCamera>().mergeBlend      = 0f;

        cam0.gameObject.SetActive(false);
        cam1.gameObject.SetActive(false);
        if (_dividerCanvas != null) _dividerCanvas.gameObject.SetActive(false);

        for (int i = 0; i < _targets.Count; i++)
        {
            var cam = i == 0 ? cam0 : cam1;
            cam.gameObject.SetActive(true);
            cam.GetComponent<PlayerCamera>().player = _players[i];
        }

        if (_targets.Count < 2) UpdateSoloLayout();
    }
}
