using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))] // 衝突判定に必須なRigidbodyを強制
public class EnemyAI : MonoBehaviour
{
    // エネミーの状態を定義
    private enum EnemyState
    {
        PatrolWalk,  // 巡回（歩き）
        PatrolIdle,  // 巡回（立ち止まり）
        Chase,       // プレイヤー追跡（走り）
        LookAround   // 見失った場所の周辺を警戒捜索
    }

    [Header("--- 索敵設定 ---")]
    [SerializeField] private float searchRadius = 8f;       // プレイヤーを検知する半径
    [SerializeField] private float searchAngle = 120f;     // プレイヤーを検知する視野角

    [Header("--- 移動速度設定 ---")]
    [SerializeField] private float walkSpeed = 1.5f;        // 巡回時の歩行速度
    [SerializeField] private float runSpeed = 4.0f;         // 追跡時の走行速度

    [Header("--- アニメーション再生速度調整 ---")]
    [SerializeField] private float walkAnimSpeedMultiplier = 1.0f; // 歩行時のアニメーション速度倍率
    [SerializeField] private float runAnimSpeedMultiplier = 0.5f;  // 走行時のアニメーション速度倍率

    [Header("--- 巡回（パトロール）設定 ---")]
    [SerializeField] private float patrolRadius = 5f;       // ランダムに動く範囲の半径
    [SerializeField] private float minIdleTime = 1.5f;      // 立ち止まる最小時間
    [SerializeField] private float maxIdleTime = 4.0f;      // 立ち止まる最大時間

    [Header("--- 見失った後の執着設定 ---")]
    [SerializeField] private float chasingForgetTime = 5.0f; // プレイヤーを見失ってから諦めるまでの秒数
    [SerializeField] private float searchAroundRadius = 4f;  // 見失った地点の周りを何メートル索敵するか

    // コンポーネントの参照保持用
    private NavMeshAgent agent;
    private Animator animator;
    private Transform playerTransform;

    // 内部制御用変数
    private EnemyState currentState;
    private float idleTimer;
    private float currentIdleDuration;
    private float forgetTimer;             // 見失ってからの経過時間計測用
    private Vector3 lastSeenPosition;     // プレイヤーを最後に見失った座標

    // 壁引っかかり（スタック）対策用の変数
    private Vector3 lastPosition;
    private float stuckTimer;
    private const float StuckCheckInterval = 0.5f;
    private const float StuckDistanceThreshold = 0.05f;

    // アニメーターのパラメーターID
    private readonly int speedHash = Animator.StringToHash("Speed");
    private readonly int isChasingHash = Animator.StringToHash("IsChasing");

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogError("ヒエラルキー上に 'Player' タグに設定されたオブジェクトが見つかりません。");
        }

        if (!agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 3.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }

        lastPosition = transform.position;
        TransitionToState(EnemyState.PatrolIdle);
    }

    private void Update()
    {
        if (playerTransform == null || !agent.isOnNavMesh || !agent.isActiveAndEnabled) return;

        bool canSeePlayer = CheckForPlayer();

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

            case EnemyState.LookAround:
                HandleLookAround(canSeePlayer);
                break;
        }

        UpdateAnimationSpeed();
    }

    private void UpdateAnimationSpeed()
    {
        float currentPhysicalSpeed = agent.velocity.magnitude;
        float finalAnimSpeed = 0f;

        if (currentState == EnemyState.Chase)
        {
            finalAnimSpeed = currentPhysicalSpeed * runAnimSpeedMultiplier;
        }
        else if (currentState == EnemyState.PatrolWalk || currentState == EnemyState.LookAround)
        {
            finalAnimSpeed = currentPhysicalSpeed * walkAnimSpeedMultiplier;
        }
        else
        {
            finalAnimSpeed = 0f;
        }

        animator.SetFloat(speedHash, finalAnimSpeed);
    }

    private void TransitionToState(EnemyState newState)
    {
        currentState = newState;

        if (!agent.isOnNavMesh) return;

        stuckTimer = 0f;
        lastPosition = transform.position;

        switch (currentState)
        {
            case EnemyState.PatrolIdle:
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                animator.SetBool(isChasingHash, false);
                currentIdleDuration = Random.Range(minIdleTime, maxIdleTime);
                idleTimer = 0f;
                break;

            case EnemyState.PatrolWalk:
                agent.isStopped = false;
                agent.speed = walkSpeed;
                animator.SetBool(isChasingHash, false);
                SetRandomDestination(transform.position, patrolRadius);
                break;

            case EnemyState.Chase:
                agent.isStopped = false;
                agent.speed = runSpeed;
                animator.SetBool(isChasingHash, true);
                break;

            case EnemyState.LookAround:
                agent.isStopped = false;
                agent.speed = walkSpeed;
                animator.SetBool(isChasingHash, false);
                forgetTimer = 0f;
                agent.SetDestination(lastSeenPosition);
                break;
        }
    }

    private void HandlePatrolWalk(bool canSeePlayer)
    {
        if (canSeePlayer)
        {
            TransitionToState(EnemyState.Chase);
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            TransitionToState(EnemyState.PatrolIdle);
            return;
        }

        CheckStuckStatus();
    }

    private void HandlePatrolIdle(bool canSeePlayer)
    {
        if (canSeePlayer)
        {
            TransitionToState(EnemyState.Chase);
            return;
        }

        idleTimer += Time.deltaTime;
        if (idleTimer >= currentIdleDuration)
        {
            TransitionToState(EnemyState.PatrolWalk);
        }
    }

    private void HandleChase(bool canSeePlayer)
    {
        if (!canSeePlayer)
        {
            lastSeenPosition = playerTransform.position;
            TransitionToState(EnemyState.LookAround);
            return;
        }

        agent.SetDestination(playerTransform.position);
    }

    private void HandleLookAround(bool canSeePlayer)
    {
        if (canSeePlayer)
        {
            TransitionToState(EnemyState.Chase);
            return;
        }

        forgetTimer += Time.deltaTime;
        if (forgetTimer >= chasingForgetTime)
        {
            TransitionToState(EnemyState.PatrolWalk);
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            SetRandomDestination(lastSeenPosition, searchAroundRadius);
        }

        CheckStuckStatus();
    }

    private void CheckStuckStatus()
    {
        float movedDistance = Vector3.Distance(transform.position, lastPosition);

        if (movedDistance < StuckDistanceThreshold)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= StuckCheckInterval)
            {
                if (currentState == EnemyState.LookAround)
                    TransitionToState(EnemyState.PatrolWalk);
                else
                    TransitionToState(EnemyState.PatrolIdle);
            }
        }
        else
        {
            stuckTimer = 0f;
            lastPosition = transform.position;
        }
    }

    private void SetRandomDestination(Vector3 centerOrigin, float radius)
    {
        if (!agent.isOnNavMesh) return;

        // 捜索中やパトロール中に、ドアが閉まっていても向こう側に行けるよう
        // ドアオブジェクトそのものはNavMeshのベイク対象から外し、床を地続きに設定してください。
        Vector3 randomDirection = Random.insideUnitSphere * radius;
        randomDirection += centerOrigin;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, radius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    private bool CheckForPlayer()
    {
        Vector3 directionToPlayer = playerTransform.position - transform.position;
        float distance = directionToPlayer.magnitude;

        if (distance > searchRadius) return false;

        float angle = Vector3.Angle(transform.forward, directionToPlayer);

        if (angle <= searchAngle * 0.5f)
        {
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

    /// <summary>
    /// 【重要】敵が何かに体当たりした瞬間の衝突イベント処理
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        // ぶつかった相手（またはその親）が "Door" タグを持っているか調べる
        if (collision.gameObject.CompareTag("Door") || (collision.transform.parent != null && collision.transform.parent.CompareTag("Door")))
        {
            // プレイヤーのRayと同様、コンポーネントを自身または親から安全に取得する
            NewDoorMech doorScript = collision.gameObject.GetComponentInParent<NewDoorMech>();

            if (doorScript != null)
            {
                // ドアが閉まっている場合のみ、開くフラグをONにする
                if (!doorScript.doorBool)
                {
                    doorScript.doorBool = true;
                    // デバッグログ：正常にぶつかって開けたことをコンソールに表示
                    Debug.Log("敵が扉に接触したため、扉を開けました。");
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, searchRadius);

        Vector3 forward = transform.forward;
        Vector3 leftBoundary = Quaternion.Euler(0, -searchAngle * 0.5f, 0) * forward;
        Vector3 rightBoundary = Quaternion.Euler(0, searchAngle * 0.5f, 0) * forward;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + leftBoundary * searchRadius);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary * searchRadius);

        if (currentState == EnemyState.LookAround)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(lastSeenPosition, searchAroundRadius);
        }
    }
}