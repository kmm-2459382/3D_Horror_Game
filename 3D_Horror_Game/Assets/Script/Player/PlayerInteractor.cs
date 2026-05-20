using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Raycast Settings")]
    [Tooltip("ドアに手が届く距離（長さ）です")]
    public float interactDistance = 3.0f;

    [Tooltip("ドアに設定したInteractableレイヤーを指定します")]
    public LayerMask interactableLayer;

    private Camera mainCamera;

    void Start()
    {
        // プレイヤーのメインカメラを自動取得
        mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("【PlayerInteractor】メインカメラ(Main Cameraタグ)が見つかりません。カメラのTagを確認してください。");
        }
    }

    /// <summary>
    /// Input System（Broadcast Messages）から呼び出される関数。
    /// スクショで設定していただいた「Interact」アクション（左クリック）に対応しています。
    /// </summary>
    public void OnInteract(InputValue value)
    {
        // マウス左クリックが「押された瞬間」だけ処理を先に進める
        if (!value.isPressed) return;

        if (mainCamera == null) return;

        // 画面中央（クロスヘアの場所）から前方に飛ばす光線（Ray）を作成
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        // デバッグ用：Unityのシーンビューに、クリックした瞬間の光線を1秒間だけ赤い線で表示します
        Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.red, 1.0f);

        // 指定した距離（interactDistance）の中に、特定のレイヤー（interactableLayer）を持つオブジェクトがあるか見張ります
        if (Physics.Raycast(ray, out hit, interactDistance, interactableLayer))
        {
            // 光線が当たったオブジェクト（またはその親）に「NewDoorMech」スクリプトがついているか探します
            NewDoorMech door = hit.collider.GetComponentInParent<NewDoorMech>();

            if (door != null)
            {
                // ドアが見つかったら、直接ドアの開閉関数を実行（命令）します！
                Debug.Log($"【レイキャスト成功】正面のドア「{hit.collider.gameObject.name}」を発見。開閉命令を送ります。");
                door.ToggleDoor();
            }
            else
            {
                Debug.LogWarning($"【検知】{hit.collider.gameObject.name} に触れましたが、NewDoorMechスクリプトが見つかりません。");
            }
        }
        else
        {
            Debug.Log("【操作検知】左クリックされましたが、射程圏内に「Interactable」レイヤーのオブジェクトがありません。");
        }
    }
}