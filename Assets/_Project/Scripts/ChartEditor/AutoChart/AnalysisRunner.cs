using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;

namespace RhythmCP.ChartEditing
{
    /// 파이썬 분석기(Tools/Analysis/analyze.py)를 별도 프로세스로 실행. 에디터 화면이 멈추지 않게 비동기로 돌리고,
    /// 표준출력 줄("STAGE …")을 모아 두었다가 메인 스레드가 Poll()로 가져간다(Unity API는 메인 스레드에서만).
    public class AnalysisRunner
    {
        readonly ConcurrentQueue<string> _lines = new ConcurrentQueue<string>();
        Process _process;
        string _stderr = "";

        public bool IsRunning => _process != null && !_process.HasExited;
        public string Stage { get; private set; } = "";

        /// 끝났으면 true + 성공 여부·오류 메시지. 실행 중이거나 시작 안 했으면 false.
        public bool TryFinish(out bool success, out string error)
        {
            success = false;
            error = null;
            if (_process == null || !_process.HasExited) return false;

            success = _process.ExitCode == 0;
            error = success ? null : (string.IsNullOrWhiteSpace(_stderr) ? $"종료 코드 {_process.ExitCode}" : LastLine(_stderr));
            _process.Dispose();
            _process = null;
            return true;
        }

        public void Start(string pythonCommand, string analyzerPath, string audioPath, string outPath, bool force)
        {
            if (IsRunning) return;
            _stderr = "";
            Stage = "start";
            var info = new ProcessStartInfo
            {
                FileName = pythonCommand,
                Arguments = $"\"{Path.GetFullPath(analyzerPath)}\" \"{Path.GetFullPath(audioPath)}\" \"{Path.GetFullPath(outPath)}\"" + (force ? " --force" : ""),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            // 분석기가 한글 경고를 출력한다 — 콘솔 기본 코드페이지(cp949)로 깨지지 않게 UTF-8 고정.
            info.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            info.StandardOutputEncoding = System.Text.Encoding.UTF8;
            info.StandardErrorEncoding = System.Text.Encoding.UTF8;

            _process = new Process { StartInfo = info };
            _process.OutputDataReceived += (_, e) => { if (e.Data != null) _lines.Enqueue(e.Data); };
            _process.ErrorDataReceived += (_, e) => { if (e.Data != null) _stderr += e.Data + "\n"; };
            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        /// 메인 스레드에서 매 프레임: 쌓인 출력 줄을 단계 표시로 반영.
        public void Poll()
        {
            while (_lines.TryDequeue(out var line))
            {
                if (line.StartsWith("STAGE ")) Stage = line.Substring(6);
                else if (line.StartsWith("CACHED")) Stage = "cached";
            }
        }

        static string LastLine(string text)
        {
            var lines = text.Trim().Split('\n');
            return lines[lines.Length - 1].Trim();
        }
    }
}
