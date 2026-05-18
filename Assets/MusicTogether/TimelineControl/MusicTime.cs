using System;
using UnityEngine;

namespace MusicTogether.TimelineControl
{
    [Serializable]
    public class MusicTime
    {
        [Tooltip("true: 用 BPM + 音符类型计算时间; false: 直接用秒数")]
        public bool useMusicalTime;

        [Tooltip("秒数（useMusicalTime = false 时生效）")]
        public double seconds;

        [Tooltip("BPM（useMusicalTime = true 时生效）")]
        public double bpm = 120;

        [Tooltip("音符类型（useMusicalTime = true 时生效）")]
        public NoteType noteType = NoteType.Quarter;

        public double ToSeconds()
        {
            if (useMusicalTime)
                return noteType.ToSeconds(bpm);
            return seconds;
        }
    }
}
