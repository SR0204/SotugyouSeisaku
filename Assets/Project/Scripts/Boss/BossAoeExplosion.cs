using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AoeExplosion : MonoBehaviour
{
    public float damage = 25.0f;     // 与えるダメージ
    public float lifeTime = 0.5f;     // 判定の存続時間

    void Start()
    {
        // 0.5秒後に自動消滅
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // プレイヤーに当たったらダメージ
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(damage);
            }
        }
    }
}