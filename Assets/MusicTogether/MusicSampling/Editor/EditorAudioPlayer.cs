using UnityEngine;
using UnityEditor;
using System;

namespace MusicTogether.MusicSampling.Editor
{
    /// <summary>
    /// Editor 模式下的音频播放控制器
    /// 在 Editor 中播放音频需要创建临时的 GameObject 和 AudioSource
    /// </summary>
    public class EditorAudioPlayer
    {
        // 播放状态枚举
        public enum PlayState
        {
            Stopped,
            Playing,
            Paused
        }

        // 私有字段
        private GameObject _audioObject;
        private AudioSource _audioSource;
        private AudioClip _currentClip;
        private PlayState _playState = PlayState.Stopped;
        private double _pausedTime = 0;
        private bool _isDragging = false;
        private PlayState _stateBeforeDragging = PlayState.Stopped;

        // 事件
        public event Action<double> OnTimeChanged;
        public event Action<PlayState> OnStateChanged;
        /// <summary>当播放位置被强制跳转（Stop/Scrub/CurrentTime.set）时触发，参数为新时间（秒）</summary>
        public event Action<double> OnSeeked;

        // 属性
        public PlayState CurrentState => _playState;
        public bool IsPlaying => _playState == PlayState.Playing;
        public bool IsPaused => _playState == PlayState.Paused;
        public AudioClip CurrentClip => _currentClip;

        /// <summary>
        /// 当前播放时间（秒）
        /// </summary>
        public double CurrentTime
        {
            get
            {
                if (_audioSource != null && _audioSource.clip != null)
                {
                    if (_playState == PlayState.Playing)
                        return _audioSource.time;
                    else if (_playState == PlayState.Paused)
                        return _pausedTime;
                }
                return 0;
            }
            set
            {
                if (_audioSource != null && _audioSource.clip != null)
                {
                    value = Mathf.Clamp((float)value, 0, _audioSource.clip.length);
                    _audioSource.time = (float)value;
                    _pausedTime = value;
                    OnTimeChanged?.Invoke(value);
                    OnSeeked?.Invoke(value);
                }
            }
        }

        /// <summary>
        /// 音频总时长（秒）
        /// </summary>
        public double Duration
        {
            get
            {
                if (_audioSource != null && _audioSource.clip != null)
                    return _audioSource.clip.length;
                return 0;
            }
        }

        /// <summary>
        /// 初始化音频播放器
        /// </summary>
        public void Initialize()
        {
            if (_audioObject == null)
            {
                _audioObject = new GameObject("EditorAudioPlayer");
                _audioObject.hideFlags = HideFlags.HideAndDontSave;
                _audioSource = _audioObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;

                // 注册 Editor Update 回调
                EditorApplication.update += Update;
            }
        }

        /// <summary>
        /// 加载音频剪辑
        /// </summary>
        public bool LoadClip(AudioClip clip)
        {
            if (clip == null)
            {
                Debug.LogWarning("尝试加载空的 AudioClip");
                return false;
            }

            // 检查加载类型
            if (clip.loadType != AudioClipLoadType.DecompressOnLoad)
            {
                Debug.LogError($"AudioClip '{clip.name}' 必须设置为 DecompressOnLoad 加载类型！");
                Debug.Log("请在 Unity 编辑器中选择该音频文件，在 Inspector 面板中将 Load Type 设为 DecompressOnLoad");
                return false;
            }

            Stop();
            _currentClip = clip;
            
            if (_audioSource != null)
            {
                _audioSource.clip = clip;
            }

            return true;
        }

        /// <summary>
        /// 播放
        /// </summary>
        public void Play()
        {
            if (_audioSource == null || _audioSource.clip == null)
                return;

            if (_playState == PlayState.Paused)
            {
                // 从暂停位置继续
                _audioSource.time = (float)_pausedTime;
            }

            _audioSource.Play();
            _playState = PlayState.Playing;
            OnStateChanged?.Invoke(_playState);
        }

        /// <summary>
        /// 暂停
        /// </summary>
        public void Pause()
        {
            if (_audioSource == null || !_audioSource.isPlaying)
                return;

            _pausedTime = _audioSource.time;
            _audioSource.Pause();
            _playState = PlayState.Paused;
            OnStateChanged?.Invoke(_playState);
        }

        /// <summary>
        /// 停止
        /// </summary>
        public void Stop()
        {
            if (_audioSource == null)
                return;

            _audioSource.Stop();
            _pausedTime = 0;
            _playState = PlayState.Stopped;
            CurrentTime = 0;
            OnSeeked?.Invoke(0);
            OnStateChanged?.Invoke(_playState);
        }

        /// <summary>
        /// 切换播放/暂停
        /// </summary>
        public void TogglePlayPause()
        {
            if (_playState == PlayState.Playing)
                Pause();
            else
                Play();
        }

        /// <summary>
        /// 设置是否正在拖拽时间轴
        /// </summary>
        public void SetDragging(bool isDragging)
        {
            if (isDragging && !_isDragging)
            {
                // 开始拖拽：保存当前状态
                _stateBeforeDragging = _playState;
                _isDragging = true;
                
                // 如果正在播放，暂停以实现擦洗效果
                if (_playState == PlayState.Playing && _audioSource != null)
                {
                    _audioSource.Pause();
                }
            }
            else if (!isDragging && _isDragging)
            {
                // 结束拖拽：恢复之前的状态
                _isDragging = false;
                
                // 如果拖拽前是播放状态，恢复播放
                if (_stateBeforeDragging == PlayState.Playing && _audioSource != null)
                {
                    _audioSource.Play();
                    _playState = PlayState.Playing;
                }
            }
        }

        /// <summary>
        /// 音频擦洗（拖拽时预览）
        /// </summary>
        public void Scrub(double time)
        {
            if (_audioSource == null || _audioSource.clip == null)
                return;

            // 确保时间在有效范围内
            time = Mathf.Clamp((float)time, 0, _audioSource.clip.length);
            CurrentTime = time;

            // 播放很短的音频片段用于预览
            if (_isDragging)
            {
                if (!_audioSource.isPlaying)
                {
                    _audioSource.Play();
                    // 延迟暂停以创建短暂预览
                    EditorApplication.delayCall += () =>
                    {
                        if (_audioSource != null && _isDragging)
                        {
                            _audioSource.Pause();
                        }
                    };
                }
            }
        }

        /// <summary>
        /// 每帧更新
        /// </summary>
        private void Update()
        {
            if (_audioSource == null)
                return;

            // 更新播放时间
            if (_playState == PlayState.Playing && !_isDragging)
            {
                OnTimeChanged?.Invoke(_audioSource.time);

                // 检查是否播放完毕
                if (!_audioSource.isPlaying)
                {
                    Stop();
                }
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            Stop();

            EditorApplication.update -= Update;

            if (_audioObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_audioObject);
                _audioObject = null;
                _audioSource = null;
            }

            _currentClip = null;
        }
    }
}
