using UnityEngine;

public class CameraShake2D : MonoBehaviour
{
	public static CameraShake2D Instance { get; private set; }

	[SerializeField] private float defaultAmplitude = 0.10f;
	[SerializeField] private float defaultDuration = 0.10f;

	private float timer;
	private float amplitude;
	private float duration;

	public Vector3 CurrentOffset { get; private set; }

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(this);
			return;
		}
		Instance = this;
		CurrentOffset = Vector3.zero;
	}

	private void OnDestroy()
	{
		if (Instance == this) Instance = null;
	}

	public void Shake(float amplitude, float duration)
	{
		this.amplitude = Mathf.Max(0f, amplitude);
		this.duration = Mathf.Max(0.0001f, duration);
		timer = this.duration;
	}

	public void ShakeDefault()
	{
		Shake(defaultAmplitude, defaultDuration);
	}

	private void LateUpdate()
	{
		if (timer <= 0f)
		{
			CurrentOffset = Vector3.zero;
			return;
		}

		timer -= Time.unscaledDeltaTime;

		// Slight decay towards end
		float t = Mathf.Clamp01(timer / duration);
		float amp = amplitude * t;

		float x = (Random.value * 2f - 1f) * amp;
		float y = (Random.value * 2f - 1f) * amp;
		CurrentOffset = new Vector3(x, y, 0f);
	}
}

