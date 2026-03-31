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
	[SerializeField] private Vector2 rayOriginOffset = new Vector2(0.25f, 0f);
	[SerializeField] private float shootCooldown = 0.8f;
	[SerializeField] private int damageAmount = 1;
	[SerializeField] private ParticleSystem muzzleFlashPrefab;
	[SerializeField] private Vector2 muzzleOffset = new Vector2(0.4f, 0f);

	[Header("Animator Params")]
	[SerializeField] private string speedParam = "Speed";
	[SerializeField] private string shootTrigger = "Shoot";

	// Cached hashes
	private int speedParamHash;
	private int shootTriggerHash; // unused; triggers are set by string/hash, but keep for flexibility

	private float desiredVelX;
	private float shootCooldownTimer;
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

		// Cooldown tick
		if (shootCooldownTimer > 0f)
			shootCooldownTimer -= Time.deltaTime;

		// Choose behavior by range
		if (dist > chaseRange)
		{
			// Idle
			desiredVelX = 0f;
			SetAnimatorSpeed(0f);
		}
		else if (dist > shootRange)
		{
			// Chase a bit
			float dir = facingRight ? 1f : -1f;
			desiredVelX = dir * runSpeed;
			SetAnimatorSpeed(Mathf.Abs(desiredVelX));
		}
		else
		{
			// Shoot window
			desiredVelX = 0f;
			SetAnimatorSpeed(0f);

			if (shootCooldownTimer <= 0f)
			{
				shootCooldownTimer = shootCooldown;
				pendingRaycast = true;
				FireShootAnimation();
			}
		}
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

	private void PerformRaycastShot()
	{
		// Raycast in facing direction. No projectile is spawned.
		Vector2 origin = (Vector2)transform.position + new Vector2(rayOriginOffset.x * (facingRight ? 1f : -1f), rayOriginOffset.y);
		Vector2 dir = facingRight ? Vector2.right : Vector2.left;

		lastShotHit = Physics2D.Raycast(origin, dir, rayDistance);
		lastHitPoint = lastShotHit.point;

		if (lastShotHit.collider != null && lastShotHit.collider.CompareTag(playerTag))
		{
			lastShotHitPlayer = true;

			var pc = lastShotHit.collider.GetComponent<PlayerController>();
			if (pc == null)
				pc = lastShotHit.collider.GetComponentInParent<PlayerController>();

			if (pc != null && pc.IsParrying())
			{
				Debug.Log($"Parried: Enemy1 shot was deflected at {lastHitPoint}", this);
				if (pc != null) pc.SpawnParrySpark();
				if (TimeEffects.Instance != null) TimeEffects.Instance.ParrySlowMo();
				if (CameraShake2D.Instance != null) CameraShake2D.Instance.ShakeDefault();
			}
			else
			{
				if (pc != null)
					pc.TakeDamage(damageAmount);
				Debug.Log($"Damage Taken: Enemy1 hit player for {damageAmount} at {lastHitPoint}", this);
			}
		}
		else
		{
			lastShotHitPlayer = false;
		}

		// Optional: visualize in editor while tuning
		// Debug.DrawLine(origin, origin + dir * rayDistance, lastShotHitPlayer ? Color.green : Color.red, 0.25f);
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
}

