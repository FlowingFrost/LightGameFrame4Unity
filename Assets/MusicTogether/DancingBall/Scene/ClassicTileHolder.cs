using System;
using System.Collections.Generic;
using System.Linq;
using MusicTogether.DancingBall.Player;
using UnityEngine;

namespace MusicTogether.DancingBall.Scene
{
    public class ClassicTileHolder : MonoBehaviour, ITileHolder
    {
        public float tileThickness = 0.2f;
        public Transform tileParent;
        public Transform bottomTile, forwardTile, backwardTile;
        public void SetTileActive(bool forward, bool backward)//必须保证Bottom是存在的
        {
            if (tileParent == null) return;
            if (forwardTile != null) forwardTile.gameObject.SetActive(forward);
            if (backwardTile != null) backwardTile.gameObject.SetActive(backward);
            if (bottomTile != null) bottomTile.gameObject.SetActive(true);
        }

        /// <summary>
        /// 返回所有已启用的地板Transform和他们的厚度。
        /// </summary>
        public List<MovementData> GetTileMovementDatum(double currentBlockTime, double singleBlockDuration, bool blockNeedTap)
        {
            List<MovementData> datum = new List<MovementData>();
            if (backwardTile != null && backwardTile.gameObject.activeSelf) datum.Add(new MovementData(false, currentBlockTime, backwardTile, tileThickness));
            datum.Add(new MovementData(false, currentBlockTime, bottomTile, tileThickness));//Bottom Tile必须存在且始终启用。
            if (forwardTile != null && forwardTile.gameObject.activeSelf) datum.Add(new MovementData(false, currentBlockTime, forwardTile, tileThickness));

            const float segmentWidthRate = 0.2f;
            for (int i = 0; i < datum.Count; i++)
            {
                datum[i].Time = currentBlockTime - singleBlockDuration * segmentWidthRate / 2 +
                                singleBlockDuration * segmentWidthRate / (datum.Count + 1) * (i + 1);
            }
            datum.First().NeedTap = blockNeedTap;
            return datum;
        }
    }
}