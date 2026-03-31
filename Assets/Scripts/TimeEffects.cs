using UnityEngine;

public class TimeEffects : MonoBehaviour
{
	public static TimeEffects Instance { get; private set; }

	[Header("Parry SlowMo")]
	[SerializeField] private float slowmoScale = 0.5f;
	[SerializeField] private float holdUnscaledSeconds = 0.08f;
	[SerializeField] private float easeBackSeconds = 0.10f;
	[SerializeField] private bool affectAudioPitch = true;
	[SerializeField] private bool includeInactiveAudioSources = false;

	private float lastAppliedAudioPitch = 1f;

	private enum State { None, Hold, EaseBack }
	private State state;
	private float timer;

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}
		Instance = this;
	}

	private void OnDestroy()
	{
		if (Instance == this) Instance = null;
		ResetTime();
	}

	public void ParrySlowMo()
	{
		// Restart policy: always restart effect timing.
		state = State.Hold;
		timer = holdUnscaledSeconds;

		ApplyTimeScale(slowmoScale);
	}

	private void Update()
	{
		if (state == State.None) return;

		float dt = Time.unscaledDeltaTime;
		timer -= dt;

		if (state == State.Hold)
		{
			if (timer <= 0f)
			{
				state = State.EaseBack;
				timer = Mathf.Max(0.0001f, easeBackSeconds);
			}
		}
		else if (state == State.EaseBack)
		{
			float t = 1f - Mathf.Clamp01(timer / Mathf.Max(0.0001f, easeBackSeconds));
			float eased = Mathf.Lerp(slowmoScale, 1f, t);
			ApplyTimeScale(eased);

			if (timer <= 0f)
			{
				state = State.None;
				ResetTime();
			}
		}
	}

	private void ApplyTimeScale(float scale)
	{
		scale = Mathf.Clamp(scale, 0.01f, 1f);
		Time.timeScale = scale;
		Time.fixedDeltaTime = 0.02f * Time.timeScale;
		if (affectAudioPitch) ApplyGlobalAudioPitch(Time.timeScale);
	}

	private void ResetTime()
	{
		Time.timeScale = 1f;
		Time.fixedDeltaTime = 0.02f;
		if (affectAudioPitch) ApplyGlobalAudioPitch(1f);
	}

	private void ApplyGlobalAudioPitch(float pitch)
	{
		pitch = Mathf.Clamp(pitch, 0.01f, 3f);
		if (Mathf.Approximately(pitch, lastAppliedAudioPitch)) return;
		lastAppliedAudioPitch = pitch;

		AudioSource[] sources;
#if UNITY_2023_1_OR_NEWER
		sources = Object.FindObjectsByType<AudioSource>(includeInactiveAudioSources ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
		sources = Object.FindObjectsOfType<AudioSource>(includeInactiveAudioSources);
#endif

		for (int i = 0; i < sources.Length; i++)
		{
			if (sources[i] == null) continue;
			sources[i].pitch = pitch;
		}
	}
}

