using Cinemachine;
using UnityEngine;


namespace PhysicsCharacterController
{
    public class ThirdPersonCameraController : MonoBehaviour
    {
        [Header("Camera controls")]
        public Vector2 mouseSensivity = new Vector2(5f, 1f);
        public float smoothSpeed = 0.17f;


        private CinemachineFreeLook cinemachineFreeLook;

        private MovementActions movementActions;
        private Vector2 smoothVelocity;
        private Vector2 currentInputVector;
        private Vector2 input;

        private float switchValueX;
        private float switchValueY;


        /**/


        private void Awake()
        {
            cinemachineFreeLook = this.GetComponent<CinemachineFreeLook>();
            movementActions = new MovementActions();
        }


        private void Update()
        {
            //DISABLE if using old input system
            input += movementActions.Gameplay.Camera.ReadValue<Vector2>() * mouseSensivity * new Vector2(0.01f, 0.001f);

            //ENABLE if using old input system
            //input = Input.mousePosition * mouseSensivity * new Vector2(0.01f, 0.001f);

            if (input.y > 1f) input.y = 1f;
            else if (input.y < 0f) input.y = 0f;

            currentInputVector = Vector2.SmoothDamp(currentInputVector, input, ref smoothVelocity, smoothSpeed);
            cinemachineFreeLook.m_XAxis.Value = currentInputVector.x;
            cinemachineFreeLook.m_YAxis.Value = currentInputVector.y;
        }


        public void SetInitialValue(float _valueX, float _valueY)
        {
            input = new Vector2(_valueX, _valueY);
            currentInputVector = input;

            cinemachineFreeLook.m_XAxis.Value = _valueX;
            cinemachineFreeLook.m_YAxis.Value = _valueY;
        }


        #region Enable / Disable

        //DISABLE if using old input system
        private void OnEnable()
        {
            movementActions.Enable();
        }


        //DISABLE if using old input system
        private void OnDisable()
        {
            movementActions.Disable();
        }

        #endregion

    }
}