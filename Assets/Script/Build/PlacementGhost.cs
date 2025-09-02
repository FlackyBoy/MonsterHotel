using System.Collections.Generic;
using UnityEngine;

// Affiche un clone "ghost" avec matériel OK/NOK
public class PlacementGhost : MonoBehaviour
{
    readonly List<Renderer> rends = new();
    Material ok, nok;

    public void InitFromPrefab(GameObject prefabOrNull, Vector3 sizeFallback, Material okMat, Material nokMat)
    {
        ok = okMat; nok = nokMat;
        if (prefabOrNull)
        {
            var inst = Instantiate(prefabOrNull, transform);
            Collect(inst);
        }
        else
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(transform, false);
            go.transform.localScale = sizeFallback;
            Destroy(go.GetComponent<Collider>());
            Collect(go);
        }
    }

    void Collect(GameObject root)
    {
        root.layer = gameObject.layer;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            rends.Add(r);
        }
    }

    public void SetOK(bool isOk)
    {
        var m = isOk ? ok : nok;
        for (int i = 0; i < rends.Count; i++)
        {
            var r = rends[i];
            if (r) r.sharedMaterial = m;
        }
    }
}
