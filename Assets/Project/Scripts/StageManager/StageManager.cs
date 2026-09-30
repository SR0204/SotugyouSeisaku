using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StageManager : MonoBehaviour
{
    [Header("プレハブ設定")]
    public GameObject arenaPrefab;
    public GameObject playerPrefab;
    public GameObject[] bossPrefabs; // 選択可能なボス一覧
    public GameObject uiCanvasPrefab;
    public GameObject restAreaPrefab; // 休息ポイント（かがり火等）のプレハブ（任意）

    [Header("生成位置")]
    public Vector3 arenaPosition = new Vector3(0, -0.5f, 0);
    public Vector3 playerPosition = new Vector3(0, 1f, 0);
    public Vector3 bossPosition = new Vector3(0, 1f, 10f);
    public Vector3 restPointPosition = new Vector3(0, 1f, 3f);

    private GameObject currentBossInstance;
    private GameObject currentRestAreaInstance;
    private PlayerController playerCtrl;
    private Slider bossHPSlider;
    private GameObject bossSelectPanel;

    private bool isStageActive = false;

    void Start()
    {
        Slider healthSlider = null;
        Slider staminaSlider = null;
        TextMeshProUGUI estusText = null;

        // 1. UI Canvas を生成
        if (uiCanvasPrefab != null)
        {
            GameObject canvasObj = Instantiate(uiCanvasPrefab);

            Transform hpObj = canvasObj.transform.Find("HPBar");
            if (hpObj != null) healthSlider = hpObj.GetComponent<Slider>();

            Transform staminaObj = canvasObj.transform.Find("StaminaBar");
            if (staminaObj != null) staminaSlider = staminaObj.GetComponent<Slider>();

            Transform potionObj = canvasObj.transform.Find("Potion");
            if (potionObj != null) estusText = potionObj.GetComponent<TextMeshProUGUI>();

            Transform bossHPObj = canvasObj.transform.Find("BossHPBar");
            if (bossHPObj != null) bossHPSlider = bossHPObj.GetComponent<Slider>();

            // ★ ここから下を書き換え！
            Transform panelObj = canvasObj.transform.Find("BossSelectPanel");
            if (panelObj != null)
            {
                bossSelectPanel = panelObj.gameObject;

                // ボタン1（Boss1Button）を探してクリックイベントをコードから自動紐付け
                Transform btn1Obj = panelObj.Find("Boss1Button");
                if (btn1Obj != null)
                {
                    Button btn1 = btn1Obj.GetComponent<Button>();
                    if (btn1 != null)
                    {
                        btn1.onClick.RemoveAllListeners();
                        btn1.onClick.AddListener(() => SelectAndStartBoss(0)); // 1体目のボス生成
                    }
                }

                // ボタン2を増やす場合は、パネル内に「Boss2Button」を作って以下を有効化
                /*
                Transform btn2Obj = panelObj.Find("Boss2Button");
                if (btn2Obj != null)
                {
                    Button btn2 = btn2Obj.GetComponent<Button>();
                    if (btn2 != null)
                    {
                        btn2.onClick.RemoveAllListeners();
                        btn2.onClick.AddListener(() => SelectAndStartBoss(1)); // 2体目のボス生成
                    }
                }
                */

                bossSelectPanel.SetActive(false); // 初期状態は非表示
            }
        }

        // 2. Arena 生成
        if (arenaPrefab != null)
        {
            Instantiate(arenaPrefab, arenaPosition, Quaternion.identity);
        }

        // 3. Player 生成
        if (playerPrefab != null)
        {
            GameObject playerObj = Instantiate(playerPrefab, playerPosition, Quaternion.identity);
            playerCtrl = playerObj.GetComponent<PlayerController>();

            if (playerCtrl != null)
            {
                playerCtrl.healthSlider = healthSlider;
                playerCtrl.staminaSlider = staminaSlider;
                playerCtrl.estusText = estusText;
            }

            if (Camera.main != null)
            {
                ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
                if (cam != null) cam.target = playerObj.transform;
            }
        }

        // 最初に休息モード（ボス選択画面）を開く
        ShowRestArea();
    }

    void Update()
    {
        // 戦闘中で、ボスが撃破された場合
        if (isStageActive && currentBossInstance == null)
        {
            OnBossDefeated();
        }
    }

    // ★ ボス撃破時の処理（休息モードへ移行）
    void OnBossDefeated()
    {
        isStageActive = false;

        if (bossHPSlider != null)
        {
            bossHPSlider.gameObject.SetActive(false);
        }

        Debug.Log("ボス撃破！休息エリアを開放します。");
        ShowRestArea();
    }

    // ★ 休息エリア＆ボス選択UIの表示
    public void ShowRestArea()
    {
        // 休息オブジェクトの生成（かがり火やポータルなど）
        if (restAreaPrefab != null && currentRestAreaInstance == null)
        {
            currentRestAreaInstance = Instantiate(restAreaPrefab, restPointPosition, Quaternion.identity);
        }

        // ボス選択UIを表示
        if (bossSelectPanel != null)
        {
            bossSelectPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None; // マウスカーソルを表示
            Cursor.visible = true;
        }
    }

    // ★ ボタンから呼び出すボス選択用メソッド（インデックス指定）
    public void SelectAndStartBoss(int bossIndex)
    {
        if (bossIndex < 0 || bossIndex >= bossPrefabs.Length) return;

        // UIを隠す
        if (bossSelectPanel != null)
        {
            bossSelectPanel.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked; // カーソルをロック
            Cursor.visible = false;
        }

        // 休息オブジェクトを消去
        if (currentRestAreaInstance != null)
        {
            Destroy(currentRestAreaInstance);
        }

        // 指定されたボスを生成
        if (bossPrefabs[bossIndex] != null)
        {
            currentBossInstance = Instantiate(bossPrefabs[bossIndex], bossPosition, Quaternion.identity);

            BossHealth bossHealth = currentBossInstance.GetComponent<BossHealth>();
            if (bossHealth != null && bossHPSlider != null)
            {
                bossHPSlider.gameObject.SetActive(true);
                bossHealth.bossHPSlider = bossHPSlider;
            }
        }

        isStageActive = true;
    }

    // ★ 休息（全回復）メソッド（かがり火などで使用）
    public void RestAndHeal()
    {
        if (playerCtrl != null)
        {
            playerCtrl.currentHealth = playerCtrl.maxHealth;
            playerCtrl.currentEstusCount = playerCtrl.maxEstusCount;
            playerCtrl.TakeDamage(0); // UI同期
            Debug.Log("休息完了：HPとエスト瓶が全回復しました！");
        }
    }
}