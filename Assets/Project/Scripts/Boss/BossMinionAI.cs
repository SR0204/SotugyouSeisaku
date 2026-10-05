using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossMinionAI : MonoBehaviour
{
    public float hp = 1.0f;
    public float damage = 10.0f;
    public float speed = 3.5f;

    private Transform player;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        Destroy(gameObject, 10.0f);
    }

    void FixedUpdate()
    {
        if (player == null) return;

        // プレイヤーの方向を向く（Y軸固定）
        Vector3 targetPosition = player.position;
        targetPosition.y = transform.position.y;
        transform.LookAt(targetPosition);

        // Y軸の移動を排除して水平方向のみ移動（浮き上がり防止）
        Vector3 moveDirection = (targetPosition - transform.position).normalized;
        moveDirection.y = 0;

        rb.MovePosition(rb.position + moveDirection * speed * Time.fixedDeltaTime);
    }

    // ぶつかった瞬間
    private void OnCollisionEnter(Collision collision)
    {
        TryDamagePlayer(collision.gameObject);
    }

    // 触れ続けている間（乗られた場合も連続ダメージ）
    private void OnCollisionStay(Collision collision)
    {
        TryDamagePlayer(collision.gameObject);
    }

    private void TryDamagePlayer(GameObject target)
    {
        if (target.CompareTag("Player"))
        {
            PlayerController playerCtrl = target.GetComponent<PlayerController>();
            if (playerCtrl != null)
            {
                playerCtrl.TakeDamage(damage * Time.deltaTime); // 触れている間ダメージ
            }
        }
    }

    // プレイヤーの攻撃を受けたときの処理
    public void TakeDamage(float amount)
    {
        hp -= amount;
        if (hp <= 0)
        {
            Destroy(gameObject);
        }
    }
}