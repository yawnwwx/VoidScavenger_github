using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private Animator anim;
    private Vector3 velocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        // 바닥 상태 체크 (중력 누적 방지)
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // 키보드 움직임 입력 (A,D = x / W,S = z)
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        // 실제 이동 처리
        Vector3 move = transform.right * x + transform.forward * z;
        controller.Move(move * speed * Time.deltaTime);

        // --- [애니메이션 연동] ---
        // 에셋 제작자가 만들어둔 파라미터(X, Y, Speed)에 값을 전달합니다.

        // 1. 좌우 걷기 (A, D 키 입력값)
        anim.SetFloat("X", x);

        // 2. 앞뒤 걷기 (W, S 키 입력값)
        anim.SetFloat("Y", z);

        // 3. 전체 이동 속도 (대각선 이동 등 벡터의 길이)
        anim.SetFloat("Speed", move.magnitude);
        // -------------------------

        // 중력 적용
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}