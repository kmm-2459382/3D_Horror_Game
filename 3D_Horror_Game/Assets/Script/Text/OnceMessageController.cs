using UnityEngine;
using TMPro;
using System.Collections;

public class OnceMessageController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("一度だけ表示させたいTextMeshProUIコンポーネントを指定します")]
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("Display Settings")]
    [Tooltip("シーン遷移後にテキストを表示し続ける時間（秒）")]
    [SerializeField] private float displayDuration = 3.0f;

    void Start()
    {
        // 参照が設定されていない場合は安全のため処理を中断
        if (messageText == null)
        {
            Debug.LogWarning("MessageTextが割り当てられていません。インスペクターを確認してください。");
            return;
        }

        // シーン開始時は確実にテキストを表示状態にする（またはコルーチンで一定時間後に非表示にする）
        messageText.gameObject.SetActive(true);

        // 指定秒数後にテキストを非表示にするコルーチンを開始
        StartCoroutine(HideMessageAfterSeconds(displayDuration));
    }

    private IEnumerator HideMessageAfterSeconds(float seconds)
    {
        // 指定した秒数だけ待機する（3D空間の物理演算やタイムスケールに依存しないようRealtimeを使うことも可能）
        yield return new WaitForSeconds(seconds);

        // テキストを非表示にする（これで「一度だけ表示して消える」動作になります）
        messageText.gameObject.SetActive(false);
    }
}