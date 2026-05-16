using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace LightGameFrame.LightAnimation
{
    /// <summary>
    /// 动画管理器基类。
    /// 持有三态列表 + Tick 状态机，边界判定由子类通过虚方法实现。
    /// </summary>
    [ExecuteAlways]
    public abstract class AnimationManagerBase : MonoBehaviour
    {
        [SerializeReference, ListDrawerSettings(DefaultExpandedState = true)]
        private List<IAnimationClip> _clips = new List<IAnimationClip>();

        private readonly List<IAnimationClip> _pendingAdd = new List<IAnimationClip>();
        private readonly List<IAnimationClip> _pendingRemove = new List<IAnimationClip>();

        private readonly List<IAnimationClip> _waitingClips = new List<IAnimationClip>();
        private readonly List<IAnimationClip> _playingClips = new List<IAnimationClip>();
        private readonly List<IAnimationClip> _endedClips = new List<IAnimationClip>();

        private double _lastTickTime = double.MinValue;
        private bool _initialized;

        private static readonly Comparison<IAnimationClip> ByBeginTimeAsc =
            (a, b) => a.BeginTime.CompareTo(b.BeginTime);
        private static readonly Comparison<IAnimationClip> ByEndTimeAsc =
            (a, b) => a.EndTime.CompareTo(b.EndTime);

        // ────────────── 子类可覆写的边界判定 ──────────────

        protected abstract bool ShouldWaitingToPlaying(IAnimationClip clip, double currentTime);
        protected abstract bool ShouldPlayingToEnded(IAnimationClip clip, double currentTime);
        protected abstract bool ShouldEndedToPlaying(IAnimationClip clip, double currentTime);
        protected abstract bool ShouldPlayingToWaiting(IAnimationClip clip, double currentTime);

        // ────────────── 外部 API ──────────────

        public void LoadClips(IEnumerable<IAnimationClip> clips)
        {
            _clips.Clear();
            _clips.AddRange(clips);
        }

        public void Register(IAnimationClip clip)
        {
            if (clip != null) _pendingAdd.Add(clip);
        }

        public void RegisterRange(IEnumerable<IAnimationClip> clips)
        {
            foreach (var c in clips)
                if (c != null) _pendingAdd.Add(c);
        }

        public void Unregister(IAnimationClip clip)
        {
            if (clip != null) _pendingRemove.Add(clip);
        }

        public void Clear()
        {
            foreach (var c in _playingClips) c.Reset();
            foreach (var c in _endedClips) c.Reset();
            foreach (var c in _waitingClips) c.Reset();

            _waitingClips.Clear();
            _playingClips.Clear();
            _endedClips.Clear();
            _pendingAdd.Clear();
            _pendingRemove.Clear();
            _clips.Clear();
            _initialized = false;
            _lastTickTime = double.MinValue;
        }

        public void Initialize(double currentTime)
        {
            if (_initialized) return;
            _initialized = true;

            foreach (var clip in _clips)
            {
                if (clip == null) continue;
                PlaceInSortedState(clip, currentTime);
                clip.CaptureOriginal();
            }
        }

        public void Tick(double currentTime)
        {
            if (!_initialized)
                Initialize(currentTime);

            FlushPending(currentTime);

            if (currentTime >= _lastTickTime)
                TickForward(currentTime);
            else
                TickReverse(currentTime);

            _lastTickTime = currentTime;
        }

        // ────────────── 正放：1→2→3 ──────────────

        private void TickForward(double currentTime)
        {
            for (int i = _waitingClips.Count - 1; i >= 0; i--)
            {
                var clip = _waitingClips[i];
                if (ShouldWaitingToPlaying(clip, currentTime))
                {
                    _waitingClips.RemoveAt(i);
                    clip.IsActive = true;
                    _playingClips.Add(clip);
                }
            }

            for (int i = _playingClips.Count - 1; i >= 0; i--)
            {
                var clip = _playingClips[i];
                if (ShouldPlayingToEnded(clip, currentTime))
                {
                    clip.Apply(1.0);
                    clip.IsActive = false;
                    _playingClips.RemoveAt(i);
                    SortedInsert(_endedClips, clip, ByEndTimeAsc);
                }
                else
                {
                    double progress = clip.Duration > 0
                        ? (currentTime - clip.BeginTime) / clip.Duration
                        : 1.0;
                    clip.Apply(Math.Max(0.0, Math.Min(1.0, progress)));
                }
            }
        }

        // ────────────── 倒放：3→2→1 ──────────────

        private void TickReverse(double currentTime)
        {
            for (int i = _endedClips.Count - 1; i >= 0; i--)
            {
                var clip = _endedClips[i];
                if (ShouldEndedToPlaying(clip, currentTime))
                {
                    _endedClips.RemoveAt(i);
                    clip.IsActive = true;
                    _playingClips.Add(clip);
                }
            }

            for (int i = _playingClips.Count - 1; i >= 0; i--)
            {
                var clip = _playingClips[i];
                if (ShouldPlayingToWaiting(clip, currentTime))
                {
                    clip.Reset();
                    clip.IsActive = false;
                    _playingClips.RemoveAt(i);
                    SortedInsert(_waitingClips, clip, ByBeginTimeAsc);
                }
                else
                {
                    double progress = clip.Duration > 0
                        ? (currentTime - clip.BeginTime) / clip.Duration
                        : 1.0;
                    clip.Apply(Math.Max(0.0, Math.Min(1.0, progress)));
                }
            }
        }

        // ────────────── 内部 ──────────────

        private enum ClipState { Pending, Active, Finished }

        private static ClipState GetState(IAnimationClip clip, double time)
        {
            if (time < clip.BeginTime) return ClipState.Pending;
            if (time > clip.EndTime) return ClipState.Finished;
            return ClipState.Active;
        }

        private void PlaceInSortedState(IAnimationClip clip, double currentTime)
        {
            switch (GetState(clip, currentTime))
            {
                case ClipState.Pending:
                    SortedInsert(_waitingClips, clip, ByBeginTimeAsc);
                    break;
                case ClipState.Active:
                    _playingClips.Add(clip);
                    clip.IsActive = true;
                    break;
                case ClipState.Finished:
                    SortedInsert(_endedClips, clip, ByEndTimeAsc);
                    break;
            }
        }

        private void FlushPending(double currentTime)
        {
            foreach (var c in _pendingAdd)
            {
                _clips.Add(c);
                c.CaptureOriginal();
                PlaceInSortedState(c, currentTime);
            }
            _pendingAdd.Clear();

            foreach (var c in _pendingRemove)
            {
                c.Reset();
                _waitingClips.Remove(c);
                _playingClips.Remove(c);
                _endedClips.Remove(c);
                _clips.Remove(c);
            }
            _pendingRemove.Clear();
        }

        private static void SortedInsert(List<IAnimationClip> list, IAnimationClip item, Comparison<IAnimationClip> comparer)
        {
            int index = list.BinarySearch(item, Comparer<IAnimationClip>.Create(comparer));
            if (index < 0) index = ~index;
            list.Insert(index, item);
        }
    }
}
