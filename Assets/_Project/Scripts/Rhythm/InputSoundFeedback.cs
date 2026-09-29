using RhythmCP.Chart;
using UnityEngine;

namespace RhythmCP.Rhythm
{
    /// 누를 때마다 판정과 무관하게 타격음 1개. 귀로 타이밍을 확인하는 용도(① 범위).
    /// 판정별 사운드·이펙트는 ⑥ juice에서 JudgementSystem.Judged를 받는 쪽으로 따로 만든다.
    public class InputSoundFeedback : MonoBehaviour
    {
        [SerializeField] RhythmInput _input;
        [SerializeField] AudioSource _source;
        [SerializeField] AudioClip _hitClip;

        void OnEnable() => _input.LanePressed += OnPressed;
        void OnDisable() => _input.LanePressed -= OnPressed;

        // PlayOneShot: 연타에서 이전 소리를 끊지 않고 겹쳐 재생하려고.
        void OnPressed(Lane lane, double realtime) => _source.PlayOneShot(_hitClip);
    }
}
