using System;
using System.Collections.Generic;
using MusicTogether.DancingLine.Interfaces;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace MusicTogether.DancingLine.Classic
{
    /// <summary>
    /// Pool是容纳线整个模拟空间的场，需要定义起始时间位置、运动方向列表和重力等全局参数。
    /// </summary>
    public class ClassicLinePool : SerializedMonoBehaviour, ILinePool
    {
        //预设参数
        [SerializeField, Required] internal Transform tailHolder;
        [SerializeField, Required] internal GameObject lineTailPrefab;
        [SerializeField] internal IPhysicsDetector physicsDetector;
        [SerializeField] internal ILineNode preNode;
        
        [SerializeField] internal NodeInputType firstNodeType = NodeInputType.Continue;
        [SerializeField] internal double beginTime;
        [SerializeField] internal Vector3 beginPoint;
        [SerializeField] internal NodeMotionType firstNodeMotionType;
        [SerializeField] internal Vector3 beginVelocity;
        [SerializeField] internal Vector3 gravity;
        [OdinSerialize] protected List<IDirection> directions = new List<IDirection>();
        [SerializeField] internal bool getLinePositionAsBeginPosition;
        
        //internal Transform tailHolder => transform;
        
        //运行信息
        internal MotionState currentMotionState;
        internal int currentNodeIndex;
        internal ILineNode currentNode => currentNodeIndex >= 0 ? lineNodes[currentNodeIndex] : preNode;
        internal bool isEmpty => lineNodes.Count == 0;
        internal bool hasPendingNodes => pendingNodes.Count > 0;
        //数据存储
        
        internal readonly List<ILineNode> lineNodes = new List<ILineNode>();
        internal readonly List<ILineNode> pendingNodes = new List<ILineNode>();
        
        //接口
        public double BeginTime => beginTime;
        public MotionState CurrentMotionState => currentMotionState;
        public IDirection CurrentDirection => currentNode.Direction;
        public int CurrentNodeIndex => currentNodeIndex;
        public ILineNode CurrentNode => currentNode;
        
        public bool IsEmpty => lineNodes.Count == 0 && pendingNodes.Count == 0;
        public IReadOnlyList<ILineNode> LineNodes => lineNodes;
        internal IDirection nextDirection(IDirection dir) => directions[dir.NextDirectionID] ?? dir;
        internal ILineNode previousNode(int index) => index > 0 ? lineNodes[index - 1] : preNode;
        
        //Debug
        [SerializeField] internal TextMeshProUGUI debugText;
        internal string debugInfo;

        public void Init(ILineComponent lineComponent, double time)
        {
            pendingNodes.Clear();
            lineNodes.Clear();
            currentNodeIndex = -1;

            if (getLinePositionAsBeginPosition)//折中方案，之后请使用新的实现方式。
            {
                var lineGlobalPosition = lineComponent.Transform.position;
                var lineLocalPosition = tailHolder.InverseTransformPoint(lineGlobalPosition);
                lineLocalPosition = directions[0].GetLineHeadMotionState(lineLocalPosition, beginTime - time).ParentSpacePosition;
                beginPoint = lineLocalPosition;
            }
            
            //AddNode(NodeInputType.Continue, beginTime);
            preNode.SetEndTime(beginTime);
            preNode.SetBeginPosition(beginPoint);
            preNode.SetDirection(directions[0]);
            var physicsState = new PhysicsState(){NodeMotionType = firstNodeMotionType, Velocity = beginVelocity, Gravity = gravity};
            preNode.InitMotion(physicsDetector, physicsState);
            
            //ProcessPendingNodes(beginTime);
            
            debugInfo += $"Pool initialized at time {beginTime} with direction {directions[0].ID}\n";
            //return UpdatePool(beginTime);
        }
        public ILineNode AddNode(NodeInputType nodeType, double time, bool isPending = true)
        {
            //if (time < beginTime) { return null; } 择机加入。需要考虑项目设计并评估最佳做法
            var newNodeObj = Instantiate(lineTailPrefab, tailHolder);
            var newNode = newNodeObj.GetComponent<ILineNode>();
            
            // 必须设置基本属性，后续的RefreshNodeByFormer会依赖这些值
            newNode.SetNodeType(nodeType);
            newNode.SetBeginTime(time);
            
            if (isPending) pendingNodes.Add(newNode);
            
            //debugInfo += $"Adding Node (Type: {nodeType}, Time : {time})\n";
            
            return newNode;
        }
        
        /*public ILineNode AddNode(NodeInputType nodeType, double time, IDirection direction, PhysicsState physicsState)
        {
            var newNodeObj = Instantiate(lineTailPrefab, tailHolder);
            var newNode = newNodeObj.GetComponent<ILineNode>();
            newNode.Init(nodeType, time, direction, physicsDetector);
            newNode.InitMotion(physicsState);
            pendingNodes.Add(newNode);
            return newNode;
        }*/
        
        internal void RefreshNode(ILineNode targetNode, IDirection direction, Vector3 beginPosition, PhysicsState beginPhysicsState)
        {
            targetNode.SetDirection(direction);
            targetNode.SetBeginPosition(beginPosition);
            targetNode.InitMotion(physicsDetector, beginPhysicsState);
        }

        /// <summary>
        /// 通过更新前一个节点而获取下一个节点的起始信息。
        /// </summary>
        /// <param name="targetNode"></param>
        /// <param name="formerNode"></param>
        internal void RefreshNodeByFormer(ILineNode targetNode, ILineNode formerNode)
        {
            var beginPosition = formerNode.UpdatePosition(targetNode.BeginTime).ParentSpacePosition;
            if (formerNode.EndDisplacement.HasValue) beginPosition += formerNode.EndDisplacement.Value;
            var beginPhysicsState = formerNode.GetPhysicsState(targetNode.BeginTime);
            
            IDirection newDir;
            switch (targetNode.NodeType)
            {
                case NodeInputType.Turn:
                    newDir = nextDirection(currentNode.Direction);
                    break;
                default:
                    newDir = currentNode.Direction;
                    break;
            }
            RefreshNode(targetNode, newDir, beginPosition, beginPhysicsState);
        }
        
        /// <summary>
        /// 此函数用于处理上一节点为有穷节点且已经到达末尾，下一节点开始时间晚于当前节点结束时间的情况。会在计算完毕后将新节点插入lineNodes，但是需要注意这里不更改currentNodeIndex.
        /// </summary>
        /// <param name="formerNode"></param>
        /// <param name="formerIndex"></param>
        internal void ContinueNode(ILineNode formerNode, int formerIndex)
        {
            var nodeBeginTime = formerNode.EndTime;
            var continuingNode = AddNode(NodeInputType.Continue, nodeBeginTime, false);
            RefreshNodeByFormer(continuingNode, formerNode);
            lineNodes.Insert(formerIndex+1, continuingNode);
        }
        
        /*protected void OnNodeEnd(ILineNode endingNode)
        {
            var nodeEndTime = endingNode.EndTime;
            //if (nodeEndTime > currentTime) return;
            var endingPhysicsState = endingNode.GetPhysicsState(nodeEndTime);

            //先查找是否存在相同时间节点，若有则更新该节点的输入类型和物理状态，若没有则添加一个新的继续节点。
            //此处需要添加的节点已经在时间范围内，不再添加到pendingNodes以防止新的错误。同时需要修改AddNode可选参数，即添加位置
            //AddNode(NodeInputType.Continue, nodeEndTime, endingNode.Direction, endingPhysicsState);
            
            debugInfo += $"Node (Time : [{endingNode.BeginTime}, {endingNode.EndTime}) Motion : {endingNode.NodeMotionType}) ended\n";
        }*/
        
        protected void ProcessPendingNodes(double time)
        {
            if (!hasPendingNodes) { return; }
            pendingNodes.Sort((a, b) => a.BeginTime.CompareTo(b.BeginTime));
            while (pendingNodes.Count > 0 && pendingNodes[0].BeginTime <= time)
            {
                var insertIndex = lineNodes.BinarySearch(pendingNodes[0], 
                    Comparer<ILineNode>.Create((a, b) => a.BeginTime.CompareTo(b.BeginTime)));
                if (insertIndex < 0) { insertIndex = ~insertIndex; }
                lineNodes.Insert(insertIndex, pendingNodes[0]);
                pendingNodes.RemoveAt(0);
                if (currentNodeIndex > insertIndex) { currentNodeIndex = insertIndex; }//指针移动到第一个未更新的节点
            }
        }

        //管理节点
        protected ILineNode UpdatePoolSequence(double time)
        {
            if (hasPendingNodes) { ProcessPendingNodes(time); }
            
            if (currentNodeIndex >= lineNodes.Count) currentNodeIndex = lineNodes.Count - 1;
            
            /*if (currentNode.BeginTime > time && currentNodeIndex < lineNodes.Count - 1)
            {
                currentNodeIndex = lineNodes.Count - 1;//确保所有超前节点都被回收。
            }*/
            
            //整个更新流程：
            //1.回收从当前位置往前的所有超前节点
            //2.遍历更新直到末尾（遇到有穷节点结尾，需要添加新节点/正常更新节点/下一个节点超前，回收）
            
            while (currentNodeIndex >= 0 && currentNode.BeginTime > time)
            {
                currentNode.SetActive(false);
                pendingNodes.Add(currentNode);
                lineNodes.RemoveAt(currentNodeIndex);
                currentNodeIndex--;
            }

            while (currentNodeIndex < lineNodes.Count)
            {
                //a.遇到有穷节点结尾，需要添加新节点
                if (currentNode.HasLimitedLength && currentNode.EndTime <= time)
                {
                    ContinueNode(currentNode, currentNodeIndex);
                    currentNodeIndex++;
                }
                //b.正常更新节点.
                else if (currentNodeIndex == lineNodes.Count - 1)//已经抵达末尾，更新位置并退出循环
                {
                    //currentMotion = currentNode.UpdatePosition(time);
                    //break;
                    return currentNode;
                }
                else if (lineNodes[currentNodeIndex + 1].BeginTime <= time)//已到达下一节点，更新当前节点位置并修正下一节点起始信息。
                {
                    RefreshNodeByFormer(lineNodes[currentNodeIndex + 1], currentNode);
                    currentNodeIndex++;
                }
                //c.下一个节点超前，回收
                else//回收完毕后变成末尾，更新位置并退出循环
                {
                    pendingNodes.AddRange( lineNodes.GetRange(currentNodeIndex + 1, lineNodes.Count - currentNodeIndex - 1));
                    lineNodes.RemoveRange(currentNodeIndex + 1, lineNodes.Count - currentNodeIndex - 1);
                    //currentMotion = currentNode.UpdatePosition(time);
                    //break;
                    return currentNode;
                }
            }
            
            /*while (currentNodeIndex < lineNodes.Count - 1 && lineNodes[currentNodeIndex + 1].BeginTime <= time)
            {
                var nextNode = lineNodes[currentNodeIndex + 1];
                RefreshNodeByFormer(nextNode, currentNode);
            }*/
            
            //若当前节点为末位或下一位时间超前，正常更新当前节点。
            //进入下一节点的处理时，继承上一节点的末位置的末物理状态。
            //if (currentIndex == lineNodes.Count - 1 || lineNodes[currentIndex + 1].BeginTime > time)
            
            //if (currentNode == null) { return new MotionState(){Position = beginPoint, Rotation = Quaternion.identity}; }
            throw new InvalidOperationException("UpdatePoolSequence should always return a valid node. Please check the implementation for potential issues.");
        }
        
        //时间正常流动时，更新和。
        public MotionState UpdatePool(double time)
        {
            //MotionState currentMotion = null;
            //if (isEmpty) { Init(); }
            var currentNode = UpdatePoolSequence(time);
            var currentMotion = currentNode.UpdatePosition(time);
            
            if(debugText != null) debugText.text = debugInfo;
            
            //return currentNode.UpdatePosition(time);
            currentMotionState = currentMotion;
            return currentMotion;
        }
        
        //发生快进快退时，使用跳转功能一次性处理当前节点的物理判断。
        
        
        //粗略实现
        public void ClearNodesAfterTime(double? time)
        {
            if (time == null) { time = beginTime; }
            for (int i = lineNodes.Count - 1; i >= 0; i--)
            {
                if (lineNodes[i].BeginTime >= time)
                {
                    lineNodes[i].SetActive(false);
                    //PendingNodes.Add(lineNodes[i]);
                    lineNodes[i].DeleteNode();
                    lineNodes.RemoveAt(i);
                }
            }
            for (int i = pendingNodes.Count - 1; i >= 0; i--)
            {
                if (pendingNodes[i].BeginTime >= time)
                {
                    pendingNodes[i].SetActive(false);
                    pendingNodes[i].DeleteNode();
                    pendingNodes.RemoveAt(i);
                }
            }
            if (currentNodeIndex >= lineNodes.Count) { currentNodeIndex = lineNodes.Count - 1; }
        }


        public void AwakeUnion()
        {
            throw new NotImplementedException("ClassicLinePool does not support AwakeUnion. Please call Init() directly to initialize the pool.");
            //Init();
        }
        public void StartUnion(double startTime = 0d) { }

        public void UpdateUnion(double currentTime)
        {
            UpdatePool(currentTime);
        }

        public void EditorPreviewUpdate(double time)
        {
            // TODO: 实现编辑器预览更新逻辑
        }
    }
}