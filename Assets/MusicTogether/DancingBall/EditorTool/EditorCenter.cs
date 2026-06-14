using System;
using System.Collections.Generic;
using LightGameFrame.Services;
using MusicTogether.DancingBall.EditorTool.Controller;
using MusicTogether.DancingBall.Data;
using MusicTogether.DancingBall.Player;
using MusicTogether.DancingBall.SceneOld;
using UnityEngine;

namespace MusicTogether.DancingBall.EditorTool
{
    [AutoService(Mode = AutoServiceMode.Dual)]
    public class EditorCenter : GameServiceBase<EditorCenter>
    {
        public IMap targetMap;
        public int SelectedRoadIndex { get; private set; }
        public int SelectedBlockIndex { get; private set; }
        public IRoad selectedRoad;
        public IBlock selectedBlock;
        public IBlockDisplacementData selectedDisplacementData;
        public BallPlayer player;

        private bool IsRoadIndexOutOfRange => targetMap == null || SelectedRoadIndex < 0 || SelectedRoadIndex >= targetMap.Roads.Count;
        private bool IsBlockIndexOutOfRange => selectedRoad == null || SelectedBlockIndex < 0 || SelectedBlockIndex >= selectedRoad.Blocks.Count;

        public Action<string> SendMessage = Debug.Log;
        public Action<int, int> OnSelectionChanged;
        public Action<IRoad> OnRoadSelectionChanged;
        public Action<IBlock, IBlockDisplacementData> OnBlockSelectionChanged;
        public Action<GameObject> LookAtObject;
        public Action OnRoadSwitchCompleted;
        public Action<List<RoadData>> OnRoadListChanged;
        public Action<List<IBlockDisplacementData>> OnBlockDisplacementListChanged;

        public bool IsBound => targetMap != null;
        public bool IsPlayerBound => player != null;

        private EditorShortcutDispatcher _dispatcher;
        private object _activeInputHandler;
        public bool IsInputEnabled { get; set; } = true;

        public EditorShortcutDispatcher Dispatcher => _dispatcher;

        protected override void OnInitialize()
        {
            _dispatcher = new EditorShortcutDispatcher(this);
            _dispatcher.LoadFromConfig();
            TryAutoBind();
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChanged += OnActiveSceneChanged;
#endif
        }

        /// <summary>抢占式注册：后来的直接覆盖。</summary>
        public void ClaimInput(object handler)
        {
            _activeInputHandler = handler;
        }

        public void ReleaseInput(object handler)
        {
            if (_activeInputHandler == handler) _activeInputHandler = null;
        }

        /// <summary>按键中转入口，仅活跃输入源的键会被处理。</summary>
        public bool ProcessKey(KeyCode key, object sender)
        {
            if (!IsInputEnabled) return false;
            if (_activeInputHandler != null && _activeInputHandler != sender) return false;
            return _dispatcher.ProcessKey(key);
        }

#if UNITY_EDITOR
        private void OnActiveSceneChanged(UnityEngine.SceneManagement.Scene previousScene, UnityEngine.SceneManagement.Scene newScene)
        {
            Unbind();
            TryAutoBind();
            if (IsBound)
            {
                RefreshSelection();
            }
        }
#endif

        public bool TryAutoBind()
        {
            targetMap = FindMapInScene();
            if (player == null)
                player = GameObject.FindObjectOfType<BallPlayer>();

            if (targetMap != null)
                RefreshSelection();

            return IsBound;
        }

        public void Bind(IMap map, BallPlayer player, int roadIndex = 0, int blockIndex = 0)
        {
            targetMap = map;
            this.player = player;
            SelectedRoadIndex = roadIndex;
            SelectedBlockIndex = blockIndex;
            RefreshSelection();
        }

        public void Unbind()
        {
            targetMap = null;
            player = null;
            selectedRoad = null;
            selectedBlock = null;
            selectedDisplacementData = null;
            SelectedRoadIndex = 0;
            SelectedBlockIndex = 0;
        }

        private static IMap FindMapInScene()
        {
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in allObjects)
            {
                if (!go.scene.isLoaded) continue;
                var components = go.GetComponents<Component>();
                foreach (var component in components)
                {
                    if (component is IMap map)
                        return map;
                }
            }
            return null;
        }

        public void PreviousBlock()
        {
            SelectedBlockIndex--;
            if (IsBlockIndexOutOfRange) PreviousRoad();
            else RefreshSelection();
        }
        public void NextBlock()
        {
            SelectedBlockIndex++;
            if (IsBlockIndexOutOfRange) NextRoad();
            else RefreshSelection();
        }
        public void PreviousRoad()
        {
            SelectedRoadIndex--;
            SelectedBlockIndex = int.MaxValue;
            RefreshSelection();
        }
        public void NextRoad()
        {
            SelectedRoadIndex++;
            SelectedBlockIndex = 0;
            RefreshSelection();
        }

        public void JumpTo(int roadIndex, int blockIndex = -1)
        {
            SelectedRoadIndex = roadIndex;
            SelectedBlockIndex = blockIndex;
            RefreshSelection();
        }

        public void RefreshSelection()
        {
            OnSelectionChanged?.Invoke(SelectedRoadIndex, SelectedBlockIndex);

            // 场景切换或外部销毁后 targetMap 可能已过期
            if (IsUnityObjectDestroyed(targetMap))
            {
                TryAutoBind();
            }

            if (targetMap == null)
            {
                SendMessage("Target map is not set.");
                return;
            }

            if (targetMap.Roads == null || targetMap.Roads.Count == 0)
            {
                SendMessage("Target map has no roads.");
                return;
            }

            if (IsRoadIndexOutOfRange)
            {
                SendMessage("Selected road index is out of range.");
                SelectedRoadIndex = SelectedRoadIndex < 0 ? 0 : targetMap.Roads.Count - 1;
            }

            selectedRoad = targetMap.Roads[SelectedRoadIndex];
            OnRoadSelectionChanged?.Invoke(selectedRoad);
            if (selectedRoad == null || IsUnityObjectDestroyed(selectedRoad))
            {
                SendMessage("Selected road is invalid.");
                return;
            }
            // road 存在但 blocks 为空，而 RoadData 要求有 block → 可能 map/road 不匹配，记录诊断信息
            if (selectedRoad.RoadData != null && selectedRoad.RoadData.BlockCount > 0 &&
                (selectedRoad.Blocks == null || selectedRoad.Blocks.Count == 0))
            {
                string mapName = (targetMap as UnityEngine.Object)?.name ?? "null";
                string roadName = selectedRoad.RoadData.roadName;
                int blockCount = selectedRoad.RoadData.BlockCount;
                int roadIdx = SelectedRoadIndex;
                SendMessage($"[Diagnostic] map={mapName}, road={roadName}(idx={roadIdx}), dataBlockCount={blockCount}, actualBlocks={selectedRoad.Blocks?.Count ?? 0}");
                SendMessage("Selected road has no blocks.");
                return;
            }
            if (selectedRoad.Blocks == null || selectedRoad.Blocks.Count == 0)
            {
                SendMessage("Selected road has no blocks.");
                return;
            }

            if (IsBlockIndexOutOfRange)
            {
                SendMessage("Selected block index is out of range.");
                SelectedBlockIndex = SelectedBlockIndex < 0 ? 0 : selectedRoad.Blocks.Count - 1;
            }

            selectedBlock = selectedRoad.Blocks[SelectedBlockIndex];

            if (IsUnityObjectDestroyed(selectedBlock))
            {
                SendMessage("Selected block was destroyed. Falling back to first valid block.");
                SelectedBlockIndex = TryFindFirstValidBlock(selectedRoad);
                if (SelectedBlockIndex < 0)
                {
                    SendMessage("No valid blocks found on road.");
                    selectedBlock = null;
                    selectedDisplacementData = null;
                    OnBlockSelectionChanged?.Invoke(null, null);
                    OnSelectionChanged?.Invoke(SelectedRoadIndex, SelectedBlockIndex);
                    return;
                }
                selectedBlock = selectedRoad.Blocks[SelectedBlockIndex];
            }

            selectedRoad.RoadData.Get_BlockData(selectedBlock.BlockLocalIndex, out selectedDisplacementData);
            OnBlockSelectionChanged?.Invoke(selectedBlock, selectedDisplacementData);

            if (IsUnityObjectDestroyed(selectedBlock))
            {
                SendMessage("Selected block was destroyed during event handling.");
                return;
            }

            OnSelectionChanged?.Invoke(SelectedRoadIndex, SelectedBlockIndex);
            if (IsUnityObjectDestroyed(selectedBlock)) return;
            LookAtObject?.Invoke(selectedBlock.Transform.gameObject);

            if (SelectedBlockIndex == 0)
            {
                OnRoadSwitchCompleted?.Invoke();
                SelectGameObject(selectedRoad);
            }
        }

        //操作功能
        public void MapRebuildRoadsRequested() { targetMap.RebuildRoads(); RefreshSelection(); }
        public void MapRefreshAllRoadsRequested() { targetMap.RefreshAllRoads(); RefreshSelection(); }

        public void ShiftBlockDisplacementIndices(int start, int end, int offset)
        {
            if (selectedRoad?.RoadData == null) return;
            selectedRoad.RoadData.ShiftBlockDisplacementIndices(start, end, offset);
            selectedRoad.OnBlockDisplacementRuleChanged();
            RefreshSelection();
            OnBlockDisplacementListChanged?.Invoke(selectedRoad.RoadData.blockDisplacementDataList);
        }

        public bool CreateRoadFromSelection()
        {
            if (targetMap?.SceneData == null) return false;
            var sceneData = targetMap.SceneData;
            var template = selectedRoad?.RoadData;
            string baseName = template?.roadName ?? "Road";
            string newName = GetUniqueRoadName(sceneData, $"{baseName}_New");
            string segmentName = template?.targetSegmentName ?? "";
            int noteBegin = template?.noteBeginIndex ?? 0;
            int noteEnd = template?.noteEndIndex ?? noteBegin;

            var created = sceneData.CreateRoadData(newName, segmentName, noteBegin, noteEnd);
            if (created == null) return false;
            if (template != null)
            {
                created.loaclPosition = template.loaclPosition;
                created.loaclRotation = template.loaclRotation;
                created.localScale = template.localScale;
            }

            targetMap.RecoverRoads();
            RefreshSelection();
            OnRoadListChanged?.Invoke(sceneData.roadDataList);
            return true;
        }

        public bool CreateRoad(string roadName, string segmentName, int noteBegin, int noteEnd)
        {
            if (targetMap?.SceneData == null) return false;
            var sceneData = targetMap.SceneData;
            var finalName = GetUniqueRoadName(sceneData, roadName);
            var created = sceneData.CreateRoadData(finalName, segmentName, noteBegin, noteEnd);
            if (created == null) return false;
            targetMap.RecoverRoads();
            RefreshSelection();
            OnRoadListChanged?.Invoke(sceneData.roadDataList);
            return true;
        }

        public bool DuplicateSelectedRoad()
        {
            if (targetMap?.SceneData == null || selectedRoad?.RoadData == null) return false;
            var sceneData = targetMap.SceneData;
            var template = selectedRoad.RoadData;
            string newName = GetUniqueRoadName(sceneData, $"{template.roadName}_Copy");
            var created = sceneData.CreateRoadData(newName, template.targetSegmentName, template.noteBeginIndex, template.noteEndIndex);
            if (created == null) return false;

            created.loaclPosition = template.loaclPosition;
            created.loaclRotation = template.loaclRotation;
            created.localScale = template.localScale;

            if (template.blockDisplacementDataList != null)
            {
                created.blockDisplacementDataList = new List<IBlockDisplacementData>();
                foreach (var data in template.blockDisplacementDataList)
                {
                    if (data is ClassicBlockDisplacementData classic)
                    {
                        created.blockDisplacementDataList.Add(new ClassicBlockDisplacementData(classic.BlockIndex_Local)
                        {
                            turnType = classic.turnType,
                            displacementType = classic.displacementType
                        });
                    }
                    else
                    {
                        created.blockDisplacementDataList.Add(data);
                    }
                }
            }

            targetMap.RecoverRoads();
            RefreshSelection();
            OnRoadListChanged?.Invoke(sceneData.roadDataList);
            return true;
        }

        public bool DeleteSelectedRoad()
        {
            if (targetMap?.SceneData == null || selectedRoad?.RoadData == null) return false;
            var sceneData = targetMap.SceneData;
            bool removed = sceneData.RemoveRoadData(selectedRoad.RoadData.roadName);
            if (!removed) return false;

            targetMap.RecoverRoads();
            RefreshSelection();
            OnRoadListChanged?.Invoke(sceneData.roadDataList);
            return true;
        }

        public bool CreateBlockDisplacementDataForSelected(Type dataType = null)
        {
            if (selectedRoad?.RoadData == null || selectedBlock == null) return false;
            int blockLocalIndex = selectedBlock.BlockLocalIndex;
            dataType ??= typeof(ClassicBlockDisplacementData);
            var newData = selectedRoad.RoadData.CreateBlockDisplacementData(blockLocalIndex, dataType);
            if (newData == null) return false;
            selectedRoad.RoadData.AddOrReplace_BlockData(newData);
            selectedRoad.OnBlockDisplacementRuleChanged();
            RefreshSelection();
            OnBlockDisplacementListChanged?.Invoke(selectedRoad.RoadData.blockDisplacementDataList);
            return true;
        }

        public bool RemoveBlockDisplacementDataForSelected()
        {
            if (selectedRoad?.RoadData == null || selectedBlock == null) return false;
            int blockLocalIndex = selectedBlock.BlockLocalIndex;
            bool removed = selectedRoad.RoadData.RemoveBlockDisplacementData(blockLocalIndex);
            if (!removed) return false;
            selectedRoad.OnBlockDisplacementRuleChanged();
            RefreshSelection();
            OnBlockDisplacementListChanged?.Invoke(selectedRoad.RoadData.blockDisplacementDataList);
            return true;
        }

        // ---- 快捷键驱动：TurnType / DisplacementType ----

        public bool SetSelectedBlockTurnType(ClassicBlockDisplacementData.TurnType turnType)
        {
            if (selectedRoad?.RoadData == null || selectedBlock == null) return false;
            var data = GetOrCreateClassicDisplacementData();
            if (data == null) return false;

            data.turnType = turnType;
            selectedRoad.OnBlockDisplacementRuleChanged();
            RefreshSelection();
            OnBlockDisplacementListChanged?.Invoke(selectedRoad.RoadData.blockDisplacementDataList);
            return true;
        }

        public bool SetSelectedBlockDisplacementType(ClassicBlockDisplacementData.DisplacementType displacementType)
        {
            if (selectedRoad?.RoadData == null || selectedBlock == null) return false;
            var data = GetOrCreateClassicDisplacementData();
            if (data == null) return false;

            data.displacementType = displacementType;
            selectedRoad.OnBlockDisplacementRuleChanged();
            RefreshSelection();
            OnBlockDisplacementListChanged?.Invoke(selectedRoad.RoadData.blockDisplacementDataList);
            return true;
        }

        private ClassicBlockDisplacementData GetOrCreateClassicDisplacementData()
        {
            int index = selectedBlock.BlockLocalIndex;
            selectedRoad.RoadData.Get_BlockData(index, out var existing);

            if (existing is ClassicBlockDisplacementData classic)
                return classic;

            var newData = new ClassicBlockDisplacementData(index);
            selectedRoad.RoadData.AddOrReplace_BlockData(newData);
            return newData;
        }

        // ---- 截断与继续创建 ----

        /// <summary>
        /// 以当前选中的 Block 为界，截断 Road。该 Block 的 noteID 成为新的 NoteEndIndex。
        /// 超出部分的 displacement 数据被移除。
        /// </summary>
        public bool TruncateRoadAtSelectedBlock()
        {
            if (selectedRoad?.RoadData == null || selectedBlock == null) return false;

            int blockLocalIndex = selectedBlock.BlockLocalIndex;
            int noteID = selectedRoad.RoadData.noteBeginIndex + blockLocalIndex;
            if (noteID >= selectedRoad.RoadData.noteEndIndex) return false;

            // ModifyNoteEndIndex → RecoverBlocks 会销毁重建 Block，提前捕获 index
            selectedRoad.ModifyNoteEndIndex(noteID);

            var list = selectedRoad.RoadData.blockDisplacementDataList;
            if (list != null)
            {
                list.RemoveAll(d => d.BlockIndex_Local > blockLocalIndex);
            }

            selectedRoad.OnBlockDisplacementRuleChanged();
            RefreshSelection();
            OnBlockDisplacementListChanged?.Invoke(selectedRoad.RoadData.blockDisplacementDataList);
            OnRoadListChanged?.Invoke(targetMap.SceneData.roadDataList);
            return true;
        }

        /// <summary>
        /// 截断当前 Road 并在截断点后创建新 Road。若选中 Block 已在末尾则退化为从末尾继续创建。
        /// 新 Road 定位在选中 Block 的位置，使用与选中 Block 相同的朝向。
        /// </summary>
        public bool TruncateAndCreateRoad()
        {
            if (selectedRoad?.RoadData == null || selectedBlock == null) return false;
            if (targetMap?.SceneData == null) return false;

            int noteID = selectedRoad.RoadData.noteBeginIndex + selectedBlock.BlockLocalIndex;
            int originalEndIndex = selectedRoad.RoadData.noteEndIndex;
            string segmentName = selectedRoad.RoadData.targetSegmentName;
            string originalRoadName = selectedRoad.RoadData.roadName;
            Vector3 scale = selectedRoad.RoadData.localScale;

            // 在任何修改前捕获位置 —— ModifyNoteEndIndex → RecoverBlocks 会销毁 Block
            var mapT = targetMap.Transform;
            Vector3 spliceWorldPos = selectedBlock.Transform.position;
            Quaternion spliceWorldRot = selectedBlock.Transform.rotation;
            Vector3 newPos = mapT.InverseTransformPoint(spliceWorldPos);
            Quaternion newRot = Quaternion.Inverse(mapT.rotation) * spliceWorldRot;

            bool canTruncate = noteID < originalEndIndex;

            if (canTruncate)
            {
                if (!TruncateRoadAtSelectedBlock()) return false;
                // TruncateRoadAtSelectedBlock 末尾已调用 RefreshSelection，selectedRoad/selectedBlock 已是最新引用
            }

            int newNoteBegin = canTruncate ? noteID : originalEndIndex;
            int newNoteEnd = originalEndIndex;

            targetMap.RecoverRoads();
            var sceneData = targetMap.SceneData;
            string suffix = canTruncate ? "_Split" : "_Next";
            string newName = GetUniqueRoadName(sceneData, $"{originalRoadName}{suffix}");
            var newRoadData = sceneData.CreateRoadData(newName, segmentName, newNoteBegin, newNoteEnd);
            if (newRoadData == null) return false;

            newRoadData.loaclPosition = newPos;
            newRoadData.loaclRotation = newRot;
            newRoadData.localScale = scale;

            targetMap.RecoverRoads();
            RefreshSelection();
            OnBlockDisplacementListChanged?.Invoke(selectedRoad.RoadData.blockDisplacementDataList);
            OnRoadListChanged?.Invoke(sceneData.roadDataList);
            return true;
        }

        /// <summary>
        /// 从当前选中 Road 末尾继续创建新 Road。
        /// 新 Road 使用相同 Segment，NoteBeginIndex = 当前 Road 的 NoteEndIndex。
        /// </summary>
        public bool ContinueCreateRoad()
        {
            if (selectedRoad?.RoadData == null) return false;
            if (targetMap?.SceneData == null) return false;
            if (selectedRoad.Blocks == null || selectedRoad.Blocks.Count == 0) return false;

            var sceneData = targetMap.SceneData;
            var template = selectedRoad.RoadData;
            string segmentName = template.targetSegmentName;
            int noteBegin = template.noteEndIndex;
            string newName = GetUniqueRoadName(sceneData, $"{template.roadName}_Next");

            var created = sceneData.CreateRoadData(newName, segmentName, noteBegin, noteBegin);
            if (created == null) return false;

            created.localScale = template.localScale;

            // 定位在前一个 Road 末尾 Block 的位置和旋转
            var lastBlock = selectedRoad.Blocks[selectedRoad.Blocks.Count - 1];
            if (lastBlock != null && !IsUnityObjectDestroyed(lastBlock))
            {
                var mapT = targetMap.Transform;
                Vector3 worldPos = lastBlock.Transform.position;
                Quaternion worldRot = lastBlock.Transform.rotation;
                created.loaclPosition = mapT.InverseTransformPoint(worldPos);
                created.loaclRotation = Quaternion.Inverse(mapT.rotation) * worldRot;
            }
            else
            {
                created.loaclPosition = template.loaclPosition;
                created.loaclRotation = template.loaclRotation;
            }

            targetMap.RecoverRoads();
            RefreshSelection();
            OnRoadListChanged?.Invoke(sceneData.roadDataList);
            return true;
        }

        /// <summary>
        /// Unity 的 fake null 不适用于接口引用（IBlock/IRoad）。
        /// 通过 cast 到 MonoBehaviour 触发 Unity 原生的空判定。
        /// 纯 C# 实现的对象（非 MonoBehaviour）始终被视为有效。
        /// </summary>
        private static bool IsUnityObjectDestroyed<T>(T obj) where T : class
        {
            if (obj is null) return true;
            return obj is UnityEngine.Object unityObj && unityObj == null;
        }

        /// <summary>
        /// 在 road 的 blocks 列表中查找第一个有效（非 destroyed）的 block 索引。
        /// 返回 -1 表示没有有效 block。
        /// </summary>
        private static int TryFindFirstValidBlock(IRoad road)
        {
            for (int i = 0; i < road.Blocks.Count; i++)
            {
                var block = road.Blocks[i];
                if (!IsUnityObjectDestroyed(block)) return i;
            }
            return -1;
        }

        private string GetUniqueRoadName(SceneData sceneData, string baseName)
        {
            if (sceneData == null) return baseName;
            string name = string.IsNullOrWhiteSpace(baseName) ? "Road" : baseName;
            if (sceneData.ValidateRoadNameUnique(name)) return name;

            int suffix = 1;
            while (!sceneData.ValidateRoadNameUnique($"{name}_{suffix}"))
            {
                suffix++;
            }
            return $"{name}_{suffix}";
        }

#if UNITY_EDITOR
        private static void SelectGameObject<T>(T obj) where T : class
        {
            if (obj is MonoBehaviour mb && mb != null)
                UnityEditor.Selection.activeGameObject = mb.gameObject;
        }
#endif
    }
}
