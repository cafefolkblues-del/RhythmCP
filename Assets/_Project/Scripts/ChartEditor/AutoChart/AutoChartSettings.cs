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

        [Tooltip("EASY 패턴 라이브러리(JSON). 에디터의 '패턴으로 저장'이 이 파일에 덧붙인다.")]
        [SerializeField] string _easyPatternsPath = "Assets/_Project/Data/AutoChart/patterns_easy.json";

        public AutoChartParams Params => _params;
        public string PythonCommand => _pythonCommand;
        public string AnalyzerPath => _analyzerPath;
        public string EasyPatternsPath => _easyPatternsPath;
    }
}
