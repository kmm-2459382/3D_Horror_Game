using UnityEngine;
using UnityEngine.InputSystem;

public class PauseManager : MonoBehaviour
{
    [Header("--- 参照設定 ---")]
    [SerializeField] private GameObject pauseCanvas;
    [Tooltip("プレイヤーのカメラ制御スクリプト（またはプレイヤーオブジェクト）")]
    [SerializeField] private MonoBehaviour playerCameraScript; // カメラ制御のスクリプトを指定

    private bool isPaused = false;

    void Start()
    {
        if (pauseCanvas != null) pauseCanvas.SetActive(false);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;

        // 1. ゲーム時間の停止/再開
        Time.timeScale = isPaused ? 0f : 1f;

        // 2. UIの表示切り替え
        if (pauseCanvas != null) pauseCanvas.SetActive(isPaused);

        // 3. カーソルの制御
        Cursor.visible = isPaused;
        Cursor.lockState = isPaused ? CursorLockMode.None : CursorLockMode.Locked;

        // 4. プレイヤーの操作(視点移動)の停止/再開
        if (playerCameraScript != null)
        {
            playerCameraScript.enabled = !isPaused;
        }
    }
}