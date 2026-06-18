using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FlashlightItem : MonoBehaviour
{
    [Header("--- 装備させるオブジェクトの設定 ---")]
    [Tooltip("プレイヤーの子供としてあらかじめ配置してある『非アクティブ状態の懐中電灯（3Dモデルやライト）』をここに割り当てます")]
    [SerializeField] private GameObject playerFlashlightObject;

    [Header("--- 演出・通知の設定（GameMessageUIのテキストに表示されます） ---")]
    [Tooltip("拾った時に画面に出すメッセージの内容")]
    [SerializeField] private string pickupMessage = "懐中電灯を手に入れた";

    [Tooltip("メッセージの文字の色")]
    [SerializeField] private Color messageColor = Color.white;

    [Header("--- 効果音の設定 ---")]
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
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!autoPickupOnTouch || isPickedUp) return;

        if (other.CompareTag("Player"))
        {
            PickupFlashlight();
        }
    }

    public void PickupFlashlight()
    {
        if (isPickedUp) return;
        isPickedUp = true;

        // 1. プレイヤーの手元にある懐中電灯オブジェクトを有効化
        if (playerFlashlightObject != null)
        {
            playerFlashlightObject.SetActive(true);
            Debug.Log($"【懐中電灯装備】{playerFlashlightObject.name} を有効化しました。");
        }

        // 2. 💡 GameMessageUIに登録されているテキストボックスを指定して表示
        if (GameMessageUI.Instance != null)
        {
            // GameMessageUIのインスペクターで指定されているテキストボックスへ文字と色を送る
            GameMessageUI.Instance.ShowMessage(pickupMessage, messageColor);
        }
        else
        {
            Debug.LogError("【実行エラー】シーン内に GameMessageUI (MessageManager) が見つからないため、テキストを表示できません。");
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