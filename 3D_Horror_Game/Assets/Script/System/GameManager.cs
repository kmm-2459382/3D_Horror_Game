using UnityEngine;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("--- 監視するドア ---")]
    [Tooltip("屋上のドア（NewDoorMech）をここにセットします")]
    [SerializeField] private NewDoorMech targetRooftopDoor;

    [Header("--- 敵AIの参照 ---")]
    [Tooltip("シーン内の敵（EnemyAI）をここにセットします")]
    [SerializeField] private EnemyAI enemyAI;

    [Header("--- 屋上イベント演出設定 ---")]
    [Tooltip("ドアを開けた瞬間に強制的に見つめる対象（敵の顔など）")]
    [SerializeField] private Transform lookTargetObject;

    [Tooltip("カメラがターゲットを自動で振り向く速度")]
    [SerializeField] private float cameraTurnSpeed = 5f;

    [Tooltip("プレイヤーが強制的に後ずさりして移動する目的地")]
    [SerializeField] private Transform retreatDestination;

    [Tooltip("後ずさり移動のスピード")]
    [SerializeField] private float retreatSpeed = 2f;

    [Tooltip("後ずさり完了後、プレイヤーの自由化から敵が動き出すまでの時間（秒）")]
    [SerializeField] private float delayBeforeEnemyMoves = 2.0f;

    [Header("--- プレイヤーオブジェクトの参照 ---")]
    [SerializeField] private GameObject playerObject;

    private bool isEventTriggered = false;
    private bool isRetreating = false;
    private bool isLookingAtTarget = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // ゲーム開始時、敵は常に見失わない設定にしつつ、完全にその場に固定（スタン）
        if (enemyAI != null)
        {
            enemyAI.SetAlwaysChase(true);
            enemyAI.ForceStunState(true);
        }
    }

    private void Update()
    {
        if (targetRooftopDoor == null || playerObject == null) return;

        // 【トリガー】指定のドアが開いた瞬間
        if (targetRooftopDoor.doorBool && !isEventTriggered)
        {
            isEventTriggered = true;
            StartCoroutine(RooftopEventCoroutine());
        }

        // カメラを常に目標に向け続ける（水平＋上下の強制同期）
        if (isLookingAtTarget && lookTargetObject != null)
        {
            UpdateCameraRotation();
        }

        // 後ずさり移動の実行
        if (isRetreating && retreatDestination != null)
        {
            UpdatePlayerRetreat();
        }
    }

    private IEnumerator RooftopEventCoroutine()
    {
        Debug.Log("【イベント開始】1. プレイヤーの入力を禁止し、敵の方へカメラを振り向かせます。");
        // ─── ① 振り向く（プレイヤーも動かない） ───
        DisablePlayerInputs();
        isLookingAtTarget = true;

        // カメラがある程度目標を捉えるまで、少しだけ待機
        yield return new WaitForSeconds(1.0f);

        Debug.Log("【イベント進行】2. プレイヤーの後ずさり移動を開始します（カメラ注視は維持）。");
        // ─── ② プレイヤーが後ずさり（カメラは常に目標に向いている） ───
        isRetreating = true;

        // 後ずさり目的地に到着するまで待機
        float tolerance = 0.2f;
        while (isRetreating && Vector3.Distance(playerObject.transform.position, retreatDestination.position) > tolerance)
        {
            yield return null;
        }
        isRetreating = false; // 到着したので後ずさり終了

        Debug.Log("【イベント進行】3. プレイヤーの移動とカメラ操作を自由にします。敵はまだ動きません。");
        // ─── ③ カメラと移動が自由にできる ───
        isLookingAtTarget = false; // カメラの強制注視を終了
        EnablePlayerInputs();       // プレイヤーの操作を解放

        // 敵が動き出すまでの「タメ（猶予時間）」を待つ
        yield return new WaitForSeconds(delayBeforeEnemyMoves);

        Debug.Log("【イベント完了】4. 敵の固定を解除！自由に動き出します。");
        // ─── ④ 敵が自由に動ける ───
        if (enemyAI != null)
        {
            enemyAI.ForceStunState(false);
        }
    }

    /// <summary>
    /// フィードバックループ（暴走）を防ぎ、上下左右すべてターゲットの中心を正確にロックオンする修正版
    /// </summary>
    private void UpdateCameraRotation()
    {
        // ターゲットの「中心座標」を自動で計算する
        Vector3 targetCenterPos = lookTargetObject.position;
        if (lookTargetObject.TryGetComponent<Collider>(out var collider))
        {
            targetCenterPos = collider.bounds.center;
        }
        else if (lookTargetObject.TryGetComponent<Renderer>(out var renderer))
        {
            targetCenterPos = renderer.bounds.center;
        }

        var fpController = playerObject.GetComponent<StarterAssets.FirstPersonController>();
        if (fpController != null && fpController.CinemachineCameraTarget != null)
        {
            // 1. カメラの基準点（CinemachineCameraTarget）から、ターゲットの中心への方向ベクトルを計算
            Vector3 lookDirection = targetCenterPos - fpController.CinemachineCameraTarget.transform.position;

            if (lookDirection.sqrMagnitude > 0.001f)
            {
                // ターゲットの中心を100%正確に捉えるための理想の目標ワールド角度
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);

                // 2. ─── 【重要修正：暴走対策】 ───
                // Camera.main から回転を取るとCinemachineの遅延を二重に計算して上空にすっ飛ぶため、
                // 現在の「プレイヤーの体(Y軸)」と「カメラのローカル(X軸)」から、現在の正確なワールド回転を合成します。
                float currentX = fpController.CinemachineCameraTarget.transform.localEulerAngles.x;
                float currentY = playerObject.transform.eulerAngles.y;

                // Unityのインサイド処理に合わせ、Euler角のマイナス値を考慮したクォータニオンを生成
                Quaternion currentLogicalRotation = Quaternion.Euler(currentX, currentY, 0f);

                // 3. 現在の論理的な回転から、目標の回転へ向けて滑らかに補間（Slerp）
                Quaternion smoothedRotation = Quaternion.Slerp(currentLogicalRotation, targetRotation, cameraTurnSpeed * Time.deltaTime);

                // 4. FirstPersonControllerの専用同期関数にこの角度を流し込む
                fpController.ForceSyncCameraRotation(smoothedRotation);
            }
        }
    }

    private void UpdatePlayerRetreat()
    {
        // 後ずさり目的地に向けて直線的に移動
        playerObject.transform.position = Vector3.MoveTowards(
            playerObject.transform.position,
            retreatDestination.position,
            retreatSpeed * Time.deltaTime
        );
    }

    private void DisablePlayerInputs()
    {
        // ─── 【重要修正】コンポーネントを false にせず、用意された IsLocked フラグを true にする ───
        var fpController = playerObject.GetComponent<StarterAssets.FirstPersonController>();
        if (fpController != null)
        {
            fpController.IsLocked = true; // これによりFirstPersonController内部の移動・視点移動のアップデートが停止します
        }

        // キャラクターコントローラー自体の物理干渉を一時的に切る（滑らかな後ずさりのため）
        var charController = playerObject.GetComponent<CharacterController>();
        if (charController != null)
        {
            charController.enabled = false;
        }
    }

    private void EnablePlayerInputs()
    {
        // キャラクターコントローラーを再起動
        var charController = playerObject.GetComponent<CharacterController>();
        if (charController != null)
        {
            charController.enabled = true;
        }

        // ─── 【重要修正】IsLocked を解除してプレイヤーの自由入力を復活させる ───
        var fpController = playerObject.GetComponent<StarterAssets.FirstPersonController>();
        if (fpController != null)
        {
            fpController.IsLocked = false;
        }
    }

    public void OnPlayerCaught()
    {
        Debug.Log("【屋上イベント】プレイヤーが敵に捕まりました。見失わないデバッグフラグをOFFにします。");
        if (enemyAI != null)
        {
            enemyAI.SetAlwaysChase(false);
        }
    }
}