using System;
using System.Collections.Generic;
using MusicTogether.General;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MusicTogether.DancingBallArchived.Map
{
    public class MapHolder : MonoBehaviour
    {
        [Title("Resources")]
        //public LevelStateManager level;
    
        public List<RoadHolder> roadHolders;
        
        [Title("Data")]
        //public InputNoteData inputNoteData;
        public GameObject[] blockPrefabs;
        public GameObject[] tapPrefabs;

        [Title("Information")] 
        public double LevelTime;// => level.LevelTime;

        [Title("Pre-Processed Data")] 
        public int[] roadLength;
        public int[] sortedAnimIndices,sortedTapIndices;
        
        //数据
        public void GetLocalIndex(int globalIndex, out int roadIndex, out int blockIndex)
        {
            roadIndex = 0;
            blockIndex = globalIndex;
            int currentRoadLength = 0;
            do
            {
                if (roadIndex >= roadHolders.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(globalIndex), 
                        "Global index 出界了，请检查Global index赋值是否正确");
                }
                blockIndex -= currentRoadLength;
                currentRoadLength = roadLength[roadIndex];
                roadIndex++;
            } while (blockIndex - currentRoadLength > 0);
        }
        
        //管理
        
    }
}
