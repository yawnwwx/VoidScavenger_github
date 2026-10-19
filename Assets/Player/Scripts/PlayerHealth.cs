using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    [Header("체력")]
    [Min(1)]
    public int maxHealth = 100;

    [SerializeField]
    private int currentHealth;

    [Header("체력 UI")]
    public Image healthFill;
    public TMP_Text healthText;

    [Header("피격 소리")]
    public AudioClip hitClip;

    [Range(0f, 1f)]
    public float hitVolume = 0.7f;

    [Tooltip("동시에 여러 공격을 맞아 소리가 과하게 겹치는 것을 방지합니다.")]
    [Min(0f)]
    public float hitSoundInterval = 0.08f;

    [Header("죽으면 끌 조작 스크립트")]
    public Behaviour[] controlsToDisable;

    public int CurrentHealth => currentHealth;
    public bool IsDead { get; private set; }

    private AudioSource hitSource;
    private float nextHitSoundTime;

    private void Awake()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = maxHealth;
        IsDead = false;

        // 총소리와 분리된 피격음 전용 AudioSource입니다.
        hitSource = gameObject.AddComponent<AudioSource>();
        hitSource.playOnAwake = false;
        hitSource.loop = false;
        hitSource.spatialBlend = 0f;
        hitSource.volume = 1f;
        hitSource.pitch = 1f;

        UpdateUI();
    }

    public void TakeDamage(int damage)
    {
        if (IsDead || damage <= 0 || Time.timeScale == 0f)
            return;

        currentHealth = Mathf.Max(0, currentHealth - damage);

        UpdateUI();
        PlayHitSound();

        Debug.Log(
            $"플레이어 피격! 체력: {currentHealth}/{maxHealth}",
            this
        );

        if (currentHealth == 0)
            Die();
    }

    private void PlayHitSound()
    {
        if (hitClip == null || hitSource == null)
            return;

        // 피해 자체는 모두 적용하고 소리의 겹침만 제한합니다.
        // 사망하는 마지막 타격음은 반드시 재생합니다.
        if (currentHealth > 0 && Time.time < nextHitSoundTime)
            return;

        nextHitSoundTime =
            Time.time + Mathf.Max(0f, hitSoundInterval);

        hitSource.PlayOneShot(hitClip, hitVolume);
    }

    private void UpdateUI()
    {
        if (healthFill != null)
        {
            healthFill.fillAmount =
                (float)currentHealth / Mathf.Max(1, maxHealth);
        }

        if (healthText != null)
            healthText.text = currentHealth.ToString();
    }

    private void Die()
    {
        IsDead = true;

        if (controlsToDisable != null)
        {
            foreach (Behaviour control in controlsToDisable)
            {
                if (control != null)
                    control.enabled = false;
            }
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("플레이어 사망! 임무 실패.", this);
    }
}