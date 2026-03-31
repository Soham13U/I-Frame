using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
	[Header("Health")]
	[SerializeField] private int maxHp = 3;
	[SerializeField] private int hp = 3;
	[SerializeField] private float onDeathDestroyDelay = 0.15f;

	[Header("Hit Feedback")]
	[SerializeField] private float knockbackImpulse = 3f;
	[SerializeField] private float hitFlashSeconds = 0.06f;
	[SerializeField] private Color hitFlashColor = new Color(1f, 0.35f, 0.35f, 1f);

	[Header("Death FX (Optional)")]
	[SerializeField] private ParticleSystem deathParticlesPrefab;

	[Header("Optional References")]
	[SerializeField] private Rigidbody2D rb;
	[SerializeField] private SpriteRenderer spriteRenderer;
	[SerializeField] private Collider2D col;
	[SerializeField] private Behaviour aiToDisable; // e.g., Enemy1AI

	private Color originalColor;
	private bool hasOriginalColor;
	private bool dead;

	private void Awake()
	{
		if (rb == null) rb = GetComponent<Rigidbody2D>();
		if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
		if (col == null) col = GetComponent<Collider2D>();

		if (spriteRenderer != null)
		{
			originalColor = spriteRenderer.color;
			hasOriginalColor = true;
		}

		if (hp <= 0) hp = maxHp;
	}

	public void TakeDamage(int amount, Vector2 hitDir)
	{
		if (dead) return;
		if (amount <= 0) return;

		hp -= amount;
		if (hp < 0) hp = 0;

		// Hit feedback
		if (rb != null)
		{
			Vector2 dir = hitDir.sqrMagnitude > 0.0001f ? hitDir.normalized : Vector2.right;
			rb.AddForce(dir * knockbackImpulse, ForceMode2D.Impulse);
		}

		if (spriteRenderer != null)
		{
			spriteRenderer.color = hitFlashColor;
			CancelInvoke(nameof(ResetColor));
			Invoke(nameof(ResetColor), hitFlashSeconds);
		}

		if (hp <= 0)
			Die();
	}

	private void Die()
	{
		if (dead) return;
		dead = true;

		if (aiToDisable != null) aiToDisable.enabled = false;
		if (col != null) col.enabled = false;

		if (deathParticlesPrefab != null)
		{
			ParticleSystem ps = Instantiate(deathParticlesPrefab, transform.position, Quaternion.identity);
			ps.Play();
			Destroy(ps.gameObject, Mathf.Max(0.25f, ps.main.duration + ps.main.startLifetime.constantMax));
		}

		Destroy(gameObject, Mathf.Max(0f, onDeathDestroyDelay));
	}

	private void ResetColor()
	{
		if (spriteRenderer == null) return;
		if (!hasOriginalColor) return;
		spriteRenderer.color = originalColor;
	}
}

