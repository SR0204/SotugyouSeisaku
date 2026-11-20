using UnityEngine;

public class BossEntranceAutoFit : MonoBehaviour
{
    [Header("ステージ床のCollider（未指定なら親から自動検出）")]
    public Collider arenaFloorCollider;

    [Header("サイズ比率設定")]
    [Tooltip("ステージ横幅に対するエントランス幅の割合（四角: 0.6〜1.0 / 円形: 0.35〜0.45）")]
    [Range(0.1f, 1.0f)]
    public float widthRatio = 0.4f;

    [Tooltip("円形の丸み・隙間を埋めるためにフチへ食い込ませる量（m）")]
    public float overlapDepth = 0.5f;

    public float CurrentInnerWidth
    {
        get
        {
            Bounds b = GetEntranceWorldBounds();
            return Mathf.Max(2f, b.size.x - 1.0f);
        }
    }

    public Vector3 CurrentEntranceWorldCenter
    {
        get
        {
            return GetEntranceWorldBounds().center;
        }
    }

    private Bounds GetEntranceWorldBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(transform.position, new Vector3(80f, 1f, 10f));

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            b.Encapsulate(renderers[i].bounds);
        }
        return b;
    }

    [ContextMenu("ステージの大きさに自動フィット")]
    public void FitToArena()
    {
        Transform parent = transform.parent;
        if (parent == null) return;

        if (arenaFloorCollider == null)
        {
            foreach (var col in parent.GetComponentsInChildren<Collider>())
            {
                if (!col.transform.IsChildOf(transform))
                {
                    arenaFloorCollider = col;
                    break;
                }
            }
        }
        if (arenaFloorCollider == null) return;

        Vector3 pScale = parent.lossyScale;
        Bounds floorBounds = arenaFloorCollider.bounds;
        float stageWidth = floorBounds.size.x;
        float stageDepth = floorBounds.size.z;

        bool isCircle = parent.name.ToLower().Contains("circle") ||
                        (arenaFloorCollider is MeshCollider mc && mc.sharedMesh != null && mc.sharedMesh.name.Contains("Cylinder"));

        float effectiveRatio = isCircle ? Mathf.Min(widthRatio, 0.4f) : widthRatio;
        float targetWorldWidth = stageWidth * effectiveRatio;

        // 1. 親スケールを打ち消しつつ目標幅にリサイズ
        transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        transform.localScale = new Vector3(1f / pScale.x, 1f / pScale.y, 1f / pScale.z);

        Bounds currentEntBounds = GetEntranceWorldBounds();
        float scaleXFactor = targetWorldWidth / Mathf.Max(0.1f, currentEntBounds.size.x);

        transform.localScale = new Vector3(
            scaleXFactor / pScale.x,
            1f / pScale.y,
            1f / pScale.z
        );

        Bounds entBounds = GetEntranceWorldBounds();

        // エントランス内の「Floor」の天面を取得（無ければ全体の最小Yを使用）
        Transform entFloor = transform.Find("Floor");
        float entFloorTopY = entBounds.min.y;
        if (entFloor != null && entFloor.TryGetComponent<Renderer>(out var floorRend))
        {
            entFloorTopY = floorRend.bounds.max.y;
        }

        // 円形の弧の深さ
        float arcDepth = 0f;
        if (isCircle)
        {
            float radius = stageWidth * 0.5f;
            float halfW = entBounds.size.x * 0.5f;
            arcDepth = radius - Mathf.Sqrt(Mathf.Max(0f, radius * radius - halfW * halfW));
        }

        // 2. X=中央、Y=床天面同士をピッタリ一致（-0.005mだけ下げてチラつき防止）、Z=フチに吸着
        float desiredMaxZ = floorBounds.min.z + overlapDepth + arcDepth;
        float deltaZ = desiredMaxZ - entBounds.max.z;
        float deltaX = floorBounds.center.x - entBounds.center.x;
        float deltaY = (floorBounds.max.y - 0.005f) - entFloorTopY;

        transform.position += new Vector3(deltaX, deltaY, deltaZ);

        Debug.Log($"[{parent.name}] 自動フィット完了！（実質横幅: {entBounds.size.x:F1}m / 床段差ゼロ調整済）");
    }
}