using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RhythmCP.EditorTools
{
    /// 씬을 훑어 '게임을 돌려야만 알 수 있는 문제'를 미리 잡아주는 창.
    /// 진단만 하고 고치지는 않는다 — 자동 수정이 의도치 않은 변경을 만드는 게 더 위험하다는 판단.
    ///
    /// 검사 항목은 ISceneCheck 구현을 리플렉션으로 모으기 때문에,
    /// 새 검사를 추가하려면 클래스 하나만 더 만들면 된다(이 파일은 안 고쳐도 됨).
    public class SceneInspectorWindow : EditorWindow
    {
        readonly List<ISceneCheck> _checks = new List<ISceneCheck>();
        readonly List<SceneIssue> _issues = new List<SceneIssue>();
        readonly HashSet<string> _mutedCategories = new HashSet<string>();

        [SerializeField] bool _showErrors = true;
        [SerializeField] bool _showWarnings = true;
        [SerializeField] bool _showInfos = true;

        Vector2 _scroll;
        bool _hasRun;

        [MenuItem("Tools/RhythmCP/씬 검사")]
        static void Open() => GetWindow<SceneInspectorWindow>("씬 검사");

        void OnEnable() => CollectChecks();

        void CollectChecks()
        {
            _checks.Clear();

            foreach (Type type in TypeCache.GetTypesDerivedFrom<ISceneCheck>())
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (Activator.CreateInstance(type) is ISceneCheck check) _checks.Add(check);
            }

            _checks.Sort((a, b) => string.CompareOrdinal(a.Category, b.Category));
        }

        void OnGUI()
        {
            DrawToolbar();

            if (!_hasRun)
            {
                EditorGUILayout.HelpBox($"검사 항목 {_checks.Count}개가 준비됐습니다. '검사하기'를 눌러주세요.", MessageType.Info);
                return;
            }

            DrawSummary();
            DrawIssues();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUI.backgroundColor = new Color(0.45f, 1f, 0.6f);
            if (GUILayout.Button("검사하기", EditorStyles.toolbarButton, GUILayout.Width(80f))) RunChecks();
            GUI.backgroundColor = Color.white;

            _showErrors = GUILayout.Toggle(_showErrors, "오류", EditorStyles.toolbarButton, GUILayout.Width(50f));
            _showWarnings = GUILayout.Toggle(_showWarnings, "경고", EditorStyles.toolbarButton, GUILayout.Width(50f));
            _showInfos = GUILayout.Toggle(_showInfos, "정보", EditorStyles.toolbarButton, GUILayout.Width(50f));

            if (GUILayout.Button("분류", EditorStyles.toolbarDropDown, GUILayout.Width(50f))) ShowCategoryMenu();

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"검사 {_checks.Count}종", EditorStyles.miniLabel, GUILayout.Width(70f));

            EditorGUILayout.EndHorizontal();
        }

        void ShowCategoryMenu()
        {
            var menu = new GenericMenu();
            foreach (string category in _checks.Select(c => c.Category).Distinct())
            {
                string captured = category;
                menu.AddItem(new GUIContent(category), !_mutedCategories.Contains(category), () =>
                {
                    if (!_mutedCategories.Remove(captured)) _mutedCategories.Add(captured);
                    Repaint();
                });
            }
            menu.ShowAsContext();
        }

        void RunChecks()
        {
            _issues.Clear();
            SceneCheckContext context = SceneCheckContext.Build();

            foreach (ISceneCheck check in _checks)
            {
                try
                {
                    _issues.AddRange(check.Run(context));
                }
                catch (Exception exception)
                {
                    // 검사 하나가 터져도 나머지는 계속 돌아야 한다.
                    _issues.Add(new SceneIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = check.Category,
                        Title = $"검사 중 오류: {check.GetType().Name}",
                        Detail = exception.Message
                    });
                }
            }

            _issues.Sort((a, b) =>
            {
                int bySeverity = a.Severity.CompareTo(b.Severity);
                return bySeverity != 0 ? bySeverity : string.CompareOrdinal(a.Category, b.Category);
            });

            _hasRun = true;
        }

        void DrawSummary()
        {
            int errors = _issues.Count(i => i.Severity == IssueSeverity.Error);
            int warnings = _issues.Count(i => i.Severity == IssueSeverity.Warning);
            int infos = _issues.Count(i => i.Severity == IssueSeverity.Info);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            GUI.color = errors > 0 ? new Color(1f, 0.5f, 0.45f) : Color.white;
            EditorGUILayout.LabelField($"오류 {errors}", EditorStyles.boldLabel, GUILayout.Width(70f));
            GUI.color = warnings > 0 ? new Color(1f, 0.85f, 0.4f) : Color.white;
            EditorGUILayout.LabelField($"경고 {warnings}", EditorStyles.boldLabel, GUILayout.Width(70f));
            GUI.color = Color.white;
            EditorGUILayout.LabelField($"정보 {infos}", GUILayout.Width(70f));

            GUILayout.FlexibleSpace();
            if (errors == 0 && warnings == 0) EditorGUILayout.LabelField("문제 없음", EditorStyles.miniLabel, GUILayout.Width(80f));

            EditorGUILayout.EndHorizontal();
        }

        void DrawIssues()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            string lastCategory = null;

            foreach (SceneIssue issue in _issues)
            {
                if (!PassesFilter(issue)) continue;

                if (issue.Category != lastCategory)
                {
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField(issue.Category, EditorStyles.miniBoldLabel);
                    lastCategory = issue.Category;
                }

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                GUI.color = ColorFor(issue.Severity);
                EditorGUILayout.LabelField(IconFor(issue.Severity), GUILayout.Width(20f));
                GUI.color = Color.white;

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(issue.Title, EditorStyles.label);
                if (!string.IsNullOrEmpty(issue.Detail))
                    EditorGUILayout.LabelField(issue.Detail, EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();

                using (new EditorGUI.DisabledScope(issue.Target == null))
                {
                    if (GUILayout.Button("선택", GUILayout.Width(50f), GUILayout.Height(28f)))
                    {
                        Selection.activeObject = issue.Target;
                        EditorGUIUtility.PingObject(issue.Target);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        bool PassesFilter(SceneIssue issue)
        {
            if (_mutedCategories.Contains(issue.Category)) return false;

            switch (issue.Severity)
            {
                case IssueSeverity.Error: return _showErrors;
                case IssueSeverity.Warning: return _showWarnings;
                default: return _showInfos;
            }
        }

        static string IconFor(IssueSeverity severity)
        {
            switch (severity)
            {
                case IssueSeverity.Error: return "✕";
                case IssueSeverity.Warning: return "!";
                default: return "·";
            }
        }

        static Color ColorFor(IssueSeverity severity)
        {
            switch (severity)
            {
                case IssueSeverity.Error: return new Color(1f, 0.45f, 0.4f);
                case IssueSeverity.Warning: return new Color(1f, 0.85f, 0.4f);
                default: return new Color(0.7f, 0.75f, 0.8f);
            }
        }
    }
}
