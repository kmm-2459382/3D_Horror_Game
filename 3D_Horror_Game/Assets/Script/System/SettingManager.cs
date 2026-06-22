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
        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>
    /// ボタンから呼び出す移動関数
    /// </summary>
    public void ResetGame()
    {
        Debug.Log("ゲームをリセットします...");
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