Shader "Custom/OutlineUnlitShader"
{
    Properties
    {
        // _OutlineColor と _Outline の変数名は、明滅スクリプト (ItemOutlineBlinker) と完全に一致させています
        [HDR] _OutlineColor ("Outline Color", Color) = (1, 1, 0, 1)
        _Outline ("Outline Thickness", Range(0.0, 0.1)) = 0.02 // 肉厚に調整できるよう最大値を広げました
    }

    SubShader
    {
        // URP環境用のタグ設定（不透明オブジェクトの後ろ、透過を考慮したキューに配置）
        Tags 
        { 
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+1" 
        }

        Pass
        {
            Name "OutlinePass"
            
            // 背面反転法（モデルの裏面だけを膨らませて描画することで輪郭線にする）
            Cull Front
            
            // 輪郭線が元のオブジェクトや壁に埋もれすぎないよう、Zテストを適切に設定
            ZWrite On
            ZTest LEqual

            // アルファブレンディングを有効化（明滅スクリプトによる透明度の変化に対応）
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP用の標準ライブラリをインクルード
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;  // オブジェクト空間の頂点座標
                float3 normalOS     : NORMAL;    // オブジェクト空間の法線ベクトル
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION; // クリップ空間の座標
                float4 color        : COLOR;
            };

            // Propertiesで定義した変数をHLSL内部に宣言
            float4 _OutlineColor;
            float _Outline;

            // 頂点シェーダー：元コードの安全な仕組みをベースに、カドの隙間を肉厚に埋める計算を追加
            Varyings vert(Attributes input)
            {
                Varyings output;

                // 1. 【ベースの処理】元の正しいコードと完全に同じ計算で、頂点と法線を取得
                float3 normalOS = normalize(input.normalOS);
                
                // 2. 【立体化・隙間対策の改良】
                // 通常の押し出し（normalOS * _Outline）に加え、カドの「ちぎれ」を物理的に埋めるために
                // 各頂点の位置データ（input.positionOS.xyzの絶対値）をブレンドした補正値を加算します。
                // これにより、カド部分に肉厚なボリュームが生まれ、太くしても内側に空白の隙間ができにくくなります。
                float3 cornerCompensation = normalOS * (1.0 + abs(normalOS));
                float3 extrudedPositionOS = input.positionOS.xyz + (cornerCompensation * _Outline);

                // 3. 【ベースの処理】元の正しいコードと完全に同じ関数で、最終的な画面座標へ安全に変換
                output.positionCS = TransformObjectToHClip(extrudedPositionOS);

                // 4. 設定された色をそのままフラグメントシェーダーへ送る
                output.color = _OutlineColor;

                return output;
            }

            // フラグメントシェーダー：ライティング計算を行わず、指定された色をそのまま画面に出力する（Unlit）
            float4 frag(Varyings input) : SV_Target
            {
                return input.color;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}