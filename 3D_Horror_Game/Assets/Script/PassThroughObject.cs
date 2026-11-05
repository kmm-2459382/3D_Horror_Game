using UnityEngine;

public class PassThroughObject : MonoBehaviour
{
    [Header("Collision Settings")]
    [Tooltip("すり抜けたい対象のコライダー（未指定の場合はプレイヤーのコライダーを自動検出します）")]
    [SerializeField] private Collider targetCollider;

    [Tooltip("有効にすると、ゲーム開始時に指定したオブジェクトとの物理的衝突を完全に無効化（すり抜け）します")]
    [SerializeField] private bool disableCollisionOnStart = true;

    private Collider myCollider;

    void Start()
    {
        // 自分のコライダーを取得
        myCollider = GetComponent<Collider>();
        if (myCollider == null)
        {
            myCollider = gameObject.AddComponent<BoxCollider>();
        }

        // ターゲットが未指定の場合、「Player」タグを持つオブジェクトを自動検索
        if (targetCollider == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                targetCollider = playerObj.GetComponent<Collider>();
            }
        }

        if (disableCollisionOnStart)
        {
            DisableCollision();
        }
    }

    /// <summary>
    /// 物理的な衝突を無効化し、すり抜けられるようにするメソッド
    /// </summary>
    public void DisableCollision()
    {
        if (myCollider != null && targetCollider != null)
        {
            // Unityの物理エンジン機能により、この2つのコライダー間の衝突を完全に無視させる
            Physics.IgnoreCollision(myCollider, targetCollider, true);
        }
        else
        {
            Debug.LogWarning("コライダーまたはターゲットが設定されていないため、衝突を無効化できませんでした。", this);
        }
    }

    /// <summary>
    /// 必要に応じて再度衝突を有効（通常の状態）に戻すメソッド
    /// </summary>
    public void EnableCollision()
    {
        if (myCollider != null && targetCollider != null)
        {
            Physics.IgnoreCollision(myCollider, targetCollider, false);
        }
    }
}