using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BossRadagonAI : MonoBehaviour
{
    private NavMeshAgent agent;
    private Transform player;
    private PlayerController playerController;
    private BossHealth bossHealth;
    private Animator animator;

    // ==========================================
    // 攻撃ジャンルごとの設定構造体
    // ==========================================
    [System.Serializable]
    public class MeleeAttackSettings
    {
        [Tooltip("通常近接のダメージ")]
        public float damage = 25f;
        [Tooltip("強攻撃・叩きつけのダメージ")]
        public float heavyDamage = 45f;
        [Tooltip("攻撃が届く距離")]
        public float attackRange = 3.5f;
    }

    [System.Serializable]
    public class RangedAttackSettings
    {
        [Tooltip("遠距離攻撃のダメージ")]
        public float damage = 20f;
        [Tooltip("遠距離攻撃を開始する距離")]
        public float minRange = 8f;
        [Tooltip("発射する弾丸・光弾プレハブ")]
        public GameObject projectilePrefab;
        [Tooltip("弾の生成・発射位置")]
        public Transform spawnPoint;
        [Tooltip("弾の飛行スピード")]
        public float projectileSpeed = 15f;
    }

    [System.Serializable]
    public class AreaAttackSettings
    {
        [Tooltip("範囲攻撃（地面叩きつけ等）のダメージ")]
        public float damage = 50f;
        [Tooltip("範囲攻撃を発動する距離")]
        public float triggerRange = 5f;
        [Tooltip("攻撃の有効加害半径")]
        public float radius = 6f;
        [Tooltip("発生させる範囲エフェクト（爆発・衝撃波等）")]
        public GameObject areaVfxPrefab;
        [Tooltip("エフェクト発生位置（足元等）")]
        public Transform areaPoint;
    }

    [System.Serializable]
    public class LaserAttackSettings
    {
        [Tooltip("レーザーの1ヒットあたりのダメージ")]
        public float damagePerTick = 15f;
        [Tooltip("レーザーの有効射程距離")]
        public float maxDistance = 15f;
        [Tooltip("照射時間（秒）")]
        public float duration = 2.0f;
        [Tooltip("レーザー用LineRenderer（または判定エフェクト）")]
        public LineRenderer laserLine;
        [Tooltip("レーザー発射位置（右手・武器先端等）")]
        public Transform firingPoint;
    }

    [System.Serializable]
    public class Laser360AttackSettings
    {
        [Tooltip("360度レーザーの1ヒットあたりのダメージ（一発大ダメージ値）")]
        public float damagePerTick = 80f;
        [Tooltip("有効射程距離")]
        public float maxDistance = 20f;
        [Tooltip("照射時間（秒）")]
        public float duration = 3.0f;
        [Tooltip("チャージ（予兆）時間（秒）")]
        public float chargeTime = 1.5f;
        [Tooltip("チャージ中に表示するエフェクト（エネルギー蓄積VFXなど）")]
        public GameObject chargeVfxPrefab;
        [Tooltip("発射・チャージ位置（未指定時は本体・発射位置）")]
        public Transform firingPoint;
    }

    [System.Serializable]
    public class EclipseMeteorSettings
    {
        [Tooltip("メテオ着弾・爆破時の極大ダメージ")]
        public float damage = 150f;
        [Tooltip("チャージ・予兆詠唱時間（秒）")]
        public float chargeTime = 3.0f;
        [Tooltip("爆破・加害半径（エリア全域をカバーするサイズ）")]
        public float explosionRadius = 25f;
        [Tooltip("メテオ落下の生成高度（頭上何メートルから落ちてくるか）")]
        public float dropHeight = 30f;
        [Tooltip("詠唱中のチャージ/魔法陣VFX")]
        public GameObject chargeVfxPrefab;
        [Tooltip("上空から落ちてくる隕石（メテオ）プレハブ")]
        public GameObject meteorPrefab;
        [Tooltip("着弾時の極大爆発VFX")]
        public GameObject impactVfxPrefab;
    }

    [System.Serializable]
    public class AttackProbabilities
    {
        [Header("--- 近接攻撃 ---")]
        public bool enableMeleeNormal = true;
        public float meleeNormalWeight = 20f;
        public bool enableMeleeHeavy = true;
        public float meleeHeavyWeight = 15f;

        [Header("--- 遠距離（弾）攻撃 ---")]
        public bool enableRangedSingle = true;
        public float rangedSingleWeight = 10f;
        public bool enableRangedRapid = true;
        public float rangedRapidWeight = 10f;
        public bool enableRangedSpread = true;
        public float rangedSpreadWeight = 10f;

        [Header("--- 範囲攻撃 ---")]
        public bool enableAreaImpact = true;
        public float areaImpactWeight = 15f;

        [Header("--- レーザー攻撃 ---")]
        public bool enableLaserStraight = true;
        public float laserStraightWeight = 10f;
        public bool enableLaserSweep360Charge = true;
        public float laserSweep360ChargeWeight = 10f;
        public bool enableLaserSweep90 = true;
        public float laserSweep90Weight = 5f;

        [Header("--- 大技（必殺技） ---")]
        public bool enableEclipseMeteor = true;
        public float eclipseMeteorWeight = 5f;
    }

    // ==========================================
    // Inspector 設定項目
    // ==========================================
    [Header("=== 1. 近接攻撃設定 ===")]
    public MeleeAttackSettings meleeAttack;

    [Header("=== 2. 遠距離攻撃設定 ===")]
    public RangedAttackSettings rangedAttack;

    [Header("=== 3. 範囲攻撃（AoE）設定 ===")]
    public AreaAttackSettings areaAttack;

    [Header("=== 4. 通常レーザー攻撃設定 ===")]
    public LaserAttackSettings laserAttack;

    [Header("=== 4-2. ★ 360度レーザー（チャージ専用） ===")]
    public Laser360AttackSettings laser360Attack;

    [Header("=== 4-3. ★ エクリプスメテオ（極大範囲攻撃） ===")]
    public EclipseMeteorSettings eclipseMeteor;

    [Header("=== 5. 各攻撃の発生確率（重み）設定 ===")]
    public AttackProbabilities attackWeights;

    [Header("=== 共通・フェーズ設定 ===")]
    public float attackInterval = 2.0f;
    public bool isPhase2 = false;
    public float phase2HpThreshold = 0.5f;

    [Header("=== ★ 第二形態（覚醒演出）設定 ===")]
    [Tooltip("第二形態移行時に発生させるエフェクト（オーラ、爆発など）")]
    public GameObject phase2AuraVfxPrefab;
    [Tooltip("溜め完了時に発生させる大爆発エフェクト")]
    public GameObject phase2ExplosionVfxPrefab;
    [Tooltip("覚醒エフェクトを出して溜める時間（秒）")]
    public float phase2TransitionTime = 2.0f;

    // ★ 魔法回避（サイドステップ）設定
    [Header("=== ★ 魔法対抗（ステップ回避）設定 ===")]
    [Tooltip("魔法検知時に回避を発動する確率（0.5 = 50%）")]
    [Range(0f, 1f)] public float spellDodgeChance = 0.6f;
    [Tooltip("回避移動のスピード")]
    public float dodgeSpeed = 12f;
    [Tooltip("回避にかかる時間（秒）")]
    public float dodgeDuration = 0.4f;

    private float attackTimer = 0f;
    private bool isPerformingAction = false;
    private AttackCategory currentAttack = AttackCategory.None;
    private bool isLaserActive = false;

    public enum AttackCategory
    {
        None,
        MeleeNormal,
        MeleeHeavy,
        Ranged,
        RangedRapid,
        RangedSpread,
        Area,
        LaserStraight,
        LaserSweep360Charge,
        LaserSweep90,
        EclipseMeteor
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        bossHealth = GetComponent<BossHealth>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerController = playerObj.GetComponent<PlayerController>();
        }

        isPerformingAction = false;
    }

    void Update()
    {
        if (player == null) return;

        // ★ HPチェックを最優先で行う
        CheckPhaseTransition();

        // 行動中またはレーザー照射中は移動・回転・攻撃思考をすべて完全停止
        if (isPerformingAction || isLaserActive)
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            if (animator != null) animator.SetFloat("Speed", 0f);
            return;
        }

        float distance = Vector3.Distance(transform.position, player.position);

        if (agent != null && agent.isOnNavMesh)
        {
            float stopDistance = meleeAttack.attackRange;
            if (distance > stopDistance)
            {
                agent.isStopped = false;
                agent.SetDestination(player.position);
                if (animator != null) animator.SetFloat("Speed", agent.velocity.magnitude);
            }
            else
            {
                agent.isStopped = true;
                LookAtPlayer();
                if (animator != null) animator.SetFloat("Speed", 0f);
            }
        }

        if (attackTimer > 0)
        {
            attackTimer -= Time.deltaTime;
        }
        else
        {
            ChooseAndExecuteAttack(distance);
            attackTimer = attackInterval;
        }
    }

    private void CheckPhaseTransition()
    {
        if (!isPhase2 && bossHealth != null)
        {
            if (bossHealth.currentHealth / bossHealth.maxHealth <= phase2HpThreshold)
            {
                isPhase2 = true;
                isPerformingAction = true;

                StopAllCoroutines();
                CancelInvoke();

                StartCoroutine(ExecutePhase2Sequence());
            }
        }
    }

    private IEnumerator ExecutePhase2Sequence()
    {
        isPerformingAction = true;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        LookAtPlayer();

        if (animator != null)
        {
            animator.SetTrigger("Phase2Transition");
        }

        GameObject auraObj = null;
        if (phase2AuraVfxPrefab != null)
        {
            auraObj = Instantiate(phase2AuraVfxPrefab, transform.position, Quaternion.identity, transform);
            auraObj.transform.localPosition = Vector3.zero;
            auraObj.transform.localRotation = Quaternion.identity;
        }

        yield return new WaitForSeconds(phase2TransitionTime);

        if (phase2ExplosionVfxPrefab != null)
        {
            GameObject explosionObj = Instantiate(phase2ExplosionVfxPrefab, transform.position, Quaternion.identity);
            Destroy(explosionObj, 3.0f);
        }

        if (auraObj != null) Destroy(auraObj);

        yield return new WaitForSeconds(0.4f);

        if (animator != null)
        {
            animator.SetTrigger("AreaAttack");
        }

        yield return StartCoroutine(EclipseMeteorRoutine());

        isPerformingAction = false;
        attackTimer = attackInterval;
    }

    private void ChooseAndExecuteAttack(float distance)
    {
        Dictionary<AttackCategory, float> attackPool = new Dictionary<AttackCategory, float>();

        if (distance <= meleeAttack.attackRange + 1.0f)
        {
            if (attackWeights.enableMeleeNormal && attackWeights.meleeNormalWeight > 0)
                attackPool.Add(AttackCategory.MeleeNormal, attackWeights.meleeNormalWeight);

            if (attackWeights.enableMeleeHeavy && attackWeights.meleeHeavyWeight > 0)
                attackPool.Add(AttackCategory.MeleeHeavy, attackWeights.meleeHeavyWeight);

            if (attackWeights.enableAreaImpact && attackWeights.areaImpactWeight > 0)
                attackPool.Add(AttackCategory.Area, attackWeights.areaImpactWeight);
        }

        if (distance >= rangedAttack.minRange)
        {
            if (attackWeights.enableRangedSingle && attackWeights.rangedSingleWeight > 0)
                attackPool.Add(AttackCategory.Ranged, attackWeights.rangedSingleWeight);

            if (attackWeights.enableRangedRapid && attackWeights.rangedRapidWeight > 0)
                attackPool.Add(AttackCategory.RangedRapid, attackWeights.rangedRapidWeight);

            if (attackWeights.enableRangedSpread && attackWeights.rangedSpreadWeight > 0)
                attackPool.Add(AttackCategory.RangedSpread, attackWeights.rangedSpreadWeight);

            if (attackWeights.enableLaserStraight && attackWeights.laserStraightWeight > 0)
                attackPool.Add(AttackCategory.LaserStraight, attackWeights.laserStraightWeight);
        }

        if (isPhase2)
        {
            if (distance <= laser360Attack.maxDistance)
            {
                if (attackWeights.enableLaserSweep360Charge && attackWeights.laserSweep360ChargeWeight > 0)
                    attackPool.Add(AttackCategory.LaserSweep360Charge, attackWeights.laserSweep360ChargeWeight);

                if (attackWeights.enableLaserSweep90 && attackWeights.laserSweep90Weight > 0)
                    attackPool.Add(AttackCategory.LaserSweep90, attackWeights.laserSweep90Weight);
            }

            if (attackWeights.enableEclipseMeteor && attackWeights.eclipseMeteorWeight > 0)
            {
                attackPool.Add(AttackCategory.EclipseMeteor, attackWeights.eclipseMeteorWeight);
            }
        }

        if (attackPool.Count == 0) return;

        float totalWeight = 0f;
        foreach (var item in attackPool) totalWeight += item.Value;

        float randomValue = Random.Range(0f, totalWeight);
        float currentSum = 0f;

        foreach (var item in attackPool)
        {
            currentSum += item.Value;
            if (randomValue <= currentSum)
            {
                currentAttack = item.Key;
                break;
            }
        }

        StartCoroutine(ExecuteAttackRoutine(currentAttack));
    }

    private IEnumerator ExecuteAttackRoutine(AttackCategory attack)
    {
        isPerformingAction = true;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        if (animator != null) animator.SetFloat("Speed", 0f);

        LookAtPlayer();

        if (animator != null)
        {
            switch (attack)
            {
                case AttackCategory.MeleeNormal:
                    animator.SetTrigger("Attack");
                    Invoke("AE_MeleeHit", 0.6f);
                    break;
                case AttackCategory.MeleeHeavy:
                    animator.SetTrigger("HeavyAttack");
                    Invoke("AE_MeleeHit", 0.9f);
                    break;
                case AttackCategory.Ranged:
                    animator.SetTrigger("RangedAttack");
                    Invoke("AE_ShootProjectile", 0.5f);
                    break;
                case AttackCategory.RangedRapid:
                    animator.SetTrigger("RangedAttack");
                    Invoke("AE_ShootProjectileRapid", 0.5f);
                    break;
                case AttackCategory.RangedSpread:
                    animator.SetTrigger("RangedAttack");
                    Invoke("AE_ShootProjectileSpread", 0.5f);
                    break;
                case AttackCategory.Area:
                    animator.SetTrigger("AreaAttack");
                    Invoke("AE_AreaImpact", 0.7f);
                    break;
                case AttackCategory.LaserStraight:
                    animator.SetTrigger("LaserAttack");
                    Invoke("AE_StartLaserStraight", 0.4f);
                    break;
                case AttackCategory.LaserSweep360Charge:
                    animator.SetTrigger("LaserAttack");
                    Invoke("AE_StartLaser360Charge", 0.2f);
                    break;
                case AttackCategory.LaserSweep90:
                    animator.SetTrigger("LaserAttack");
                    Invoke("AE_StartLaser90", 0.4f);
                    break;
                case AttackCategory.EclipseMeteor:
                    animator.SetTrigger("AreaAttack");
                    break;
            }
        }
        else
        {
            switch (attack)
            {
                case AttackCategory.MeleeNormal:
                case AttackCategory.MeleeHeavy:
                    Invoke("AE_MeleeHit", 0.5f);
                    break;
                case AttackCategory.Ranged:
                    Invoke("AE_ShootProjectile", 0.5f);
                    break;
                case AttackCategory.RangedRapid:
                    Invoke("AE_ShootProjectileRapid", 0.5f);
                    break;
                case AttackCategory.RangedSpread:
                    Invoke("AE_ShootProjectileSpread", 0.5f);
                    break;
                case AttackCategory.Area:
                    Invoke("AE_AreaImpact", 0.5f);
                    break;
                case AttackCategory.LaserStraight:
                    Invoke("AE_StartLaserStraight", 0.4f);
                    break;
                case AttackCategory.LaserSweep360Charge:
                    Invoke("AE_StartLaser360Charge", 0.2f);
                    break;
                case AttackCategory.LaserSweep90:
                    Invoke("AE_StartLaser90", 0.4f);
                    break;
            }
        }

        if (attack == AttackCategory.LaserStraight ||
            attack == AttackCategory.LaserSweep360Charge ||
            attack == AttackCategory.LaserSweep90)
        {
            isLaserActive = true;
            while (isLaserActive)
            {
                yield return null;
            }
            yield return new WaitForSeconds(0.8f);
        }
        else if (attack == AttackCategory.EclipseMeteor)
        {
            yield return StartCoroutine(EclipseMeteorRoutine());
        }
        else
        {
            yield return new WaitForSeconds(2.5f);
        }

        isPerformingAction = false;
    }

    private IEnumerator EclipseMeteorRoutine()
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        Vector3 targetPos = (player != null) ? player.position : transform.position;
        targetPos.y = transform.position.y;

        GameObject chargeVfx = null;
        if (eclipseMeteor.chargeVfxPrefab != null)
        {
            chargeVfx = Instantiate(eclipseMeteor.chargeVfxPrefab, targetPos, Quaternion.identity);
            float diameter = eclipseMeteor.explosionRadius * 2f;
            chargeVfx.transform.localScale = new Vector3(diameter, 1f, diameter);

            ParticleSystem[] chargeParticles = chargeVfx.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in chargeParticles)
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.loop = false;
            }
        }

        yield return new WaitForSeconds(eclipseMeteor.chargeTime);

        if (chargeVfx != null) Destroy(chargeVfx);

        Vector3 spawnPos = targetPos + Vector3.up * eclipseMeteor.dropHeight;
        GameObject meteorObj = null;
        if (eclipseMeteor.meteorPrefab != null)
        {
            meteorObj = Instantiate(eclipseMeteor.meteorPrefab, spawnPos, Quaternion.identity);
            StartCoroutine(DropMeteorToGround(meteorObj, spawnPos, targetPos, 1.2f));
        }

        yield return new WaitForSeconds(1.2f);

        if (meteorObj != null) Destroy(meteorObj);

        if (eclipseMeteor.impactVfxPrefab != null)
        {
            GameObject impactVfx = Instantiate(eclipseMeteor.impactVfxPrefab, targetPos, Quaternion.identity);
            float diameter = eclipseMeteor.explosionRadius * 2f;
            impactVfx.transform.localScale = new Vector3(diameter, diameter, diameter);

            ParticleSystem[] impactParticles = impactVfx.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in impactParticles)
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.loop = false;
            }

            Destroy(impactVfx, 2.5f);
        }

        Collider[] hits = Physics.OverlapSphere(targetPos, eclipseMeteor.explosionRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                PlayerController playerComp = hit.GetComponent<PlayerController>();
                if (playerComp == null) playerComp = hit.GetComponentInParent<PlayerController>();

                if (playerComp != null)
                {
                    playerComp.TakeDamage(eclipseMeteor.damage);
                }
            }
        }

        yield return new WaitForSeconds(2.0f);
    }

    private IEnumerator DropMeteorToGround(GameObject meteor, Vector3 startPos, Vector3 endPos, float dropDuration)
    {
        float timer = 0f;
        while (timer < dropDuration)
        {
            if (meteor == null) yield break;
            timer += Time.deltaTime;
            meteor.transform.position = Vector3.Lerp(startPos, endPos, timer / dropDuration);
            yield return null;
        }
    }

    private void LookAtPlayer()
    {
        if (player == null) return;
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    public void AE_MeleeHit()
    {
        if (player == null) return;
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= meleeAttack.attackRange + 1.5f && playerController != null)
        {
            Vector3 originPos = transform.position + Vector3.up * 1.0f;
            Vector3 targetPos = player.position + Vector3.up * 1.0f;
            Vector3 dir = (targetPos - originPos).normalized;
            float checkDist = Vector3.Distance(originPos, targetPos);

            if (Physics.Raycast(originPos, dir, out RaycastHit hit, checkDist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.CompareTag("Player")) return;
            }

            float dmg = (currentAttack == AttackCategory.MeleeHeavy) ? meleeAttack.heavyDamage : meleeAttack.damage;
            playerController.TakeDamage(dmg);
        }
    }

    public void AE_ShootProjectile() => SpawnSingleBullet(Quaternion.identity);
    public void AE_ShootProjectileRapid() => StartCoroutine(RapidFireRoutine(3, 0.15f));

    private IEnumerator RapidFireRoutine(int count, float interval)
    {
        for (int i = 0; i < count; i++)
        {
            SpawnSingleBullet(Quaternion.identity);
            yield return new WaitForSeconds(interval);
        }
    }

    public void AE_ShootProjectileSpread()
    {
        SpawnSingleBullet(Quaternion.identity);
        SpawnSingleBullet(Quaternion.Euler(0, -15f, 0));
        SpawnSingleBullet(Quaternion.Euler(0, 15f, 0));
    }

    private void SpawnSingleBullet(Quaternion offsetRotation)
    {
        Transform spawnPoint = rangedAttack.spawnPoint != null ? rangedAttack.spawnPoint : transform;
        if (rangedAttack.projectilePrefab != null && player != null)
        {
            Vector3 spawnPos = spawnPoint.position + transform.forward * 0.8f;
            Vector3 baseDir = (player.position + Vector3.up * 1.0f - spawnPos).normalized;
            Vector3 finalDir = offsetRotation * baseDir;

            GameObject bullet = Instantiate(rangedAttack.projectilePrefab, spawnPos, Quaternion.LookRotation(finalDir));
            Rigidbody rb = bullet.GetComponent<Rigidbody>();
            if (rb != null) rb.velocity = finalDir * rangedAttack.projectileSpeed;

            Destroy(bullet, 5.0f);
        }
    }

    public void AE_AreaImpact()
    {
        Transform origin = (areaAttack.areaPoint != null) ? areaAttack.areaPoint : transform;

        if (areaAttack.areaVfxPrefab != null)
        {
            GameObject vfx = Instantiate(areaAttack.areaVfxPrefab, origin.position, Quaternion.identity);
            StartCoroutine(FadeOutAndDestroy(vfx, 1.2f));
        }

        Collider[] hits = Physics.OverlapSphere(origin.position, areaAttack.radius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player") && playerController != null)
            {
                Vector3 originPos = origin.position + Vector3.up * 0.5f;
                Vector3 targetPos = hit.transform.position + Vector3.up * 1.0f;
                Vector3 dir = (targetPos - originPos).normalized;
                float checkDist = Vector3.Distance(originPos, targetPos);

                if (Physics.Raycast(originPos, dir, out RaycastHit hitInfo, checkDist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (!hitInfo.collider.CompareTag("Player")) continue;
                }

                playerController.TakeDamage(areaAttack.damage);
            }
        }
    }

    private IEnumerator FadeOutAndDestroy(GameObject target, float fadeDuration)
    {
        if (target == null) yield break;

        Vector3 originalScale = target.transform.localScale;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        float timer = 0f;

        while (timer < fadeDuration)
        {
            if (target == null) yield break;

            timer += Time.deltaTime;
            float progress = timer / fadeDuration;

            target.transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, progress);

            foreach (var rend in renderers)
            {
                if (rend != null && rend.material != null && rend.material.HasProperty("_Color"))
                {
                    Color c = rend.material.color;
                    c.a = Mathf.Lerp(1f, 0f, progress);
                    rend.material.color = c;
                }
            }

            yield return null;
        }

        Destroy(target);
    }

    public void AE_StartLaserStraight()
    {
        Transform origin = GetLaserFiringPoint();
        LaserBeam laserBeam = origin.GetComponent<LaserBeam>();
        if (laserBeam != null)
        {
            laserBeam.FireLaser(0.8f, laserAttack.duration, 0.5f, laserAttack.maxDistance, laserAttack.damagePerTick, 0f);
        }
    }

    public void AE_StartLaser360Charge()
    {
        StartCoroutine(Laser360ChargeRoutine());
    }

    private IEnumerator Laser360ChargeRoutine()
    {
        Transform origin = GetLaser360FiringPoint();

        GameObject chargeVfx = null;
        if (laser360Attack.chargeVfxPrefab != null)
        {
            chargeVfx = Instantiate(laser360Attack.chargeVfxPrefab, origin.position, Quaternion.identity, origin);
        }

        yield return new WaitForSeconds(laser360Attack.chargeTime);

        if (chargeVfx != null) Destroy(chargeVfx);

        LaserBeam laserBeam = origin.GetComponent<LaserBeam>();
        if (laserBeam != null)
        {
            laserBeam.FireLaser(0.8f, laser360Attack.duration, 0.5f, laser360Attack.maxDistance, laser360Attack.damagePerTick, 360f);
        }
    }

    public void AE_StartLaser90()
    {
        Transform origin = GetLaserFiringPoint();
        LaserBeam laserBeam = origin.GetComponent<LaserBeam>();
        if (laserBeam != null)
        {
            laserBeam.FireLaser(0.6f, laserAttack.duration, 0.5f, laserAttack.maxDistance, laserAttack.damagePerTick, 90f);
        }
    }

    private Transform GetLaserFiringPoint()
    {
        return (laserAttack.firingPoint != null) ? laserAttack.firingPoint : transform;
    }

    private Transform GetLaser360FiringPoint()
    {
        if (laser360Attack.firingPoint != null) return laser360Attack.firingPoint;
        if (laserAttack.firingPoint != null) return laserAttack.firingPoint;
        return transform;
    }

    public void AE_OnActionEnd()
    {
        isPerformingAction = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (areaAttack != null)
        {
            Gizmos.color = Color.yellow;
            Transform origin = (areaAttack.areaPoint != null) ? areaAttack.areaPoint : transform;
            Gizmos.DrawWireSphere(origin.position, areaAttack.radius);
        }

        if (eclipseMeteor != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, eclipseMeteor.explosionRadius);
        }
    }

    public void OnLaserFinished()
    {
        isLaserActive = false;
    }

    // ★ 1. プレイヤーがポーションを飲んだ時（エスト狩り）
    public void OnPlayerUsedPotion(Vector3 playerPos)
    {
        if (!isPerformingAction && !isLaserActive && Vector3.Distance(transform.position, playerPos) > 5f)
        {
            Debug.Log("ボスがエスト検知！確定で割り込み遠距離レーザー発射！");
            StopAllCoroutines();
            StartCoroutine(PunishPotionRoutine());
        }
    }

    private IEnumerator PunishPotionRoutine()
    {
        isPerformingAction = true;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        if (player != null)
        {
            LookAtPlayer();
        }

        if (animator != null)
        {
            animator.SetTrigger("LaserAttack");
        }

        AE_StartLaserStraight();

        yield return new WaitForSeconds(laserAttack.duration + 0.5f);

        isPerformingAction = false;
    }

    // ★ 2. プレイヤーが魔法を詠唱した時の反応（確率回避）
    public void OnPlayerCastedSpell()
    {
        // 行動中でなく、一定の確率を満たした場合にサイドステップ実行
        if (!isPerformingAction && !isLaserActive && Random.value <= spellDodgeChance)
        {
            Debug.Log("ボスがプレイヤーの魔法詠唱を検知！ステップ回避！");
            StopAllCoroutines();
            StartCoroutine(DodgeSpellRoutine());
        }
    }

    private IEnumerator DodgeSpellRoutine()
    {
        isPerformingAction = true;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        LookAtPlayer();

        // 左右ランダムな方向を決定（1 = 右, -1 = 左）
        float sideDirection = (Random.value > 0.5f) ? 1f : -1f;
        Vector3 dodgeDir = transform.right * sideDirection;

        // ★ 1. まずアニメーショントリガーを叩く
        if (animator != null)
        {
            animator.SetTrigger("Roll");
        }

        // ★ 2. Animator が実際に Roll ステートに切り替わるまで移動を開始しない（前滑り防止）
        float waitSafetyTimer = 0f;
        while (animator != null && !animator.GetCurrentAnimatorStateInfo(0).IsName("Roll") && waitSafetyTimer < 0.2f)
        {
            waitSafetyTimer += Time.deltaTime;
            yield return null; // 1フレーム待機
        }

        // ★ 3. モーションが確実に始まってから、なめらかに移動（イージング移動）
        float timer = 0f;
        while (timer < dodgeDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / dodgeDuration;

            // 前半にしっかり移動し、後半にかけて自然に減速するカーブ
            float speedMultiplier = Mathf.Sin(progress * Mathf.PI); // 綺麗な加減速カーブ
            float currentDodgeSpeed = dodgeSpeed * speedMultiplier;

            if (agent != null && agent.isOnNavMesh)
            {
                agent.Move(dodgeDir * currentDodgeSpeed * Time.deltaTime);
            }
            else
            {
                transform.position += dodgeDir * currentDodgeSpeed * Time.deltaTime;
            }

            yield return null;
        }

        isPerformingAction = false;
    }

    // ★ 魔法弾が直前に迫った時に呼ばれる回避処理
    public void DodgeIncomingSpell()
    {
        // 行動中でなく、確率チェックをクリアした場合に回避
        if (!isPerformingAction && !isLaserActive && Random.value <= spellDodgeChance)
        {
            Debug.Log("魔法弾を直前で検知！ジャスト回避！");
            StopAllCoroutines();
            StartCoroutine(DodgeSpellRoutine());
        }
    }
}