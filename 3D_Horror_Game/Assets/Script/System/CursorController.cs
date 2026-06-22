using UnityEngine;

public class CursorController : MonoBehaviour
{
    [Tooltip("true: カーソルを隠す(FPS向き), false: カーソルを表示する")]
    [SerializeField] private bool hideCursor = true;

    void Start()
    {
        // ゲーム開始時またはシーンロード時の初期状態のみを設定
        ApplyCursorState(!hideCursor);
    }

    // 内部でカーソル状態を反映するメソッド
    private void ApplyCursorState(bool shouldShowCursor)
    {
        Cursor.visible = shouldShowCursor;
        Cursor.lockState = shouldShowCursor ? CursorLockMode.None : CursorLockMode.Locked;
    }
}