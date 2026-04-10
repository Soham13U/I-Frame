using UnityEngine;

/// <summary>
/// Simple straight-flying dagger that sticks to Ground on first contact.
/// - Uses Rigidbody2D with gravityScale=0 and continuous collision for reliability.
/// - Only sticks to colliders on the Ground layer mask.
/// </summary>
public class TeleportDagger : MonoBehaviour
{
	[Header("Motion")]
	[SerializeField] private float speed = 18f;
	[SerializeField] private float maxFlightTime = 3.0f;
	[SerializeField] private float spinSpeedDegPerSec = 720f;

	[Header("Stick")]
	[SerializeField] private LayerMask groundLayer;
	[SerializeField] private float stickNormalOffset = 0.06f;
	[SerializeField] private bool childToHit = true;

	[Header("References")]
	[SerializeField] private Rigidbody2D rb;
	[SerializeField] private Collider2D col;

	private float lifeTimer;
	private bool stuck;
	private Vector2 lastHitNormal = Vector2.up;

	public bool IsStuck => stuck;
	public Vector3 StuckPosition => transform.position;
	public Vector2 LastHitNormal => lastHitNormal;
	public Collider2D Collider => col;

	private void Awake()
	{
		if (rb == null) rb = GetComponent<Rigidbody2D>();
		if (col == null) col = GetComponent<Collider2D>();
		if (rb != null)
		{
			rb.gravityScale = 0f;
			rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
		}
		// Use trigger so it doesn't physically collide with player/enemies
		if (col != null) col.isTrigger = true;
	}

	public void Launch(Vector2 direction)
	{
		stuck = false;
		lifeTimer = maxFlightTime;
		lastHitNormal = Vector2.up;
		if (rb != null)
		{
			rb.isKinematic = false;
			rb.linearVelocity = direction.normalized * speed;
		}
		// Face flight direction initially (optional)
		if (direction.sqrMagnitude > 0.0001f)
		{
			float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
			transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
		}
	}

	private void Update()
	{
		if (!stuck)
		{
			// Spin while flying
			transform.Rotate(0f, 0f, -spinSpeedDegPerSec * Time.deltaTime, Space.Self);
		}
		else
		{
			return;
		}
		lifeTimer -= Time.deltaTime;
		if (lifeTimer <= 0f)
		{
			// Timeout: stop and stick-in-place so teleport remains consistent,
			// or destroy if preferred. We'll stick-in-place here.
			Stick(transform.position, Vector2.up, null);
		}
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		if (stuck) return;
		// Only react to Ground layer
		if (((1 << other.gameObject.layer) & groundLayer) == 0) return;

		Vector2 pos = rb != null ? rb.position : (Vector2)transform.position;
		Vector2 surfacePoint = other.ClosestPoint(pos);
		Vector2 fromSurfaceToDagger = pos - surfacePoint;
		bool daggerInsideSolid = other.OverlapPoint(pos);

		Vector2 normal;
		// When already inside the collider, (pos - closestPoint) often points deeper into the tile, not out.
		if (!daggerInsideSolid && fromSurfaceToDagger.sqrMagnitude > 0.0004f)
		{
			normal = fromSurfaceToDagger.normalized;
		}
		else
		{
			// Interior / degenerate overlap: recover contact from outside
			RaycastHit2D probe = Physics2D.Raycast(pos + Vector2.up * 4f, Vector2.down, 8f, groundLayer);
			if (probe.collider != null)
			{
				normal = probe.normal;
				surfacePoint = probe.point;
			}
			else
			{
				Vector2 dir = rb != null && rb.linearVelocity.sqrMagnitude > 0.0001f
					? rb.linearVelocity.normalized
					: (Vector2)transform.right;
				RaycastHit2D hit = Physics2D.Raycast(pos - dir * 0.35f, dir, 0.7f, groundLayer);
				if (hit.collider != null)
				{
					normal = hit.normal;
					surfacePoint = hit.point;
				}
				else
				{
					normal = Vector2.up;
				}
			}
		}

		// Snap normal/point from a cast into the collider when the path is unambiguous
		RaycastHit2D refine = Physics2D.Raycast(pos - normal * 0.25f, normal, 0.5f, groundLayer);
		if (refine.collider == other)
		{
			normal = refine.normal;
			surfacePoint = refine.point;
		}

		Stick(surfacePoint, normal, other.transform);
	}

	private void Stick(Vector2 hitPoint, Vector2 hitNormal, Transform hitTransform)
	{
		stuck = true;
		lastHitNormal = hitNormal;
		if (rb != null)
		{
			rb.linearVelocity = Vector2.zero;
			rb.isKinematic = true;
		}

		// Place dagger slightly embedded along normal for a nice look
		transform.position = hitPoint + hitNormal * stickNormalOffset;
		// Align to surface normal on stick
		float angle = Mathf.Atan2(hitNormal.y, hitNormal.x) * Mathf.Rad2Deg;
		transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
		if (childToHit && hitTransform != null)
		{
			transform.SetParent(hitTransform, true);
		}
	}
}

