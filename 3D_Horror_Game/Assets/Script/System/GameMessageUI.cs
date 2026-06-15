using UnityEngine;
using TMPro; // TextMeshProを使用するため必要

public class GameMessageUI : MonoBehaviour
{
    // シーン内に1つだけ存在させるための仕組み（シングルトン）
    public static GameMessageUI Instance { get; private set; }

    [Header("--- UIの設定 ---")]
    [Tooltip("画面に文字を表示するための TextMeshProUGUI コンポーネントをここに割り当てます")]
    [SerializeField] private TextMeshProUGUI messageText;

    [Tooltip("メッセージを表示しておく時間（秒）")]
    [SerializeField] private float displayDuration = 3.0f;

    [Tooltip("文字が消えるときのフェードアウト速度")]
    [SerializeField] private float fadeSpeed = 2.0f;

    private float hideTimer = 0f;
    private bool isFading = false;

    private void Awake()
    {
        // シングルトンの初期化設定
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 開始時はテキストを空にしておく
        if (messageText != null)
        {
            messageText.text = "";
            SetTextAlpha(0f); // 完全に透明にしておく
        }
        else
        {
            Debug.LogError("【GameMessageUI】MessageText が割り当てられていません！");
        }
    }

    private void Update()
    {
        // タイマーのカウントダウン
        if (hideTimer > 0f)
        {
            hideTimer -= Time.deltaTime;
            if (hideTimer <= 0f)
            {
                isFading = true; // 時間が来たらフェードアウトモードへ
            }
        }

        // 文字を徐々に透明にして消す処理
        if (isFading && messageText != null)
        {
            float currentAlpha = messageText.color.a;
            float newAlpha = Mathf.MoveTowards(currentAlpha, 0f, Time.deltaTime * fadeSpeed);
            SetTextAlpha(newAlpha);

            if (newAlpha <= 0f)
            {
                isFading = false;
                messageText.text = ""; // 完全に消えたらテキストをクリア
            }
        }
    }

    /// <summary>
    /// 【外部呼び出し用】画面に新しいメッセージを表示する関数
    /// </summary>
    /// <param name="text">表示したい文字列</param>
    /// <param name="textColor">文字の色（指定しなければ白）</param>
    public void ShowMessage(string text, Color? textColor = null)
    {
        if (messageText == null) return;

        // 文字列をセットし、不透明度を100%にする
        messageText.text = text;
        messageText.color = textColor ?? Color.white; // 色指定がなければ白
        SetTextAlpha(1f);

        // 各フラグとタイマーをリセット
        hideTimer = displayDuration;
        isFading = false;
    }

    // 文字の透明度(Alpha)だけを安全に変更する内部関数
    private void SetTextAlpha(float alpha)
    {
        if (messageText == null) return;
        Color c = messageText.color;
        c.a = alpha;
        messageText.color = c;
    }
}