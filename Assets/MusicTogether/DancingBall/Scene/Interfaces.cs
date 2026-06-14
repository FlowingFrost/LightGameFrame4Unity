using System;
using LightGameFrame.LightAnimation;
using LightGameFrame.LightAnimation.Timeline;
using MusicTogether.DancingBall.Data;
using MusicTogether.DancingBall.SceneOld;
using UnityEngine;

namespace MusicTogether.DancingBall.Scene
{
    public interface ITileHolder
    {
        public GameObject TileObject { get; }
        public CompositeClip TileAnimations { get; }
    }

    public interface ITile
    {
        //外部引用
        public IRoad Road { get; }
        //本体绑定信息
        public Transform Transform { get; }
        public ITileHolder TileHolder { get; }
        //参数
        public int BlockLocalIndex { get; set; }
        public int TargetNoteIndex { get; set; }
        [Obsolete]public bool IsDataValid { get; }
        //数据
        public ITileDisplacementData DisplacementData { get; }
        //方法
        public void Initialize(IRoad road, int blockLocalIndex, int targetNoteIndex);
        public void SetDisplacementData(ITileDisplacementData data);
    }
}