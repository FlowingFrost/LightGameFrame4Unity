using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace MusicTogether.MusicSampling
{
    /// <summary>
    /// MusicSampling 的 Controller。
    /// 纯 C# 逻辑，不含任何 UnityEditor API。可被 Editor 和 Play Mode 共用。
    /// 管理数据加载、状态、标记/位移操作，并通过 UIManager 驱动 UI 更新。
    /// </summary>
    public class MusicSamplingController
    {
        private MusicSamplingUIManager _ui;
        private AudioSamplingData _samplingData;
        private float[] _audioSamples;

        // 状态
        private (int seg, int note) _currentNote = (-1, -1);
        private readonly Dictionary<int, int> _highlightedNotes = new();

        // 音频状态（由 Host 通过 OnTimeChanged / OnStateChanged 同步）
        public double CurrentTime { get; private set; }
        public double Duration { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsDraggingTimeline { get; set; }
        public AudioSamplingData Data => _samplingData;

        /// <summary>当 Controller 修改了 AudioSamplingData 时触发，供 Host 调用 EditorUtility.SetDirty。</summary>
        public event System.Action OnDataModified;

        /// <summary>
        /// 绑定 UIManager，订阅数据/逻辑类 Action。
        /// 注意：音频相关 Action（OnPlayClicked, OnStopClicked, OnRefreshClicked,
        /// OnTimelineValueChanged, OnTimelineDragStarted, OnTimelineDragEnded）
        /// 由 Host 在调用 Bind 之前自行订阅。
        /// </summary>
        public void Bind(MusicSamplingUIManager ui)
        {
            _ui = ui;

            // 数据/逻辑类操作 → Controller 处理
            _ui.OnNoteClicked = OnNoteClicked;
            _ui.OnMarkCurrentClicked = OnMarkCurrentClicked;
            _ui.OnShiftClicked = OnShiftClicked;
            _ui.OnWaveformBlankClicked = OnMarkCurrentClicked;
            _ui.OnShiftSegmentChanged = UpdateShiftInfo;
        }

        /// <summary>
        /// 加载 AudioSamplingData，提取音频采样，构建波形。
        /// </summary>
        public void LoadData(AudioSamplingData data)
        {
            _samplingData = data;
            _highlightedNotes.Clear();
            _currentNote = (-1, -1);
            ResetScroll();

            if (data == null || data.audioClip == null)
            {
                _audioSamples = null;
                Duration = 0;
                _ui?.ClearWaveformDisplay();
                _ui?.SetMarkCurrentButtonEnabled(false);
                return;
            }

            // 提取音频采样
            var clip = data.audioClip;
            Duration = clip.length;
            _audioSamples = ExtractAudioSamples(clip);

            // 更新 UI
            _ui?.UpdateWaveformDisplay(data, _audioSamples);
            _ui?.SetMarkCurrentButtonEnabled(true);
            UpdateShiftInfo();
        }

        /// <summary>
        /// 被 Host 在音频时间变化时调用。
        /// </summary>
        public void OnTimeChanged(double time)
        {
            CurrentTime = time;

            if (_ui == null || _samplingData == null) return;

            // 更新 timeline slider（非拖拽时）
            if (!IsDraggingTimeline)
                _ui.SetTimelineValue((float)time);

            // 播放头
            float pixelX = _samplingData.GetPixelXAtTime(time);
            _ui.SetPlayheadPosition(pixelX);

            // 瞬切滚动
            float viewportW = _ui.WaveformViewportWidth;
            _ui.SetScrollOffset(Mathf.Max(0, pixelX - viewportW / 2f));

            // 高亮
            UpdateHighlightedNotes(_samplingData.GetAllActiveNotesAtTime(time));

            // 信息标签
            UpdateInfoLabels(time);

            // 当前音符追踪
            var note = _samplingData.GetSegmentNoteAtTime(time);
            if (note != _currentNote)
                _currentNote = note;
        }

        /// <summary>
        /// 被 Host 在音频播放状态变化时调用。
        /// </summary>
        public void OnStateChanged(bool isPlaying)
        {
            IsPlaying = isPlaying;
            _ui?.SetPlayButtonState(isPlaying);
        }

        /// <summary>
        /// 被 Host 在音频 Stop 或跳转时调用。
        /// </summary>
        public void OnSeeked()
        {
            if (!IsDraggingTimeline)
                ResetScroll();
        }

        // ── 内部逻辑 ────────────────────────────────────────────────────────

        private float[] ExtractAudioSamples(AudioClip clip)
        {
            try
            {
                int totalSamples = clip.samples * clip.channels;
                var samples = new float[totalSamples];
                if (!clip.GetData(samples, 0))
                {
                    Debug.LogError("音频数据读取失败！");
                    return null;
                }
                return samples;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"音频采样异常: {e.Message}");
                return null;
            }
        }

        private void UpdateInfoLabels(double time)
        {
            if (_ui == null || _samplingData == null) return;

            _ui.SetTimeLabel($"{FormatTime(time)} / {FormatTime(Duration)}");

            var (segIdx, localNoteIdx) = _samplingData.GetSegmentNoteAtTime(time);

            if (_samplingData.segments != null && segIdx < _samplingData.segments.Count)
            {
                var seg = _samplingData.segments[segIdx];
                int barIndex = localNoteIdx / seg.NotesPerBar;
                int noteInBar = localNoteIdx % seg.NotesPerBar;

                _ui.SetBpmLabel($"[{segIdx + 1}] {seg.name}  BPM: {seg.bpm} ({seg.beatsPerBar}/{seg.beatDivision})");
                _ui.SetNoteIndexLabel($"小节: {barIndex + 1} | 音符: {localNoteIdx} ({noteInBar + 1}/{seg.NotesPerBar})");
            }
        }

        private static string FormatTime(double time)
        {
            int minutes = (int)(time / 60);
            int seconds = (int)(time % 60);
            int milliseconds = (int)((time % 1) * 1000);
            return $"{minutes:D2}:{seconds:D2}.{milliseconds:D3}";
        }

        private void OnNoteClicked(int segIdx, int localNoteIdx)
        {
            if (_samplingData == null) return;

            _samplingData.ToggleMarkedNote(segIdx, localNoteIdx);
            _ui?.RefreshNoteMarkedState(segIdx, localNoteIdx);
            OnDataModified?.Invoke();
        }

        private void OnMarkCurrentClicked()
        {
            if (_samplingData == null) return;

            MarkCurrentNotes(CurrentTime);
        }

        public void MarkCurrentNotes(double time)
        {
            if (_samplingData == null) return;

            var activeNotes = _samplingData.GetAllActiveNotesAtTime(time);
            bool changed = false;
            foreach (var (segIdx, localNoteIdx) in activeNotes)
            {
                if (_samplingData.IsNoteMarked(segIdx, localNoteIdx)) continue;
                _samplingData.AddMarkedNote(segIdx, localNoteIdx);
                _ui?.RefreshNoteMarkedState(segIdx, localNoteIdx);
                changed = true;
            }
            if (changed) OnDataModified?.Invoke();
        }

        private void OnShiftClicked(int offset)
        {
            if (_samplingData == null) return;

            int segIdx = _ui?.ShiftSegmentValue ?? 0;
            int from = _ui?.ShiftFromValue ?? 0;
            int to = _ui?.ShiftToValue ?? 0;

            _samplingData.ShiftMarkedNoteIndices(segIdx, from, to, offset);
            _ui?.RefreshAllMarkedStates();
            OnDataModified?.Invoke();
        }

        private void UpdateShiftInfo()
        {
            if (_ui == null || _samplingData == null) return;

            int segIdx = _ui.ShiftSegmentValue;
            if (segIdx >= 0 && segIdx < _samplingData.segments.Count)
            {
                var seg = _samplingData.segments[segIdx];
                _ui.UpdateShiftSegmentInfo($"[{seg.name}]", _samplingData.GetSegmentTotalNotes(segIdx));
            }
            else
            {
                _ui.UpdateShiftSegmentInfo("(无效)", 0);
            }
        }

        private void UpdateHighlightedNotes(List<(int seg, int note)> activeNotes)
        {
            if (_ui == null) return;

            var toRemove = new List<int>();
            foreach (var kv in _highlightedNotes)
            {
                int segIdx = kv.Key;
                int noteIdx = kv.Value;
                bool stillActive = activeNotes != null &&
                                   activeNotes.Exists(n => n.seg == segIdx && n.note == noteIdx);
                if (!stillActive)
                {
                    _ui.SetNoteHighlight(segIdx, noteIdx, false);
                    toRemove.Add(segIdx);
                }
            }
            foreach (var s in toRemove) _highlightedNotes.Remove(s);

            if (activeNotes == null) return;

            foreach (var (segIdx, noteIdx) in activeNotes)
            {
                if (_highlightedNotes.TryGetValue(segIdx, out int prev) && prev == noteIdx)
                    continue;

                if (_highlightedNotes.TryGetValue(segIdx, out int oldNote))
                    _ui.SetNoteHighlight(segIdx, oldNote, false);

                _ui.SetNoteHighlight(segIdx, noteIdx, true);
                _highlightedNotes[segIdx] = noteIdx;
            }
        }

        private void ResetScroll()
        {
            _ui?.SetScrollOffset(0f);
        }

        public void Dispose()
        {
            _ui?.Dispose();
            _ui = null;
        }
    }
}
