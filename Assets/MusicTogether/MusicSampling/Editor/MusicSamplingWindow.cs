using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine.Video;

namespace MusicTogether.MusicSampling.Editor
{
    /// <summary>
    /// 音频采样 Editor 窗口
    /// 提供音频播放、波形可视化、音符标记等功能
    /// </summary>
    public class MusicSamplingWindow : EditorWindow
    {
        // 数据和播放器
        private AudioSamplingData _samplingData;
        private EditorAudioPlayer _audioPlayer;
        private float[] _audioSamples;

        // UI 元素
        private ObjectField _dataField;
        private Button _playButton;
        private Button _stopButton;
        private Button _refreshButton;
        private Button _markCurrentButton;
        private Slider _timelineSlider;
        private Label _timeLabel;
        private Label _bpmLabel;
        private Label _noteIndexLabel;
        private ScrollView _waveformContainer;
        private VisualElement _playhead;
        private WaveformVisualElement _waveformElement;

        // 视频播放
        private GameObject _videoObject;
        private VideoPlayer _videoPlayer;
        private RenderTexture _videoRenderTexture;
        private IMGUIContainer _videoContainer;
        private VisualElement _videoArea;

        // 状态
        private bool _isDraggingTimeline = false;
        private (int seg, int note) _currentNote   = (-1, -1);
        // 每个 segIdx 当前高亮的 localNoteIdx（-1 表示未高亮）
        private Dictionary<int, int> _highlightedNotes = new Dictionary<int, int>();

        // Shift 控件
        private IntegerField _shiftSegment;
        private Label _shiftSegmentName;
        private IntegerField _shiftFrom;
        private IntegerField _shiftTo;

        // 平滑滚动状态
        private float _targetScrollX = 0f;
        private float _currentScrollX = 0f;
        private bool _scrollInitialized = false;

        // 常量
        private const float PLAYHEAD_UPDATE_INTERVAL = 0.00f;
        private const float SCROLL_SMOOTH_SPEED = 8f;
        private double _lastUpdateTime = 0;

        // 是否启用指数平滑滚动追踪
        private bool _enableSmoothScroll = true;

        [MenuItem("Window/MusicTogether/Music Sampling Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<MusicSamplingWindow>();
            window.titleContent = new GUIContent("Music Sampling");
            window.minSize = new Vector2(800, 600);
        }

        private void OnEnable()
        {
            // 初始化音频播放器
            _audioPlayer = new EditorAudioPlayer();
            _audioPlayer.Initialize();
            _audioPlayer.OnTimeChanged += OnAudioTimeChanged;
            _audioPlayer.OnStateChanged += OnAudioStateChanged;
            _audioPlayer.OnSeeked += OnAudioSeeked;

            // 注册更新回调用于平滑滚动
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            // 清理资源
            EditorApplication.update -= OnEditorUpdate;

            if (_audioPlayer != null)
            {
                _audioPlayer.OnTimeChanged -= OnAudioTimeChanged;
                _audioPlayer.OnStateChanged -= OnAudioStateChanged;
                _audioPlayer.OnSeeked -= OnAudioSeeked;
                _audioPlayer.Dispose();
                _audioPlayer = null;
            }

            DisposeVideo();
        }

        private void CreateGUI()
        {
            // 加载 UXML（USS 已在 UXML 中绑定）
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/MusicTogether/MusicSampling/Editor/MusicSamplingWindow.uxml");
            
            visualTree.CloneTree(rootVisualElement);

            // 查询 UI 元素
            QueryUIElements();

            // 绑定事件
            BindEvents();
            
            // 全局鼠标抬起事件（安全措施）
            rootVisualElement.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (_isDraggingTimeline)
                {
                    _isDraggingTimeline = false;
                    if (_audioPlayer != null)
                    {
                        _audioPlayer.SetDragging(false);
                    }
                    Debug.Log($"[Timeline] 结束拖拽 (Global) - _isDraggingTimeline = false");
                }
            }, TrickleDown.TrickleDown);
        }

        /// <summary>
        /// 查询 UI 元素
        /// </summary>
        private void QueryUIElements()
        {
            _dataField = rootVisualElement.Q<ObjectField>("data-field");
            _playButton = rootVisualElement.Q<Button>("play-button");
            _stopButton = rootVisualElement.Q<Button>("stop-button");
            _refreshButton = rootVisualElement.Q<Button>("refresh-button");
            _markCurrentButton = rootVisualElement.Q<Button>("mark-current-button");
            _timelineSlider = rootVisualElement.Q<Slider>("timeline-slider");
            _timeLabel = rootVisualElement.Q<Label>("time-label");
            _bpmLabel = rootVisualElement.Q<Label>("bpm-label");
            _noteIndexLabel = rootVisualElement.Q<Label>("note-index-label");
            _waveformContainer = rootVisualElement.Q<ScrollView>("waveform-container");
            _videoArea = rootVisualElement.Q<VisualElement>("VideoArea");

            // 设置 ObjectField 类型
            if (_dataField != null)
            {
                _dataField.objectType = typeof(AudioSamplingData);
            }
            
            // 初始状态下禁用标记按钮
            if (_markCurrentButton != null)
            {
                _markCurrentButton.SetEnabled(false);
            }

            _shiftSegment = rootVisualElement.Q<IntegerField>("shift-segment");
            _shiftSegmentName = rootVisualElement.Q<Label>("shift-segment-name");
            _shiftFrom = rootVisualElement.Q<IntegerField>("shift-from");
            _shiftTo = rootVisualElement.Q<IntegerField>("shift-to");
        }

        /// <summary>
        /// 绑定事件
        /// </summary>
        private void BindEvents()
        {
            if (_dataField != null)
                _dataField.RegisterValueChangedCallback(OnDataChanged);

            if (_playButton != null)
                _playButton.clicked += OnPlayButtonClicked;

            if (_stopButton != null)
                _stopButton.clicked += OnStopButtonClicked;

            if (_refreshButton != null)
                _refreshButton.clicked += OnRefreshButtonClicked;

            if (_markCurrentButton != null)
                _markCurrentButton.clicked += OnMarkCurrentButtonClicked;

            var shiftMinus = rootVisualElement.Q<Button>("shift-minus-button");
            if (shiftMinus != null)
                shiftMinus.clicked += () => OnShiftClicked(-1);

            var shiftPlus = rootVisualElement.Q<Button>("shift-plus-button");
            if (shiftPlus != null)
                shiftPlus.clicked += () => OnShiftClicked(1);

            if (_shiftSegment != null)
                _shiftSegment.RegisterValueChangedCallback(_ => UpdateShiftSegmentInfo());

            if (_timelineSlider != null)
            {
                _timelineSlider.RegisterValueChangedCallback(OnTimelineValueChanged);
                
                // 鼠标按下时开始拖拽
                _timelineSlider.RegisterCallback<MouseDownEvent>(evt =>
                {
                    _isDraggingTimeline = true;
                    _audioPlayer.SetDragging(true);
                    Debug.Log($"[Timeline] 开始拖拽 - _isDraggingTimeline = true");
                });
                
                // 鼠标抬起时结束拖拽（捕获阶段，确保一定能收到）
                _timelineSlider.RegisterCallback<MouseUpEvent>(evt =>
                {
                    _isDraggingTimeline = false;
                    _audioPlayer.SetDragging(false);
                    Debug.Log($"[Timeline] 结束拖拽 (Slider) - _isDraggingTimeline = false");
                }, TrickleDown.TrickleDown);
                
                // 额外的安全措施：鼠标离开编辑器窗口时也结束拖拽
                _timelineSlider.RegisterCallback<MouseLeaveEvent>(evt =>
                {
                    if (_isDraggingTimeline)
                    {
                        _isDraggingTimeline = false;
                        _audioPlayer.SetDragging(false);
                        Debug.Log($"[Timeline] 结束拖拽 (MouseLeave) - _isDraggingTimeline = false");
                    }
                });
            }
        }

        /// <summary>
        /// 数据文件改变事件
        /// </summary>
        private void OnDataChanged(ChangeEvent<UnityEngine.Object> evt)
        {
            _samplingData = evt.newValue as AudioSamplingData;
            
            if (_samplingData != null && _samplingData.audioClip != null)
            {
                LoadAudioData();
                UpdateWaveformDisplay();
                UpdateInfoLabels();
                LoadVideo(_samplingData.referenceVideo);

                // 启用标记按钮
                if (_markCurrentButton != null)
                    _markCurrentButton.SetEnabled(true);

                UpdateShiftSegmentInfo();
            }
            else
            {
                ClearWaveformDisplay();
                LoadVideo(null);

                // 禁用标记按钮
                if (_markCurrentButton != null)
                    _markCurrentButton.SetEnabled(false);
            }
        }

        /// <summary>
        /// 加载音频数据
        /// </summary>
        private void LoadAudioData()
        {
            if (_samplingData == null || _samplingData.audioClip == null)
                return;

            // 加载到播放器
            if (!_audioPlayer.LoadClip(_samplingData.audioClip))
                return;

            // 更新时间轴范围
            _timelineSlider.lowValue = 0;
            _timelineSlider.highValue = (float)_audioPlayer.Duration;
            _timelineSlider.value = 0;

            // 提取音频采样数据
            ExtractAudioSamples();
        }

        /// <summary>
        /// 提取音频采样数据
        /// </summary>
        private void ExtractAudioSamples()
        {
            var clip = _samplingData.audioClip;
            if (clip == null) return;

            try
            {
                int totalSamples = clip.samples * clip.channels;
                _audioSamples = new float[totalSamples];
                
                if (!clip.GetData(_audioSamples, 0))
                {
                    Debug.LogError("音频数据读取失败！");
                    _audioSamples = null;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"音频采样异常: {e.Message}");
                _audioSamples = null;
            }
        }

        /// <summary>
        /// 更新波形显示
        /// </summary>
        private void UpdateWaveformDisplay()
        {
            _waveformContainer.Clear();

            if (_samplingData == null || _audioSamples == null)
                return;

            // 创建波形可视化元素
            _waveformElement = new WaveformVisualElement(_samplingData, _audioSamples);
            _waveformElement.OnNoteClicked += OnNoteClicked;
            _waveformContainer.Add(_waveformElement);

            // 创建播放头（高度覆盖所有轨道，等布局完成后调整）
            _playhead = new VisualElement();
            _playhead.AddToClassList("playhead");
            _waveformContainer.Add(_playhead);

            // 布局完成后将播放头高度对齐到波形实际高度
            _waveformElement.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                float waveformH = _waveformElement.resolvedStyle.height;
                if (waveformH > 0)
                    _playhead.style.height = waveformH;
            });

            // 添加点击留白区域选中当前音符的功能
            _waveformContainer.RegisterCallback<MouseDownEvent>(OnWaveformContainerClicked, TrickleDown.TrickleDown);
        }

        /// <summary>
        /// 清除波形显示
        /// </summary>
        private void ClearWaveformDisplay()
        {
            _waveformContainer.Clear();
            var hintLabel = new Label("请选择 AudioSamplingData 资源文件");
            hintLabel.name = "hint-label";
            hintLabel.AddToClassList("hint-label");
            _waveformContainer.Add(hintLabel);
        }

        /// <summary>
        /// 播放按钮点击
        /// </summary>
        private void OnPlayButtonClicked()
        {
            if (_audioPlayer == null || _samplingData == null)
                return;

            _audioPlayer.TogglePlayPause();
        }

        /// <summary>
        /// 停止按钮点击
        /// </summary>
        private void OnStopButtonClicked()
        {
            if (_audioPlayer == null)
                return;

            _audioPlayer.Stop();
            // 重置平滑滚动状态，让下次播放从头开始
            _targetScrollX = 0f;
            _currentScrollX = 0f;
            _scrollInitialized = false;
        }

        /// <summary>
        /// 刷新按钮点击 —— 重新提取音频采样数据并重建波形显示。
        /// 适用于在 Inspector 中修改了 AudioSamplingData 的段落配置后手动刷新。
        /// 刷新后尝试恢复刷新前的播放进度和播放状态。
        /// </summary>
        private void OnRefreshButtonClicked()
        {
            if (_samplingData == null || _samplingData.audioClip == null) return;

            // 记录刷新前的播放进度与状态
            double savedTime    = _audioPlayer?.CurrentTime ?? 0.0;
            bool   wasPlaying   = _audioPlayer?.IsPlaying  ?? false;

            // 暂停（不 Stop，避免重置 CurrentTime）
            if (wasPlaying) _audioPlayer?.Pause();

            _highlightedNotes.Clear();
            _currentNote = (-1, -1);

            // 重新加载音频并重建波形
            LoadAudioData();
            UpdateWaveformDisplay();
            UpdateInfoLabels();

            // 恢复进度：将时间轴与播放器都定位到原来的时刻
            if (_audioPlayer != null && savedTime > 0.0)
            {
                // 限制 savedTime 不超过新的音频总时长
                double clampedTime = Math.Min(savedTime, _audioPlayer.Duration);
                _audioPlayer.CurrentTime = clampedTime;

                // 同步 slider 显示
                if (_timelineSlider != null)
                    _timelineSlider.SetValueWithoutNotify((float)clampedTime);

                // 同步平滑滚动目标到原位置
                if (_samplingData != null)
                {
                    float exactPixelX = _samplingData.GetPixelXAtTime(clampedTime);
                    _targetScrollX  = Mathf.Max(0, exactPixelX - (_waveformContainer?.contentRect.width ?? 0) / 2);
                    _currentScrollX = _targetScrollX;
                    _scrollInitialized = true;

                    if (_waveformContainer != null)
                        _waveformContainer.scrollOffset = new Vector2(_currentScrollX, 0);
                }
            }
            else
            {
                _targetScrollX    = 0f;
                _currentScrollX   = 0f;
                _scrollInitialized = false;
            }

            // 如果原本在播放，则继续播放
            if (wasPlaying) _audioPlayer?.Play();
        }

        /// <summary>
        /// 标记当前音符按钮点击 —— 对所有当前活跃段的当前音符执行只加不减的标记。
        /// </summary>
        private void OnMarkCurrentButtonClicked()
        {
            if (_samplingData == null || _audioPlayer == null) return;
            MarkCurrentNotes(_audioPlayer.CurrentTime);
        }

        /// <summary>
        /// Shift 按钮点击：对指定段落的标记音符索引执行批量位移。
        /// </summary>
        private void OnShiftClicked(int offset)
        {
            if (_samplingData == null || _audioPlayer == null) return;

            int segIdx = _shiftSegment?.value ?? 0;
            int from = _shiftFrom?.value ?? 0;
            int to = _shiftTo?.value ?? 0;

            _samplingData.ShiftMarkedNoteIndices(segIdx, from, to, offset);
            EditorUtility.SetDirty(_samplingData);

            _waveformElement?.RefreshAllMarkedStates();
        }

        /// <summary>
        /// 根据 shift-segment 字段刷新段名显示和 To 默认值。
        /// </summary>
        private void UpdateShiftSegmentInfo()
        {
            if (_samplingData == null || _shiftSegment == null) return;

            int segIdx = _shiftSegment.value;
            if (segIdx >= 0 && segIdx < _samplingData.segments.Count)
            {
                var seg = _samplingData.segments[segIdx];
                _shiftSegmentName.text = $"[{seg.name}]";
                _shiftTo.value = _samplingData.GetSegmentTotalNotes(segIdx);
            }
            else
            {
                _shiftSegmentName.text = "(无效)";
            }
        }

        /// <summary>
        /// 时间轴值改变
        /// </summary>
        private void OnTimelineValueChanged(ChangeEvent<float> evt)
        {
            if (_audioPlayer == null || _isDraggingTimeline == false)
                return;

            // 拖拽时实现音频擦洗
            _audioPlayer.Scrub(evt.newValue);
        }

        /// <summary>
        /// 音频时间改变回调
        /// </summary>
        private void OnAudioTimeChanged(double time)
        {
            // 节流更新，避免过于频繁
            if (EditorApplication.timeSinceStartup - _lastUpdateTime < PLAYHEAD_UPDATE_INTERVAL)
                return;

            _lastUpdateTime = EditorApplication.timeSinceStartup;

            // 更新时间轴（非拖拽状态下）
            if (!_isDraggingTimeline)
            {
                _timelineSlider.SetValueWithoutNotify((float)time);
            }

            // 更新播放头位置和滚动目标
            if (_playhead != null && _samplingData != null)
            {
                // 时间直接映射像素：段落位置已与时间轴对齐，无需复杂跨段计算
                float exactPixelX = _samplingData.GetPixelXAtTime(time);

                _playhead.style.left = exactPixelX;

                // 间隙期间不高亮任何音符，否则高亮所有活跃段
                if (_samplingData.IsTimeInGap(time))
                    UpdateHighlightedNotes(null);
                else
                    UpdateHighlightedNotes(_samplingData.GetAllActiveNotesAtTime(time));

                // 只写入目标值，实际滚动由 OnEditorUpdate 平滑完成
                if (_waveformContainer != null)
                {
                    float raw = exactPixelX - _waveformContainer.contentRect.width / 2;
                    _targetScrollX = Mathf.Max(0, raw);
                    if (!_scrollInitialized)
                    {
                        _currentScrollX = _targetScrollX;
                        _scrollInitialized = true;
                    }
                }
            }

            // 更新信息标签
            UpdateInfoLabels();

            // 检查当前音符
            CheckCurrentNote(time);
        }

        /// <summary>
        /// Editor 更新回调（用于平滑滚动）
        /// </summary>
        private void OnEditorUpdate()
        {
            if (_waveformContainer == null || _isDraggingTimeline)
                return;

            if (_enableSmoothScroll)
            {
                // 指数平滑追踪：1 - e^(-k * dt)
                float dt = (float)(EditorApplication.timeSinceStartup - _lastUpdateTime);
                dt = Mathf.Clamp(dt, 0.001f, 0.05f);
                _currentScrollX = Mathf.Lerp(_currentScrollX, _targetScrollX, 1f - Mathf.Exp(-SCROLL_SMOOTH_SPEED * dt));

                // 只有差异超过 0.5px 才写入，避免无意义的脏帧
                if (Mathf.Abs(_currentScrollX - _waveformContainer.scrollOffset.x) > 0.5f)
                {
                    _waveformContainer.scrollOffset = new Vector2(_currentScrollX, 0);
                }
            }
            else
            {
                // 直接跳到目标位置（无平滑）
                _currentScrollX = _targetScrollX;
                _waveformContainer.scrollOffset = new Vector2(_currentScrollX, 0);
            }
        }

        /// <summary>
        /// 更新所有高亮音符。activeNotes 为当前时刻所有活跃段的 (segIdx, localNoteIdx) 列表；
        /// 传 null 或空列表表示清除所有高亮。
        /// </summary>
        private void UpdateHighlightedNotes(System.Collections.Generic.List<(int seg, int note)> activeNotes)
        {
            if (_waveformElement == null) return;

            // 清除已不在活跃列表中的旧高亮
            var toRemove = new System.Collections.Generic.List<int>();
            foreach (var kv in _highlightedNotes)
            {
                int segIdx    = kv.Key;
                int noteIdx   = kv.Value;
                bool stillActive = activeNotes != null &&
                                   activeNotes.Exists(n => n.seg == segIdx && n.note == noteIdx);
                if (!stillActive)
                {
                    _waveformElement.SetNoteHighlight(segIdx, noteIdx, false);
                    toRemove.Add(segIdx);
                }
            }
            foreach (var s in toRemove) _highlightedNotes.Remove(s);

            // 点亮新增的活跃音符
            if (activeNotes == null) return;
            foreach (var (segIdx, noteIdx) in activeNotes)
            {
                if (_highlightedNotes.TryGetValue(segIdx, out int prev) && prev == noteIdx)
                    continue; // 没变化，跳过

                // 先摘掉同 seg 的旧高亮
                if (_highlightedNotes.TryGetValue(segIdx, out int oldNote))
                    _waveformElement.SetNoteHighlight(segIdx, oldNote, false);

                _waveformElement.SetNoteHighlight(segIdx, noteIdx, true);
                _highlightedNotes[segIdx] = noteIdx;
            }
        }

        /// <summary>
        /// 音频状态改变回调
        /// </summary>
        private void OnAudioStateChanged(EditorAudioPlayer.PlayState state)
        {
            switch (state)
            {
                case EditorAudioPlayer.PlayState.Playing:
                    _playButton.text = "⏸ Pause";
                    SyncVideoState(true);
                    break;
                case EditorAudioPlayer.PlayState.Paused:
                    _playButton.text = "▶ Play";
                    SyncVideoState(false);
                    break;
                case EditorAudioPlayer.PlayState.Stopped:
                    _playButton.text = "▶ Play";
                    SyncVideoState(false);
                    break;
            }
        }

        // ── 视频同步 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 加载（或卸载）参考视频。videoClip 为 null 时显示提示，否则初始化 VideoPlayer。
        /// </summary>
        private void LoadVideo(VideoClip videoClip)
        {
            DisposeVideo();

            if (_videoArea == null) return;

            if (videoClip == null)
            {
                // 无视频：隐藏整个区域
                _videoArea.AddToClassList("hidden");
                return;
            }

            // 有视频：显示区域
            _videoArea.RemoveFromClassList("hidden");

            // 创建 VideoPlayer GameObject（先不设 targetTexture，等 Prepare 完成后按真实尺寸创建 RenderTexture）
            _videoObject = new GameObject("EditorVideoPlayer");
            _videoObject.hideFlags = HideFlags.HideAndDontSave;
            _videoPlayer = _videoObject.AddComponent<VideoPlayer>();
            _videoPlayer.clip = videoClip;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.None; // 静音
            _videoPlayer.playOnAwake = false;
            _videoPlayer.isLooping = false;

            // Prepare 完成后再用视频真实尺寸创建 RenderTexture，避免竖屏/横屏尺寸错误
            _videoPlayer.prepareCompleted += OnVideoPrepareCompleted;
            _videoPlayer.Prepare();

            // IMGUIContainer 是 UIToolkit 中渲染 RenderTexture 的唯一方式，无法在 UXML 中静态声明
            _videoContainer = new IMGUIContainer(() =>
            {
                if (_videoRenderTexture != null)
                    GUI.DrawTexture(
                        new Rect(0, 0, _videoContainer.resolvedStyle.width, _videoContainer.resolvedStyle.height),
                        _videoRenderTexture, ScaleMode.ScaleToFit);
            });
            _videoContainer.AddToClassList("video-display");
            _videoArea.Add(_videoContainer);
        }

        /// <summary>
        /// VideoPlayer Prepare 完成回调：用视频真实宽高创建 RenderTexture 并绑定。
        /// </summary>
        private void OnVideoPrepareCompleted(VideoPlayer vp)
        {
            vp.prepareCompleted -= OnVideoPrepareCompleted;

            uint w = vp.texture != null ? (uint)vp.texture.width  : vp.width;
            uint h = vp.texture != null ? (uint)vp.texture.height : vp.height;
            if (w == 0 || h == 0) { w = 1080; h = 1920; } // 兜底

            // 销毁旧的 RenderTexture（如有）
            if (_videoRenderTexture != null)
            {
                _videoRenderTexture.Release();
                UnityEngine.Object.DestroyImmediate(_videoRenderTexture);
            }

            _videoRenderTexture = new RenderTexture((int)w, (int)h, 0);
            _videoRenderTexture.Create();
            vp.targetTexture = _videoRenderTexture;
        }

        /// <summary>
        /// 根据音频播放状态同步视频播放/暂停。
        /// </summary>
        private void SyncVideoState(bool playing)
        {
            if (_videoPlayer == null) return;

            if (playing)
            {
                // 先对齐时间再播放
                if (_audioPlayer != null)
                    _videoPlayer.time = _audioPlayer.CurrentTime;
                _videoPlayer.Play();
            }
            else
            {
                _videoPlayer.Pause();
            }
        }

        /// <summary>
        /// 音频跳转（Stop/Scrub）时同步视频时间。
        /// </summary>
        private void OnAudioSeeked(double time)
        {
            if (_videoPlayer == null) return;
            _videoPlayer.time = time;
            // 暂停一帧后更新，确保画面刷新
            _videoPlayer.Play();
            EditorApplication.delayCall += () =>
            {
                if (_videoPlayer != null && _audioPlayer?.IsPlaying == false)
                    _videoPlayer.Pause();
            };
        }

        /// <summary>
        /// 销毁视频相关资源。
        /// </summary>
        private void DisposeVideo()
        {
            if (_videoContainer != null)
            {
                _videoContainer.RemoveFromHierarchy();
                _videoContainer = null;
            }

            if (_videoPlayer != null)
            {
                _videoPlayer.prepareCompleted -= OnVideoPrepareCompleted;
                _videoPlayer.Stop();
                _videoPlayer = null;
            }

            if (_videoObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_videoObject);
                _videoObject = null;
            }

            if (_videoRenderTexture != null)
            {
                _videoRenderTexture.Release();
                UnityEngine.Object.DestroyImmediate(_videoRenderTexture);
                _videoRenderTexture = null;
            }
        }

        /// <summary>
        /// 更新信息标签
        /// </summary>
        private void UpdateInfoLabels()
        {
            if (_audioPlayer == null || _samplingData == null) return;

            double currentTime = _audioPlayer.CurrentTime;
            double duration    = _audioPlayer.Duration;

            _timeLabel.text = $"{FormatTime(currentTime)} / {FormatTime(duration)}";

            var (segIdx, localNoteIdx) = _samplingData.GetSegmentNoteAtTime(currentTime);

            if (_samplingData.segments != null && segIdx < _samplingData.segments.Count)
            {
                var seg        = _samplingData.segments[segIdx];
                int barIndex   = localNoteIdx / seg.NotesPerBar;
                int noteInBar  = localNoteIdx % seg.NotesPerBar;

                _bpmLabel.text      = $"[{segIdx + 1}] {seg.name}  BPM: {seg.bpm} ({seg.beatsPerBar}/{seg.beatDivision})";
                _noteIndexLabel.text = $"小节: {barIndex + 1} | 音符: {localNoteIdx} ({noteInBar + 1}/{seg.NotesPerBar})";
            }
        }

        /// <summary>
        /// 格式化时间显示
        /// </summary>
        private string FormatTime(double time)
        {
            int minutes = (int)(time / 60);
            int seconds = (int)(time % 60);
            int milliseconds = (int)((time % 1) * 1000);
            return $"{minutes:D2}:{seconds:D2}.{milliseconds:D3}";
        }

        /// <summary>
        /// 检查当前音符并触发标记
        /// </summary>
        private void CheckCurrentNote(double time)
        {
            if (_samplingData == null) return;

            var note = _samplingData.GetSegmentNoteAtTime(time);
            if (note != _currentNote)
                _currentNote = note;
        }

        /// <summary>
        /// 音符点击事件（直接点击音符格）—— Toggle 标记状态。
        /// </summary>
        private void OnNoteClicked(int segIdx, int localNoteIdx)
        {
            if (_samplingData == null) return;

            _samplingData.ToggleMarkedNote(segIdx, localNoteIdx);
            EditorUtility.SetDirty(_samplingData);

            if (_waveformElement != null)
                _waveformElement.RefreshNoteMarkedState(segIdx, localNoteIdx);
        }

        /// <summary>
        /// 对当前时刻所有活跃段的当前音符执行只加不减的标记。
        /// 用于按钮/快捷键/点击留白区域触发的"标记当前"操作。
        /// </summary>
        private void MarkCurrentNotes(double time)
        {
            if (_samplingData == null) return;

            var activeNotes = _samplingData.GetAllActiveNotesAtTime(time);
            foreach (var (segIdx, localNoteIdx) in activeNotes)
            {
                if (_samplingData.IsNoteMarked(segIdx, localNoteIdx)) continue; // 已标记则跳过

                _samplingData.AddMarkedNote(segIdx, localNoteIdx);

                if (_waveformElement != null)
                    _waveformElement.RefreshNoteMarkedState(segIdx, localNoteIdx);
            }

            if (activeNotes.Count > 0)
                EditorUtility.SetDirty(_samplingData);
        }

        /// <summary>
        /// 波形容器点击事件（点击留白区域）
        /// </summary>
        private void OnWaveformContainerClicked(MouseDownEvent evt)
        {
            if (evt.target == _waveformContainer && _samplingData != null && _audioPlayer != null)
            {
                MarkCurrentNotes(_audioPlayer.CurrentTime);
            }
        }

        /// <summary>
        /// 处理键盘输入
        /// </summary>
        private void Update()
        {
            if (_samplingData == null || _audioPlayer == null) return;

            if (Event.current != null && Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Space)
                {
                    MarkCurrentNotes(_audioPlayer.CurrentTime);
                    Event.current.Use();
                }
            }
        }
    }
}
