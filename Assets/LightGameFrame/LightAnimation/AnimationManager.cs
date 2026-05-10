using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace LightGameFrame.LightAnimation
{
    [ExecuteAlways]
    public class AnimationManager : MonoBehaviour
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

        private void TickForward(double currentTime)
        {
            int promote = 0;
            for (int i = 0; i < _waitingClips.Count; i++)
            {
                if (currentTime >= _waitingClips[i].BeginTime)
                    promote++;
                else
                    break;
            }
            for (int i = 0; i < promote; i++)
            {
                var clip = _waitingClips[i];
                clip.IsActive = true;
                _playingClips.Add(clip);
            }
            if (promote > 0) _waitingClips.RemoveRange(0, promote);

            for (int i = _playingClips.Count - 1; i >= 0; i--)
            {
                var clip = _playingClips[i];
                if (currentTime > clip.EndTime)
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

        private void TickReverse(double currentTime)
        {
            int promote = 0;
            for (int i = _endedClips.Count - 1; i >= 0; i--)
            {
                if (currentTime <= _endedClips[i].EndTime)
                    promote++;
                else
                    break;
            }
            int promoteStart = _endedClips.Count - promote;
            for (int i = promoteStart; i < _endedClips.Count; i++)
            {
                var clip = _endedClips[i];
                clip.IsActive = true;
                _playingClips.Add(clip);
            }
            if (promote > 0) _endedClips.RemoveRange(promoteStart, promote);

            for (int i = _playingClips.Count - 1; i >= 0; i--)
            {
                var clip = _playingClips[i];
                if (currentTime < clip.BeginTime)
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
