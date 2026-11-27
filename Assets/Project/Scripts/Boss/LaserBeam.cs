using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaserBeam : MonoBehaviour
{
    [Header("=== レーザー表示設定（2層構造） ===")]
    [Tooltip("外側のオーラ用LineRenderer（既存のものをそのまま指定）")]
    public LineRenderer lineRenderer;
    [Tooltip("中心の白く輝く芯用LineRenderer（自動生成も可）")]
    public LineRenderer coreLineRenderer;

    [Header("=== 色設定（HDR発光対応） ===")]
    [ColorUsage(true, true)]
    public Color warningColor = new Color(3.0f, 0.2f, 0.2f, 1f);
    [ColorUsage(true, true)]
    public Color outerAttackColor = new Color(0.1f, 1.8f, 3.5f, 0.85f); // 外側の魔力オーラ
    [ColorUsage(true, true)]
    public Color coreAttackColor = new Color(4.0f, 4.0f, 4.0f, 1f);     // 中心の高輝度コア

    [Header("=== 演出・照り返しオプション ===")]
    [Tooltip("発射口に置く光の球やパーティクル（任意）")]
    public Transform muzzleFlashEffect;
    [Tooltip("ビームの脈動スピードと振れ幅")]
    public float pulseSpeed = 35f;
    public float pulseAmplitude = 0.15f;
    [Tooltip("地面や周囲を照らす照り返しライトの強さ（0で無効）")]
    public float beamLightIntensity = 12f;
    [Tooltip("照り返しライトが届く範囲")]
    public float beamLightRange = 22f;
    [Tooltip("地面や鎧に落ちる照り返しの色")]
    public Color beamLightColor = new Color(1.0f, 0.72f, 0.15f);

    [Header("=== レイヤー設定 ===")]
    [Tooltip("柱や壁（DefaultやEnvironmentなど）のレイヤーを指定")]
    public LayerMask obstacleMask = ~0;
    [Tooltip("プレイヤーのレイヤー（またはTagチェック用）")]
    public LayerMask playerMask = ~0;

    private float laserRadius = 1.0f;
    private float laserDistance = 100f;
    private float laserDamagePerSecond = 40f;
    private Light beamLight;

    void Awake()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        SetupLineRenderer(lineRenderer, true);

        // 芯（Core）用のLineRendererが未設定なら子オブジェクトとして自動生成
        if (coreLineRenderer == null)
        {
            GameObject coreObj = new GameObject("LaserCore");
            coreObj.transform.SetParent(transform, false);
            coreLineRenderer = coreObj.AddComponent<LineRenderer>();
        }
        SetupLineRenderer(coreLineRenderer, false);

        // 地面と周囲を照らす照り返し用ライトを自動生成
        GameObject lightObj = new GameObject("LaserDynamicLight");
        lightObj.transform.SetParent(transform, false);
        beamLight = lightObj.AddComponent<Light>();
        beamLight.type = LightType.Point;
        beamLight.color = beamLightColor;
        beamLight.intensity = beamLightIntensity;
        beamLight.range = beamLightRange;
        beamLight.enabled = false;

        if (muzzleFlashEffect != null)
        {
            muzzleFlashEffect.gameObject.SetActive(false);
        }
    }

    private void SetupLineRenderer(LineRenderer lr, bool isOuter)
    {
        if (lr == null) return;

        // 加算合成（Additive）シェーダーを割り当てて光の重なりを作る
        if (lr.material == null || lr.material.name.Contains("Default"))
        {
            Shader additiveShader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (additiveShader == null) additiveShader = Shader.Find("Particles/Standard Unlit");
            if (additiveShader == null) additiveShader = Shader.Find("Sprites/Default");
            lr.material = new Material(additiveShader);
        }

        // 先端を丸くしてビームの切り口を自然にする
        lr.numCapVertices = 8;
        lr.numCornerVertices = 4;
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.enabled = false;
    }

    /// <summary>
    /// レーザーを発射・回転させる
    /// </summary>
    public void FireLaser(float chargeTime, float duration, float radius, float distance, float damage, float sweepAngle = 0f)
    {
        laserRadius = radius;
        laserDistance = distance;
        laserDamagePerSecond = damage;

        DisableLaser();
        StartCoroutine(LaserRoutine(chargeTime, duration, sweepAngle));
    }

    /// <summary>
    /// レーザーを安全に停止・非表示化する
    /// </summary>
    public void DisableLaser()
    {
        StopAllCoroutines();
        if (lineRenderer != null) lineRenderer.enabled = false;
        if (coreLineRenderer != null) coreLineRenderer.enabled = false;
        if (beamLight != null) beamLight.enabled = false;
        if (muzzleFlashEffect != null) muzzleFlashEffect.gameObject.SetActive(false);
    }

    private IEnumerator LaserRoutine(float warningTime, float duration, float sweepAngle)
    {
        if (lineRenderer == null) lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null) yield break;

        lineRenderer.enabled = true;
        lineRenderer.positionCount = 2;

        if (coreLineRenderer != null)
        {
            coreLineRenderer.enabled = true;
            coreLineRenderer.positionCount = 2;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        if (playerObj != null)
        {
            Vector3 targetPos = playerObj.transform.position + Vector3.up * 0.8f;
            Vector3 targetDir = (targetPos - transform.position).normalized;
            if (targetDir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(targetDir);
            }
        }

        Quaternion startWorldRot = transform.rotation;

        // ----------------------------------------------------
        // 1. 予兆（赤い警告線＋収束する光）
        // ----------------------------------------------------
        ApplyBeamStyle(0.18f, 0.05f, warningColor, coreAttackColor);

        if (muzzleFlashEffect != null)
        {
            muzzleFlashEffect.gameObject.SetActive(true);
        }

        float timer = 0f;
        while (timer < warningTime)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / Mathf.Max(0.01f, warningTime));

            // 発射直前に向かって警告線が細く鋭く収束する演出
            float warnWidth = Mathf.Lerp(0.35f, 0.08f, t);
            ApplyBeamStyle(warnWidth, warnWidth * 0.3f, warningColor, coreAttackColor);

            if (muzzleFlashEffect != null)
            {
                muzzleFlashEffect.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.2f, t);
            }

            transform.rotation = startWorldRot;
            UpdateLinePositions(laserRadius);
            yield return null;
        }

        // ----------------------------------------------------
        // 2. 本照射（2層極太レーザー＋脈動＋照り返し＋薙ぎ払い）
        // ----------------------------------------------------
        HashSet<PlayerController> hitPlayersThisBeam = new HashSet<PlayerController>();
        float forwardOffset = laserRadius + 0.6f;

        timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / duration);

            if (Mathf.Abs(sweepAngle) > 0.1f)
            {
                float currentAngle = Mathf.Lerp(0f, sweepAngle, progress);
                transform.rotation = startWorldRot * Quaternion.Euler(0, currentAngle, 0);
            }

            // エネルギーの脈動（細かく震えて高出力感を出す）＋終了間際の減衰フェード
            float fadeOut = (progress > 0.85f) ? Mathf.InverseLerp(1.0f, 0.85f, progress) : 1.0f;
            float pulse = 1.0f + Mathf.Sin(timer * pulseSpeed) * pulseAmplitude;

            float baseOuterWidth = (laserRadius * 2.2f) * pulse * fadeOut;
            float baseCoreWidth = (laserRadius * 0.75f) * pulse * fadeOut;

            ApplyBeamStyle(baseOuterWidth, baseCoreWidth, outerAttackColor, coreAttackColor);

            if (muzzleFlashEffect != null)
            {
                muzzleFlashEffect.localScale = Vector3.one * (laserRadius * 1.8f * pulse * fadeOut);
            }

            float currentHitDistance = UpdateLinePositions(laserRadius);

            // 地面と主人公に黄金の照り返し（リアルタイムライト）を落とす
            if (beamLight != null && beamLightIntensity > 0f)
            {
                beamLight.enabled = true;
                beamLight.color = beamLightColor;
                Vector3 beamEnd = transform.position + transform.forward * currentHitDistance;
                beamLight.transform.position = Vector3.Lerp(transform.position, beamEnd, 0.45f) + Vector3.up * 1.5f;
                beamLight.intensity = beamLightIntensity * pulse * fadeOut;
                beamLight.range = beamLightRange;
            }

            Vector3 castOrigin = transform.position + transform.forward * forwardOffset;
            float adjustedDistance = Mathf.Max(0.1f, currentHitDistance - forwardOffset);

            RaycastHit[] hits = Physics.SphereCastAll(
                castOrigin,
                laserRadius,
                transform.forward,
                adjustedDistance,
                playerMask,
                QueryTriggerInteraction.Ignore
            );

            foreach (var hit in hits)
            {
                if (hit.collider.CompareTag("Player"))
                {
                    Vector3 toPlayer = hit.collider.transform.position - transform.position;
                    if (Vector3.Dot(transform.forward, toPlayer) <= 0f)
                    {
                        continue;
                    }

                    PlayerController player = hit.collider.GetComponent<PlayerController>();
                    if (player == null) player = hit.collider.GetComponentInParent<PlayerController>();

                    if (player != null)
                    {
                        if (!hitPlayersThisBeam.Contains(player))
                        {
                            hitPlayersThisBeam.Add(player);
                            player.TakeDamage(laserDamagePerSecond);
                        }
                    }
                }
            }

            yield return null;
        }

        // ----------------------------------------------------
        // 3. 終了処理
        // ----------------------------------------------------
        DisableLaser();
        transform.rotation = startWorldRot;

        BossRadagonAI bossAI = GetComponentInParent<BossRadagonAI>();
        if (bossAI != null)
        {
            bossAI.OnLaserFinished();
        }
    }

    private void ApplyBeamStyle(float outerWidth, float coreWidth, Color outerCol, Color coreCol)
    {
        if (lineRenderer != null)
        {
            // 発射口側を少し太く、先端に向かってわずかに絞ると遠近感と迫力が出る
            lineRenderer.startWidth = outerWidth * 1.15f;
            lineRenderer.endWidth = outerWidth * 0.85f;
            lineRenderer.startColor = outerCol;
            lineRenderer.endColor = new Color(outerCol.r, outerCol.g, outerCol.b, outerCol.a * 0.5f);
        }

        if (coreLineRenderer != null)
        {
            coreLineRenderer.startWidth = coreWidth * 1.1f;
            coreLineRenderer.endWidth = coreWidth * 0.8f;
            coreLineRenderer.startColor = coreCol;
            coreLineRenderer.endColor = coreCol;
        }
    }

    private float UpdateLinePositions(float checkRadius)
    {
        if (lineRenderer == null) return laserDistance;

        Vector3 startPos = transform.position;
        Vector3 endPos = transform.position + transform.forward * laserDistance;
        float finalDistance = laserDistance;

        RaycastHit hit;
        bool isHit = false;

        if (checkRadius > 0.01f)
        {
            isHit = Physics.SphereCast(transform.position, checkRadius, transform.forward, out hit, laserDistance, obstacleMask, QueryTriggerInteraction.Ignore);
        }
        else
        {
            isHit = Physics.Raycast(transform.position, transform.forward, out hit, laserDistance, obstacleMask, QueryTriggerInteraction.Ignore);
        }

        if (isHit)
        {
            if (!hit.collider.CompareTag("Player") && !hit.collider.CompareTag("Boss"))
            {
                finalDistance = hit.distance;
                endPos = transform.position + transform.forward * finalDistance;
            }
        }

        lineRenderer.SetPosition(0, startPos);
        lineRenderer.SetPosition(1, endPos);

        if (coreLineRenderer != null)
        {
            coreLineRenderer.SetPosition(0, startPos);
            coreLineRenderer.SetPosition(1, endPos);
        }

        return finalDistance;
    }
}