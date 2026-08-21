using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rend le joueur visible (silhouette) même quand un objet opaque (bloc destructible, mur Lit
/// standard...) le cache à la caméra — voir Assets/Shaders/PlayerSilhouette.shader (ZTest Greater,
/// ne dessine que les pixels déjà occultés dans le depth buffer). Aucune détection géométrique
/// côté C#, purement porté par le GPU.
///
/// Setup : créer un matériau avec le shader "MonsterHotel/PlayerSilhouette" et l'assigner ici.
/// </summary>
public class PlayerSilhouette : MonoBehaviour
{
    public Material silhouetteMaterial;

    void Start()
    {
        if (silhouetteMaterial == null) return;

        foreach (var rend in GetComponentsInChildren<Renderer>())
        {
            var mats = rend.sharedMaterials;
            bool alreadyApplied = false;
            foreach (var m in mats)
                if (m == silhouetteMaterial) { alreadyApplied = true; break; }
            if (alreadyApplied) continue;

            var newMats = new List<Material>(mats) { silhouetteMaterial };
            rend.sharedMaterials = newMats.ToArray();
        }
    }
}
