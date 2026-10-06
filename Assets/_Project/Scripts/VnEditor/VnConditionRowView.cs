using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RhythmCP.Vn;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.VnEditing
{
    /// 인스펙터 조건(if) 한 행(템플릿): 플래그 드롭다운 · 비교 드롭다운 · 값 · 삭제.
    /// 플래그는 등록부에서만 고른다 — 오타가 원천적으로 안 생기게(팀 작업 가독성, 결정 5·6).
    /// 누적 = ">= <= == != > <" + 숫자, 단일 = "= true" / "= false"(값 칸 숨김).
    public class VnConditionRowView : MonoBehaviour
    {
        [SerializeField] TMP_Dropdown _flag;
        [SerializeField] TMP_Dropdown _op;
        [SerializeField] TMP_InputField _value;
        [SerializeField] Button _delete;

        static readonly string[] CounterOps = { ">=", "<=", "==", "!=", ">", "<" };
        static readonly string[] BoolOps = { "true", "false" };

        /// 행 index, 플래그 id, JSON 값(">=2" / "true").
        public event Action<int, string, string> Changed;
        public event Action<int> DeleteClicked;

        readonly List<string> _flagIds = new List<string>();
        int _index;
        bool _counter;

        void Awake()
        {
            _flag.onValueChanged.AddListener(OnFlagPicked);
            _op.onValueChanged.AddListener(_ => Emit());
            _value.onEndEdit.AddListener(_ => Emit());
            _delete.onClick.AddListener(() => DeleteClicked?.Invoke(_index));
        }

        /// registry에 없는 플래그(손으로 쓴 JSON의 오타 등)도 "(미등록)"으로 보이게 — 지우거나 바꿀 수 있도록.
        public void Set(int index, VnFlagRegistry registry, string flag, string expr)
        {
            _index = index;
            _flagIds.Clear();
            _flagIds.AddRange(registry.flags.Select(f => f.id));
            bool unknown = !_flagIds.Contains(flag);
            if (unknown) _flagIds.Add(flag);

            _flag.ClearOptions();
            _flag.AddOptions(_flagIds.Select(id => id == flag && unknown ? $"{id} (미등록)" : id).ToList());
            _flag.SetValueWithoutNotify(_flagIds.IndexOf(flag));

            var def = registry.Find(flag);
            _counter = def != null ? def.IsCounter : !VnCondition.IsBoolExpr(expr);
            FillOps();
            if (_counter)
            {
                VnCondition.TryParse(expr, out string op, out int value);
                _op.SetValueWithoutNotify(Mathf.Max(0, Array.IndexOf(CounterOps, op)));
                if (!_value.isFocused) _value.SetTextWithoutNotify(value.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                _op.SetValueWithoutNotify(expr != null && expr.Trim().ToLowerInvariant() == "false" ? 1 : 0);
            }
        }

        void FillOps()
        {
            _op.ClearOptions();
            _op.AddOptions(_counter ? CounterOps.ToList() : BoolOps.Select(b => "= " + b).ToList());
            _value.gameObject.SetActive(_counter);
        }

        /// 플래그를 바꾸면 종류에 맞는 기본값으로(누적 ">=1", 단일 "true").
        void OnFlagPicked(int i)
        {
            string flag = _flagIds[i];
            Changed?.Invoke(_index, flag, null);
        }

        void Emit()
        {
            string flag = _flagIds[_flag.value];
            string expr;
            if (_counter)
            {
                if (!int.TryParse(_value.text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) n = 0;
                expr = CounterOps[_op.value] + n.ToString(CultureInfo.InvariantCulture);
            }
            else expr = BoolOps[_op.value];
            Changed?.Invoke(_index, flag, expr);
        }
    }
}
