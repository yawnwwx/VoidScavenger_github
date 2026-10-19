using UnityEngine;

public class PlayerAnimationEvents : MonoBehaviour
{
    [Header("구르기 소리 - 없으면 비워 두세요")]
    public AudioSource audioSource;
    public AudioClip rollClip;

    [Range(0f, 1f)]
    public float rollVolume = 0.7f;

    // Roll 애니메이션에서 호출합니다.
    public void RollSound()
    {
        if (audioSource != null && rollClip != null)
        {
            audioSource.PlayOneShot(rollClip, rollVolume);
        }
    }

    // 기존 에셋의 회전 제어 이벤트를 받습니다.
    // 현재 회피 방향은 PlayerMovement에서 처리합니다.
    public void CantRotate()
    {
    }

    // 기존 에셋의 구르기 종료 이벤트를 받습니다.
    // 현재 회피 종료는 PlayerMovement의 타이머가 처리합니다.
    public void EndRoll()
    {
    }
}