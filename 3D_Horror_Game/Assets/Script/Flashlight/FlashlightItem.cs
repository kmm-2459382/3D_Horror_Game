using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FlashlightItem : MonoBehaviour
{
    [Header("--- 装備させるオブジェクトの設定 ---")]
    [Tooltip("プレイヤーの子供としてあらかじめ配置してある『非アクティブ状態の懐中電灯（3Dモデルやライト）』をここに割り当てます")]
    [SerializeField] private GameObject playerFlashlightObject;

    [Header("--- 演出・通知の設定 ---")]
    [Tooltip("拾った時に画面に出すメッセージの内容")]
    [SerializeField] private string pickupMessage = "懐中電灯を手に入れた！";

    [Tooltip("メッセージの文字の色")]
    [SerializeField] private Color messageColor = Color.yellow;

    [Tooltip("拾った時に鳴らす効果音（カチャッ、スイッチ音など）")]
    [SerializeField] private AudioClip pickupSE;

    [Tooltip("効果音の音量")]
    [SerializeField][Range(0f, 1f)] private float volume = 0.8f;

    [Header("--- 取得方法の設定 ---")]
    [Tooltip("True: 体が触れただけで自動取得します。\nFalse: 視線(Raycast)で見てクリックした時に取得させます。")]
    [SerializeField] private bool autoPickupOnTouch = false;

    private bool isPickedUp = false;

    private void Awake()
    {
        // 接触時に物理的にプレイヤーが引っかかったり弾かれたりしないよう、
        // コライダーを自動的に「Is Trigger（すり抜け状態）」に設定します。
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    /// <summary>
    /// 【自動取得用】体がすり抜けた（Trigger）瞬間の接触検知
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!autoPickupOnTouch || isPickedUp) return;

        // 衝突相手がプレイヤー（TagがPlayer）の場合のみ取得を実行
        if (other.CompareTag("Player"))
        {
            PickupFlashlight();
        }
    }

    /// <summary>
    /// 【メイン処理】懐中電灯を有効化し、メッセージを出し、自身を削除します。
    /// （PlayerInteractorからのクリック、または接触時のどちらからでも呼び出されます）
    /// </summary>
    public void PickupFlashlight()
    {
        if (isPickedUp) return;
        isPickedUp = true;

        // 1. プレイヤーの手元にある懐中電灯オブジェクトを有効化（装備状態に）する
        if (playerFlashlightObject != null)
        {
            playerFlashlightObject.SetActive(true);
            Debug.Log($"【懐中電灯装備】{playerFlashlightObject.name} を有効化しました。");
        }
        else
        {
            Debug.LogError($"【設定エラー】[{gameObject.name}] に『Player Flashlight Object』が割り当てられていないため、有効化できません。");
        }

        // 2. 先ほど作成した共通テキストUIにメッセージを表示
        if (GameMessageUI.Instance != null)
        {
            GameMessageUI.Instance.ShowMessage(pickupMessage, messageColor);
        }

        // 3. 効果音をその場で再生
        if (pickupSE != null)
        {
            AudioSource.PlayClipAtPoint(pickupSE, transform.position, volume);
        }

        // 4. ステージ上に落ちているこのアイテム自体を消去
        Destroy(gameObject);
    }
}