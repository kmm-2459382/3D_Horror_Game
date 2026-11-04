using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

public class AutoTransitionSequenceController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("メッセージを表示するTextMeshProUIコンポーネント")]
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("Message Settings")]
    [Tooltip("順番に連続して表示したいメッセージのリスト")]
    [SerializeField] private string[] messages;

    [Tooltip("各メッセージを画面に表示し続ける時間（秒）")]
    [SerializeField] private float displayDuration = 3.0f;

    [Tooltip("メッセージとメッセージの間のインターバル時間（秒）")]
    [SerializeField] private float intervalTime = 0.5f;

    [Header("Scene Transition Settings")]
    [Tooltip("すべてのテキスト終了後に自動で遷移するシーン名")]
    [SerializeField] private string nextSceneName = "NextScene";

    void Start()
    {
        // 参照やメッセージが設定されていない場合の安全対策
        if (messageText == null || messages == null || messages.Length == 0)
        {
            Debug.LogWarning("TextMeshProUGUIまたはメッセージが正しく設定されていません。");
            return;
        }

        // 自動シーン遷移シーケンスを開始する
        StartCoroutine(PlayMessageSequenceAndTransition());
    }

    private IEnumerator PlayMessageSequenceAndTransition()
    {
        messageText.text = "";
        messageText.gameObject.SetActive(false);

        // シーン遷移直後のカクつきを避けるため少し待機
        yield return new WaitForSeconds(0.5f);

        // 登録されたメッセージの数だけ順番にループ処理を行う
        for (int i = 0; i < messages.Length; i++)
        {
            messageText.text = messages[i];
            messageText.gameObject.SetActive(true);

            // 指定した秒数だけ画面に留める
            yield return new WaitForSeconds(displayDuration);

            // 最後のメッセージでなければ、次のメッセージに移る前に一度非表示にする
            if (i < messages.Length - 1)
            {
                messageText.gameObject.SetActive(false);
                yield return new WaitForSeconds(intervalTime);
            }
        }

        // 最後のメッセージの表示時間が終わったら、テキストオブジェクトを非表示（見えない状態）にする
        messageText.gameObject.SetActive(false);

        // すべてのメッセージの表示が完了したあと、少し余韻として待機
        yield return new WaitForSeconds(1.0f);

        // 指定された次のシーンへ自動的に遷移する
        SceneManager.LoadScene(nextSceneName);
    }
}