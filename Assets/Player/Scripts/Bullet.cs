using UnityEngine;

public class Bullet : MonoBehaviour
{
    public int damage = 1; // 총알 한 발의 데미지

    // 총알이 무언가에 꽝! 하고 부딪히면 유니티가 자동으로 실행해 주는 기능
    void OnCollisionEnter(Collision collision)
    {
        // 부딪힌 물체의 이름표(태그)가 "Enemy"인가?
        if (collision.gameObject.CompareTag("Enemy"))
        {
            // 적의 몸에서 EnemyHealth 스크립트를 찾아옵니다.
            EnemyHealth enemy = collision.gameObject.GetComponent<EnemyHealth>();

            if (enemy != null)
            {
                // 데미지를 1 줍니다!
                enemy.TakeDamage(damage);
            }
        }

        // 적인지 벽인지 상관없이, 어딘가에 부딪혔다면 총알 자신은 파괴됩니다.
        Destroy(gameObject);
    }
}