using System;

namespace MusicTogether.TimelineControl
{
    public enum NoteType
    {
        Whole = 1,
        Half = 2,
        Quarter = 4,
        Eighth = 8,
        Sixteenth = 16,
        Triplet_8th = 83,
        Triplet_16th = 163
    }

    public static class NoteTypeExtensions
    {
        /// <summary>
        /// 将音符类型转为秒数。bpm 为每分钟拍数（quarter note）。
        /// 常规音符: (4 / denominator) * (60 / bpm)
        /// 三连音: 常规时值 * 2/3
        /// </summary>
        public static double ToSeconds(this NoteType note, double bpm)
        {
            if (bpm <= 0) return 0;

            int raw = (int)note;
            bool isTriplet = raw > 50;

            if (isTriplet)
            {
                int denominator = raw % 100; // 83 -> 8, 163 -> 16
                double normal = (4.0 / denominator) * (60.0 / bpm);
                return normal * 2.0 / 3.0;
            }

            return (4.0 / raw) * (60.0 / bpm);
        }
    }
}
