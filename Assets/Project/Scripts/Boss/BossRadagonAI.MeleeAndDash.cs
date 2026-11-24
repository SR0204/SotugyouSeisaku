using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public partial class BossRadagonAI : MonoBehaviour
{
    // ==========================================
    // 近接攻撃（通常・強攻撃・判定共通処理）
    // ==========================================
    public void AE_MeleeHit()
    {
        float dmg = (currentAttack == AttackCategory.MeleeHeavy) ? meleeAttack.heavyDamage : meleeAttack.damage;
        PerformMeleeHitCheck(dmg);
    }

    private void PerformMeleeHitCheck(float dmg)
    {
        if (player == null || playerController == null) return;
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= meleeAttack.attackRange + 1.5f)
        {
            Vector3 dirToPlayer = (player.position - transform.position).normalized;
            dirToPlayer.y = 0f;
            float angle = Vector3.Angle(transform.forward, dirToPlayer);
            if (angle > 60f) return;

            Vector3 originPos = transform.position + Vector3.up * 1.0f;
            Vector3 targetPos = player.position + Vector3.up * 1.0f;
            Vector3 dir = (targetPos - originPos).normalized;
            float checkDist = Vector3.Distance(originPos, targetPos);

            if (Physics.Raycast(originPos, dir, out RaycastHit hit, checkDist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.CompareTag("Player")) return;
            }

            playerController.TakeDamage(dmg);
        }
    }

    // ==========================================
    // 連続コンボ斬撃
    // ==========================================
    private IEnumerator MeleeComboRoutine()
    {
        for (int i = 0; i < comboMelee.hitCount; i++)
        {
            if (player == null) yield break;

            LookAtPlayer();

            if (animator != null)
            {
                if (i == comboMelee.hitCount - 1)
                    animator.SetTrigger("HeavyAttack");
                else
                    animator.SetTrigger("Attack");
            }

            // 踏み込み移動
            float stepTimer = 0f;
            float stepDuration = 0.2f;
            Vector3 stepDir = transform.forward;
            while (stepTimer < stepDuration)
            {
                stepTimer += Time.deltaTime;
                float moveStep = (comboMelee.stepForwardDistance / stepDuration) * Time.deltaTime;
                if (agent != null && agent.isOnNavMesh)
                    agent.Move(stepDir * moveStep);
                else
                    transform.position += stepDir * moveStep;
                yield return null;
            }

            yield return new WaitForSeconds(0.2f);

            float dmg = (i == comboMelee.hitCount - 1) ? comboMelee.damagePerHit * 1.4f : comboMelee.damagePerHit;
            PerformMeleeHitCheck(dmg);

            yield return new WaitForSeconds(comboMelee.hitInterval);
        }

        yield return new WaitForSeconds(0.5f);
    }

    // ==========================================
    // 高速突進・タックル（赤い予測線表示つき）
    // ==========================================
    private IEnumerator DashAttackRoutine()
    {
        LookAtPlayer();

        if (animator != null)
        {
            animator.SetTrigger("HeavyAttack");
        }

        GameObject vfx = null;
        if (dashAttack.dashVfxPrefab != null)
        {
            vfx = Instantiate(dashAttack.dashVfxPrefab, transform.position + Vector3.up * 0.5f, transform.rotation, transform);
        }

        // 突進予測線の生成
        GameObject lineObj = null;
        LineRenderer lineRenderer = null;
        if (dashAttack.showTelegraphLine)
        {
            lineObj = new GameObject("DashTelegraphLine");
            lineObj.transform.SetParent(transform);
            lineRenderer = lineObj.AddComponent<LineRenderer>();
            lineRenderer.positionCount = 2;
            lineRenderer.startWidth = dashAttack.lineWidth;
            lineRenderer.endWidth = dashAttack.lineWidth;
            lineRenderer.useWorldSpace = true;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.alignment = LineAlignment.TransformZ;
            lineObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Shader lineShader = Shader.Find("Sprites/Default");
            if (lineShader != null)
            {
                lineRenderer.material = new Material(lineShader);
            }
            lineRenderer.startColor = dashAttack.aimingLineColor;
            lineRenderer.endColor = dashAttack.aimingLineColor;
        }

        float maxDashDistance = dashAttack.dashSpeed * dashAttack.dashDuration;

        // 溜め中の追尾＆予測線更新
        float chargeTimer = 0f;
        while (chargeTimer < dashAttack.chargeTime)
        {
            chargeTimer += Time.deltaTime;
            bool isLocked = chargeTimer >= dashAttack.chargeTime * 0.75f;

            if (!isLocked)
            {
                LookAtPlayer();
            }

            if (lineRenderer != null)
            {
                Vector3 startPos = transform.position + Vector3.up * 0.08f;
                Vector3 dir = transform.forward;
                dir.y = 0f;
                dir.Normalize();

                float actualDist = maxDashDistance;
                if (Physics.Raycast(transform.position + Vector3.up * 0.8f, dir, out RaycastHit wallHit, maxDashDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (!wallHit.collider.CompareTag("Player"))
                    {
                        actualDist = wallHit.distance;
                    }
                }

                lineRenderer.SetPosition(0, startPos);
                lineRenderer.SetPosition(1, startPos + dir * actualDist);

                Color lineCol = isLocked ? dashAttack.lockedLineColor : dashAttack.aimingLineColor;
                lineRenderer.startColor = lineCol;
                lineRenderer.endColor = lineCol;
            }

            yield return null;
        }

        if (lineObj != null) Destroy(lineObj);

        // 突進開始
        Vector3 dashDir = transform.forward;
        dashDir.y = 0f;
        dashDir.Normalize();

        if (animator != null) animator.SetFloat("Speed", 1.0f);

        float dashTimer = 0f;
        bool hasHitPlayer = false;

        while (dashTimer < dashAttack.dashDuration)
        {
            dashTimer += Time.deltaTime;
            Vector3 moveDelta = dashDir * dashAttack.dashSpeed * Time.deltaTime;

            if (agent != null && agent.isOnNavMesh)
                agent.Move(moveDelta);
            else
                transform.position += moveDelta;

            if (!hasHitPlayer && player != null && playerController != null)
            {
                float dist = Vector3.Distance(transform.position + Vector3.up * 1.0f, player.position + Vector3.up * 1.0f);
                if (dist <= dashAttack.hitRadius)
                {
                    hasHitPlayer = true;
                    playerController.TakeDamage(dashAttack.damage);
                }
            }

            yield return null;
        }

        if (animator != null) animator.SetFloat("Speed", 0f);
        if (vfx != null) Destroy(vfx);

        yield return new WaitForSeconds(0.9f);
    }

    // ==========================================
    // ミニオン召喚
    // ==========================================
    private int GetAliveMinionCount()
    {
        spawnedMinions.RemoveAll(m => m == null);
        return spawnedMinions.Count;
    }

    private IEnumerator SummonMinionsRoutine()
    {
        LookAtPlayer();

        if (animator != null) animator.SetTrigger("RangedAttack");

        GameObject bossVfx = null;
        if (summonAttack.summonVfxPrefab != null)
        {
            bossVfx = Instantiate(summonAttack.summonVfxPrefab, transform.position + Vector3.up * 0.1f, Quaternion.identity);
        }

        yield return new WaitForSeconds(summonAttack.castTime);

        if (bossVfx != null) Destroy(bossVfx);

        int aliveCount = GetAliveMinionCount();
        int canSpawnCount = Mathf.Min(summonAttack.summonCount, summonAttack.maxAliveMinions - aliveCount);

        for (int i = 0; i < canSpawnCount; i++)
        {
            float angle = (canSpawnCount == 1) ? 0f : Mathf.Lerp(-60f, 60f, (float)i / (canSpawnCount - 1));
            Vector3 offsetDir = Quaternion.Euler(0f, angle, 0f) * transform.forward;
            Vector3 desiredPos = transform.position + offsetDir * summonAttack.spawnRadius;

            Vector3 finalSpawnPos = desiredPos;
            if (NavMesh.SamplePosition(desiredPos, out NavMeshHit navHit, 3.0f, NavMesh.AllAreas))
            {
                finalSpawnPos = navHit.position;
            }

            if (summonAttack.summonVfxPrefab != null)
            {
                GameObject spawnVfx = Instantiate(summonAttack.summonVfxPrefab, finalSpawnPos + Vector3.up * 0.1f, Quaternion.identity);
                Destroy(spawnVfx, 2.0f);
            }

            GameObject minion = Instantiate(summonAttack.minionPrefab, finalSpawnPos, transform.rotation);
            spawnedMinions.Add(minion);
        }

        yield return new WaitForSeconds(0.6f);
    }

    private void OnDestroy()
    {
        foreach (var minion in spawnedMinions)
        {
            if (minion != null) Destroy(minion);
        }
        spawnedMinions.Clear();
    }
}