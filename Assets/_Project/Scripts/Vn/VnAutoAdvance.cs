using System;

namespace RhythmCP.Vn
{
    public enum VnAdvanceMode
    {
        Manual,

        /// 다 찍히고 (기본 + 글자당) 초 뒤 자동 진행. 선택지에서 기다렸다가 고르면 계속.
        Auto,

        /// 읽은 라인만 빠르게 넘김. 안 읽은 라인·선택지에서 멈추고 꺼진다.
        ReadSkip,
    }

    /// 오토·기읽 스킵·Ctrl 빨리감기 타이밍(순수). 세션이 매 프레임 Tick을 부르고 true면 한 줄 진행한다.
    /// "스킵" 버튼은 여기 없다 — 확인 팝업 뒤 다음 선택지까지 한 번에 건너뛰는 방식(VnPlayer.SkipToNextStop, 2026-10-07 플레이테스트 후 변경).
    public class VnAutoAdvance
    {
        public event Action<VnAdvanceMode> ModeChanged;

        readonly float _autoBaseSec, _autoPerCharSec, _skipIntervalSec;
        VnAdvanceMode _mode;
        float _wait;
        int _chars;

        public VnAdvanceMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value) return;
                _mode = value;
                _wait = 0f;
                ModeChanged?.Invoke(value);
            }
        }

        public bool IsSkipping => _mode == VnAdvanceMode.ReadSkip;

        public VnAutoAdvance(float autoBaseSec, float autoPerCharSec, float skipIntervalSec)
        {
            _autoBaseSec = autoBaseSec;
            _autoPerCharSec = autoPerCharSec;
            _skipIntervalSec = skipIntervalSec;
        }

        /// 새 라인이 뜰 때. 기읽 스킵은 안 읽은 라인에서 여기서 꺼진다.
        public void OnLineShown(VnShownLine line, int charCount)
        {
            _wait = 0f;
            _chars = charCount;
            if (_mode == VnAdvanceMode.ReadSkip && !line.WasRead) Mode = VnAdvanceMode.Manual;
        }

        /// held = 빨리감기 키(Ctrl)를 누르는 중 — 모드와 상관없이 빠르게 넘김.
        public bool Tick(float dt, bool typing, bool awaitingChoice, bool held)
        {
            if (awaitingChoice)
            {
                if (IsSkipping) Mode = VnAdvanceMode.Manual;
                return false;
            }

            if (held || IsSkipping)
            {
                _wait += dt;
                if (_wait < _skipIntervalSec) return false;
                _wait = 0f;
                return true;
            }

            if (_mode != VnAdvanceMode.Auto || typing) return false;
            _wait += dt;
            return _wait >= _autoBaseSec + _autoPerCharSec * _chars;
        }
    }
}
