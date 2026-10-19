using UnityEngine;

public class PickupItem : MonoBehaviour
{
    public enum ItemType
    {
        SecurityCard,
        Bomb
    }

    [Header("아이템 종류")]
    public ItemType itemType = ItemType.SecurityCard;
}