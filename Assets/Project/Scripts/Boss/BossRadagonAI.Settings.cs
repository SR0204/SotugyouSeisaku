using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public partial class BossRadagonAI : MonoBehaviour
{
    private NavMeshAgent agent;
    private Transform player;
    private PlayerController playerController;
    private BossHealth bossHealth;
    private Animator animator;

    // ==========================================
    // 攻撃ジャンルごとの設定構造体
    // ==========================================
    [System.Serializable]
    public class MeleeAttackSettings
    {
        [Tooltip("通常近接のダメージ")]
        public float damage = 25f;
        [Tooltip("強攻撃・叩きつけのダメージ")]
        public float heavyDamage = 45f;
        [Tooltip("攻撃が届く距離")]
        public float attackRange = 3.5f;
    }

    [System.Serializable]
    public class ComboMeleeSettings
    {
        [Tooltip("1ヒットあたりのダメージ")]
        public float damagePerHit = 20f;
        [Tooltip("連撃の回数（例: 3回）")]
        public int hitCount = 3;
        [Tooltip("連撃の間隔（秒）")]
        public float hitInterval = 0.45f;
        [Tooltip("1撃ごとの前進踏み込み距離（m）")]
        public float stepForwardDistance = 1.2f;
    }

    [System.Serializable]
    public class DashAttackSettings
    {
        [Tooltip("突進ヒット時のダメージ（30以上で吹っ飛びダウン）")]
        public float damage = 35f;
        [Tooltip("突進を発動する最小距離")]
        public float minRange = 6f;
        [Tooltip("突進を発動する最大距離")]
        public float maxRange = 18f;
        [Tooltip("突進前の溜め（予兆）時間（秒）")]
        public float chargeTime = 0.8f;
        [Tooltip("突進スピード（m/s）")]
        public float dashSpeed = 24f;
        [Tooltip("突進の継続時間（秒）")]
        public float dashDuration = 0.55f;
        [Tooltip("突進の当たり判定半径")]
        public float hitRadius = 1.6f;
        [Tooltip("溜め中・突進中のエフェクト（任意）")]
        public GameObject dashVfxPrefab;

        [Header("--- 突進予測線（警告ライン）設定 ---")]
        [Tooltip("突進の予測線を地面に表示するか")]
        public bool showTelegraphLine = true;
        [Tooltip("予測線の幅（m）")]
        public float lineWidth = 1.6f;
        [Tooltip("追尾中の予測線の色")]
        public Color aimingLineColor = new Color(1f, 0.2f, 0.1f, 0.35f);
        [Tooltip("方向確定（ロック）後の予測線の色")]
        public Color lockedLineColor = new Color(1f, 0.05f, 0.05f, 0.85f);
    }

    [System.Serializable]
    public class SummonAttackSettings
    {
        [Tooltip("召喚するミニオンのプレハブ（BossMinionAI等）")]
        public GameObject minionPrefab;
        [Tooltip("1回の召喚で呼び出す体数")]
        public int summonCount = 2;
        [Tooltip("フィールド上に同時に存在できるミニオンの上限数")]
        public int maxAliveMinions = 4;
        [Tooltip("ボスからの召喚半径（m）")]
        public float spawnRadius = 3.5f;
        [Tooltip("詠唱時間（秒）")]
        public float castTime = 1.2f;
        [Tooltip("召喚時の魔法陣エフェクト（任意）")]
        public GameObject summonVfxPrefab;
    }

    [System.Serializable]
    public class RangedAttackSettings
    {
        [Tooltip("遠距離攻撃のダメージ")]
        public float damage = 20f;
        [Tooltip("遠距離攻撃を開始する距離")]
        public float minRange = 8f;
        [Tooltip("発射する弾丸・光弾プレハブ")]
        public GameObject projectilePrefab;
        [Tooltip("弾の生成・発射位置")]
        public Transform spawnPoint;
        [Tooltip("弾の飛行スピード")]
        public float projectileSpeed = 15f;
    }

    [System.Serializable]
    public class AreaAttackSettings
    {
        [Tooltip("範囲攻撃（地面叩きつけ等）のダメージ")]
        public float damage = 50f;
        [Tooltip("範囲攻撃を発動する距離")]
        public float triggerRange = 5f;
        [Tooltip("攻撃の有効加害半径")]
        public float radius = 6f;
        [Tooltip("発生させる範囲エフェクト（爆発・衝撃波等）")]
        public GameObject areaVfxPrefab;
        [Tooltip("エフェクト発生位置（足元等）")]
        public Transform areaPoint;
    }

    [System.Serializable]
    public class LaserAttackSettings
    {
        [Tooltip("レーザーの1ヒットあたりのダメージ")]
        public float damagePerTick = 15f;
        [Tooltip("レーザーの有効射程距離")]
        public float maxDistance = 15f;
        [Tooltip("照射時間（秒）")]
        public float duration = 2.0f;
        [Tooltip("レーザー用LineRenderer（または判定エフェクト）")]
        public LineRenderer laserLine;
        [Tooltip("レーザー発射位置（右手・武器先端等）")]
        public Transform firingPoint;
    }

    [System.Serializable]
    public class Laser360AttackSettings
    {
        [Tooltip("360度レーザーの1ヒットあたりのダメージ")]
        public float damagePerTick = 80f;
        [Tooltip("有効射程距離")]
        public float maxDistance = 20f;
        [Tooltip("照射時間（秒）")]
        public float duration = 3.0f;
        [Tooltip("チャージ（予兆）時間（秒）")]
        public float chargeTime = 1.5f;
        [Tooltip("チャージ中に表示するエフェクト")]
        public GameObject chargeVfxPrefab;
        [Tooltip("発射・チャージ位置")]
        public Transform firingPoint;
    }

    [System.Serializable]
    public class EclipseMeteorSettings
    {
        [Tooltip("メテオ着弾・爆破時の極大ダメージ")]
        public float damage = 150f;
        [Tooltip("チャージ・予兆詠唱時間（秒）")]
        public float chargeTime = 3.0f;
        [Tooltip("爆破・加害半径")]
        public float explosionRadius = 25f;
        [Tooltip("メテオ落下の生成高度")]
        public float dropHeight = 30f;
        [Tooltip("詠唱中のチャージ/魔法陣VFX")]
        public GameObject chargeVfxPrefab;
        [Tooltip("上空から落ちてくる隕石プレハブ")]
        public GameObject meteorPrefab;
        [Tooltip("着弾時の極大爆発VFX")]
        public GameObject impactVfxPrefab;
    }

    [System.Serializable]
    public class AttackProbabilities
    {
        [Header("--- 近接攻撃 ---")]
        public bool enableMeleeNormal = true;
        public float meleeNormalWeight = 20f;
        public bool enableMeleeHeavy = true;
        public float meleeHeavyWeight = 15f;
        public bool enableMeleeCombo = true;
        public float meleeComboWeight = 15f;

        [Header("--- 突進攻撃 ---")]
        public bool enableDashAttack = true;
        public float dashAttackWeight = 15f;

        [Header("--- ミニオン召喚 ---")]
        public bool enableSummonMinions = false;
        public float summonMinionsWeight = 15f;
        [Tooltip("第二形態になってから召喚を解禁する場合はチェック")]
        public bool summonOnlyInPhase2 = false;

        [Header("--- 遠距離（弾）攻撃 ---")]
        public bool enableRangedSingle = true;
        public float rangedSingleWeight = 10f;
        public bool enableRangedRapid = true;
        public float rangedRapidWeight = 10f;
        public bool enableRangedSpread = true;
        public float rangedSpreadWeight = 10f;

        [Header("--- 範囲攻撃 ---")]
        public bool enableAreaImpact = true;
        public float areaImpactWeight = 15f;

        [Header("--- レーザー攻撃 ---")]
        public bool enableLaserStraight = true;
        public float laserStraightWeight = 10f;
        public bool enableLaserSweep360Charge = true;
        public float laserSweep360ChargeWeight = 10f;
        public bool enableLaserSweep90 = true;
        public float laserSweep90Weight = 5f;

        [Header("--- 大技（必殺技） ---")]
        public bool enableEclipseMeteor = true;
        public float eclipseMeteorWeight = 5f;
    }

    // ==========================================
    // Inspector 設定項目
    // ==========================================
    [Header("=== 1. 近接攻撃設定 ===")]
    public MeleeAttackSettings meleeAttack;

    [Header("=== 1-2. 連続コンボ斬撃設定 ===")]
    public ComboMeleeSettings comboMelee;

    [Header("=== 1-3. 高速突進・タックル設定 ===")]
    public DashAttackSettings dashAttack;

    [Header("=== 1-4. ミニオン召喚設定 ===")]
    public SummonAttackSettings summonAttack;

    [Header("=== 2. 遠距離攻撃設定 ===")]
    public RangedAttackSettings rangedAttack;

    [Header("=== 3. 範囲攻撃（AoE）設定 ===")]
    public AreaAttackSettings areaAttack;

    [Header("=== 4. 通常レーザー攻撃設定 ===")]
    public LaserAttackSettings laserAttack;

    [Header("=== 4-2. 360度レーザー（チャージ専用） ===")]
    public Laser360AttackSettings laser360Attack;

    [Header("=== 4-3. エクリプスメテオ（極大範囲攻撃） ===")]
    public EclipseMeteorSettings eclipseMeteor;

    [Header("=== 5. 各攻撃の発生確率（重み）設定 ===")]
    public AttackProbabilities attackWeights;

    [Header("=== 共通・攻撃タイミング設定 ===")]
    public float attackInterval = 1.5f;
    public float attackCooldown = 1.2f;

    [Header("=== フェーズ設定 ===")]
    public bool isPhase2 = false;
    public float phase2HpThreshold = 0.5f;

    [Header("=== 第二形態（覚醒演出）設定 ===")]
    public GameObject phase2AuraVfxPrefab;
    public GameObject phase2ExplosionVfxPrefab;
    public float phase2TransitionTime = 2.0f;

    [Header("=== 魔法対抗（ステップ回避）設定 ===")]
    [Range(0f, 1f)] public float spellDodgeChance = 0.6f;
    public float dodgeSpeed = 12f;
    public float dodgeDuration = 0.4f;

    // 内部状態管理用
    private float attackTimer = 0f;
    private bool isPerformingAction = false;
    private AttackCategory currentAttack = AttackCategory.None;
    private bool isLaserActive = false;
    private List<GameObject> spawnedMinions = new List<GameObject>();

    public enum AttackCategory
    {
        None,
        MeleeNormal,
        MeleeHeavy,
        MeleeCombo,
        DashAttack,
        SummonMinions,
        Ranged,
        RangedRapid,
        RangedSpread,
        Area,
        LaserStraight,
        LaserSweep360Charge,
        LaserSweep90,
        EclipseMeteor
    }
}