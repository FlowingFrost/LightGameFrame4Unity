using System;
using System.Collections.Generic;
using MusicTogether.DancingBall.Data;
using UnityEngine;
using MusicTogether.DancingBall.Player;
using MusicTogether.DancingBall.EditorTool;
using MusicTogether.DancingBall.SceneElements;

namespace MusicTogether.DancingBall.Scene
{
    /// <summary>
    /// 内置在Prefab里面。控制Tile的布局。拓展内容：动画。
    /// </summary>
    public interface ITileHolder
    {
        public void SetTileActive(bool forward, bool backward);
        /// <summary>
        /// 返回所有已启用的地板Transform和他们的厚度。
        /// </summary>
        public List<MovementData> GetTileMovementDatum(double currentBlockTime, double singleBlockDuration, bool blockNeedTap);//由于Tile可能存在移动，这里使用Transform确保实时跟踪最新位置。
    }

    /// <summary>
    /// 自动添加。显示调试信息。为了不让Block过于臃肿，BlockDisplay被设计成一个独立的组件。拓展内容：显示的信息。
    /// </summary>
    public interface IBlockDebug
    {
        public void RefreshBlockDisplay(Color color);
        public void DisplayBlockInformation(string info);
    }
    
    /// <summary>
    /// 自动添加。管理Block的位置，负责完成组件与Road通信。拓展内容：无
    /// </summary>
    public interface IBlock
    {
        //外部引用
        public IRoad Road { get; }//自动创建双向引用。
        //本体绑定信息
        public Transform Transform { get; }
        public ITileHolder TileHolder { get; }
        public IBlockDebug BlockDebugDisplay { get; }
        //参数
        public int BlockLocalIndex { get; set; }
        public bool IsDataValid { get; }
        //函数
        public void Init(IRoad targetRoad, int blockLocalIndex);
        //public List<MovementData> GetBlockMovementData();时间计算由Player完成。
    }

    /// <summary>
    /// 自动添加。管理一段的Block。负责完成组件与Map通信。拓展内容：无
    /// </summary>
    public interface IRoad
    {
        //外部引用
        public IMap Map { get; }
        public RoadData RoadData { get; }
        public string RoadName { get; }
        //本体绑定信息
        public Transform Transform { get; }
        public List<IBlock> Blocks { get; }
        //参数
        public bool IsDataValid { get; }
        //public string TargetRoadData { get; } 实现接口的类自己声明。用于RoadData丢失时访问。
        //预生成信息
        public double RoadBeginTime { get; }
        public double RoadEndTime { get; }
        public List<MovementData> MovementDatum { get; }
        public List<IAnimationData> AnimationEventDatum { get; }
        //函数
        public void Init(IMap map, RoadData roadData, GameObject blockPrefab);
        
        #region Road_Operations //操作功能
            //物体操作================================================================================================
            public void RebuildBlocks();
            /// <summary>
            /// 重建Block列表，清理无效Block并补齐缺失Block，更新Block位置
            /// </summary>
            public void RecoverBlocks();
            public void OnBlockDisplacementRuleChanged();
            public void RefreshBlockInfoDisplay();
            
            //数据操作================================================================================================
            //Road级别
            public void ModifyNoteBeginIndex(int newBeginIndex);
            public void ModifyNoteEndIndex(int newEndIndex);
            public void ModifyNoteRange(int newBeginIndex, int newEndIndex);
            public void ModifyTargetSegmentName(string targetSegmentName);
            public void ModifyTargetRoadDataName(string newName);
            public void SaveTransformData();
            
            //预处理数据
            public void GenerateBlockMovementData();
        #endregion

        #region Road_DataFunctions
            public List<MovementData> GetBlockMovementDatum(int blockBeginIndex);
            public void ModifyDisplacementData(int blockLocalIndex, IBlockDisplacementData newDisplacementData);
        #endregion
        
        #region Road_MapFunctions //地图操作
            public IBlock CreateBlock(int blockLocalIndex);
            public List<IBlock> CreateBlocks(IEnumerable<int> index);
            public List<IBlock> CreateBlocks(int indexBegin, int count);
            public void RemoveBlocks(List<IBlock> blocksToRemove);
        #endregion
    }

    public interface IMap
    {
        public const int MaxBlockCountThreshold = 5000;
        //外部绑定
        //自身绑定
        public Transform Transform { get; }
        //public EditManager EditManager { get; }
        public SceneData SceneData { get; }
        public List<IRoad> Roads { get; }

        #region Map_Operations
            //操作功能
            public void RebuildRoads();
            public void RecoverRoads();
            public void RefreshAllRoads();
            //预处理运行数据
            public void GenerateMovementData();
        #endregion
        
        //地图操作
        public void AddRoads(List<RoadData> roadDataToAdd);
        public void RemoveRoads(List<IRoad> roadsToRemove);
        public void OnRoadDataMissing(IRoad road);
        
    }
}