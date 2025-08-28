using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem; // ← PlayerInput / InputAction

namespace PhysicsCharacterController
{
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class CharacterManager : MonoBehaviour
    {
        [Header("Movement specifics")]
        [SerializeField] LayerMask groundMask;
        public float movementSpeed = 14f;
        [Range(0f, 1f)] public float crouchSpeedMultiplier = 0.248f;
        [Range(0.01f, 0.99f)] public float movementThrashold = 0.01f;
        [Space(10)]
        public float dampSpeedUp = 0.2f;
        public float dampSpeedDown = 0.1f;

        [Header("Jump and gravity specifics")]
        public float jumpVelocity = 20f;
        public float fallMultiplier = 1.7f;
        public float holdJumpMultiplier = 5f;
        [Range(0f, 1f)] public float frictionAgainstFloor = 0.3f;
        [Range(0.01f, 0.99f)] public float frictionAgainstWall = 0.839f;
        [Space(10)]
        public bool canLongJump = true;

        [Header("Slope and step specifics")]
        public float groundCheckerThrashold = 0.1f;
        public float slopeCheckerThrashold = 0.51f;
        public float stepCheckerThrashold = 0.6f;
        [Space(10)]
        [Range(1f, 89f)] public float maxClimbableSlopeAngle = 53.6f;
        public float maxStepHeight = 0.74f;
        [Space(10)]
        public AnimationCurve speedMultiplierOnAngle = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Range(0.01f, 1f)] public float canSlideMultiplierCurve = 0.061f;
        [Range(0.01f, 1f)] public float cantSlideMultiplierCurve = 0.039f;
        [Range(0.01f, 1f)] public float climbingStairsMultiplierCurve = 0.637f;
        [Space(10)]
        public float gravityMultiplier = 6f;
        public float gravityMultiplyerOnSlideChange = 3f;
        public float gravityMultiplierIfUnclimbableSlope = 30f;
        [Space(10)]
        public bool lockOnSlope = false;

        [Header("Wall slide specifics")]
        public float wallCheckerThrashold = 0.8f;
        public float hightWallCheckerChecker = 0.5f;
        [Space(10)]
        public float jumpFromWallMultiplier = 30f;
        public float multiplierVerticalLeap = 1f;

        [Header("Sprint and crouch specifics")]
        public float sprintSpeed = 20f;
        public float crouchHeightMultiplier = 0.5f;
        public Vector3 POV_normalHeadHeight = new Vector3(0f, 0.5f, -0.1f);
        public Vector3 POV_crouchHeadHeight = new Vector3(0f, -0.1f, -0.1f);

        [Header("References")]
        public GameObject characterCamera;
        public GameObject characterModel;
        public float characterModelRotationSmooth = 0.1f;
        [Space(10)]
        public GameObject meshCharacter;
        public GameObject meshCharacterCrouch;
        public Transform headPoint;
        [Space(10)]

        public bool debug = true;

        [Header("Events")]
        [SerializeField] UnityEvent OnJump;
        [Space(15)]
        public float minimumVerticalSpeedToLandEvent;
        [SerializeField] UnityEvent OnLand;
        [Space(15)]
        public float minimumHorizontalSpeedToFastEvent;
        [SerializeField] UnityEvent OnFast;
        [Space(15)]
        [SerializeField] UnityEvent OnWallSlide;
        [Space(15)]
        [SerializeField] UnityEvent OnSprint;
        [Space(15)]
        [SerializeField] UnityEvent OnCrouch;

        // Vecteurs et états de sol/murs
        private Vector3 forward, globalForward, reactionForward;
        private Vector3 down, globalDown, reactionGlobalDown;
        private float currentSurfaceAngle;
        private bool currentLockOnSlope;
        private Vector3 wallNormal, groundNormal, prevGroundNormal;
        private bool prevGrounded;

        private float coyoteJumpMultiplier = 1f;
        private bool isGrounded, isTouchingSlope, isTouchingStep, isTouchingWall, isJumping, isCrouch;

        // ===== Inputs par joueur (remplacent InputReader) =====
        private Vector2 axisInput;
        private bool jump;      // impulsion (edge)
        private bool jumpHold;  // maintien
        private bool sprint;
        private bool crouch;

        // PlayerInput local + actions
        private PlayerInput pi;
        private InputAction moveA, jumpA, sprintA, crouchA;

        [HideInInspector] public float targetAngle;
        private Rigidbody rigidbody;
        private CapsuleCollider collider;
        private float originalColliderHeight;
        private Vector3 currVelocity = Vector3.zero;
        private float turnSmoothVelocity;
        private bool lockRotation = false;
        private bool lockToCamera = false;

        private void Awake()
        {
            rigidbody = GetComponent<Rigidbody>();
            collider = GetComponent<CapsuleCollider>();
            originalColliderHeight = collider.height;

            if (!characterCamera) characterCamera = GameObject.FindWithTag("MainCamera");

            // Récupère les actions du PlayerInput local
            pi = GetComponent<PlayerInput>();
            if (pi != null && pi.actions != null)
            {
                moveA = pi.actions.FindAction("Movement", false);
                jumpA = pi.actions.FindAction("Jump", false);
                sprintA = pi.actions.FindAction("Sprint", false);
                crouchA = pi.actions.FindAction("Crouch", false);

                moveA?.Enable();
                jumpA?.Enable();
                sprintA?.Enable();
                crouchA?.Enable();
            }
            else
            {
                Debug.LogError("CharacterManager: PlayerInput ou actions manquants sur ce prefab.");
            }

            SetFriction(frictionAgainstFloor, true);
            currentLockOnSlope = lockOnSlope;
        }

        private void Update()
        {
            // Lecture directe (polling) des actions locales
            axisInput = moveA != null ? moveA.ReadValue<Vector2>() : Vector2.zero;
            jumpHold = jumpA != null && jumpA.ReadValue<float>() > 0.5f;
            sprint = sprintA != null && sprintA.ReadValue<float>() > 0.5f;
            crouch = crouchA != null && crouchA.ReadValue<float>() > 0.5f;
            if (jumpA != null && jumpA.triggered) jump = true;
        }

        private void FixedUpdate()
        {
            CheckGrounded();
            CheckStep();
            CheckWall();
            CheckSlopeAndDirections();

            MoveCrouch();
            MoveWalk();

            if (!lockToCamera) MoveRotation();
            else ForceRotation();

            MoveJump();

            // reset impulsion saut après traitement physique
            jump = false;

            ApplyGravity();
            UpdateEvents();
        }

        // ====== Callbacks optionnels si tu utilises PlayerInputBinder (facultatif) ======
        public void Input_Movement(InputAction.CallbackContext ctx)
        {
            axisInput = ctx.ReadValue<Vector2>();
        }
        public void Input_Jump(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) jump = true;
            jumpHold = ctx.ReadValueAsButton();
        }
        public void Input_Sprint(InputAction.CallbackContext ctx)
        {
            sprint = ctx.ReadValueAsButton();
        }
        public void Input_Crouch(InputAction.CallbackContext ctx)
        {
            crouch = ctx.ReadValueAsButton();
        }
        // ==============================================================================

        #region Checks
        private void CheckGrounded()
        {
            prevGrounded = isGrounded;
            isGrounded = Physics.CheckSphere(transform.position - new Vector3(0, originalColliderHeight / 2f, 0), groundCheckerThrashold, groundMask);
        }

        private void CheckStep()
        {
            bool tmpStep = false;
            Vector3 bottomStepPos = transform.position - new Vector3(0f, originalColliderHeight / 2f, 0f) + new Vector3(0f, 0.05f, 0f);

            RaycastHit stepLowerHit;
            if (Physics.Raycast(bottomStepPos, globalForward, out stepLowerHit, stepCheckerThrashold, groundMask))
            {
                RaycastHit stepUpperHit;
                if (RoundValue(stepLowerHit.normal.y) == 0 && !Physics.Raycast(bottomStepPos + new Vector3(0f, maxStepHeight, 0f), globalForward, out stepUpperHit, stepCheckerThrashold + 0.05f, groundMask))
                    tmpStep = true;
            }

            RaycastHit stepLowerHit45;
            if (Physics.Raycast(bottomStepPos, Quaternion.AngleAxis(45, transform.up) * globalForward, out stepLowerHit45, stepCheckerThrashold, groundMask))
            {
                RaycastHit stepUpperHit45;
                if (RoundValue(stepLowerHit45.normal.y) == 0 && !Physics.Raycast(bottomStepPos + new Vector3(0f, maxStepHeight, 0f), Quaternion.AngleAxis(45, Vector3.up) * globalForward, out stepUpperHit45, stepCheckerThrashold + 0.05f, groundMask))
                    tmpStep = true;
            }

            RaycastHit stepLowerHitMinus45;
            if (Physics.Raycast(bottomStepPos, Quaternion.AngleAxis(-45, transform.up) * globalForward, out stepLowerHitMinus45, stepCheckerThrashold, groundMask))
            {
                RaycastHit stepUpperHitMinus45;
                if (RoundValue(stepLowerHitMinus45.normal.y) == 0 && !Physics.Raycast(bottomStepPos + new Vector3(0f, maxStepHeight, 0f), Quaternion.AngleAxis(-45, Vector3.up) * globalForward, out stepUpperHitMinus45, stepCheckerThrashold + 0.05f, groundMask))
                    tmpStep = true;
            }

            isTouchingStep = tmpStep;
        }

        private void CheckWall()
        {
            bool tmpWall = false;
            Vector3 tmpWallNormal = Vector3.zero;
            Vector3 topWallPos = new Vector3(transform.position.x, transform.position.y + hightWallCheckerChecker, transform.position.z);

            RaycastHit wallHit;
            if (Physics.Raycast(topWallPos, globalForward, out wallHit, wallCheckerThrashold, groundMask)) { tmpWallNormal = wallHit.normal; tmpWall = true; }
            else if (Physics.Raycast(topWallPos, Quaternion.AngleAxis(45, transform.up) * globalForward, out wallHit, wallCheckerThrashold, groundMask)) { tmpWallNormal = wallHit.normal; tmpWall = true; }
            else if (Physics.Raycast(topWallPos, Quaternion.AngleAxis(90, transform.up) * globalForward, out wallHit, wallCheckerThrashold, groundMask)) { tmpWallNormal = wallHit.normal; tmpWall = true; }
            else if (Physics.Raycast(topWallPos, Quaternion.AngleAxis(135, transform.up) * globalForward, out wallHit, wallCheckerThrashold, groundMask)) { tmpWallNormal = wallHit.normal; tmpWall = true; }
            else if (Physics.Raycast(topWallPos, Quaternion.AngleAxis(180, transform.up) * globalForward, out wallHit, wallCheckerThrashold, groundMask)) { tmpWallNormal = wallHit.normal; tmpWall = true; }
            else if (Physics.Raycast(topWallPos, Quaternion.AngleAxis(225, transform.up) * globalForward, out wallHit, wallCheckerThrashold, groundMask)) { tmpWallNormal = wallHit.normal; tmpWall = true; }
            else if (Physics.Raycast(topWallPos, Quaternion.AngleAxis(270, transform.up) * globalForward, out wallHit, wallCheckerThrashold, groundMask)) { tmpWallNormal = wallHit.normal; tmpWall = true; }
            else if (Physics.Raycast(topWallPos, Quaternion.AngleAxis(315, transform.up) * globalForward, out wallHit, wallCheckerThrashold, groundMask)) { tmpWallNormal = wallHit.normal; tmpWall = true; }

            isTouchingWall = tmpWall;
            wallNormal = tmpWallNormal;
        }

        private void CheckSlopeAndDirections()
        {
            prevGroundNormal = groundNormal;

            RaycastHit slopeHit;
            if (Physics.SphereCast(transform.position, slopeCheckerThrashold, Vector3.down, out slopeHit, originalColliderHeight / 2f + 0.5f, groundMask))
            {
                groundNormal = slopeHit.normal;

                if (slopeHit.normal.y == 1)
                {
                    forward = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
                    globalForward = forward;
                    reactionForward = forward;

                    SetFriction(frictionAgainstFloor, true);
                    currentLockOnSlope = lockOnSlope;

                    currentSurfaceAngle = 0f;
                    isTouchingSlope = false;
                }
                else
                {
                    Vector3 tmpGlobalForward = transform.forward.normalized;
                    Vector3 tmpForward = new Vector3(tmpGlobalForward.x, Vector3.ProjectOnPlane(transform.forward.normalized, slopeHit.normal).normalized.y, tmpGlobalForward.z);
                    Vector3 tmpReactionForward = new Vector3(tmpForward.x, tmpGlobalForward.y - tmpForward.y, tmpForward.z);

                    if (currentSurfaceAngle <= maxClimbableSlopeAngle && !isTouchingStep)
                    {
                        forward = tmpForward * ((speedMultiplierOnAngle.Evaluate(currentSurfaceAngle / 90f) * canSlideMultiplierCurve) + 1f);
                        globalForward = tmpGlobalForward * ((speedMultiplierOnAngle.Evaluate(currentSurfaceAngle / 90f) * canSlideMultiplierCurve) + 1f);
                        reactionForward = tmpReactionForward * ((speedMultiplierOnAngle.Evaluate(currentSurfaceAngle / 90f) * canSlideMultiplierCurve) + 1f);

                        SetFriction(frictionAgainstFloor, true);
                        currentLockOnSlope = lockOnSlope;
                    }
                    else if (isTouchingStep)
                    {
                        forward = tmpForward * ((speedMultiplierOnAngle.Evaluate(currentSurfaceAngle / 90f) * climbingStairsMultiplierCurve) + 1f);
                        globalForward = tmpGlobalForward * ((speedMultiplierOnAngle.Evaluate(currentSurfaceAngle / 90f) * climbingStairsMultiplierCurve) + 1f);
                        reactionForward = tmpReactionForward * ((speedMultiplierOnAngle.Evaluate(currentSurfaceAngle / 90f) * climbingStairsMultiplierCurve) + 1f);

                        SetFriction(frictionAgainstFloor, true);
                        currentLockOnSlope = true;
                    }
                    else
                    {
                        forward = tmpForward * ((speedMultiplierOnAngle.Evaluate(currentSurfaceAngle / 90f) * cantSlideMultiplierCurve) + 1f);
                        globalForward = tmpGlobalForward * ((speedMultiplierOnAngle.Evaluate(currentSurfaceAngle / 90f) * cantSlideMultiplierCurve) + 1f);
                        reactionForward = tmpReactionForward * ((speedMultiplierOnAngle.Evaluate(currentSurfaceAngle / 90f) * cantSlideMultiplierCurve) + 1f);

                        SetFriction(0f, true);
                        currentLockOnSlope = lockOnSlope;
                    }

                    currentSurfaceAngle = Vector3.Angle(Vector3.up, slopeHit.normal);
                    isTouchingSlope = true;
                }

                down = Vector3.Project(Vector3.down, slopeHit.normal);
                globalDown = Vector3.down.normalized;
                reactionGlobalDown = Vector3.up.normalized;
            }
            else
            {
                groundNormal = Vector3.zero;

                forward = Vector3.ProjectOnPlane(transform.forward, slopeHit.normal).normalized;
                globalForward = forward;
                reactionForward = forward;

                down = Vector3.down.normalized;
                globalDown = Vector3.down.normalized;
                reactionGlobalDown = Vector3.up.normalized;

                SetFriction(frictionAgainstFloor, true);
                currentLockOnSlope = lockOnSlope;
            }
        }
        #endregion

        #region Move
        private void MoveCrouch()
        {
            if (crouch && isGrounded)
            {
                isCrouch = true;
                if (meshCharacterCrouch && meshCharacter) meshCharacter.SetActive(false);
                if (meshCharacterCrouch) meshCharacterCrouch.SetActive(true);

                float newHeight = originalColliderHeight * crouchHeightMultiplier;
                collider.height = newHeight;
                collider.center = new Vector3(0f, -newHeight * crouchHeightMultiplier, 0f);

                headPoint.position = new Vector3(transform.position.x + POV_crouchHeadHeight.x, transform.position.y + POV_crouchHeadHeight.y, transform.position.z + POV_crouchHeadHeight.z);
            }
            else
            {
                isCrouch = false;
                if (meshCharacterCrouch && meshCharacter) meshCharacter.SetActive(true);
                if (meshCharacterCrouch) meshCharacterCrouch.SetActive(false);

                collider.height = originalColliderHeight;
                collider.center = Vector3.zero;

                headPoint.position = new Vector3(transform.position.x + POV_normalHeadHeight.x, transform.position.y + POV_normalHeadHeight.y, transform.position.z + POV_normalHeadHeight.z);
            }
        }

        private void MoveWalk()
        {
            float crouchMultiplier = isCrouch ? crouchSpeedMultiplier : 1f;

            if (axisInput.magnitude > movementThrashold)
            {
                targetAngle = Mathf.Atan2(axisInput.x, axisInput.y) * Mathf.Rad2Deg + characterCamera.transform.eulerAngles.y;

                if (!sprint)
                    rigidbody.linearVelocity = Vector3.SmoothDamp(rigidbody.linearVelocity, forward * movementSpeed * crouchMultiplier, ref currVelocity, dampSpeedUp);
                else
                    rigidbody.linearVelocity = Vector3.SmoothDamp(rigidbody.linearVelocity, forward * sprintSpeed * crouchMultiplier, ref currVelocity, dampSpeedUp);
            }
            else
            {
                rigidbody.linearVelocity = Vector3.SmoothDamp(rigidbody.linearVelocity, Vector3.zero * crouchMultiplier, ref currVelocity, dampSpeedDown);
            }
        }

        private void MoveRotation()
        {
            float angle = Mathf.SmoothDampAngle(characterModel.transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, characterModelRotationSmooth);
            transform.rotation = Quaternion.Euler(0f, targetAngle, 0f);

            if (!lockRotation) characterModel.transform.rotation = Quaternion.Euler(0f, angle, 0f);
            else
            {
                var lookPos = -wallNormal; lookPos.y = 0;
                characterModel.transform.rotation = Quaternion.LookRotation(lookPos);
            }
        }

        public void ForceRotation()
        {
            characterModel.transform.rotation = Quaternion.Euler(0f, characterCamera.transform.rotation.eulerAngles.y, 0f);
        }

        private void MoveJump()
        {
            if (jump && isGrounded && ((isTouchingSlope && currentSurfaceAngle <= maxClimbableSlopeAngle) || !isTouchingSlope) && !isTouchingWall)
            {
                rigidbody.linearVelocity += Vector3.up * jumpVelocity;
                isJumping = true;
            }
            else if (jump && !isGrounded && isTouchingWall)
            {
                rigidbody.linearVelocity += wallNormal * jumpFromWallMultiplier + (Vector3.up * jumpFromWallMultiplier) * multiplierVerticalLeap;
                isJumping = true;

                targetAngle = Mathf.Atan2(wallNormal.x, wallNormal.z) * Mathf.Rad2Deg;
                forward = wallNormal; globalForward = forward; reactionForward = forward;
            }

            if (rigidbody.linearVelocity.y < 0 && !isGrounded) coyoteJumpMultiplier = fallMultiplier;
            else if (rigidbody.linearVelocity.y > 0.1f && (currentSurfaceAngle <= maxClimbableSlopeAngle || isTouchingStep))
            {
                if (!jumpHold || !canLongJump) coyoteJumpMultiplier = 1f;
                else coyoteJumpMultiplier = 1f / holdJumpMultiplier;
            }
            else
            {
                isJumping = false;
                coyoteJumpMultiplier = 1f;
            }
        }
        #endregion

        #region Gravity
        private void ApplyGravity()
        {
            Vector3 gravity = Vector3.zero;

            if (currentLockOnSlope || isTouchingStep) gravity = down * gravityMultiplier * -Physics.gravity.y * coyoteJumpMultiplier;
            else gravity = globalDown * gravityMultiplier * -Physics.gravity.y * coyoteJumpMultiplier;

            if (groundNormal.y != 1 && groundNormal.y != 0 && isTouchingSlope && prevGroundNormal != groundNormal)
                gravity *= gravityMultiplyerOnSlideChange;

            if (groundNormal.y != 1 && groundNormal.y != 0 && (currentSurfaceAngle > maxClimbableSlopeAngle && !isTouchingStep))
            {
                if (currentSurfaceAngle > 0f && currentSurfaceAngle <= 30f) gravity = globalDown * gravityMultiplierIfUnclimbableSlope * -Physics.gravity.y;
                else if (currentSurfaceAngle > 30f && currentSurfaceAngle <= 89f) gravity = globalDown * gravityMultiplierIfUnclimbableSlope / 2f * -Physics.gravity.y;
            }

            if (isTouchingWall && rigidbody.linearVelocity.y < 0) gravity *= frictionAgainstWall;

            rigidbody.AddForce(gravity);
        }
        #endregion

        #region Events
        private void UpdateEvents()
        {
            if ((jump && isGrounded && ((isTouchingSlope && currentSurfaceAngle <= maxClimbableSlopeAngle) || !isTouchingSlope)) || (jump && !isGrounded && isTouchingWall)) OnJump.Invoke();
            if (isGrounded && !prevGrounded && rigidbody.linearVelocity.y > -minimumVerticalSpeedToLandEvent) OnLand.Invoke();
            if (Mathf.Abs(rigidbody.linearVelocity.x) + Mathf.Abs(rigidbody.linearVelocity.z) > minimumHorizontalSpeedToFastEvent) OnFast.Invoke();
            if (isTouchingWall && rigidbody.linearVelocity.y < 0) OnWallSlide.Invoke();
            if (sprint) OnSprint.Invoke();
            if (isCrouch) OnCrouch.Invoke();
        }
        #endregion

        #region Friction and Round
        private void SetFriction(float _frictionWall, bool _isMinimum)
        {
            collider.material.dynamicFriction = 0.6f * _frictionWall;
            collider.material.staticFriction = 0.6f * _frictionWall;
            collider.material.frictionCombine = _isMinimum ? PhysicsMaterialCombine.Minimum : PhysicsMaterialCombine.Maximum;
        }

        private float RoundValue(float _value)
        {
            float unit = Mathf.Round(_value);
            if (_value - unit < 0.000001f && _value - unit > -0.000001f) return unit;
            else return _value;
        }
        #endregion

        #region GettersSetters
        public bool GetGrounded() => isGrounded;
        public bool GetTouchingSlope() => isTouchingSlope;
        public bool GetTouchingStep() => isTouchingStep;
        public bool GetTouchingWall() => isTouchingWall;
        public bool GetJumping() => isJumping;
        public bool GetCrouching() => isCrouch;
        public float GetOriginalColliderHeight() => originalColliderHeight;
        public void SetLockRotation(bool _lock) { lockRotation = _lock; }
        public void SetLockToCamera(bool _lockToCamera) { lockToCamera = _lockToCamera; if (!_lockToCamera) targetAngle = characterModel.transform.eulerAngles.y; }
        #endregion

        #region Gizmos
        private void OnDrawGizmos()
        {
            if (!debug) return;
            rigidbody = GetComponent<Rigidbody>();
            collider = GetComponent<CapsuleCollider>();

            Vector3 bottomStepPos = transform.position - new Vector3(0f, originalColliderHeight / 2f, 0f) + new Vector3(0f, 0.05f, 0f);
            Vector3 topWallPos = new Vector3(transform.position.x, transform.position.y + hightWallCheckerChecker, transform.position.z);

            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position - new Vector3(0, originalColliderHeight / 2f, 0), groundCheckerThrashold);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position - new Vector3(0, originalColliderHeight / 2f, 0), slopeCheckerThrashold);

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, transform.position + forward * 2f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + globalForward * 2);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + reactionForward * 2f);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + down * 2f);
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, transform.position + globalDown * 2f);
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, transform.position + reactionGlobalDown * 2f);

            Gizmos.color = Color.black;
            Gizmos.DrawLine(bottomStepPos, bottomStepPos + globalForward * stepCheckerThrashold);
            Gizmos.DrawLine(bottomStepPos + new Vector3(0f, maxStepHeight, 0f), bottomStepPos + new Vector3(0f, maxStepHeight, 0f) + globalForward * (stepCheckerThrashold + 0.05f));
            Gizmos.DrawLine(bottomStepPos, bottomStepPos + Quaternion.AngleAxis(45, transform.up) * (globalForward * stepCheckerThrashold));
            Gizmos.DrawLine(bottomStepPos + new Vector3(0f, maxStepHeight, 0f), bottomStepPos + Quaternion.AngleAxis(45, Vector3.up) * (globalForward * stepCheckerThrashold) + new Vector3(0f, maxStepHeight, 0f));
            Gizmos.DrawLine(bottomStepPos, bottomStepPos + Quaternion.AngleAxis(-45, transform.up) * (globalForward * stepCheckerThrashold));
            Gizmos.DrawLine(bottomStepPos + new Vector3(0f, maxStepHeight, 0f), bottomStepPos + Quaternion.AngleAxis(-45, Vector3.up) * (globalForward * stepCheckerThrashold) + new Vector3(0f, maxStepHeight, 0f));

            Gizmos.DrawLine(topWallPos, topWallPos + globalForward * wallCheckerThrashold);
            Gizmos.DrawLine(topWallPos, topWallPos + Quaternion.AngleAxis(45, transform.up) * (globalForward * wallCheckerThrashold));
            Gizmos.DrawLine(topWallPos, topWallPos + Quaternion.AngleAxis(90, transform.up) * (globalForward * wallCheckerThrashold));
            Gizmos.DrawLine(topWallPos, topWallPos + Quaternion.AngleAxis(135, transform.up) * (globalForward * wallCheckerThrashold));
            Gizmos.DrawLine(topWallPos, topWallPos + Quaternion.AngleAxis(180, transform.up) * (globalForward * wallCheckerThrashold));
            Gizmos.DrawLine(topWallPos, topWallPos + Quaternion.AngleAxis(225, transform.up) * (globalForward * wallCheckerThrashold));
            Gizmos.DrawLine(topWallPos, topWallPos + Quaternion.AngleAxis(270, transform.up) * (globalForward * wallCheckerThrashold));
            Gizmos.DrawLine(topWallPos, topWallPos + Quaternion.AngleAxis(315, transform.up) * (globalForward * wallCheckerThrashold));
        }
        #endregion
    }
}
