using UnityEngine;
using UnityEngine.SceneManagement; // シーン操作に必須

public class SettingManager : MonoBehaviour
{
    [Header("--- 設定 ---")]
    [Tooltip("移動先のシーン名（ビルド設定で追加しておくこと）")]
    [SerializeField] private string titleSceneName = "Title";
    [SerializeField] private string gameSceneName = "Phase1";

    /// <summary>
    /// ボタンから呼び出す移動関数
    /// </summary>
    public void StartGame()
    {
        Debug.Log("ゲームを開始します...");
        Time.timeScale = 1f; // 時間を戻す
        Cursor.lockState = CursorLockMode.Locked; // ロック状態にリセット
        Cursor.visible = false;
        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>
    /// ボタンから呼び出す移動関数
    /// </summary>
    public void ResetGame()
    {
        Debug.Log("ゲームをリセットします...");
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None; // タイトルなら表示
        Cursor.visible = true;
        SceneManager.LoadScene(titleSceneName);
    }

    /// <summary>
    /// アプリを終了する関数（PC用）
    /// </summary>
    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("アプリケーションを終了します");
    }
}