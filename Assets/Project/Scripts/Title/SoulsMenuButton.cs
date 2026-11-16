using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SoulsMenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    [Header("UIパーツの指定")]
    public Text buttonText;
    public Image highlightBar;

    [Header("色設定（エルデンリング風）")]
    public Color normalColor = new Color(0.82f, 0.80f, 0.75f, 1f);      // 通常：落ち着いたオフホワイト
    public Color hoverColor = new Color(0.96f, 0.84f, 0.45f, 1f);       // ホバー：淡い黄金色
    public Color clickFlashColor = new Color(1f, 0.98f, 0.85f, 1f);     // クリック瞬間：白金フラッシュ

    [Header("帯（バー）の最大透明度")]
    [Range(0f, 0.5f)]
    public float maxBarAlpha = 0.22f;

    private bool isHovered = false;
    private bool isClicked = false;
    private Vector3 defaultScale;

    void Start()
    {
        defaultScale = transform.localScale;

        if (buttonText != null)
            buttonText.color = normalColor;

        if (highlightBar != null)
        {
            Color c = highlightBar.color;
            c.a = 0f;
            highlightBar.color = c;
        }
    }

    void Update()
    {
        if (isClicked) return;

        // 目標の色と帯の透明度へなめらかに変化させる
        Color targetTextColor = isHovered ? hoverColor : normalColor;
        float targetAlpha = isHovered ? maxBarAlpha : 0f;

        if (buttonText != null)
            buttonText.color = Color.Lerp(buttonText.color, targetTextColor, Time.deltaTime * 12f);

        if (highlightBar != null)
        {
            Color c = highlightBar.color;
            c.a = Mathf.Lerp(c.a, targetAlpha, Time.deltaTime * 12f);
            highlightBar.color = c;
        }
    }

    // マウスが乗った時
    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
    }

    // マウスが離れた時
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
    }

    // クリックした瞬間（押した手応え演出）
    public void OnPointerDown(PointerEventData eventData)
    {
        if (isClicked) return;
        isClicked = true;
        StartCoroutine(ClickEffect());
    }

    private IEnumerator ClickEffect()
    {
        // 1. 文字を白金色に強く発光させ、少しだけ拡大して「決定した！」手応えを出す
        if (buttonText != null)
            buttonText.color = clickFlashColor;

        if (highlightBar != null)
        {
            Color c = highlightBar.color;
            c.a = 0.45f; // 帯も一瞬強く光らせる
            highlightBar.color = c;
        }

        transform.localScale = defaultScale * 1.06f;
        yield return new WaitForSeconds(0.12f);

        // 2. 元のサイズにスッと戻りつつ黄金色を維持（このまま画面が暗転していく）
        transform.localScale = defaultScale;
        if (buttonText != null)
            buttonText.color = hoverColor;
    }
}