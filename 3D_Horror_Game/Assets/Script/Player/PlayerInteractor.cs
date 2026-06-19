using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Raycast Settings")]
    [Tooltip("ドアやアイテムに手が届く距離（長さ）です")]
    public float interactDistance = 3.0f;

    [Tooltip("ドアやアイテムに設定したInteractableレイヤーを指定します")]
    public LayerMask interactableLayer;

    [Header("--- 外部システム連携 ---")]
    [Tooltip("シーン上にある DoorSystemManager (DoorController) をここに割り当ててください")]
    [SerializeField] private DoorController doorController;

    [Tooltip("プレイヤー自身にある PlayerInventory スクリプトをここに割り当ててください")]
    [SerializeField] private PlayerInventory playerInventory;

    private Camera mainCamera;

    // 【連打・誤作動防止用】
    private float lastInteractTime = 0f;
    private const float interactCooldown = 0.3f; // 次の入力を受け付けるまでの最低間隔（秒）

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
            Debug.LogError($"{gameObject.name} の PlayerInteractor に「Player Inventory」が割り当てられていません。");
        }
    }

    /// <summary>
    /// 新しいInputSystemの「Interact」ボタンなどが押されたときに呼び出される関数
    /// </summary>
    public void OnInteract(InputValue value)
    {
        if (mainCamera == null) return;

        // 【チャタリング対策】前回の決定から0.3秒経っていない場合は、高速連打とみなして処理を拒否する
        if (Time.time - lastInteractTime < interactCooldown)
        {
            return;
        }

        // 画面の中央（クロスヘアの位置）から前方に飛ばす光線（Ray）を作成
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        // デバッグ用：Unityのシーンビューに、クリックした瞬間の光線を1秒間だけ赤い線で表示します
        Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.red, 1.0f);

        // 指定した距離の中に、特定のレイヤーを持つオブジェクトがあるか見張ります
        if (Physics.Raycast(ray, out hit, interactDistance, interactableLayer))
        {
            // --- 1. ドアへのインタラクト処理 ---
            NewDoorMech door = hit.collider.GetComponentInParent<NewDoorMech>();

            if (door != null)
            {
                if (doorController != null)
                {
                    Debug.Log($"【マネージャー経由】{hit.collider.gameObject.name} へのインタラクトを DoorController に委託します。");
                    doorController.Interact(door, playerInventory);
                    lastInteractTime = Time.time; // 成功時間を記録
                }
                else
                {
                    Debug.LogError("【実行エラー】DoorControllerの参照がないため、ロック・連動処理を行えません。単体開閉で代用します。");
                    door.doorBool = !door.doorBool;
                    lastInteractTime = Time.time;
                }
                return; // ドアの処理をした場合はここで終了
            }

            // --- 2. 鍵（KeyItem）へのインタラクト処理 ---
            KeyItem key = hit.collider.GetComponentInParent<KeyItem>();

            if (key != null)
            {
                if (playerInventory != null)
                {
                    Debug.Log($"【鍵の取得】{hit.collider.gameObject.name} をクリックしたため、インベントリへ追加します。");
                    key.PickupKey(playerInventory);
                    lastInteractTime = Time.time; // 成功時間を記録
                }
                else
                {
                    Debug.LogError("【実行エラー】PlayerInventoryの参照がないため、鍵を取得できません。");
                }
                return; // 鍵の処理をした場合はここで終了
            }

            // --- 3. 懐中電灯（FlashlightItem）へのインタラクト処理 ---
            // 光線が当たったオブジェクト（またはその親）に「FlashlightItem」がついているか探します
            FlashlightItem flashlight = hit.collider.GetComponentInParent<FlashlightItem>();

            if (flashlight != null)
            {
                Debug.Log($"【懐中電灯の取得】{hit.collider.gameObject.name} をクリックしたため、装備を有効化します。");

                // 懐中電灯側のスクリプトに装備処理（SetActive処理など）を行わせる
                flashlight.PickupFlashlight();

                lastInteractTime = Time.time; // 成功時間を記録
                return; // 懐中電灯の処理をした場合はここで終了
            }

            // --- 4. パズル用ボタンへのインタラクト処理 ---
            PushButton button = hit.collider.GetComponentInParent<PushButton>();

            if (button != null)
            {
                Debug.Log($"【ボタン操作】{button.gameObject.name} を押しました。");
                button.OnInteract(); // ボタン側の処理を呼び出す
                lastInteractTime = Time.time;
                return; // ボタンの処理をした場合はここで終了
            }
        }
    }
}