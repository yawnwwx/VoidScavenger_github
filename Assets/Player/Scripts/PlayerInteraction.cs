using UnityEngine;
using System.Collections;

public class PlayerInteraction : MonoBehaviour
{
    private GameObject targetItem;
    private Animator anim;
    private bool isLooting = false;

    // [추가] 보안 카드를 가지고 있는지 기억하는 변수 (기본값: false)
    public bool hasSecurityCard = false;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Item"))
        {
            targetItem = other.gameObject;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Item"))
        {
            targetItem = null;
        }
    }

    void Update()
    {
        if (targetItem != null && Input.GetKeyDown(KeyCode.F) && !isLooting)
        {
            StartCoroutine(PickupRoutine());
        }
    }

    IEnumerator PickupRoutine()
    {
        isLooting = true;
        anim.SetTrigger("Use");
        yield return new WaitForSeconds(1.5f);

        if (targetItem != null)
        {
            // [추가] 카드를 파괴하기 직전에, 카드를 얻었다고 내 머릿속에 체크합니다!
            hasSecurityCard = true;
            Debug.Log("보안 카드 획득! 이제 문을 열 수 있습니다.");

            Destroy(targetItem);
            targetItem = null;
        }

        anim.SetTrigger("StopUse");
        isLooting = false;
    }
}