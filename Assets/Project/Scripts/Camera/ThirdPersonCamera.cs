using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;           // 追従する対象（Player）
    public float distance = 4.0f;      // プレイヤーからの距離
    public float height = 2.0f;        // プレイヤーからの高さ
    public float xSpeed = 120.0f;      // マウス左右移動の感度
    public float ySpeed = 80.0f;       // マウス上下移動の感度

    public float yMinLimit = -20f;     // 見下ろす限界（角度）
    public float yMaxLimit = 80f;      // 見上げる限界（角度）

    private float x = 0.0f;
    private float y = 0.0f;

    void Start()
    {
        // 角度の初期値を現在のカメラの向きから取得
        Vector3 angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;

        // カーソルを画面中央にロックして消す（ESCキーで解除可能）
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // マウスの移動量を取得して回転角度に加算
        x += Input.GetAxis("Mouse X") * xSpeed * Time.deltaTime;
        y -= Input.GetAxis("Mouse Y") * ySpeed * Time.deltaTime;

        // 上下の回転角度に制限をかける（地面を突き抜けたり真上を超えないように）
        y = Mathf.Clamp(y, yMinLimit, yMaxLimit);

        // 回転と位置の計算
        Quaternion rotation = Quaternion.Euler(y, x, 0);
        Vector3 targetPosition = target.position + Vector3.up * height;
        Vector3 position = targetPosition - (rotation * Vector3.forward * distance);

        // カメラの位置と向きを適用
        transform.rotation = rotation;
        transform.position = position;
    }
}