using System.Collections.Generic;
using UnityEngine;

public class PushButton : MonoBehaviour
{
    [Header("--- 移動・操作対象の設定 ---")]
    public Transform targetButton;
    public List<Transform> movePoints = new List<Transform>();
    public float moveLerpSpeed = 5.0f;

    [Header("--- 連動するオブジェクト ---")]
    public List<GameObject> targetObjects = new List<GameObject>();

    [Header("--- パズル管理用 ---")]
    public PuzzleDoorManager puzzleManager;

    // 💡 インスペクターでの確認用 (readonly的な扱い)
    [Header("--- 現在の状態 (編集不可) ---")]
    [SerializeField] private bool _isActivated = false;
    public bool isActivated => _isActivated;

    private int currentIndex = 0;

    void Update()
    {
        if (targetButton != null && movePoints.Count > 0 && movePoints[currentIndex] != null)
        {
            targetButton.position = Vector3.Lerp(targetButton.position, movePoints[currentIndex].position, moveLerpSpeed * Time.deltaTime);
        }
    }

    public void OnInteract()
    {
        if (movePoints.Count > 0)
        {
            currentIndex = (currentIndex + 1) % movePoints.Count;
        }

        _isActivated = !_isActivated;

        foreach (var obj in targetObjects)
        {
            if (obj != null) obj.SetActive(_isActivated);
        }

        if (puzzleManager != null)
        {
            puzzleManager.NotifyButtonStateChanged();
        }
    }
}