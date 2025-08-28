using UnityEngine;
using Unity.Cinemachine;

//DISABLE if using old input system
using UnityEngine.InputSystem;
using System.Collections;

namespace PhysicsCharacterController
{

    public class CameraManager : MonoBehaviour
    {
        [Header("Camera properties")]
        //public CinemachineVirtualCamera firstPersonCamera;
        public CinemachineCamera thirdPersonCamera;
        public Camera mainCamera;
        public GameObject player;
        public CharacterManager characterManager;
        [Space(10)]

        public LayerMask firstPersonMask;
        public float firstPersonMaskChangeDelay = 0.1f;
        public float firstPersonHeightOnTransition = 0f;
        [Space(10)]

        public LayerMask thirdPersonMask;
        public float thirdPersonMaskChangeDelay = 0.1f;
        public float thirdPersonHeightOnTransition = 0.5f;
        [Space(10)]

        public bool activeThirdPerson = true;
        public bool activeDebug = true;


        //private FirstPersonCameraController firstPersonCameraController;
        //private CinemachinePOV firstPersonCameraControllerPOV;

        private ThirdPersonCameraController thirdPersonCameraController;


        /**/



        private void Awake()
        {
            // Ne touche pas à characterManager ici
            if (thirdPersonCamera != null)
                thirdPersonCameraController = thirdPersonCamera.GetComponent<ThirdPersonCameraController>();
        }

        private void Start()
        {
            player = GameObject.FindWithTag("Player");
            if (player == null) { Debug.LogError("[CameraManager] Tag 'Player' introuvable."); enabled = false; return; }

            characterManager = player.GetComponent<CharacterManager>();
            if (characterManager == null) { Debug.LogError("[CameraManager] CharacterManager manquant sur Player."); enabled = false; return; }

            SetCamera();   // Maintenant characterManager est prêt
            SetDebug();
        }

        private void Update()
        {
            Debug.Log("Player "+player.name);

            //DISABLE if using old input system

            if (Keyboard.current.mKey.wasPressedThisFrame)
            {
                activeThirdPerson = !activeThirdPerson;
                SetCamera();
            }

            //DISABLE if using old input system
            if (Keyboard.current.nKey.wasPressedThisFrame)
            {
                SetDebug();
            }

            //ENABLE if using old input system

            /*

            if (Input.GetKeyDown(KeyCode.M))
            {
                activeThirdPerson = !activeThirdPerson;
                SetCamera();
            }

            if (Input.GetKeyDown(KeyCode.N))
            {
                SetDebug();
            }

            */
        }


        public void SetCamera()
        {
            if (activeThirdPerson)
            {
                characterManager.SetLockToCamera(false);

                //firstPersonCamera.gameObject.SetActive(false);
                thirdPersonCamera.gameObject.SetActive(true);

               // thirdPersonCameraController.SetInitialValue(firstPersonCameraControllerPOV.m_HorizontalAxis.Value, thirdPersonHeightOnTransition);

                StartCoroutine(UpdateMask(thirdPersonMaskChangeDelay, thirdPersonMask));
            }
            else
            {
                characterManager.SetLockToCamera(true);

               // firstPersonCamera.gameObject.SetActive(true);
                thirdPersonCamera.gameObject.SetActive(false);

                //firstPersonCameraController.SetInitialValue(thirdPersonCamera.m_XAxis.Value, firstPersonHeightOnTransition);

                StartCoroutine(UpdateMask(firstPersonMaskChangeDelay, firstPersonMask));
            }
        }


        public void SetDebug()
        {
            characterManager.debug = !characterManager.debug;
        }


        private IEnumerator UpdateMask(float _duration, LayerMask _mask)
        {
            yield return new WaitForSeconds(_duration);
            mainCamera.cullingMask = _mask;
        }
    }
}