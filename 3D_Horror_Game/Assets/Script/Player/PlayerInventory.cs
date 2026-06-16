using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [Header("--- 所持している鍵のIDリスト ---")]
    [Tooltip("ゲーム中にプレイヤーが拾った鍵のID（文字列）がここに蓄積されます")]
    [SerializeField] private List<string> possessedKeyIds = new List<string>();

    [Header("--- その他の所持アイテム状態 ---")]
    [Tooltip("懐中電灯を所持しているかどうか")]
    [SerializeField] private bool hasFlashlight = false;

    /// <summary>
    /// 新しく鍵を拾い上げた時に呼び出す関数
    /// </summary>
    /// <param name="keyId">入手した鍵の固有ID</param>
    public void AddKey(string keyId)
    {
        if (!possessedKeyIds.Contains(keyId))
        {
            possessedKeyIds.Add(keyId);
            Debug.Log($"インベントリに鍵を追加: {keyId}");
        }
    }

    /// <summary>
    /// 特定の鍵を持っているかどうかをチェックする関数
    /// </summary>
    /// <param name="keyId">調べたい鍵の固有ID</param>
    /// <returns>持っていればtrue</returns>
    public bool HasKey(string keyId)
    {
        return possessedKeyIds.Contains(keyId);
    }

    /// <summary>
    /// 使用した鍵を消費して手元からなくしたい場合に呼び出す関数
    /// </summary>
    /// <param name="keyId">削除する鍵の固有ID</param>
    public void RemoveKey(string keyId)
    {
        if (possessedKeyIds.Contains(keyId))
        {
            possessedKeyIds.Remove(keyId);
            Debug.Log($"インベントリから鍵を削除: {keyId}");
        }
    }

    // =================================================================
    // 💡 懐中電灯用の拡張機能
    // =================================================================

    /// <summary>
    /// 懐中電灯の所持状態を設定する関数
    /// </summary>
    public void SetFlashlightPossession(bool state)
    {
        hasFlashlight = state;
        Debug.Log($"インベントリの懐中電灯所持状態を更新: {state}");
    }

    /// <summary>
    /// 懐中電灯を持っているかどうかをチェックする関数
    /// </summary>
    public bool HasFlashlight()
    {
        return hasFlashlight;
    }
}