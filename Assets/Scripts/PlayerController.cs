using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
	private static readonly int AnimatorSpeed = Animator.StringToHash("Speed");
	private static readonly int AnimatorGrounded = Animator.StringToHash("Grounded");
	private static readonly int AnimatorVerticalSpeed = Animator.StringToHash("VerticalSpeed");
	private static readonly int AnimatorSwing = Animator.StringToHash("Swing");
	private static readonly int AnimatorSwingVariant = Animator.StringToHash("SwingVariant");
	private static readonly int AnimatorRunSpeedMult = Animator.StringToHash("RunSpeedMultiplier");
	private static readonly int AnimatorHit = Animator.StringToHash("Hit");
    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference dashAction;
	[SerializeField] private InputActionReference attackAction;

    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Collider2D playerCollider;
	[SerializeField] private Animator animator;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float acceleration = 80f;
    [SerializeField] private float deceleration = 100f;
    [SerializeField] private float airControlMultiplier = 0.8f;
	[SerializeField] private float inputDeadzoneEnter = 0.08f;
	[SerializeField] private float inputDeadzoneExit = 0.04f;
	[SerializeField] private float minStartSpeed = 1.2f;
	[SerializeField] private float wallCheckDistance = 0.08f;
	[SerializeField] private float stopDecelerationMultiplier = 0.6f;

	[Header("Physics Materials")]
	[SerializeField] private PhysicsMaterial2D groundedMaterial;
	[SerializeField] private PhysicsMaterial2D noFrictionMaterial;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 14f;
    [SerializeField] private float jumpCutMultiplier = 0.5f;
    [SerializeField] private float fallGravityMultiplier = 2f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime = 0.12f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 0.1f;
    [SerializeField] private float groundCheckWidthMultiplier = 0.95f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 18f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 0.4f;

	[Header("Animation (Sprite Swap)")]
	[SerializeField] private Sprite idleSprite;
	[SerializeField] private Sprite[] idleSprites;
	[SerializeField] private Sprite[] runSprites;
	[SerializeField] private float runFps = 12f;
	[SerializeField] private float idleFps = 8f;
	[SerializeField] private float runSpeedThreshold = 0.1f;
	[SerializeField] private bool runOnlyWhenGrounded = true;
	[SerializeField] private bool useAnimatorGroundedForAnimation = false;

	[Header("Animation (Animator)")]
	[SerializeField] private bool useAnimatorForAnimation = true;
	[SerializeField] private float runAnimSpeedMin = 0.25f;
	[SerializeField] private float runAnimSpeedMax = 1.0f;
	[SerializeField] private float runAnimLerpRate = 12f;

	private float moveInput;
	private float moveInputFiltered;
	private float prevMoveInput;
    private bool facingRight = true;

    private bool isGrounded;
	private bool isTouchingWall;
	private int wallSide; // -1 = left, +1 = right, 0 = none
    private float coyoteCounter;
    private float jumpBufferCounter;

    private bool isDashing;
    private float dashTimer;
    private float dashCooldownTimer;
    private float dashDirection;

    private bool jumpPressed;
    private bool jumpReleased;

	private float runAnimTimer;
	private int runAnimIndex;
	private float idleAnimTimer;
	private int idleAnimIndex;
	private bool wasRunning;
	private float currentRunAnimSpeed = 1f;
	private float stopCoastTimer;

	[Header("Stopping Feel")]
	[SerializeField] private float stopCoastTime = 0.08f;

	[Header("Parry / Damage (Prototype)")]
	[SerializeField] private float parryWindowSeconds = 0.2f;
	[SerializeField] private int hp = 5;
	private bool parryActive;
	private float parryTimer;

	[SerializeField] private bool triggerHitAnimationOnDamage = true;

	[Header("Melee (Swing)")]
	[SerializeField] private LayerMask enemyLayer;
	[SerializeField] private Vector2 swingHitboxOffset = new Vector2(0.8f, 0f);
	[SerializeField] private Vector2 swingHitboxSize = new Vector2(1.2f, 1.0f);
	[SerializeField] private int swingDamage = 1;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (playerCollider == null) playerCollider = GetComponent<Collider2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
		if (animator == null) animator = GetComponentInChildren<Animator>();

		// Ensure a no-friction material exists to prevent wall sticking if not assigned in Inspector
		if (noFrictionMaterial == null)
		{
			noFrictionMaterial = new PhysicsMaterial2D("NoFrictionRuntime");
			noFrictionMaterial.friction = 0f;
			noFrictionMaterial.bounciness = 0f;
		}
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
        jumpAction.action.Enable();
        dashAction.action.Enable();
		if (attackAction != null) attackAction.action.Enable();

        jumpAction.action.performed += OnJumpPerformed;
        jumpAction.action.canceled += OnJumpCanceled;
    }

    private void OnDisable()
    {
        jumpAction.action.performed -= OnJumpPerformed;
        jumpAction.action.canceled -= OnJumpCanceled;

        moveAction.action.Disable();
        jumpAction.action.Disable();
        dashAction.action.Disable();
		if (attackAction != null) attackAction.action.Disable();
    }

    private void Update()
    {
        ReadInput();
        HandleTimers();
        CheckGrounded();
        HandleJumpLogic();
        HandleSpriteFlip();
		if (!useAnimatorForAnimation) UpdateSpriteAnimation();
		UpdateAnimatorParameters();
		HandleAttackInput();

        jumpPressed = false;
        jumpReleased = false;
    }

	private void HandleAttackInput()
	{
		if (attackAction == null || animator == null) return;

		// Left click (or whatever you bind Attack to) triggers a swing variant.
		if (attackAction.action.triggered)
		{
			int variant = Random.Range(1, 3); // 1 or 2
			animator.SetInteger(AnimatorSwingVariant, variant);
			animator.SetTrigger(AnimatorSwing);

			// Start parry window immediately with the swing (prototype timing).
			parryActive = true;
			parryTimer = parryWindowSeconds;
		}
	}

	// Exposed for Enemy1 raycast to check parry timing (prototype).
	public bool IsParrying()
	{
		return parryActive;
	}

	// Temporary placeholder damage logic for the prototype.
	public void TakeDamage(int amount)
	{
		if (amount <= 0) return;

		hp -= amount;
		if (hp < 0) hp = 0;

		// Play Hit animation on damage (prototype).
		if (triggerHitAnimationOnDamage && animator != null)
		{
			// Avoid warnings if the Animator doesn't have the parameter.
			if (AnimatorHasTriggerParameter(AnimatorHit))
				animator.SetTrigger(AnimatorHit);
		}

		Debug.Log($"Damage Taken: -{amount} | HP now: {hp}", this);
	}

	/// <summary>
	/// Animation Event entry point. Add this event to Swing1 and Swing2 at the impact frame.
	/// </summary>
	public void OnSwingHitEvent()
	{
		Vector2 center = (Vector2)transform.position;
		Vector2 offset = swingHitboxOffset;
		offset.x *= facingRight ? 1f : -1f;
		Vector2 hitboxCenter = center + offset;

		Collider2D[] hits = Physics2D.OverlapBoxAll(hitboxCenter, swingHitboxSize, 0f, enemyLayer);
		if (hits == null || hits.Length == 0) return;

		for (int i = 0; i < hits.Length; i++)
		{
			if (hits[i] == null) continue;
			EnemyHealth eh = hits[i].GetComponent<EnemyHealth>();
			if (eh == null) eh = hits[i].GetComponentInParent<EnemyHealth>();
			if (eh == null) continue;

			Vector2 dir = facingRight ? Vector2.right : Vector2.left;
			eh.TakeDamage(swingDamage, dir);
		}
	}

    private void FixedUpdate()
    {
        if (isDashing)
        {
            rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0f);
            return;
        }

		HandleMovement();
        ApplyBetterJumpGravity();
		CheckWallContact();
		ApplyFrictionMaterial();
    }

    private void ReadInput()
    {
		float rawInput = moveAction.action.ReadValue<Vector2>().x;

		// Hysteresis deadzone: prevents chattering that can cause sudden stops
		if (Mathf.Abs(moveInputFiltered) < 0.0001f)
		{
			// Currently idle: require enter threshold
			moveInputFiltered = Mathf.Abs(rawInput) >= inputDeadzoneEnter ? rawInput : 0f;
		}
		else
		{
			// Currently moving: only zero when below exit threshold
			moveInputFiltered = Mathf.Abs(rawInput) <= inputDeadzoneExit ? 0f : rawInput;
		}

		moveInput = moveInputFiltered;

		// Start a brief "extra step" coast when the player releases movement.
		if (Mathf.Abs(prevMoveInput) > 0.01f && Mathf.Abs(moveInput) <= 0.01f && isGrounded)
			stopCoastTimer = stopCoastTime;
		else if (Mathf.Abs(moveInput) > 0.01f)
			stopCoastTimer = 0f;

		prevMoveInput = moveInput;

        if (jumpPressed)
        {
            jumpBufferCounter = jumpBufferTime;
        }

        if (jumpReleased && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
        }

        if (dashAction.action.triggered && !isDashing && dashCooldownTimer <= 0f)
        {
            StartDash();
        }
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        jumpPressed = true;
    }

    private void OnJumpCanceled(InputAction.CallbackContext context)
    {
        jumpReleased = true;
    }

    private void HandleTimers()
    {
        if (jumpBufferCounter > 0f)
            jumpBufferCounter -= Time.deltaTime;

		if (stopCoastTimer > 0f)
			stopCoastTimer -= Time.deltaTime;

		// Parry window timer (prototype)
		if (parryActive)
		{
			parryTimer -= Time.deltaTime;
			if (parryTimer <= 0f)
				parryActive = false;
		}

        if (dashCooldownTimer > 0f)
            dashCooldownTimer -= Time.deltaTime;

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
            {
                isDashing = false;
            }
        }
    }

    private void CheckGrounded()
    {
        Vector2 boxCenter = playerCollider.bounds.center;
        Vector2 boxSize = new Vector2(
            playerCollider.bounds.size.x * groundCheckWidthMultiplier,
			playerCollider.bounds.size.y
        );

        RaycastHit2D hit = Physics2D.BoxCast(
            boxCenter,
            boxSize,
            0f,
            Vector2.down,
            groundCheckDistance,
            groundLayer
        );

        isGrounded = hit.collider != null;

        if (isGrounded)
            coyoteCounter = coyoteTime;
        else
            coyoteCounter -= Time.deltaTime;
    }

    private void HandleJumpLogic()
    {
        if (isDashing) return;

        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            Jump();
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
        }
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
    }

    private void HandleMovement()
    {
        float targetSpeed = moveInput * moveSpeed;
		float control = isGrounded ? 1f : airControlMultiplier;
		bool braking = Mathf.Abs(targetSpeed) <= 0.05f;

		// During the coast window, don't brake at all (keeps current speed briefly).
		if (braking && isGrounded && stopCoastTimer > 0f)
		{
			targetSpeed = rb.linearVelocity.x;
			braking = false;
		}

		float baseAccel = braking ? deceleration : acceleration;
		// Apply a softer deceleration when stopping to allow a slight coast
		if (braking && isGrounded) baseAccel *= stopDecelerationMultiplier;
		float accelRate = baseAccel * control;

        float newVelX = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeed, accelRate * Time.fixedDeltaTime);

		// Anti-stall nudge: if input present but speed nearly zero, kick-start motion
		if (isGrounded && Mathf.Abs(moveInput) > 0.0f && Mathf.Abs(newVelX) < 0.05f)
		{
			newVelX = Mathf.Sign(moveInput) * Mathf.Max(0.05f, minStartSpeed);
			rb.WakeUp();
		}

		// If airborne and touching a wall, prevent pushing into it (clip horizontal velocity towards the wall to zero)
		if (!isGrounded && isTouchingWall)
		{
			if (wallSide == -1) // left wall
				newVelX = Mathf.Max(0f, newVelX);
			else if (wallSide == 1) // right wall
				newVelX = Mathf.Min(0f, newVelX);
		}

		rb.linearVelocity = new Vector2(newVelX, rb.linearVelocity.y);
    }

    private void ApplyBetterJumpGravity()
    {
        if (isGrounded || isDashing) return;

        if (rb.linearVelocity.y < 0f)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallGravityMultiplier - 1f) * Time.fixedDeltaTime;
        }
    }

	private void CheckWallContact()
	{
		Vector2 center = playerCollider.bounds.center;
		Vector2 size = new Vector2(playerCollider.bounds.size.x * 0.9f, playerCollider.bounds.size.y * 0.9f);

		RaycastHit2D hitRight = Physics2D.Raycast(center, Vector2.right, wallCheckDistance, groundLayer);
		RaycastHit2D hitLeft = Physics2D.Raycast(center, Vector2.left, wallCheckDistance, groundLayer);

		bool right = hitRight.collider != null;
		bool left = hitLeft.collider != null;

		isTouchingWall = !isGrounded && (left || right);
		wallSide = 0;
		if (left) wallSide = -1;
		else if (right) wallSide = 1;
	}

	private void ApplyFrictionMaterial()
	{
		if (playerCollider == null) return;

		// Use no friction when airborne or when touching a wall to prevent sticking
		PhysicsMaterial2D target = (isGrounded && !isTouchingWall) ? groundedMaterial : noFrictionMaterial;
		if (playerCollider.sharedMaterial != target)
		{
			playerCollider.sharedMaterial = target;
		}

	}

    private void StartDash()
    {
        isDashing = true;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;

        if (Mathf.Abs(moveInput) > 0.01f)
            dashDirection = Mathf.Sign(moveInput);
        else
            dashDirection = facingRight ? 1f : -1f;

        rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0f);
    }

    private void HandleSpriteFlip()
    {
        if (moveInput > 0.01f)
            facingRight = true;
        else if (moveInput < -0.01f)
            facingRight = false;

        if (spriteRenderer != null)
            spriteRenderer.flipX = !facingRight;
    }

	private void UpdateAnimatorParameters()
	{
		if (animator == null) return;

		float speedX = Mathf.Abs(rb.linearVelocity.x);
		animator.SetFloat(AnimatorSpeed, speedX);
		animator.SetBool(AnimatorGrounded, isGrounded);
		animator.SetFloat(AnimatorVerticalSpeed, rb.linearVelocity.y);

		// Drive run animation speed multiplier based on normalized horizontal speed
		float normalized = moveSpeed > 0.001f ? Mathf.Clamp01(speedX / moveSpeed) : 0f;
		float targetMult = Mathf.Lerp(runAnimSpeedMin, runAnimSpeedMax, normalized);
		currentRunAnimSpeed = Mathf.MoveTowards(currentRunAnimSpeed, targetMult, runAnimLerpRate * Time.deltaTime);
		if (AnimatorHasParameter(AnimatorRunSpeedMult))
			animator.SetFloat(AnimatorRunSpeedMult, currentRunAnimSpeed);
	}

	private bool AnimatorHasParameter(int nameHash)
	{
		if (animator == null) return false;
		foreach (var p in animator.parameters)
		{
			if (p.nameHash == nameHash) return true;
		}
		return false;
	}

	private bool AnimatorHasTriggerParameter(int nameHash)
	{
		if (animator == null) return false;
		foreach (var p in animator.parameters)
		{
			if (p.nameHash != nameHash) continue;
			return p.type == AnimatorControllerParameterType.Trigger;
		}
		return false;
	}

	private void UpdateSpriteAnimation()
	{
		if (spriteRenderer == null) return;

		bool groundedForAnim = isGrounded;
		if (useAnimatorGroundedForAnimation && animator != null)
			groundedForAnim = animator.GetBool(AnimatorGrounded);

		bool canRunAnim = !runOnlyWhenGrounded || groundedForAnim;
		bool shouldRun = canRunAnim && Mathf.Abs(rb.linearVelocity.x) >= runSpeedThreshold;

		// Reset timers only when switching between run/idle so animations don't constantly restart.
		if (shouldRun != wasRunning)
		{
			if (shouldRun)
			{
				runAnimTimer = 0f;
				runAnimIndex = 0;
			}
			else
			{
				idleAnimTimer = 0f;
				idleAnimIndex = 0;
			}
			wasRunning = shouldRun;
		}

		// Run animation (multi-sprite)
		if (shouldRun)
		{
			if (runSprites == null || runSprites.Length == 0)
			{
				// Fallback to idle if run frames weren't assigned.
				if (idleSprite != null) spriteRenderer.sprite = idleSprite;
				return;
			}

			float fps = Mathf.Max(1f, runFps);
			runAnimTimer += Time.deltaTime;
			float frameTime = 1f / fps;

			while (runAnimTimer >= frameTime)
			{
				runAnimTimer -= frameTime;
				runAnimIndex = (runAnimIndex + 1) % runSprites.Length;
			}

			Sprite s = runSprites[runAnimIndex];
			if (s != null) spriteRenderer.sprite = s;
			return;
		}

		// Idle animation (multi-sprite). If you only want idle on ground, keep idle pose while airborne too.
		if (!canRunAnim)
		{
			if (idleSprite != null) spriteRenderer.sprite = idleSprite;
			return;
		}

		if (idleSprites != null && idleSprites.Length > 0)
		{
			float fps = Mathf.Max(1f, idleFps);
			idleAnimTimer += Time.deltaTime;
			float frameTime = 1f / fps;

			while (idleAnimTimer >= frameTime)
			{
				idleAnimTimer -= frameTime;
				idleAnimIndex = (idleAnimIndex + 1) % idleSprites.Length;
			}

			Sprite s = idleSprites[idleAnimIndex];
			if (s != null) spriteRenderer.sprite = s;
		}
		else
		{
			// Fallback to single idle sprite if multi-sprite idle isn't assigned.
			if (idleSprite != null) spriteRenderer.sprite = idleSprite;
		}
	}

    private void OnDrawGizmosSelected()
    {
        if (playerCollider == null) return;

        Gizmos.color = Color.green;

        Vector3 boxCenter = playerCollider.bounds.center + Vector3.down * groundCheckDistance;
        Vector3 boxSize = new Vector3(
            playerCollider.bounds.size.x * groundCheckWidthMultiplier,
            playerCollider.bounds.size.y,
            1f
        );

        Gizmos.DrawWireCube(boxCenter, boxSize);

		// Draw swing hitbox (attack range) for tuning.
		Gizmos.color = Color.red;
		Vector2 offset = swingHitboxOffset;
		offset.x *= facingRight ? 1f : -1f;
		Vector3 swingCenter = transform.position + (Vector3)offset;
		Gizmos.DrawWireCube(swingCenter, (Vector3)swingHitboxSize);
    }
}