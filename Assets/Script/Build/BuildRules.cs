using UnityEngine;

[CreateAssetMenu(fileName = "BuildRules", menuName = "MonsterHotel/Build Rules")]
public class BuildRules : ScriptableObject
{
    [Header("Layers")]
    public LayerMask groundMask;        // Sol porteur
    public LayerMask blockMask;         // Ce qui bloque la pose (Room, Item, NoBuild)
    public LayerMask corridorMask;      // Colliders de couloir pour porte
    public LayerMask roomVolumeMask;    // Triggers volume intérieurs des chambres

    [Header("Grille")]
    public float gridSizeRoom = 0.5f;
    public float gridSizeItem = 0.25f;

    [Header("Tolérances")]
    public float maxSlopeDeg = 5f;        // pente max du sol
    public float doorConnectRadius = 0.4f;// rayon test de porte
    public float supportRay = 2f;         // longueur raycast vers le bas

    [Header("Matériaux Ghost")]
    public Material ghostOK;
    public Material ghostNOK;
}
