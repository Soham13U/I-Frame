using UnityEngine;

/// <summary>
/// Smooth 2D camera follow with a slight delay (platformer-style).
/// If your camera is parented under the player, enable DetachOnStart so smoothing can work.
/// </summary>
public class CameraFollow2D : MonoBehaviour
{
	[Header("Target")]
	[SerializeField] private Transform target;
	[SerializeField] private bool detachOnStart = true;

	[Header("Follow")]
	[SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);
	[SerializeField] private float smoothTime = 0.12f;
	[SerializeField] private float maxSpeed = 50f;

	[Header("Look Ahead (Optional)")]
	[SerializeField] private bool useLookAhead = true;
	[SerializeField] private float lookAheadDistance = 1.0f;
	[SerializeField] private float lookAheadSmoothTime = 0.08f;

	private Vector3 velocity;
	private Vector3 lookAheadVelocity;
	private Vector3 currentLookAhead;
	private Vector3 lastTargetPos;

	private void Awake()
	{
		if (target == null)
		{
			GameObject player = GameObject.FindGameObjectWithTag("Player");
			if (player != null) target = player.transform;
		}

		if (detachOnStart && transform.parent != null)
			transform.SetParent(null, true);

		if (target != null) lastTargetPos = target.position;
	}

	private void LateUpdate()
	{
		if (target == null) return;

		Vector3 desired = target.position + offset;

		if (useLookAhead)
		{
			float dx = target.position.x - lastTargetPos.x;
			float desiredLookAheadX = Mathf.Clamp(dx * 10f, -lookAheadDistance, lookAheadDistance);
			Vector3 desiredLookAhead = new Vector3(desiredLookAheadX, 0f, 0f);
			currentLookAhead = Vector3.SmoothDamp(currentLookAhead, desiredLookAhead, ref lookAheadVelocity, lookAheadSmoothTime);
			desired += currentLookAhead;
		}

		Vector3 smoothPos = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime, maxSpeed, Time.deltaTime);

		// Apply shake AFTER follow so it never drifts away from the player.
		Vector3 shakeOffset = Vector3.zero;
		if (CameraShake2D.Instance != null)
			shakeOffset = CameraShake2D.Instance.CurrentOffset;

		transform.position = smoothPos + shakeOffset;
		lastTargetPos = target.position;
	}
}

