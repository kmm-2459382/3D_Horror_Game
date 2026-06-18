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
        PatrolWalk,
        PatrolIdle,
        Chase,
        LinearDash,
        PredictedChase,
        LookAround,
        JumpscareStun
    }

    [Header("--- 【デバッグ設定】 ---")]
    [Tooltip("最初からtrueにしておいて大丈夫です！チェックが入っている間は見失いません。途中で外すとリアルタイムに見失うようになります。")]
    [SerializeField] private bool debugAlwaysChasePlayer = true;

    // ─── 👁️【新設：インスペクター可視化用モニター】 ───
    [Header("--- 🔍 【AI内部状態モニター（変更不可）】 ---")]
    [SerializeField] private string 現在のステート;
    [SerializeField] private bool エージェント停止フラグ_isStopped;
    [SerializeField] private float 目的地への残り距離_remainingDistance;
    [SerializeField] private string パス計算の状態_pathStatus;
    [SerializeField] private bool 目的地が存在するか_hasPath;
    [SerializeField] private Vector3 現在の目標座標_destination;

    [Header("--- 【統合】敵の全挙動カスタマイズ総合倍率 ---")]
    [Tooltip("数値を直接入力してください (1.0 = 従来の等倍)。この値一つですべての動き（速度・制動・旋回）の倍率が変わります。")]
    [SerializeField] private float enemyMotionMultiplier = 1.0f;

    [Header("--- オーディオ設定 ---")]
    [SerializeField] private AudioManager audioManager;

    [Header("--- 索敵設定 ---")]
    [SerializeField] private float searchRadius = 25f;
    [SerializeField] private float searchAngle = 140f;

    [Header("--- 移動速度設定（ベース値） ---")]
    [SerializeField] private float walkSpeed = 1.5f;
    [SerializeField] private float runSpeed = 4.5f;

    [Header("--- 回転速度設定（度/秒） ---")]
    [SerializeField] private float normalTurnSpeed = 180f;
    [SerializeField] private float chaseTurnSpeed = 600f;

    [Header("--- アニメーション再生速度調整 ---")]
    [SerializeField] private float walkAnimSpeedMultiplier = 1.0f;
    [SerializeField] private float runAnimSpeedMultiplier = 1.0f;

    [Header("--- 巡回（パトロール）設定 ---")]
    [SerializeField] private float patrolRadius = 6f;
    [SerializeField] private float minIdleTime = 1.5f;
    [SerializeField] private float maxIdleTime = 4.0f;

    [Header("--- 見失った直後の直線突進設定 ---")]
    [SerializeField] private float chasePredictDistance = 6.0f;
    [SerializeField] private float visionLostBufferTime = 1.5f;

    [Header("--- 見失った後の足跡追跡設定 ---")]
    [SerializeField] private float pathRecordInterval = 0.15f;
    [SerializeField] private int maxRecordedPositions = 40;
    [SerializeField] private float waypointTolerance = 1.3f;
    [SerializeField] private float chasingForgetTime = 5.0f;
    [SerializeField] private float searchAroundRadius = 5f;

    [Header("--- 扉自動検知・開放設定 ---")]
    [Tooltip("ドアを感知するベースの距離（等倍時）。速度が上がると自動でこの距離も拡張されます")]
    [SerializeField] private float doorCheckRadius = 1.8f;
    [SerializeField] private float doorCheckInterval = 0.05f;

    [Tooltip("シーン内の DoorController オブジェクトをここにセットしてください")]
    [SerializeField] private DoorController doorController;

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

    private List<Vector3> playerPositionHistory = new List<Vector3>();
    private Queue<Vector3> activeChasePath = new Queue<Vector3>();

    private float pathRecordTimer;
    private float doorCheckTimer;

    // 💡【新設】現在のBGM状態を追跡するためのキャッシュフラグ（重複呼び出し防止）
    private bool isBGMChasing = false;

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

        // 初期はパトロールBGM
        if (audioManager != null)
        {
            audioManager.ChangeBGMState(AudioManager.BGMMode.Patrol);
            isBGMChasing = false;
        }

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        TransitionToState(EnemyState.PatrolIdle);
    }

    private void Update()
    {
        if (playerTransform == null || !agent.isOnNavMesh || !agent.isActiveAndEnabled) return;

        // ─── インスペクター可視化モニターのリアルタイム同期 ───
        MonitorAIStatus();

        // 1. 完全スタン状態の処理
        if (currentState == EnemyState.JumpscareStun)
        {
            HandleStunBehavior();
            UpdateBGMState(); // 💡 スタンス中もBGMの判定を行う（Patrolへ引き戻すため）
            return;
        }

        // 2. 共通センサー更新
        CalculatePlayerVectorAndPath();
        CheckAndOpenNearbyDoors();

        // 3. デバッグフラグの適用
        bool isAlwaysChaseActive = debugAlwaysChasePlayer;
        bool canSeePlayer = CheckForPlayer();

        if (isAlwaysChaseActive)
        {
            canSeePlayer = true;
            if (currentState != EnemyState.Chase)
            {
                TransitionToState(EnemyState.Chase);
            }
        }

        // 4. 各挙動関数へ分岐
        switch (currentState)
        {
            case EnemyState.PatrolWalk: HandlePatrolWalk(canSeePlayer); break;
            case EnemyState.PatrolIdle: HandlePatrolIdle(canSeePlayer); break;
            case EnemyState.Chase: HandleChase(canSeePlayer, isAlwaysChaseActive); break;
            case EnemyState.LinearDash: HandleLinearDash(canSeePlayer); break;
            case EnemyState.PredictedChase: HandlePredictedChase(canSeePlayer); break;
            case EnemyState.LookAround: HandleLookAround(canSeePlayer); break;
        }

        // パラメータ・回転・アニメーション・BGM同期
        UpdateAgentDynamicParameters(isAlwaysChaseActive);
        float currentTurnSpeed = (currentState == EnemyState.Chase) ? chaseTurnSpeed : normalTurnSpeed;
        ApplyCustomRotation(currentTurnSpeed, isAlwaysChaseActive);
        UpdateAnimation();

        // 💡【重要追加】毎フレームのBGM監視と切り替えを実行
        UpdateBGMState();
    }

    /// <summary>
    /// インスペクター上で現在の設定値や状態を見える化する関数
    /// </summary>
    private void MonitorAIStatus()
    {
        現在のステート = currentState.ToString();
        エージェント停止フラグ_isStopped = agent.isStopped;
        目的地への残り距離_remainingDistance = agent.remainingDistance;
        パス計算の状態_pathStatus = agent.pathStatus.ToString();
        目的地が存在するか_hasPath = agent.hasPath;
        現在の目標座標_destination = agent.destination;
    }

    private void HandleStunBehavior()
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        if (animator != null)
        {
            animator.SetFloat(speedHash, 0f);
            animator.SetBool(isChasingHash, false);
            animator.speed = walkAnimSpeedMultiplier;
        }
        KeepLookingAtPlayer();
    }

    /// <summary>
    /// 💡【BGM動的切り替え制御】
    /// 追跡中（Chase/LinearDash/PredictedChase）かつ 敵が自由に動ける時に AlertChase へ切り替えます。
    /// </summary>
    private void UpdateBGMState()
    {
        if (audioManager == null) return;

        bool isAgentMovable = (!agent.isStopped && currentState != EnemyState.JumpscareStun);

        // 判定ロジック
        if (isAgentMovable)
        {
            // 完全に視認している状態
            if (currentState == EnemyState.Chase)
            {
                if (audioManager.CurrentMode != AudioManager.BGMMode.AlertChase)
                    audioManager.ChangeBGMState(AudioManager.BGMMode.AlertChase);
            }
            // 見失った直後の推測追跡・ダッシュ状態
            else if (currentState == EnemyState.PredictedChase || currentState == EnemyState.LinearDash)
            {
                if (audioManager.CurrentMode != AudioManager.BGMMode.LostTracking)
                    audioManager.ChangeBGMState(AudioManager.BGMMode.LostTracking);
            }
            // それ以外（巡回など）
            else
            {
                if (audioManager.CurrentMode != AudioManager.BGMMode.Patrol)
                    audioManager.ChangeBGMState(AudioManager.BGMMode.Patrol);
            }
        }
        else
        {
            // 停止・スタン中は巡回BGMへ
            if (audioManager.CurrentMode != AudioManager.BGMMode.Patrol)
                audioManager.ChangeBGMState(AudioManager.BGMMode.Patrol);
        }
    }

    /// <summary>
    /// 【滑りやすさ反比例化】
    /// 総合倍率が上がれば上がるほど、制動力(加速度)をそれ以上に引き上げ、
    /// ドリフトせずにピタッと止まって鋭角に曲がれるようにする
    /// </summary>
    private void UpdateAgentDynamicParameters(bool isAlwaysChaseActive)
    {
        float baseSpeed = walkSpeed;
        float baseAcceleration = isAlwaysChaseActive ? 120f : 80f;
        float baseAngular = isAlwaysChaseActive ? 360f : 300f;

        if (currentState == EnemyState.Chase || currentState == EnemyState.LinearDash || currentState == EnemyState.PredictedChase)
        {
            baseSpeed = runSpeed;
            baseAcceleration = isAlwaysChaseActive ? 120f : 80f;
            baseAngular = isAlwaysChaseActive ? 360f : 300f;
        }

        agent.speed = baseSpeed * enemyMotionMultiplier;

        float multiplierSquare = enemyMotionMultiplier * enemyMotionMultiplier;
        float brakeCompensation = Mathf.Max(enemyMotionMultiplier, multiplierSquare);

        agent.acceleration = baseAcceleration * brakeCompensation;
        agent.angularSpeed = baseAngular * brakeCompensation;
    }

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

    private void ApplyCustomRotation(float turnSpeed, bool isAlwaysChaseActive)
    {
        Vector3 targetDirection = Vector3.zero;

        if ((currentState == EnemyState.Chase || isAlwaysChaseActive) && playerTransform != null)
        {
            targetDirection = playerTransform.position - transform.position;
        }
        else
        {
            targetDirection = agent.velocity;
        }

        targetDirection.y = 0f;
        if (targetDirection.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            float finalTurnSpeed = isAlwaysChaseActive ? 1200f : turnSpeed;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, finalTurnSpeed * enemyMotionMultiplier * Time.deltaTime);
        }
    }

    private void CalculatePlayerVectorAndPath()
    {
        Vector3 currentMovement = playerTransform.position - lastPlayerPosition;
        if (currentMovement.magnitude > 0.01f) { playerMoveDirection = currentMovement.normalized; }
        lastPlayerPosition = playerTransform.position;

        pathRecordTimer += Time.deltaTime;
        if (pathRecordTimer >= pathRecordInterval)
        {
            pathRecordTimer = 0f;
            playerPositionHistory.Add(playerTransform.position);
            if (playerPositionHistory.Count > maxRecordedPositions) { playerPositionHistory.RemoveAt(0); }
        }
    }

    private void CheckAndOpenNearbyDoors()
    {
        doorCheckTimer += Time.deltaTime;
        if (doorCheckTimer >= doorCheckInterval)
        {
            doorCheckTimer = 0f;
            Vector3 moveDirection = agent.desiredVelocity.normalized;
            if (moveDirection.magnitude < 0.1f) moveDirection = transform.forward;

            Vector3 rayStartPos = transform.position + Vector3.up * 0.8f;
            RaycastHit hit;

            float scaledCheckRadius = Mathf.Max(doorCheckRadius, doorCheckRadius * enemyMotionMultiplier);

            if (Physics.Raycast(rayStartPos, moveDirection, out hit, scaledCheckRadius))
            {
                if (hit.collider.CompareTag("Door") || (hit.transform.parent != null && hit.transform.parent.CompareTag("Door")))
                {
                    NewDoorMech doorScript = hit.collider.gameObject.GetComponentInParent<NewDoorMech>();

                    if (doorScript != null)
                    {
                        if (doorController != null && doorController.IsDoorLocked(doorScript))
                        {
                            return;
                        }

                        if (!doorScript.doorBool)
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
                    if (hit.collider.CompareTag("Player")) return true;
                }
            }
        }
        return false;
    }

    private void HandlePatrolWalk(bool canSeePlayer)
    {
        if (canSeePlayer) { TransitionToState(EnemyState.Chase); return; }
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance) TransitionToState(EnemyState.PatrolIdle);
    }

    private void HandlePatrolIdle(bool canSeePlayer)
    {
        if (canSeePlayer) { TransitionToState(EnemyState.Chase); return; }
        idleTimer += Time.deltaTime;
        if (idleTimer >= currentIdleDuration)
        {
            SetRandomPatrolDestination();
            TransitionToState(EnemyState.PatrolWalk);
        }
    }

    private void HandleChase(bool canSeePlayer, bool isAlwaysChaseActive)
    {
        if (isAlwaysChaseActive)
        {
            agent.SetDestination(playerTransform.position);
            lastSeenPosition = playerTransform.position;
            playerPositionHistory.Clear();
        }
        else
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
    }

    private void HandleLinearDash(bool canSeePlayer)
    {
        if (canSeePlayer) { TransitionToState(EnemyState.Chase); return; }
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
            if (dist < minDistance) { minDistance = dist; closestIndex = i; }
        }

        int startIndex = Mathf.Max(closestIndex, playerPositionHistory.Count - 8);

        for (int i = startIndex; i < playerPositionHistory.Count; i++)
        {
            activeChasePath.Enqueue(playerPositionHistory[i]);
        }
    }

    private void HandlePredictedChase(bool canSeePlayer)
    {
        if (canSeePlayer) { TransitionToState(EnemyState.Chase); return; }

        if (activeChasePath.Count > 0)
        {
            Vector3 nextWaypoint = activeChasePath.Peek();

            if (Vector3.Distance(agent.destination, nextWaypoint) > 0.3f)
            {
                agent.SetDestination(nextWaypoint);
            }

            if ((!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance) ||
                Vector3.Distance(transform.position, nextWaypoint) <= waypointTolerance)
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
        if (canSeePlayer) { TransitionToState(EnemyState.Chase); return; }
        forgetTimer += Time.deltaTime;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            Vector3 randomSearchPos = lastSeenPosition + Random.insideUnitSphere * searchAroundRadius;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomSearchPos, out hit, searchAroundRadius, NavMesh.AllAreas))
            {
                if (Vector3.Distance(agent.destination, hit.position) > 0.5f)
                {
                    agent.SetDestination(hit.position);
                }
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
                agent.isStopped = false;
                break;

            case EnemyState.PatrolIdle:
                agent.ResetPath();
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                idleTimer = 0f;
                currentIdleDuration = Random.Range(minIdleTime, maxIdleTime);
                break;

            case EnemyState.Chase:
                agent.isStopped = false;
                break;

            case EnemyState.LinearDash:
                agent.isStopped = false;
                visionLostTimer = 0f;
                Vector3 targetDashPos = lastSeenPosition + (playerMoveDirection * chasePredictDistance);
                NavMeshHit hit;
                if (NavMesh.SamplePosition(targetDashPos, out hit, chasePredictDistance, NavMesh.AllAreas)) agent.SetDestination(hit.position);
                else agent.SetDestination(targetDashPos);
                break;

            case EnemyState.PredictedChase:
                agent.isStopped = false;
                break;

            case EnemyState.LookAround:
                agent.isStopped = false;
                forgetTimer = 0f;
                agent.SetDestination(lastSeenPosition);
                break;

            case EnemyState.JumpscareStun:
                agent.ResetPath();
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
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
        if (collision.gameObject.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerCaught();
            }
        }
    }

    public void SetAlwaysChase(bool enable)
    {
        debugAlwaysChasePlayer = enable;
        Debug.Log($"【敵AI】debugAlwaysChasePlayer が {enable} に書き換えられました。");

        if (!enable && agent != null && agent.isOnNavMesh)
        {
            agent.ResetPath();
            playerPositionHistory.Clear();
            activeChasePath.Clear();
        }
    }

    public void ForceStunState(bool shouldStun)
    {
        if (shouldStun)
        {
            TransitionToState(EnemyState.JumpscareStun);
            Debug.Log("【敵AI】イベント用の完全固定状態（JumpscareStun）に入りました。");
        }
        else
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;
            }
            TransitionToState(EnemyState.Chase);
            Debug.Log("【敵AI】固定状態が解除され、猛追跡を開始しました。");
        }
    }
}