using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public partial class BossRadagonAI : MonoBehaviour
{
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        bossHealth = GetComponent<BossHealth>();

        // ★ 戦闘開始時は必ず第一形態（isPhase2 = false）に強制リセット！
        isPhase2 = false;

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

        // HPチェックを最優先
        CheckPhaseTransition();

        // 行動中またはレーザー照射中は移動・回転・攻撃思考を停止
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

                float moveSpeed = (agent.velocity.sqrMagnitude > 0.1f) ? 1.0f : 0f;
                if (animator != null) animator.SetFloat("Speed", moveSpeed);
            }
            else
            {
                agent.isStopped = true;
                LookAtPlayer();
                if (animator != null) animator.SetFloat("Speed", 0f);
            }
        }

        // タイマー更新と攻撃ルーチンの発火
        if (attackTimer > 0)
        {
            attackTimer -= Time.deltaTime;
        }
        else
        {
            if (playerController != null && playerController.isKnockedDown)
            {
                attackTimer = 1.0f;
                return;
            }

            ChooseAndExecuteAttack(distance);
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

        isPerformingAction = false;
        attackTimer = attackInterval + attackCooldown;
    }

    private void ChooseAndExecuteAttack(float distance)
    {
        Dictionary<AttackCategory, float> attackPool = new Dictionary<AttackCategory, float>();

        // 1. 近距離系
        if (distance <= meleeAttack.attackRange + 1.0f)
        {
            if (attackWeights.enableMeleeNormal && attackWeights.meleeNormalWeight > 0)
                attackPool.Add(AttackCategory.MeleeNormal, attackWeights.meleeNormalWeight);

            if (attackWeights.enableMeleeHeavy && attackWeights.meleeHeavyWeight > 0)
                attackPool.Add(AttackCategory.MeleeHeavy, attackWeights.meleeHeavyWeight);

            if (attackWeights.enableMeleeCombo && attackWeights.meleeComboWeight > 0)
                attackPool.Add(AttackCategory.MeleeCombo, attackWeights.meleeComboWeight);

            if (attackWeights.enableAreaImpact && attackWeights.areaImpactWeight > 0)
                attackPool.Add(AttackCategory.Area, attackWeights.areaImpactWeight);
        }

        // 2. 中〜遠距離・突進系
        if (attackWeights.enableDashAttack && attackWeights.dashAttackWeight > 0)
        {
            if (distance >= dashAttack.minRange && distance <= dashAttack.maxRange)
            {
                attackPool.Add(AttackCategory.DashAttack, attackWeights.dashAttackWeight);
            }
        }

        // 3. ミニオン召喚
        if (attackWeights.enableSummonMinions && attackWeights.summonMinionsWeight > 0 && summonAttack.minionPrefab != null)
        {
            bool phaseCondition = !attackWeights.summonOnlyInPhase2 || isPhase2;
            if (phaseCondition && GetAliveMinionCount() < summonAttack.maxAliveMinions)
            {
                attackPool.Add(AttackCategory.SummonMinions, attackWeights.summonMinionsWeight);
            }
        }

        // 4. 遠距離弾・通常レーザー
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

        // 5. 第二形態専用大技
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

        // 専用コルーチンを持つ攻撃
        if (attack == AttackCategory.MeleeCombo)
        {
            yield return StartCoroutine(MeleeComboRoutine());
            attackTimer = attackInterval + attackCooldown;
            isPerformingAction = false;
            yield break;
        }
        else if (attack == AttackCategory.DashAttack)
        {
            yield return StartCoroutine(DashAttackRoutine());
            attackTimer = attackInterval + attackCooldown;
            isPerformingAction = false;
            yield break;
        }
        else if (attack == AttackCategory.SummonMinions)
        {
            yield return StartCoroutine(SummonMinionsRoutine());
            attackTimer = attackInterval + attackCooldown;
            isPerformingAction = false;
            yield break;
        }

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
        else if (attack == AttackCategory.MeleeNormal)
        {
            yield return new WaitForSeconds(0.8f);
        }
        else if (attack == AttackCategory.MeleeHeavy || attack == AttackCategory.Area)
        {
            yield return new WaitForSeconds(1.2f);
        }
        else
        {
            yield return new WaitForSeconds(1.0f);
        }

        attackTimer = attackInterval + attackCooldown;
        isPerformingAction = false;
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

    public void AE_OnActionEnd()
    {
        isPerformingAction = false;
    }

    // ==========================================
    // プレイヤー行動への割り込み反応（エスト狩り・魔法回避）
    // ==========================================
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

        if (player != null) LookAtPlayer();
        if (animator != null) animator.SetTrigger("LaserAttack");

        AE_StartLaserStraight();

        yield return new WaitForSeconds(laserAttack.duration + 0.5f);

        isPerformingAction = false;
        attackTimer = attackInterval + attackCooldown;
    }

    public void OnPlayerCastedSpell()
    {
        if (!isPerformingAction && !isLaserActive && Random.value <= spellDodgeChance)
        {
            Debug.Log("ボスがプレイヤーの魔法詠唱を検知！ステップ回避！");
            StopAllCoroutines();
            StartCoroutine(DodgeSpellRoutine());
        }
    }

    public void DodgeIncomingSpell()
    {
        if (!isPerformingAction && !isLaserActive && Random.value <= spellDodgeChance)
        {
            Debug.Log("魔法弾を直前で検知！ジャスト回避！");
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

        float sideDirection = (Random.value > 0.5f) ? 1f : -1f;
        Vector3 dodgeDir = transform.right * sideDirection;

        if (animator != null) animator.SetTrigger("Roll");

        float waitSafetyTimer = 0f;
        while (animator != null && !animator.GetCurrentAnimatorStateInfo(0).IsName("Roll") && waitSafetyTimer < 0.2f)
        {
            waitSafetyTimer += Time.deltaTime;
            yield return null;
        }

        float timer = 0f;
        while (timer < dodgeDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / dodgeDuration;

            float speedMultiplier = Mathf.Sin(progress * Mathf.PI);
            float currentDodgeSpeed = dodgeSpeed * speedMultiplier;

            if (agent != null && agent.isOnNavMesh)
                agent.Move(dodgeDir * currentDodgeSpeed * Time.deltaTime);
            else
                transform.position += dodgeDir * currentDodgeSpeed * Time.deltaTime;

            yield return null;
        }

        isPerformingAction = false;
        attackTimer = attackInterval;
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
}