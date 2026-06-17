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
        Chase,           // プレイヤー追跡
        LinearDash,
        PredictedChase,
        LookAround,
        JumpscareStun
    }

    [Header("--- オーディオ設定 ---")]
    [SerializeField] private AudioManager audioManager;

    [Header("--- 索敵設定 ---")]
    [SerializeField] private float searchRadius = 25f;
    [SerializeField] private float searchAngle = 140f;

    [Header("--- 移動速度設定 ---")]
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
    [SerializeField] private float doorCheckRadius = 1.8f;
    [SerializeField] private float doorCheckInterval = 0.1f;

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

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        TransitionToState(EnemyState.PatrolIdle);
    }

    private void Update()
    {
        // ─── 屋上演出フェーズにおける挙動 ───
        if (GameManager.Instance != null)
        {
            GameManager.GamePhase phase = GameManager.Instance.CurrentPhase;

            if (phase <= GameManager.GamePhase.Phase4_RooftopEvent)
            {
                // BGMがAlertChase（見つめ合い終了）になるまでは完全停止
                if (audioManager == null || audioManager.CurrentMode != AudioManager.BGMMode.AlertChase)
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
                    }
                    KeepLookingAtPlayer(); // じっとプレイヤーを凝視
                    return;
                }

                // 見つめ合い終了後：確定追跡（超高速で突進）
                if (agent != null && agent.isActiveAndEnabled && playerTransform != null)
                {
                    agent.isStopped = false;
                    agent.speed = 8.5f; // 通常時より大幅に速い速度
                    agent.SetDestination(playerTransform.position);

                    // 💡 移動速度が早いので、振り向き速度を「1200」の超高速にして適用
                    ApplyCustomRotation(1200f);
                    UpdateAnimation();
                }
                return;
            }
        }

        // ─── 通常AIロジック（後半戦以降） ───
        if (playerTransform == null || !agent.isOnNavMesh || !agent.isActiveAndEnabled) return;

        if (currentState == EnemyState.JumpscareStun)
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
            return;
        }

        CalculatePlayerVectorAndPath();
        CheckAndOpenNearbyDoors();

        bool canSeePlayer = CheckForPlayer();

        switch (currentState)
        {
            case EnemyState.PatrolWalk: HandlePatrolWalk(canSeePlayer); break;
            case EnemyState.PatrolIdle: HandlePatrolIdle(canSeePlayer); break;
            case EnemyState.Chase: HandleChase(canSeePlayer); break;
            case EnemyState.LinearDash: HandleLinearDash(canSeePlayer); break;
            case EnemyState.PredictedChase: HandlePredictedChase(canSeePlayer); break;
            case EnemyState.LookAround: HandleLookAround(canSeePlayer); break;
        }

        // 通常時の振り向き（チェイス時はchaseTurnSpeed、それ以外はnormalTurnSpeed）
        float currentTurnSpeed = (currentState == EnemyState.Chase) ? chaseTurnSpeed : normalTurnSpeed;
        ApplyCustomRotation(currentTurnSpeed);
        UpdateAnimation();
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

    // 💡 任意の回転速度を受け取れるように拡張しました
    private void ApplyCustomRotation(float turnSpeed)
    {
        Vector3 targetDirection = Vector3.zero;

        // チェイス時、または屋上イベント時
        if ((currentState == EnemyState.Chase || (GameManager.Instance != null && GameManager.Instance.CurrentPhase == GameManager.GamePhase.Phase4_RooftopEvent)) && playerTransform != null)
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
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
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
            if (Physics.Raycast(rayStartPos, moveDirection, out hit, doorCheckRadius))
            {
                if (hit.collider.CompareTag("Door") || (hit.transform.parent != null && hit.transform.parent.CompareTag("Door")))
                {
                    NewDoorMech doorScript = hit.collider.gameObject.GetComponentInParent<NewDoorMech>();
                    if (doorScript != null && !doorScript.doorBool)
                    {
                        doorScript.doorBool = true;
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

    private void HandleChase(bool canSeePlayer)
    {
        if (canSeePlayer) { agent.SetDestination(playerTransform.position); lastSeenPosition = playerTransform.position; }
        else TransitionToState(EnemyState.LinearDash);
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
        for (int i = closestIndex; i < playerPositionHistory.Count; i++) { activeChasePath.Enqueue(playerPositionHistory[i]); }
    }

    private void HandleRecordedChase(bool canSeePlayer) { HandlePredictedChase(canSeePlayer); }

    private void HandlePredictedChase(bool canSeePlayer)
    {
        if (canSeePlayer) { TransitionToState(EnemyState.Chase); return; }
        if (activeChasePath.Count > 0)
        {
            Vector3 nextWaypoint = activeChasePath.Peek();
            agent.SetDestination(nextWaypoint);
            if (Vector3.Distance(transform.position, nextWaypoint) <= waypointTolerance) activeChasePath.Dequeue();
        }
        else TransitionToState(EnemyState.LookAround);
    }

    private void HandleLookAround(bool canSeePlayer)
    {
        if (canSeePlayer) { TransitionToState(EnemyState.Chase); return; }
        forgetTimer += Time.deltaTime;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            Vector3 randomSearchPos = lastSeenPosition + Random.insideUnitSphere * searchAroundRadius;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomSearchPos, out hit, searchAroundRadius, NavMesh.AllAreas)) agent.SetDestination(hit.position);
        }
        if (forgetTimer >= chasingForgetTime) TransitionToState(EnemyState.PatrolIdle);
    }

    private void SetRandomPatrolDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection += transform.position;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, patrolRadius, NavMesh.AllAreas)) agent.SetDestination(hit.position);
    }

    private void TransitionToState(EnemyState newState)
    {
        if (!agent.isOnNavMesh) return;
        currentState = newState;

        switch (currentState)
        {
            case EnemyState.PatrolWalk: agent.speed = walkSpeed; agent.isStopped = false; break;
            case EnemyState.PatrolIdle: agent.isStopped = true; idleTimer = 0f; currentIdleDuration = Random.Range(minIdleTime, maxIdleTime); break;
            case EnemyState.Chase: agent.speed = runSpeed; agent.isStopped = false; break;
            case EnemyState.LinearDash:
                agent.speed = runSpeed; agent.isStopped = false; visionLostTimer = 0f;
                Vector3 targetDashPos = lastSeenPosition + (playerMoveDirection * chasePredictDistance);
                NavMeshHit hit;
                if (NavMesh.SamplePosition(targetDashPos, out hit, chasePredictDistance, NavMesh.AllAreas)) agent.SetDestination(hit.position);
                else agent.SetDestination(targetDashPos);
                break;
            case EnemyState.PredictedChase: agent.speed = runSpeed; agent.isStopped = false; break;
            case EnemyState.LookAround: agent.speed = walkSpeed; agent.isStopped = false; forgetTimer = 0f; agent.SetDestination(lastSeenPosition); break;
            case EnemyState.JumpscareStun: agent.isStopped = true; agent.velocity = Vector3.zero; break;
        }

        if (audioManager != null && GameManager.Instance != null && GameManager.Instance.CurrentPhase > GameManager.GamePhase.Phase4_RooftopEvent)
        {
            if (currentState == EnemyState.Chase) audioManager.ChangeBGMState(AudioManager.BGMMode.AlertChase);
            else if (currentState == EnemyState.LinearDash || currentState == EnemyState.PredictedChase) audioManager.ChangeBGMState(AudioManager.BGMMode.LostTracking);
            else audioManager.ChangeBGMState(AudioManager.BGMMode.Patrol);
        }
    }

    private void UpdateAnimation()
    {
        float currentPhysicalSpeed = agent.velocity.magnitude;
        animator.SetFloat(speedHash, currentPhysicalSpeed);
        bool isRunning = (currentState == EnemyState.Chase || currentState == EnemyState.LinearDash || currentState == EnemyState.PredictedChase || (GameManager.Instance != null && GameManager.Instance.CurrentPhase == GameManager.GamePhase.Phase4_RooftopEvent));
        animator.SetBool(isChasingHash, isRunning);
        animator.speed = isRunning ? runAnimSpeedMultiplier : walkAnimSpeedMultiplier;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerCaughtAtRooftop();
            }
        }
    }
}