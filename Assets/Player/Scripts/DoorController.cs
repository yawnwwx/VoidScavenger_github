using UnityEngine;
using System.Collections;

public class DoorController : MonoBehaviour
{
    [Header("완성형 문 설정")]
    public GameObject doorModel; // 움직일 실제 문 오브젝트
    public Vector3 openOffset = new Vector3(0, 4f, 0); // 문이 열리는 방향과 거리 (기본값: 위로 4칸)
    public float openSpeed = 2f; // 문이 열리는 속도

    private bool isOpen = false; // 문이 이미 열렸는지 확인

    void OnTriggerEnter(Collider other)
    {
        // 이미 문이 열린 상태라면 더 이상 스캔하지 않음
        if (isOpen) return;

        PlayerInteraction player = other.GetComponent<PlayerInteraction>();

        if (player != null && player.hasSecurityCard == true)
        {
            Debug.Log("보안 인증 완료. 코어룸 개방을 시작합니다.");
            isOpen = true; // 중복 실행 방지

            // 문이 부드럽게 열리는 연출 시작
            StartCoroutine(OpenDoorRoutine());
        }
        else if (player != null && player.hasSecurityCard == false)
        {
            Debug.Log("접근 거부: 보안 카드가 필요합니다.");
        }
    }

    IEnumerator OpenDoorRoutine()
    {
        // 문의 처음 위치와 최종 도착 위치를 계산합니다.
        Vector3 startPos = doorModel.transform.position;
        Vector3 endPos = startPos + openOffset;

        float percent = 0f;

        // percent가 1(100%)이 될 때까지 반복해서 문을 움직입니다.
        while (percent < 1f)
        {
            percent += Time.deltaTime * openSpeed;

            // Vector3.Lerp는 시작점과 끝점 사이를 부드럽게 이어주는 유니티의 강력한 이동 함수입니다.
            doorModel.transform.position = Vector3.Lerp(startPos, endPos, percent);

            yield return null; // 1프레임 대기 후 다시 이동 (자연스러운 슬라이딩 효과)
        }
    }
}