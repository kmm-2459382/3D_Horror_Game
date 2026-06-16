using System.Collections.Generic;
using UnityEngine;

public class FlickeringLightGroups : MonoBehaviour
{
    // インスペクターで2重のリストを表示するためのグループクラス
    [System.Serializable]
    public class LightGroup
    {
        public string groupName = "新グループ"; // 管理用の名前
        [Tooltip("このグループに所属するライトのリスト")]
        public List<Light> lights = new List<Light>();

        // グループごとに保持する内部データ
        [HideInInspector] public List<float> maxIntensities = new List<float>();
        [HideInInspector] public float timeOffset;
    }

    [Header("グループ設定")]
    [Tooltip("ライトのグループリスト")]
    public List<LightGroup> lightGroups = new List<LightGroup>();

    [Header("明滅の基本設定")]
    [Tooltip("チカチカした時の最小輝度の割合（0.0で完全消灯、1.0で変化なし）")]
    [Range(0f, 1f)]
    public float minIntensityRatio = 0.1f;

    [Header("スピードとランダム性")]
    [Tooltip("明滅の速さ（小さいほど高速、大きいほどゆっくり）")]
    public float flickerSpeed = 0.05f;
    [Tooltip("グループごとのタイミングのズレ幅")]
    public float groupOffsetRange = 100f;

    private float timer = 0f;

    void Start()
    {
        if (lightGroups == null || lightGroups.Count == 0) return;

        // ゲーム開始時に各グループの初期設定を行う
        foreach (var group in lightGroups)
        {
            // グループごとに固有のランダムなオフセット（周期のズレ）を設定
            group.timeOffset = Random.Range(0f, groupOffsetRange);

            // グループ内の各ライトの「最初の輝度（最大値）」を記憶
            foreach (var light in group.lights)
            {
                if (light != null)
                {
                    group.maxIntensities.Add(light.intensity);
                }
                else
                {
                    group.maxIntensities.Add(0f); // ヌルチェック用
                }
            }
        }
    }

    void Update()
    {
        if (lightGroups == null || lightGroups.Count == 0) return;

        timer += Time.deltaTime;

        // タイマーが設定したスピードに達したら輝度を更新
        if (timer >= flickerSpeed)
        {
            // ① まずグループごとにループ
            foreach (var group in lightGroups)
            {
                // グループ共通のノイズ値を計算（同じグループなら同じ値になる）
                float noiseInput = (Time.time / flickerSpeed) + group.timeOffset;
                float noise = Mathf.PerlinNoise(noiseInput, 0f);

                // ② グループ内のライトを一斉に更新
                for (int i = 0; i < group.lights.Count; i++)
                {
                    if (group.lights[i] == null) continue;

                    float max = group.maxIntensities[i];
                    float min = max * minIntensityRatio;

                    // 同じグループ内のライトは、同じ「noise」を使って同時に明滅する
                    group.lights[i].intensity = Mathf.Lerp(min, max, noise);
                }
            }

            timer = 0f; // タイマーリセット
        }
    }
}