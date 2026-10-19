using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerInteraction : MonoBehaviour
{
    [Header("보유 아이템")]
    public bool hasSecurityCard = false;
    public bool hasBomb = false;

    [Header("줍기 설정")]
    [Min(0f)]
    public float pickupDuration = 1.5f;

    public bool IsLooting => isLooting;

    private Animator anim;
    private PlayerHealth health;
    private bool isLooting;

    private bool hasUseTrigger;
    private bool hasStopUseTrigger;

    private readonly Dictionary<Collider, GameObject> nearbyItems =
        new Dictionary<Collider, GameObject>();

    private readonly List<Collider> invalidColliders =
        new List<Collider>();

    private void Awake()
    {
        anim = GetComponent<Animator>();
        health = GetComponent<PlayerHealth>();

        // 해당 Trigger가 있을 때만 애니메이션을 호출합니다.
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter parameter in anim.parameters)
            {
                if (parameter.type != AnimatorControllerParameterType.Trigger)
                    continue;

                if (parameter.name == "Use")
                    hasUseTrigger = true;

                if (parameter.name == "StopUse")
                    hasStopUseTrigger = true;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        RegisterItem(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // 스크립트가 다시 활성화되었을 때도 주변 아이템을 찾습니다.
        RegisterItem(other);
    }

    private void RegisterItem(Collider other)
    {
        if (nearbyItems.ContainsKey(other))
            return;

        PickupItem pickup = other.GetComponentInParent<PickupItem>();

        if (pickup != null)
        {
            nearbyItems.Add(other, pickup.gameObject);
        }
        else if (other.CompareTag("Item"))
        {
            // 기존 보안 카드와 호환됩니다.
            nearbyItems.Add(other, other.gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        nearbyItems.Remove(other);
    }

    private bool CanInteract()
    {
        return Time.timeScale > 0f &&
               (health == null || !health.IsDead);
    }

    private void Update()
    {
        if (!CanInteract() || isLooting)
            return;

        if (!Input.GetKeyDown(KeyCode.F))
            return;

        GameObject item = FindNearestItem();

        if (item != null)
            StartCoroutine(PickupRoutine(item));
    }

    private void CleanNearbyItems()
    {
        invalidColliders.Clear();

        foreach (KeyValuePair<Collider, GameObject> entry in nearbyItems)
        {
            if (entry.Key == null ||
                !entry.Key.enabled ||
                !entry.Key.gameObject.activeInHierarchy ||
                entry.Value == null ||
                !entry.Value.activeInHierarchy)
            {
                invalidColliders.Add(entry.Key);
            }
        }

        foreach (Collider col in invalidColliders)
            nearbyItems.Remove(col);
    }

    private GameObject FindNearestItem()
    {
        CleanNearbyItems();

        GameObject nearest = null;
        float nearestDistance = float.PositiveInfinity;

        foreach (KeyValuePair<Collider, GameObject> entry in nearbyItems)
        {
            PickupItem pickup = entry.Value.GetComponent<PickupItem>();

            // 폭탄은 한 개만 소지합니다.
            if (pickup != null &&
                pickup.itemType == PickupItem.ItemType.Bomb &&
                hasBomb)
            {
                continue;
            }

            float distance = (
                entry.Key.ClosestPoint(transform.position) -
                transform.position
            ).sqrMagnitude;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = entry.Value;
            }
        }

        return nearest;
    }

    private bool IsItemNearby(GameObject item)
    {
        if (item == null)
            return false;

        CleanNearbyItems();

        foreach (GameObject nearby in nearbyItems.Values)
        {
            if (nearby == item)
                return true;
        }

        return false;
    }

    private IEnumerator PickupRoutine(GameObject item)
    {
        isLooting = true;

        if (anim != null && hasUseTrigger)
            anim.SetTrigger("Use");

        float elapsed = 0f;
        bool cancelled = false;

        while (elapsed < pickupDuration)
        {
            if ((health != null && health.IsDead) ||
                !IsItemNearby(item))
            {
                cancelled = true;
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!cancelled && CanInteract() && IsItemNearby(item))
        {
            PickupItem pickup = item.GetComponent<PickupItem>();

            if (pickup != null &&
                pickup.itemType == PickupItem.ItemType.Bomb)
            {
                hasBomb = true;
                Debug.Log("폭탄 획득! 막힌 잔해에 설치할 수 있습니다.", this);
            }
            else
            {
                hasSecurityCard = true;
                Debug.Log("보안 카드 획득! 보안 문을 열 수 있습니다.", this);
            }

            // 같은 프레임에 다시 선택되지 않도록 먼저 비활성화합니다.
            item.SetActive(false);
            Destroy(item);
        }

        FinishPickupAnimation();
        isLooting = false;
    }

    private void FinishPickupAnimation()
    {
        if (anim == null)
            return;

        if (hasUseTrigger)
            anim.ResetTrigger("Use");

        if (hasStopUseTrigger)
            anim.SetTrigger("StopUse");
    }

    // 다음 단계의 폭탄 설치 기능에서 호출합니다.
    public bool TryConsumeBomb()
    {
        if (!CanInteract() || isLooting || !hasBomb)
            return false;

        hasBomb = false;
        return true;
    }

    private void OnDisable()
    {
        StopAllCoroutines();

        if (isLooting)
            FinishPickupAnimation();

        isLooting = false;
        nearbyItems.Clear();
    }
}