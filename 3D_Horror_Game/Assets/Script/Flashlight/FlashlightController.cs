using UnityEngine;
// 新しいInput Systemを使用するために必要な名前空間
using UnityEngine.InputSystem;

public class FlashlightController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField]
    private bool startsOn = false; // ゲーム開始時にライトがついているかどうか

    [Header("References")]
    [SerializeField]
    private Light targetLight; // 操作対象となるSpotlightコンポーネント

    [Tooltip("💡 懐中電灯の本体オブジェクト（3Dモデルなど）。これが無効化（非アクティブ）されている時は、スイッチを押してもON/OFFできなくなります")]
    [SerializeField]
    private GameObject flashlightObject;

    [Header("Audio (Optional)")]
    [SerializeField]
    private AudioClip toggleSound; // スイッチを切り替えた時の効果音

    private AudioSource audioSource;
    private bool isLightOn;

    private void Start()
    {
        // 対象のライトがインスペクターで設定されていない場合の警告・自動取得
        if (targetLight == null)
        {
            targetLight = GetComponentInChildren<Light>();
            if (targetLight == null)
            {
                Debug.LogError("Target Lightが設定されていません！インスペクターからLightをアサインするか、子オブジェクトにLightを配置してください。");
            }
        }

        // 💡 もし懐中電灯オブジェクトの指定が空っぽなら、自動的にライトのGameObjectを身代わりとしてセットする
        if (flashlightObject == null && targetLight != null)
        {
            flashlightObject = targetLight.gameObject;
        }

        // オーディオソースの取得または追加
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && toggleSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // 初期状態の設定
        isLightOn = startsOn;
        if (targetLight != null)
        {
            targetLight.enabled = isLightOn;
        }
    }

    /// <summary>
    /// 新しいInput Systemからメッセージを受け取るメソッド
    /// プレイヤー入力の「Flashlight」アクションに対応します
    /// </summary>
    public void OnFlashlight(InputValue value)
    {
        // キーが押された瞬間のみ処理を実行する
        if (value.isPressed)
        {
            ToggleFlashlight();
        }
    }

    /// <summary>
    /// 懐中電灯のON/OFFを切り替える処理
    /// </summary>
    private void ToggleFlashlight()
    {
        if (targetLight == null) return;

        // 💡【最重要対策】参照している懐中電灯オブジェクトが、画面上（Hierarchy上）で無効化されていたら処理を完全に中断する
        if (flashlightObject != null && !flashlightObject.activeInHierarchy)
        {
            // 無効化されているので、ライトの切り替えもSE再生もせずここでリターン
            return;
        }

        // 状態を反転
        isLightOn = !isLightOn;

        // ライトの有効化/無効化を切り替え
        targetLight.enabled = isLightOn;

        // 効果音の再生
        if (audioSource != null && toggleSound != null)
        {
            audioSource.PlayOneShot(toggleSound);
        }
    }
}