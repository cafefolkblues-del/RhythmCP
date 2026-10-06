using UnityEngine;

namespace RhythmCP.ChartEditing
{
    /// 자동 채보 설정 에셋: 배치 튜닝값 + 분석기 실행 경로.
    [CreateAssetMenu(menuName = "RhythmCP/Auto Chart Settings", fileName = "AutoChartSettings")]
    public class AutoChartSettings : ScriptableObject
    {
        [SerializeField] AutoChartParams _params = new AutoChartParams();

        [Tooltip("분석기 실행 명령. 기본 py(파이썬 런처).")]
        [SerializeField] string _pythonCommand = "py";

        [Tooltip("프로젝트 루트 기준 분석기 경로.")]
        [SerializeField] string _analyzerPath = "Tools/Analysis/analyze.py";

        public AutoChartParams Params => _params;
        public string PythonCommand => _pythonCommand;
        public string AnalyzerPath => _analyzerPath;
    }
}
