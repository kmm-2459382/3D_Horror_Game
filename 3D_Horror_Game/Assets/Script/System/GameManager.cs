using UnityEngine;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GamePhase
    {
        BeforeRooftopEvent,   // 屋上イベント前の探索フェーズ（前半戦）
        Phase4_RooftopEvent,  // 屋上確定演出中
        Phase5_Collection,    // 目を覚ました後の5つのボタン収集（後半戦）
        Phase6_EscapeReady,   // 脱出可能な状態
        Phase7_GameClear      // ゲームクリア
    }

    [Header("--- 現在のゲームフェーズ ---")]
    [SerializeField] private GamePhase currentPhase = GamePhase.BeforeRooftopEvent;
    public GamePhase CurrentPhase => currentPhase;

    [Header("--- アイテム・フラグ管理 ---")]
    [SerializeField] private bool hasFlashlight = false;
    [SerializeField] private bool hasRooftopKey = false;
    public bool HasFlashlight => hasFlashlight;
    public bool HasRooftopKey => hasRooftopKey;

    [Header("--- 外部コンポーネントの参照 ---")]
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private FlashlightController flashlightController;
    [SerializeField] private GameObject playerObject;

    [Header("--- 後半戦（ボタン）の設定 ---")]
    [SerializeField] private int requiredItemCount = 5;
    private int currentItemCount = 0;

    [Header("--- リスポーン位置 ---")]
    [SerializeField] private Transform wakeUpPoint;

    [Header("--- 演出時間の設定 ---")]
    [Tooltip("扉を開けて、お互いフリーズして見つめ合う時間（秒）")]
    [SerializeField] private float stareDuration = 3.0f;
    [Tooltip("自動で後ずさり（後退）する時間（秒）")]
    [SerializeField] private float backDuration = 2.0f;
    [Tooltip("自動後ずさりのスピード")]
    [SerializeField] private float backSpeed = 1.5f;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    private void Start()
    {
        // 懐中電灯の初期OFF制御のみ（プレイヤーのスクリプト等には一切干渉しません）
        if (flashlightController != null)
        {
            flashlightController.gameObject.SetActive(false);
        }
    }

    public void OnGetFlashlight()
    {
        if (hasFlashlight) return;
        hasFlashlight = true;
        if (flashlightController != null) flashlightController.gameObject.SetActive(true);
    }

    public void OnGetRooftopKey()
    {
        if (hasRooftopKey) return;
        hasRooftopKey = true;
    }

    // 💡 屋上の扉を開けた瞬間に呼び出される関数
    public void OnOpenRooftopDoor()
    {
        if (currentPhase != GamePhase.BeforeRooftopEvent || !hasRooftopKey) return;
        currentPhase = GamePhase.Phase4_RooftopEvent;
        StartCoroutine(RooftopEventRoutine());
    }

    private IEnumerator RooftopEventRoutine()
    {
        Debug.Log("【屋上イベント】開始：お互い見つめ合います。");
        CharacterController cc = playerObject != null ? playerObject.GetComponent<CharacterController>() : null;

        // 💡 プレイヤーのスクリプトや入力を一切OFFにしません（バグ回避のため）
        // 見つめ合っている間（stareDuration）はそのまま待機します
        yield return new WaitForSeconds(stareDuration);

        // 💡 自動で後ずさり（後退）する演出
        Debug.Log("【屋上イベント】プレイヤーが恐怖で後ずさりします。");
        float elapsed = 0f;
        while (elapsed < backDuration)
        {
            if (cc != null)
            {
                // キャラクターコントローラーを使い、スクリプトの有効化状態に関わらず強制移動
                Vector3 moveDir = -playerObject.transform.forward * backSpeed;
                moveDir.y = Physics.gravity.y;
                cc.Move(moveDir * Time.deltaTime);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.Log("【屋上イベント】確定チェイス開始！");
        if (audioManager != null)
        {
            audioManager.ChangeBGMState(AudioManager.BGMMode.AlertChase);
        }
    }

    // 💡 敵に接触して、ジャンプスケア演出が終わった後に呼び出される関数
    public void OnPlayerCaughtAtRooftop()
    {
        if (currentPhase != GamePhase.Phase4_RooftopEvent) return;
        StartCoroutine(TransitionToCollectionPhaseRoutine());
    }

    private IEnumerator TransitionToCollectionPhaseRoutine()
    {
        Debug.Log("【暗転】後半戦（ボタン集め）の準備をします…");
        currentPhase = GamePhase.Phase5_Collection;

        CharacterController cc = playerObject != null ? playerObject.GetComponent<CharacterController>() : null;

        // 💡 座標ワープの瞬間だけ、挙動が荒れるのを防ぐために一時的にCCをOFFにします
        if (cc != null) cc.enabled = false;

        if (playerObject != null && wakeUpPoint != null)
        {
            playerObject.transform.position = wakeUpPoint.position;
            playerObject.transform.rotation = wakeUpPoint.rotation;
        }

        // ワープを物理エンジンに反映させるため一瞬だけ待つ
        yield return new WaitForFixedUpdate();

        // 💡 ワープ完了後、即座に戻します
        if (cc != null) cc.enabled = true;

        if (audioManager != null)
        {
            audioManager.ChangeBGMState(AudioManager.BGMMode.LostTracking);
        }

        yield return new WaitForSeconds(2.0f);
        Debug.Log("【後半戦スタート】敵は通常AIになり、5つのボタンがアパートに出現しました。");
    }

    public void OnPickUpCollectionItem()
    {
        if (currentPhase != GamePhase.Phase5_Collection) return;
        currentItemCount++;
        if (currentItemCount >= requiredItemCount)
        {
            currentPhase = GamePhase.Phase6_EscapeReady;
            if (audioManager != null) audioManager.ChangeBGMState(AudioManager.BGMMode.AlertChase);
        }
    }

    public void OnEscapeSuccess()
    {
        if (currentPhase != GamePhase.Phase6_EscapeReady) return;
        currentPhase = GamePhase.Phase7_GameClear;
        Debug.Log("【GameManager】脱出成功！クリア！");
    }
}