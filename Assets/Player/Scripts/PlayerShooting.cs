using UnityEngine;

public class PlayerShooting : MonoBehaviour
{
    [Header("발사 설정")]
    public GameObject bulletPrefab; // 총알 프리팹
    public Transform firePoint;     // 총구 위치
    public float bulletSpeed = 50f; // 총알 속도

    private Animator anim;
    private Camera mainCam;

    void Start()
    {
        anim = GetComponent<Animator>();
        // 씬에 있는 메인 카메라를 자동으로 찾아옵니다.
        mainCam = Camera.main;
    }

    void Update()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            Shoot();
        }
    }

    void Shoot()
    {
        anim.SetTrigger("Shoot");

        // 1. 화면 정중앙(0.5, 0.5)에서 앞으로 뻗어나가는 가상의 레이저(Ray)를 만듭니다.
        Ray ray = mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;
        Vector3 targetPoint; // 총알이 날아갈 최종 목적지

        // 2. 레이저가 맵이나 적에 닿았다면 그곳을 목적지로, 허공이라면 아주 먼 곳을 목적지로 설정합니다.
        if (Physics.Raycast(ray, out hit))
        {
            targetPoint = hit.point;
        }
        else
        {
            targetPoint = ray.GetPoint(100);
        }

        // 3. 총구(FirePoint)에서 목적지(targetPoint)를 향하는 '방향'을 계산합니다.
        Vector3 direction = targetPoint - firePoint.position;

        // 4. 총알을 생성하고, 계산된 방향으로 날려보냅니다.
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        bullet.transform.forward = direction.normalized; // 총알의 머리를 목적지 방향으로 돌림

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = direction.normalized * bulletSpeed;
        }

        Destroy(bullet, 2f);
    }
}