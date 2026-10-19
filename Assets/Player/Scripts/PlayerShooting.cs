using TMPro;
using UnityEngine;

public class PlayerShooting : MonoBehaviour
{
    [Header("기존 발사 설정")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float bulletSpeed = 50f;

    [Header("탄약 설정")]
    [Min(1)]
    public int magazineSize = 30;

    [Min(0)]
    public int startingReserveAmmo = 120;

    [Min(0.1f)]
    public float reloadDuration = 2f;

    public KeyCode reloadKey = KeyCode.R;

    [Header("탄약 UI")]
    public TMP_Text ammoText;
    public TMP_Text reserveAmmoText;
    public TMP_Text reloadText;

    [Header("발사 소리")]
    public AudioClip shootClip;

    [Range(0f, 1f)]
    public float shootVolume = 0.7f;

    [Header("재장전 소리")]
    public AudioClip reloadClip;

    [Range(0f, 1f)]
    public float reloadVolume = 0.6f;

    [Header("실행 중 탄약 확인")]
    [SerializeField]
    private int currentAmmo;

    [SerializeField]
    private int reserveAmmo;

    private Animator anim;
    private Camera mainCam;
    private PlayerHealth playerHealth;
    private PlayerMovement movement;

    private AudioSource shootSource;
    private AudioSource reloadSource;

    private bool isReloading;
    private float reloadTimer;
    private bool audioPaused;

    public int CurrentAmmo => currentAmmo;
    public int ReserveAmmo => reserveAmmo;
    public bool IsReloading => isReloading;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        playerHealth = GetComponent<PlayerHealth>();
        movement = GetComponent<PlayerMovement>();

        magazineSize = Mathf.Max(1, magazineSize);
        currentAmmo = magazineSize;
        reserveAmmo = Mathf.Max(0, startingReserveAmmo);

        // 발사음과 장전음을 따로 관리합니다.
        // 장전 취소 시 발사음까지 끊기지 않습니다.
        shootSource = CreateAudioSource();
        reloadSource = CreateAudioSource();

        UpdateAmmoUI();
    }

    private AudioSource CreateAudioSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = false;

        // 자신의 무기 소리는 카메라 거리와 관계없이 들립니다.
        source.spatialBlend = 0f;
        source.volume = 1f;
        source.pitch = 1f;

        return source;
    }

    private void Start()
    {
        mainCam = Camera.main;

        if (bulletPrefab == null ||
            firePoint == null ||
            mainCam == null)
        {
            Debug.LogError(
                "PlayerShooting: Bullet Prefab, Fire Point와 " +
                "MainCamera 태그가 있는 카메라를 확인해 주세요.",
                this
            );

            enabled = false;
            return;
        }

        Rigidbody bulletBody = bulletPrefab.GetComponent<Rigidbody>();

        if (bulletBody == null || bulletBody.isKinematic)
        {
            Debug.LogError(
                "PlayerShooting: 총알 프리팹에 Rigidbody가 필요하며 " +
                "Is Kinematic은 꺼져 있어야 합니다.",
                this
            );

            enabled = false;
            return;
        }

        SetReloadAnimation(false);
    }

    private void Update()
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            CancelReload();
            StopWeaponAudio();
            return;
        }

        UpdateAudioPause();

        if (Time.timeScale == 0f)
            return;

        // 구르면 재장전을 취소하고 사격도 막습니다.
        if (movement != null && movement.IsDodging)
        {
            CancelReload();
            ResetShootTrigger();
            return;
        }

        if (isReloading)
        {
            reloadTimer -= Time.deltaTime;

            if (reloadTimer <= 0f)
                FinishReload();

            return;
        }

        // 메뉴 등을 조작하는 중에는 사격하지 않습니다.
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        if (Input.GetKeyDown(reloadKey))
        {
            TryStartReload();

            if (isReloading)
                return;
        }

        // 왼쪽 Ctrl이 Fire1에 등록되어 있어도 발사되지 않도록
        // 마우스 왼쪽 버튼을 직접 확인합니다.
        if (Input.GetMouseButtonDown(0))
            Shoot();
    }

    private void Shoot()
    {
        if (isReloading || currentAmmo <= 0)
            return;

        if (movement != null && movement.IsDodging)
            return;

        if (mainCam == null ||
            bulletPrefab == null ||
            firePoint == null)
        {
            return;
        }

        Ray ray = mainCam.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        Vector3 targetPoint;

        if (Physics.Raycast(ray, out RaycastHit hit))
            targetPoint = hit.point;
        else
            targetPoint = ray.GetPoint(100f);

        Vector3 direction = targetPoint - firePoint.position;

        if (direction.sqrMagnitude < 0.0001f)
            direction = firePoint.forward;

        direction.Normalize();

        GameObject bullet = Instantiate(
            bulletPrefab,
            firePoint.position,
            Quaternion.LookRotation(direction)
        );

        Rigidbody rb = bullet.GetComponent<Rigidbody>();

        if (rb != null)
            rb.linearVelocity = direction * bulletSpeed;

        if (anim != null &&
            anim.runtimeAnimatorController != null)
        {
            anim.ResetTrigger("Shoot");
            anim.SetTrigger("Shoot");
        }

        if (shootClip != null && shootSource != null)
            shootSource.PlayOneShot(shootClip, shootVolume);

        currentAmmo--;
        UpdateAmmoUI();

        Destroy(bullet, 2f);
    }

    private void TryStartReload()
    {
        if (isReloading ||
            currentAmmo >= magazineSize ||
            reserveAmmo <= 0)
        {
            return;
        }

        if (movement != null && movement.IsDodging)
            return;

        isReloading = true;
        reloadTimer = Mathf.Max(0.1f, reloadDuration);

        ResetShootTrigger();
        SetReloadAnimation(true);

        if (reloadSource != null)
        {
            reloadSource.Stop();
            reloadSource.clip = reloadClip;
            reloadSource.volume = reloadVolume;

            if (reloadClip != null)
                reloadSource.Play();
        }

        UpdateAmmoUI();
    }

    private void FinishReload()
    {
        int neededAmmo = magazineSize - currentAmmo;
        int ammoToLoad = Mathf.Min(neededAmmo, reserveAmmo);

        currentAmmo += ammoToLoad;
        reserveAmmo -= ammoToLoad;

        isReloading = false;
        reloadTimer = 0f;

        SetReloadAnimation(false);

        if (reloadSource != null)
            reloadSource.Stop();

        UpdateAmmoUI();
    }

    private void CancelReload()
    {
        isReloading = false;
        reloadTimer = 0f;

        SetReloadAnimation(false);

        if (reloadSource != null)
            reloadSource.Stop();

        UpdateAmmoUI();
    }

    private void SetReloadAnimation(bool reloading)
    {
        if (anim != null &&
            anim.runtimeAnimatorController != null)
        {
            anim.SetBool("Reloading", reloading);
        }
    }

    private void ResetShootTrigger()
    {
        if (anim != null &&
            anim.runtimeAnimatorController != null)
        {
            anim.ResetTrigger("Shoot");
        }
    }

    private void UpdateAudioPause()
    {
        bool shouldPause = Time.timeScale == 0f;

        if (audioPaused == shouldPause)
            return;

        audioPaused = shouldPause;

        if (audioPaused)
        {
            if (shootSource != null)
                shootSource.Pause();

            if (reloadSource != null)
                reloadSource.Pause();
        }
        else
        {
            if (shootSource != null)
                shootSource.UnPause();

            if (reloadSource != null)
                reloadSource.UnPause();
        }
    }

    private void StopWeaponAudio()
    {
        if (shootSource != null)
            shootSource.Stop();

        if (reloadSource != null)
            reloadSource.Stop();

        audioPaused = false;
    }

    public void AddAmmo(int amount)
    {
        if (amount <= 0)
            return;

        reserveAmmo += amount;
        UpdateAmmoUI();
    }

    private void UpdateAmmoUI()
    {
        if (ammoText != null)
        {
            ammoText.text = reserveAmmoText != null
                ? currentAmmo.ToString()
                : $"{currentAmmo} / {reserveAmmo}";
        }

        if (reserveAmmoText != null)
            reserveAmmoText.text = reserveAmmo.ToString();

        if (reloadText != null)
        {
            if (isReloading)
                reloadText.text = "RELOADING...";
            else if (currentAmmo == 0 && reserveAmmo > 0)
                reloadText.text = "[R] RELOAD";
            else if (currentAmmo == 0 && reserveAmmo == 0)
                reloadText.text = "NO AMMO";
            else
                reloadText.text = "";
        }
    }

    private void OnDisable()
    {
        CancelReload();
        ResetShootTrigger();
        StopWeaponAudio();
    }
}