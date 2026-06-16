using UnityEngine;

[RequireComponent(typeof(Collider))]
public class KeyItem : MonoBehaviour
{
    [Header("--- 鍵の設定 ---")]
    [Tooltip("この鍵の固有ID。DoorControllerの【doorId】に登録した文字列と完全に一致させてください（例: Room_A_Key）")]
    [SerializeField] private string keyId = "Room_A_Key";

    [Tooltip("鍵を拾った時に鳴らす効果音（カチャッ、シャリーンなど）")]
    [SerializeField] private AudioClip pickupSE;

    [Tooltip("効果音の音量")]
    [SerializeField][Range(0f, 1f)] private float volume = 0.8f;

    [Header("--- 取得方法の設定 ---")]
    [Tooltip("True: プレイヤーが体に触れただけで自動取得します。\nFalse: プレイヤーが視線(Raycast)等で捉えてクリックした時に取得させます。")]
    [SerializeField] private bool autoPickupOnTouch = true;

    private bool isPickedUp = false;

    private void Awake()
    {
        // 接触時に物理的にプレイヤーが引っかかったり弾かれたりしないよう、
        // コライダーを自動的に「Is Trigger（すり抜け状態）」に強制設定します。
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    /// <summary>
    /// 体がすり抜けた（Trigger）瞬間の接触検知
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // 体当たり取得が無効、またはすでに拾われている場合は無視
        if (!autoPickupOnTouch || isPickedUp) return;

        // 衝突相手がプレイヤー（TagがPlayer）の場合のみ処理
        if (other.CompareTag("Player"))
        {
            // プレイヤー自身、あるいはその子オブジェクト・親オブジェクトからインベントリを検索
            PlayerInventory inventory = other.GetComponentInChildren<PlayerInventory>();
            if (inventory == null)
            {
                inventory = other.GetComponentInParent<PlayerInventory>();
            }

            if (inventory != null)
            {
                PickupKey(inventory);
            }
            else
            {
                Debug.LogWarning($"【設定エラー】接触したプレイヤー [{other.name}] の階層に 'PlayerInventory' スクリプトが見つかりません。");
            }
        }
    }

    /// <summary>
    /// 【外部連携用メイン関数】
    /// 鍵をインベントリに追加し、音を鳴らして自身を消去します。
    /// （PlayerInteractorなどのRaycastシステムから呼び出すことも可能です）
    /// </summary>
    /// <param name="playerInventory">鍵を追加する対象のプレイヤーインベントリ参照</param>
    public void PickupKey(PlayerInventory playerInventory)
    {
        // 二重取得の防止
        if (isPickedUp || playerInventory == null) return;

        isPickedUp = true;

        // ご提示いただいた PlayerInventory の AddKey(string keyId) を実行し、鍵を追加
        playerInventory.AddKey(keyId);
        Debug.Log($"【鍵取得成功】アイテム「{gameObject.name}」を取得しました。インベントリ登録ID: {keyId}");

        // 💡【GameMessageUI対応】
        // GameMessageUIに指定・登録してあるテキストコンポーネントへ、拾った鍵のメッセージを黄色い文字で表示します
        if (GameMessageUI.Instance != null)
        {
            GameMessageUI.Instance.ShowMessage($"「{keyId}」を手に入れた！", Color.yellow);
        }
        else
        {
            Debug.LogWarning("【GameMessageUIが見つかりません】MessageManagerオブジェクトがシーン内に存在するか確認してください。");
        }

        // 鍵を拾った効果音が設定されていれば、3D空間上のその場で再生（Destroyされても音が消えない方法）
        if (pickupSE != null)
        {
            AudioSource.PlayClipAtPoint(pickupSE, transform.position, volume);
        }

        // シーンから鍵の3Dオブジェクトを完全に削除
        Destroy(gameObject);
    }
}