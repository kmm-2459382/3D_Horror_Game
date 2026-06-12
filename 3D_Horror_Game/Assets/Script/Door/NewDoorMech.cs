using UnityEngine;

public class NewDoorMech : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("ドアが開いたときの回転角度をXYZで指定します")]
    public Vector3 OpenRotation;

    [Tooltip("ドアが閉じているときの初期回転角度をXYZで指定します")]
    public Vector3 CloseRotation;

    [Tooltip("ドアが開閉するスピードです（Lerp用: 3〜5推奨）")]
    public float rotSpeed = 3f;

    [Header("Door State")]
    [Tooltip("現在のドアの状態（チェック有＝開く、チェック無＝閉じる）")]
    public bool doorBool;

    [Header("--- 3D SE（効果音）設定 ---")]
    [SerializeField] private AudioClip doorOpenSE;  // このドアが開く時のSE
    [SerializeField] private AudioClip doorCloseSE; // このドアが閉じる時のSE
    [SerializeField][Range(0f, 1f)] private float seVolume = 0.8f; // このドアのSE音量

    private Collider doorCollider;     // 子オブジェクトから見つけるコライダー
    private AudioSource myAudioSource; // 子オブジェクトから見つけるAudioSource
    private bool previousDoorBool;     // 前フレームのドアの状態を記録

    void Start()
    {
        // ゲーム開始時は初期角度（閉じている状態）にする
        transform.localRotation = Quaternion.Euler(CloseRotation);
        previousDoorBool = doorBool;

        // 【ここを重要修正】当たり判定は「このスクリプトがついている子」にあるため、
        // 自身のコライダーではなく、子オブジェクトからコライダーを確実に取得する
        doorCollider = GetComponentInChildren<Collider>();
        if (doorCollider == null)
        {
            Debug.LogError($"【設定エラー】{gameObject.name} の「子オブジェクト」に Collider（当たり判定）が見つかりません！");
        }

        // 子や孫の階層からAudioSourceを自動検索
        myAudioSource = GetComponentInChildren<AudioSource>();
        if (myAudioSource == null)
        {
            Debug.LogError($"【設定エラー】{gameObject.name} の子オブジェクトに AudioSource が見つかりません！");
        }
        else
        {
            myAudioSource.playOnAwake = false;
            myAudioSource.loop = false;
            myAudioSource.spatialBlend = 1f; // 3Dサウンド化
        }

        // ゲーム開始時点の当たり判定の状態を正しく更新
        UpdateColliderState();
    }

    void Update()
    {
        // 目標とする回転クォータニオンの決定
        Quaternion targetRotation = doorBool ? Quaternion.Euler(OpenRotation) : Quaternion.Euler(CloseRotation);

        // ドアの状態（開閉の指示）が変わった瞬間を検知してSEを鳴らす
        if (doorBool != previousDoorBool)
        {
            if (myAudioSource != null)
            {
                if (doorBool)
                {
                    if (doorOpenSE != null) myAudioSource.PlayOneShot(doorOpenSE, seVolume);
                }
                else
                {
                    if (doorCloseSE != null) myAudioSource.PlayOneShot(doorCloseSE, seVolume);
                }
            }
            previousDoorBool = doorBool;
        }

        // 【ご要望】動きの好みが前の方が良かったため、Lerpの滑らかな挙動に戻しました
        transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRotation, rotSpeed * Time.deltaTime);

        // 毎フレーム、Lerpで動きながら角度を厳密にチェックし、子オブジェクトの当たり判定を切り替える
        UpdateColliderState();
    }

    /// <summary>
    /// 子オブジェクトにある当たり判定を、完全に「開ききった/閉じきった」時以外は強制的に消すロジック
    /// </summary>
    private void UpdateColliderState()
    {
        if (doorCollider == null) return;

        // 現在の目標角度
        Quaternion targetRotation = doorBool ? Quaternion.Euler(OpenRotation) : Quaternion.Euler(CloseRotation);

        // Lerpは目標角度にジワジワ近づくため、完全に同じ角度（誤差0度）にはなりにくい特性があります。
        // そのため、「見た目上ほぼ完全に止まったとみなせる超微小な角度差（0.05度未満）」になったかを判定します。
        if (Quaternion.Angle(transform.localRotation, targetRotation) < 0.05f)
        {
            // 完全に目標角度に達した（止まった）ので、角度を完全に固定し、子コライダーを有効化（当たり判定を出す）
            transform.localRotation = targetRotation;
            doorCollider.enabled = true;
        }
        else
        {
            // Lerpで回転している最中（1度でもズレている間）は、子コライダーを強制的に無効化して当たり判定を完全に消す
            doorCollider.enabled = false;
        }
    }

    /// <summary>
    /// プレイヤーや敵が接触したときにトリガーとして呼び出される（従来通りの互換性を維持）
    /// </summary>
    public void ToggleDoor()
    {
        doorBool = !doorBool;
    }
}