using System.Collections.Generic;
using System.Linq;
using MusicTogether.DancingLine.Interfaces;
using MusicTogether.LevelManagement;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MusicTogether.DancingLine.Classic
{
    struct PoolRegistration
    {
        public ILinePool Pool;
        public double ClipStart;
        public double ClipEnd;
            
        /// <summary>
        /// 判断给定时间是否在此 Pool 的活动范围内
        /// </summary>
        public bool IsActiveAt(double time)
        {
            return time >= ClipStart && time <= ClipEnd;
        }
    }
    public class ClassicLineController : SerializedMonoBehaviour , ILineController
    {
        [SerializeField]protected ILevelManager levelManager;
        protected double time => levelManager.LevelTime;
        public LevelState LevelState => levelManager.CurrentLevelState;
        internal List<PoolRegistration> activePools = new List<PoolRegistration>();
        internal List<PoolRegistration> inactivePools = new List<PoolRegistration>();
        internal string debugInfo;
        

        
        private void Update()
        {
            var inactivedP = activePools.Where(p => !p.IsActiveAt(time));
            var newActiveP = inactivePools.Where(p => p.IsActiveAt(time));
            activePools.AddRange(newActiveP);
            activePools.RemoveAll(p => !p.IsActiveAt(time));
            inactivePools.AddRange(inactivedP);
            inactivePools.RemoveAll(p => p.IsActiveAt(time));

            switch (LevelState)
            {
                case LevelState.Playing:
                    if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
                    {
                        if (!EventSystem.current.IsPointerOverGameObject())
                        {
                            debugInfo += $"Input detected at time {Time.time}\n Events:";
                            foreach (var poolStruct in activePools)
                            {
                                poolStruct.Pool.AddNode(NodeInputType.Turn, time);
                            }
                        }
                    }
                    break;
            }
        }

        public void RegisterPool(ILinePool pool, double startTime, double endTime)
        {
            if (activePools.Exists(p => p.Pool == pool) || inactivePools.Exists(p => p.Pool == pool))
            {
                Debug.LogWarning($"Pool {pool} is already registered.");
                return;
            }
            inactivePools.Add(new PoolRegistration(){Pool = pool, ClipStart = startTime, ClipEnd = endTime});
        }

        public void UnregisterPool(ILinePool pool)
        {
            activePools.RemoveAll(p => p.Pool == pool);
            inactivePools.RemoveAll(p => p.Pool == pool);
        }
    }
}