using UnityEngine;
using System.Collections.Generic;

public class ItemMultiOutlineBlinker : MonoBehaviour
{
    [Header("--- アウトライン明滅の設定 ---")]
    [Tooltip("明滅のスピード。数値が大きいほど素早く点滅します")]
    [SerializeField] private float blinkSpeed = 2.0f;

    [Tooltip("Trueにすると、Rendererに登録されているすべてのマテリアルを対象にします。\nFalseの場合、下のアウトライン対象インデックスリストで指定したものだけを対象にします")]
    [SerializeField] private bool targetAllMaterials = true;

    [Tooltip("特定のインデックス番号だけを操作したい場合は、ここに番号を追加してください（例: 0, 2 など）\n※上記の『Target All Materials』がFalseの時のみ有効です")]
    [SerializeField] private List<int> targetMaterialIndices = new List<int> { 0 };

    [Tooltip("操作するシェーダーのプロパティ名。使用するアウトラインシェーダーに合わせて書き換えてください。\n（例: _Outline, _Thickness, _OutlineWidth, _Color）")]
    [SerializeField] private string thicknessPropertyName = "_Outline";

    [Tooltip("アウトラインの最小の太さ（または透明度）")]
    [SerializeField] private float minThickness = 0.0f;

    [Tooltip("アウトラインの最大の太さ（最も目立つ瞬間）")]
    [SerializeField] private float maxThickness = 0.02f;

    [Header("--- 色の明滅も同時に行う場合（任意） ---")]
    [Tooltip("Trueにすると、太さだけでなくアウトラインの色（透明度）も同期して変化させます")]
    [SerializeField] private bool controlColorAlpha = true;

    [Tooltip("色のプロパティ名（標準は _OutlineColor や _Color）")]
    [SerializeField] private string colorPropertyName = "_OutlineColor";

    private Renderer targetRenderer;
    private MaterialPropertyBlock propertyBlock;

    // 各マテリアルの初期色を保存するためのリスト
    private Dictionary<int, Color> originalOutlineColors = new Dictionary<int, Color>();

    // シェーダープロパティのIDキャッシュ
    private int thicknessPropID;
    private int colorPropID;

    // 有効なインデックスのみを保持するリスト
    private List<int> validIndices = new List<int>();

    private void Start()
    {
        targetRenderer = GetComponentInChildren<Renderer>();
        propertyBlock = new MaterialPropertyBlock();

        // 文字列IDのキャッシュ
        thicknessPropID = Shader.PropertyToID(thicknessPropertyName);
        colorPropID = Shader.PropertyToID(colorPropertyName);

        if (targetRenderer != null && targetRenderer.sharedMaterials.Length > 0)
        {
            int matCount = targetRenderer.sharedMaterials.Length;

            // 対象とするインデックスの確定
            if (targetAllMaterials)
            {
                for (int i = 0; i < matCount; i++) validIndices.Add(i);
            }
            else
            {
                foreach (int index in targetMaterialIndices)
                {
                    if (index >= 0 && index < matCount)
                    {
                        validIndices.Add(index);
                    }
                }
            }

            // 各マテリアルのプロパティ存在チェックと初期色取得
            foreach (int index in validIndices)
            {
                Material mat = targetRenderer.sharedMaterials[index];
                if (mat != null)
                {
                    if (controlColorAlpha && mat.HasProperty(colorPropID))
                    {
                        originalOutlineColors[index] = mat.GetColor(colorPropID);
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning($"【ItemMultiOutlineBlinker】[{gameObject.name}] にマテリアルが登録されていないか、Rendererが見つかりません。処理をスキップします。");
            enabled = false;
        }
    }

    private void Update()
    {
        if (targetRenderer == null || validIndices.Count == 0) return;

        // サイン波を利用して、0.0 〜 1.0 の周期（t）を作る
        float lerpFactor = (Mathf.Sin(Time.time * blinkSpeed) + 1.0f) / 2.0f;

        // 1. 線の太さと色を計算
        float currentThickness = Mathf.Lerp(minThickness, maxThickness, lerpFactor);

        // 2. 登録されているすべての対象マテリアル（インデックス）に適用
        foreach (int index in validIndices)
        {
            // 現在のインデックスに対応するPropertyBlockの状態を取得
            targetRenderer.GetPropertyBlock(propertyBlock, index);

            // 太さの設定
            Material mat = targetRenderer.sharedMaterials[index];
            if (mat != null && mat.HasProperty(thicknessPropID))
            {
                propertyBlock.SetFloat(thicknessPropID, currentThickness);
            }

            // 色（透明度）の設定
            if (controlColorAlpha && originalOutlineColors.ContainsKey(index))
            {
                Color targetColor = originalOutlineColors[index];
                targetColor.a = Mathf.Lerp(0.1f, 1.0f, lerpFactor);
                propertyBlock.SetColor(colorPropID, targetColor);
            }

            // 変更したプロパティをそのインデックスに適用
            targetRenderer.SetPropertyBlock(propertyBlock, index);
        }
    }
}