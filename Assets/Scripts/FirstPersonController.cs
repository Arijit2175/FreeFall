using UnityEngine;

public class FirstPersonController : MonoBehaviour
{
    public bool CanMove { get; private set; } = true;
    private bool isSprinting => canSprint && Input.GetKey(sprintKey);
    private bool shouldJump => Input.GetKeyDown(jumpKey);

    [Header("Functional Options")]
    [SerializeField] private bool canSprint = true;
    [SerializeField] private bool canJump = true;
    [SerializeField] private bool canHeadBob = true;

    [Header("Controls")]
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode slideKey = KeyCode.LeftAlt;

    [Header("Movement Parameters")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float sprintSpeed = 6f;

    [Header("Slide Parameters")]
    [SerializeField] private float slideInitialSpeed = 14f;
    [SerializeField] private float slideFriction = 8f;
    [SerializeField] private float slideHeight = 1f;
    [SerializeField] private float postSlideFriction = 6f;

    [Header("Wall Run Parameters")]
    [SerializeField] private float wallCheckDistance = 0.8f;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private float wallRunSpeed = 10f;
    [SerializeField] private float wallRunGravity = 0f;
    [SerializeField] private float wallStickForce = 3f;
    private float wallRunCooldown = 0f;
    [SerializeField] private float wallReattachDelay = 0.6f;
    [SerializeField] private float wallRunTiltAngle = 8f;
    [SerializeField] private float wallTiltSpeed = 8f;

    [Header("Look Parameters")]
    [SerializeField, Range(1, 10)] private float lookSpeedX = 2.0f;
    [SerializeField, Range(1, 10)] private float lookSpeedY = 2.0f;
    [SerializeField, Range(1, 180)] private float upperLookLimit = 80.0f;
    [SerializeField, Range(1, 180)] private float lowerLookLimit = 80.0f;

    [Header("Jumping Parameters")]
    [SerializeField] private float jumpForce = 8.0f;
    [SerializeField] private float gravity = 30.0f;

    [Header("Double Jump Parameters")]
    [SerializeField] private bool canDoubleJump = true;

    [Header("Headbob Parameters")]
    [SerializeField] private float walkBobSpeed = 14f;
    [SerializeField] private float walkBobAmount = 0.05f;
    [SerializeField] private float sprintBobSpeed = 18f;
    [SerializeField] private float sprintBobAmount = 0.11f;

    [Header("FOV Parameters")]
    [SerializeField] private float baseFOV = 78f;
    [SerializeField] private float sprintFOV = 86f;
    [SerializeField] private float slideFOV = 96f;
    [SerializeField] private float wallRunFOV = 90f;
    [SerializeField] private float fovSmooth = 10f;

    [Header("Camera Collision")]
    [SerializeField] private float cameraCollisionRadius = 0.15f;
    [SerializeField] private float cameraCollisionOffset = 0.2f;
    [SerializeField] private float cameraCollisionSmooth = 15f;
    [SerializeField] private LayerMask collisionMask;

    private float defaultYPos = 0;
    private float timer;

    private Camera playerCamera;
    private Vector3 cameraDefaultLocalPos;
    private CharacterController characterController;
    private Animator animator;

    private Vector3 moveDirection;
    private Vector2 currentInput;
    private float rotationX = 0;

    private bool isSliding = false;
    private bool slideMomentumEnded = false;
    private bool hasDoubleJumped = false;

    private float originalHeight;
    private float currentSlideSpeed;
    private Vector3 slideDirection;

    private bool wallLeft;
    private bool wallRight;
    private bool isWallRunning = false;
    private float currentTilt = 0f;
    private int lastWallSlide = 0;

    private RaycastHit leftWallHit;
    private RaycastHit rightWallHit;

    void Awake()
    {
        playerCamera = GetComponentInChildren<Camera>();
        characterController = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        playerCamera.fieldOfView = baseFOV;

        cameraDefaultLocalPos = playerCamera.transform.localPosition;

        defaultYPos = playerCamera.transform.localPosition.y;
        originalHeight = characterController.height;
    }

    void Update()
    {
        if (wallRunCooldown > 0)
            wallRunCooldown -= Time.deltaTime;

        if (CanMove)
        {
            CheckForWall();
            HandleWallRun();
            HandleSlide();
            HandleMovementInput();
            HandleMouseLook();
            HandleFOV();
            HandleAnimations();

            if (canJump) HandleJump();
            if (canHeadBob) HandleHeadBob();

            HandleCameraHeight();
            HandleCameraCollision();
            ApplyFinalMovements();
        }
    }

    private void HandleMovementInput()
    {
        if (isWallRunning) return;
        if (slideMomentumEnded)
        {
            Vector3 horizontal = new Vector3(moveDirection.x, 0, moveDirection.z);
            horizontal = Vector3.MoveTowards(
                horizontal,
                Vector3.zero,
                postSlideFriction * Time.deltaTime
            );

            moveDirection.x = horizontal.x;
            moveDirection.z = horizontal.z;
            return;
        }

        if (isSliding)
        {
            float yVel = moveDirection.y;
            moveDirection = slideDirection * currentSlideSpeed;
            moveDirection.y = yVel;
            return;
        }

        currentInput = new Vector2(
            (isSprinting ? sprintSpeed : walkSpeed) * Input.GetAxis("Vertical"),
            (isSprinting ? sprintSpeed : walkSpeed) * Input.GetAxis("Horizontal")
        );

        float moveDirectionY = moveDirection.y;

        moveDirection =
            (transform.TransformDirection(Vector3.forward) * currentInput.x) +
            (transform.TransformDirection(Vector3.right) * currentInput.y);

        moveDirection.y = moveDirectionY;
    }

    private void HandleMouseLook()
    {
        rotationX -= Input.GetAxis("Mouse Y") * lookSpeedY;
        rotationX = Mathf.Clamp(rotationX, -upperLookLimit, lowerLookLimit);

        float targetTilt = 0f;

        if (isWallRunning)
        {
            if (wallLeft)
                targetTilt = -wallRunTiltAngle;
            else if (wallRight)
                targetTilt = wallRunTiltAngle;
        }

        currentTilt = Mathf.Lerp(
            currentTilt,
            targetTilt,
            wallTiltSpeed * Time.deltaTime
        );

        playerCamera.transform.localRotation =
            Quaternion.Euler(rotationX, 0, currentTilt);
        transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeedX, 0);
    }

    private void HandleFOV()
    {
        float targetFOV = baseFOV;

        if (isWallRunning)
            targetFOV = wallRunFOV;
        else if (isSliding)
            targetFOV = slideFOV;
        else if (isSprinting)
            targetFOV = sprintFOV;

        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFOV,
            fovSmooth * Time.deltaTime
        );
    }

    private void HandleCameraCollision()
    {
        Vector3 origin = transform.position + Vector3.up * 1.6f;

        Vector3 desiredWorldPos = transform.TransformPoint(cameraDefaultLocalPos);

        Vector3 dir = desiredWorldPos - origin;
        float dist = dir.magnitude;

        if (Physics.SphereCast(
            origin,
            cameraCollisionRadius,
            dir.normalized,
            out RaycastHit hit,
            dist,
            collisionMask))
        {
            float safeDist = Mathf.Max(0f, hit.distance - cameraCollisionOffset);

            Vector3 safeWorldPos = origin + dir.normalized * safeDist;
            Vector3 safeLocalPos = transform.InverseTransformPoint(safeWorldPos);

            playerCamera.transform.localPosition = Vector3.Lerp(
                playerCamera.transform.localPosition,
                safeLocalPos,
                cameraCollisionSmooth * Time.deltaTime
            );
        }
        else
        {
            playerCamera.transform.localPosition = Vector3.Lerp(
                playerCamera.transform.localPosition,
                cameraDefaultLocalPos,
                cameraCollisionSmooth * Time.deltaTime
            );
        }
    }

    private void HandleAnimations()
    {
        bool isMoving =
            characterController.isGrounded &&
            !isSliding &&
            !slideMomentumEnded &&
            !isWallRunning &&
            (
                Mathf.Abs(Input.GetAxis("Vertical")) > 0.1f ||
                Mathf.Abs(Input.GetAxis("Horizontal")) > 0.1f
            );

        animator.SetBool("isMoving", isMoving);
    }

    private void HandleJump()
    {
        if (!shouldJump || isSliding || slideMomentumEnded)
            return;

        if (isWallRunning)
        {
            RaycastHit wallHit = wallRight ? rightWallHit : leftWallHit;
            Vector3 wallNormal = wallHit.normal;

            Vector3 jumpDir =
                wallNormal * 12f +
                Vector3.up * 8f +
                transform.forward * 5f;

            moveDirection = jumpDir;
            isWallRunning = false;
            wallRunCooldown = wallReattachDelay;
            lastWallSlide = wallRight ? 1 : -1;

            hasDoubleJumped = false;

            return;
        }

        if (characterController.isGrounded)
        {
            moveDirection.y = jumpForce;
        }
        else if (canDoubleJump && !hasDoubleJumped)
        {
            moveDirection.y = jumpForce;
            hasDoubleJumped = true;
        }
    }

    private void HandleSlide()
    {
        bool wantsToSlide =
            Input.GetKey(slideKey) &&
            isSprinting &&
            characterController.isGrounded;

        if (wantsToSlide && !isSliding && !slideMomentumEnded)
        {
            StartSlide();
        }

        if (isSliding)
        {
            if (!Input.GetKey(slideKey))
            {
                StopSlide();
                return;
            }

            currentSlideSpeed -= slideFriction * Time.deltaTime;

            if (currentSlideSpeed <= walkSpeed)
            {
                isSliding = false;
                slideMomentumEnded = true;
            }
        }

        if (slideMomentumEnded)
        {
            if (!Input.GetKey(slideKey))
            {
                slideMomentumEnded = false;
                characterController.height = originalHeight;
            }
        }
    }

    private void StartSlide()
    {
        isSliding = true;
        characterController.height = slideHeight;

        currentSlideSpeed = slideInitialSpeed;
        slideDirection = transform.forward;
    }

    private void StopSlide()
    {
        isSliding = false;
        slideMomentumEnded = false;
        characterController.height = originalHeight;
    }

    private void HandleHeadBob()
    {
        if (isSliding || slideMomentumEnded || isWallRunning) return;
        if (!characterController.isGrounded) return;

        if (Mathf.Abs(moveDirection.x) > 0.1f || Mathf.Abs(moveDirection.z) > 0.1f)
        {
            timer += Time.deltaTime * (isSprinting ? sprintBobSpeed : walkBobSpeed);

            Vector3 pos = playerCamera.transform.localPosition;
            pos.y = defaultYPos + Mathf.Sin(timer) * (isSprinting ? sprintBobAmount : walkBobAmount);
            playerCamera.transform.localPosition = pos;
        }
        else
        {
            Vector3 pos = playerCamera.transform.localPosition;
            pos.y = Mathf.Lerp(pos.y, defaultYPos, 10f * Time.deltaTime);
            playerCamera.transform.localPosition = pos;
        }
    }

    private void HandleCameraHeight()
    {
        float targetY = (isSliding || slideMomentumEnded)
            ? defaultYPos - 0.5f
            : defaultYPos;

        Vector3 pos = playerCamera.transform.localPosition;
        pos.y = Mathf.Lerp(pos.y, targetY, 10f * Time.deltaTime);
        playerCamera.transform.localPosition = pos;
    }

    private void CheckForWall()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 1f;

        wallRight = Physics.Raycast(rayOrigin, transform.right, out rightWallHit, wallCheckDistance, wallLayer);
        wallLeft = Physics.Raycast(rayOrigin, -transform.right, out leftWallHit, wallCheckDistance, wallLayer);
    }

    private void HandleWallRun()
    {
        bool validWall =
    (wallLeft && lastWallSlide != -1) ||
    (wallRight && lastWallSlide != 1);

        bool canWallRun =
            wallRunCooldown <= 0f &&
            !characterController.isGrounded &&
            moveDirection.y < 2f &&
            Input.GetAxis("Vertical") > 0 &&
            validWall &&
            !isSliding &&
            !slideMomentumEnded;

        if (canWallRun)
            StartWallRun();
        else
            StopWallRun();
    }

    private void StartWallRun()
    {
        isWallRunning = true;

        RaycastHit wallHit = wallRight ? rightWallHit : leftWallHit;
        Vector3 wallNormal = wallHit.normal;

        Vector3 wallForward = Vector3.Cross(wallNormal, Vector3.up);

        if (Vector3.Dot(wallForward, transform.forward) < 0)
            wallForward = -wallForward;

        Vector3 wallVelocity = wallForward * wallRunSpeed;

        wallVelocity += -wallNormal * wallStickForce;

        moveDirection.x = wallVelocity.x;
        moveDirection.z = wallVelocity.z;

        moveDirection.y = -wallRunGravity;
    }

    private void StopWallRun()
    {
        isWallRunning = false;
    }

    private void ApplyFinalMovements()
    {
        if (characterController.isGrounded)
        {
            lastWallSlide = 0;
            hasDoubleJumped = false;
        }
        if (!characterController.isGrounded)
        {
            if (!isWallRunning)
                moveDirection.y -= gravity * Time.deltaTime;
            else
                moveDirection.y = 0f;
        }

        characterController.Move(moveDirection * Time.deltaTime);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Vector3 origin = transform.position + Vector3.up * 1f;
        Gizmos.DrawRay(origin, transform.right * wallCheckDistance);
        Gizmos.DrawRay(origin, -transform.right * wallCheckDistance);
    }
}