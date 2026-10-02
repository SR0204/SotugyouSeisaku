using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("追従設定")]
    public Transform target;
    public float distance = 4.0f;
    public float height = 2.0f;
    public float xSpeed = 120.0f;
    public float ySpeed = 80.0f;

    public float yMinLimit = -20f;
    public float yMaxLimit = 80f;

    [Header("ロックオン設定")]
    public KeyCode lockOnKey = KeyCode.Mouse2;
    public bool isLockedOn = false;
    public Transform lockOnTarget;
    public Image lockOnIcon; // ★ UIのロックオンアイコン参照
    public Vector3 lockOnOffset = new Vector3(0, 1.0f, 0); // ボスのどの位置（高さ）にアイコンを出すか

    private float x = 0.0f;
    private float y = 0.0f;
    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        Vector3 angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(lockOnKey) || Input.GetKeyDown(KeyCode.R))
        {
            ToggleLockOn();
        }

        if (isLockedOn && lockOnTarget == null)
        {
            ToggleLockOn(); // ターゲットが消えたら解除
        }

        if (!isLockedOn)
        {
            x += Input.GetAxis("Mouse X") * xSpeed * Time.deltaTime;
            y -= Input.GetAxis("Mouse Y") * ySpeed * Time.deltaTime;
            y = Mathf.Clamp(y, yMinLimit, yMaxLimit);
        }

        // ★ ロックオンアイコンの追従制御
        UpdateLockOnIcon();
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (isLockedOn && lockOnTarget != null)
        {
            Vector3 dirToTarget = lockOnTarget.position - target.position;
            dirToTarget.y = 0;

            if (dirToTarget != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(dirToTarget);

                Vector3 position = target.position - (targetRotation * Vector3.forward * distance) + Vector3.up * height;
                transform.position = Vector3.Lerp(transform.position, position, Time.deltaTime * 10f);

                Vector3 lookAtPoint = Vector3.Lerp(target.position + Vector3.up * 1.5f, lockOnTarget.position + Vector3.up * 1.0f, 0.3f);
                transform.LookAt(lookAtPoint);

                target.rotation = Quaternion.Slerp(target.rotation, targetRotation, Time.deltaTime * 10f);

                Vector3 angles = transform.eulerAngles;
                x = angles.y;
                y = angles.x;
            }
        }
        else
        {
            Quaternion rotation = Quaternion.Euler(y, x, 0);
            Vector3 targetPosition = target.position + Vector3.up * height;
            Vector3 position = targetPosition - (rotation * Vector3.forward * distance);

            transform.rotation = rotation;
            transform.position = position;
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
            BossHealth boss = FindObjectOfType<BossHealth>();
            if (boss != null)
            {
                lockOnTarget = boss.transform;
                isLockedOn = true;
                if (lockOnIcon != null) lockOnIcon.gameObject.SetActive(true);
            }
        }
    }

    // ★ 3D座標を2D画面座標に変換してアイコンを追従させる処理
    void UpdateLockOnIcon()
    {
        if (lockOnIcon == null) return;

        if (isLockedOn && lockOnTarget != null)
        {
            if (!lockOnIcon.gameObject.activeSelf)
            {
                lockOnIcon.gameObject.SetActive(true);
            }

            // ボスの位置 + オフセットをスクリーン座標に変換
            Vector3 targetWorldPos = lockOnTarget.position + lockOnOffset;
            Vector3 screenPos = cam.WorldToScreenPoint(targetWorldPos);

            // カメラの画面内（前方）にある場合のみ表示
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