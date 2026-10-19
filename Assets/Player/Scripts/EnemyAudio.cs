using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(EnemyMeleeAttack))]
public class EnemyAudio : MonoBehaviour
{
    [Header("소리 파일")]
    public AudioClip idleClip;
    public AudioClip attackClip;
    public AudioClip deathClip;

    [Header("소리 크기")]
    [Range(0f, 1f)]
    public float idleVolume = 0.35f;

    [Range(0f, 1f)]
    public float attackVolume = 0.7f;

    [Range(0f, 1f)]
    public float deathVolume = 0.8f;

    [Header("평상시 울음 간격")]
    [Min(0.1f)]
    public float minimumIdleInterval = 5f;

    [Min(0.1f)]
    public float maximumIdleInterval = 10f;

    [Header("소리가 들리는 거리")]
    [Min(0.1f)]
    public float minDistance = 2f;

    [Min(0.2f)]
    public float maxDistance = 20f;

    private EnemyHealth health;
    private EnemyMeleeAttack melee;
    private AudioSource audioSource;

    private float nextIdleTime;
    private bool wasAttacking;
    private bool deathPlayed;
    private bool audioPaused;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        melee = GetComponent<EnemyMeleeAttack>();

        audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.pitch = 1f;

        // 적의 위치에서 들리는 입체 음향입니다.
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;

        audioSource.minDistance = Mathf.Max(0.1f, minDistance);
        audioSource.maxDistance = Mathf.Max(
            audioSource.minDistance + 0.1f,
            maxDistance
        );

        // 이동 속도에 따라 음높이가 변하지 않게 합니다.
        audioSource.dopplerLevel = 0f;
    }

    private void OnEnable()
    {
        ScheduleNextIdle();
    }

    private void LateUpdate()
    {
        UpdateAudioPause();

        if (Time.timeScale == 0f)
            return;

        if (health.IsDead)
        {
            if (!deathPlayed)
            {
                deathPlayed = true;
                PlayClip(deathClip, deathVolume);
            }

            return;
        }

        bool attacking =
            melee.isActiveAndEnabled && melee.IsAttacking;

        // 공격 시작 순간에 한 번만 재생합니다.
        if (attacking && !wasAttacking)
        {
            PlayClip(attackClip, attackVolume);
            ScheduleNextIdle();
        }

        wasAttacking = attacking;

        if (attacking || audioSource.isPlaying)
            return;

        if (Time.time >= nextIdleTime)
        {
            PlayClip(idleClip, idleVolume);
            ScheduleNextIdle();
        }
    }

    private void PlayClip(AudioClip clip, float volume)
    {
        // 사망 또는 새 공격 시 이전 울음을 끊습니다.
        audioSource.Stop();

        if (clip == null)
            return;

        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.Play();
    }

    private void ScheduleNextIdle()
    {
        float minimum = Mathf.Max(0.1f, minimumIdleInterval);
        float maximum = Mathf.Max(minimum, maximumIdleInterval);

        nextIdleTime = Time.time + Random.Range(minimum, maximum);
    }

    private void UpdateAudioPause()
    {
        bool shouldPause = Time.timeScale == 0f;

        if (audioPaused == shouldPause)
            return;

        audioPaused = shouldPause;

        if (audioPaused)
            audioSource.Pause();
        else
            audioSource.UnPause();
    }

    private void OnDisable()
    {
        if (audioSource != null)
            audioSource.Stop();

        audioPaused = false;
        wasAttacking = false;
    }
}