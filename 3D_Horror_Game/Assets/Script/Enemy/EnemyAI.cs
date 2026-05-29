using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyAI : MonoBehaviour
{
    private enum EnemyState
    {
        PatrolWalk,      // 巡回（歩き）
        PatrolIdle,      // 巡回（立ち止まり）
        Chase,           // プレイヤー追跡（目視できている状態のダッシュ）
        LinearDash,      // 見失った瞬間、プレイヤーが走っていた方向の直線先へ猛突進する状態
        PredictedChase,  // 突進後、敵の現在地から近い足跡をスマートに選別して辿る状態
        LookAround       // 最終地点まで追いついた後、周辺を警戒捜索
    }

    [Header("--- 索敵設定 ---")]
    [SerializeField] private float searchRadius = 12f;      // プレイヤーを検知する半径
    [SerializeField] private float searchAngle = 140f;      // プレイヤーを検知する視野角

    [Header("--- 移動速度設定 ---")]
    [SerializeField] private float walkSpeed = 1.5f;        // 巡回・警戒時の歩行速度
    [SerializeField] private float runSpeed = 4.5f;         // 追跡・突進時の走行速度

    [Header("--- アニメーション再生速度調整 ---")]
    [SerializeField] private float walkAnimSpeedMultiplier = 1.0f; // 歩行時のアニメーション速度倍率
    [SerializeField] private float runAnimSpeedMultiplier = 1.0f;  // 走行時のアニメーション速度倍率

    [Header("--- 巡回（パトロール）設定 ---")]
    [SerializeField] private float patrolRadius = 6f;       // ランダムに動く範囲の半径
    [SerializeField] private float minIdleTime = 1.5f;      // 立ち止まる最小時間
    [SerializeField] private float maxIdleTime = 4.0f;      // 立ち止まる最大時間

    [Header("--- 見失った直後の直線突進設定 ---")]
    [SerializeField] private float chasePredictDistance = 4.0f; // 見失った瞬間、進行方向の「何メートル先」までまっすぐ突っ込ませるか
    [SerializeField] private float visionLostBufferTime = 1.5f; // 直線突進（LinearDash）を維持する最大秒数

    [Header("--- 見失った後の足跡追跡設定 ---")]
    [SerializeField] private float pathRecordInterval = 0.15f; // プレイヤーの足跡を記録する時間間隔
    [SerializeField] private int maxRecordedPositions = 40;    // 保持する足跡の最大記録数（少し余裕を持たせて40に拡張）
    [SerializeField] private float waypointTolerance = 1.3f;  // 足跡を踏破したと判定する敵との距離
    [SerializeField] private float chasingForgetTime = 5.0f;  // 足跡を辿りきってから完全に見切るまでの秒数
    [SerializeField] private float searchAroundRadius = 5f;   // 最終地点の周りを何メートル索敵するか

    [Header("--- 扉自動検知・開放設定 ---")]
    [SerializeField] private float doorCheckRadius = 1.2f;    // 敵が目の前の扉を感知する半径
    [SerializeField] private float doorCheckInterval = 0.1f;  // 扉をスキャンする時間間隔

    private NavMeshAgent agent;
    private Animator animator;
    private Transform playerTransform;

    private EnemyState currentState;
    private float idleTimer;
    private float currentIdleDuration;
    private float forgetTimer;
    private Vector3 lastSeenPosition;
    private float visionLostTimer;

    private Vector3 lastPlayerPosition;
    private Vector3 playerMoveDirection = Vector3.forward;

    // 足跡データ。引き返し防止のためリスト構造で管理します
    private List<Vector3> playerPositionHistory = new List<Vector3>();
    private Queue<Vector3> activeChasePath = new Queue<Vector3>();

    private float pathRecordTimer;
    private float doorCheckTimer;

    private readonly int speedHash = Animator.StringToHash("Speed");
    private readonly int isChasingHash = Animator.StringToHash("IsChasing");

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            lastPlayerPosition = playerTransform.position;
        }

        if (!agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 3.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }

        TransitionToState(EnemyState.PatrolIdle);
    }

    private void Update()
    {
        if (playerTransform == null || !agent.isOnNavMesh || !agent.isActiveAndEnabled) return;

        CalculatePlayerVectorAndPath();
        CheckAndOpenNearbyDoors();

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
            case EnemyState.LinearDash:
                HandleLinearDash(canSeePlayer);
                break;
            case EnemyState.PredictedChase:
                HandlePredictedChase(canSeePlayer);
                break;
            case EnemyState.LookAround:
                HandleLookAround(canSeePlayer);
                break;
        }

        UpdateAnimation();
    }

    private void CalculatePlayerVectorAndPath()
    {
        Vector3 currentMovement = playerTransform.position - lastPlayerPosition;
        if (currentMovement.magnitude > 0.01f)
        {
            playerMoveDirection = currentMovement.normalized;
        }
        lastPlayerPosition = playerTransform.position;

        pathRecordTimer += Time.deltaTime;
        if (pathRecordTimer >= pathRecordInterval)
        {
            pathRecordTimer = 0f;
            playerPositionHistory.Add(playerTransform.position);

            if (playerPositionHistory.Count > maxRecordedPositions)
            {
                playerPositionHistory.RemoveAt(0);
            }
        }
    }

    private void CheckAndOpenNearbyDoors()
    {
        doorCheckTimer += Time.deltaTime;
        if (doorCheckTimer >= doorCheckInterval)
        {
            doorCheckTimer = 0f;

            Collider[] hitColliders = Physics.OverlapSphere(transform.position + Vector3.up * 0.5f, doorCheckRadius);
            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.CompareTag("Door") || (hitCollider.transform.parent != null && hitCollider.transform.parent.CompareTag("Door")))
                {
                    NewDoorMech doorScript = hitCollider.gameObject.GetComponentInParent<NewDoorMech>();
                    if (doorScript != null && !doorScript.doorBool)
                    {
                        doorScript.doorBool = true;
                        Debug.Log("【センサー検知】敵の進路に扉を発見したため、強制的に開けました。");
                    }
                }
            }
        }
    }

    private bool CheckForPlayer()
    {
        Vector3 directionToPlayer = playerTransform.position - transform.position;
        float distanceToPlayer = directionToPlayer.magnitude;

        if (distanceToPlayer <= searchRadius)
        {
            float angle = Vector3.Angle(transform.forward, directionToPlayer);
            if (angle <= searchAngle * 0.5f)
            {
                RaycastHit hit;
                Vector3 startRayPos = transform.position + Vector3.up * 1.5f;
                Vector3 targetRayPos = playerTransform.position + Vector3.up * 1.0f;
                Vector3 rayDirection = targetRayPos - startRayPos;

                if (Physics.Raycast(startRayPos, rayDirection, out hit, searchRadius))
                {
                    if (hit.collider.CompareTag("Player"))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
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
        }
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
            SetRandomPatrolDestination();
            TransitionToState(EnemyState.PatrolWalk);
        }
    }

    private void HandleChase(bool canSeePlayer)
    {
        if (canSeePlayer)
        {
            agent.SetDestination(playerTransform.position);
            lastSeenPosition = playerTransform.position;
        }
        else
        {
            TransitionToState(EnemyState.LinearDash);
        }
    }

    private void HandleLinearDash(bool canSeePlayer)
    {
        if (canSeePlayer)
        {
            TransitionToState(EnemyState.Chase);
            return;
        }

        visionLostTimer += Time.deltaTime;

        // 直線突進が終わる、または目標地点にほぼ到達したら足跡追跡モードに切り替える
        if (visionLostTimer >= visionLostBufferTime || (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance))
        {
            // 【最重要】不自然な引き返しを防止する足跡の最適化関数を呼び出す
            BuildOptimizedChasePath();
            TransitionToState(EnemyState.PredictedChase);
        }
    }

    /// <summary>
    /// 敵の現在地より「後ろ」にある古い足跡をカットし、最適な追跡ルートを再構築するロジック
    /// </summary>
    private void BuildOptimizedChasePath()
    {
        activeChasePath.Clear();
        if (playerPositionHistory.Count == 0) return;

        int closestIndex = 0;
        float minDistance = float.MaxValue;

        // 1. 敵の現在地から「最も近い足跡」がどれかを検索する
        for (int i = 0; i < playerPositionHistory.Count; i++)
        {
            float dist = Vector3.Distance(transform.position, playerPositionHistory[i]);
            if (dist < minDistance)
            {
                minDistance = dist;
                closestIndex = i;
            }
        }

        // 2. 最も近い足跡（closestIndex）よりも古い足跡（インデックスが若いもの）は全て無視！
        // これにより、過去に遡って戻ってしまう現象を完全にシャットアウトします。
        for (int i = closestIndex; i < playerPositionHistory.Count; i++)
        {
            activeChasePath.Enqueue(playerPositionHistory[i]);
        }

        Debug.Log($"【経路最適化】不自然な引き返しを防止。古い足跡を {closestIndex} 個スキップしました。");
    }

    private void HandlePredictedChase(bool canSeePlayer)
    {
        if (canSeePlayer)
        {
            TransitionToState(EnemyState.Chase);
            return;
        }

        if (activeChasePath.Count > 0)
        {
            Vector3 nextWaypoint = activeChasePath.Peek();
            agent.SetDestination(nextWaypoint);

            float distanceToWaypoint = Vector3.Distance(transform.position, nextWaypoint);
            if (distanceToWaypoint <= waypointTolerance)
            {
                activeChasePath.Dequeue();
            }
        }
        else
        {
            TransitionToState(EnemyState.LookAround);
        }
    }

    private void HandleLookAround(bool canSeePlayer)
    {
        if (canSeePlayer)
        {
            TransitionToState(EnemyState.Chase);
            return;
        }

        forgetTimer += Time.deltaTime;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            Vector3 randomSearchPos = lastSeenPosition + Random.insideUnitSphere * searchAroundRadius;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomSearchPos, out hit, searchAroundRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }

        if (forgetTimer >= chasingForgetTime)
        {
            TransitionToState(EnemyState.PatrolIdle);
        }
    }

    private void SetRandomPatrolDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection += transform.position;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, patrolRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    private void TransitionToState(EnemyState newState)
    {
        if (!agent.isOnNavMesh) return;

        currentState = newState;

        switch (currentState)
        {
            case EnemyState.PatrolWalk:
                agent.speed = walkSpeed;
                agent.isStopped = false;
                break;

            case EnemyState.PatrolIdle:
                agent.isStopped = true;
                idleTimer = 0f;
                currentIdleDuration = Random.Range(minIdleTime, maxIdleTime);
                break;

            case EnemyState.Chase:
                agent.speed = runSpeed;
                agent.isStopped = false;
                break;

            case EnemyState.LinearDash:
                agent.speed = runSpeed;
                agent.isStopped = false;
                visionLostTimer = 0f;

                Vector3 targetDashPos = lastSeenPosition + (playerMoveDirection * chasePredictDistance);
                NavMeshHit hit;
                if (NavMesh.SamplePosition(targetDashPos, out hit, chasePredictDistance, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                }
                else
                {
                    agent.SetDestination(targetDashPos);
                }
                break;

            case EnemyState.PredictedChase:
                agent.speed = runSpeed;
                agent.isStopped = false;
                break;

            case EnemyState.LookAround:
                agent.speed = walkSpeed;
                agent.isStopped = false;
                forgetTimer = 0f;
                agent.SetDestination(lastSeenPosition);
                break;
        }
    }

    private void UpdateAnimation()
    {
        float currentPhysicalSpeed = agent.velocity.magnitude;
        animator.SetFloat(speedHash, currentPhysicalSpeed);

        bool isRunning = (currentState == EnemyState.Chase || currentState == EnemyState.LinearDash || currentState == EnemyState.PredictedChase);
        animator.SetBool(isChasingHash, isRunning);

        animator.speed = isRunning ? runAnimSpeedMultiplier : walkAnimSpeedMultiplier;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Door") || (collision.transform.parent != null && collision.transform.parent.CompareTag("Door")))
        {
            NewDoorMech doorScript = collision.gameObject.GetComponentInParent<NewDoorMech>();
            if (doorScript != null && !doorScript.doorBool)
            {
                doorScript.doorBool = true;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, doorCheckRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, searchRadius);
    }
}