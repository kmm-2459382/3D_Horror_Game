using UnityEngine;
using UnityEngine.InputSystem;

public class PauseManager : MonoBehaviour
{
    [SerializeField] private GameObject pauseCanvas;
    [SerializeField] private MonoBehaviour playerCameraScript;

    private bool isPaused = false;

    void Start()
    {
        // ゲーム開始時にポーズ画面を隠す
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

        // 2. Canvasの表示切り替え
        if (pauseCanvas != null) pauseCanvas.SetActive(isPaused);

        // 3. カーソルの直接制御
        // ポーズ中(isPaused)ならカーソル表示(true)、解除時なら非表示(false)
        Cursor.visible = isPaused;
        Cursor.lockState = isPaused ? CursorLockMode.None : CursorLockMode.Locked;

        // 4. カメラ操作の有効/無効
        if (playerCameraScript != null)
        {
            playerCameraScript.enabled = !isPaused;
        }
    }
}