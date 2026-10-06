using System;
using System.Collections.Generic;

namespace RhythmCP.Vn
{
    [Serializable]
    public class VnActor
    {
        public string id;
        public string expr;
        public string pos;
    }

    /// 무대 상태 = 배경·CG·BGM·올라와 있는 캐릭터. 세이브에 그대로 들어가고, 중간부터 재생할 때 다시 만든다.
    [Serializable]
    public class VnStageState
    {
        public string bg;
        public string cg;
        public string bgm;
        public List<VnActor> actors = new List<VnActor>();

        public VnActor Actor(string id) => actors.Find(a => a.id == id);
    }

    public enum VnSpeakerKind
    {
        Character,
        Mc,
        Narration,
    }

    /// 화면에 띄울 라인 한 줄(이름표·텍스트·연출). 뷰는 이것만 보고 그린다.
    public class VnShownLine
    {
        public int Index;
        public VnLine Line;
        public VnSpeakerKind Kind;

        /// 캐릭터 = 표시 이름, mc = 호칭, 나레이션 = null.
        public string Name;
        public string SpeakerId;
        public bool WasRead;

        /// 이 라인에서 배경이 바뀌었으면 전환 방식("cut"/"fade"), 아니면 null.
        public string BgTransition;
        public bool CgChanged;
        public bool BgmChanged;
        public List<string> Exited = new List<string>();
    }

    public class VnBacklogEntry
    {
        public string Name;
        public string Text;
        public VnSpeakerKind Kind;
    }

    public interface IVnReadLog
    {
        bool IsRead(string episodeId, string lineId);
        void MarkRead(string episodeId, string lineId);
    }

    /// VN 진행(순수 로직). 조건이 안 맞는 라인은 건너뛰고, 무대 변경을 계산하고, 선택지 플래그를 반영한다.
    /// 화면을 모른다 — 플레이 씬과 에디터 프리뷰가 같은 진행 규칙을 쓰게 하려고 분리.
    public class VnPlayer
    {
        public event Action<VnShownLine> LineShown;
        public event Action Finished;

        readonly Func<string, string> _displayName;
        readonly IVnReadLog _readLog;
        string _lastCharacter;

        public VnEpisode Episode { get; }
        public VnFlags Flags { get; private set; }
        public VnStageState Stage { get; private set; }
        public VnShownLine Current { get; private set; }
        public List<VnBacklogEntry> Backlog { get; } = new List<VnBacklogEntry>();
        public bool Ended { get; private set; }
        public bool AwaitingChoice => Current != null && Current.Line.IsChoice && !_chosen;

        bool _chosen;

        /// displayName: 캐릭터 id → 표시 이름(카탈로그). readLog: 기읽 기록(없으면 null).
        public VnPlayer(VnEpisode episode, VnFlags flags, Func<string, string> displayName, IVnReadLog readLog)
        {
            Episode = episode;
            Flags = flags ?? new VnFlags();
            _displayName = displayName ?? (id => id);
            _readLog = readLog;
        }

        /// startIndex부터 재생. stage를 주면(세이브 불러오기) 그대로, 없으면 앞 라인들의 무대 지시를 다시 쌓는다.
        public void Begin(int startIndex = 0, VnStageState stage = null)
        {
            Ended = false;
            Stage = stage ?? StageAt(Episode, startIndex, Flags);
            _lastCharacter = LastCharacterBefore(startIndex);
            Current = null;
            ShowFrom(startIndex);
        }

        /// 다음 라인으로. 선택지에서 아직 안 골랐으면 진행하지 않는다.
        public bool Advance()
        {
            if (Ended || AwaitingChoice) return false;
            ShowFrom(Current == null ? 0 : Current.Index + 1);
            return !Ended;
        }

        public void Choose(int index)
        {
            if (!AwaitingChoice) return;
            var choice = Current.Line.choice[index];
            Flags.Apply(choice);
            _chosen = true;
            Backlog.Add(new VnBacklogEntry { Name = Current.Name, Text = "▶ " + choice.text, Kind = VnSpeakerKind.Mc });
        }

        public bool IsVisible(int index) => VnCondition.Evaluate(Episode.lines[index].condition, Flags);

        void ShowFrom(int index)
        {
            while (index < Episode.lines.Count && !IsVisible(index)) index++;
            if (index >= Episode.lines.Count)
            {
                Ended = true;
                Finished?.Invoke();
                return;
            }

            var line = Episode.lines[index];
            var shown = new VnShownLine { Index = index, Line = line, SpeakerId = line.speaker };
            ApplyStage(Stage, line, shown);

            if (line.speaker == VnIds.Narration || string.IsNullOrEmpty(line.speaker))
                shown.Kind = VnSpeakerKind.Narration;
            else if (line.speaker == VnIds.Mc)
            {
                shown.Kind = VnSpeakerKind.Mc;
                string whose = string.IsNullOrEmpty(line.asCharacter) ? _lastCharacter : line.asCharacter;
                shown.Name = (whose != null ? Episode.HonorificOf(whose) : null) ?? "???";
            }
            else
            {
                shown.Kind = VnSpeakerKind.Character;
                shown.Name = _displayName(line.speaker);
                _lastCharacter = line.speaker;
            }

            string ep = Episode.meta.id;
            shown.WasRead = _readLog != null && _readLog.IsRead(ep, line.id);
            _readLog?.MarkRead(ep, line.id);

            Current = shown;
            _chosen = false;
            Backlog.Add(new VnBacklogEntry { Name = shown.Kind == VnSpeakerKind.Narration ? null : shown.Name, Text = line.text, Kind = shown.Kind });
            LineShown?.Invoke(shown);
        }

        string LastCharacterBefore(int index)
        {
            for (int i = Math.Min(index, Episode.lines.Count) - 1; i >= 0; i--)
            {
                var s = Episode.lines[i].speaker;
                if (s != VnIds.Mc && s != VnIds.Narration && !string.IsNullOrEmpty(s) && IsVisible(i)) return s;
            }
            return null;
        }

        /// index 직전까지 보이는 라인들의 무대 지시를 쌓은 상태. 선택지 효과는 반영하지 않는다(현재 플래그 기준).
        public static VnStageState StageAt(VnEpisode episode, int index, VnFlags flags)
        {
            var stage = new VnStageState();
            for (int i = 0; i < index && i < episode.lines.Count; i++)
                if (VnCondition.Evaluate(episode.lines[i].condition, flags))
                    ApplyStage(stage, episode.lines[i], null);
            return stage;
        }

        /// 라인 한 줄의 무대 지시 반영. shown이 있으면 무엇이 바뀌었는지 기록(뷰가 전환 연출을 고른다).
        public static void ApplyStage(VnStageState stage, VnLine line, VnShownLine shown)
        {
            if (!string.IsNullOrEmpty(line.bg))
            {
                int dot = line.bg.LastIndexOf('.');
                string id = dot > 0 ? line.bg.Substring(0, dot) : line.bg;
                string transition = dot > 0 ? line.bg.Substring(dot + 1) : "cut";
                if (id != stage.bg)
                {
                    stage.bg = id;
                    if (shown != null) shown.BgTransition = transition;
                }
            }

            if (!string.IsNullOrEmpty(line.cg))
            {
                string cg = line.cg == VnIds.Off ? null : line.cg;
                if (cg != stage.cg) { stage.cg = cg; if (shown != null) shown.CgChanged = true; }
            }

            if (!string.IsNullOrEmpty(line.bgm))
            {
                string bgm = line.bgm == VnIds.Stop ? null : line.bgm;
                if (bgm != stage.bgm) { stage.bgm = bgm; if (shown != null) shown.BgmChanged = true; }
            }

            if (line.exit != null)
                foreach (var id in line.exit)
                    if (stage.actors.RemoveAll(a => a.id == id) > 0) shown?.Exited.Add(id);

            string speaker = line.speaker;
            if (string.IsNullOrEmpty(speaker) || speaker == VnIds.Mc || speaker == VnIds.Narration) return;

            var actor = stage.Actor(speaker);
            if (!string.IsNullOrEmpty(line.pos))
            {
                // 같은 자리에 다른 캐릭터가 있으면 그 캐릭터는 내려간다.
                foreach (var other in stage.actors.FindAll(a => a.pos == line.pos && a.id != speaker))
                {
                    stage.actors.Remove(other);
                    shown?.Exited.Add(other.id);
                }
                if (actor == null)
                {
                    actor = new VnActor { id = speaker };
                    stage.actors.Add(actor);
                }
                actor.pos = line.pos;
            }
            if (actor != null && !string.IsNullOrEmpty(line.expr)) actor.expr = line.expr;
        }
    }
}
