using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    // 次の戦闘シーンで呼び出すボスのインデックス（シーン間で保持）
    public static int SelectedBossIndex = 0;

    // ボス選択時に呼ばれる（RestAreaScene から呼び出し）
    public static void LoadBattleScene(int bossIndex)
    {
        SelectedBossIndex = bossIndex;
        SceneManager.LoadScene("BattleScene");
    }

    // 拠点（休憩所）に戻る時に呼ばれる
    public static void LoadRestAreaScene()
    {
        SceneManager.LoadScene("RestAreaScene");
    }

    // タイトル画面に戻る時に呼ばれる
    public static void LoadTitleScene()
    {
        SceneManager.LoadScene("TitleScene");
    }
}