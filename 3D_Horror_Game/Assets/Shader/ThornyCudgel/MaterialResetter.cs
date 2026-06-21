#if UNITY_EDITOR // エディタ実行時のみ有効にする
using UnityEngine;
using UnityEditor;

public class MaterialResetter : Editor
{
    [MenuItem("Tools/Reset Selected Materials Keywords")]
    public static void ResetKeywords()
    {
        foreach (Object obj in Selection.objects)
        {
            if (obj is Material mat)
            {
                foreach (string keyword in mat.shaderKeywords)
                {
                    mat.DisableKeyword(keyword);
                }
                EditorUtility.SetDirty(mat);
                Debug.Log($"{mat.name} のキーワードをリセットしました");
            }
        }
    }
}
#endif // ここまで