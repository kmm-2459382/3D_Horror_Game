using System.Collections;
using UnityEngine;
using StarterAssets; // StarterAssetsの機能を使用

[RequireComponent(typeof(Collider))]
public class JumpscareTrigger : MonoBehaviour
{
    [Header("--- Cinemachine 設定 ---")]
    [Tooltip("Cinemachineコンポーネントがついているカメラオブジェクト（Main Cameraなど）をそのまま割り当てます")]
    [SerializeField] private GameObject playerCameraObject;

    [Header("--- ターゲット設定 ---")]
    [Tooltip("プレイヤーが強制的に振り向く対象（敵の顔に設置したEmptyオブジェクトを割り当ててください）")]
    [SerializeField] private Transform lookAtTarget;

    [Header("--- アニメーション設定 ---")]
    [Tooltip("敵のAnimatorコントローラー内にある、ジャンプスケア用アニメーションのトリガー名")]
    [SerializeField] private string jumpscareAnimationTrigger = "Jumpscare";

    [Header("--- 演出・エフェクト設定 ---")]
    [Tooltip("💡 ジャンプスケア音を再生させる専用のAudioSource（空欄の場合は自動的にカメラ等から再生されます）")]
    [SerializeField] private AudioSource scareAudioSource;

    [Tooltip("ドカン！と鳴らす恐怖効果音")]
    [SerializeField] private AudioClip scareSE;

    [Tooltip("💡 恐怖効果音の音量。1.0を超える数値（2倍、3倍など）を設定して大音量にできます")]
    [SerializeField] private float seVolume = 1.0f;

    [Tooltip("画面に表示させる恐怖画像（UIオブジェクト）。使わない場合は空欄でOK")]
    [SerializeField] private GameObject scareImageUI;

    [Header("--- 演出・カメラ時間の設定 ---")]
    [Tooltip("敵の顔を完全に向き終わるまでにかける時間（秒）。0.15〜0.2秒くらいがおすすめ")]
    [SerializeField] private float turnDuration = 0.2f;

    [Tooltip("敵の顔を強制的に見せ続ける時間（秒）※振り向き時間も含みます")]
    [SerializeField] private float scareDuration = 2.0f;

    [Tooltip("演出終了後、何秒経ったらもう一度次のジャンプスケアを発動可能にするか（連発防止用）")]
    [SerializeField] private float cooldownTime = 3.0f;

    [Header("--- カメラシェイク（画面揺れ）設定 ---")]
    [Tooltip("振り向く瞬間の激しい揺れの強さ（1コマ目の衝撃度）")]
    [SerializeField] private float turnShakeMagnitude = 0.3f;

    [Tooltip("正面を向いた後の持続的な震えの強さ（恐怖による戦慄）")]
    [SerializeField] private float lookShakeMagnitude = 0.05f;

    [Header("--- シーン遷移設定 ---")]
    [Tooltip("遷移先のシーン名")]
    [SerializeField] private string nextSceneName = "GameOverScene";
    [Tooltip("フェード用のCanvasGroup（アルファ値で制御します）")]
    [SerializeField] private CanvasGroup fadePanel;
    [Tooltip("フェードにかかる時間")]
    [SerializeField] private float fadeDuration = 1.0f;

    private bool isTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (isTriggered) return;

        if (other.CompareTag("Player"))
        {
            FirstPersonController player = other.GetComponent<FirstPersonController>();
            if (player != null)
            {
                StartCoroutine(TriggerScareRoutine(player));
            }
        }
    }

    private IEnumerator TriggerScareRoutine(FirstPersonController player)
    {
        isTriggered = true;
        Debug.Log("【ジャンプスケア】演出開始！プレイヤーと敵を完全フリーズします。");

        // 敵のコンポーネント一式を取得して完全停止させる
        EnemyAI enemy = null;
        UnityEngine.AI.NavMeshAgent enemyAgent = null;
        Collider[] enemyColliders = null;

        if (lookAtTarget != null)
        {
            enemy = lookAtTarget.GetComponentInParent<EnemyAI>();
            if (enemy != null)
            {
                // 1. ナビゲーションシステム（移動機能）を即座にOFFにする
                enemyAgent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (enemyAgent != null)
                {
                    enemyAgent.isStopped = true;
                    enemyAgent.velocity = Vector3.zero;
                    enemyAgent.enabled = false;
                }

                // 2. 通常の歩行・走行アニメを止め、ジャンプスケア専用アニメーションを再生
                Animator enemyAnim = enemy.GetComponent<Animator>();
                if (enemyAnim != null)
                {
                    enemyAnim.SetFloat("Speed", 0f);
                    enemyAnim.SetBool("IsChasing", false);
                    enemyAnim.speed = 1.0f;

                    if (!string.IsNullOrEmpty(jumpscareAnimationTrigger))
                    {
                        enemyAnim.SetTrigger(jumpscareAnimationTrigger);
                    }
                }

                // 3. コライダーによる押し合い反発を防止するため、物理衝突を一時無視
                enemyColliders = enemy.GetComponents<Collider>();
                Collider playerCollider = player.GetComponent<Collider>();
                if (enemyColliders != null && playerCollider != null)
                {
                    foreach (var col in enemyColliders)
                    {
                        Physics.IgnoreCollision(col, playerCollider, true);
                    }
                }

                // 4. 敵のAIスクリプトをOFF
                enemy.enabled = false;
            }
        }

        // 1. プレイヤーの移動・視点操作を完全にロック
        player.IsLocked = true;

        // 2. 💡【音響システム（大音量・AudioSource対応版）】
        if (scareSE != null)
        {
            // インスペクターで AudioSource が直接指定されている場合
            if (scareAudioSource != null)
            {
                scareAudioSource.PlayOneShot(scareSE, seVolume);
            }
            // 指定がない場合のフォールバック1：カメラから再生
            else if (playerCameraObject != null)
            {
                AudioSource cameraAudio = playerCameraObject.GetComponent<AudioSource>();
                if (cameraAudio != null)
                {
                    cameraAudio.PlayOneShot(scareSE, seVolume);
                }
                else
                {
                    // カメラにAudioSourceがなければ動的に追加して鳴らす
                    AudioSource newAudio = playerCameraObject.AddComponent<AudioSource>();
                    newAudio.PlayOneShot(scareSE, seVolume);
                }
            }
            // 指定がない場合のフォールバック2：プレイヤーの位置から再生
            else
            {
                AudioSource.PlayClipAtPoint(scareSE, player.transform.position, seVolume);
            }
        }

        // 3. 恐怖画像（UI）を表示
        if (scareImageUI != null)
        {
            scareImageUI.SetActive(true);
        }

        // 演出開始時のカメラの初期角度を記憶
        Quaternion startRotation = playerCameraObject != null ? playerCameraObject.transform.rotation : Quaternion.identity;

        // 4. 指定された演出時間の間、毎フレームカメラ角度を上書きし続ける
        float elapsed = 0f;
        while (elapsed < scareDuration)
        {
            if (playerCameraObject != null && lookAtTarget != null)
            {
                // ターゲット（顔のEmpty）への正確な方向ベクトル
                Vector3 targetDirection = lookAtTarget.position - playerCameraObject.transform.position;

                if (targetDirection != Vector3.zero)
                {
                    // 最終的に向くべき目標角度（Quaternion）
                    Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                    Quaternion finalRotation;

                    // 滑らかに振り向く補間計算（turnDurationで設定した時間で徐々に近づける）
                    if (elapsed < turnDuration)
                    {
                        float t = elapsed / turnDuration;
                        t = t * t * (3f - 2f * t); // スムーズな緩急（SmoothStep）
                        finalRotation = Quaternion.Slerp(startRotation, targetRotation, t);
                    }
                    else
                    {
                        // 振り向き時間を過ぎたら、完全にロックオン
                        finalRotation = targetRotation;
                    }

                    // カメラシェイクの計算
                    float currentShakeMagnitude = (elapsed < turnDuration) ? turnShakeMagnitude : lookShakeMagnitude;

                    // ランダムなノイズ（揺れ）のオフセットを計算
                    Vector3 shakeOffset = new Vector3(
                        Random.Range(-1f, 1f) * currentShakeMagnitude,
                        Random.Range(-1f, 1f) * currentShakeMagnitude,
                        Random.Range(-1f, 1f) * currentShakeMagnitude
                    );

                    // 本来のターゲットの角度（finalRotation）に、シェイクの微小な回転ノイズを掛け合わせる
                    Quaternion shookRotation = finalRotation * Quaternion.Euler(shakeOffset * 20f);

                    // カメラ自体の角度にシェイクを加えた角度を直接更新
                    playerCameraObject.transform.rotation = shookRotation;

                    // FirstPersonControllerの内部角度変数も、シェイクを加えた角度に完全同期
                    if (player != null)
                    {
                        player.ForceSyncCameraRotation(shookRotation);
                    }
                }
            }

            // 安全のための物理ゼロ固定
            if (enemy != null)
            {
                Rigidbody enemyRb = enemy.GetComponent<Rigidbody>();
                if (enemyRb != null)
                {
                    enemyRb.linearVelocity = Vector3.zero;
                    enemyRb.angularVelocity = Vector3.zero;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 5. 演出終了後の後処理
        if (scareImageUI != null)
        {
            scareImageUI.SetActive(false);
        }

        // 6. プレイヤーのロックを解除
        if (player != null)
        {
            player.IsLocked = false;
        }

        // 演出終了後に敵のシステムを安全に再起動
        if (enemy != null)
        {
            Collider playerCollider = player.GetComponent<Collider>();
            if (enemyColliders != null && playerCollider != null)
            {
                foreach (var col in enemyColliders)
                {
                    Physics.IgnoreCollision(col, playerCollider, false);
                }
            }

            if (enemyAgent != null)
            {
                enemyAgent.enabled = true;
            }

            // --- 追加：フェードしてシーン遷移 ---
            if (fadePanel != null)
            {
                float alpha = 0f;
                while (alpha < 1f)
                {
                    alpha += Time.deltaTime / fadeDuration;
                    fadePanel.alpha = alpha;
                    yield return null;
                }
            }

            // シーン遷移
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);

            enemy.enabled = true;
            Debug.Log("【ジャンプスケア】演出終了。敵のAIを再開しました。");
        }

        yield return new WaitForSeconds(cooldownTime);
        isTriggered = false;
    }
}