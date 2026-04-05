using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles throw / recall / teleport for a single dagger.
/// - Disallows throw if a dagger is active (must Recall first).
/// - Teleport finds a safe landing near the stuck point by overlap checks.
/// </summary>
public class PlayerTeleportController : MonoBehaviour
{
	[Header("Input")]
	[SerializeField] private InputActionReference throwAction;   // e.g., RightClick
	[SerializeField] private InputActionReference recallAction;  // e.g., R
	[SerializeField] private InputActionReference teleportAction; // e.g., E

	[Header("References")]
	[SerializeField] private GameObject daggerPrefab;
	[SerializeField] private Rigidbody2D playerRb;
	[SerializeField] private Collider2D playerCollider;
	[SerializeField] private CrosshairController crosshair;

	[Header("Spawn")]
	[SerializeField] private Vector2 spawnOffset = new Vector2(0.6f, 0.0f);

	[Header("Ground & Safe Teleport")]
	[SerializeField] private LayerMask groundLayer;
	[SerializeField] private LayerMask blockingLayers; // usually Ground + other solids
	[SerializeField] private Vector2 safeCapsuleSize = new Vector2(0.8f, 1.8f);
	[SerializeField] private float safeSearchRadius = 0.3f;
	[SerializeField] private int safeSearchRings = 3;
	[SerializeField] private bool resetVelocityOnTeleport = true;

	private TeleportDagger activeDagger;
	private bool facingRight = true;

	private void Awake()
	{
		if (playerRb == null) playerRb = GetComponent<Rigidbody2D>();
		if (playerCollider == null) playerCollider = GetComponent<Collider2D>();
	}

	private void OnEnable()
	{
		if (throwAction != null) throwAction.action.Enable();
		if (recallAction != null) recallAction.action.Enable();
		if (teleportAction != null) teleportAction.action.Enable();
	}

	private void OnDisable()
	{
		if (throwAction != null) throwAction.action.Disable();
		if (recallAction != null) recallAction.action.Disable();
		if (teleportAction != null) teleportAction.action.Disable();
	}

	private void Update()
	{
		UpdateFacing();

		if (throwAction != null && throwAction.action.triggered)
			TryThrow();

		if (recallAction != null && recallAction.action.triggered)
			Recall();

		if (teleportAction != null && teleportAction.action.triggered)
			TeleportToDagger();
	}

	private void UpdateFacing()
	{
		// Derive facing from scale or velocity; here we infer from local scale X if available.
		facingRight = transform.localScale.x >= 0f;
	}

	private void TryThrow()
	{
		// Disallow if dagger active
		if (activeDagger != null) return;
		if (daggerPrefab == null) return;

		// Aim toward crosshair if present
		Vector2 dir;
		Vector2 playerPos = transform.position;
		if (crosshair != null)
		{
			Vector2 aim = crosshair.GetWorldPosition();
			dir = (aim - playerPos).normalized;
			if (dir.sqrMagnitude < 0.0001f) dir = facingRight ? Vector2.right : Vector2.left;
		}
		else
		{
			dir = facingRight ? Vector2.right : Vector2.left;
		}

		// Compute spawn in front along aim direction
		float forward = Mathf.Abs(spawnOffset.x);
		Vector2 pos = playerPos + dir * forward + Vector2.up * spawnOffset.y;

		GameObject go = Instantiate(daggerPrefab, pos, Quaternion.identity);
		activeDagger = go.GetComponent<TeleportDagger>();
		if (activeDagger == null) activeDagger = go.AddComponent<TeleportDagger>();

		// Ignore collision with the player
		if (activeDagger.Collider != null && playerCollider != null)
		{
			Physics2D.IgnoreCollision(activeDagger.Collider, playerCollider, true);
		}

		activeDagger.Launch(dir);
	}

	private void Recall()
	{
		if (activeDagger == null) return;
		Destroy(activeDagger.gameObject);
		activeDagger = null;
	}

	private void TeleportToDagger()
	{
		if (activeDagger == null) return;
		if (!activeDagger.IsStuck) return; // only teleport to stuck dagger

		Vector2 target = activeDagger.StuckPosition;

		// Compute a safe position around the target (avoid embedding)
		Vector2 safe = FindSafeTeleportPosition(target, activeDagger.LastHitNormal);
		if (resetVelocityOnTeleport && playerRb != null) playerRb.linearVelocity = Vector2.zero;
		transform.position = safe;
	}

	private Vector2 FindSafeTeleportPosition(Vector2 center, Vector2 hitNormal)
	{
		// Start at dagger point + small lift along normal
		Vector2 basePos = center + hitNormal.normalized * 0.05f;
		if (!IsBlocked(basePos)) return basePos;

		// Search rings around basePos
		float step = safeSearchRadius / Mathf.Max(1, safeSearchRings);
		for (int ring = 1; ring <= safeSearchRings; ring++)
		{
			float r = ring * step;
			const int samples = 12;
			for (int i = 0; i < samples; i++)
			{
				float a = (Mathf.PI * 2f) * (i / (float)samples);
				Vector2 p = basePos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
				if (!IsBlocked(p)) return p;
			}
		}

		// Clamp: find nearest free direction by casting small increments upward then outwards
		for (int i = 1; i <= safeSearchRings * 2; i++)
		{
			Vector2 p = basePos + Vector2.up * (i * 0.05f);
			if (!IsBlocked(p)) return p;
		}

		// As last resort, return original (may overlap)
		return basePos;
	}

	private bool IsBlocked(Vector2 pos)
	{
		// Use capsule overlap roughly matching player
		// If playerCollider exists and is a Capsule/Box, we could mirror size; for now use safeCapsuleSize.
		var hit = Physics2D.OverlapBox(pos, safeCapsuleSize, 0f, blockingLayers);
		return hit != null;
	}

	private void OnDrawGizmosSelected()
	{
		// Visualize spawn offset
		Gizmos.color = Color.yellow;
		Vector2 s = (Vector2)transform.position + new Vector2(spawnOffset.x * (transform.localScale.x >= 0f ? 1f : -1f), spawnOffset.y);
		Gizmos.DrawWireSphere(s, 0.08f);

		// Visualize safe capsule
		Gizmos.color = Color.magenta;
		Gizmos.DrawWireCube(transform.position, safeCapsuleSize);
	}
}

