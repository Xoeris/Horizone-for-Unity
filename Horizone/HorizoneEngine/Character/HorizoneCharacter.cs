using UnityEngine;
using System.Collections.Generic;
using System.Collections;
// REQUIRED: Install Input System package from Package Manager
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

namespace Horizone
{
    [AddComponentMenu("Horizone/Horizone Character")]
    [DisallowMultipleComponent]
    public class HorizoneCharacter : MonoBehaviour
    {
        // --- Enums & Sub-Classes ---
        public enum PhysicsType { Rigidbody, CharacterController }
        public enum MovementState { Idle, Walk, Jog, Run, Flashrun, Crouch, InAir, Climbing, Swimming, Flying, Levitating, Dashing, Ragdoll, Teleporting }
        public enum CombatState { Idle, Attacking, Blocking, Evading, Stunned, Dead }

        [System.Serializable]
        public class InputSettings
        {
            [Header("Movement Actions")]
            public InputActionReference moveAction;
            public InputActionReference walkAction;     // Hold to walk
            public InputActionReference sprintAction;   // Hold to sprint
            public InputActionReference flashRunAction; // Hold with sprint
            public InputActionReference crouchAction;   // Toggle or Hold
            public InputActionReference jumpAction;     // Press
            public InputActionReference superJumpAction;// Hold with jump

            [Header("Abilities & Interaction")]
            public InputActionReference flyAction;      // Press
            public InputActionReference dashAction;     // Press (Evade)
            public InputActionReference attackAction;   // Press
            public InputActionReference interactAction; // Press
            public InputActionReference teleportAction; // Press
            
            public void SetInputs(bool enabled)
            {
                EnableAction(moveAction, enabled);
                EnableAction(walkAction, enabled);
                EnableAction(sprintAction, enabled);
                EnableAction(flashRunAction, enabled);
                EnableAction(crouchAction, enabled);
                EnableAction(jumpAction, enabled);
                EnableAction(superJumpAction, enabled);
                EnableAction(flyAction, enabled);
                EnableAction(dashAction, enabled);
                EnableAction(attackAction, enabled);
                EnableAction(interactAction, enabled);
                EnableAction(teleportAction, enabled);
            }

            private void EnableAction(InputActionReference refAction, bool enabled)
            {
                if (refAction != null && refAction.action != null)
                {
                    if (enabled) refAction.action.Enable();
                    else refAction.action.Disable();
                }
            }
        }

        [System.Serializable]
        public class AnimatorSettings
        {
            [Header("Parameters")]
            public string speedParam = "Speed";
            public string horizontalParam = "Horizontal";
            public string verticalParam = "Vertical";
            public string comboIndexParam = "ComboIndex";

            [Header("Booleans")]
            public string isIdleParam = "IsIdle";
            public string isGroundedParam = "IsGrounded";
            public string isWalkingParam = "IsWalking";
            public string isJoggingParam = "IsJoggingActive";
            public string isRunningParam = "IsRunningActive";
            public string isFlashRunParam = "IsHyperunningActive";
            public string isCrouchingParam = "IsCrouching";
            public string isJumpingParam = "IsJumping";
            public string isFlyingParam = "IsFlying";
            public string isLevitatingParam = "IsLevitating";
            public string isDashingParam = "IsDashing";
            public string isEvadingParam = "IsEvading";
            public string isTeleportingParam = "IsTeleporting";
            public string isFallingParam = "IsFalling";
            public string isFallingToRollParam = "IsFallingtoRolling";
            public string isAttackingParam = "IsAttacking";
            public string isAttackingOneParam = "IsAttackingOne";
            public string isDeathParam = "IsDeath";

            [Header("Jump States")]
            public string isWalkingJumpParam = "IsWalkingJump";
            public string isJoggingJumpParam = "IsJoggingJump";
            public string isRunningJumpParam = "IsRunningJump";
            public string isFlashRunJumpParam = "IsHyperunningJump";

            [Header("Triggers")]
            public string jumpTrigger = "Jump";
            public string attackTrigger = "Attack";
            public string evadeTrigger = "Evade";
            public string hitTrigger = "Hit";
        }

        [System.Serializable]
        public class CharacterStats
        {
            public float maxHealth = 100f;
            public float currentHealth = 100f;
            public float healthRegenRate = 1f;
            [Space]
            public int currentLevel = 1;
            public float currentXP = 0f;
            public float xpToNextLevel = 1000f;
            public float stamina = 100f;
            public float maxStamina = 100f;
        }

        [System.Serializable]
        public class AttackData
        {
            public string attackName = "Light Attack";
            public AnimationClip animation;
            public float damage = 10f;
            public float staminaCost = 15f;
            public float attackRange = 1.5f;
            public float impactForce = 5f;
            public ParticleSystem hitEffect;
        }

        [System.Serializable]
        public class ComboData
        {
            public string comboName = "Basic Combo";
            public List<AttackData> attacks = new List<AttackData>();
            public float comboResetTime = 1.0f;
        }

        [System.Serializable]
        public class InteractionData
        {
            public string interactionName = "Interact";
            public float distance = 2.0f;
            public LayerMask interactLayer;
        }

        // --- PUBLIC SETTINGS ---
        [Header("Engine Settings")]
        public PhysicsType physicsEngine = PhysicsType.Rigidbody;

        // 1. Inputs
        public InputSettings inputs = new InputSettings();

        // 2. Animator Parameters
        public AnimatorSettings animParams = new AnimatorSettings();

        // 3. Movement & Physics
        [HideInInspector] public float walkSpeed = 2f;
        [HideInInspector] public float jogSpeed = 4f;
        [HideInInspector] public float runSpeed = 7f;
        [HideInInspector] public float flashRunSpeed = 15f;
        [HideInInspector] public float crouchSpeed = 1.5f;
        [HideInInspector] public float jumpForce = 6f;
        [HideInInspector] public float superJumpForce = 12f;
        [HideInInspector] public float dashForce = 10f;
        [HideInInspector] public float airControl = 0.5f;
        [HideInInspector] public float rotationSpeed = 10f;
        [HideInInspector] public float gravityMultiplier = 2f; 

        // 4. Flight & Levitation
        [HideInInspector] public float flySpeed = 10f;
        [HideInInspector] public float levitateHeight = 2f;
        [HideInInspector] public bool canFly = false;

        // 5. Parkour & Climbing
        [HideInInspector] public bool useParkour = true;
        [HideInInspector] public float climbSpeed = 3f;
        [HideInInspector] public LayerMask parkourLayers;
        [HideInInspector] public float wallCheckDistance = 0.7f;
        [HideInInspector] public float ledgeGrabHeight = 1.8f;
        [HideInInspector] public bool enableIK = true;

        // 6. Combat & Stats
        [HideInInspector] public CharacterStats stats = new CharacterStats();
        [HideInInspector] public List<AttackData> attacks = new List<AttackData>();
        [HideInInspector] public List<ComboData> combos = new List<ComboData>();
        [HideInInspector] public float evadeDuration = 0.5f;
        [HideInInspector] public float evadeCooldown = 1.0f;

        // 7. Visual FX
        [Header("Visual FX")]
        public GameObject hideTarget;
        public bool invertHideLogic = false;
        private Renderer[] cachedRenderers;

        // 8. Interactions
        [HideInInspector] public List<InteractionData> interactions = new List<InteractionData>();
        [HideInInspector] public LayerMask waterLayer;

        // --- RUNTIME VARIABLES ---
        private Rigidbody rb;
        private Animator anim;
        private CapsuleCollider col;
        private CharacterController cc;
        private Transform mainCam;

        // State Tracking
        public MovementState currentMoveState = MovementState.Idle;
        public CombatState currentCombatState = CombatState.Idle;
        private Vector3 moveInput;
        private bool isGrounded;
        private float lastAttackTime;
        private int currentComboIndex;
        private float verticalVelocity; 
        private float originalColHeight;
        private Vector3 originalColCenter;

        // Flight Tracking
        private bool isFlyingActive = false; 

        // Jump State Tracking
        private bool isJumping = false;
        private MovementState stateBeforeJump = MovementState.Idle;
        private float lastJumpTime = 0f;

        // Falling
        private bool isFalling = false;
        private float airTime = 0f;

        // Respawn
        private Vector3 spawnPoint;

        // Public Property for external access
        public bool isAttackingOne => currentCombatState == CombatState.Attacking && currentComboIndex == 1;

        private void Reset()
        {
            ValidateComponents();
        }

        private void OnValidate()
        {
        }

        private void OnEnable()
        {
            inputs.SetInputs(true);
        }

        private void OnDisable()
        {
            inputs.SetInputs(false);
        }

        public void ValidateComponents()
        {
            anim = GetComponent<Animator>();
            if (anim == null) anim = gameObject.AddComponent<Animator>();
            anim.hideFlags = HideFlags.HideInInspector;

            if (physicsEngine == PhysicsType.Rigidbody)
            {
                rb = GetComponent<Rigidbody>();
                if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
                rb.hideFlags = HideFlags.HideInInspector;
                rb.constraints = RigidbodyConstraints.FreezeRotation;

                col = GetComponent<CapsuleCollider>();
                if (col == null) col = gameObject.AddComponent<CapsuleCollider>();
                col.hideFlags = HideFlags.HideInInspector;

                cc = GetComponent<CharacterController>();
                if (cc != null) StartCoroutine(DestroyComponentNextFrame(cc));
            }
            else
            {
                cc = GetComponent<CharacterController>();
                if (cc == null) cc = gameObject.AddComponent<CharacterController>();
                cc.hideFlags = HideFlags.HideInInspector;

                rb = GetComponent<Rigidbody>();
                if (rb != null) StartCoroutine(DestroyComponentNextFrame(rb));
                col = GetComponent<CapsuleCollider>();
                if (col != null) StartCoroutine(DestroyComponentNextFrame(col));
            }
        }

        IEnumerator DestroyComponentNextFrame(Component c)
        {
            yield return null;
            if (c != null) DestroyImmediate(c);
        }

        private void Awake()
        {
            ValidateComponents();
            mainCam = Camera.main ? Camera.main.transform : transform;
            spawnPoint = transform.position;

            if (col != null)
            {
                originalColHeight = col.height;
                originalColCenter = col.center;
            }
            else if (cc != null)
            {
                originalColHeight = cc.height;
                originalColCenter = cc.center;
            }

            if (attacks.Count == 0) attacks.Add(new AttackData { attackName = "Punch" });
            if (combos.Count == 0) combos.Add(new ComboData { comboName = "Punch Combo" });

            // Initialize Renderers for Hide Logic
            if (hideTarget != null)
            {
                cachedRenderers = hideTarget.GetComponentsInChildren<Renderer>(true);
            }
        }

        private void Update()
        {
            if (stats.currentHealth <= 0) return;

            HandleInput();
            HandleStates();
            HandleInteractions();
            RegenerateStats();
            HandleAttackVisibility();
            
            if (physicsEngine == PhysicsType.CharacterController)
            {
                CheckGround();
                HandleMovementPhysics();
                if (useParkour) HandleParkourPhysics();
            }

            UpdateAnimator();
        }

        private void FixedUpdate()
        {
            if (stats.currentHealth <= 0) return;
            if (currentMoveState == MovementState.Ragdoll) return;

            if (physicsEngine == PhysicsType.Rigidbody)
            {
                CheckGround();
                HandleMovementPhysics();
                if (useParkour) HandleParkourPhysics();
            }
        }

        // --- CORE LOGIC ---

        void HandleAttackVisibility()
        {
            if (hideTarget == null) return;

            bool active = isAttackingOne;
            bool show = invertHideLogic ? !active : active;

            if (hideTarget == gameObject)
            {
                if (cachedRenderers != null)
                {
                    for (int i = 0; i < cachedRenderers.Length; i++) 
                        cachedRenderers[i].enabled = show;
                }
            }
            else
            {
                if (hideTarget.activeSelf != show) hideTarget.SetActive(show);
            }
        }

        void HandleInput()
        {
            Vector2 inputVector = Vector2.zero;
            if (inputs.moveAction != null && inputs.moveAction.action != null)
            {
                inputVector = inputs.moveAction.action.ReadValue<Vector2>();
            }

            moveInput = new Vector3(inputVector.x, 0, inputVector.y);

            bool isCrouch = IsPressed(inputs.crouchAction);
            bool isSprint = IsPressed(inputs.sprintAction);
            bool isFlash = IsPressed(inputs.flashRunAction);
            bool isWalk = IsPressed(inputs.walkAction);

            if (WasPressed(inputs.flyAction) && canFly) ToggleFlight();

            if (isFlyingActive)
            {
                bool isMoving = moveInput.magnitude > 0.1f;
                bool isVertical = IsPressed(inputs.jumpAction) || IsPressed(inputs.crouchAction);

                if (isMoving || isVertical) currentMoveState = MovementState.Flying;
                else currentMoveState = MovementState.Levitating;
            }
            else
            {
                if (isCrouch) currentMoveState = MovementState.Crouch;
                else if (isSprint)
                {
                    if (isFlash) currentMoveState = MovementState.Flashrun;
                    else currentMoveState = MovementState.Run;
                }
                else if (isWalk) currentMoveState = MovementState.Walk;
                else if (moveInput.magnitude > 0.1f) currentMoveState = MovementState.Jog; 
                else currentMoveState = MovementState.Idle;
            }

            if (currentCombatState == CombatState.Evading) currentMoveState = MovementState.Dashing;
            
            if (WasPressed(inputs.attackAction)) PerformAttack();
            if (WasPressed(inputs.dashAction)) PerformEvade();

            if (WasPressed(inputs.jumpAction)) HandleJumpInput();
            if (WasPressed(inputs.teleportAction)) 
            {
                Teleport(transform.position + transform.forward * 5f);
                StartCoroutine(TeleportStateRoutine());
            }
        }

        bool IsPressed(InputActionReference actionRef)
        {
            return actionRef != null && actionRef.action != null && actionRef.action.IsPressed();
        }

        bool WasPressed(InputActionReference actionRef)
        {
            return actionRef != null && actionRef.action != null && actionRef.action.WasPressedThisFrame();
        }

        void HandleMovementPhysics()
        {
            if (currentCombatState == CombatState.Attacking || currentCombatState == CombatState.Evading) return;

            float targetSpeed = walkSpeed;
            switch(currentMoveState)
            {
                case MovementState.Walk: targetSpeed = walkSpeed; break;
                case MovementState.Jog: targetSpeed = jogSpeed; break;
                case MovementState.Run: targetSpeed = runSpeed; break;
                case MovementState.Flashrun: targetSpeed = flashRunSpeed; break;
                case MovementState.Crouch: targetSpeed = crouchSpeed; break;
                case MovementState.Flying: targetSpeed = flySpeed; break;
                case MovementState.Levitating: targetSpeed = flySpeed; break;
                case MovementState.Climbing: targetSpeed = climbSpeed; break;
            }

            Vector3 moveDir;

            if (currentMoveState == MovementState.Flying || currentMoveState == MovementState.Levitating)
            {
                 // Flight: Move relative to camera rotation including vertical
                 moveDir = (moveInput.x * mainCam.right + moveInput.z * mainCam.forward).normalized;
            }
            else
            {
                // Ground: Flatten camera vector
                Vector3 camForward = Vector3.Scale(mainCam.forward, new Vector3(1, 0, 1)).normalized;
                moveDir = (moveInput.x * mainCam.right + moveInput.z * camForward).normalized;
            }

            if (moveDir.magnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * (physicsEngine == PhysicsType.Rigidbody ? Time.fixedDeltaTime : Time.deltaTime));
            }

            bool jumpHeld = IsPressed(inputs.jumpAction);
            bool crouchHeld = IsPressed(inputs.crouchAction);

            if (physicsEngine == PhysicsType.Rigidbody)
            {
                if (currentMoveState == MovementState.Flying || currentMoveState == MovementState.Levitating)
                {
                    rb.useGravity = false;
                    Vector3 flyMove = moveDir * targetSpeed;
                    if (jumpHeld) flyMove.y = climbSpeed;
                    if (crouchHeld) flyMove.y = -climbSpeed;
                    rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, flyMove, Time.fixedDeltaTime * 5f);
                }
                else if (currentMoveState == MovementState.Climbing)
                {
                    rb.useGravity = false;
                    float vInput = (inputs.moveAction != null) ? inputs.moveAction.action.ReadValue<Vector2>().y : 0;
                    rb.linearVelocity = new Vector3(0, vInput * climbSpeed, 0);
                }
                else
                {
                    rb.useGravity = true;
                    if (isGrounded)
                    {
                        Vector3 velocity = moveDir * targetSpeed;
                        velocity.y = rb.linearVelocity.y;
                        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, velocity, Time.fixedDeltaTime * 10f);
                    }
                    else
                    {
                        Vector3 airTarget = moveDir * targetSpeed;
                        airTarget.y = rb.linearVelocity.y;
                        float responsiveness = airControl * 2f; 
                        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, airTarget, Time.fixedDeltaTime * responsiveness);
                    }
                }
            }
            else 
            {
                Vector3 finalMove = Vector3.zero;

                if (currentMoveState == MovementState.Flying || currentMoveState == MovementState.Levitating)
                {
                    verticalVelocity = 0;
                    finalMove = moveDir * targetSpeed;
                    if (jumpHeld) finalMove.y = climbSpeed;
                    if (crouchHeld) finalMove.y = -climbSpeed;
                }
                else if (currentMoveState == MovementState.Climbing)
                {
                    verticalVelocity = 0;
                    float vInput = (inputs.moveAction != null) ? inputs.moveAction.action.ReadValue<Vector2>().y : 0;
                    finalMove = new Vector3(0, vInput * climbSpeed, 0);
                }
                else
                {
                    if (isGrounded && verticalVelocity < 0) verticalVelocity = -2f;
                    verticalVelocity += Physics.gravity.y * gravityMultiplier * Time.deltaTime;
                    finalMove = moveDir * targetSpeed;
                    finalMove.y = verticalVelocity;
                }
                cc.Move(finalMove * Time.deltaTime);
            }

            if (Physics.CheckSphere(transform.position + Vector3.up * 0.5f, 0.2f, waterLayer))
            {
                currentMoveState = MovementState.Swimming;
                if(rb) rb.linearDamping = 3f;
            }
            else if(currentMoveState != MovementState.Flying && currentMoveState != MovementState.Levitating && currentMoveState != MovementState.Climbing)
            {
                if(rb) rb.linearDamping = 0f;
            }
        }

        void HandleJumpInput()
        {
            if (currentMoveState == MovementState.Climbing) { ExitClimb(); return; }
            if (!isGrounded && currentMoveState != MovementState.Flying && currentMoveState != MovementState.Levitating && currentMoveState != MovementState.Swimming) return;
            if (isFlyingActive) return;

            stateBeforeJump = currentMoveState;
            isJumping = true;
            isGrounded = false; 
            lastJumpTime = Time.time; 

            float force = jumpForce;
            if (IsPressed(inputs.superJumpAction)) force = superJumpForce;

            if (physicsEngine == PhysicsType.Rigidbody)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                rb.AddForce(Vector3.up * force, ForceMode.Impulse);
            }
            else
            {
                verticalVelocity = Mathf.Sqrt(force * -2f * Physics.gravity.y * gravityMultiplier);
            }
            anim.SetTrigger(animParams.jumpTrigger);
        }

        void HandleParkourPhysics()
        {
            if (!isGrounded && currentMoveState != MovementState.Climbing && currentMoveState != MovementState.Flying && currentMoveState != MovementState.Levitating)
            {
                RaycastHit hit;
                if (Physics.Raycast(transform.position + Vector3.up, transform.forward, out hit, wallCheckDistance, parkourLayers))
                {
                    currentMoveState = MovementState.Climbing;
                    if (physicsEngine == PhysicsType.Rigidbody) rb.linearVelocity = Vector3.zero;
                    else verticalVelocity = 0;
                    transform.forward = -hit.normal;
                }
            }
        }

        void ExitClimb()
        {
            currentMoveState = MovementState.InAir;
            Vector3 pushDir = (Vector3.up + -transform.forward).normalized;
            if (physicsEngine == PhysicsType.Rigidbody) rb.AddForce(pushDir * 3f, ForceMode.Impulse);
            else verticalVelocity = 5f;
        }

        void PerformAttack()
        {
            if (Time.time - lastAttackTime < 0.2f) return;
            if (stats.stamina < 10f) return;

            currentCombatState = CombatState.Attacking;
            lastAttackTime = Time.time;
            
            if (currentComboIndex >= 3) currentComboIndex = 0;
            
            anim.SetInteger(animParams.comboIndexParam, currentComboIndex);
            anim.SetTrigger(animParams.attackTrigger);
            
            stats.stamina -= 10f;
            currentComboIndex++;
            
            StartCoroutine(ResetComboTimer());
        }

        void PerformEvade()
        {
            if (currentCombatState == CombatState.Evading) return;
            StartCoroutine(EvadeRoutine());
        }

        IEnumerator EvadeRoutine()
        {
            currentCombatState = CombatState.Evading;
            anim.SetTrigger(animParams.evadeTrigger);
            
            if (physicsEngine == PhysicsType.Rigidbody)
                rb.AddForce(moveInput.normalized * dashForce, ForceMode.VelocityChange);
            else
                cc.Move(moveInput.normalized * dashForce * Time.deltaTime * 5f);
            
            yield return new WaitForSeconds(evadeDuration);
            currentCombatState = CombatState.Idle;
        }

        IEnumerator ResetComboTimer()
        {
            yield return new WaitForSeconds(1.5f);
            currentComboIndex = 0;
            currentCombatState = CombatState.Idle;
        }

        IEnumerator TeleportStateRoutine()
        {
            currentMoveState = MovementState.Teleporting;
            yield return new WaitForSeconds(0.2f);
            if(!isGrounded) currentMoveState = MovementState.InAir;
            else currentMoveState = MovementState.Idle;
        }

        public void TakeDamage(float amount)
        {
            if (currentCombatState == CombatState.Evading) return;
            stats.currentHealth -= amount;
            anim.SetTrigger(animParams.hitTrigger);
            if (stats.currentHealth <= 0) Die();
        }

        void Die()
        {
            currentCombatState = CombatState.Dead;
            EnableRagdoll(true);
        }

        public void Revive()
        {
            stats.currentHealth = stats.maxHealth;
            transform.position = spawnPoint;
            EnableRagdoll(false);
            currentCombatState = CombatState.Idle;
            anim.Play("Idle");
        }

        void EnableRagdoll(bool enable)
        {
            anim.enabled = !enable;
            
            if (physicsEngine == PhysicsType.Rigidbody)
            {
                rb.isKinematic = enable;
                if(col) col.enabled = !enable;
            }
            else
            {
                cc.enabled = !enable;
            }

            Rigidbody[] limbs = GetComponentsInChildren<Rigidbody>();
            foreach(var limb in limbs)
            {
                if(limb != rb)
                {
                    limb.isKinematic = !enable;
                    limb.detectCollisions = enable;
                }
            }
            currentMoveState = enable ? MovementState.Ragdoll : MovementState.Idle;
        }

        void Teleport(Vector3 pos)
        {
            if (physicsEngine == PhysicsType.CharacterController)
            {
                cc.enabled = false;
                transform.position = pos;
                cc.enabled = true;
            }
            else
            {
                transform.position = pos;
            }
        }

        void ToggleFlight()
        {
            isFlyingActive = !isFlyingActive;

            if (!isFlyingActive)
            {
                currentMoveState = MovementState.InAir;
                if(physicsEngine == PhysicsType.Rigidbody) rb.useGravity = true;
                anim.SetBool(animParams.isFlyingParam, false);
                anim.SetBool(animParams.isLevitatingParam, false);
            }
            else
            {
                currentMoveState = MovementState.Levitating;
                if(physicsEngine == PhysicsType.Rigidbody)
                {
                    rb.useGravity = false;
                    rb.linearVelocity = Vector3.zero;
                }
                anim.SetBool(animParams.isFlyingParam, true);
                anim.SetBool(animParams.isLevitatingParam, true);
                transform.position += Vector3.up * 0.5f;
            }
        }

        void HandleInteractions()
        {
            if (WasPressed(inputs.interactAction))
            {
                foreach(var interact in interactions)
                {
                    if (Physics.CheckSphere(transform.position, interact.distance, interact.interactLayer))
                    {
                        Debug.Log("Interacting with: " + interact.interactionName);
                    }
                }
            }
        }

        void CheckGround()
        {
            if (Time.time < lastJumpTime + 0.55f)
            {
                isGrounded = false;
                return;
            }

            bool wasGrounded = isGrounded;
            
            if (physicsEngine == PhysicsType.CharacterController)
            {
                isGrounded = cc.isGrounded;
            }
            else
            {
                // --- FIX: Modified Ground Check ---
                // Offset the check slightly downwards so we don't detect ground when hovering just above it
                // Center: 0.05 units below pivot. Radius: 0.15 units.
                // Effectively checks for ground from -0.2 to +0.1 relative to feet.
                Vector3 spherePos = transform.position - Vector3.up * 0.05f;
                float radius = 0.15f;
                isGrounded = Physics.CheckSphere(spherePos, radius, LayerMask.GetMask("Default", "Ground"));
            }

            if(isGrounded) 
            {
                isJumping = false;
                isFalling = false;
                airTime = 0f;
            }
            else
            {
                if (currentMoveState != MovementState.Flying && currentMoveState != MovementState.Levitating)
                {
                    float yVel = physicsEngine == PhysicsType.Rigidbody ? rb.linearVelocity.y : verticalVelocity;
                    if(yVel < -0.1f) 
                    {
                        isFalling = true;
                        airTime += Time.deltaTime;
                    }
                }
                else
                {
                    isFalling = false;
                    airTime = 0f;
                }
            }
            
            if(!wasGrounded && isGrounded && airTime > 1.5f)
            {
                anim.SetBool(animParams.isFallingToRollParam, true);
                StartCoroutine(ResetRollingParam());
            }
        }

        IEnumerator ResetRollingParam()
        {
            yield return new WaitForSeconds(0.1f);
            anim.SetBool(animParams.isFallingToRollParam, false);
        }

        void RegenerateStats()
        {
            if (currentCombatState != CombatState.Dead)
            {
                stats.stamina = Mathf.MoveTowards(stats.stamina, stats.maxStamina, Time.deltaTime * 5f);
                if (stats.currentHealth < stats.maxHealth)
                    stats.currentHealth += stats.healthRegenRate * Time.deltaTime;
            }
        }

        void HandleStates()
        {
            float targetH = originalColHeight;
            Vector3 targetC = originalColCenter;

            if (currentMoveState == MovementState.Crouch)
            {
                targetH = originalColHeight * 0.6f;
                targetC = originalColCenter * 0.6f;
            }

            if (physicsEngine == PhysicsType.Rigidbody && col != null)
            {
                col.height = Mathf.Lerp(col.height, targetH, Time.deltaTime * 5f);
                col.center = Vector3.Lerp(col.center, targetC, Time.deltaTime * 5f);
            }
            else if (physicsEngine == PhysicsType.CharacterController && cc != null)
            {
                cc.height = Mathf.Lerp(cc.height, targetH, Time.deltaTime * 5f);
                cc.center = Vector3.Lerp(cc.center, targetC, Time.deltaTime * 5f);
            }
        }

        void UpdateAnimator()
        {
            if (!anim) return;

            Vector3 localVel = Vector3.zero;
            if(physicsEngine == PhysicsType.Rigidbody) 
                localVel = transform.InverseTransformDirection(rb.linearVelocity);
            else if (physicsEngine == PhysicsType.CharacterController)
                localVel = transform.InverseTransformDirection(cc.velocity);
            
            anim.SetFloat(animParams.speedParam, localVel.magnitude);
            anim.SetFloat(animParams.horizontalParam, localVel.x);
            anim.SetFloat(animParams.verticalParam, localVel.z);

            anim.SetBool(animParams.isIdleParam, currentMoveState == MovementState.Idle);
            anim.SetBool(animParams.isGroundedParam, isGrounded);
            
            bool moving = localVel.magnitude > 0.1f;
            anim.SetBool(animParams.isWalkingParam, currentMoveState == MovementState.Walk && moving);
            anim.SetBool(animParams.isJoggingParam, currentMoveState == MovementState.Jog && moving);
            anim.SetBool(animParams.isRunningParam, currentMoveState == MovementState.Run && moving);
            anim.SetBool(animParams.isFlashRunParam, currentMoveState == MovementState.Flashrun && moving);
            anim.SetBool(animParams.isCrouchingParam, currentMoveState == MovementState.Crouch);

            anim.SetBool(animParams.isJumpingParam, isJumping);

            anim.SetBool(animParams.isFlyingParam, currentMoveState == MovementState.Flying);
            anim.SetBool(animParams.isLevitatingParam, currentMoveState == MovementState.Levitating);
            
            anim.SetBool(animParams.isDashingParam, currentMoveState == MovementState.Dashing);
            anim.SetBool(animParams.isEvadingParam, currentCombatState == CombatState.Evading);
            anim.SetBool(animParams.isTeleportingParam, currentMoveState == MovementState.Teleporting);

            bool inJumpState = isJumping || (!isGrounded && !isFalling && currentMoveState != MovementState.Flying && currentMoveState != MovementState.Levitating);
            anim.SetBool(animParams.isWalkingJumpParam, inJumpState && stateBeforeJump == MovementState.Walk);
            anim.SetBool(animParams.isJoggingJumpParam, inJumpState && (stateBeforeJump == MovementState.Jog || stateBeforeJump == MovementState.Idle)); 
            anim.SetBool(animParams.isRunningJumpParam, inJumpState && stateBeforeJump == MovementState.Run);
            anim.SetBool(animParams.isFlashRunJumpParam, inJumpState && stateBeforeJump == MovementState.Flashrun);

            anim.SetBool(animParams.isFallingParam, isFalling);
            
            anim.SetBool(animParams.isAttackingParam, currentCombatState == CombatState.Attacking);
            anim.SetBool(animParams.isAttackingOneParam, isAttackingOne); 

            anim.SetBool(animParams.isDeathParam, currentCombatState == CombatState.Dead);
        }

        public void AddXP(float amount)
        {
            stats.currentXP += amount;
            if(stats.currentXP >= stats.xpToNextLevel)
            {
                stats.currentLevel++;
                stats.currentXP -= stats.xpToNextLevel;
                stats.xpToNextLevel *= 1.2f;
            }
        }
    }

    // ---------------------------------------------------------
    // CUSTOM INSPECTOR (EDITOR CODE)
    // ---------------------------------------------------------
#if UNITY_EDITOR
    [CustomEditor(typeof(HorizoneCharacter))]
    public class HorizoneCharacterEditor : Editor
    {
        HorizoneCharacter script;
        const string ICON_PATH = "Assets/Horizone/HorizoneEngine/Character/HorizoneCharacter.png";

        bool showInputs = false;
        bool showAnimParams = false; 
        bool showMove = true;
        bool showFly = false;
        bool showParkour = false;
        bool showCombat = false;
        bool showStats = false;
        bool showInteract = false;
        bool showInternal = true;

        SerializedProperty pEngine;
        SerializedProperty inputSettings;
        SerializedProperty animParamSettings;
        SerializedProperty speedWalk, speedJog, speedRun, speedFlash, speedCrouch;
        SerializedProperty jump, sJump, dash, airCtrl, rotSpd, gMult;
        SerializedProperty flySpd, levHeight, cFly;
        SerializedProperty uParkour, cSpeed, pLayer, wCheck, lGrab, eIK;
        SerializedProperty charStats, atkList, cmbList, evDur, evCool;
        SerializedProperty hideTarget, invertHide; 
        SerializedProperty intList, wLayer;

        // Internal Components SO
        SerializedObject rbSO, colSO, ccSO, animSO;

        // NEW: Add static constructor to register hierarchy callback
        [InitializeOnLoadMethod]
        static void InitHierarchyIcon()
        {
            EditorApplication.hierarchyWindowItemOnGUI -= DrawHierarchyIcon;
            EditorApplication.hierarchyWindowItemOnGUI += DrawHierarchyIcon;
        }

        static void DrawHierarchyIcon(int instanceID, Rect selectionRect)
        {
            GameObject gameObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (gameObject == null) return;

            if (gameObject.GetComponent<HorizoneCharacter>() != null)
            {
                // Draw icon on the right
                Rect iconRect = new Rect(selectionRect.xMax - 16, selectionRect.y, 16, 16);
                
                Texture2D customIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(ICON_PATH);
                // Fallback to d_Avatar Icon if custom one is missing
                Texture icon = (customIcon != null) ? customIcon : EditorGUIUtility.IconContent("d_Avatar Icon").image;

                if (icon != null)
                {
                    GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                }
            }
        }

        [MenuItem("GameObject/Horizone/Horizone Character", false, 10)]
        static void CreateHorizoneCharacter(MenuCommand menuCommand)
        {
            GameObject go = new GameObject("Horizone Character");
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            go.AddComponent<HorizoneCharacter>();
            Selection.activeObject = go;

            Texture2D customIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(ICON_PATH);
            if (customIcon != null) EditorGUIUtility.SetIconForObject(go, customIcon);
        }

        private void OnEnable()
        {
            script = (HorizoneCharacter)target;
            Texture2D customIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(ICON_PATH);
            if (customIcon != null) EditorGUIUtility.SetIconForObject(script.gameObject, customIcon);

            // Ensure components match selected physics engine (Editor safety check)
            script.ValidateComponents();

            pEngine = serializedObject.FindProperty("physicsEngine");
            inputSettings = serializedObject.FindProperty("inputs");
            animParamSettings = serializedObject.FindProperty("animParams");

            speedWalk = serializedObject.FindProperty("walkSpeed");
            speedJog = serializedObject.FindProperty("jogSpeed");
            speedRun = serializedObject.FindProperty("runSpeed");
            speedFlash = serializedObject.FindProperty("flashRunSpeed");
            speedCrouch = serializedObject.FindProperty("crouchSpeed");
            jump = serializedObject.FindProperty("jumpForce");
            sJump = serializedObject.FindProperty("superJumpForce");
            dash = serializedObject.FindProperty("dashForce");
            airCtrl = serializedObject.FindProperty("airControl");
            rotSpd = serializedObject.FindProperty("rotationSpeed");
            gMult = serializedObject.FindProperty("gravityMultiplier");

            flySpd = serializedObject.FindProperty("flySpeed");
            levHeight = serializedObject.FindProperty("levitateHeight");
            cFly = serializedObject.FindProperty("canFly");

            uParkour = serializedObject.FindProperty("useParkour");
            cSpeed = serializedObject.FindProperty("climbSpeed");
            pLayer = serializedObject.FindProperty("parkourLayers");
            wCheck = serializedObject.FindProperty("wallCheckDistance");
            lGrab = serializedObject.FindProperty("ledgeGrabHeight");
            eIK = serializedObject.FindProperty("enableIK");

            charStats = serializedObject.FindProperty("stats");
            atkList = serializedObject.FindProperty("attacks");
            cmbList = serializedObject.FindProperty("combos");
            evDur = serializedObject.FindProperty("evadeDuration");
            evCool = serializedObject.FindProperty("evadeCooldown");
            hideTarget = serializedObject.FindProperty("hideTarget");
            invertHide = serializedObject.FindProperty("invertHideLogic");

            intList = serializedObject.FindProperty("interactions");
            wLayer = serializedObject.FindProperty("waterLayer");

            // Init Internal SOs
            RefreshInternalSOs();
        }

        void RefreshInternalSOs()
        {
            if (script.GetComponent<Rigidbody>() != null) rbSO = new SerializedObject(script.GetComponent<Rigidbody>());
            else rbSO = null;

            if (script.GetComponent<CapsuleCollider>() != null) colSO = new SerializedObject(script.GetComponent<CapsuleCollider>());
            else colSO = null;

            if (script.GetComponent<CharacterController>() != null) ccSO = new SerializedObject(script.GetComponent<CharacterController>());
            else ccSO = null;

            if (script.GetComponent<Animator>() != null) animSO = new SerializedObject(script.GetComponent<Animator>());
            else animSO = null;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawHeader();
            EditorGUILayout.Space(10);
            DrawDebugInfo();
            EditorGUILayout.Space(5);

            // Engine Selection
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(pEngine, new GUIContent("Physics Engine", "Select the physics engine to use."));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                script.ValidateComponents(); // Apply switch immediately
                RefreshInternalSOs();
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.Space(5);

            // --- INPUTS ---
            showInputs = EditorGUILayout.Foldout(showInputs, "Input Settings", true, EditorStyles.foldoutHeader);
            if (showInputs)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(inputSettings, new GUIContent("Input Configuration", "Configure input actions for movement and abilities."), true);
                EditorGUILayout.EndVertical();
            }

            // --- ANIMATOR PARAMETERS ---
            showAnimParams = EditorGUILayout.Foldout(showAnimParams, "Animator Parameters", true, EditorStyles.foldoutHeader);
            if (showAnimParams)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(animParamSettings, new GUIContent("Animator Settings", "Configure parameter names for the Animator Controller."), true);
                EditorGUILayout.EndVertical();
            }

            // --- MOVEMENT ---
            showMove = EditorGUILayout.Foldout(showMove, "Movement & Physics", true, EditorStyles.foldoutHeader);
            if (showMove)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(speedWalk, new GUIContent("Walk Speed", "Speed when walking."));
                EditorGUILayout.PropertyField(speedJog, new GUIContent("Jog Speed", "Speed when jogging."));
                EditorGUILayout.PropertyField(speedRun, new GUIContent("Run Speed", "Speed when running."));
                EditorGUILayout.PropertyField(speedFlash, new GUIContent("FlashRun Speed", "Speed when using Flash Run ability."));
                EditorGUILayout.PropertyField(speedCrouch, new GUIContent("Crouch Speed", "Speed when crouching."));
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(jump, new GUIContent("Jump Force", "Force applied when jumping."));
                EditorGUILayout.PropertyField(sJump, new GUIContent("Super Jump Force", "Force applied when super jumping."));
                EditorGUILayout.PropertyField(dash, new GUIContent("Dash Force", "Force applied when dashing/evading."));
                EditorGUILayout.PropertyField(airCtrl, new GUIContent("Air Control", "Responsiveness of movement while in the air."));
                EditorGUILayout.PropertyField(rotSpd, new GUIContent("Rotation Speed", "How fast the character turns."));
                if(script.physicsEngine == HorizoneCharacter.PhysicsType.CharacterController)
                {
                    EditorGUILayout.PropertyField(gMult, new GUIContent("Gravity Multiplier", "Multiplier for gravity affect."));
                }
                EditorGUILayout.EndVertical();
            }

            // --- INTERNAL COMPONENT SETTINGS ---
            showInternal = EditorGUILayout.Foldout(showInternal, "Internal Component Settings", true, EditorStyles.foldoutHeader);
            if (showInternal)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                if (script.physicsEngine == HorizoneCharacter.PhysicsType.Rigidbody)
                {
                    if (rbSO != null)
                    {
                        rbSO.Update();
                        EditorGUILayout.LabelField("Rigidbody Settings", EditorStyles.boldLabel);
                        SafeProp(rbSO, "m_Mass");
                        SafeProp(rbSO, "m_Drag");
                        SafeProp(rbSO, "m_AngularDrag");
                        SafeProp(rbSO, "m_UseGravity");
                        SafeProp(rbSO, "m_IsKinematic");
                        SafeProp(rbSO, "m_Interpolate");
                        SafeProp(rbSO, "m_CollisionDetection");
                        SafeProp(rbSO, "m_Constraints");
                        rbSO.ApplyModifiedProperties();
                        EditorGUILayout.Space();
                    }
                    if (colSO != null)
                    {
                        colSO.Update();
                        EditorGUILayout.LabelField("Capsule Collider Settings", EditorStyles.boldLabel);
                        SafeProp(colSO, "m_Center");
                        SafeProp(colSO, "m_Radius");
                        SafeProp(colSO, "m_Height");
                        SafeProp(colSO, "m_Material");
                        colSO.ApplyModifiedProperties();
                        EditorGUILayout.Space();
                    }
                }
                else
                {
                    if (ccSO != null)
                    {
                        ccSO.Update();
                        EditorGUILayout.LabelField("Character Controller Settings", EditorStyles.boldLabel);
                        SafeProp(ccSO, "m_SlopeLimit");
                        SafeProp(ccSO, "m_StepOffset");
                        SafeProp(ccSO, "m_SkinWidth");
                        SafeProp(ccSO, "m_MinMoveDistance");
                        SafeProp(ccSO, "m_Center");
                        SafeProp(ccSO, "m_Radius");
                        SafeProp(ccSO, "m_Height");
                        SafeProp(ccSO, "m_Material");
                        ccSO.ApplyModifiedProperties();
                        EditorGUILayout.Space();
                    }
                }

                if (animSO != null)
                {
                    animSO.Update();
                    EditorGUILayout.LabelField("Animator Settings", EditorStyles.boldLabel);
                    SafeProp(animSO, "m_Controller");
                    SafeProp(animSO, "m_Avatar");
                    SafeProp(animSO, "m_ApplyRootMotion");
                    SafeProp(animSO, "m_UpdateMode");
                    SafeProp(animSO, "m_CullingMode");
                    animSO.ApplyModifiedProperties();
                }

                EditorGUILayout.EndVertical();
            }

            // --- FLYING ---
            showFly = EditorGUILayout.Foldout(showFly, "Flight Systems", true, EditorStyles.foldoutHeader);
            if (showFly)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(cFly, new GUIContent("Can Fly?", "Enable or disable flying capability."));
                if (cFly.boolValue)
                {
                    EditorGUILayout.PropertyField(flySpd, new GUIContent("Fly Speed", "Movement speed while flying."));
                    EditorGUILayout.PropertyField(levHeight, new GUIContent("Levitate Height", "Default height from ground when levitating."));
                }
                EditorGUILayout.EndVertical();
            }

            // --- PARKOUR ---
            showParkour = EditorGUILayout.Foldout(showParkour, "Parkour & Climbing", true, EditorStyles.foldoutHeader);
            if (showParkour)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(uParkour, new GUIContent("Enable Parkour", "Enable or disable parkour/climbing features."));
                if (uParkour.boolValue)
                {
                    EditorGUILayout.PropertyField(cSpeed, new GUIContent("Climb Speed", "Speed when climbing surfaces."));
                    EditorGUILayout.PropertyField(pLayer, new GUIContent("Parkour Layers", "Layers considered climbable."));
                    EditorGUILayout.PropertyField(wCheck, new GUIContent("Wall Check Distance", "Distance to check for walls to climb."));
                    EditorGUILayout.PropertyField(lGrab, new GUIContent("Ledge Grab Height", "Height offset for ledge grabbing."));
                    EditorGUILayout.PropertyField(eIK, new GUIContent("Enable IK", "Enable Inverse Kinematics for hand/foot placement."));
                }
                EditorGUILayout.EndVertical();
            }

            // --- COMBAT & STATS ---
            showCombat = EditorGUILayout.Foldout(showCombat, "Combat System", true, EditorStyles.foldoutHeader);
            if (showCombat)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Timings", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(evDur, new GUIContent("Evade Duration", "How long an evade/dash lasts."));
                EditorGUILayout.PropertyField(evCool, new GUIContent("Evade Cooldown", "Time before you can evade again."));
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Visual FX", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(hideTarget, new GUIContent("Hide on Idle", "GameObject to hide/show based on attack state."));
                EditorGUILayout.PropertyField(invertHide, new GUIContent("Invert Hiding", "If true, object is HIDDEN when attacking, instead of SHOWN."));

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Attacks & Combos", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(atkList, new GUIContent("Attacks List", "List of available single attacks."), true);
                EditorGUILayout.PropertyField(cmbList, new GUIContent("Combos List", "List of defined combo chains."), true);
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }

            showStats = EditorGUILayout.Foldout(showStats, "RPG Stats", true, EditorStyles.foldoutHeader);
            if (showStats)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(charStats, new GUIContent("Character Stats", "Health, Stamina, XP, etc."), true);
                EditorGUILayout.EndVertical();
            }

            // --- INTERACTION ---
            showInteract = EditorGUILayout.Foldout(showInteract, "Interactions", true, EditorStyles.foldoutHeader);
            if (showInteract)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(wLayer, new GUIContent("Water Layer", "Layer used to detect water for swimming."));
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(intList, new GUIContent("Interactions List", "Defined interactions and their ranges."), true);
                EditorGUILayout.EndVertical();
            }

            serializedObject.ApplyModifiedProperties();
        }

        void SafeProp(SerializedObject so, string propName)
        {
            SerializedProperty sp = so.FindProperty(propName);
            if (sp != null) EditorGUILayout.PropertyField(sp, new GUIContent(sp.displayName, "Internal property."), true);
        }

        new void DrawHeader()
        {
            // Reserve space for the header to draw into
            GUILayout.Space(10);
            Rect headerRect = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));

            // Safety check: Don't draw if rect is invalid (happens during initialization)
            if (headerRect.width <= 1) return;

            // --- Background & Border ---
            if (Event.current.type == EventType.Repaint)
            {
                // Draw rounded background
                GUI.DrawTexture(headerRect, MakeRoundedTex((int)headerRect.width, (int)headerRect.height, new Color(0.1f, 0.1f, 0.1f, 0.9f)));

                // Draw border manually using single-pixel lines
                Color borderColor = new Color(0.196f, 0.945f, 0.541f, 1.0f);
                GUI.DrawTexture(new Rect(headerRect.x, headerRect.y, headerRect.width, 2), MakeTex((int)headerRect.width, 2, borderColor)); // Top
                GUI.DrawTexture(new Rect(headerRect.x, headerRect.yMax - 2, headerRect.width, 2), MakeTex((int)headerRect.width, 2, borderColor)); // Bottom
                GUI.DrawTexture(new Rect(headerRect.x, headerRect.y, 2, headerRect.height), MakeTex(2, (int)headerRect.height, borderColor)); // Left
                GUI.DrawTexture(new Rect(headerRect.xMax - 2, headerRect.y, 2, headerRect.height), MakeTex(2, (int)headerRect.height, borderColor)); // Right
            }

            // --- Content (Manual GUI Positioning) ---
            
            // 1. Draw Icon
            Rect iconRect = new Rect(headerRect.x + 10, headerRect.y + 10, 40, 40);
            Texture2D customIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(ICON_PATH);
            Texture iconTex = (customIcon != null) ? customIcon : EditorGUIUtility.IconContent("d_Avatar Icon").image;
            
            if (iconTex != null)
            {
                GUI.DrawTexture(iconRect, iconTex, ScaleMode.ScaleToFit);
            }

            // 2. Draw Title
            Rect titleRect = new Rect(iconRect.xMax + 10, headerRect.y + 10, headerRect.width - 60, 20);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.fontSize = 16;
            titleStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f); // Bright text for dark background
            GUI.Label(titleRect, "HORIZONE CHARACTER", titleStyle);

            // 3. Draw Subtitle
            Rect subTitleRect = new Rect(iconRect.xMax + 10, headerRect.y + 30, headerRect.width - 60, 20);
            GUI.Label(subTitleRect, "Horizone Powerful Advanced Smart-AI Logic Character System", EditorStyles.miniLabel);
        }

        // Helper to create rounded texture
        private Texture2D MakeRoundedTex(int width, int height, Color col)
        {
            if (width <= 0 || height <= 0) return null;

            Texture2D result = new Texture2D(width, height);
            Color[] pix = new Color[width * height];
            
            // Corner radius
            int radius = 10;
            // Limit radius to half the smallest dimension to prevent overlap artifacts
            int minDim = Mathf.Min(width, height);
            if (radius * 2 > minDim) radius = minDim / 2;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Normalize coordinates to a single corner
                    int px = x < width / 2 ? x : width - 1 - x;
                    int py = y < height / 2 ? y : height - 1 - y;

                    float alpha = 1f;

                    // If we are inside the corner square region
                    if (px < radius && py < radius)
                    {
                        float dx = radius - px;
                        float dy = radius - py;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);

                        // Corner out logic (Convex)
                        if (dist > radius) 
                            alpha = 0f;
                        else if (dist > radius - 1f) 
                            alpha = 1f - (dist - (radius - 1f));
                        else 
                            alpha = 1f;
                    }
                    
                    pix[y * width + x] = new Color(col.r, col.g, col.b, col.a * alpha);
                }
            }
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        void DrawDebugInfo()
        {
            GUI.enabled = false;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField("State", script.currentMoveState.ToString());
            EditorGUILayout.TextField("Combat", script.currentCombatState.ToString());
            EditorGUILayout.EndHorizontal();
            GUI.enabled = true;
        }

        // Helper method to create a texture for the background
        private Texture2D MakeTex(int width, int height, Color col)
        {
            if (width <= 0 || height <= 0) return null;

            Color[] pix = new Color[width * height];
            for(int i = 0; i < pix.Length; ++i)
            {
                pix[i] = col;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
#endif
}