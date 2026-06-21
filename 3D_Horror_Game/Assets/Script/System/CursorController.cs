using UnityEngine;

public class CursorController : MonoBehaviour
{
    [Tooltip("true: カーソルを隠す(FPS向き), false: カーソルを表示する")]
    [SerializeField] private bool hideCursor = true;

    void Start()
    {
        // カーソルを隠す設定
        Cursor.visible = !hideCursor;

        // カーソルを画面中央に固定（ロック）する設定
        if (hideCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
        }
    }
}