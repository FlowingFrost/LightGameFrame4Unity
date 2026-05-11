using System.Collections.Generic;
using System.Linq;
using MusicTogether.General;
using Sirenix.OdinInspector;
using Unity.VisualScripting;
using UnityEngine;
using MusicTogether.DancingBallArchived.Map;

namespace MusicTogether.DancingBallArchived.Maker
{
    public class RoadMaker : MonoBehaviour
    {
        [Title("Resources")]
        public MapMaker mapMaker;
        public MapHolder mapHolder=>mapMaker.mapHolder;
        public RoadHolder roadHolder;
        public int RoadIndex=>roadHolder.roadIndex;
        public List<BlockHolder> BlockHolders=>roadHolder.blockHolders;
        public List<BlockMaker> blockMakers;
        
        
        [Title("Data")]
        [OnValueChanged("UpdateBlockManagement")]
        [SerializeField]private int musicPartIndex;
        //private Segemnt Segemnt => mapHolder.inputNoteData.noteLists[musicPartIndex];
        //private int BPM=>Segemnt.bpm;
        //private NoteType NoteType=>Segemnt.noteType;
        [SerializeField]private int noteBegin,noteEnd;
        [OnValueChanged("UpdateBlock")]
        [SerializeField]private int blockPrefabIndex;
        private GameObject BlockPrefab=>mapHolder.blockPrefabs[blockPrefabIndex];
        
        [SerializeField]private bool customBlockSize;
        [SerializeField]private float roadDefaultBlockSize;
        public float RoadDefaultBlockSize => customBlockSize? roadDefaultBlockSize : mapMaker.mapDefaultBlockSize;
        [Title("Settings")]
        
        [Title("Editing Data")] 
        public bool enablePlacementEdit;
        //Before-editing Function
        public void Init(MapMaker _mapMaker,MapHolder _mapHolder,RoadHolder _roadHolder,int _index)
        {
            mapMaker = _mapMaker;
            roadHolder = _roadHolder;
            roadHolder.mapHolder = _mapHolder;
            roadHolder.roadIndex = _index;
        }
        public void UpdateBlockManagement()
        {
            while (noteEnd - noteBegin + 1 < blockMakers.Count - 1)
            {
                Destroy(blockMakers.Last().gameObject);
                blockMakers.RemoveAt(blockMakers.Count - 1);
            }
            while (noteEnd - noteBegin + 1 > blockMakers.Count - 1)
            {
                int index = blockMakers.Count;
                var newBlockMaker = CreateBlockMaker(index);
                blockMakers.Add(newBlockMaker);
                UpdateBlockPlacement(index - 1);
            }
        }

        public void UpdateBlock()
        {
            foreach (var blockMaker in blockMakers)
            {
                blockMaker.ResetBlock(BlockPrefab);
            }
        }
        
        //Editing Tools

        //Editing Function
        [Button]
        public BlockMaker CreateBlockMaker()
        {
            return CreateBlockMaker(blockMakers.Count);
        }
        public BlockMaker CreateBlockMaker(int index)
        {
            float noteIndex = noteBegin + index;
            int insertIndex = blockMakers.FindIndex(a => a.blockHolder.noteIndex > noteIndex);
            //bool isClickNote = Segemnt.notes.Exists(a=>a == (int)noteIndex);
            
            var obj = new GameObject($"BlockHolder{index}", typeof(BlockMaker));
            var maker = obj.AddComponent<BlockMaker>();
            var holder = obj.AddComponent<BlockHolder>();
            
            obj.transform.SetParent(transform);
            obj.transform.SetAsLastSibling();
            //maker.Init(this,roadHolder,holder,BlockPrefab,index,noteIndex,isClickNote);
            
            blockMakers.Insert(insertIndex,maker);
            BlockHolders.Insert(insertIndex,holder);
            return maker;
        }
        [Button]
        public void CreateAllBlockMakers()
        {
            blockMakers.Sort((a,b)=>a.BlockIndex.CompareTo(b.BlockIndex));
            BlockHolders.Sort((a,b)=>a.blockIndex.CompareTo(b.blockIndex));
            int insertIndex = 0;
            for (int i = noteBegin; i < noteEnd; i++)
            {
                
            }
        }
        public void UpdateBlockPlacement(int beginIndex)
        {
            int endIndex = beginIndex;
            while (endIndex < blockMakers.Count-1)
            {
                beginIndex = endIndex;
                endIndex = NextCornerIndex(beginIndex);
                var beginMaker = blockMakers[beginIndex];
                Vector3 beginPoint = beginMaker.transform.localPosition;
                Vector3 dir = beginMaker.PlacementData.forwardDirection;
                Vector3 eulerAngles = beginMaker.PlacementData.eulerAngles;
                if (beginMaker.PlacementData.style == BlockPlacementStyle.Classic)
                {
                    eulerAngles = beginMaker.LastCorner(out _).PlacementData.eulerAngles;
                }
                float totalLength = beginMaker.BlockSize/2;
                beginMaker.transform.localEulerAngles = eulerAngles;
                for (int i = beginIndex+1; i < endIndex; i++)
                {
                    totalLength+=blockMakers[i].BlockSize/2;
                    var maker = blockMakers[i];
                    maker.transform.localPosition = beginPoint + dir*totalLength;
                    maker.Block.transform.localEulerAngles = eulerAngles;
                    totalLength+=blockMakers[i].BlockSize/2;
                }
            }
        }
        
        public int NextCornerIndex(int beginIndex)
        {
            int result = blockMakers.FindIndex(beginIndex, (b => b.IsCorner));
            return result !=-1 ? result : blockMakers.Count-1;
        }
        //Pre-Playing Function
        public void GetTimeInformation()
        {
            float blockAdvanceTime = mapHolder.blockPrefabs[blockPrefabIndex].GetComponentInChildren<Block>().animation.GetMaxTimeRange().startTime;
            float tapAdvanceTime =
                mapHolder.tapPrefabs[roadHolder.tapPrefabIndex].GetComponent<Tap>().timeRange.startTime;
            foreach (var blockHolder in BlockHolders)
            {
                //float noteTime = (float)NoteConverter.GetNoteTime(BPM,NoteType,blockHolder.noteIndex);
                //blockHolder.animTime = noteTime + blockAdvanceTime;
                //blockHolder.tapTime = noteTime + tapAdvanceTime;
            }
        }
    }
}
