using UnityEngine;
using UnityEngine.SceneManagement;

public class CreditScroller : MonoBehaviour
{
    [Header("Scroll Settings")]
    [Tooltip("クレジットが流れるスピード")]
    [SerializeField] private float scrollSpeed = 50f;

    [Tooltip("クレジットが流れ終わる（画面外に出る）までの限界Y座標")]
    [SerializeField] private float finishYPosition = 1000f;

    [Tooltip("クレジット終了後に戻るシーン名")]
    [SerializeField] private string titleSceneName = "TitleScene";

    private RectTransform rectTransform;
    private bool isFinished = false;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    void Update()
    {
        if (isFinished) return;

        // 上方向へテキストを移動させる
        rectTransform.anchoredPosition += Vector2.up * scrollSpeed * Time.deltaTime;

        // 指定した位置を超えたら終了処理へ
        if (rectTransform.anchoredPosition.y >= finishYPosition)
        {
            isFinished = true;
            ReturnToTitle();
        }

        // キーボードの Esc キーなどでスキップできるようにする場合
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ReturnToTitle();
        }
    }

    private void ReturnToTitle()
    {
        SceneManager.LoadScene(titleSceneName);
    }
}