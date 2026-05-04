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
    /// 同一首歌的不同段落可以拥有各自的 BPM、拍型、细分粒度和时间范围。
    /// <para>
    /// 时间范围用"全局小节序号"表示：<see cref="startBarIndex"/> 是本段第一个小节在整首曲子中的全局小节编号。
    /// 全局时间 = barIndex * (60 / bpm * beatsPerBar)。
    /// </para>
    /// </summary>
    [Serializable]
    public class SamplingSegment
    {
        [LabelText("名称")]
        public string name = "Segment";

        //[BoxGroup("时间范围（全局小节序号）")]
        [HorizontalGroup("时间范围（全局小节序号）"), LabelText("起始小节"), LabelWidth(60), Min(0)]
        public int startBarIndex = 0;

        [HorizontalGroup("时间范围（全局小节序号）"), LabelText("结束小节"), LabelWidth(60), Min(0)]
        public int endBarIndex = 0;

        //[BoxGroup("节拍配置")]HorizontalGroup("节拍配置"), 
        [LabelText("BPM"), LabelWidth(40), Range(60, 300)]
        public int bpm = 120;

        [LabelText("拍/小节"), LabelWidth(40), Range(2, 16)]
        public int beatsPerBar = 4;

        [LabelText("细分"), LabelWidth(40), Range(1, 16)]
        public int beatDivision = 4;

        [Title("已标记音符")]
        [HideLabel, ListDrawerSettings(ShowFoldout = true, ShowPaging = true, DefaultExpandedState = false)]
        public List<int> markedNoteIndices = new List<int>();

        // ── 计算属性 ──────────────────────────────────────────────────────────

        /// <summary>每个音符的时长（秒）</summary>
        public double SecondsPerNote => 60.0 / (bpm * beatDivision);

        /// <summary>每小节的时长（秒）</summary>
        public double SecondsPerBar => 60.0 / bpm * beatsPerBar;

        /// <summary>每小节包含的音符数</summary>
        public int NotesPerBar => beatsPerBar * beatDivision;

        /// <summary>
        /// 本段起始的全局时间（秒）= startBarIndex * SecondsPerBar
        /// </summary>
        public double StartTime => startBarIndex * SecondsPerBar;

        /// <summary>
        /// 本段结束的全局时间（秒）= (endBarIndex + 1) * SecondsPerBar（含尾小节的结束时刻）
        /// </summary>
        public double EndTime => (endBarIndex + 1) * SecondsPerBar;

        /// <summary>
        /// 本段声明的总小节数（endBarIndex >= startBarIndex 时有效，含首含尾闭区间）
        /// </summary>
        public int DeclaredBarCount => (endBarIndex >= startBarIndex) ? (endBarIndex - startBarIndex + 1) : 0;

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
        [InfoBox("与音频同步播放的参考视频，播放时自动静音 \n" + 
                 "结束小节 ≤ 起始小节时，自动延伸到下一段起始或音频末尾")]
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

        [HorizontalGroup("SegmentButtons"), Button("↕ 按起始小节排序", ButtonSizes.Medium), GUIColor(0.9f, 0.85f, 0.4f)]
        private void SortSegmentsByStartBar()
        {
            if (segments == null || segments.Count < 2) return;
            segments.Sort((a, b) => a.startBarIndex.CompareTo(b.startBarIndex));
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
                // 只有当设置了有效的结束小节时才进行裁剪
                if (seg.endBarIndex >= seg.startBarIndex)
                {
                    int maxNotes = seg.DeclaredBarCount * seg.NotesPerBar;
                    int removedCount = seg.markedNoteIndices.RemoveAll(idx => idx >= maxNotes);
                    if (removedCount > 0)
                    {
                        changed = true;
                        Debug.Log($"[AudioSamplingData] Segment '{seg.name}': Removed {removedCount} out-of-bounds notes.");
                    }
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
        /// 获取指定段落在时间轴上的起始时间（秒）。
        /// = startBarIndex * SecondsPerBar（使用本段 BPM）
        /// </summary>
        public double GetSegmentStartTime(int segmentIndex)
        {
            if (segments == null || segmentIndex < 0 || segmentIndex >= segments.Count)
                return 0;
            return segments[segmentIndex].StartTime;
        }

        /// <summary>
        /// 获取指定段落的时长（秒）。
        /// 优先使用段落自身声明的 endBarIndex；
        /// 若 endBarIndex 未设置（≤ startBarIndex），则用下一段的 StartTime 或 audioClip.length 作为边界。
        /// </summary>
        public double GetSegmentDuration(int segmentIndex)
        {
            if (segments == null || segmentIndex < 0 || segmentIndex >= segments.Count)
                return 0;

            var seg = segments[segmentIndex];

            // 优先使用显式声明的 endBarIndex
            if (seg.endBarIndex > seg.startBarIndex)
                return seg.DeclaredBarCount * seg.SecondsPerBar;

            // 退回：用下一段的 StartTime 推算
            if (segmentIndex + 1 < segments.Count)
                return segments[segmentIndex + 1].StartTime - seg.StartTime;

            // 最后一段：延伸到 audioClip 末尾
            if (audioClip != null && audioClip.length > seg.StartTime)
                return audioClip.length - seg.StartTime;

            return 0;
        }

        /// <summary>
        /// 返回给定全局时间下，所有"时间范围覆盖该时刻"的段落各自的当前音符。
        /// 用于多段重叠时同时高亮所有活跃段的当前音符。
        /// 判定标准：segStart &lt;= globalTime &lt; segEnd（有显式 endBarIndex 时用 EndTime，否则用下一段起始或音频末尾）。
        /// </summary>
        public List<(int segIdx, int localNoteIdx)> GetAllActiveNotesAtTime(double globalTime)
        {
            var result = new List<(int, int)>();
            if (segments == null || segments.Count == 0) return result;

            for (int i = 0; i < segments.Count; i++)
            {
                double segStart = GetSegmentStartTime(i);
                var seg = segments[i];

                double segEnd = seg.endBarIndex > seg.startBarIndex
                    ? seg.EndTime
                    : (i + 1 < segments.Count
                        ? GetSegmentStartTime(i + 1)
                        : double.MaxValue);

                if (globalTime < segStart || globalTime >= segEnd) continue;

                double localTime = globalTime - segStart;
                int localNote = Mathf.RoundToInt((float)(localTime * seg.bpm * seg.beatDivision / 60.0));
                int maxNote = GetSegmentTotalNotes(i);
                if (maxNote > 0)
                    localNote = Mathf.Clamp(localNote, 0, maxNote - 1);

                result.Add((i, localNote));
            }

            return result;
        }

        /// <summary>
        /// 根据全局时间获取 (segmentIndex, localNoteIndex) 对。
        /// 当时间落在两段之间的间隙时，夹紧到前一段的最后一个音符，
        /// 等待时间进入下一段后再切换——避免播放头在 UI 上飞出段落范围。
        /// </summary>
        public (int segIdx, int localNoteIdx) GetSegmentNoteAtTime(double globalTime)
        {
            if (segments == null || segments.Count == 0)
                return (0, 0);

            for (int i = segments.Count - 1; i >= 0; i--)
            {
                double segStart = GetSegmentStartTime(i);
                if (globalTime >= segStart)
                {
                    double localTime = globalTime - segStart;
                    var seg = segments[i];
                    int localNote = Mathf.RoundToInt((float)(localTime * seg.bpm * seg.beatDivision / 60.0));

                    // 如果段落有显式结束边界，将 localNote 夹紧到段内，
                    // 防止间隙时间让 localNote 超出该段的 UI 范围
                    int maxNote = GetSegmentTotalNotes(i);
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
        /// 获取指定段落在波形 UI 上实际显示的总音符数。
        /// 优先使用显式声明的 endBarIndex（DeclaredBarCount × NotesPerBar）；
        /// 仅当未声明 endBarIndex 时，才退回到时间推算。
        /// </summary>
        public int GetSegmentTotalNotes(int segIdx)
        {
            if (segments == null || segIdx < 0 || segIdx >= segments.Count)
                return 0;

            var seg = segments[segIdx];

            // 优先：endBarIndex > startBarIndex 时视为有效声明（含尾闭区间，至少2小节）
            if (seg.endBarIndex > seg.startBarIndex)
                return seg.DeclaredBarCount * seg.NotesPerBar;

            // 退回：用时间推算（段落未声明 endBarIndex 时，如单段或最后一段）
            if (audioClip == null) return 0;
            double dur = GetSegmentDuration(segIdx);
            return Mathf.CeilToInt((float)(dur * seg.bpm * seg.beatDivision / 60.0));
        }

        /// <summary>
        /// 将 (segIdx, localNoteIdx) 转换为全局音符序号。
        /// 全局序号定义：全局音符序号 = startBarIndex * NotesPerBar + localNoteIdx。
        /// </summary>
        public int GetGlobalNoteIndex(int segIdx, int localNoteIdx)
        {
            if (segments == null || segIdx < 0 || segIdx >= segments.Count)
                return localNoteIdx;
            var seg = segments[segIdx];
            return seg.startBarIndex * seg.NotesPerBar + localNoteIdx;
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

        // ── Editor 验证 ───────────────────────────────────────────────────────

        /// <summary>
        /// 判断给定的全局时间是否处于两段之间的"间隙"区域（不属于任何段落的声明范围）。
        /// 仅当段落有显式 endBarIndex 时才会产生间隙；若段落未声明结束，则视为连续。
        /// </summary>
        public bool IsTimeInGap(double globalTime)
        {
            if (segments == null || segments.Count == 0) return false;

            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                double segStart = GetSegmentStartTime(i);
                double segEnd   = seg.endBarIndex > seg.startBarIndex
                    ? seg.EndTime                              // 有显式结束边界（含尾小节末尾）
                    : (i + 1 < segments.Count                 // 无显式结束：延续到下一段
                        ? GetSegmentStartTime(i + 1)
                        : double.MaxValue);                   // 最后一段：无边界

                if (globalTime >= segStart && globalTime < segEnd)
                    return false; // 在段内
            }

            return true; // 不在任何段内 → 在间隙中
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (segments == null || segments.Count == 0) return;

            // 注意：不再自动排序，排序请使用 Inspector 中的"↕ 按起始小节排序"按钮

            // 检测小节序号重叠（含尾闭区间：cur 占用 [startBarIndex, endBarIndex]）
            for (int i = 0; i < segments.Count - 1; i++)
            {
                var cur  = segments[i];
                var next = segments[i + 1];

                // cur 有显式声明 && cur 末尾小节 >= 下一段起始小节 → 重叠
                if (cur.endBarIndex > cur.startBarIndex && cur.endBarIndex >= next.startBarIndex)
                {
                    Debug.LogWarning(
                        $"[AudioSamplingData] \"{name}\": " +
                        $"段落 \"{cur.name}\" 的 endBarIndex ({cur.endBarIndex}) " +
                        $">= 下一段 \"{next.name}\" 的 startBarIndex ({next.startBarIndex})，" +
                        $"两段发生重叠，将被分配到不同轨道。");
                }
            }

            // 检查最后一段不超出 audioClip 时长
            if (audioClip != null && segments.Count > 0)
            {
                var last = segments[segments.Count - 1];
                if (last.endBarIndex > last.startBarIndex)
                {
                    double endSec = last.EndTime;
                    if (endSec > audioClip.length + 0.001)
                    {
                        Debug.LogWarning(
                            $"[AudioSamplingData] \"{name}\": " +
                            $"最后一段 \"{last.name}\" 的 endBarIndex ({last.endBarIndex}) " +
                            $"对应时间 {endSec:F3}s，超过了 AudioClip 时长 ({audioClip.length:F3}s)。");
                    }
                }
            }
        }
#endif
    }
}
