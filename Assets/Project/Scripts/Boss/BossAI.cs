using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BossAI : MonoBehaviour
{
    private NavMeshAgent agent;
    private Transform player;
    private PlayerController playerController;
    private BossProximity bossProximity;

    [Header("ボスのタイプ設定")]
    [SerializeField] private bool canMelee = true;          // チェックを入れると近接攻撃を候補に含める
    [SerializeField] private bool canShoot = true;          // チェックを入れると射撃を候補に含める

    [Header("ステータス設定")]
    [SerializeField] private float meleeRange = 2.5f;       // 近接攻撃に入る距離（移動停止判定用）
    [SerializeField] private float meleeDamage = 15.0f;     // 近接攻撃のデフォルトダメージ
    [SerializeField] private float attackInterval = 2.0f;   // 行動の間隔（秒）

    [Header("射撃設定")]
    [SerializeField] private GameObject bulletPrefab;        // 弾のプレハブ
    [SerializeField] private Transform firePoint;           // 弾の発射位置
    [SerializeField] private int omniBulletCount = 12;      // 全方位ショットの弾数

    private float attackTimer = 0f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerController = playerObj.GetComponent<PlayerController>();
        }

        bossProximity = GetComponent<BossProximity>();
    }

    void Update()
    {
        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);

        if (attackTimer > 0)
        {
            attackTimer -= Time.deltaTime;
        }

        LookAtPlayer();

        // 近接範囲内なら足を止める、離れていれば追従
        if (distance > meleeRange)
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }
        else
        {
            agent.isStopped = true;
        }

        if (attackTimer <= 0f)
        {
            ChooseAction();
            attackTimer = attackInterval;
        }
    }

    // 利用可能な全攻撃（近接・各種射撃）から完全ランダムで選んで実行
    private void ChooseAction()
    {
        List<int> availableActions = new List<int>();

        // 近接攻撃が許可されていれば候補に追加 (ID: 0)
        if (canMelee)
        {
            availableActions.Add(0);
        }

        // 射撃が許可されていれば各種射撃パターンを候補に追加 (ID: 1:通常, 2:3way, 3:全方位)
        if (canShoot)
        {
            availableActions.Add(1);
            availableActions.Add(2);
            availableActions.Add(3);
        }

        // 実行可能な行動がない場合は処理を抜ける
        if (availableActions.Count == 0) return;

        // 候補の中からランダムで1つ選択
        int selectedAction = availableActions[Random.Range(0, availableActions.Count)];

        switch (selectedAction)
        {
            case 0:
                MeleeAttack();
                break;
            case 1:
                SingleShot();
                break;
            case 2:
                ThreeWayShot();
                break;
            case 3:
                OmniShot();
                break;
        }
    }

    // 近接攻撃
    private void MeleeAttack()
    {
        if (bossProximity != null)
        {
            // BossProximity に作られたランダム近接技を実行
            bossProximity.PerformRandomMeleeAttack();
        }
        else if (playerController != null)
        {
            // BossProximityが付いていない場合のバックアップ
            playerController.TakeDamage(meleeDamage);
        }
    }

    // 1. 通常単発射撃
    private void SingleShot()
    {
        if (bulletPrefab == null) return;
        Debug.Log("ボスの通常射撃！");

        Vector3 spawnPos = GetFirePosition();
        Quaternion spawnRot = GetTargetRotation(spawnPos);

        Instantiate(bulletPrefab, spawnPos, spawnRot);
    }

    // 2. 3wayショット（正面・左15度・右15度）
    private void ThreeWayShot()
    {
        if (bulletPrefab == null) return;
        Debug.Log("ボスの3wayショット！");

        Vector3 spawnPos = GetFirePosition();
        Quaternion baseRot = GetTargetRotation(spawnPos);

        float[] angles = { 0f, -15f, 15f };

        foreach (float angle in angles)
        {
            Quaternion rot = baseRot * Quaternion.Euler(0, angle, 0);
            Instantiate(bulletPrefab, spawnPos, rot);
        }
    }

    // 3. 全方位（360度）ショット
    private void OmniShot()
    {
        if (bulletPrefab == null) return;
        Debug.Log("ボスの全方位ショット！");

        Vector3 spawnPos = GetFirePosition();
        float angleStep = 360f / omniBulletCount;

        for (int i = 0; i < omniBulletCount; i++)
        {
            float currentAngle = i * angleStep;
            Quaternion rot = Quaternion.Euler(0, currentAngle, 0);
            Instantiate(bulletPrefab, spawnPos, rot);
        }
    }

    private Vector3 GetFirePosition()
    {
        return firePoint != null ? firePoint.position : transform.position + transform.forward + Vector3.up;
    }

    private Quaternion GetTargetRotation(Vector3 spawnPos)
    {
        Vector3 targetDirection = (player.position + Vector3.up * 1.0f) - spawnPos;
        return Quaternion.LookRotation(targetDirection);
    }

    private void LookAtPlayer()
    {
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
        }
    }
}