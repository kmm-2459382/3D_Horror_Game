using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [Header("--- 所持している鍵のIDリスト ---")]
    [Tooltip("ゲーム中にプレイヤーが拾った鍵のID（文字列）がここに蓄積されます")]
    [SerializeField] private List<string> possessedKeyIds = new List<string>();

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
}