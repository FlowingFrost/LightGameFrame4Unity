using UnityEngine;
using UnityEngine.UIElements;
using System;

namespace MusicTogether.MusicSampling
{
    /// <summary>
    /// MusicSampling 的 UIManager。
    /// 只负责 UI 控件查询、底层事件绑定和显示更新，不知道 Controller 或 Editor 的存在。
    /// </summary>
    public class MusicSamplingUIManager : IDisposable
    {
        private readonly VisualElement _root;

        // UI 控件
        private readonly Button _playButton;
        private readonly Button _stopButton;
        private readonly Button _refreshButton;
        private readonly Button _markCurrentButton;
        private readonly Slider _timelineSlider;
        private readonly Label _timeLabel;
        private readonly Label _bpmLabel;
        private readonly Label _noteIndexLabel;
        private readonly ScrollView _waveformContainer;
        private readonly IntegerField _shiftSegment;
        private readonly Label _shiftSegmentName;
        private readonly IntegerField _shiftFrom;
        private readonly IntegerField _shiftTo;
        private readonly Button _shiftMinusButton;
        private readonly Button _shiftPlusButton;

        // 波形
        private WaveformVisualElement _waveformElement;
        private VisualElement _playhead;

        // ── 公开的 Action 属性（供 Controller 订阅）────────────────────────

        public Action OnPlayClicked;
        public Action OnStopClicked;
        public Action OnRefreshClicked;
        public Action OnMarkCurrentClicked;
        public Action<float> OnTimelineValueChanged;
        public Action OnTimelineDragStarted;
        public Action OnTimelineDragEnded;
        public Action<int> OnShiftClicked;           // offset: +1 或 -1
        public Action<int, int> OnNoteClicked;       // (segIdx, localNoteIdx)
        public Action OnWaveformBlankClicked;
        public Action OnShiftSegmentChanged;          // shift 目标段索引变化

        // ── 可读属性 ────────────────────────────────────────────────────────

        public int ShiftSegmentValue => _shiftSegment?.value ?? 0;
        public int ShiftFromValue => _shiftFrom?.value ?? 0;
        public int ShiftToValue => _shiftTo?.value ?? 0;
        public float WaveformViewportWidth => _waveformContainer?.contentRect.width ?? 800f;

        public MusicSamplingUIManager(VisualElement root)
        {
            _root = root;

            // 查询控件
            _playButton = root.Q<Button>("play-button");
            _stopButton = root.Q<Button>("stop-button");
            _refreshButton = root.Q<Button>("refresh-button");
            _markCurrentButton = root.Q<Button>("mark-current-button");
            _timelineSlider = root.Q<Slider>("timeline-slider");
            _timeLabel = root.Q<Label>("time-label");
            _bpmLabel = root.Q<Label>("bpm-label");
            _noteIndexLabel = root.Q<Label>("note-index-label");
            _waveformContainer = root.Q<ScrollView>("waveform-container");
            _shiftSegment = root.Q<IntegerField>("shift-segment");
            _shiftSegmentName = root.Q<Label>("shift-segment-name");
            _shiftFrom = root.Q<IntegerField>("shift-from");
            _shiftTo = root.Q<IntegerField>("shift-to");
            _shiftMinusButton = root.Q<Button>("shift-minus-button");
            _shiftPlusButton = root.Q<Button>("shift-plus-button");

            BindEvents();
            ClearWaveformDisplay();
        }

        private void BindEvents()
        {
            if (_playButton != null)
                _playButton.clicked += () => OnPlayClicked?.Invoke();

            if (_stopButton != null)
                _stopButton.clicked += () => OnStopClicked?.Invoke();

            if (_refreshButton != null)
                _refreshButton.clicked += () => OnRefreshClicked?.Invoke();

            if (_markCurrentButton != null)
            {
                _markCurrentButton.clicked += () => OnMarkCurrentClicked?.Invoke();
                _markCurrentButton.SetEnabled(false);
            }

            if (_shiftMinusButton != null)
                _shiftMinusButton.clicked += () => OnShiftClicked?.Invoke(-1);

            if (_shiftPlusButton != null)
                _shiftPlusButton.clicked += () => OnShiftClicked?.Invoke(1);

            if (_shiftSegment != null)
                _shiftSegment.RegisterValueChangedCallback(_ => OnShiftSegmentChanged?.Invoke());

            if (_timelineSlider != null)
            {
                _timelineSlider.RegisterValueChangedCallback(evt =>
                {
                    OnTimelineValueChanged?.Invoke(evt.newValue);
                });

                _timelineSlider.RegisterCallback<MouseDownEvent>(_ =>
                    OnTimelineDragStarted?.Invoke());

                _timelineSlider.RegisterCallback<MouseUpEvent>(_ =>
                    OnTimelineDragEnded?.Invoke(), TrickleDown.TrickleDown);

                _timelineSlider.RegisterCallback<MouseLeaveEvent>(_ =>
                    OnTimelineDragEnded?.Invoke());
            }

            if (_root != null)
            {
                _root.RegisterCallback<MouseUpEvent>(_ =>
                    OnTimelineDragEnded?.Invoke(), TrickleDown.TrickleDown);
            }
        }

        // ── 显示更新方法 ────────────────────────────────────────────────────

        public void SetPlayButtonState(bool isPlaying)
        {
            if (_playButton != null)
                _playButton.text = isPlaying ? "⏸ Pause" : "▶ Play";
        }

        public void SetTimeLabel(string text)
        {
            if (_timeLabel != null) _timeLabel.text = text;
        }

        public void SetBpmLabel(string text)
        {
            if (_bpmLabel != null) _bpmLabel.text = text;
        }

        public void SetNoteIndexLabel(string text)
        {
            if (_noteIndexLabel != null) _noteIndexLabel.text = text;
        }

        public void SetTimelineRange(float low, float high)
        {
            if (_timelineSlider == null) return;
            _timelineSlider.lowValue = low;
            _timelineSlider.highValue = high;
            _timelineSlider.value = low;
        }

        public void SetTimelineValue(float value)
        {
            _timelineSlider?.SetValueWithoutNotify(value);
        }

        public void SetMarkCurrentButtonEnabled(bool enabled)
        {
            _markCurrentButton?.SetEnabled(enabled);
        }

        public void UpdateWaveformDisplay(AudioSamplingData data, float[] audioSamples)
        {
            if (_waveformContainer == null) return;

            _waveformContainer.Clear();

            if (data == null || audioSamples == null) return;

            _waveformElement = new WaveformVisualElement(data, audioSamples);
            _waveformElement.OnNoteClicked += (segIdx, noteIdx) =>
                OnNoteClicked?.Invoke(segIdx, noteIdx);
            _waveformContainer.Add(_waveformElement);

            // 播放头
            _playhead = new VisualElement();
            _playhead.AddToClassList("playhead");
            _waveformContainer.Add(_playhead);

            _waveformElement.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                float h = _waveformElement.resolvedStyle.height;
                if (h > 0) _playhead.style.height = h;
            });

            // 点击留白区域 → 标记当前
            _waveformContainer.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.target == _waveformContainer)
                    OnWaveformBlankClicked?.Invoke();
            }, TrickleDown.TrickleDown);
        }

        public void ClearWaveformDisplay()
        {
            if (_waveformContainer == null) return;

            _waveformContainer.Clear();
            _waveformElement = null;
            _playhead = null;

            var hintLabel = new Label("请选择 AudioSamplingData 资源文件");
            hintLabel.name = "hint-label";
            hintLabel.AddToClassList("hint-label");
            _waveformContainer.Add(hintLabel);
        }

        public void SetPlayheadPosition(float pixelX)
        {
            if (_playhead != null)
                _playhead.style.left = pixelX;
        }

        public void SetScrollOffset(float x)
        {
            if (_waveformContainer != null)
                _waveformContainer.scrollOffset = new Vector2(x, 0);
        }

        public void RefreshNoteMarkedState(int segIdx, int localNoteIdx)
        {
            _waveformElement?.RefreshNoteMarkedState(segIdx, localNoteIdx);
        }

        public void RefreshAllMarkedStates()
        {
            _waveformElement?.RefreshAllMarkedStates();
        }

        public void SetNoteHighlight(int segIdx, int localNoteIdx, bool highlighted)
        {
            _waveformElement?.SetNoteHighlight(segIdx, localNoteIdx, highlighted);
        }

        public void UpdateShiftSegmentInfo(string nameText, int toValue)
        {
            if (_shiftSegmentName != null)
                _shiftSegmentName.text = nameText;
            if (_shiftTo != null)
                _shiftTo.value = toValue;
        }

        public void Dispose()
        {
            if (_waveformElement != null)
            {
                _waveformElement.OnNoteClicked -= (_1, _2) => { };
                _waveformElement = null;
            }
        }
    }
}
