// Assets/Scripts/Build/BuildSmokeTest.cs
using UnityEngine;
using UnityEngine.InputSystem; // Input System (Keyboard.current)

public class BuildSmokeTest : MonoBehaviour
{
    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
        {
            Debug.Log("[Smoke] B press détecté -> spawn cube");
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "SMOKE_CUBE";
            go.transform.position = transform.position + transform.forward * 2f + Vector3.up * 1f;
            go.transform.localScale = new Vector3(1, 1, 1);
            // matériau bien visible
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Color.green };
        }
    }
}
