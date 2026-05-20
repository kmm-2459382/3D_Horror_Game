using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class EnemyAI : MonoBehaviour
{
    // エネミーの状態を定義
    private enum EnemyState
    {
        PatrolWalk, // 巡回（歩き）
        PatrolIdle, // 巡回（立ち止まり）
        Chase       // プレイヤー追跡（走り）
    }

    [Header("--- 索敵設定 ---")]
    [SerializeField] private float searchRadius = 8f;       // プレイヤーを検知する半径
    [SerializeField] private float searchAngle = 120f;     // プレイヤーを検知する視野角（度数法）

    [Header("--- 移動速度設定 ---")]
    [SerializeField] private float walkSpeed = 1.5f;        // 巡回時の歩行速度
    [SerializeField] private float runSpeed = 4.0f;         // 追跡時の走行速度

    [Header("--- 巡回（パトロール）設定 ---")]
    [SerializeField] private float patrolRadius = 5f;       // ランダムに動く範囲の半径
    [SerializeField] private float minIdleTime = 1.5f;      // 立ち止まる最小時間
    [SerializeField] private float maxIdleTime = 4.0f;      // 立ち止まる最大時間

    // コンポーネントの参照保持用
    private NavMeshAgent agent;
    private Animator animator;
    private Transform playerTransform;

    // 内部制御用変数
    private EnemyState currentState;
    private float idleTimer;
    private float currentIdleDuration;

    // アニメーターのパラメーターID
    private readonly int speedHash = Animator.StringToHash("Speed");
    private readonly int isChasingHash = Animator.StringToHash("IsChasing");

    private void Awake()
    {
        // 必要なコンポーネントを自動取得
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        // "Player" タグが付いたオブジェクトを探して位置への参照を取得
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogError("ヒエラルキー上に 'Player' タグに設定されたオブジェクトが見つかりません。");
        }

        // エラー防止対策：もし開始時にNavMeshの上に載っていなければ、強制的に一番近いNavMesh上に接地させる
        if (!agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 3.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            else
            {
                Debug.LogWarning($"{gameObject.name} の近くに NavMesh が見つかりません。床がベイクされているか、配置を確認してください。");
            }
        }

        // 初期状態は立ち止まり（パトロール待機）からスタート
        TransitionToState(EnemyState.PatrolIdle);
    }

    private void Update()
    {
        // プレイヤーがシーン内に存在しない、またはAgentがNavMesh上に有効化されていない場合はエラー防止のため処理をスキップ
        if (playerTransform == null || !agent.isOnNavMesh || !agent.isActiveAndEnabled) return;

        // 毎フレーム、プレイヤーが視界内（索敵範囲内）にいるかをチェック
        bool canSeePlayer = CheckForPlayer();

        // 状態（ステート）に応じた行動分岐
        switch (currentState)
        {
            case EnemyState.PatrolWalk:
                HandlePatrolWalk(canSeePlayer);
                break;

            case EnemyState.PatrolIdle:
                HandlePatrolIdle(canSeePlayer);
                break;

            case EnemyState.Chase:
                HandleChase(canSeePlayer);
                break;
        }

        // アニメーターへ現在の移動速度を伝える
        animator.SetFloat(speedHash, agent.velocity.magnitude);
    }

    /// <summary>
    /// 状態を切り替え、それに伴う初期設定（速度やアニメーションフラグ）を行う関数
    /// </summary>
    private void TransitionToState(EnemyState newState)
    {
        currentState = newState;

        // 状態遷移時にNavMesh上にいない場合はNavMesh操作をスキップしてエラーを防ぐ
        if (!agent.isOnNavMesh) return;

        switch (currentState)
        {
            case EnemyState.PatrolIdle:
                agent.isStopped = true; // ナビゲーションによる移動を一時停止
                agent.velocity = Vector3.zero;
                animator.SetBool(isChasingHash, false);

                // 次に動き出すまでの時間をランダムで決定
                currentIdleDuration = Random.Range(minIdleTime, maxIdleTime);
                idleTimer = 0f;
                break;

            case EnemyState.PatrolWalk:
                agent.isStopped = false;
                agent.speed = walkSpeed; // 歩き速度を適用
                animator.SetBool(isChasingHash, false);

                // ランダムな次の目的地を設定
                SetRandomDestination();
                break;

            case EnemyState.Chase:
                agent.isStopped = false;
                agent.speed = runSpeed; // 走り速度を適用
                animator.SetBool(isChasingHash, true);
                break;
        }
    }

    /// <summary>
    /// 巡回移動中の処理
    /// </summary>
    private void HandlePatrolWalk(bool canSeePlayer)
    {
        if (canSeePlayer)
        {
            TransitionToState(EnemyState.Chase);
            return;
        }

        // 目的地に十分近づいた、または経路計算が完了してこれ以上進めなくなった場合
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            TransitionToState(EnemyState.PatrolIdle);
        }
    }

    /// <summary>
    /// 巡回中の立ち止まり（待機）処理
    /// </summary>
    private void HandlePatrolIdle(bool canSeePlayer)
    {
        if (canSeePlayer)
        {
            TransitionToState(EnemyState.Chase);
            return;
        }

        // 時間を経過させ、規定時間を超えたら再び歩き出す
        idleTimer += Time.deltaTime;
        if (idleTimer >= currentIdleDuration)
        {
            TransitionToState(EnemyState.PatrolWalk);
        }
    }

    /// <summary>
    /// プレイヤー追跡中の処理
    /// </summary>
    private void HandleChase(bool canSeePlayer)
    {
        if (!canSeePlayer)
        {
            // プレイヤーを見失ったら巡回（立ち止まり）に戻る
            TransitionToState(EnemyState.PatrolIdle);
            return;
        }

        // 常にプレイヤーの現在地を目的地として更新し続ける
        agent.SetDestination(playerTransform.position);
    }

    /// <summary>
    /// ランダムな目的地をNavMesh上で計算して設定する
    /// </summary>
    private void SetRandomDestination()
    {
        if (!agent.isOnNavMesh) return;

        // 自身の現在地を中心に、設定されたパトロール半径内のランダムな位置を球状に取得
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection += transform.position;

        NavMeshHit hit;
        // 計算された位置から一番近いNavMesh上の有効な座標（着地点）を検索する
        if (NavMesh.SamplePosition(randomDirection, out hit, patrolRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    /// <summary>
    /// プレイヤーが設定された視界（距離・角度）に入っているかを判定する
    /// </summary>
    private bool CheckForPlayer()
    {
        Vector3 directionToPlayer = playerTransform.position - transform.position;
        float distance = directionToPlayer.magnitude;

        // 1. そもそも索敵半径を超えて離れている場合は不成立
        if (distance > searchRadius) return false;

        // 2. 視界の「角度」をチェック
        float angle = Vector3.Angle(transform.forward, directionToPlayer);

        // なす角が「視野角の半分」以下であれば、正面の扇形の中にプレイヤーがいると判定
        if (angle <= searchAngle * 0.5f)
        {
            // 3. 壁などの遮蔽物を考慮するためのレイキャスト
            Vector3 rayStart = transform.position + Vector3.up * 1f;
            Vector3 rayDirection = (playerTransform.position + Vector3.up * 1f) - rayStart;

            RaycastHit hit;
            if (Physics.Raycast(rayStart, rayDirection, out hit, searchRadius))
            {
                if (hit.collider.CompareTag("Player"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, searchRadius);

        // 視野角の限界線を2本描画して扇形を表現
        Vector3 forward = transform.forward;
        Vector3 leftBoundary = Quaternion.Euler(0, -searchAngle * 0.5f, 0) * forward;
        Vector3 rightBoundary = Quaternion.Euler(0, searchAngle * 0.5f, 0) * forward;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + leftBoundary * searchRadius);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary * searchRadius);
    }
}