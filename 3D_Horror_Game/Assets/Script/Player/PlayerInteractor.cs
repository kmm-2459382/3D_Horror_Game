using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Raycast Settings")]
    [Tooltip("ドアに手が届く距離（長さ）です")]
    public float interactDistance = 3.0f;

    [Tooltip("ドアに設定したInteractableレイヤーを指定します")]
    public LayerMask interactableLayer;

    [Header("--- 外部システム連携 ---")]
    [Tooltip("シーン上にある DoorSystemManager (DoorController) をここに割り当ててください")]
    [SerializeField] private DoorController doorController;

    [Tooltip("プレイヤー自身にある PlayerInventory スクリプトをここに割り当ててください")]
    [SerializeField] private PlayerInventory playerInventory;

    private Camera mainCamera;

    void Start()
    {
        // プレイヤーのメインカメラを自動取得
        mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("【PlayerInteractor】メインカメラ(Main Cameraタグ)が見つかりません。カメラのTagを確認してください。");
        }

        // インスペクターでの割り当て忘れ防止チェック
        if (doorController == null)
        {
            Debug.LogError($"【設定エラー】{gameObject.name} の PlayerInteractor に「Door Controller」が割り当てられていません！");
        }
        if (playerInventory == null)
        {
            Debug.LogError($"【設定エラー】{gameObject.name} の PlayerInteractor に「Player Inventory」が割り当てられていません！");
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
                // 【修正箇所】ドア単体の開閉を直接呼ぶのをやめ、必ず一元管理マネージャーを介して命令を送るように変更
                if (doorController != null)
                {
                    Debug.Log($"【マネージャー経由】{hit.collider.gameObject.name} へのインタラクトを DoorController に委託します。");
                    doorController.Interact(door, playerInventory);
                }
                else
                {
                    Debug.LogError("【実行エラー】DoorControllerの参照がないため、ロック・連動処理を行えません。単体開閉で代用します。");
                    door.ToggleDoor(); // バックアップ用の単体開閉
                }
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