using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public int hp = 3; // 몹의 체력 (총알 3대 맞으면 죽음)

    // 데미지를 입었을 때 실행되는 기능
    public void TakeDamage(int damageAmount)
    {
        hp -= damageAmount;

        if (hp <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        // 나중에 쓰러지는 애니메이션이나 폭발 이펙트를 넣을 수 있습니다.
        // 지금은 일단 화면에서 팡! 하고 사라지게 만듭니다.
        Destroy(gameObject);
    }
}