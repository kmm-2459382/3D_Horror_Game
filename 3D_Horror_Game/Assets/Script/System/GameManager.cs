using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GamePhase
    {
        Phase1_Start,
        Phase2_GetFlashlight,
        Phase3_GetRooftopKey,
        Phase4_RooftopEvent,    // 屋上確定演出中
        Phase5_Collection,      // 后半戦（ボタン収集）
        Phase6_EscapeReady,
        Phase7_GameClear
    }

    [Header("--- 現在のゲームフェーズ ---")]
    [SerializeField] private GamePhase currentPhase = GamePhase.Phase3_GetRooftopKey;
    public GamePhase CurrentPhase => currentPhase;

    [Header("--- 監視するドアの参照 ---")]
    [Tooltip("屋上のドア（NewDoorMech）をここにセットします")]
    [SerializeField] private NewDoorMech targetRooftopDoor;

    [Header("--- 外部コンポーネントの参照 ---")]
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private GameObject playerObject;

    [Header("--- 演出時間の設定 ---")]
    [Tooltip("扉が開いてから、敵が動き出すまでの見つめ合い時間（秒）")]
    [SerializeField] private float stareDuration = 3.0f;

    [Header("--- 捕まった後の切り替え設定 ---")]
    [Tooltip("シーンチェンジで後半戦に移行する場合はチェックを入れてください")]
    [SerializeField] private bool useSceneChange = true;

    [Tooltip("移動先のシーン名（例: Apartment_Phase2）")]
    [SerializeField] private string nextSceneName = "Apartment_Phase2";

    [Header("同じシーン内で切り替える場合の設定")]
    [SerializeField] private GameObject oldObjects;
    [SerializeField] private GameObject newObjects;

    private bool isEventStarted = false;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    private void Update()
    {
        // 参照しているドアが開けられた瞬間を初回のみ検知
        if (!isEventStarted && targetRooftopDoor != null && targetRooftopDoor.doorBool)
        {
            isEventStarted = true;
            currentPhase = GamePhase.Phase4_RooftopEvent;
            StartCoroutine(RooftopEventRoutine());
        }
    }

    private IEnumerator RooftopEventRoutine()
    {
        Debug.Log("【屋上イベント】ドアが開きました。見つめ合い開始（プレイヤー操作不能・敵完全停止）。");

        float elapsed = 0f;
        while (elapsed < stareDuration)
        {
            // 💡 見つめ合っている間、毎フレームプレイヤーの入力を完全にゼロ（操作不能）にする
            if (playerObject != null)
            {
                var inputs = playerObject.GetComponent<StarterAssets.StarterAssetsInputs>();
                if (inputs != null)
                {
                    inputs.move = Vector2.zero;
                    inputs.look = Vector2.zero;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.Log("【屋上イベント】見つめ合い終了。プレイヤー操作解放＆敵の追従突進開始！");

        if (audioManager != null)
        {
            audioManager.ChangeBGMState(AudioManager.BGMMode.AlertChase);
        }
    }

    /// <summary>
    /// 敵に接触した瞬間に呼び出される関数
    /// </summary>
    public void OnPlayerCaughtAtRooftop()
    {
        Debug.Log("【屋上イベント】プレイヤーが捕まりました。");
        currentPhase = GamePhase.Phase5_Collection;

        if (useSceneChange)
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            if (oldObjects != null) oldObjects.SetActive(false);
            if (newObjects != null) newObjects.SetActive(true);
            Debug.Log("オブジェクトの有効・無効を切り替えました。");
        }
    }

    // エラー防止用のダミープロパティ・関数
    public bool HasFlashlight => true;
    public bool HasRooftopKey => true;
    public void OnGetFlashlight() { }
    public void OnGetRooftopKey() { }
    public void OnPickUpCollectionItem() { }
}