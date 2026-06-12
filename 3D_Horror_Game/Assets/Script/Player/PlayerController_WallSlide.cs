using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController_WallSlide : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5.0f;
    [SerializeField] private float gravity = 9.81f;

    [Header("Wall Slide Settings")]
    [SerializeField] private LayerMask wallLayerMask = ~0; // すべてのレイヤーを対象（初期値）
    [SerializeField] private float wallDetectionRange = 0.1f;

    private CharacterController characterController;
    private Vector3 velocity;

    private void Awake()
    {
        // 必須となるCharacterControllerの参照を自動取得
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        // 1. キーボード/ゲームパッドからの入力を取得 (3D空間のX軸とZ軸)
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        // 2. カメラの向きやプレイヤーの正面方向を基準にした移動方向ベクトルを作成
        Vector3 inputDirection = (transform.right * moveX + transform.forward * moveZ).normalized;

        // 3. 移動方向ベクトルの決定（デフォルトは入力された方向）
        Vector3 finalMoveDirection = inputDirection;

        // 4. 壁に当たっている場合の「壁ずり（スライド）」補正処理
        if (inputDirection.magnitude > 0.1f)
        {
            finalMoveDirection = AdjustDirectionForWallCast(inputDirection);
        }

        // 5. 最終的な移動速度の計算
        Vector3 moveVelocity = finalMoveDirection * moveSpeed;

        // 6. 重力処理（Character Controllerは自前で重力を計算する必要があるため）
        if (characterController.isGrounded)
        {
            velocity.y = -0.5f; // 接地時は完全に0にせず、少し下向きの力を残して安定させる
        }
        else
        {
            velocity.y -= gravity * Time.deltaTime; // 空中にいる間は重力を累積
        }

        // 7. 水平移動速度と垂直速度（重力）を統合
        velocity.x = moveVelocity.x;
        velocity.z = moveVelocity.z;

        // 8. 最終的なベクトルでCharacter Controllerを動かす (Time.deltaTimeを掛けてフレームレートを安定化)
        characterController.Move(velocity * Time.deltaTime);
    }

    /// <summary>
    /// レイキャスト（目に見えない光線）を飛ばし、壁に接触している場合は進行方向を壁沿いに曲げるメソッド
    /// </summary>
    private Vector3 AdjustDirectionForWallCast(Vector3 currentDirection)
    {
        // CharacterControllerのサイズを考慮してレイの起点と球体の半径を設定
        Vector3 origin = transform.position + Vector3.up * (characterController.height * 0.5f);
        float radius = characterController.radius;

        // 移動しようとしている少し先に向かって球体状の判定（SphereCast）を飛ばす
        float castDistance = wallDetectionRange;

        // 物理的な厚みを考慮した3D特有のレイキャスト判定
        if (Physics.SphereCast(origin, radius, currentDirection, out RaycastHit hit, castDistance, wallLayerMask))
        {
            // 接触した面が「歩ける床」ではなく「垂直に近い壁」である場合のみ処理する（斜面での誤作動防止）
            if (Vector3.Dot(hit.normal, Vector3.up) < 0.5f)
            {
                // 【3D数学の核心処理】
                // Vector3.ProjectOnPlane を使用し、入力された進行ベクトルを壁の「法線ベクトル（面の向き）」に対して平行なベクトルに投影します。
                // これにより、壁に向かって突進しても速度が殺されず、壁の表面を滑るようなベクトルへ綺麗に変換されます。
                Vector3 slideDirection = Vector3.ProjectOnPlane(currentDirection, hit.normal).normalized;

                return slideDirection;
            }
        }

        // 壁に当たっていない場合は元の方向をそのまま返す
        return currentDirection;
    }
}