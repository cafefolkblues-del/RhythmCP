using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace RhythmCP.EditorTools
{
    public enum IssueSeverity { Error, Warning, Info }

    public class SceneIssue
    {
        public IssueSeverity Severity;
        public string Category;
        public string Title;
        public string Detail;
        public UnityEngine.Object Target;
    }

    public class SceneCheckContext
    {
        public Scene Scene;
        public GameObject[] Roots;
        public MonoBehaviour[] OurComponents;

        public static SceneCheckContext Build()
        {
            Scene scene = SceneManager.GetActiveScene();

            var ours = new List<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour == null) continue;
                string ns = behaviour.GetType().Namespace;
                if (ns == null || !ns.StartsWith("RhythmCP")) continue;
                ours.Add(behaviour);
            }

            return new SceneCheckContext
            {
                Scene = scene,
                Roots = scene.GetRootGameObjects(),
                OurComponents = ours.ToArray()
            };
        }
    }

    /// 검사 하나. 새 검사를 추가하려면 이 인터페이스를 구현한 클래스를 하나 더 만들면 된다
    /// (창이 리플렉션으로 자동 수집한다).
    public interface ISceneCheck
    {
        string Category { get; }
        IEnumerable<SceneIssue> Run(SceneCheckContext context);
    }

    // ------------------------------------------------------------------ 배선

    /// 우리 컴포넌트(RhythmCP 네임스페이스)의 참조 슬롯이 비어 있는지 전수 검사.
    /// 게임 코드가 생기면 Critical / SelfHealing 에 "타입명._필드명" 을 채워 등급을 나눈다.
    public class ReferenceCheck : ISceneCheck
    {
        public string Category => "배선";

        /// Awake에서 스스로 찾아 채우는 필드들. 비어 있어도 동작하므로 '정보'로만 알린다.
        static readonly HashSet<string> SelfHealing = new HashSet<string>();

        /// 비면 그 오브젝트가 아예 기능을 못 하는 필드.
        static readonly HashSet<string> Critical = new HashSet<string>();

        public IEnumerable<SceneIssue> Run(SceneCheckContext context)
        {
            foreach (MonoBehaviour behaviour in context.OurComponents)
            {
                var serialized = new SerializedObject(behaviour);
                SerializedProperty property = serialized.GetIterator();
                bool enterChildren = true;

                while (property.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (property.propertyPath == "m_Script") continue;
                    if (property.depth > 0) continue;
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (property.objectReferenceValue != null) continue;

                    string key = behaviour.GetType().Name + "." + property.propertyPath;

                    IssueSeverity severity;
                    string detail;

                    if (Critical.Contains(key))
                    {
                        severity = IssueSeverity.Error;
                        detail = "이 참조가 없으면 해당 오브젝트가 동작하지 않습니다.";
                    }
                    else if (SelfHealing.Contains(key))
                    {
                        severity = IssueSeverity.Info;
                        detail = "Awake에서 씬을 뒤져 자동으로 채웁니다. 대상이 씬에 있으면 문제 없습니다.";
                    }
                    else
                    {
                        severity = IssueSeverity.Warning;
                        detail = "인스펙터에서 채워주세요.";
                    }

                    yield return new SceneIssue
                    {
                        Severity = severity,
                        Category = Category,
                        Title = $"{behaviour.GetType().Name}.{property.displayName} 비어 있음",
                        Detail = detail,
                        Target = behaviour.gameObject
                    };
                }
            }
        }
    }

    // ------------------------------------------------------------------ 필수/중복

    /// 씬에 반드시 하나만 있어야 하는 것들. 게임 매니저류가 생기면 Targets 에 추가한다.
    public class SingletonCheck : ISceneCheck
    {
        public string Category => "필수/중복";

        static readonly (Type type, bool required)[] Targets = { };

        public IEnumerable<SceneIssue> Run(SceneCheckContext context)
        {
            foreach ((Type type, bool required) in Targets)
            {
                var found = context.OurComponents.Where(c => c.GetType() == type).ToList();

                if (found.Count == 0 && required)
                {
                    yield return new SceneIssue
                    {
                        Severity = IssueSeverity.Error,
                        Category = Category,
                        Title = $"{type.Name} 가 씬에 없음",
                        Detail = "이게 없으면 관련 기능 전체가 동작하지 않습니다."
                    };
                }
                else if (found.Count > 1)
                {
                    yield return new SceneIssue
                    {
                        Severity = IssueSeverity.Error,
                        Category = Category,
                        Title = $"{type.Name} 가 {found.Count}개 있음",
                        Detail = "중복되면 이벤트를 여러 번 받아 판정이 두 번 나거나 점수가 두 배로 오릅니다.",
                        Target = found[0].gameObject
                    };
                }
            }

            if (Camera.main == null)
            {
                yield return new SceneIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = Category,
                    Title = "MainCamera 태그가 붙은 카메라가 없음",
                    Detail = "Camera.main 을 쓰는 코드가 전부 null 을 받습니다."
                };
            }

            var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (listeners.Length == 0)
            {
                yield return new SceneIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = Category,
                    Title = "AudioListener 가 없음",
                    Detail = "소리가 아예 나지 않습니다. 리듬게임에선 치명적입니다."
                };
            }
            else if (listeners.Length > 1)
            {
                yield return new SceneIssue
                {
                    Severity = IssueSeverity.Warning,
                    Category = Category,
                    Title = $"AudioListener 가 {listeners.Length}개 있음",
                    Detail = "Unity가 매 프레임 경고를 띄우고, 어느 쪽 기준으로 들릴지 보장되지 않습니다.",
                    Target = listeners[1].gameObject
                };
            }

            bool hasCanvas = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include) != null;
            if (hasCanvas && UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
            {
                yield return new SceneIssue
                {
                    Severity = IssueSeverity.Warning,
                    Category = Category,
                    Title = "Canvas 는 있는데 EventSystem 이 없음",
                    Detail = "UI 버튼 클릭이 동작하지 않습니다."
                };
            }
        }
    }

    /// 스크립트를 지웠을 때 남는 'Missing Script'.
    public class MissingScriptCheck : ISceneCheck
    {
        public string Category => "필수/중복";

        public IEnumerable<SceneIssue> Run(SceneCheckContext context)
        {
            foreach (GameObject root in context.Roots)
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                    if (count == 0) continue;

                    yield return new SceneIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = Category,
                        Title = $"{transform.name} 에 깨진 스크립트 {count}개",
                        Detail = "삭제된 스크립트의 잔재입니다. 인스펙터에서 제거하세요.",
                        Target = transform.gameObject
                    };
                }
            }
        }
    }

    // ------------------------------------------------------------------ 오디오

    /// 오디오 소스 설정 중 리듬게임에서 사고가 잘 나는 것들.
    public class AudioSourceCheck : ISceneCheck
    {
        public string Category => "오디오";

        public IEnumerable<SceneIssue> Run(SceneCheckContext context)
        {
            foreach (AudioSource source in UnityEngine.Object.FindObjectsByType<AudioSource>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (source.playOnAwake && source.clip != null && source.clip.length > 30f)
                {
                    yield return new SceneIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = Category,
                        Title = $"{source.name} 가 곡을 Play On Awake 로 재생함",
                        Detail = "씬 로딩 시점에 맞춰 시작해 박자 기준(dspTime)이 매번 달라집니다. PlayScheduled 로 시작하세요.",
                        Target = source.gameObject
                    };
                }

                if (source.spatialBlend > 0f)
                {
                    yield return new SceneIssue
                    {
                        Severity = IssueSeverity.Info,
                        Category = Category,
                        Title = $"{source.name} 가 3D 사운드로 설정됨 (Spatial Blend {source.spatialBlend:F2})",
                        Detail = "카메라와의 거리에 따라 볼륨이 달라집니다. 의도한 게 아니면 0으로 두세요.",
                        Target = source.gameObject
                    };
                }
            }
        }
    }

    // ------------------------------------------------------------------ 잔여물

    public class LeftoverCheck : ISceneCheck
    {
        public string Category => "잔여물";

        public IEnumerable<SceneIssue> Run(SceneCheckContext context)
        {
            foreach (GameObject root in context.Roots)
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!transform.name.StartsWith("__")) continue;

                    yield return new SceneIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = Category,
                        Title = $"테스트 오브젝트가 남아 있음: {transform.name}",
                        Detail = "검증 스크립트가 남긴 잔여물로 보입니다. 지워도 됩니다.",
                        Target = transform.gameObject
                    };
                }
            }
        }
    }
}
