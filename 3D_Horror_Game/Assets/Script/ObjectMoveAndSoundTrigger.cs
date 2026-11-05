using UnityEngine;
using System.Collections;

public class ObjectMoveAndSoundTrigger : MonoBehaviour
{
    [Header("Target Object Settings")]
    [Tooltip("動かしたい、かつ音を鳴らしたいオブジェクトを指定します")]
    [SerializeField] private Transform targetObject;

    [Header("Local Transform Settings")]
    [Tooltip("移動先の目標ローカル座標（親オブジェクトからの相対位置）")]
    [SerializeField] private Vector3 targetLocalPosition;

    [Tooltip("回転先の目標ローカル回転角（オイラー角）")]
    [SerializeField] private Vector3 targetLocalEulerAngles;

    [Tooltip("移動・回転にかかるスピード")]
    [SerializeField] private float moveSpeed = 3f;

    [Header("Audio Settings")]
    [Tooltip("オブジェクト自身から鳴らす効果音（SE）")]
    [SerializeField] private AudioClip actionSE;
    [SerializeField][Range(0f, 1f)] private float seVolume = 1.0f;

    [Header("Trigger Settings")]
    [Tooltip("一度だけ発動させる場合はチェックを入れる")]
    [SerializeField] private bool triggerOnce = true;

    private bool hasTriggered = false;
    private AudioSource targetAudioSource;

    void Start()
    {
        // 指定された動かすオブジェクトから AudioSource を取得、なければ自動追加
        if (targetObject != null)
        {
            targetAudioSource = targetObject.GetComponent<AudioSource>();
            if (targetAudioSource == null)
            {
                targetAudioSource = targetObject.gameObject.AddComponent<AudioSource>();
            }

            targetAudioSource.playOnAwake = false;
            targetAudioSource.loop = false;
            targetAudioSource.spatialBlend = 1.0f; // 3Dサウンドを確実に有効化
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // すでに発動済み（1回限定の場合）ならスルーし、プレイヤーの侵入か確認
        if (triggerOnce && hasTriggered) return;

        if (other.CompareTag("Player"))
        {
            hasTriggered = true;

            // 動くオブジェクトの位置から効果音を再生する
            if (actionSE != null && targetAudioSource != null)
            {
                targetAudioSource.PlayOneShot(actionSE, seVolume);
            }

            // オブジェクトのローカル座標・回転ベースでの移動処理を開始
            if (targetObject != null)
            {
                StartCoroutine(MoveAndRotateLocalRoutine());
            }
        }
    }

    private IEnumerator MoveAndRotateLocalRoutine()
    {
        Quaternion targetLocalRotation = Quaternion.Euler(targetLocalEulerAngles);

        // ローカル座標とローカル回転を基準にして、目標のローカル位置・角度へ向かってスムーズに移動・回転する
        while (Vector3.Distance(targetObject.localPosition, targetLocalPosition) > 0.01f ||
               Quaternion.Angle(targetObject.localRotation, targetLocalRotation) > 0.1f)
        {
            targetObject.localPosition = Vector3.MoveTowards(targetObject.localPosition, targetLocalPosition, moveSpeed * Time.deltaTime);
            targetObject.localRotation = Quaternion.Slerp(targetObject.localRotation, targetLocalRotation, moveSpeed * Time.deltaTime);
            yield return null;
        }

        // 位置と回転を完全に合わせる
        targetObject.localPosition = targetLocalPosition;
        targetObject.localRotation = targetLocalRotation;
    }
}