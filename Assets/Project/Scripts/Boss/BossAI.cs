using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BossAI : MonoBehaviour
{
    private NavMeshAgent agent;
    private Transform player;
    private PlayerController playerController;

    [Header("ステータス設定")]
    [SerializeField] private float attackRange = 2.0f; // 攻撃間隔範囲
    [SerializeField] private float attackDamage = 15.0f; // 1回の攻撃ダメージ
    [SerializeField] private float attackInterval = 1.5f; // 攻撃の間隔（秒）

    private float attackTimer = 0f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        // "Player" タグが付いたオブジェクトを検索
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerController = playerObj.GetComponent<PlayerController>();
        }
    }

    void Update()
    {
        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);

        // クールダウンタイマーの更新
        if (attackTimer > 0)
        {
            attackTimer -= Time.deltaTime;
        }

        // プレイヤーとの距離判定
        if (distance > attackRange)
        {
            // 範囲外なら追尾
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }
        else
        {
            // 範囲内なら停止してプレイヤーを向く
            agent.isStopped = true;
            LookAtPlayer();

            // 攻撃クールダウンが明けていればダメージを与える
            if (attackTimer <= 0f)
            {
                Attack();
                attackTimer = attackInterval; // タイマーリセット
            }
        }
    }

    private void Attack()
    {
        if (playerController != null)
        {
            Debug.Log("ボスの攻撃！ プレイヤーに " + attackDamage + " ダメージ！");
            playerController.TakeDamage(attackDamage);
        }
    }

    private void LookAtPlayer()
    {
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0; // 上下傾き防止
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
        }
    }
}