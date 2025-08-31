using UnityEngine;

[DefaultExecutionOrder(1000)] // apr�s la plupart des contr�leurs
public class FollowTargetSmooth : MonoBehaviour
{
    [Header("Cible")]
    public Transform target;

    [Header("Param�tres")]
    public Vector3 offset = new Vector3(0f, 22f, 0f);
    public bool lookAtTarget = false;
    public float posLerp = 12f; // plus grand = plus r�actif
    public float rotLerp = 10f;

    Vector3 _vel; // pas utilis� pour SmoothDamp ici, mais garde si besoin

    void LateUpdate()
    {
        if (!target) return;

        // Lerp exponentiel pour une vitesse constante ind�pendamment du framerate
        float kp = 1f - Mathf.Exp(-posLerp * Time.deltaTime);
        Vector3 wanted = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, wanted, kp);

        if (lookAtTarget)
        {
            float kr = 1f - Mathf.Exp(-rotLerp * Time.deltaTime);
            Vector3 dir = (target.position - transform.position);
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion wantedRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, wantedRot, kr);
            }
        }
        // Si lookAtTarget = false, on conserve l�orientation actuelle (par ex. X=90� fix� dans l�Inspector).
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        posLerp = Mathf.Max(0f, posLerp);
        rotLerp = Mathf.Max(0f, rotLerp);
    }
#endif
}
