using UnityEngine;
using UnityEngine.SceneManagement; // シーン操作に必須

public class SceneChanger : MonoBehaviour
{
    [Header("--- 設定 ---")]
    [Tooltip("移動先のシーン名を正確に入力してください")]
    [SerializeField] private string targetSceneName = "title";

    [Tooltip("プレイヤーオブジェクトに設定してあるタグ名")]
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        // 衝突した相手がプレイヤーのタグを持っているか確認
        if (other.CompareTag(playerTag))
        {
            Debug.Log($"{targetSceneName} へシーン移動します。");

            // シーンの読み込み
            SceneManager.LoadScene(targetSceneName);
        }
    }
}