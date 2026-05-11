using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;

namespace MusicTogether.MusicSampling
{
    /// <summary>
    /// 波形可视化 VisualElement。
    /// 支持多 Segment，每个 Segment 可以拥有独立的 BPM、拍型与细分粒度。
    /// 当 Segment 之间存在时间重叠时，自动分配到不同的水平轨道行（垂直堆叠）。
    /// 每个 Segment 在轨道内使用绝对定位，水平位置与实际时间对齐。
    /// </summary>
    public class WaveformVisualElement : VisualElement
    {
        private AudioSamplingData _data;
        private float[] _audioSamples;

        // 二维存储：[segmentIndex][localNoteIndex]
        private List<List<NoteVisualElement>> _noteElementsBySegment = new();

        // 每个轨道行的 VisualElement
        private List<VisualElement> _trackRows = new();

        // 轨道分配结果：segIdx → trackIndex
        private int[] _segmentTrackMap;

        // 每条轨道当前末尾的像素 X（用于贪心分配）
        private List<float> _trackEndPixels = new();

        // 事件：(segmentIndex, localNoteIndex)
        public event Action<int, int> OnNoteClicked;

        public WaveformVisualElement(AudioSamplingData data, float[] audioSamples)
        {
            _data = data;
            _audioSamples = audioSamples;

            AddToClassList("waveform-element");
            // flex-direction: column 由 USS .waveform-element 控制

            BuildWaveform();
        }

        // ── 构建 ─────────────────────────────────────────────────────────────

        private void BuildWaveform()
        {
            if (_data == null || _audioSamples == null || _data.audioClip == null)
                return;

            _noteElementsBySegment.Clear();
            _trackRows.Clear();
            _trackEndPixels.Clear();

            int segCount = _data.segments?.Count ?? 0;
            if (segCount == 0) return;

            // ── 第一步：为每个 segment 分配轨道（贪心）─────────────────────
            _segmentTrackMap = new int[segCount];
            for (int segIdx = 0; segIdx < segCount; segIdx++)
            {
                var seg = _data.segments[segIdx];
                float segStartPx = _data.GetSegmentPixelStartX(segIdx);
                // 用时间终点映射像素，保证跨段 BPM/拍型不同时仍正确
                double segEndTime = _data.GetSegmentStartTime(segIdx) + _data.GetSegmentDuration(segIdx);
                float segEndPx   = _data.GetPixelXAtTime(segEndTime);

                // 找第一个末尾 ≤ 当前段起始的轨道
                int assignedTrack = -1;
                for (int t = 0; t < _trackEndPixels.Count; t++)
                {
                    if (_trackEndPixels[t] <= segStartPx)
                    {
                        assignedTrack = t;
                        break;
                    }
                }

                if (assignedTrack == -1)
                {
                    // 没有空闲轨道，新建一条
                    assignedTrack = _trackRows.Count;
                    var trackRow = CreateTrackRow(assignedTrack);
                    _trackRows.Add(trackRow);
                    _trackEndPixels.Add(0f);
                    Add(trackRow);
                }

                _segmentTrackMap[segIdx] = assignedTrack;
                _trackEndPixels[assignedTrack] = segEndPx;
            }

            // ── 第二步：向各轨道中填充 segment 内容 ─────────────────────────
            for (int segIdx = 0; segIdx < segCount; segIdx++)
            {
                var seg = _data.segments[segIdx];
                var noteElements = new List<NoteVisualElement>();
                _noteElementsBySegment.Add(noteElements);

                int trackIdx     = _segmentTrackMap[segIdx];
                var trackRow     = _trackRows[trackIdx];
                float startPixel = _data.GetSegmentPixelStartX(segIdx);

                // segment 容器：绝对定位到轨道内的正确水平位置
                // position: absolute; top: 0; height: 100%; flex-direction: row 由 USS .segment-container 控制
                var segContainer = new VisualElement();
                segContainer.AddToClassList("segment-container");
                segContainer.style.left = startPixel;  // 动态值，必须保留

                // segment 信息头
                segContainer.Add(CreateSegmentSeparator(segIdx, seg));

                // 小节内容
                int totalNotes = _data.GetSegmentTotalNotes(segIdx);
                int notesPerBar = seg.NotesPerBar;
                int totalBars   = Mathf.CeilToInt((float)totalNotes / notesPerBar);

                for (int barIndex = 0; barIndex < totalBars; barIndex++)
                {
                    var barElement = CreateBarElement(segIdx, barIndex, seg, noteElements);
                    segContainer.Add(barElement);
                }

                trackRow.Add(segContainer);
            }
        }

        /// <summary>创建一条轨道行</summary>
        private VisualElement CreateTrackRow(int trackIdx)
        {
            var row = new VisualElement();
            row.AddToClassList("track-row");
            // position: relative 和 flex-direction: row 由 USS .track-row 控制

            // 轨道序号标签（左侧固定）
            var trackLabel = new Label($"Track {trackIdx + 1}");
            trackLabel.AddToClassList("track-label");
            row.Add(trackLabel);

            return row;
        }

        /// <summary>创建段落信息头（含段落序号、BPM、拍型），绝对定位叠在 segment 左侧，不占 flex 空间</summary>
        private VisualElement CreateSegmentSeparator(int segIdx, SamplingSegment seg)
        {
            var sep = new VisualElement();
            sep.AddToClassList("segment-separator");

            // position: absolute; left: 0; top: 0; height: 100% 由 USS .segment-separator 控制

            var label = new Label($"[{segIdx + 1}] {seg.name}  {seg.bpm} BPM  {seg.beatsPerBar}/{seg.beatDivision}");
            label.AddToClassList("segment-label");
            sep.Add(label);

            return sep;
        }

        private VisualElement CreateBarElement(int segIdx, int barIndex,
            SamplingSegment seg, List<NoteVisualElement> noteElements)
        {
            var barContainer = new VisualElement();
            barContainer.AddToClassList("bar-container");

            // 左侧竖线：absolute 叠加，不占 flex 宽度，避免推移后续小节导致跨 segment 错位
            var leftBorder = new VisualElement();
            leftBorder.AddToClassList("bar-left-border");
            barContainer.Add(leftBorder);

            // 小节顶部标记行
            var barMarker = new VisualElement();
            barMarker.AddToClassList("bar-marker");
            var barLabel = new Label($"{barIndex + 1}");
            barLabel.AddToClassList("bar-label");
            barMarker.Add(barLabel);
            barContainer.Add(barMarker);

            // 拍容器行
            var beatsContainer = new VisualElement();
            beatsContainer.AddToClassList("beats-container");

            for (int beatIndex = 0; beatIndex < seg.beatsPerBar; beatIndex++)
            {
                var beatElement = CreateBeatElement(segIdx, barIndex, beatIndex, seg, noteElements);
                beatsContainer.Add(beatElement);
            }

            barContainer.Add(beatsContainer);
            return barContainer;
        }

        private VisualElement CreateBeatElement(int segIdx, int barIndex, int beatInBar,
            SamplingSegment seg, List<NoteVisualElement> noteElements)
        {
            var beatContainer = new VisualElement();
            beatContainer.AddToClassList("beat-container");

            // 左侧竖线：absolute 叠加，不占 flex 宽度
            var leftBorder = new VisualElement();
            leftBorder.AddToClassList("beat-left-border");
            beatContainer.Add(leftBorder);

            for (int i = 0; i < seg.beatDivision; i++)
            {
                int localNoteIndex = barIndex * seg.NotesPerBar + beatInBar * seg.beatDivision + i;
                var noteElement = CreateNoteElement(segIdx, localNoteIndex, seg);
                beatContainer.Add(noteElement);
                noteElements.Add(noteElement);
            }

            return beatContainer;
        }

        private NoteVisualElement CreateNoteElement(int segIdx, int localNoteIndex, SamplingSegment seg)
        {
            var noteElement = new NoteVisualElement(_data, _audioSamples, segIdx, localNoteIndex, seg);
            noteElement.OnClicked += () => OnNoteClicked?.Invoke(segIdx, localNoteIndex);
            return noteElement;
        }

        // ── 外部接口 ──────────────────────────────────────────────────────────

        /// <summary>返回所有轨道的总高度（用于播放头高度设置）</summary>
        public float TotalHeight => resolvedStyle.height;

        /// <summary>刷新指定音符的标记状态（性能优化：只更新单个音符）</summary>
        public void RefreshNoteMarkedState(int segIdx, int localNoteIndex)
        {
            var noteElement = GetNoteElement(segIdx, localNoteIndex);
            if (noteElement == null) return;
            bool isMarked = _data.IsNoteMarked(segIdx, localNoteIndex);
            noteElement.UpdateMarkedState(isMarked);
        }

        /// <summary>刷新所有段落的全部音符标记状态（批量位移后调用）</summary>
        public void RefreshAllMarkedStates()
        {
            for (int segIdx = 0; segIdx < _noteElementsBySegment.Count; segIdx++)
            {
                var list = _noteElementsBySegment[segIdx];
                for (int noteIdx = 0; noteIdx < list.Count; noteIdx++)
                {
                    bool isMarked = _data.IsNoteMarked(segIdx, noteIdx);
                    list[noteIdx].UpdateMarkedState(isMarked);
                }
            }
        }

        /// <summary>设置音符高亮状态（当前播放位置）</summary>
        public void SetNoteHighlight(int segIdx, int localNoteIndex, bool isHighlighted)
        {
            GetNoteElement(segIdx, localNoteIndex)?.SetHighlight(isHighlighted);
        }

        // ── 向后兼容（单段）──────────────────────────────────────────────────

        /// <summary>向后兼容：操作 Segment 0</summary>
        public void RefreshNoteMarkedState(int noteIndex)
            => RefreshNoteMarkedState(0, noteIndex);

        /// <summary>向后兼容：操作 Segment 0</summary>
        public void SetNoteHighlight(int noteIndex, bool isHighlighted)
            => SetNoteHighlight(0, noteIndex, isHighlighted);

        // ── 私有工具 ──────────────────────────────────────────────────────────

        private NoteVisualElement GetNoteElement(int segIdx, int localNoteIndex)
        {
            if (_noteElementsBySegment == null ||
                segIdx < 0 || segIdx >= _noteElementsBySegment.Count)
                return null;

            var list = _noteElementsBySegment[segIdx];
            if (localNoteIndex < 0 || localNoteIndex >= list.Count)
                return null;

            return list[localNoteIndex];
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 单个音符的可视化元素。
    /// 持有所属 Segment 的引用，用于正确计算波形采样范围。
    /// </summary>
    public class NoteVisualElement : VisualElement
    {
        private AudioSamplingData _data;
        private float[] _audioSamples;
        private int _segIdx;
        private int _localNoteIndex;
        private SamplingSegment _seg;
        private bool _isMarked;
        private bool _isHighlighted;

        private static readonly Color WaveformColor = new Color(0.6f, 0.8f, 1f, 1f);

        public event Action OnClicked;

        public NoteVisualElement(AudioSamplingData data, float[] audioSamples,
            int segIdx, int localNoteIndex, SamplingSegment seg)
        {
            _data = data;
            _audioSamples = audioSamples;
            _segIdx = segIdx;
            _localNoteIndex = localNoteIndex;
            _seg = seg;
            _isMarked = data.IsNoteMarked(segIdx, localNoteIndex);

            style.width = data.noteWidth;
            AddToClassList("note-element");
            AddToClassList(_isMarked ? "note-marked" : "note-normal");

            // ── 顶端局部序号标签 ──────────────────────────────────────────────
            var indexLabel = new Label(localNoteIndex.ToString());
            indexLabel.AddToClassList("note-index-label");
            Add(indexLabel);
            // ────────────────────────────────────────────────────────────────

            RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    OnClicked?.Invoke();
                    evt.StopPropagation();
                }
            });
            RegisterCallback<MouseEnterEvent>(_ => AddToClassList("note-hover"));
            RegisterCallback<MouseLeaveEvent>(_ => RemoveFromClassList("note-hover"));

            generateVisualContent += GenerateWaveform;
        }

        private void GenerateWaveform(MeshGenerationContext ctx)
        {
            if (_audioSamples == null || _data == null || _data.audioClip == null)
                return;

            int sampleRate = _data.audioClip.frequency;
            int channels   = _data.audioClip.channels;

            // 用所属 Segment 的 SecondsPerNote 来确定采样范围
            double noteStart = _data.GetSegmentStartTime(_segIdx)
                               + _localNoteIndex       * _seg.SecondsPerNote;
            double noteEnd   = _data.GetSegmentStartTime(_segIdx)
                               + (_localNoteIndex + 1) * _seg.SecondsPerNote;

            int sampleStart = Mathf.Clamp((int)(noteStart * sampleRate * channels), 0, _audioSamples.Length - 1);
            int sampleEnd   = Mathf.Clamp((int)(noteEnd   * sampleRate * channels), 0, _audioSamples.Length - 1);

            if (sampleStart >= sampleEnd) return;

            int   samplesPerSegment  = _data.samplesPerNote;
            float segmentWidth       = contentRect.width / samplesPerSegment;
            int   samplesPerDivision = Mathf.Max(1, (sampleEnd - sampleStart) / samplesPerSegment);

            var painter = ctx.painter2D;
            painter.strokeColor = WaveformColor;
            painter.lineWidth   = segmentWidth * 0.8f;
            painter.lineCap     = LineCap.Round;

            for (int i = 0; i < samplesPerSegment; i++)
            {
                int segStart = sampleStart + i * samplesPerDivision;
                int segEnd   = Mathf.Min(segStart + samplesPerDivision, sampleEnd);

                float sum = 0;
                for (int j = segStart; j < segEnd; j++)
                    sum += Mathf.Abs(_audioSamples[j]);

                float amplitude = (sum / (segEnd - segStart)) * _data.waveformZoom;
                amplitude = Mathf.Clamp01(amplitude);

                float x       = i * segmentWidth + segmentWidth / 2;
                float centerY = contentRect.height / 2;
                float half    = amplitude * contentRect.height / 2;

                painter.BeginPath();
                painter.MoveTo(new Vector2(x, centerY - half));
                painter.LineTo(new Vector2(x, centerY + half));
                painter.Stroke();
            }
        }

        public void UpdateMarkedState(bool isMarked)
        {
            _isMarked = isMarked;
            UpdateVisualState();
        }

        public void SetHighlight(bool isHighlighted)
        {
            _isHighlighted = isHighlighted;
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            RemoveFromClassList("note-normal");
            RemoveFromClassList("note-marked");
            RemoveFromClassList("note-current");

            if (_isHighlighted)
                AddToClassList("note-current");
            else if (_isMarked)
                AddToClassList("note-marked");
            else
                AddToClassList("note-normal");
        }
    }
}
