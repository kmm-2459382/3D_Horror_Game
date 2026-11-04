using UnityEngine;

public class NewDoorMech : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("ドアが開いたときの回転角度をXYZで指定します")]
    public Vector3 OpenRotation;

    [Tooltip("ドアが閉じているときの初期回転角度をXYZで指定します")]
    public Vector3 CloseRotation;

    [Tooltip("ドアが開閉するスピードです（3〜10推奨）")]
    public float rotSpeed = 5f;

    [Header("Door State")]
    [Tooltip("現在のドアの状態（チェック有＝開く、チェック無＝閉じる）")]
    public bool doorBool;

    [Header("--- SE（効果音）設定 ---")]
    [SerializeField] private AudioClip doorOpenSE;
    [SerializeField] private AudioClip doorCloseSE;
    [SerializeField][Range(0f, 1f)] private float seVolume = 0.8f;

    private AudioSource myAudioSource;
    private bool previousDoorBool;

    void Start()
    {
        // ゲーム開始時は初期角度（閉じている状態）にする
        transform.localRotation = Quaternion.Euler(CloseRotation);
        previousDoorBool = doorBool;

        // 子階層からAudioSourceを自動検索して3Dサウンド設定を行う
        myAudioSource = GetComponentInChildren<AudioSource>();
        if (myAudioSource == null)
        {
            // AudioSourceがない場合は動的に追加する安全対策
            myAudioSource = gameObject.AddComponent<AudioSource>();
        }

        myAudioSource.playOnAwake = false;
        myAudioSource.loop = false;
        myAudioSource.spatialBlend = 1f; // 3Dサウンド化
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

        // クォータニオンを用いたスムーズな3D回転処理（コライダーを消さないため常に安定動作します）
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, rotSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 外部（プレイヤーやマネージャー）から呼び出される開閉切り替えメソッド
    /// </summary>
    public void ToggleDoor()
    {
        doorBool = !doorBool;
    }

    /// <summary>
    /// ドアの状態を直接指定するメソッド
    /// </summary>
    public void SetDoorState(bool isOpen)
    {
        doorBool = isOpen;
    }
}