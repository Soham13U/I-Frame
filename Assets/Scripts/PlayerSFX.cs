using UnityEngine;

public class PlayerSFX : MonoBehaviour
{
	public static PlayerSFX Instance { get; private set; }

	[Header("Audio Source (Global 2D)")]
	[SerializeField] private AudioSource audioSource;

	[Header("Clips")]
	[SerializeField] private AudioClip[] swingClips;
	[SerializeField] private AudioClip[] hitClips;
	[SerializeField] private AudioClip[] parryClips;

	private bool impactPlayedThisFrame;

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}
		Instance = this;

		if (audioSource == null) audioSource = GetComponent<AudioSource>();
		if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
		audioSource.playOnAwake = false;
		audioSource.spatialBlend = 0f; // 2D
	}

	private void LateUpdate()
	{
		// Reset per-frame impact flag using unscaled timing
		impactPlayedThisFrame = false;
	}

	public void PlayWhooshIfNoImpactThisFrame(float volume = 1f, float pitchJitter = 0.05f)
	{
		if (impactPlayedThisFrame) return;
		if (swingClips == null || swingClips.Length == 0) return;
		if (audioSource == null) return;

		PlayOneOf(swingClips, volume, pitchJitter, preempt:false);
	}

	public void PlayHitImpact(float volume = 1f, float pitchJitter = 0.03f)
	{
		impactPlayedThisFrame = true;
		if (hitClips == null || hitClips.Length == 0) return;
		if (audioSource == null) return;

		PlayOneOf(hitClips, volume, pitchJitter, preempt:true);
	}

	public void PlayParryImpact(float volume = 1f, float pitchJitter = 0.03f)
	{
		impactPlayedThisFrame = true;
		if (parryClips == null || parryClips.Length == 0) return;
		if (audioSource == null) return;

		PlayOneOf(parryClips, volume, pitchJitter, preempt:true);
	}

	private void PlayOneOf(AudioClip[] clips, float volume, float pitchJitter, bool preempt)
	{
		if (clips == null || clips.Length == 0) return;
		AudioClip clip = clips[Random.Range(0, clips.Length)];
		if (clip == null) return;

		float basePitch = 1f;
		float jitter = Mathf.Clamp(pitchJitter, 0f, 0.5f);
		audioSource.pitch = basePitch + Random.Range(-jitter, jitter);

		if (preempt) audioSource.Stop();
		audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
	}
}

