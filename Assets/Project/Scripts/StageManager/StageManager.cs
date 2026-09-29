using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StageManager : MonoBehaviour
{
    [Header("生成するプレハブ")]
    public GameObject arenaPrefab;
    public GameObject playerPrefab;
    public GameObject bossPrefab;
    public GameObject uiCanvasPrefab; // ★Canvasプレハブを登録する枠

    [Header("生成位置")]
    public Vector3 arenaPosition = new Vector3(0, -0.5f, 0);
    public Vector3 playerPosition = new Vector3(0, 1f, 0);
    public Vector3 bossPosition = new Vector3(0, 1f, 10f);

    void Awake()
    {
        Slider healthSlider = null;
        Slider staminaSlider = null;
        TextMeshProUGUI estusText = null;
        Slider bossHPSlider = null; // ★ボスのHPスライダー用変数を追加

        // 1. UI Canvas を動的生成し、UI要素を取得する
        if (uiCanvasPrefab != null)
        {
            GameObject canvasObj = Instantiate(uiCanvasPrefab);

            // Canvas の子要素から各 UI コンポーネントを名前で検索して取得
            Transform hpObj = canvasObj.transform.Find("HPBar");
            if (hpObj != null) healthSlider = hpObj.GetComponent<Slider>();

            Transform staminaObj = canvasObj.transform.Find("StaminaBar");
            if (staminaObj != null) staminaSlider = staminaObj.GetComponent<Slider>();

            Transform potionObj = canvasObj.transform.Find("Potion");
            if (potionObj != null) estusText = potionObj.GetComponent<TextMeshProUGUI>();

            // ★ BossHPBar を取得する処理を追加！
            Transform bossHPObj = canvasObj.transform.Find("BossHPBar");
            if (bossHPObj != null) bossHPSlider = bossHPObj.GetComponent<Slider>();
        }

        // 2. Arena（足場）を生成
        if (arenaPrefab != null)
        {
            Instantiate(arenaPrefab, arenaPosition, Quaternion.identity);
        }

        // 3. Player（プレイヤー）を生成
        if (playerPrefab != null)
        {
            GameObject playerObj = Instantiate(playerPrefab, playerPosition, Quaternion.identity);

            PlayerController playerCtrl = playerObj.GetComponent<PlayerController>();
            if (playerCtrl != null)
            {
                playerCtrl.healthSlider = healthSlider;
                playerCtrl.staminaSlider = staminaSlider;
                playerCtrl.estusText = estusText;
            }

            if (Camera.main != null)
            {
                ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
                if (cam != null)
                {
                    cam.target = playerObj.transform;
                }
            }
        }

        // 4. Boss（ボス）を生成＆UIをセット
        if (bossPrefab != null)
        {
            GameObject bossObj = Instantiate(bossPrefab, bossPosition, Quaternion.identity);

            // ★ ボスの BossHealth スクリプトに HP バーを渡す処理を追加！
            BossHealth bossHealth = bossObj.GetComponent<BossHealth>();
            if (bossHealth != null && bossHPSlider != null)
            {
                bossHealth.bossHPSlider = bossHPSlider;
            }
        }
    }
}