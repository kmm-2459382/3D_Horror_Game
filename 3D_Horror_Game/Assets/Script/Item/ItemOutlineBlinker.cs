using UnityEngine;

public class ItemOutlineBlinker : MonoBehaviour
{
    [Header("--- アウトライン明滅の設定 ---")]
    [Tooltip("明滅のスピード。数値が大きいほど素早く点滅します")]
    [SerializeField] private float blinkSpeed = 2.0f;

    [Tooltip("アウトラインマテリアルのインデックス番号。通常、元モデルの素材の『次（1番以降）』に追加します")]
    [SerializeField] private int outlineMaterialIndex = 1;

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
    private Material outlineMaterial;
    private Color originalOutlineColor;

    private void Start()
    {
        // 自身、または子オブジェクトからRenderer（MeshRendererなど）を取得
        targetRenderer = GetComponentInChildren<Renderer>();

        if (targetRenderer != null && targetRenderer.materials.Length > outlineMaterialIndex)
        {
            // 特定のインデックスのアウトライン用マテリアルにアクセス（動的複製）
            outlineMaterial = targetRenderer.materials[outlineMaterialIndex];

            if (controlColorAlpha && outlineMaterial.HasProperty(colorPropertyName))
            {
                originalOutlineColor = outlineMaterial.GetColor(colorPropertyName);
            }
        }
        else
        {
            Debug.LogWarning($"【ItemOutlineBlinker】[{gameObject.name}] に十分な数のマテリアルが登録されていないか、Rendererが見つかりません。アウトラインの明滅をスキップします。");
            enabled = false; // 処理を自動で無効化
        }
    }

    private void Update()
    {
        if (outlineMaterial == null) return;

        // サイン波を利用して、0.0 〜 1.0 の周期（t）を作る
        float lerpFactor = (Mathf.Sin(Time.time * blinkSpeed) + 1.0f) / 2.0f;

        // 1. 線の太さ（Thickness）を滑らかに往復させる
        if (outlineMaterial.HasProperty(thicknessPropertyName))
        {
            float currentThickness = Mathf.Lerp(minThickness, maxThickness, lerpFactor);
            outlineMaterial.SetFloat(thicknessPropertyName, currentThickness);
        }

        // 2. 線の透明度（Alpha）も同時に滑らかに往復させる（よりボウッと光る表現になります）
        if (controlColorAlpha && outlineMaterial.HasProperty(colorPropertyName))
        {
            Color targetColor = originalOutlineColor;
            // アルファ値（透明度）を 0.1(うっすら) 〜 1.0(くっきり) の間でブレンド
            targetColor.a = Mathf.Lerp(0.1f, 1.0f, lerpFactor);
            outlineMaterial.SetColor(colorPropertyName, targetColor);
        }
    }

    private void OnDestroy()
    {
        // メモリリーク防止のため、オブジェクト消滅時に複製されたマテリアルを破棄
        if (outlineMaterial != null)
        {
            Destroy(outlineMaterial);
        }
    }
}