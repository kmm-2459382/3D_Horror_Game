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
    [Tooltip("プレイヤーが強制的に振り向く対象（敵の顔のトランスフォームなど）")]
    [SerializeField] private Transform lookAtTarget;

    [Header("--- 演出・エフェクト設定 ---")]
    [Tooltip("ドカン！と鳴らす恐怖効果音")]
    [SerializeField] private AudioClip scareSE;
    [SerializeField][Range(0f, 1f)] private float seVolume = 1.0f;

    [Tooltip("画面に表示させる恐怖画像（UIオブジェクト）。使わない場合は空欄でOK")]
    [SerializeField] private GameObject scareImageUI;

    [Header("--- 演出時間の設定 ---")]
    [Tooltip("敵の顔を強制的に見せ続ける時間（秒）")]
    [SerializeField] private float scareDuration = 2.0f;

    [Tooltip("演出終了後、何秒経ったらもう一度次のジャンプスケアを発動可能にするか（連発防止用）")]
    [SerializeField] private float cooldownTime = 3.0f;

    private bool isTriggered = false;

    private void Start()
    {
        if (playerCameraObject == null && Camera.main != null)
        {
            playerCameraObject = Camera.main.gameObject;
        }

        if (lookAtTarget == null)
        {
            lookAtTarget = this.transform;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // プレイヤーに触れた、かつ、現在クールダウン中でない場合
        if (other.CompareTag("Player") && !isTriggered)
        {
            isTriggered = true;

            FirstPersonController playerController = other.GetComponent<FirstPersonController>();
            StartCoroutine(TriggerCinemachineScareRoutine(playerController));
        }
    }

    private IEnumerator TriggerCinemachineScareRoutine(FirstPersonController player)
    {
        Debug.Log("【ジャンプスケア】接触検知。");

        // 1. プレイヤーの移動・入力を完全にロック
        if (player != null)
        {
            player.IsLocked = true;
        }

        // 2. 恐怖音を再生
        if (scareSE != null)
        {
            AudioSource.PlayClipAtPoint(scareSE, transform.position, seVolume);
        }

        // 3. ビックリ画像を画面に表示
        if (scareImageUI != null)
        {
            scareImageUI.SetActive(true);
        }

        // 4. 指定された時間、毎フレームカメラを敵の顔へ向け、コントローラー側の記憶も毎フレーム上書きする
        float elapsed = 0f;
        while (elapsed < scareDuration)
        {
            if (playerCameraObject != null && lookAtTarget != null)
            {
                // カメラから敵の顔への方向を計算
                Vector3 targetDirection = lookAtTarget.position - playerCameraObject.transform.position;

                if (targetDirection != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(targetDirection);

                    // カメラ自体の角度を更新
                    playerCameraObject.transform.rotation = targetRotation;

                    // 💡【最重要】FirstPersonController側が記憶している視線の角度も、敵の顔の方向で上書きし続ける
                    if (player != null)
                    {
                        player.ForceSyncCameraRotation(targetRotation);
                    }
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

        // 6. プレイヤーのロックを解除（この時点でコントローラーの内部角度が敵の顔と一致しているため、引き戻されません）
        if (player != null)
        {
            player.IsLocked = false;
        }

        Debug.Log($"【ジャンプスケア】演出終了。{cooldownTime}秒のクールダウンに入ります。");

        // 7. 💡 指定したクールダウン時間（秒）だけ待ってから、再度触れたら鳴るようにフラグをリセットする
        yield return new WaitForSeconds(cooldownTime);

        isTriggered = false;
        Debug.Log("【ジャンプスケア】再発動が可能になりました。");
    }
}