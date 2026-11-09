using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StageManager : MonoBehaviour
{
    [Header("プレハブ設定")]
    [Tooltip("通常のアリーナプレハブ（ランダム選出用）")]
    public GameObject[] arenaPrefabs;

    [Tooltip("特定ボス専用のアリーナ（例: コロシアムなど）。空欄なら通常アリーナからランダム選出")]
    public GameObject[] bossSpecificArenas;

    public GameObject restAreaPrefab;           // 休息専用エリア
    public GameObject playerPrefab;
    public GameObject[] bossPrefabs;
    public GameObject uiCanvasPrefab;

    [Header("生成位置設定")]
    public Vector3 stagePosition = Vector3.zero;        // ★ (0, 0, 0) に統一
    public Vector3 playerPosition = Vector3.zero;       // ★ (0, 0, 0) に統一
    public Vector3 bossPosition = new Vector3(0, 0, 10f); // ★ Y=0 に統一（Z軸のみ距離を保つ）

    private GameObject currentStageInstance;
    private GameObject currentBossInstance;
    private GameObject currentPlayerInstance;
    private PlayerController playerCtrl;

    // UI参照用
    private Slider healthSlider;
    private Slider staminaSlider;
    private TextMeshProUGUI estusText;
    private Slider bossHPSlider;
    private GameObject bossSelectPanel;
    private TextMeshProUGUI gameOverText;

    private bool isStageActive = false;

    void Start()
    {
        InitializeStage();
    }

    void InitializeStage()
    {
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

            Transform gameOverObj = canvasObj.transform.Find("GameOverText");
            if (gameOverObj != null)
            {
                gameOverText = gameOverObj.GetComponent<TextMeshProUGUI>();
                gameOverText.gameObject.SetActive(false);
            }

            Transform lockOnIconObj = canvasObj.transform.Find("LockOnIcon");
            if (lockOnIconObj != null && Camera.main != null)
            {
                ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
                if (cam != null)
                {
                    cam.lockOnIcon = lockOnIconObj.GetComponent<Image>();
                }
            }

            // ボス選択ボタン自動登録
            Transform panelObj = canvasObj.transform.Find("BossSelectPanel");
            if (panelObj != null)
            {
                bossSelectPanel = panelObj.gameObject;

                for (int i = 0; i < bossPrefabs.Length; i++)
                {
                    int bossIndex = i;
                    string buttonName = $"Boss{bossIndex + 1}Button";

                    Transform btnObj = panelObj.Find(buttonName);
                    if (btnObj != null)
                    {
                        Button btn = btnObj.GetComponent<Button>();
                        if (btn != null)
                        {
                            btn.onClick.RemoveAllListeners();
                            btn.onClick.AddListener(() => SelectAndStartBoss(bossIndex));
                        }
                    }
                }

                // ★ 起動時はボス選択UIを表示してマウスカーソルを解放
                bossSelectPanel.SetActive(true);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        // ★ 起動時はステージ・プレイヤーを生成せず、ボタン選択待ちにする（落下事故防止）
    }

    void SpawnPlayer(Slider hpSlider, Slider stSlider, TextMeshProUGUI estText)
    {
        if (currentPlayerInstance != null) Destroy(currentPlayerInstance);

        if (playerPrefab != null)
        {
            currentPlayerInstance = Instantiate(playerPrefab, playerPosition, Quaternion.identity);
            playerCtrl = currentPlayerInstance.GetComponent<PlayerController>();

            if (playerCtrl != null)
            {
                playerCtrl.healthSlider = hpSlider;
                playerCtrl.staminaSlider = stSlider;
                playerCtrl.estusText = estText;
            }

            // ★ カメラターゲットのセット ＋ 敵の方向を向かせるリセットを実行
            if (Camera.main != null)
            {
                ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
                if (cam != null)
                {
                    cam.target = currentPlayerInstance.transform;
                    cam.InitCameraPosition(); // ★ カメラアングルとプレイヤーの向きを初期化
                }
            }
        }
    }

    // ★ プレイヤーの安全接地テレポート（レイキャストの誤検知を排除）
    private void TeleportPlayer(Vector3 targetPos)
    {
        if (currentPlayerInstance == null) return;

        CharacterController cc = currentPlayerInstance.GetComponent<CharacterController>();
        Rigidbody rb = currentPlayerInstance.GetComponent<Rigidbody>();

        // 1. 位置更新を妨げるコンポーネントを一時オフ
        if (cc != null) cc.enabled = false;

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 2. 指定された座標（+0.2f 浮かせた高さ）に配置
        Vector3 finalPos = targetPos;
        finalPos.y += 0.2f;

        currentPlayerInstance.transform.position = finalPos;

        // 3. コンポーネントを再有効化
        if (cc != null) cc.enabled = true;
    }

    void Update()
    {
        if (isStageActive && currentBossInstance == null)
        {
            // ボスオブジェクトが破棄（Destroy）されたことを検知して発火
            OnBossDefeated();
        }
    }

    void OnBossDefeated()
    {
        isStageActive = false;

        if (bossHPSlider != null)
        {
            bossHPSlider.gameObject.SetActive(false);
        }

        Debug.Log("ボス撃破！余韻を残して休息エリアを表示します。");

        // ★ 撃破後、少しの間（2.5秒）勝利の余韻を持たせてから休息エリアに切り替える
        StartCoroutine(TransitionToRestAreaRoutine());
    }

    private IEnumerator TransitionToRestAreaRoutine()
    {
        yield return new WaitForSeconds(2.5f);
        ShowRestArea();
    }

    public void ShowRestArea()
    {
        if (currentStageInstance != null)
        {
            Destroy(currentStageInstance);
        }

        if (restAreaPrefab != null)
        {
            currentStageInstance = Instantiate(restAreaPrefab, stagePosition, Quaternion.identity);
            Physics.SyncTransforms(); // 地形物理の即時更新
        }

        // 休息エリア切替時はプレイヤー生成＆テレポート＆回復を行ってUIを表示
        SpawnPlayer(healthSlider, staminaSlider, estusText);
        TeleportPlayer(playerPosition);
        RestAndHeal();

        if (bossSelectPanel != null)
        {
            bossSelectPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // ★ ボス選択＆アリーナ生成（ボタン押下時に一括安全生成）
    public void SelectAndStartBoss(int bossIndex)
    {
        if (bossIndex < 0 || bossIndex >= bossPrefabs.Length) return;

        if (bossSelectPanel != null)
        {
            bossSelectPanel.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (currentStageInstance != null)
        {
            Destroy(currentStageInstance);
        }

        // 1. アリーナの決定と生成（床を最優先配置）
        GameObject selectedArenaPrefab = null;

        if (bossSpecificArenas != null && bossIndex < bossSpecificArenas.Length && bossSpecificArenas[bossIndex] != null)
        {
            selectedArenaPrefab = bossSpecificArenas[bossIndex];
        }
        else if (arenaPrefabs != null && arenaPrefabs.Length > 0)
        {
            int randomIndex = Random.Range(0, arenaPrefabs.Length);
            selectedArenaPrefab = arenaPrefabs[randomIndex];
        }

        if (selectedArenaPrefab != null)
        {
            currentStageInstance = Instantiate(selectedArenaPrefab, stagePosition, Quaternion.identity);
            Physics.SyncTransforms(); // ★ 生成したアリーナの物理コライダーを即時同期
        }

        // 2. 床が固定されてからプレイヤーを生成＆安全配置
        SpawnPlayer(healthSlider, staminaSlider, estusText);
        TeleportPlayer(playerPosition);

        // 3. ボス生成処理
        if (bossPrefabs[bossIndex] != null)
        {
            currentBossInstance = Instantiate(bossPrefabs[bossIndex], bossPosition, Quaternion.identity);

            // コンポーネントの一時停止（位置ずれ防止）
            CharacterController cc = currentBossInstance.GetComponent<CharacterController>();
            UnityEngine.AI.NavMeshAgent agent = currentBossInstance.GetComponent<UnityEngine.AI.NavMeshAgent>();
            Collider bossCol = currentBossInstance.GetComponent<Collider>();

            if (cc != null) cc.enabled = false;
            if (agent != null) agent.enabled = false;
            if (bossCol != null) bossCol.enabled = false;

            // 高さ補正
            Vector3 spawnPos = bossPosition;
            RaycastHit hit;
            if (Physics.Raycast(bossPosition + Vector3.up * 3f, Vector3.down, out hit, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                spawnPos.y = hit.point.y + 0.2f;
            }
            else
            {
                spawnPos.y = bossPosition.y + 0.2f;
            }

            currentBossInstance.transform.position = spawnPos;

            if (bossCol != null) bossCol.enabled = true;
            if (cc != null) cc.enabled = true;

            if (agent != null)
            {
                agent.Warp(spawnPos);
                agent.enabled = true;
            }

            // UI設定
            BossHealth bossHealth = currentBossInstance.GetComponent<BossHealth>();
            if (bossHealth != null && bossHPSlider != null)
            {
                bossHPSlider.gameObject.SetActive(true);
                bossHealth.bossHPSlider = bossHPSlider;
            }

            // カメラのターゲットセット＆敵方向自動ロックオン
            if (Camera.main != null)
            {
                ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
                if (cam != null)
                {
                    cam.target = currentPlayerInstance.transform;
                    cam.InitCameraPosition(); // 敵を検出して自動ロックオン＆正対
                }
            }
        }

        isStageActive = true;
    }

    public void RestAndHeal()
    {
        if (playerCtrl != null)
        {
            // HP・FP（マナ）と両方のポーションを全回復
            playerCtrl.currentHealth = playerCtrl.maxHealth;
            playerCtrl.currentFp = playerCtrl.maxFp;
            playerCtrl.currentHpEstusCount = playerCtrl.hpEstusCount;
            playerCtrl.currentFpEstusCount = playerCtrl.fpEstusCount;

            // UI表示更新
            playerCtrl.TakeDamage(0);

            Debug.Log("休息完了：HP・FP・ポーションが全回復しました！");
        }
    }

    public void OnPlayerDied()
    {
        StartCoroutine(GameOverRoutine());
    }

    private IEnumerator GameOverRoutine()
    {
        isStageActive = false;

        if (currentBossInstance != null)
        {
            Destroy(currentBossInstance);
        }

        if (bossHPSlider != null)
        {
            bossHPSlider.gameObject.SetActive(false);
        }

        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(true);
        }

        yield return new WaitForSeconds(3.0f);

        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(false);
        }

        // 死亡時は休息エリアへ復帰
        ShowRestArea();
    }
}