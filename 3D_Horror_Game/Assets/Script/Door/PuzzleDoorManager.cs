using System.Collections.Generic;
using UnityEngine;

public class PuzzleDoorManager : MonoBehaviour
{
    [Tooltip("パズルに含まれるすべてのボタン")]
    public List<PushButton> buttons = new List<PushButton>();

    [Header("--- アンロック対象（DoorController内のdoorIdと一致させること） ---")]
    public DoorController doorController;
    public List<string> targetDoorIds = new List<string>();

    public void NotifyButtonStateChanged()
    {
        CheckPuzzleCompletion();
    }

    private void CheckPuzzleCompletion()
    {
        // すべてのボタンがONかチェック
        bool allActive = true;
        foreach (var btn in buttons)
        {
            if (btn == null || !btn.isActivated)
            {
                allActive = false;
                break;
            }
        }

        SetDoorLocks(allActive);
    }

    private void SetDoorLocks(bool shouldUnlock)
    {
        if (doorController == null)
        {
            Debug.LogError("【エラー】PuzzleDoorManager に DoorController が設定されていません！");
            return;
        }

        bool changed = false;
        foreach (var id in targetDoorIds)
        {
            foreach (var room in doorController.roomDoorsList)
            {
                foreach (var group in room.doorGroups)
                {
                    if (group.doorId == id)
                    {
                        // ロック状態を反転させる値（shouldUnlockがtrueなら、isLockedはfalseにする）
                        bool targetLockState = !shouldUnlock;

                        // 現在の状態と異なる場合のみ更新
                        if (group.isLocked != targetLockState)
                        {
                            // 💡 ここで DoorController のメソッドを呼ぶ
                            doorController.UpdateLockState(group, targetLockState);
                            changed = true;
                        }
                    }
                }
            }
        }

        if (changed)
        {
            string msg = shouldUnlock ? "扉のロックが解除されました" : "扉がロックされました";
            Color col = shouldUnlock ? Color.green : Color.red;

            if (GameMessageUI.Instance != null)
            {
                GameMessageUI.Instance.ShowMessage(msg, col);
            }
        }
    }
}