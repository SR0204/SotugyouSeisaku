using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // ★追加

public class StageManager : MonoBehaviour
{
    [Header("生成するプレハブ")]
    public GameObject arenaPrefab;
    public GameObject playerPrefab;

    [Header("UI参照")]
    public Slider staminaSlider; // ★画面上のStaminaBarを登録する枠

    [Header("生成位置")]
    public Vector3 arenaPosition = new Vector3(0, -0.5f, 0);
    public Vector3 playerPosition = new Vector3(0, 1f, 0);

    void Awake()
    {
        // 1. Arena（足場）を生成
        if (arenaPrefab != null)
        {
            Instantiate(arenaPrefab, arenaPosition, Quaternion.identity);
        }

        // 2. Player（プレイヤー）を生成
        if (playerPrefab != null)
        {
            GameObject playerObj = Instantiate(playerPrefab, playerPosition, Quaternion.identity);

            // プレイヤーのスクリプトに StaminaBar を渡す（★自動セット！）
            PlayerController playerCtrl = playerObj.GetComponent<PlayerController>();
            if (playerCtrl != null && staminaSlider != null)
            {
                playerCtrl.staminaSlider = staminaSlider;
            }

            // 3. カメラの追従対象にセット
            if (Camera.main != null)
            {
                ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
                if (cam != null)
                {
                    cam.target = playerObj.transform;
                }
            }
        }
    }
}