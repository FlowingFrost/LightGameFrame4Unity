using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace MusicTogether.MusicSampling.Editor
{
    /// <summary>
    /// 添加采样段弹窗。
    /// 在 <see cref="AudioSamplingData"/> Inspector 中点击"＋ 添加段落"按钮时弹出，
    /// 让用户填写完整信息后再添加，避免段落信息全部依赖默认值看不清。
    /// </summary>
    public class AddSegmentPopupWindow : OdinEditorWindow
    {
        // ── 目标数据 ──────────────────────────────────────────────────────────
        private AudioSamplingData _target;

        // ── 新段落信息 ────────────────────────────────────────────────────────

        [BoxGroup("基本信息")]
        [LabelText("段落名称"), LabelWidth(80)]
        public string segmentName = "Segment";

        [BoxGroup("小节配置")]
        [HorizontalGroup("小节配置/Row")]
        [LabelText("小节数"), LabelWidth(60), Min(1)]
        public int barCount = 4;

        [BoxGroup("节拍配置")]
        [HorizontalGroup("节拍配置/Row")]
        [LabelText("BPM"), LabelWidth(40), Range(60, 300)]
        public int bpm = 120;

        [HorizontalGroup("节拍配置/Row")]
        [LabelText("拍/小节"), LabelWidth(55), Range(2, 16)]
        public int beatsPerBar = 4;

        [HorizontalGroup("节拍配置/Row")]
        [LabelText("细分"), LabelWidth(35), Range(1, 16)]
        public int beatDivision = 4;

        // ── 预览信息 ──────────────────────────────────────────────────────────

        [ShowInInspector, ReadOnly]
        [BoxGroup("预览信息")]
        [LabelText("每音符时长(s)"), LabelWidth(90)]
        private double SecondsPerNote => bpm > 0 && beatDivision > 0
            ? System.Math.Round(60.0 / (bpm * beatDivision), 4)
            : 0;

        [ShowInInspector, ReadOnly]
        [BoxGroup("预览信息")]
        [LabelText("每小节时长(s)"), LabelWidth(90)]
        private double SecondsPerBar => bpm > 0
            ? System.Math.Round(60.0 / bpm * beatsPerBar, 4)
            : 0;

        [ShowInInspector, ReadOnly]
        [BoxGroup("预览信息")]
        [LabelText("段总时长(s)"), LabelWidth(90)]
        private double SegmentDuration => System.Math.Round(barCount * SecondsPerBar, 3);

        // ── 静态入口 ──────────────────────────────────────────────────────────

        /// <summary>打开弹窗，绑定目标 AudioSamplingData。</summary>
        public static void Open(AudioSamplingData target)
        {
            if (target == null) return;

            var window = GetWindow<AddSegmentPopupWindow>(false, "添加采样段", true);
            window._target = target;
            window.minSize = new Vector2(380, 360);
            window.maxSize = new Vector2(520, 480);

            // 用目标现有最后一段的参数做默认值，方便连续添加
            if (target.segments != null && target.segments.Count > 0)
            {
                var last = target.segments[target.segments.Count - 1];
                window.bpm          = last.bpm;
                window.beatsPerBar  = last.beatsPerBar;
                window.beatDivision = last.beatDivision;
                window.barCount     = last.barCount;
                window.segmentName  = $"Segment {target.segments.Count + 1}";
            }
            else
            {
                window.segmentName = "Segment 1";
            }
        }

        // ── 按钮 ──────────────────────────────────────────────────────────────

        [HorizontalGroup("Actions"), Button("✔ 确认添加", ButtonSizes.Large), GUIColor(0.4f, 0.9f, 0.4f)]
        private void Confirm()
        {
            if (_target == null)
            {
                EditorUtility.DisplayDialog("错误", "目标数据已失效，请重新打开弹窗。", "确定");
                Close();
                return;
            }

            var seg = new SamplingSegment
            {
                name          = string.IsNullOrWhiteSpace(segmentName) ? "Segment" : segmentName.Trim(),
                barCount      = barCount,
                bpm           = bpm,
                beatsPerBar   = beatsPerBar,
                beatDivision  = beatDivision,
            };

            _target.segments.Add(seg);
            _target.RecalculateSegmentTimes();
            EditorUtility.SetDirty(_target);

            Debug.Log($"[AudioSamplingData] 已添加段落 \"{seg.name}\"（{seg.barCount} 小节，{seg.bpm} BPM，时长 {seg.endTime - seg.startTime:F3}s）");
            Close();
        }

        [HorizontalGroup("Actions"), Button("✖ 取消", ButtonSizes.Large), GUIColor(0.9f, 0.4f, 0.4f)]
        private void Cancel()
        {
            Close();
        }
    }
}
