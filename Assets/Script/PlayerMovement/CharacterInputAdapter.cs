using UnityEngine;


[RequireComponent(typeof(PerPlayerInput))]
public class CharacterInputAdapter : MonoBehaviour
{
    PerPlayerInput input;


    void Awake() => input = GetComponent<PerPlayerInput>();


    void Update()
    {
        Vector2 m = input.Move; // applique à ton contrôleur de déplacement
        bool j = input.JumpPressed;
        // TODO: appeler ici tes méthodes de mouvement/saut.
        // Exemple:
        // GetComponent<YourMover>().SetMove(m);
        // if (j) GetComponent<YourMover>().Jump();
    }
}