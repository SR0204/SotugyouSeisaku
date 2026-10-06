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
        // 前方に飛ぶ
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // プレイヤーに当たったらダメージを与えて消滅
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null) player.TakeDamage(damage);
            Destroy(gameObject);
        }
        // ★ ボス自身や他の弾以外（柱、壁、床など）に当たったら消滅
        else if (!other.CompareTag("Boss") && !other.CompareTag("Bullet"))
        {
            Destroy(gameObject);
        }
    }
}