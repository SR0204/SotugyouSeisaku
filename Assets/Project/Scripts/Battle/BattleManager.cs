using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BattleManager : MonoBehaviour
{
    [Header("プレハブ設定")]
    [Tooltip("アリーナプレハブ（ボスID対応、またはランダム選出用）")]
    public GameObject[] arenaPrefabs;
    [Tooltip("特定ボス専用アリーナ（空欄なら通常アリーナ）")]
    public GameObject[] bossSpecificArenas;
    [Tooltip("ボスプレハブ一覧")]
    public GameObject[] bossPrefabs;
    [Tooltip("プレイヤープレハブ")]
    public GameObject playerPrefab;
    [Tooltip("UIキャンバスのプレハブ（Canvas）")]
    public GameObject uiCanvasPrefab;

    [Header("生成位置")]
    public Vector3 playerSpawnPos = Vector3.zero;
    public Vector3 bossPosition = new Vector3(0, 0, 10f);

    private GameObject currentBoss;
    private GameObject currentPlayer;

    // UI参照用
    private Slider healthSlider;
    private Slider staminaSlider;
    private TextMeshProUGUI estusText;
    private Slider bossHPSlider;
    private TextMeshProUGUI gameOverText;

    private bool isBattleEnd = false;
    private bool isSetupComplete = false; // ★ 初期化完了フラグ

    void Start()
    {
        StartCoroutine(SetupBattleRoutine());
    }

    private IEnumerator SetupBattleRoutine()
    {
        int bossIndex = GameManager.SelectedBossIndex;

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

            Transform panelObj = canvasObj.transform.Find("BossSelectPanel");
            if (panelObj != null) panelObj.gameObject.SetActive(false);

            Transform lockOnObj = canvasObj.transform.Find("LockOnIcon");
            if (lockOnObj != null && Camera.main != null)
            {
                ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
                if (cam != null) cam.lockOnIcon = lockOnObj.GetComponent<Image>();
            }
        }

        // 2. アリーナ（床）生成
        GameObject selectedArena = null;
        if (bossSpecificArenas != null && bossIndex < bossSpecificArenas.Length && bossSpecificArenas[bossIndex] != null)
        {
            selectedArena = bossSpecificArenas[bossIndex];
        }
        else if (arenaPrefabs != null && arenaPrefabs.Length > 0)
        {
            int idx = Mathf.Clamp(bossIndex, 0, arenaPrefabs.Length - 1);
            selectedArena = arenaPrefabs[idx];
        }

        if (selectedArena != null)
        {
            Instantiate(selectedArena, Vector3.zero, Quaternion.identity);
        }

        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate(); // 物理同期完了を確実に待つ

        // 3. プレイヤー生成（位置を地面に密着）
        if (playerPrefab != null)
        {
            currentPlayer = Instantiate(playerPrefab, playerSpawnPos, Quaternion.identity);

            PlayerController playerCtrl = currentPlayer.GetComponent<PlayerController>();
            if (playerCtrl != null)
            {
                playerCtrl.healthSlider = healthSlider;
                playerCtrl.staminaSlider = staminaSlider;
                playerCtrl.estusText = estusText;
            }

            if (Camera.main != null)
            {
                ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
                if (cam != null) cam.target = currentPlayer.transform;
            }
        }

        // 4. ボス生成
        if (bossPrefabs != null && bossIndex < bossPrefabs.Length && bossPrefabs[bossIndex] != null)
        {
            currentBoss = Instantiate(bossPrefabs[bossIndex], bossPosition, Quaternion.identity);

            BossHealth bossHealth = currentBoss.GetComponent<BossHealth>();
            if (bossHealth != null && bossHPSlider != null)
            {
                bossHPSlider.gameObject.SetActive(true);
                bossHealth.bossHPSlider = bossHPSlider;
            }
        }

        // 5. カメラ初期化
        if (Camera.main != null)
        {
            ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
            if (cam != null)
            {
                cam.InitCameraPosition();
            }
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 少しだけ待ってからボス判定を開始させる（誤作動防止）
        yield return new WaitForSeconds(0.5f);
        isSetupComplete = true;
    }

    void Update()
    {
        if (isBattleEnd || !isSetupComplete) return;

        // ボスが正常に生成された後、死亡・破壊された場合のみ撃破処理を実行
        if (currentBoss == null)
        {
            OnBossDefeated();
        }
    }

    public void OnPlayerDied()
    {
        if (isBattleEnd) return;
        StartCoroutine(PlayerDiedRoutine());
    }

    private IEnumerator PlayerDiedRoutine()
    {
        isBattleEnd = true;
        if (bossHPSlider != null) bossHPSlider.gameObject.SetActive(false);
        if (gameOverText != null) gameOverText.gameObject.SetActive(true);

        yield return new WaitForSeconds(3.0f);

        GameManager.LoadRestAreaScene();
    }

    private void OnBossDefeated()
    {
        isBattleEnd = true;
        if (bossHPSlider != null) bossHPSlider.gameObject.SetActive(false);

        StartCoroutine(BossDefeatedRoutine());
    }

    private IEnumerator BossDefeatedRoutine()
    {
        yield return new WaitForSeconds(2.5f);
        GameManager.LoadRestAreaScene();
    }
}