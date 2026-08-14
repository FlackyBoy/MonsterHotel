using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Caméra d'un joueur en coop écran splitté STATIQUE (RW1) — possède entièrement son
/// transform.position chaque frame, sans dépendance d'ordre d'exécution avec un autre script
/// (contrairement à l'ancien système adaptatif où SplitScreenEffect réécrivait la position en
/// premier, obligeant CameraShaker/BuildModeCameraZoom à s'exécuter après lui).
///
/// Consolide ce qui était avant réparti entre CameraShaker, BuildModeCameraZoom et
/// BuildCursorTargetProvider (RW2, système adaptatif) — plus simple maintenant que chaque joueur
/// a sa propre caméra indépendante, plus besoin du hook ISplitScreenTargetPosition du package.
///
/// Mode fusion (joueurs proches, voir SplitScreenManager) : si mergeWithPlayer est renseigné, le
/// cadrage et le dézoom sont lissés en continu entre "suivre seulement player" et "cadrer le point
/// milieu des deux joueurs" via mergeBlend (0 = solo, 1 = fusion complète) — piloté frame par frame
/// par SplitScreenManager pour une transition fluide, pas un basculement instantané.
///
/// Un par caméra joueur (cam0/cam1), créé par SplitScreenManager.CreateCamera().
/// </summary>
public class PlayerCamera : MonoBehaviour
{
    /// <summary>Joueur suivi par cette caméra — assigné par SplitScreenManager à la jointure.</summary>
    public PlayerInput player;

    /// <summary>Non-null dès qu'un 2e joueur existe : cible de fusion potentielle — voir mergeBlend pour l'intensité réelle.</summary>
    public PlayerInput mergeWithPlayer;

    /// <summary>0 = suit uniquement "player" (mode normal/solo), 1 = cadre le point milieu des deux joueurs (fusion complète) — piloté en continu par SplitScreenManager pour une transition lissée.</summary>
    [Range(0f, 1f)] public float mergeBlend;

    /// <summary>Distance de base caméra ↔ joueur (vue top-down) — voir SplitScreenManager.cameraHeight.</summary>
    public float baseDistance = 22f;

    /// <summary>En fusion complète, dézoom additionnel par unité de distance entre les deux joueurs — assigné par SplitScreenManager à la création (réglable là-bas, en Edit mode, pas ici).</summary>
    public float mergeDistancePerUnit = 0.6f;

    Camera  _camera;
    int     _buildGridLayer = -1;
    float   _extraDistance;
    Vector3 _smoothedFollowTarget;
    bool    _followTargetInitialized;

    // Shake (repris tel quel de l'ancien CameraShaker.cs)
    float _shakeAmplitude, _shakeDuration, _shakeFrequency, _shakeElapsed;
    bool  _shaking;

    void Awake()
    {
        _camera = GetComponent<Camera>();
        _buildGridLayer = LayerMask.NameToLayer("BuildGrid");
    }

    /// <summary>Déclenche un shake caméra — appelé par FeedbackManager.ShakeCamera().</summary>
    public void Shake(float amplitude, float duration, float frequency)
    {
        _shakeAmplitude = amplitude;
        _shakeDuration  = duration;
        _shakeFrequency = frequency;
        _shakeElapsed   = 0f;
        _shaking        = true;
    }

    void LateUpdate()
    {
        if (player == null) return;

        var controller = player.GetComponent<TopDownController>();
        bool building   = controller != null && controller.IsBuilding;

        var cfg = HotelConfig.Camera;
        float zoomTarget   = building ? (cfg != null ? cfg.buildModeExtraDistance : 12f) : 0f;
        float zoomSpeed    = cfg != null ? cfg.buildModeZoomLerpSpeed : 4f;
        float followSpeed  = cfg != null ? cfg.buildModeCameraFollowSpeed : 15f;
        _extraDistance = Mathf.Lerp(_extraDistance, zoomTarget, zoomSpeed * Time.deltaTime);

        // Cible "solo" : le joueur en temps normal (suivi direct, sans lissage, comme avant) —
        // pendant la construction, lisse vers le curseur de placement pour éviter un saut de
        // caméra à l'entrée/sortie du mode (reprend BuildCursorTargetProvider).
        Vector3 soloTarget;
        if (building)
        {
            if (!_followTargetInitialized) _smoothedFollowTarget = player.transform.position;
            _smoothedFollowTarget = Vector3.Lerp(_smoothedFollowTarget, controller.BuildCursorWorldPos, followSpeed * Time.deltaTime);
            soloTarget = _smoothedFollowTarget;
        }
        else
        {
            soloTarget = player.transform.position;
        }
        _followTargetInitialized = true;

        Vector3 followTarget = soloTarget;
        float   distance     = baseDistance + _extraDistance;

        // Mélange continu vers le cadrage fusion selon mergeBlend — pas de branche binaire, donc
        // pas de saut de caméra quand SplitScreenManager fait varier mergeBlend au fil du temps.
        if (mergeWithPlayer != null && mergeBlend > 0.0001f)
        {
            Vector3 p2         = mergeWithPlayer.transform.position;
            Vector3 mergedMid  = (player.transform.position + p2) * 0.5f;
            float   separation = Vector3.Distance(player.transform.position, p2);
            float   mergedDist = baseDistance + separation * mergeDistancePerUnit;

            followTarget = Vector3.Lerp(soloTarget, mergedMid, mergeBlend);
            distance     = Mathf.Lerp(distance, mergedDist, mergeBlend);
        }

        Vector3 basePos = followTarget + transform.rotation * new Vector3(0f, 0f, -distance);
        transform.position = basePos + ComputeShakeOffset();

        // Grille de construction (layer "BuildGrid") — visible uniquement pour ce joueur pendant
        // qu'il construit (voir GridVisualBuilder).
        if (_camera != null && _buildGridLayer >= 0)
        {
            int bit = 1 << _buildGridLayer;
            _camera.cullingMask = building
                ? (_camera.cullingMask | bit)
                : (_camera.cullingMask & ~bit);
        }
    }

    Vector3 ComputeShakeOffset()
    {
        if (!_shaking) return Vector3.zero;

        _shakeElapsed += Time.deltaTime;
        if (_shakeElapsed >= _shakeDuration)
        {
            _shaking = false;
            return Vector3.zero;
        }

        // Offset horizontal en vue top-down (identique à l'ancien CameraShaker)
        float envelope = 1f - (_shakeElapsed / _shakeDuration);
        float offsetX  = Mathf.Sin(_shakeElapsed * _shakeFrequency * Mathf.PI * 2f) * _shakeAmplitude * envelope;
        float offsetZ  = Mathf.Cos(_shakeElapsed * _shakeFrequency * 1.3f * Mathf.PI * 2f) * _shakeAmplitude * 0.5f * envelope;
        return new Vector3(offsetX, 0f, offsetZ);
    }
}
