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
    private Animator animator;

    [System.Serializable]
    public struct LaserSettings
    {
        public string name;
        public bool enabled;
        public float chargeTime;
        public float duration;
        public float radius;
        public float distance;
        public float damage;
        public float sweepAngle;
    }

    // ★ ディレイ撃ち設定構造体
    [System.Serializable]
    public struct DelayShotSettings
    {
        public string name;
        public bool enabled;
        public int shotCount;             // 連射数 (例: 4)
        public float baseInterval;        // 基本の発射間隔 (例: 0.3)
        public float delayMultiplier;    // ディレイ倍率 (例: 2.0 で一瞬タメが入る)
        public float damage;
    }

    // ★ クロスファイア設定構造体
    [System.Serializable]
    public struct CrossFireSettings
    {
        public string name;
        public bool enabled;
        public float waveInterval;        // +字から×字までの時間差
        public float damage;
    }

    // ★ ホーミング弾設定構造体
    [System.Serializable]
    public struct HomingSettings
    {
        public string name;
        public bool enabled;
        public int bulletCount;           // 発射数
        public float speed;               // 弾速
        public float homingSpeed;         // 追従性能 (回転速度)
        public float damage;
    }

    // ==========================================
    // 1. ボスのタイプ設定 (ON/OFF)
    // ==========================================
    [Header("=== 1. 行動フラグ (ON/OFF) ===")]
    [SerializeField] private bool canMelee = true;
    [SerializeField] private bool canShoot = true;
    [SerializeField] private bool canAreaAttack = true;
    [SerializeField] private bool canDash = true;
    [SerializeField] private bool canSummon = true;
    [SerializeField] private bool canFlee = false;

    // ==========================================
    // 2. 基本ステータス
    // ==========================================
    [Header("=== 2. 基本ステータス ===")]
    [SerializeField] private float meleeRange = 3.0f;
    [SerializeField] private float meleeDamage = 15.0f;
    [SerializeField] private float attackInterval = 0.2f;

    // ==========================================
    // 3. 移動・逃走設定
    // ==========================================
    [Header("=== 3. 移動・逃走設定 ===")]
    [SerializeField] private float keepDistance = 8.0f;
    [SerializeField] private float fleeSpeed = 3.5f;
    [SerializeField] private float normalSpeed = 5.0f;

    // ==========================================
    // 4. 各攻撃の詳細設定
    // ==========================================
    [Header("=== 4-1. 射撃攻撃設定 ===")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [Range(4, 36)]
    [SerializeField] private int omniBulletCount = 12;

    [Header("--- 新規弾幕パターン設定 ---")]
    [SerializeField]
    private DelayShotSettings delayShot = new DelayShotSettings
    {
        name = "緩急ディレイ撃ち",
        enabled = true,
        shotCount = 4,
        baseInterval = 0.2f,
        delayMultiplier = 2.5f,
        damage = 25f
    };

    [SerializeField]
    private CrossFireSettings crossFire = new CrossFireSettings
    {
        name = "十字・X字クロスファイア",
        enabled = true,
        waveInterval = 0.6f,
        damage = 30f
    };

    [SerializeField]
    private HomingSettings homingShot = new HomingSettings
    {
        name = "時間差ホーミング弾",
        enabled = true,
        bulletCount = 3,
        speed = 8.0f,
        homingSpeed = 3.0f,
        damage = 35f
    };

    [Header("=== 4-2. 範囲予兆攻撃設定 ===")]
    [SerializeField] private GameObject warningAreaPrefab;
    [SerializeField] private GameObject aoeExplosionPrefab;
    [SerializeField] private float warningDuration = 1.0f;

    [Header("=== 4-3. 突進攻撃設定 ===")]
    [SerializeField] private float dashSpeed = 20.0f;
    [SerializeField] private float dashDuration = 0.4f;

    [Header("=== 4-4. 雑魚召喚設定 ===")]
    [SerializeField] private GameObject minionPrefab;
    [SerializeField] private Transform[] minionSpawnPoints;

    [Header("=== 4-5. レーザー攻撃設定 ===")]
    [SerializeField] private GameObject laserPrefab;

    [SerializeField]
    private LaserSettings omniLaser = new LaserSettings
    {
        name = "360度なぎ払いレーザー",
        enabled = true,
        chargeTime = 1.8f,
        duration = 4.5f,
        radius = 1.0f,
        distance = 100.0f,
        damage = 100.0f,
        sweepAngle = 360.0f
    };

    [SerializeField]
    private LaserSettings straightGigaLaser = new LaserSettings
    {
        name = "一直線極太レーザー",
        enabled = true,
        chargeTime = 1.5f,
        duration = 2.0f,
        radius = 2.5f,
        distance = 100.0f,
        damage = 80.0f,
        sweepAngle = 0.0f
    };

    [SerializeField]
    private LaserSettings frontSweepLaser = new LaserSettings
    {
        name = "正面45度なぎ払いレーザー",
        enabled = true,
        chargeTime = 0.5f,
        duration = 0.8f,
        radius = 1.2f,
        distance = 100.0f,
        damage = 30.0f,
        sweepAngle = 45.0f
    };

    private float attackTimer = 0f;
    private bool isPerformingAction = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();

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

        if (!isPerformingAction)
        {
            float distance = Vector3.Distance(transform.position, player.position);

            if (agent != null && agent.isOnNavMesh)
            {
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
                    LookAtPlayer();
                }
            }

            if (attackTimer > 0)
            {
                attackTimer -= Time.deltaTime;
            }
            else
            {
                ChooseAction(distance);
                attackTimer = attackInterval;
            }
        }
    }

    private void ChooseAction(float distance)
    {
        List<int> availableActions = new List<int>();

        if (canMelee && distance <= meleeRange) availableActions.Add(0);

        if (canShoot && bulletPrefab != null)
        {
            availableActions.Add(1);
            availableActions.Add(2);
            availableActions.Add(3);

            // 新規弾幕のON/OFF判定
            if (delayShot.enabled) availableActions.Add(10);
            if (crossFire.enabled) availableActions.Add(11);
            if (homingShot.enabled) availableActions.Add(12);
        }

        if (canAreaAttack && warningAreaPrefab != null) availableActions.Add(4);
        if (canDash) availableActions.Add(5);
        if (canSummon && minionPrefab != null) availableActions.Add(6);

        if (laserPrefab != null)
        {
            if (omniLaser.enabled) availableActions.Add(7);
            if (straightGigaLaser.enabled) availableActions.Add(8);
            if (frontSweepLaser.enabled) availableActions.Add(9);
        }

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
            case 7: StartCoroutine(LaserAttackRoutine(omniLaser)); break;
            case 8: StartCoroutine(LaserAttackRoutine(straightGigaLaser)); break;
            case 9: StartCoroutine(LaserAttackRoutine(frontSweepLaser)); break;
            case 10: StartCoroutine(DelayShotRoutine()); break;
            case 11: StartCoroutine(CrossFireRoutine()); break;
            case 12: StartCoroutine(HomingShotRoutine()); break;
        }
    }

    // ==========================================
    // 新規弾幕処理コルーチン
    // ==========================================

    // 1. 緩急ディレイ撃ち（パン…パン…パパン！）
    private IEnumerator DelayShotRoutine()
    {
        isPerformingAction = true;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;

        for (int i = 0; i < delayShot.shotCount; i++)
        {
            LookAtPlayer();
            if (animator != null) animator.SetTrigger("Attack");

            Vector3 spawnPos = GetFirePosition();
            Quaternion spawnRot = GetTargetRotation(spawnPos);
            Instantiate(bulletPrefab, spawnPos, spawnRot);

            // 3発目に一瞬溜め（ディレイ）を入れて回避タイミングをずらす
            float currentWait = (i == 2) ? delayShot.baseInterval * delayShot.delayMultiplier : delayShot.baseInterval;
            yield return new WaitForSeconds(currentWait);
        }

        isPerformingAction = false;
    }

    // 2. 十字 ➔ X字 クロスファイア
    private IEnumerator CrossFireRoutine()
    {
        isPerformingAction = true;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;

        if (animator != null) animator.SetTrigger("Attack");
        Vector3 spawnPos = GetFirePosition();

        // 1波目：十字（0°, 90°, 180°, 270°）
        float[] crossAngles = { 0f, 90f, 180f, 270f };
        foreach (float angle in crossAngles)
        {
            Quaternion rot = Quaternion.Euler(0, angle, 0);
            Instantiate(bulletPrefab, spawnPos, rot);
        }

        yield return new WaitForSeconds(crossFire.waveInterval);

        // 2波目：X字（45°, 135°, 225°, 315°）
        if (animator != null) animator.SetTrigger("Attack");
        float[] xAngles = { 45f, 135f, 225f, 315f };
        foreach (float angle in xAngles)
        {
            Quaternion rot = Quaternion.Euler(0, angle, 0);
            Instantiate(bulletPrefab, spawnPos, rot);
        }

        isPerformingAction = false;
    }

    // 3. 時間差ホーミング弾
    private IEnumerator HomingShotRoutine()
    {
        isPerformingAction = true;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;

        if (animator != null) animator.SetTrigger("Attack");

        Vector3 spawnPos = GetFirePosition();

        // プレイヤーの左右に分散して展開
        for (int i = 0; i < homingShot.bulletCount; i++)
        {
            float offsetAngle = -30f + (i * (60f / Mathf.Max(1, homingShot.bulletCount - 1)));
            Quaternion spawnRot = transform.rotation * Quaternion.Euler(0, offsetAngle, 0);

            GameObject bullet = Instantiate(bulletPrefab, spawnPos, spawnRot);

            // HomingBullet コンポーネントを動的に追加・初期化
            HomingBullet homing = bullet.GetComponent<HomingBullet>();
            if (homing == null) homing = bullet.AddComponent<HomingBullet>();

            homing.Init(player, homingShot.speed, homingShot.homingSpeed, homingShot.damage);

            yield return new WaitForSeconds(0.2f); // 順番に放つ
        }

        isPerformingAction = false;
    }

    // ==========================================
    // 既存処理（レーザー・近接・移動など）
    // ==========================================
    private IEnumerator LaserAttackRoutine(LaserSettings settings)
    {
        isPerformingAction = true;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;

        if (animator != null) animator.SetTrigger("Attack");

        LookAtPlayer();

        Vector3 spawnPos = GetFirePosition();
        Quaternion spawnRot = transform.rotation;

        GameObject laserObj = Instantiate(laserPrefab, spawnPos, spawnRot, transform);
        LaserBeam laserBeam = laserObj.GetComponent<LaserBeam>();

        if (laserBeam != null)
        {
            laserBeam.FireLaser(
                settings.chargeTime,
                settings.duration,
                settings.radius,
                settings.distance,
                settings.damage,
                settings.sweepAngle
            );
        }

        yield return new WaitForSeconds(settings.chargeTime + settings.duration);

        if (laserObj != null) Destroy(laserObj);
        isPerformingAction = false;
    }

    private IEnumerator AreaWarningRoutine()
    {
        isPerformingAction = true;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;

        if (animator != null) animator.SetTrigger("Attack");

        Vector3 targetPos = player.position;
        targetPos.y = 0.01f;

        GameObject warning = Instantiate(warningAreaPrefab, targetPos, Quaternion.identity);
        yield return new WaitForSeconds(warningDuration);

        if (warning != null) Destroy(warning);
        if (aoeExplosionPrefab != null)
        {
            Instantiate(aoeExplosionPrefab, targetPos, Quaternion.identity);
        }

        isPerformingAction = false;
    }

    private IEnumerator DashRoutine()
    {
        isPerformingAction = true;

        if (animator != null) animator.SetTrigger("Attack");

        yield return new WaitForSeconds(0.1f);

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = dashSpeed;
            Vector3 dashTarget = transform.position + transform.forward * (dashSpeed * dashDuration);
            agent.SetDestination(dashTarget);
        }

        yield return new WaitForSeconds(dashDuration);

        if (agent != null && agent.isOnNavMesh)
        {
            agent.speed = normalSpeed;
            agent.isStopped = true;
        }

        isPerformingAction = false;
    }

    private IEnumerator SummonRoutine()
    {
        isPerformingAction = true;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;

        if (animator != null) animator.SetTrigger("Attack");

        yield return new WaitForSeconds(0.3f);

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
        if (animator != null) animator.SetTrigger("Attack");

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
        if (animator != null) animator.SetTrigger("Attack");

        Vector3 spawnPos = GetFirePosition();
        Quaternion spawnRot = GetTargetRotation(spawnPos);
        Instantiate(bulletPrefab, spawnPos, spawnRot);
    }

    private void ThreeWayShot()
    {
        if (bulletPrefab == null) return;
        if (animator != null) animator.SetTrigger("Attack");

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
        if (animator != null) animator.SetTrigger("Attack");

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
            transform.rotation = lookRotation;
        }
    }
}