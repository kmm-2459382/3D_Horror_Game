using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

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
        LookAround,      // 最終地点まで追いた後、周辺を警戒捜索
        JumpscareStun    // 【追加】ジャンプスケア演出中の完全停止状態
    }

    [Header("--- オーディオ設定 ---")]
    [SerializeField] private AudioManager audioManager;     // BGM制御用のマネージャー参照

    [Header("--- 索敵設定 ---")]
    [SerializeField] private float searchRadius = 25f;      // プレイヤーを検知する半径
    [SerializeField] private float searchAngle = 140f;      // プレイヤーを検知する視野角

    [Header("--- 移動速度設定 ---")]
    [SerializeField] private float walkSpeed = 1.5f;        // 巡回・警戒時の歩行速度
    [SerializeField] private float runSpeed = 4.5f;         // 追跡・突進時の走行速度

    [Header("--- 回転速度設定（度/秒） ---")]
    [SerializeField] private float normalTurnSpeed = 180f;   // 【通常時】通常方向転換速度
    [SerializeField] private float chaseTurnSpeed = 600f;  // 【追跡時】プレイヤーを追いかけている時の超高速回転速度

    [Header("--- アニメーション再生速度調整 ---")]
    [SerializeField] private float walkAnimSpeedMultiplier = 1.0f; // 歩行時のアニメーション速度倍率
    [SerializeField] private float runAnimSpeedMultiplier = 1.0f;  // 走行時のアニメーション速度倍率

    [Header("--- 巡回（パトロール）設定 ---")]
    [SerializeField] private float patrolRadius = 6f;       // ランダムに動く範囲の半径
    [SerializeField] private float minIdleTime = 1.5f;      // 立ち止まる最小時間
    [SerializeField] private float maxIdleTime = 4.0f;      // 立ち止まる最大時間

    [Header("--- 見失った直後の直線突進設定 ---")]
    [SerializeField] private float chasePredictDistance = 6.0f; // 見失った瞬間、進行方向の何メートル先までまっすぐ突っ込ませるか
    [SerializeField] private float visionLostBufferTime = 1.5f; // 直線突進（LinearDash）を維持する最大秒数

    [Header("--- 見失った後の足跡追跡設定 ---")]
    [SerializeField] private float pathRecordInterval = 0.15f; // プレイヤーの足跡を記録する時間間隔
    [SerializeField] private int maxRecordedPositions = 40;    // 保持する足跡の最大記録数
    [SerializeField] private float waypointTolerance = 1.3f;  // 足跡を踏破したと判定する敵との距離
    [SerializeField] private float chasingForgetTime = 5.0f;  // 足跡を辿りきってから完全に見切るまでの秒数
    [SerializeField] private float searchAroundRadius = 5f;   // 最終地点の周りを何メートル索敵するか

    [Header("--- 扉自動検知・開放設定 ---")]
    [SerializeField] private float doorCheckRadius = 1.8f;    // 敵が前方の扉を検知する前方射程
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

    // 足跡データ
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
        agent.updateRotation = false;

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

        if (audioManager != null)
        {
            audioManager.ChangeBGMState(AudioManager.BGMMode.Patrol);
        }

        TransitionToState(EnemyState.PatrolIdle);
    }

    private void Update()
    {
        if (playerTransform == null || !agent.isOnNavMesh || !agent.isActiveAndEnabled) return;

        // 💡【完全フリーズ処理】
        // ジャンプスケア停止状態の時は、他の全AIロジック、移動アニメ、カスタム回転を
        // 完全に飛び越えて、プレイヤーを見つめる処理「だけ」を徹底的に実行する
        if (currentState == EnemyState.JumpscareStun)
        {
            // 毎フレーム、速度を物理的にもナビゲーション的にも「ゼロ」に強制上書き
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }

            // アニメーションを毎フレーム「Idle（直立不動）」に強制固定
            if (animator != null)
            {
                animator.SetFloat(speedHash, 0f);
                animator.SetBool(isChasingHash, false);
                animator.speed = walkAnimSpeedMultiplier;
            }

            KeepLookingAtPlayer();
            return; // 💡完全にUpdateをここで終わらせる（下の移動ロジックに絶対に触れさせない）
        }

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

        ApplyCustomRotation();
        UpdateAnimation();
    }

    /// <summary>
    /// 演出中に毎フレーム敵がプレイヤーをじっと見つめ直させる処理
    /// </summary>
    private void KeepLookingAtPlayer()
    {
        if (playerTransform != null)
        {
            Vector3 targetDirection = playerTransform.position - transform.position;
            targetDirection.y = 0f;

            if (targetDirection.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(targetDirection);
            }
        }
    }

    private void ApplyCustomRotation()
    {
        Vector3 targetDirection = Vector3.zero;
        float currentTurnSpeed = normalTurnSpeed;

        if (currentState == EnemyState.Chase && playerTransform != null)
        {
            targetDirection = playerTransform.position - transform.position;
            currentTurnSpeed = chaseTurnSpeed;
        }
        else
        {
            targetDirection = agent.velocity;
            currentTurnSpeed = normalTurnSpeed;
        }

        targetDirection.y = 0f;

        if (targetDirection.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, currentTurnSpeed * Time.deltaTime);
        }
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

            Vector3 moveDirection = agent.desiredVelocity.normalized;
            if (moveDirection.magnitude < 0.1f)
            {
                moveDirection = transform.forward;
            }

            Vector3 rayStartPos = transform.position + Vector3.up * 0.8f;
            RaycastHit hit;
            if (Physics.Raycast(rayStartPos, moveDirection, out hit, doorCheckRadius))
            {
                if (hit.collider.CompareTag("Door") || (hit.transform.parent != null && hit.transform.parent.CompareTag("Door")))
                {
                    NewDoorMech doorScript = hit.collider.gameObject.GetComponentInParent<NewDoorMech>();
                    if (doorScript != null && !doorScript.doorBool)
                    {
                        DoorController doorController = FindFirstObjectByType<DoorController>();
                        if (doorController != null)
                        {
                            if (doorController.IsDoorLocked(doorScript))
                            {
                                agent.isStopped = true;
                                agent.velocity = Vector3.zero;

                                if (currentState == EnemyState.Chase || currentState == EnemyState.LinearDash || currentState == EnemyState.PredictedChase)
                                {
                                    TransitionToState(EnemyState.LookAround);
                                }
                                else if (currentState == EnemyState.PatrolWalk)
                                {
                                    TransitionToState(EnemyState.PatrolIdle);
                                }
                                return;
                            }
                            doorController.Interact(doorScript, null);
                        }
                        else
                        {
                            doorScript.doorBool = true;
                        }
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

        if (visionLostTimer >= visionLostBufferTime || (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance))
        {
            BuildOptimizedChasePath();
            TransitionToState(EnemyState.PredictedChase);
        }
    }

    private void BuildOptimizedChasePath()
    {
        activeChasePath.Clear();
        if (playerPositionHistory.Count == 0) return;

        int closestIndex = 0;
        float minDistance = float.MaxValue;

        for (int i = 0; i < playerPositionHistory.Count; i++)
        {
            float dist = Vector3.Distance(transform.position, playerPositionHistory[i]);
            if (dist < minDistance)
            {
                minDistance = dist;
                closestIndex = i;
            }
        }

        for (int i = closestIndex; i < playerPositionHistory.Count; i++)
        {
            activeChasePath.Enqueue(playerPositionHistory[i]);
        }
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

            case EnemyState.JumpscareStun:
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                break;
        }

        if (audioManager != null)
        {
            if (currentState == EnemyState.Chase)
            {
                audioManager.ChangeBGMState(AudioManager.BGMMode.AlertChase);
            }
            else if (currentState == EnemyState.LinearDash || currentState == EnemyState.PredictedChase)
            {
                audioManager.ChangeBGMState(AudioManager.BGMMode.LostTracking);
            }
            else
            {
                audioManager.ChangeBGMState(AudioManager.BGMMode.Patrol);
            }
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
        if (collision.gameObject.CompareTag("Player") && currentState != EnemyState.JumpscareStun)
        {
            // 💡 プレイヤーの向いている方向（正面ベクトル）を取得
            Vector3 playerForward = collision.transform.forward;
            playerForward.y = 0f;
            playerForward.Normalize();

            // 💡 プレイヤーの正面1.3mの位置を計算（ここが敵の停止位置）
            Vector3 stopPosition = collision.transform.position + playerForward * 1.3f;

            StartCoroutine(StopEnemyRoutine(2.5f, stopPosition));
        }

        if (collision.gameObject.CompareTag("Door") || (collision.transform.parent != null && collision.transform.parent.CompareTag("Door")))
        {
            NewDoorMech doorScript = collision.gameObject.GetComponentInParent<NewDoorMech>();
            if (doorScript != null && !doorScript.doorBool)
            {
                DoorController doorController = FindFirstObjectByType<DoorController>();
                if (doorController != null)
                {
                    if (doorController.IsDoorLocked(doorScript))
                    {
                        agent.isStopped = true;
                        agent.velocity = Vector3.zero;
                        return;
                    }
                    doorController.Interact(doorScript, null);
                }
                else
                {
                    doorScript.doorBool = true;
                }
            }
        }
    }

    /// <summary>
    /// ジャンプスケア演出中に敵の位置を完全にフリーズし、お互いを見つめ合わせる
    /// </summary>
    private IEnumerator StopEnemyRoutine(float stopDuration, Vector3 exactStopPosition)
    {
        // 1. 即座にJumpscareStun状態に切り替える（これによりUpdate側の強力なロックが始まります）
        TransitionToState(EnemyState.JumpscareStun);
        Debug.Log($"【敵AI】プレイヤーと接触。正面に固定して {stopDuration}秒間足を止めます。");

        // 2. ナビゲーションの過去の目的地（足跡など）を完全にリセットする
        if (agent != null && agent.isOnNavMesh)
        {
            agent.ResetPath(); // 👈【重要】これまでの足跡や追跡ルートを完全に消去！
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.Warp(exactStopPosition); // プレイヤーの正面の指定位置にワープ
        }

        // お互いのコライダーの押し合いを無視
        Collider[] enemyColliders = GetComponents<Collider>();
        Collider playerCollider = playerTransform != null ? playerTransform.GetComponent<Collider>() : null;
        if (playerCollider != null)
        {
            foreach (var col in enemyColliders)
            {
                Physics.IgnoreCollision(col, playerCollider, true);
            }
        }

        if (audioManager != null)
        {
            audioManager.ChangeBGMState(AudioManager.BGMMode.Patrol);
        }

        // プレイヤーの視線を敵の正面に向ける
        if (playerTransform != null)
        {
            Vector3 dirToPlayer = (transform.position - playerTransform.position).normalized;
            dirToPlayer.y = 0f;
            if (dirToPlayer.sqrMagnitude > 0.01f)
            {
                playerTransform.rotation = Quaternion.LookRotation(dirToPlayer);
            }
        }

        // 3. 指定された秒数（2.5秒間）、位置を毎フレームガチガチに固定して待機
        float elapsed = 0f;
        while (elapsed < stopDuration)
        {
            transform.position = exactStopPosition; // 座標を完全固定

            // プレイヤーの入力も演出中のみ毎フレーム消去して動けないようにする
            if (playerTransform != null)
            {
                var inputs = playerTransform.GetComponent<StarterAssets.StarterAssetsInputs>();
                if (inputs != null)
                {
                    inputs.move = Vector2.zero;
                    inputs.look = Vector2.zero;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 4. 演出終了にともない、衝突判定を元に戻す
        if (playerCollider != null)
        {
            foreach (var col in enemyColliders)
            {
                Physics.IgnoreCollision(col, playerCollider, false);
            }
        }

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }

        Debug.Log("【敵AI】停止時間終了。追跡を再開します。");
        TransitionToState(EnemyState.Chase);
    }
}