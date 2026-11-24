using System.Collections;
using UnityEngine;

public partial class BossRadagonAI : MonoBehaviour
{
    // ==========================================
    // 遠距離攻撃（単発・連射・拡散）
    // ==========================================
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

    // ==========================================
    // 範囲衝撃波（AoE）
    // ==========================================
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

    // ==========================================
    // レーザー攻撃（直線・90度・360度）
    // ==========================================
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

    public void OnLaserFinished()
    {
        isLaserActive = false;
    }

    // ==========================================
    // エクリプスメテオ（極大範囲大技）
    // ==========================================
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
}