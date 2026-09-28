using UnityEngine;

public class MouseLook : MonoBehaviour
{
    public float mouseSensitivity = 200f; // 마우스 감도
    public Transform playerBody; // 플레이어 몸통

    float xRotation = 0f;

    void Start()
    {
        // 게임 시작 시 마우스 커서를 화면 중앙에 숨기고 고정합니다. (슈팅게임처럼)
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // 마우스 움직임 값을 받아옵니다.
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // 위아래 고개 끄덕이기 (카메라만 위아래로 회전)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); // 목이 꺾이지 않게 위아래 각도 제한

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // 좌우 회전 (플레이어 몸통 전체를 좌우로 회전)
        playerBody.Rotate(Vector3.up * mouseX);
    }
}