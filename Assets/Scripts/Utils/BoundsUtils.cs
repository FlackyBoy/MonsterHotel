using UnityEngine;

/// <summary>
/// Calcule l'AABB monde d'un GameObject en agrégeant les Renderer de ses enfants.
/// Si le GO est un FurnitureInstance, utilise CurrentVisual pour ignorer les enfants
/// pending-Destroy qui fausseraient les bounds.
/// </summary>
public static class BoundsUtils
{
    public static Bounds Get(GameObject go)
    {
        if (go == null) return new Bounds(Vector3.zero, Vector3.one * 0.5f);

        var fi = go.GetComponent<FurnitureInstance>();
        var source = (fi != null && fi.CurrentVisual != null) ? fi.CurrentVisual : go;

        var rends = source.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.5f);

        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }
}
