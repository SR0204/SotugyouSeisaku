using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossBullet : MonoBehaviour
{
    public float speed = 15f;
    public float damage = 15f;
    public float lifetime = 5f;

    void Start()
    {
        Destroy(gameObject, lifetime); // 一定時間で消滅
    }

    void Update()
    {
        // 前方に直線移動
        transform.Translate(Vector3.forward * speed * Time.deltaTime, Space.Self);
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. ボス自身、または「Boss」タグが付いているオブジェクトは完全に無視
        if (other.CompareTag("Boss") || other.GetComponent<BossRadagonAI>() != null)
        {
            return;
        }

        // 2. 弾同士の衝突も無視
        if (other.CompareTag("Bullet") || other.GetComponent<BossBullet>() != null)
        {
            return;
        }

        // 3. プレイヤーに当たったらダメージを与えて消滅
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player == null)
            {
                player = other.GetComponentInParent<PlayerController>();
            }

            if (player != null)
            {
                player.TakeDamage(damage);
            }

            Destroy(gameObject);
        }
        // 4. 壁や床（Trigger以外のコライダー）に当たったら消滅
        else if (!other.isTrigger)
        {
            Destroy(gameObject);
        }
    }
}