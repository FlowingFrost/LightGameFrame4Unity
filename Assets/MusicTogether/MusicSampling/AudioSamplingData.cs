using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace MusicTogether.MusicSampling
{
    /// <summary>
    /// 单个采样段配置 —— 描述一段时间内的节拍信息与标记的音符。
    /// 同一首歌的不同段落可以拥有各自的 BPM、拍型、细分粒度。
    /// <para>
    /// 段与段之间紧密接续：seg[i+1].startTime == seg[i].endTime。
    /// startTime / endTime 由 AudioSamplingData.RecalculateSegmentTimes() 统一计算并存储。
    /// </para>
    /// </summary>
    [Serializable]
    public class SamplingSegment
    {
        [LabelText("名称")]
        public string name = "Segment";

        [HorizontalGroup("小节配置"), LabelText("小节数"), LabelWidth(60), Min(1)]
        public int barCount = 4;

        [HorizontalGroup("小节配置"), LabelText("音符偏移"), LabelWidth(60)]
        [Tooltip("段总音符数 = barCount * NotesPerBar + noteOffset。正值延长段尾，负值提前结束（用于在 mid-bar 处切换到下一段）。")]
        public int noteOffset = 0;

        [LabelText("BPM"), LabelWidth(40), Range(60, 300)]
        public int bpm = 120;

        [LabelText("拍/小节"), LabelWidth(40), Range(2, 16)]
        public int beatsPerBar = 4;

        [LabelText("细分"), LabelWidth(40), Range(1, 16)]
        public int beatDivision = 4;

        [Title("已标记音符")]
        [HideLabel, ListDrawerSettings(ShowFoldout = true, ShowPaging = true, DefaultExpandedState = false)]
        public List<int> markedNoteIndices = new List<int>();

        // ── 存储的时间边界（由 AudioSamplingData.RecalculateSegmentTimes() 填充）──

        [HideInInspector] public double startTime;
        [HideInInspector] public double endTime;

        // ── 计算属性 ──────────────────────────────────────────────────────────

        /// <summary>每个音符的时长（秒）</summary>
        public double SecondsPerNote => 60.0 / (bpm * beatDivision);

        /// <summary>每小节的时长（秒）</summary>
        public double SecondsPerBar => 60.0 / bpm * beatsPerBar;

        /// <summary>每小节包含的音符数</summary>
        public int NotesPerBar => beatsPerBar * beatDivision;

        /// <summary>本段总音符数</summary>
        public int TotalNotes => barCount * NotesPerBar + noteOffset;

        /// <summary>获取段内第 localNoteIndex 个音符的全局时间（秒）</summary>
        public double GetNoteTimeAt(int localNoteIndex)
            => startTime + localNoteIndex * SecondsPerNote;

        // ── 音符标记操作 ──────────────────────────────────────────────────────

        public bool IsNoteMarked(int localNoteIndex)
            => markedNoteIndices.Contains(localNoteIndex);

        public void AddMarkedNote(int localNoteIndex)
        {
            if (!markedNoteIndices.Contains(localNoteIndex))
            {
                markedNoteIndices.Add(localNoteIndex);
                markedNoteIndices.Sort();
            }
        }

        public void RemoveMarkedNote(int localNoteIndex)
            => markedNoteIndices.Remove(localNoteIndex);

        public void ToggleMarkedNote(int localNoteIndex)
        {
            if (IsNoteMarked(localNoteIndex))
                RemoveMarkedNote(localNoteIndex);
            else
                AddMarkedNote(localNoteIndex);
        }

        public void ClearAllMarkedNotes()
            => markedNoteIndices.Clear();
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 音频采样数据 ScriptableObject。
    /// 持有一个 AudioClip 以及多个 <see cref="SamplingSegment"/>，
    /// 每个 Segment 可以独立配置 BPM、拍型和细分粒度。
    /// </summary>
    [CreateAssetMenu(fileName = "AudioSamplingData", menuName = "MusicTogether/Audio Sampling Data")]
    public class AudioSamplingData : ScriptableObject
    {
        [InfoBox("与音频同步播放的参考视频，播放时自动静音")]
        //[BoxGroup("音频资源")]HorizontalGroup("音频资源/Row"), 
        [LabelText("音频文件"), LabelWidth(60)]
        public AudioClip audioClip;
        //HorizontalGroup("音频资源/Row"),
        [LabelText("参考视频"), LabelWidth(60)]
        public VideoClip referenceVideo;

        [FoldoutGroup("可视化配置")]//HorizontalGroup("可视化配置/Row"), 
        [LabelText("音符宽度(px)"), LabelWidth(90), Range(10, 100)]
        public float noteWidth = 40f;
        [FoldoutGroup("可视化配置")]
        [LabelText("波形缩放"), LabelWidth(60), Range(0.1f, 10f)]
        public float waveformZoom = 1.0f;
        [FoldoutGroup("可视化配置")]
        [LabelText("采样条数"), LabelWidth(60), Range(1, 20)]
        public int samplesPerNote = 10;

        // 提示信息

        // ── 段落列表 ──────────────────────────────────────────────────────────

        [Title("采样段列表")]
        [ListDrawerSettings(
            ShowFoldout = true,
            ShowPaging = false,
            HideAddButton = true,          // 隐藏默认加号，改用下方按钮
            CustomAddFunction = nameof(OpenAddSegmentPopup)
        )]
        public List<SamplingSegment> segments = new List<SamplingSegment>();

        [HorizontalGroup("SegmentButtons"), Button("＋ 添加段落", ButtonSizes.Medium), GUIColor(0.4f, 0.9f, 0.4f)]
        private void OpenAddSegmentPopup()
        {
#if UNITY_EDITOR
            var type = System.Type.GetType(
                "MusicTogether.MusicSampling.Editor.AddSegmentPopupWindow, Assembly-CSharp-Editor");
            if (type != null)
            {
                var method = type.GetMethod("Open",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                method?.Invoke(null, new object[] { this });
            }
            else
            {
                UnityEngine.Debug.LogError("[AudioSamplingData] 找不到 AddSegmentPopupWindow，请确认 Editor 脚本已编译。");
            }
#endif
        }

        [HorizontalGroup("SegmentButtons"), Button("🔄 重算段时间", ButtonSizes.Medium), GUIColor(0.55f, 0.75f, 1f)]
        private void RecalculateTimesButton()
        {
            RecalculateSegmentTimes();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        [HorizontalGroup("SegmentButtons"), Button("✂ 删除超范围音符", ButtonSizes.Medium), GUIColor(1f, 0.6f, 0.6f)]
        private void RemoveOutOfBoundsNotes()
        {
            if (segments == null) return;

            bool changed = false;
            foreach (var seg in segments)
            {
                int maxNotes = seg.TotalNotes;
                if (maxNotes <= 0) continue;
                int removedCount = seg.markedNoteIndices.RemoveAll(idx => idx >= maxNotes);
                if (removedCount > 0)
                {
                    changed = true;
                    Debug.Log($"[AudioSamplingData] Segment '{seg.name}': Removed {removedCount} out-of-bounds notes.");
                }
            }

#if UNITY_EDITOR
            if (changed)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
#endif
        }

        // ── 向后兼容：把旧的单段属性重定向到 segments[0] ──────────────────────

        private SamplingSegment FirstSegment
        {
            get
            {
                EnsureAtLeastOneSegment();
                return segments[0];
            }
        }

        private void EnsureAtLeastOneSegment()
        {
            if (segments == null) segments = new List<SamplingSegment>();
            if (segments.Count == 0) segments.Add(new SamplingSegment());
        }

        /// <summary>向后兼容：首段 BPM</summary>
        public int bpm => FirstSegment.bpm;
        /// <summary>向后兼容：首段拍型</summary>
        public int beatsPerBar => FirstSegment.beatsPerBar;
        /// <summary>向后兼容：首段细分</summary>
        public int beatDivision => FirstSegment.beatDivision;
        /// <summary>向后兼容：首段标记音符列表（直接引用）</summary>
        public List<int> markedNoteIndices => FirstSegment.markedNoteIndices;

        /// <summary>向后兼容：首段每音符时长</summary>
        public double SecondsPerNote => FirstSegment.SecondsPerNote;
        /// <summary>向后兼容：首段每小节时长</summary>
        public double SecondsPerBar => FirstSegment.SecondsPerBar;
        /// <summary>向后兼容：首段每小节音符数</summary>
        public int NotesPerBar => FirstSegment.NotesPerBar;

        // ── 跨段时间工具 ──────────────────────────────────────────────────────

        /// <summary>
        /// 获取指定段落的起始时间（秒）。直接返回存储的 startTime。
        /// </summary>
        public double GetSegmentStartTime(int segmentIndex)
        {
            if (segments == null || segmentIndex < 0 || segmentIndex >= segments.Count)
                return 0;
            return segments[segmentIndex].startTime;
        }

        /// <summary>
        /// 获取指定段落的时长（秒）= endTime - startTime。
        /// </summary>
        public double GetSegmentDuration(int segmentIndex)
        {
            if (segments == null || segmentIndex < 0 || segmentIndex >= segments.Count)
                return 0;
            var seg = segments[segmentIndex];
            return seg.endTime - seg.startTime;
        }

        /// <summary>
        /// 返回给定全局时间下，所有"时间范围覆盖该时刻"的段落各自的当前音符。
        /// 段与段之间紧密接续无间隙，直接使用存储的 startTime / endTime。
        /// </summary>
        public List<(int segIdx, int localNoteIdx)> GetAllActiveNotesAtTime(double globalTime)
        {
            var result = new List<(int, int)>();
            if (segments == null || segments.Count == 0) return result;

            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                if (globalTime < seg.startTime || globalTime >= seg.endTime) continue;

                double localTime = globalTime - seg.startTime;
                int localNote = Mathf.FloorToInt((float)(localTime * seg.bpm * seg.beatDivision / 60.0));
                int maxNote = seg.TotalNotes;
                if (maxNote > 0)
                    localNote = Mathf.Clamp(localNote, 0, maxNote - 1);

                result.Add((i, localNote));
            }

            return result;
        }

        /// <summary>
        /// 根据全局时间获取 (segmentIndex, localNoteIndex) 对。
        /// 段之间紧密接续无间隙，时间超出最后一段时夹紧到最后一段末尾。
        /// </summary>
        public (int segIdx, int localNoteIdx) GetSegmentNoteAtTime(double globalTime)
        {
            if (segments == null || segments.Count == 0)
                return (0, 0);

            for (int i = segments.Count - 1; i >= 0; i--)
            {
                var seg = segments[i];
                if (globalTime >= seg.startTime)
                {
                    double localTime = globalTime - seg.startTime;
                    int localNote = Mathf.FloorToInt((float)(localTime * seg.bpm * seg.beatDivision / 60.0));
                    int maxNote = seg.TotalNotes;
                    if (maxNote > 0)
                        localNote = Mathf.Clamp(localNote, 0, maxNote - 1);

                    return (i, localNote);
                }
            }

            return (0, 0);
        }

        /// <summary>
        /// 将 (segmentIndex, localNoteIndex) 转换为全局时间（秒）。
        /// </summary>
        public double GetTimeAtSegmentNote(int segIdx, int localNoteIdx)
        {
            if (segments == null || segIdx < 0 || segIdx >= segments.Count)
                return 0;

            return GetSegmentStartTime(segIdx) + localNoteIdx * segments[segIdx].SecondsPerNote;
        }

        /// <summary>
        /// 获取指定段落在波形 UI 中的像素起始 X 坐标。
        /// 基于该段的 StartTime 与第 0 段的 pixelsPerSecond（noteWidth / SecondsPerNote），
        /// 使不同 BPM/拍型的段落在时间轴上位置仍然对齐。
        /// </summary>
        public float GetSegmentPixelStartX(int segIdx)
        {
            if (segments == null || segIdx < 0 || segIdx >= segments.Count)
                return 0;

            // 以第 0 段的时间-像素比为全局基准：1 秒 = 多少像素
            var seg0 = segments[0];
            float pixelsPerSecond = noteWidth / (float)seg0.SecondsPerNote;

            double startTime = GetSegmentStartTime(segIdx);
            return (float)(startTime * pixelsPerSecond);
        }

        /// <summary>
        /// 将 (segmentIndex, localNoteIndex) 转换为该音符在整个波形中的像素 X 坐标。
        /// 使用全局 pixelsPerSecond 基准，与 GetSegmentPixelStartX 保持一致。
        /// </summary>
        public float GetPixelXAtSegmentNote(int segIdx, int localNoteIdx)
        {
            if (segments == null || segments.Count == 0)
                return 0;

            double noteTime = GetTimeAtSegmentNote(segIdx, localNoteIdx);
            var seg0 = segments[0];
            float pixelsPerSecond = noteWidth / (float)seg0.SecondsPerNote;
            return (float)(noteTime * pixelsPerSecond);
        }

        /// <summary>
        /// 将全局时间（秒）直接映射到波形 UI 的像素 X 坐标。
        /// 使用与 GetSegmentPixelStartX 相同的全局 pixelsPerSecond 基准（seg0 的时间-像素比），
        /// 保证播放头在任意段落内都与音符格精确对齐。
        /// </summary>
        public float GetPixelXAtTime(double globalTime)
        {
            if (segments == null || segments.Count == 0) return 0;

            var seg0 = segments[0];
            float pixelsPerSecond = noteWidth / (float)seg0.SecondsPerNote;
            return (float)(globalTime * pixelsPerSecond);
        }

        /// <summary>
        /// 获取指定段落的音符总数 = barCount * NotesPerBar + noteOffset。
        /// </summary>
        public int GetSegmentTotalNotes(int segIdx)
        {
            if (segments == null || segIdx < 0 || segIdx >= segments.Count)
                return 0;
            return segments[segIdx].TotalNotes;
        }

        // ── 向后兼容的单段音符操作 ────────────────────────────────────────────

        /// <summary>向后兼容：操作首段音符</summary>
        public int GetNoteIndexAtTime(double time) => GetSegmentNoteAtTime(time).localNoteIdx;

        /// <summary>向后兼容：操作首段音符</summary>
        public double GetTimeAtNoteIndex(int noteIndex) => GetTimeAtSegmentNote(0, noteIndex);

        /// <summary>向后兼容：操作首段小节索引</summary>
        public int GetBarIndexAtNote(int noteIndex) => noteIndex / NotesPerBar;

        /// <summary>向后兼容：操作首段</summary>
        public int GetNoteIndexAtBar(int barIndex) => barIndex * NotesPerBar;

        /// <summary>向后兼容：操作首段标记</summary>
        public bool IsNoteMarked(int noteIndex) => FirstSegment.IsNoteMarked(noteIndex);

        /// <summary>向后兼容：操作首段标记</summary>
        public void AddMarkedNote(int noteIndex) => FirstSegment.AddMarkedNote(noteIndex);

        /// <summary>向后兼容：操作首段标记</summary>
        public void RemoveMarkedNote(int noteIndex) => FirstSegment.RemoveMarkedNote(noteIndex);

        /// <summary>向后兼容：操作首段标记</summary>
        public void ToggleMarkedNote(int noteIndex) => FirstSegment.ToggleMarkedNote(noteIndex);

        /// <summary>向后兼容：清除首段所有标记</summary>
        public void ClearAllMarkedNotes() => FirstSegment.ClearAllMarkedNotes();

        // ── 多段标记操作（推荐使用）──────────────────────────────────────────

        /// <summary>添加指定段落中指定局部音符的标记（已标记则忽略）</summary>
        public void AddMarkedNote(int segIdx, int localNoteIdx)
        {
            if (segments == null || segIdx < 0 || segIdx >= segments.Count)
                return;
            segments[segIdx].AddMarkedNote(localNoteIdx);
        }

        /// <summary>切换指定段落中指定局部音符的标记状态</summary>
        public void ToggleMarkedNote(int segIdx, int localNoteIdx)
        {
            if (segments == null || segIdx < 0 || segIdx >= segments.Count)
                return;
            segments[segIdx].ToggleMarkedNote(localNoteIdx);
        }

        /// <summary>检查指定段落中指定局部音符是否已标记</summary>
        public bool IsNoteMarked(int segIdx, int localNoteIdx)
        {
            if (segments == null || segIdx < 0 || segIdx >= segments.Count)
                return false;
            return segments[segIdx].IsNoteMarked(localNoteIdx);
        }

        // ── 批量位移 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 批量位移指定段落的标记音符索引。
        /// </summary>
        /// <param name="segIdx">段落索引</param>
        /// <param name="start">范围起始（含）</param>
        /// <param name="end">范围结束（不含）。start &gt;= end 时视为从 start 到末尾</param>
        /// <param name="offset">位移量（+1 或 -1）。负值结果会被跳过</param>
        public void ShiftMarkedNoteIndices(int segIdx, int start, int end, int offset)
        {
            if (segments == null || segIdx < 0 || segIdx >= segments.Count) return;
            if (offset == 0) return;

            var seg = segments[segIdx];
            int effectiveEnd = start < end ? end : int.MaxValue;

            // 降序取出范围内的索引，避免移除时 index 漂移
            var toShift = seg.markedNoteIndices
                .Where(idx => idx >= start && idx < effectiveEnd)
                .OrderByDescending(idx => idx)
                .ToList();

            if (toShift.Count == 0) return;

            // 先全部移除
            foreach (var idx in toShift)
                seg.markedNoteIndices.Remove(idx);

            // 再添加新索引（冲突：先添加的保留）
            foreach (var idx in toShift)
            {
                int newIdx = idx + offset;
                if (newIdx < 0) continue;
                if (!seg.markedNoteIndices.Contains(newIdx))
                    seg.markedNoteIndices.Add(newIdx);
            }

            seg.markedNoteIndices.Sort();

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        // ── 段时间重算 ───────────────────────────────────────────────────────

        /// <summary>
        /// 从 seg[0] 开始逐段计算并存储 startTime / endTime。
        /// seg[0].startTime = 0，seg[i+1].startTime = seg[i].endTime。
        /// </summary>
        public void RecalculateSegmentTimes()
        {
            if (segments == null || segments.Count == 0) return;

            double currentTime = 0;
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                seg.startTime = currentTime;
                double duration = seg.TotalNotes * seg.SecondsPerNote;
                seg.endTime = currentTime + duration;
                currentTime = seg.endTime;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (segments == null || segments.Count == 0) return;

            RecalculateSegmentTimes();

            // 检查最后一段不超出 audioClip 时长
            if (audioClip != null && segments.Count > 0)
            {
                var last = segments[segments.Count - 1];
                if (last.endTime > audioClip.length + 0.001)
                {
                    Debug.LogWarning(
                        $"[AudioSamplingData] \"{name}\": " +
                        $"最后一段 \"{last.name}\" 的结束时间 {last.endTime:F3}s，" +
                        $"超过了 AudioClip 时长 ({audioClip.length:F3}s)。");
                }
            }
        }
#endif
    }
}
