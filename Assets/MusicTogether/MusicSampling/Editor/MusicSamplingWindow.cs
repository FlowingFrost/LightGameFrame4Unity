using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEngine.Video;

namespace MusicTogether.MusicSampling.Editor
{
    /// <summary>
    /// MusicSampling EditorWindow — 纯 Host 角色。
    /// 负责：窗口注册、EditorAudioPlayer 管理、ObjectField 绑定、
    /// 键盘输入、视频播放。业务逻辑全部在 MusicSamplingController 中。
    /// </summary>
    public class MusicSamplingWindow : EditorWindow
    {
        private EditorAudioPlayer _audioPlayer;
        private MusicSamplingController _controller;
        private MusicSamplingUIManager _ui;
        private ObjectField _dataField;

        // 视频
        private GameObject _videoObject;
        private VideoPlayer _videoPlayer;
        private RenderTexture _videoRenderTexture;
        private IMGUIContainer _videoContainer;
        private VisualElement _videoArea;

        [MenuItem("MusicTogether/Music Sampling Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<MusicSamplingWindow>();
            window.titleContent = new GUIContent("Music Sampling");
            window.minSize = new Vector2(800, 600);
        }

        private void OnEnable()
        {
            _audioPlayer = new EditorAudioPlayer();
            _audioPlayer.Initialize();
            _audioPlayer.OnTimeChanged += OnAudioTimeChanged;
            _audioPlayer.OnStateChanged += OnAudioStateChanged;
            _audioPlayer.OnSeeked += OnAudioSeeked;
        }

        private void OnDisable()
        {
            if (_audioPlayer != null)
            {
                _audioPlayer.OnTimeChanged -= OnAudioTimeChanged;
                _audioPlayer.OnStateChanged -= OnAudioStateChanged;
                _audioPlayer.OnSeeked -= OnAudioSeeked;
                _audioPlayer.Dispose();
                _audioPlayer = null;
            }

            DisposeVideo();
            _controller?.Dispose();
            _controller = null;
            _ui = null;
        }

        private void CreateGUI()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/MusicTogether/MusicSampling/MusicSamplingWindow.uxml");
            visualTree.CloneTree(rootVisualElement);

            // 1. 创建 UIManager
            _ui = new MusicSamplingUIManager(rootVisualElement);

            // 2. Host 订阅音频相关 Action
            _ui.OnPlayClicked = OnPlayClicked;
            _ui.OnStopClicked = OnStopClicked;
            _ui.OnRefreshClicked = OnRefreshClicked;
            _ui.OnTimelineValueChanged = OnTimelineScrub;
            _ui.OnTimelineDragStarted = () =>
            {
                _audioPlayer?.SetDragging(true);
                if (_controller != null) _controller.IsDraggingTimeline = true;
            };
            _ui.OnTimelineDragEnded = () =>
            {
                _audioPlayer?.SetDragging(false);
                if (_controller != null) _controller.IsDraggingTimeline = false;
            };

            // 3. 创建 Controller，传入 UIManager
            _controller = new MusicSamplingController();
            _controller.Bind(_ui);
            _controller.OnDataModified += () => EditorUtility.SetDirty(_controller.Data);

            // 4. ObjectField（Editor-only）
            _dataField = rootVisualElement.Q<ObjectField>("data-field");
            if (_dataField != null)
            {
                _dataField.objectType = typeof(AudioSamplingData);
                _dataField.RegisterValueChangedCallback(OnDataFieldChanged);
            }

            // 5. 视频区域引用
            _videoArea = rootVisualElement.Q<VisualElement>("VideoArea");

            // 6. 键盘输入
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        // ── 音频 Action 处理 ─────────────────────────────────────────────────

        private void OnPlayClicked()
        {
            _audioPlayer?.TogglePlayPause();
        }

        private void OnStopClicked()
        {
            _audioPlayer?.Stop();
            _controller?.OnSeeked();
        }

        private void OnRefreshClicked()
        {
            if (_controller?.Data == null || _controller.Data.audioClip == null) return;

            double savedTime = _audioPlayer?.CurrentTime ?? 0.0;
            bool wasPlaying = _audioPlayer?.IsPlaying ?? false;

            if (wasPlaying) _audioPlayer?.Pause();

            _controller.LoadData(_controller.Data);

            if (_audioPlayer != null)
            {
                _audioPlayer.LoadClip(_controller.Data.audioClip);

                if (savedTime > 0.0)
                {
                    double clamped = System.Math.Min(savedTime, _audioPlayer.Duration);
                    _audioPlayer.CurrentTime = clamped;
                }
            }

            if (wasPlaying) _audioPlayer?.Play();
        }

        private void OnTimelineScrub(float time)
        {
            _audioPlayer?.Scrub(time);
        }

        // ── ObjectField ───────────────────────────────────────────────────────

        private void OnDataFieldChanged(ChangeEvent<Object> evt)
        {
            var data = evt.newValue as AudioSamplingData;
            if (data != null && data.audioClip != null)
            {
                _audioPlayer?.LoadClip(data.audioClip);
                _ui?.SetTimelineRange(0, (float)_audioPlayer.Duration);
                LoadVideo(data.referenceVideo);
            }
            else
            {
                LoadVideo(null);
            }

            _controller?.LoadData(data);
        }

        // ── 音频事件 → Controller ────────────────────────────────────────────

        private void OnAudioTimeChanged(double time)
        {
            _controller?.OnTimeChanged(time);
        }

        private void OnAudioStateChanged(EditorAudioPlayer.PlayState state)
        {
            bool playing = state == EditorAudioPlayer.PlayState.Playing;
            _controller?.OnStateChanged(playing);
            SyncVideoState(playing);
        }

        private void OnAudioSeeked(double time)
        {
            _controller?.OnSeeked();

            if (_videoPlayer == null) return;
            _videoPlayer.time = time;
            _videoPlayer.Play();
            EditorApplication.delayCall += () =>
            {
                if (_videoPlayer != null && _audioPlayer?.IsPlaying == false)
                    _videoPlayer.Pause();
            };
        }

        // ── 键盘 ──────────────────────────────────────────────────────────────

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (_controller?.Data == null) return;

            if (evt.keyCode == KeyCode.Space)
            {
                double time = _audioPlayer?.CurrentTime ?? _controller.CurrentTime;
                _controller.MarkCurrentNotes(time);
                evt.StopPropagation();
            }
        }

        // ── 视频（全部保留在 Host）────────────────────────────────────────────

        private void LoadVideo(VideoClip videoClip)
        {
            DisposeVideo();

            if (_videoArea == null) return;

            if (videoClip == null)
            {
                _videoArea.AddToClassList("hidden");
                return;
            }

            _videoArea.RemoveFromClassList("hidden");

            _videoObject = new GameObject("EditorVideoPlayer");
            _videoObject.hideFlags = HideFlags.HideAndDontSave;
            _videoPlayer = _videoObject.AddComponent<VideoPlayer>();
            _videoPlayer.clip = videoClip;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            _videoPlayer.playOnAwake = false;
            _videoPlayer.isLooping = false;

            _videoPlayer.prepareCompleted += OnVideoPrepareCompleted;
            _videoPlayer.Prepare();

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

        private void OnVideoPrepareCompleted(VideoPlayer vp)
        {
            vp.prepareCompleted -= OnVideoPrepareCompleted;

            uint w = vp.texture != null ? (uint)vp.texture.width  : vp.width;
            uint h = vp.texture != null ? (uint)vp.texture.height : vp.height;
            if (w == 0 || h == 0) { w = 1080; h = 1920; }

            if (_videoRenderTexture != null)
            {
                _videoRenderTexture.Release();
                Object.DestroyImmediate(_videoRenderTexture);
            }

            _videoRenderTexture = new RenderTexture((int)w, (int)h, 0);
            _videoRenderTexture.Create();
            vp.targetTexture = _videoRenderTexture;
        }

        private void SyncVideoState(bool playing)
        {
            if (_videoPlayer == null) return;

            if (playing)
            {
                if (_audioPlayer != null)
                    _videoPlayer.time = _audioPlayer.CurrentTime;
                _videoPlayer.Play();
            }
            else
            {
                _videoPlayer.Pause();
            }
        }

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
                Object.DestroyImmediate(_videoObject);
                _videoObject = null;
            }

            if (_videoRenderTexture != null)
            {
                _videoRenderTexture.Release();
                Object.DestroyImmediate(_videoRenderTexture);
                _videoRenderTexture = null;
            }
        }
    }
}
