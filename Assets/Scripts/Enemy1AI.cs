using UnityEngine;

/// <summary>
/// Enemy1 AI (idle -> small chase -> raycast "shoot").
/// For now, "shooting" is raycast-only (no projectile instantiation).
/// </summary>
public class Enemy1AI : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private Rigidbody2D rb;
	[SerializeField] private SpriteRenderer spriteRenderer;
	[SerializeField] private Animator animator;
	[SerializeField] private Transform player;

	[Header("Ranges")]
	[SerializeField] private float chaseRange = 4f;
	[SerializeField] private float shootRange = 2f;
	[SerializeField] private float rayDistance = 8f;

	[Header("Chase")]
	[SerializeField] private float runSpeed = 2.5f;

	[Header("Shooting")]
	[SerializeField] private string playerTag = "Player";
	[SerializeField] private bool ignoreOtherEnemiesInRaycast = true;
	[SerializeField] private Vector2 rayOriginOffset = new Vector2(0.25f, 0f);
	[SerializeField] private float shootCooldown = 0.8f; // legacy fallback
	[SerializeField] private int damageAmount = 1;
	[SerializeField] private ParticleSystem muzzleFlashPrefab;
	[SerializeField] private Vector2 muzzleOffset = new Vector2(0.4f, 0f);

	[Header("Engagement")]
	[SerializeField] private float preferredRange = 3.0f;
	[SerializeField] private float rangeTolerance = 0.7f;

	[Header("Behavior Chances")]
	[SerializeField] private float strafeChance = 0.4f;
	[SerializeField] private float burstChance = 0.35f;
	[SerializeField] private float jitterChance = 0.35f;
	[SerializeField] private float retreatChance = 0.25f;

	[Header("Timings (Ranges)")]
	[SerializeField] private Vector2 pauseRange = new Vector2(0.2f, 0.6f);
	[SerializeField] private Vector2 windupRange = new Vector2(0.1f, 0.2f);
	[SerializeField] private Vector2 strafeDurationRange = new Vector2(0.3f, 0.8f);
	[SerializeField] private Vector2 singleShotCooldownRange = new Vector2(0.6f, 1.2f);
	[SerializeField] private Vector2 timeBetweenBurstShotsRange = new Vector2(0.12f, 0.22f);
	[SerializeField] private Vector2 postBurstRestRange = new Vector2(0.9f, 1.5f);
	[SerializeField] private Vector2 jitterDurationRange = new Vector2(0.1f, 0.2f);
	[SerializeField] private Vector2 burstCountRange = new Vector2(2f, 3f);

	[Header("Speeds")]
	[SerializeField] private float strafeSpeedMultiplier = 0.7f;
	[SerializeField] private float retreatSpeedMultiplier = 0.9f;
	[SerializeField] private float jitterDistance = 0.25f;

	[Header("Animator Params")]
	[SerializeField] private string speedParam = "Speed";
	[SerializeField] private string shootTrigger = "Shoot";

	// Cached hashes
	private int speedParamHash;
	private int shootTriggerHash; // unused; triggers are set by string/hash, but keep for flexibility

	private float desiredVelX;
	private float timer;
	private float interShotTimer;
	private int remainingBurstShots;
	private bool inBurst;
	private bool pendingRaycast;

	private bool facingRight = true;

	// Stored for later parry/deflect integration
	public Vector2 LastHitPoint => lastHitPoint;
	public bool LastShotHitPlayer => lastShotHitPlayer;
	public RaycastHit2D LastShotHit => lastShotHit;

	private Vector2 lastHitPoint;
	private bool lastShotHitPlayer;
	private RaycastHit2D lastShotHit;

	private void Awake()
	{
		if (rb == null) rb = GetComponent<Rigidbody2D>();
		if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
		if (animator == null) animator = GetComponentInChildren<Animator>();

		speedParamHash = Animator.StringToHash(speedParam);
		shootTriggerHash = Animator.StringToHash(shootTrigger);
	}

	private void Update()
	{
		if (player == null) return;

		// Distance check
		Vector2 enemyPos = transform.position;
		Vector2 playerPos = player.position;
		float dist = Vector2.Distance(enemyPos, playerPos);

		// Update facing direction based on player position.
		float dx = playerPos.x - enemyPos.x;
		if (dx > 0.01f) facingRight = true;
		else if (dx < -0.01f) facingRight = false;

		if (spriteRenderer != null)
			spriteRenderer.flipX = !facingRight;

		RunBehavior(dist, enemyPos, playerPos);
	}

	private void FixedUpdate()
	{
		if (rb == null) return;

		// Preserve vertical velocity (jumps/falls still handled by physics).
		rb.linearVelocity = new Vector2(desiredVelX, rb.linearVelocity.y);
	}

	// Called from code when the shoot animation should start.
	private void FireShootAnimation()
	{
		if (animator != null)
		{
			// Trigger swing/shoot animation.
			animator.SetTrigger(shootTrigger);
		}
	}

	/// <summary>
	/// Animation Event entry point. Put this event at the END of the Enemy1Shoot clip.
	/// </summary>
	public void OnShootRaycastEvent()
	{
		if (!pendingRaycast) return;
		pendingRaycast = false;

		SpawnMuzzleFlash();
		PerformRaycastShot();
	}

	#region Behavior
	private enum AIState { Approach, Pause, Strafe, Windup, Shooting, Cooldown, Jitter, Retreat }
	private AIState state;
	private int strafeDir = 1; // +1 right, -1 left

	private void RunBehavior(float dist, Vector2 enemyPos, Vector2 playerPos)
	{
		float dt = Time.deltaTime;
		if (timer > 0f) timer -= dt;
		if (interShotTimer > 0f) interShotTimer -= dt;

		switch (state)
		{
			case AIState.Approach:
				HandleApproach(dist);
				if (IsInPreferredBand(dist)) ChooseNextPreEngage();
				break;

			case AIState.Pause:
				desiredVelX = 0f;
				SetAnimatorSpeed(0f);
				if (timer <= 0f) ChooseNextPreEngage();
				break;

			case AIState.Jitter:
				// A brief micro step forward/back to look less robotic
				float jdir = (Random.value < 0.5f ? -1f : 1f);
				desiredVelX = jdir * runSpeed * 0.5f;
				SetAnimatorSpeed(Mathf.Abs(desiredVelX));
				if (timer <= 0f)
				{
					desiredVelX = 0f;
					SetAnimatorSpeed(0f);
					state = AIState.Pause;
					timer = RandomRange(pauseRange);
				}
				break;

			case AIState.Strafe:
				desiredVelX = strafeDir * runSpeed * strafeSpeedMultiplier;
				SetAnimatorSpeed(Mathf.Abs(desiredVelX));
				if (timer <= 0f) BeginWindup();
				break;

			case AIState.Windup:
				desiredVelX = 0f;
				SetAnimatorSpeed(0f);
				if (timer <= 0f) BeginShooting();
				break;

			case AIState.Shooting:
				desiredVelX = 0f;
				SetAnimatorSpeed(0f);
				// During burst, trigger additional shots after small delays
				if (inBurst && remainingBurstShots > 0 && interShotTimer <= 0f)
				{
					interShotTimer = RandomRange(timeBetweenBurstShotsRange);
					remainingBurstShots--;
					TriggerShot();
				}
				// When burst queue is done and no pending inter-shot delay, go cooldown
				if ((!inBurst || remainingBurstShots <= 0) && interShotTimer <= 0f && pendingRaycast == false)
				{
					state = AIState.Cooldown;
					timer = inBurst ? RandomRange(postBurstRestRange) : RandomRange(singleShotCooldownRange);
					inBurst = false;
				}
				break;

			case AIState.Cooldown:
				desiredVelX = 0f;
				SetAnimatorSpeed(0f);
				if (timer <= 0f)
				{
					if (Random.value < retreatChance)
					{
						state = AIState.Retreat;
						timer = RandomRange(strafeDurationRange);
						strafeDir = (facingRight ? -1 : 1); // brief back step
					}
					else
					{
						state = AIState.Approach;
					}
				}
				break;

			case AIState.Retreat:
				desiredVelX = (strafeDir) * runSpeed * retreatSpeedMultiplier;
				SetAnimatorSpeed(Mathf.Abs(desiredVelX));
				if (timer <= 0f) state = AIState.Approach;
				break;
		}
	}

	private void HandleApproach(float dist)
	{
		// Move closer/farther to stay near preferred band
		float dir = 0f;
		if (dist > preferredRange + rangeTolerance) dir = (facingRight ? 1f : -1f);
		else if (dist < preferredRange - rangeTolerance) dir = (facingRight ? -1f : 1f);
		else dir = 0f;

		desiredVelX = dir * runSpeed;
		SetAnimatorSpeed(Mathf.Abs(desiredVelX));

		// Once within legacy shootRange, we can start pre-engage choices too
	}

	private bool IsInPreferredBand(float dist)
	{
		return Mathf.Abs(dist - preferredRange) <= rangeTolerance || dist <= shootRange;
	}

	private void ChooseNextPreEngage()
	{
		// Randomly jitter, strafe, or go straight to windup/shoot
		if (Random.value < jitterChance)
		{
			state = AIState.Jitter;
			timer = RandomRange(jitterDurationRange);
			return;
		}

		if (Random.value < strafeChance)
		{
			state = AIState.Strafe;
			timer = RandomRange(strafeDurationRange);
			strafeDir = (Random.value < 0.5f ? -1 : 1);
			return;
		}

		BeginWindup();
	}

	private void BeginWindup()
	{
		state = AIState.Windup;
		timer = RandomRange(windupRange);
	}

	private void BeginShooting()
	{
		state = AIState.Shooting;
		inBurst = (Random.value < burstChance);
		if (inBurst)
		{
			remainingBurstShots = Mathf.RoundToInt(RandomRange(burstCountRange));
			// First shot now, then remainingBurstShots will trigger in loop
			TriggerShot();
		}
		else
		{
			TriggerShot();
		}
	}

	private void TriggerShot()
	{
		pendingRaycast = true;
		FireShootAnimation();
	}
	#endregion

	private void PerformRaycastShot()
	{
		// Raycast in facing direction. No projectile is spawned.
		Vector2 origin = (Vector2)transform.position + new Vector2(rayOriginOffset.x * (facingRight ? 1f : -1f), rayOriginOffset.y);
		Vector2 dir = facingRight ? Vector2.right : Vector2.left;

		// Important: if multiple enemies line up, a plain Raycast will hit the front enemy and
		// never reach the player. We treat enemies as "transparent" for hitscan purposes,
		// while still allowing world geometry to block shots.
		if (ignoreOtherEnemiesInRaycast)
		{
			lastShotHit = GetFirstRelevantHit(origin, dir, rayDistance);
		}
		else
		{
			lastShotHit = Physics2D.Raycast(origin, dir, rayDistance);
		}

		lastHitPoint = lastShotHit.collider != null ? lastShotHit.point : (origin + dir * rayDistance);

		if (lastShotHit.collider != null && lastShotHit.collider.CompareTag(playerTag))
		{
			lastShotHitPlayer = true;

			var pc = lastShotHit.collider.GetComponent<PlayerController>();
			if (pc == null)
				pc = lastShotHit.collider.GetComponentInParent<PlayerController>();

			if (pc != null)
			{
				bool parried = pc.HandleIncomingAttack(lastShotHit.point);
				if (parried)
				{
					Debug.Log($"Parried: Enemy1 shot was deflected at {lastHitPoint}", this);
				}
				else
				{
					Debug.Log($"Damage Taken: Enemy1 hit player for {damageAmount} at {lastHitPoint}", this);
				}
			}
		}
		else
		{
			lastShotHitPlayer = false;
		}

		// Optional: visualize in editor while tuning
		// Debug.DrawLine(origin, origin + dir * rayDistance, lastShotHitPlayer ? Color.green : Color.red, 0.25f);
	}

	/// <summary>
	/// Returns the first "meaningful" hit in front of this enemy:
	/// - Ignores triggers
	/// - Ignores other enemies (anything with EnemyHealth in self/parent), so they don't block LoS
	/// - Stops at the first non-enemy solid collider (e.g. walls/ground) OR the player
	/// </summary>
	private RaycastHit2D GetFirstRelevantHit(Vector2 origin, Vector2 dir, float distance)
	{
		RaycastHit2D[] hits = Physics2D.RaycastAll(origin, dir, distance);
		if (hits == null || hits.Length == 0) return default;

		for (int i = 0; i < hits.Length; i++)
		{
			var h = hits[i];
			if (h.collider == null) continue;
			if (h.collider.isTrigger) continue;

			// Player always counts as a hit.
			if (h.collider.CompareTag(playerTag)) return h;

			// Treat other enemies as transparent for this hitscan.
			if (IsEnemyCollider(h.collider)) continue;

			// Any other collider blocks the shot (world geometry, props, etc).
			return h;
		}

		// Only triggers/enemies were hit -> no meaningful hit.
		return default;
	}

	private static bool IsEnemyCollider(Collider2D col)
	{
		if (col == null) return false;
		// Most robust for this project: enemies have EnemyHealth somewhere on their hierarchy.
		return col.GetComponent<EnemyHealth>() != null || col.GetComponentInParent<EnemyHealth>() != null;
	}

	private void SpawnMuzzleFlash()
	{
		if (muzzleFlashPrefab == null) return;

		Vector2 pos = (Vector2)transform.position + new Vector2(muzzleOffset.x * (facingRight ? 1f : -1f), muzzleOffset.y);
		ParticleSystem ps = Instantiate(muzzleFlashPrefab, pos, Quaternion.identity);
		ps.Play();
		Destroy(ps.gameObject, GetParticleLifetimeSeconds(ps));
	}

	private float GetParticleLifetimeSeconds(ParticleSystem ps)
	{
		if (ps == null) return 0.5f;
		var main = ps.main;
		float duration = main.duration;
		float lifetime = 0.5f;

		// Approximate max lifetime depending on startLifetime mode
		switch (main.startLifetime.mode)
		{
			case ParticleSystemCurveMode.Constant:
				lifetime = main.startLifetime.constant;
				break;
			case ParticleSystemCurveMode.TwoConstants:
				lifetime = main.startLifetime.constantMax;
				break;
			default:
				lifetime = 0.5f;
				break;
		}

		return Mathf.Max(0.25f, duration + lifetime);
	}

	private void SetAnimatorSpeed(float speed)
	{
		if (animator == null) return;

		// Only set if the parameter exists; avoids runtime errors on mismatched enemy Animator controllers.
		if (!AnimatorHasParameter(speedParamHash)) return;

		animator.SetFloat(speedParamHash, speed);
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

	private float RandomRange(Vector2 range)
	{
		return Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
	}
}

