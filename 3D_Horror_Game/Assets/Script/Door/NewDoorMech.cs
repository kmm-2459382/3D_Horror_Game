using UnityEngine;

public class NewDoorMech : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("ドアが開いたときの回転角度をXYZで指定します")]
    public Vector3 OpenRotation;

    [Tooltip("ドアが閉じているときの初期回転角度をXYZで指定します")]
    public Vector3 CloseRotation;

    [Tooltip("ドアが開閉するスピードです")]
    public float rotSpeed = 3f;

    [Header("Door State")]
    [Tooltip("現在のドアの状態（チェック有＝開く、チェック無＝閉じる）")]
    public bool doorBool;

    void Start()
    {
        // ゲーム開始時は初期角度（閉じている状態）にする
        transform.localRotation = Quaternion.Euler(CloseRotation);
    }

    void Update()
    {
        // 3D数学・クォータニオン処理:
        // 静的(Static)が解除されたことにより、このローカル回転の計算が
        // 子や孫の3Dモデル（メッシュ）に対して正常に適用されるようになりました。
        if (doorBool)
        {
            transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(OpenRotation), rotSpeed * Time.deltaTime);
        }
        else
        {
            transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(CloseRotation), rotSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// プレイヤー（PlayerInteractor.cs）から直接呼び出される、ドア開閉のトリガー関数
    /// </summary>
    public void ToggleDoor()
    {
        doorBool = !doorBool;

        // 【ホラー演出のヒント】
        // ここに「AudioSource.PlayClipAtPoint(開閉音, transform.position);」などを追記すれば
        // ドアを開けた瞬間に怖い環境音や軋み音を鳴らすことができます。
        Debug.Log($"【NewDoorMech】ドアの開閉が実行されました。現在の状態: {(doorBool ? "開" : "閉")}");
    }
}