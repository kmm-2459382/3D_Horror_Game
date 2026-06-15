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

    // 【チャタリング・2連打防止用】
    private float lastInteractTime = 0f;
    private const float interactCooldown = 0.3f; // 次のクリックを受け付けるまでの秒数(0.3秒)

    void Start()
    {
        mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("【PlayerInteractor】メインカメラ(Main Cameraタグ)が見つかりません。カメラのTagを確認してください。");
        }

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
    /// 新しいInputSystemの「Interact」ボタンなどが押されたときに呼び出される関数
    /// </summary>
    public void OnInteract(InputValue value)
    {
        if (mainCamera == null) return;

        // 【修正点】Input Systemの「ボタンが離された瞬間」の呼び出しなどは無視する
        // かつ、前回のクリックから0.3秒経っていない場合は、連打とみなして処理を強制拒否する
        if (Time.time - lastInteractTime < interactCooldown)
        {
            return;
        }

        // 画面の中央から前方に飛ばす光線（Ray）を作成
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.red, 1.0f);

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

                    // アクションが成功したので、最終インタラクト時間を更新
                    lastInteractTime = Time.time;
                }
                else
                {
                    Debug.LogError("【実行エラー】DoorControllerの参照がないため、ロック・連動処理を行えません。単体開閉で代用します。");
                    door.doorBool = !door.doorBool;
                    lastInteractTime = Time.time;
                }
                return;
            }

            // --- 2. 鍵（KeyItem）へのインタラクト処理 ---
            KeyItem key = hit.collider.GetComponentInParent<KeyItem>();

            if (key != null)
            {
                if (playerInventory != null)
                {
                    Debug.Log($"【鍵の取得】{hit.collider.gameObject.name} をクリックしたため、インベントリへ追加します。");
                    key.PickupKey(playerInventory);

                    // アクションが成功したので、最終インタラクト時間を更新
                    lastInteractTime = Time.time;
                }
                else
                {
                    Debug.LogError("【実行エラー】PlayerInventoryの参照がないため、鍵を取得できません。");
                }
                return;
            }

            // --- 3. 懐中電灯（FlashlightItem）へのインタラクト処理 ---
            // 光線が当たったオブジェクトに「FlashlightItem」スクリプトがついているか探します
            FlashlightItem flashlight = hit.collider.GetComponentInParent<FlashlightItem>();

            if (flashlight != null)
            {
                Debug.Log($"【懐中電灯の取得】{hit.collider.gameObject.name} をクリックしたため、装備を有効化します。");
                flashlight.PickupFlashlight();

                // アクションが成功したので、連打防止用の最終インタラクト時間を更新して終了
                lastInteractTime = Time.time;
                return;
            }
        }
    }
}