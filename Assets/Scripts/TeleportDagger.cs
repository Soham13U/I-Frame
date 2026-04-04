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

	[Header("Stick")]
	[SerializeField] private LayerMask groundLayer;
	[SerializeField] private float stickNormalOffset = 0.02f;
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

	private void Awake()
	{
		if (rb == null) rb = GetComponent<Rigidbody2D>();
		if (col == null) col = GetComponent<Collider2D>();
		if (rb != null)
		{
			rb.gravityScale = 0f;
			rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
		}
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
	}

	private void Update()
	{
		if (stuck) return;
		lifeTimer -= Time.deltaTime;
		if (lifeTimer <= 0f)
		{
			// Timeout: stop and stick-in-place so teleport remains consistent,
			// or destroy if preferred. We'll stick-in-place here.
			Stick(transform.position, Vector2.up, null);
		}
	}

	private void OnCollisionEnter2D(Collision2D collision)
	{
		if (stuck) return;
		// Only stick to Ground layer
		if (((1 << collision.collider.gameObject.layer) & groundLayer) == 0) return;

		Vector2 hitPoint = collision.GetContact(0).point;
		Vector2 hitNormal = collision.GetContact(0).normal;
		Stick(hitPoint, hitNormal, collision.collider.transform);
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
		if (childToHit && hitTransform != null)
		{
			transform.SetParent(hitTransform, true);
		}
	}
}

