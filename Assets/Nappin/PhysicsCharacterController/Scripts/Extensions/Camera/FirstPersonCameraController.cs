using Cinemachine;
using UnityEngine;


namespace PhysicsCharacterController
{
    public class FirstPersonCameraController : MonoBehaviour
    {
        [Header("Camera controls")]
        public Vector2 mouseSensivity = new Vector2(8f, -50f);
        public float smoothSpeed = 0.01f;


        private CinemachinePOV cinemachinePOV;

        private MovementActions movementActions;
        private Vector2 smoothVelocity;
        private Vector2 currentInputVector;
        private Vector2 input;

        private float switchValueX;
        private float switchValueY;


        /**/


        private void Awake()
        {
            cinemachinePOV = this.GetComponent<CinemachineVirtualCamera>().GetCinemachineComponent<CinemachinePOV>();
            movementActions = new MovementActions();
        }


        private void Update()
        {
            //DISABLE if using old input system
            input += movementActions.Gameplay.Camera.ReadValue<Vector2>() * mouseSensivity * new Vector2(0.01f, 0.001f);

            //ENABLE if using old input system
            //input = Input.mousePosition * mouseSensivity * new Vector2(0.01f, 0.001f);

            if (input.y > cinemachinePOV.m_VerticalAxis.m_MaxValue) input.y = cinemachinePOV.m_VerticalAxis.m_MaxValue;
            else if (input.y < cinemachinePOV.m_VerticalAxis.m_MinValue) input.y = cinemachinePOV.m_VerticalAxis.m_MinValue;

            currentInputVector = Vector2.SmoothDamp(currentInputVector, input, ref smoothVelocity, smoothSpeed);
            cinemachinePOV.m_HorizontalAxis.Value = currentInputVector.x;
            cinemachinePOV.m_VerticalAxis.Value = currentInputVector.y;
        }


        public void SetInitialValue(float _valueX, float _valueY)
        {
            input = new Vector2(_valueX, _valueY);
            currentInputVector = input;

            cinemachinePOV.m_HorizontalAxis.Value = _valueX;
            cinemachinePOV.m_VerticalAxis.Value = _valueY;
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