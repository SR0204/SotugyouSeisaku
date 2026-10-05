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

    // ==========================================
    // 1. ボスのタイプ設定 (ON/OFF)
    // ==========================================
    [Header("=== 1. 行動フラグ (ON/OFF) ===")]
    [Tooltip("近接攻撃を行うか")]
    [SerializeField] private bool canMelee = true;
    [Tooltip("遠距離射撃を行うか")]
    [SerializeField] private bool canShoot = true;
    [Tooltip("範囲予兆攻撃を行うか")]
    [SerializeField] private bool canAreaAttack = true;
    [Tooltip("突進攻撃を行うか")]
    [SerializeField] private bool canDash = true;
    [Tooltip("雑魚召喚を行うか")]
    [SerializeField] private bool canSummon = true;
    [Tooltip("プレイヤーが近づいたときに逃げるか（距離を取るか）")]
    [SerializeField] private bool canFlee = false; // デフォルトはOFF（サモンボス等でのみONにする）

    // ==========================================
    // 2. ステータス・基本設定
    // ==========================================
    [Header("=== 2. 基本ステータス ===")]
    [Tooltip("近接攻撃に入る距離")]
    [SerializeField] private float meleeRange = 2.5f;
    [Tooltip("近接攻撃のダメージ")]
    [SerializeField] private float meleeDamage = 15.0f;
    [Tooltip("攻撃間隔（秒）")]
    [SerializeField] private float attackInterval = 2.0f;

    // ==========================================
    // 3. 移動・引き撃ち（逃走）設定
    // ==========================================
    [Header("=== 3. 移動・逃走設定 ===")]
    [Tooltip("この距離内にプレイヤーが来たら逃げる（canFleeがONのときのみ有効）")]
    [SerializeField] private float keepDistance = 8.0f;
    [Tooltip("逃げるときのスピード")]
    [SerializeField] private float fleeSpeed = 3.5f;
    [Tooltip("通常時の移動スピード")]
    [SerializeField] private float normalSpeed = 5.0f;

    // ==========================================
    // 4. 各攻撃の詳細設定
    // ==========================================
    [Header("=== 4-1. 射撃攻撃設定 ===")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [Range(4, 36)]
    [Tooltip("全方位ショットの弾数")]
    [SerializeField] private int omniBulletCount = 12;

    [Header("=== 4-2. 範囲予兆攻撃設定 ===")]
    [SerializeField] private GameObject warningAreaPrefab;
    [SerializeField] private GameObject aoeExplosionPrefab;
    [Tooltip("予兆が出てから爆発するまでの時間")]
    [SerializeField] private float warningDuration = 1.5f;

    [Header("=== 4-3. 突進攻撃設定 ===")]
    [SerializeField] private float dashSpeed = 20.0f;
    [SerializeField] private float dashDuration = 0.5f;

    [Header("=== 4-4. 雑魚召喚設定 ===")]
    [SerializeField] private GameObject minionPrefab;
    [SerializeField] private Transform[] minionSpawnPoints;

    // --- 内部変数 ---
    private float attackTimer = 0f;
    private bool isPerformingAction = false;

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
        if (player == null || isPerformingAction) return;

        float distance = Vector3.Distance(transform.position, player.position);

        if (attackTimer > 0)
        {
            attackTimer -= Time.deltaTime;
        }

        LookAtPlayer();

        // --- 移動・引き撃ち処理 ---
        // canFlee が ON のときのみ逃走処理を実行
        if (canFlee && distance < keepDistance)
        {
            Vector3 fleeDirection = (transform.position - player.position).normalized;
            Vector3 fleeTarget = transform.position + fleeDirection * 3.0f;

            agent.isStopped = false;
            agent.speed = fleeSpeed;
            agent.SetDestination(fleeTarget);
        }
        else if (distance > meleeRange)
        {
            agent.isStopped = false;
            agent.speed = normalSpeed;
            agent.SetDestination(player.position);
        }
        else
        {
            agent.isStopped = true;
        }

        // --- 攻撃実行 ---
        if (attackTimer <= 0f)
        {
            ChooseAction();
            attackTimer = attackInterval;
        }
    }

    private void ChooseAction()
    {
        List<int> availableActions = new List<int>();

        if (canMelee) availableActions.Add(0);

        if (canShoot)
        {
            availableActions.Add(1);
            availableActions.Add(2);
            availableActions.Add(3);
        }

        if (canAreaAttack && warningAreaPrefab != null) availableActions.Add(4);
        if (canDash) availableActions.Add(5);
        if (canSummon && minionPrefab != null) availableActions.Add(6);

        if (availableActions.Count == 0) return;

        int selectedAction = availableActions[Random.Range(0, availableActions.Count)];

        switch (selectedAction)
        {
            case 0: MeleeAttack(); break;
            case 1: SingleShot(); break;
            case 2: ThreeWayShot(); break;
            case 3: OmniShot(); break;
            case 4: StartCoroutine(AreaWarningRoutine()); break;
            case 5: StartCoroutine(DashRoutine()); break;
            case 6: StartCoroutine(SummonRoutine()); break;
        }
    }

    private IEnumerator AreaWarningRoutine()
    {
        isPerformingAction = true;
        if (agent != null) agent.isStopped = true;

        Vector3 targetPos = player.position;
        targetPos.y = 0.01f;

        GameObject warning = Instantiate(warningAreaPrefab, targetPos, Quaternion.identity);
        yield return new WaitForSeconds(warningDuration);

        Destroy(warning);
        if (aoeExplosionPrefab != null)
        {
            Instantiate(aoeExplosionPrefab, targetPos, Quaternion.identity);
        }

        isPerformingAction = false;
    }

    private IEnumerator DashRoutine()
    {
        isPerformingAction = true;
        if (agent != null) agent.isStopped = true;

        yield return new WaitForSeconds(0.5f);

        Vector3 dashDir = transform.forward;
        float timer = 0f;

        while (timer < dashDuration)
        {
            transform.position += dashDir * dashSpeed * Time.deltaTime;
            timer += Time.deltaTime;
            yield return null;
        }

        isPerformingAction = false;
    }

    private IEnumerator SummonRoutine()
    {
        isPerformingAction = true;
        if (agent != null) agent.isStopped = true;

        yield return new WaitForSeconds(0.8f);

        if (minionSpawnPoints != null && minionSpawnPoints.Length > 0)
        {
            foreach (var point in minionSpawnPoints)
            {
                if (point != null) Instantiate(minionPrefab, point.position, point.rotation);
            }
        }
        else
        {
            Instantiate(minionPrefab, transform.position + transform.right * 2f, Quaternion.identity);
            Instantiate(minionPrefab, transform.position - transform.right * 2f, Quaternion.identity);
        }

        isPerformingAction = false;
    }

    private void MeleeAttack()
    {
        if (bossProximity != null)
        {
            bossProximity.PerformRandomMeleeAttack();
        }
        else if (playerController != null)
        {
            playerController.TakeDamage(meleeDamage);
        }
    }

    private void SingleShot()
    {
        if (bulletPrefab == null) return;
        Vector3 spawnPos = GetFirePosition();
        Quaternion spawnRot = GetTargetRotation(spawnPos);
        Instantiate(bulletPrefab, spawnPos, spawnRot);
    }

    private void ThreeWayShot()
    {
        if (bulletPrefab == null) return;
        Vector3 spawnPos = GetFirePosition();
        Quaternion baseRot = GetTargetRotation(spawnPos);
        float[] angles = { 0f, -15f, 15f };

        foreach (float angle in angles)
        {
            Quaternion rot = baseRot * Quaternion.Euler(0, angle, 0);
            Instantiate(bulletPrefab, spawnPos, rot);
        }
    }

    private void OmniShot()
    {
        if (bulletPrefab == null) return;
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