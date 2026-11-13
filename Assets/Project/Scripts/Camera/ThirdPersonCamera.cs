using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ThirdPersonCamera : MonoBehaviour
{
    public static ThirdPersonCamera Instance; // ★ 追加：どこからでも呼び出せるようにする

    [Header("追従設定")]
    public Transform target;
    public float distance = 4.0f;
    public float height = 2.0f;
    public float xSpeed = 120.0f;
    public float ySpeed = 80.0f;

    public float yMinLimit = -20f;
    public float yMaxLimit = 80f;

    [Header("エルデンリング風・落下カメラ設定")]
    [Tooltip("通常時のY軸追従スピード")]
    public float verticalSmoothSpeed = 5.0f;
    [Tooltip("落下検知しきい値（この速度以上で急速落下するとY軸追従を停止して見下ろす）")]
    public float fallThresholdVelocity = -8.0f;

    [Header("ロックオン設定")]
    public KeyCode lockOnKey = KeyCode.Mouse2;
    public bool isLockedOn = false;
    public Transform lockOnTarget;
    public Image lockOnIcon;

    [Tooltip("ロックオンアイコンの高さ")]
    public Vector3 lockOnOffset = new Vector3(0, 1.5f, 0);

    [Tooltip("ロックオン可能な最大距離")]
    public float maxLockOnDistance = 25.0f;

    [Header("★ ロックオン画角・距離調整設定")]
    [Tooltip("ロックオン時の最小カメラ距離（超近接時）")]
    public float minLockOnDistance = 4.5f;
    [Tooltip("ロックオン時の最大カメラ距離（離れている時）")]
    public float maxLockOnCameraDistance = 7.0f;
    [Tooltip("ロックオン時のカメラの高さ補正")]
    public float lockOnCameraHeight = 2.5f;

    private float x = 0.0f;
    private float y = 15.0f; // ★ 初期角度を15度に明示設定
    private Camera cam;

    private float currentTargetY;
    private CharacterController targetController;
    private Vector3 lastTargetPos;

    // ★ 追加：カメラシェイク用変数
    private float shakeTimer = 0f;
    private float shakeMagnitude = 0f;

    void Start()
    {
        Instance = this; // ★ 追加
        cam = GetComponent<Camera>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (target != null)
        {
            InitCameraPosition();
        }
    }

    // ★ ゲーム開始時・ターゲット切り替え時のカメラ角度・位置初期化
    public void InitCameraPosition()
    {
        if (target == null) return;

        currentTargetY = target.position.y;
        lastTargetPos = target.position;
        targetController = target.GetComponent<CharacterController>();

        // 1. 周囲のボス（敵）を検索して、始まった瞬間に敵の方を向かせる＆自動ロックオン
        BossHealth enemy = FindNearestEnemy();
        if (enemy != null)
        {
            lockOnTarget = enemy.transform;
            isLockedOn = true;
            if (lockOnIcon != null) lockOnIcon.gameObject.SetActive(true);

            Vector3 dirToEnemy = enemy.transform.position - target.position;
            dirToEnemy.y = 0;
            if (dirToEnemy != Vector3.zero)
            {
                Quaternion enemyRot = Quaternion.LookRotation(dirToEnemy);
                x = enemyRot.eulerAngles.y;
                target.rotation = enemyRot;
            }
        }
        else
        {
            isLockedOn = false;
            lockOnTarget = null;
            if (lockOnIcon != null) lockOnIcon.gameObject.SetActive(false);
            x = target.eulerAngles.y;
        }

        y = 15.0f; // ★ 15度（自然な見下ろし角度）に固定

        // 2. 初回フレームの位置計算
        Quaternion rotation = Quaternion.Euler(y, x, 0);
        Vector3 targetPosition = target.position + Vector3.up * height;
        transform.position = targetPosition - (rotation * Vector3.forward * distance);
        transform.rotation = rotation;
    }

    void Update()
    {
        if (Input.GetKeyDown(lockOnKey) || Input.GetKeyDown(KeyCode.R))
        {
            ToggleLockOn();
        }

        if (isLockedOn && lockOnTarget == null)
        {
            ToggleLockOn();
        }

        if (!isLockedOn)
        {
            x += Input.GetAxis("Mouse X") * xSpeed * Time.deltaTime;
            y -= Input.GetAxis("Mouse Y") * ySpeed * Time.deltaTime;
            y = Mathf.Clamp(y, yMinLimit, yMaxLimit);
        }

        UpdateLockOnIcon();
    }

    void LateUpdate()
    {
        if (target == null) return;

        // プレイヤーの落下速度（Y軸速度）を計算
        float verticalVelocity = (target.position.y - lastTargetPos.y) / Time.deltaTime;
        bool isGrounded = (targetController != null) ? targetController.isGrounded : true;

        bool isFallingFast = !isGrounded && verticalVelocity < fallThresholdVelocity;

        if (isFallingFast)
        {
            currentTargetY = Mathf.Lerp(currentTargetY, target.position.y, Time.deltaTime * 0.5f);
        }
        else
        {
            currentTargetY = Mathf.Lerp(currentTargetY, target.position.y, Time.deltaTime * verticalSmoothSpeed);
        }

        lastTargetPos = target.position;

        // ==========================================
        // ★ ロックオン時のカメラ位置計算
        // ==========================================
        if (isLockedOn && lockOnTarget != null)
        {
            Vector3 playerPos = new Vector3(target.position.x, currentTargetY, target.position.z);
            Vector3 enemyPos = lockOnTarget.position;

            Vector3 dirToEnemy = enemyPos - playerPos;
            float currentDistToEnemy = dirToEnemy.magnitude;
            dirToEnemy.y = 0;
            dirToEnemy.Normalize();

            if (dirToEnemy != Vector3.zero)
            {
                float targetCamDistance = Mathf.Lerp(minLockOnDistance, maxLockOnCameraDistance, currentDistToEnemy / maxLockOnDistance);
                Vector3 centerPoint = Vector3.Lerp(playerPos + Vector3.up * 1.2f, enemyPos + lockOnOffset, 0.4f);

                Quaternion targetRotation = Quaternion.LookRotation(dirToEnemy);
                Vector3 desiredCameraPos = playerPos - (targetRotation * Vector3.forward * targetCamDistance) + Vector3.up * lockOnCameraHeight;

                transform.position = Vector3.Lerp(transform.position, desiredCameraPos, Time.deltaTime * 8f);
                transform.LookAt(centerPoint);

                target.rotation = Quaternion.Slerp(target.rotation, targetRotation, Time.deltaTime * 10f);

                // ★ 安全な角度同期（オイラー角壊れ防止）
                Vector3 angles = transform.eulerAngles;
                x = angles.y;
                float clampedX = angles.x > 180f ? angles.x - 360f : angles.x;
                y = Mathf.Clamp(clampedX, yMinLimit, yMaxLimit);
            }
        }
        // ==========================================
        // 非ロックオン時のカメラ（通常時）
        // ==========================================
        else
        {
            Quaternion rotation = Quaternion.Euler(y, x, 0);

            Vector3 smoothTargetPos = new Vector3(target.position.x, currentTargetY, target.position.z);
            Vector3 targetPosition = smoothTargetPos + Vector3.up * height;
            Vector3 position = targetPosition - (rotation * Vector3.forward * distance);

            transform.position = position;

            // ★ LookAt で y の値を壊さないように制御
            if (isFallingFast)
            {
                transform.LookAt(target.position + Vector3.up * 0.5f);
                x = transform.eulerAngles.y;
            }
            else
            {
                transform.rotation = rotation;
            }
        }

        // ==========================================
        // ★ 追加：カメラシェイク（画面揺れ）処理
        // ==========================================
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            Vector3 shakeOffset = Random.insideUnitSphere * shakeMagnitude;
            transform.position += shakeOffset;
        }
    }

    /// <summary>
    /// ★ 追加：外部からカメラを揺らすメソッド
    /// </summary>
    public void ShakeCamera(float duration = 0.15f, float magnitude = 0.25f)
    {
        shakeTimer = duration;
        shakeMagnitude = magnitude;
    }

    // ★ 開始時に自動で最寄りの敵をロックオンするメソッド
    public void StartLockOnNearestEnemy()
    {
        BossHealth nearestEnemy = FindNearestEnemy();
        if (nearestEnemy != null)
        {
            lockOnTarget = nearestEnemy.transform;
            isLockedOn = true;
            if (lockOnIcon != null) lockOnIcon.gameObject.SetActive(true);
        }
    }

    void ToggleLockOn()
    {
        if (isLockedOn)
        {
            isLockedOn = false;
            lockOnTarget = null;
            if (lockOnIcon != null) lockOnIcon.gameObject.SetActive(false);
        }
        else
        {
            StartLockOnNearestEnemy();
        }
    }

    private BossHealth FindNearestEnemy()
    {
        BossHealth[] enemies = FindObjectsOfType<BossHealth>();
        BossHealth nearest = null;
        float minDistance = maxLockOnDistance;

        foreach (var enemy in enemies)
        {
            if (enemy == null || target == null) continue;

            float dist = Vector3.Distance(target.position, enemy.transform.position);

            if (dist < minDistance)
            {
                minDistance = dist;
                nearest = enemy;
            }
        }

        return nearest;
    }

    void UpdateLockOnIcon()
    {
        if (lockOnIcon == null) return;

        if (isLockedOn && lockOnTarget != null)
        {
            if (!lockOnIcon.gameObject.activeSelf)
            {
                lockOnIcon.gameObject.SetActive(true);
            }

            Vector3 targetWorldPos = lockOnTarget.position + lockOnOffset;
            Vector3 screenPos = cam.WorldToScreenPoint(targetWorldPos);

            if (screenPos.z > 0)
            {
                lockOnIcon.transform.position = screenPos;
            }
            else
            {
                lockOnIcon.gameObject.SetActive(false);
            }
        }
        else
        {
            if (lockOnIcon.gameObject.activeSelf)
            {
                lockOnIcon.gameObject.SetActive(false);
            }
        }
    }
}