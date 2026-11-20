using System;
using System.Collections;
using UnityEngine;
using TMPro;

public class BossFogEntry : MonoBehaviour
{
    [Header("霧をくぐる操作設定")]
    [Tooltip("霧をくぐるために押すキー（デフォルト: E）")]
    public KeyCode enterKey = KeyCode.E;
    [Tooltip("霧の前で反応する距離（m）")]
    public float interactDistance = 3.5f;
    [Tooltip("霧をくぐり抜ける移動距離（m）")]
    public float passThroughDistance = 4.0f;
    [Tooltip("くぐり抜けにかかる時間（秒）")]
    public float passThroughDuration = 1.0f;

    [Header("表示テキスト設定（任意）")]
    public string promptMessage = "[E] 霧の中に入る";

    private Action onEnteredCallback;
    private Collider solidWallCollider;
    private Transform playerTransform;
    private CharacterController playerCC;
    private bool isPassing = false;
    private bool isLocked = false;
    private bool isPlayerNear = false;
    private float halfInteractWidth = 9.5f;

    public void Initialize(Action onEntered)
    {
        onEnteredCallback = onEntered;
    }

    void Start()
    {
        solidWallCollider = GetComponentInChildren<Collider>();
        if (solidWallCollider != null)
        {
            solidWallCollider.isTrigger = false;
        }

        // ★修正1：親でなくてもシーン内から BossEntranceAutoFit を確実に取得する
        BossEntranceAutoFit autoFit = GetComponentInParent<BossEntranceAutoFit>();
        if (autoFit == null)
        {
            autoFit = FindObjectOfType<BossEntranceAutoFit>();
        }

        if (autoFit != null)
        {
            float innerWidth = autoFit.CurrentInnerWidth;
            halfInteractWidth = innerWidth * 0.5f;

            // 霧の壁のワールドX位置をエントランス中央に揃える
            Vector3 pos = transform.position;
            pos.x = autoFit.CurrentEntranceWorldCenter.x;
            transform.position = pos;

            // 親のスケール倍率を逆算して、ワールド上で正確に innerWidth メートルにする
            float parentLossyX = (transform.parent != null) ? transform.parent.lossyScale.x : 1f;
            if (Mathf.Approximately(parentLossyX, 0f)) parentLossyX = 1f;
            float targetLocalWidth = innerWidth / parentLossyX;

            // 1. 当たり判定用Cubeの横幅を合わせる
            Transform cube = transform.Find("Cube");
            if (cube != null)
            {
                Vector3 s = cube.localScale;
                cube.localScale = new Vector3(targetLocalWidth, s.y, s.z);
            }

            // 2. 霧パーティクルの発生横幅・中心ズレ・濃さを合わせる
            ParticleSystem ps = GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                var shape = ps.shape;
                Vector3 shapePos = shape.position;
                shape.position = new Vector3(0f, shapePos.y, shapePos.z);

                Vector3 shapeScale = shape.scale;
                shape.scale = new Vector3(targetLocalWidth, shapeScale.y, shapeScale.z);

                // ★修正2：横幅が広がった分だけ霧の濃さ（発生量）も比例して増やす
                var emission = ps.emission;
                float ratio = Mathf.Max(1f, innerWidth / 19f);
                emission.rateOverTimeMultiplier *= ratio;

                // ★修正3：古い狭い幅の霧を消して、新しい幅いっぱいに即座に霧を出す
                ps.Clear();
                ps.Play();
            }
        }
    }

    void Update()
    {
        // すでにボス部屋に入って閉じ込められた後、または移動中は何もしない
        if (isLocked || isPassing) return;

        // プレイヤーを自動検索
        if (playerTransform == null)
        {
            GameObject pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj == null)
            {
                PlayerController pc = FindObjectOfType<PlayerController>();
                if (pc != null) pObj = pc.gameObject;
            }
            if (pObj != null)
            {
                playerTransform = pObj.transform;
                playerCC = pObj.GetComponent<CharacterController>();
            }
            return;
        }

        // プレイヤーが霧の壁の手前側（ボス部屋の外側）にいるか判定
        float zDiff = transform.position.z - playerTransform.position.z;
        float xDiff = Mathf.Abs(transform.position.x - playerTransform.position.x);

        isPlayerNear = (zDiff > 0f && zDiff <= interactDistance && xDiff <= halfInteractWidth);

        // 近くで Eキー（enterKey）を押したら霧をくぐり抜ける！
        if (isPlayerNear && Input.GetKeyDown(enterKey))
        {
            StartCoroutine(PassThroughFogRoutine());
        }
    }

    private IEnumerator PassThroughFogRoutine()
    {
        isPassing = true;
        isPlayerNear = false;

        // 1. 一時的に壁の当たり判定をオフにして通り抜けられるようにする
        if (solidWallCollider != null)
        {
            solidWallCollider.enabled = false;
        }

        // プレイヤーの向きを霧の奥（ボス部屋側）へ揃える
        Vector3 forwardDir = Vector3.forward;
        playerTransform.rotation = Quaternion.LookRotation(forwardDir);

        // 2. ゆっくり歩いて霧をくぐり抜ける演出
        float elapsed = 0f;
        float moveSpeed = passThroughDistance / passThroughDuration;

        while (elapsed < passThroughDuration)
        {
            float step = moveSpeed * Time.deltaTime;
            if (playerCC != null && playerCC.enabled)
            {
                playerCC.Move(forwardDir * step);
            }
            else
            {
                playerTransform.position += forwardDir * step;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 3. くぐり抜け完了！壁の当たり判定を再びオンにして「後ろに戻れない壁」にする
        if (solidWallCollider != null)
        {
            solidWallCollider.enabled = true;
            solidWallCollider.isTrigger = false;
        }

        isPassing = false;
        isLocked = true;

        // 4. ボス戦開始（HPバー表示・ボス行動開始）を呼び出す
        onEnteredCallback?.Invoke();
    }

    // 霧の前に近づいたとき画面下にガイド表示（UI設定不要で自動表示）
    void OnGUI()
    {
        if (!isPlayerNear || isPassing || isLocked) return;

        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 24;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.white;

        float width = 320f;
        float height = 50f;
        float x = (Screen.width - width) * 0.5f;
        float y = Screen.height * 0.72f;

        GUI.Box(new Rect(x, y, width, height), promptMessage, style);
    }
}