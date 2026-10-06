using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HomingBullet : MonoBehaviour
{
    private Transform target;
    private float speed;
    private float rotateSpeed;
    private float damage;
    private float lifeTime;

    public void Init(Transform targetTransform, float moveSpeed, float homingSpeed, float bulletDamage, float maxLifeTime = 5f)
    {
        target = targetTransform;
        speed = moveSpeed;
        rotateSpeed = homingSpeed;
        damage = bulletDamage;
        lifeTime = maxLifeTime;
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        // プレイヤーが存在する場合はプレイヤーに向かって回転
        if (target != null)
        {
            Vector3 direction = (target.position + Vector3.up * 1.0f) - transform.position;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotateSpeed);
        }

        // 前進処理
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController pc = other.GetComponent<PlayerController>();
            if (pc != null) pc.TakeDamage(damage);
            Destroy(gameObject);
        }
        // ボス自身と弾以外のあらゆる物体（Environmentなど含む）に当たったら消滅
        else if (!other.CompareTag("Boss") && !other.CompareTag("Bullet"))
        {
            Destroy(gameObject);
        }
    }
}
