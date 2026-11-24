using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ResultBannerUI : MonoBehaviour
{
    public static ResultBannerUI Instance { get; private set; }

    [Header("UI要素（自動生成も可）")]
    [SerializeField] private CanvasGroup bannerGroup;
    [SerializeField] private Image backgroundBand;
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("演出設定")]
    [SerializeField] private float fadeInDuration = 1.0f;
    [SerializeField] private float holdDuration = 2.2f;
    [SerializeField] private float fadeOutDuration = 0.8f;

    private Coroutine currentRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (bannerGroup != null)
        {
            bannerGroup.alpha = 0f;
            bannerGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// ボス撃破時の演出（「GREAT ENEMY FELLED」）
    /// </summary>
    public void ShowVictory(string text = "GREAT ENEMY FELLED")
    {
        // エルデンリング／ダクソ風の重厚な金色
        Color goldColor = new Color(0.92f, 0.76f, 0.36f, 1f);
        PlayBanner(text, goldColor);
    }

    /// <summary>
    /// プレイヤー死亡時の演出（「YOU DIED」）
    /// </summary>
    public void ShowDeath(string text = "YOU DIED")
    {
        // ダークソウル風の深い血赤色
        Color crimsonColor = new Color(0.75f, 0.12f, 0.12f, 1f);
        PlayBanner(text, crimsonColor);
    }

    private void PlayBanner(string text, Color textColor)
    {
        if (bannerGroup == null || messageText == null) return;

        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
        }
        currentRoutine = StartCoroutine(BannerRoutine(text, textColor));
    }

    private IEnumerator BannerRoutine(string text, Color textColor)
    {
        messageText.text = text;
        messageText.color = textColor;

        // 少し拡大しながらフェードインする重厚なアニメーション
        RectTransform textRect = messageText.rectTransform;
        Vector3 startScale = new Vector3(0.95f, 0.95f, 1f);
        Vector3 endScale = new Vector3(1.03f, 1.03f, 1f);

        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            float rate = Mathf.Clamp01(t / fadeInDuration);
            bannerGroup.alpha = rate;
            textRect.localScale = Vector3.Lerp(startScale, endScale, rate);
            yield return null;
        }
        bannerGroup.alpha = 1f;

        // 表示維持
        yield return new WaitForSecondsRealtime(holdDuration);

        // フェードアウト
        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float rate = Mathf.Clamp01(t / fadeOutDuration);
            bannerGroup.alpha = 1f - rate;
            yield return null;
        }
        bannerGroup.alpha = 0f;
        currentRoutine = null;
    }
}