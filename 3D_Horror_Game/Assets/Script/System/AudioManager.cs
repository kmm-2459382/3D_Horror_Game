using UnityEngine;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    // 敵の状態に応じたBGMの再生モード
    public enum BGMMode
    {
        Patrol,      // 通常の巡回時（巡回BGMが最大音量、追跡BGMは無音）
        AlertChase,  // プレイヤー発見・追跡時（状況に応じて即時最大化、または滑らかに音量上昇）
        LostTracking // 見失った後の足跡追跡時（追跡BGMの音量を0.5に下げる）
    }

    [Header("--- BGM音源設定 ---")]
    [SerializeField] private AudioClip patrolBGM; // 巡回時のBGM
    [SerializeField] private AudioClip chaseBGM;  // 追跡・突進時のBGM

    [Header("--- フェード設定 ---")]
    [SerializeField] private float fadeDuration = 1.5f;     // 通常フェードにかかる時間（秒）
    [SerializeField] private float reDiscoverFadeDuration = 0.5f; // 足跡追跡から再発見された時の素早いフェード時間（秒）
    [SerializeField] private float maxVolume = 0.5f;        // BGMの最大音量

    private AudioSource sourceA;
    private AudioSource sourceB;
    private BGMMode currentMode = BGMMode.Patrol;
    private Coroutine fadeCoroutine;

    // 💡【追加】EnemyAIなど、外部から現在のBGMモードを読み取るためのパブリックプロパティ
    public BGMMode CurrentMode => currentMode;

    private void Awake()
    {
        // ドアSEが分離されたため、BGM用の2つだけを動的に取得・追加
        AudioSource[] sources = GetComponents<AudioSource>();

        if (sources.Length >= 2)
        {
            sourceA = sources[0];
            sourceB = sources[1];
        }
        else
        {
            sourceA = gameObject.AddComponent<AudioSource>();
            sourceB = gameObject.AddComponent<AudioSource>();
        }

        ConfigureAudioSource(sourceA, patrolBGM);
        ConfigureAudioSource(sourceB, chaseBGM);

        // 最初は通常巡回BGMを最大音量で開始、追跡BGMは裏で無音ループ
        sourceA.volume = maxVolume;
        sourceA.Play();

        sourceB.volume = 0f;
        sourceB.Play();
    }

    private void ConfigureAudioSource(AudioSource source, AudioClip clip)
    {
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f; // BGMは常に2D（どこにいても均一に聞こえる）
    }

    /// <summary>
    /// 敵の状況に合わせてBGMのモードを切り替える
    /// </summary>
    public void ChangeBGMState(BGMMode targetMode)
    {
        if (currentMode == targetMode) return;

        BGMMode previousMode = currentMode;
        currentMode = targetMode;

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        if (currentMode == BGMMode.AlertChase)
        {
            if (previousMode == BGMMode.Patrol)
            {
                sourceA.volume = 0f;
                sourceB.time = 0f;
                sourceB.volume = maxVolume;

                if (!sourceB.isPlaying)
                {
                    sourceB.Play();
                }
            }
            else if (previousMode == BGMMode.LostTracking)
            {
                fadeCoroutine = StartCoroutine(ApplyBGMFade(currentMode, reDiscoverFadeDuration));
            }
        }
        else
        {
            fadeCoroutine = StartCoroutine(ApplyBGMFade(currentMode, fadeDuration));
        }
    }

    private IEnumerator ApplyBGMFade(BGMMode mode, float duration)
    {
        float targetA = 0f;
        float targetB = 0f;

        if (mode == BGMMode.Patrol)
        {
            targetA = maxVolume;
            targetB = 0f;
        }
        else if (mode == BGMMode.LostTracking)
        {
            targetA = 0f;
            targetB = maxVolume * 0.5f;
        }
        else if (mode == BGMMode.AlertChase)
        {
            targetA = 0f;
            targetB = maxVolume;
        }

        float startA = sourceA.volume;
        float startB = sourceB.volume;

        if (mode == BGMMode.Patrol && !sourceA.isPlaying) sourceA.Play();
        if (mode == BGMMode.AlertChase && !sourceB.isPlaying) sourceB.Play();

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float percent = timer / duration;

            sourceA.volume = Mathf.Lerp(startA, targetA, percent);
            sourceB.volume = Mathf.Lerp(startB, targetB, percent);
            yield return null;
        }

        sourceA.volume = targetA;
        sourceB.volume = targetB;
    }
}