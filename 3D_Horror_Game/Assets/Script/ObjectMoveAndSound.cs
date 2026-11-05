using UnityEngine;
using System.Collections;

public class ObjectMoveAndSound : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("移動先の目標位置（ワールド座標またはローカル座標）")]
    public Vector3 targetPosition;
    [Tooltip("移動にかかるスピード")]
    public float moveSpeed = 3f;

    [Header("Audio Settings")]
    [Tooltip("移動開始時やエリア侵入時に再生する効果音（SE）")]
    [SerializeField] private AudioClip actionSE;
    [SerializeField][Range(0f, 1f)] private float seVolume = 1.0f;

    private Vector3 startPosition;
    private bool hasTriggered = false;
    private AudioSource audioSource;

    void Start()
    {
        // 初期位置を記録
        startPosition = transform.position;

        // AudioSourceの取得、なければ自動追加
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 1f; // 3D空間音響を有効化
    }

    /// <summary>
    /// プレイヤーがトリガーエリアに入ったときに呼び出される
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // すでに発動済みでなく、入ってきたのが「Player」タグの場合
        if (!hasTriggered && other.CompareTag("Player"))
        {
            hasTriggered = true;

            // 効果音の再生
            if (actionSE != null && audioSource != null)
            {
                audioSource.PlayOneShot(actionSE, seVolume);
            }

            // 移動処理の開始
            StartCoroutine(MoveObjectRoutine());
        }
    }

    private IEnumerator MoveObjectRoutine()
    {
        // 目標地点へ向かってスムーズに移動
        while (Vector3.Distance(transform.position, targetPosition) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = targetPosition; // 位置を完全に合わせる
    }
}