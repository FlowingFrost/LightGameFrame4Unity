using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace MusicTogether.DancingBall.Data
{
    [Serializable]
    public class SceneDataJsonArchive
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string createdAtUtc;
        public List<RoadArchive> roads = new List<RoadArchive>();

        public static SceneDataJsonArchive FromSceneData(SceneData sceneData)
        {
            if (sceneData == null) throw new ArgumentNullException(nameof(sceneData));

            var archive = new SceneDataJsonArchive
            {
                createdAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                roads = sceneData.roadDataList.Select(RoadArchive.FromRoadData).ToList()
            };

            return archive;
        }

        public static string ToJson(SceneData sceneData, bool prettyPrint = true)
        {
            var archive = FromSceneData(sceneData);
            return JsonUtility.ToJson(archive, prettyPrint);
        }

        public static SceneDataJsonArchive FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON 不能为空", nameof(json));
            return JsonUtility.FromJson<SceneDataJsonArchive>(json);
        }

        public void ApplyToSceneData(SceneData sceneData, bool replaceRoads = true)
        {
            if (sceneData == null) throw new ArgumentNullException(nameof(sceneData));

            if (replaceRoads)
            {
                var roadList = roads?.Select(r => r.ToRoadData()).Where(r => r != null).ToList()
                    ?? new List<RoadData>();
                sceneData.roadDataList.Clear();
                foreach (var road in roadList)
                {
                    if (road == null) continue;
                    sceneData.Set_RoadData(road);
                }
            }
        }
    }

    [Serializable]
    public class RoadArchive
    {
        public string roadName;
        public string targetSegmentName;
        public int noteBeginIndex;
        public int noteEndIndex;
        public SerializableVector3 localPosition;
        public SerializableQuaternion localRotation;
        public SerializableVector3 localScale;
        public List<BlockDisplacementArchive> blocks = new List<BlockDisplacementArchive>();

        public static RoadArchive FromRoadData(RoadData roadData)
        {
            if (roadData == null) return null;
            return new RoadArchive
            {
                roadName = roadData.roadName,
                targetSegmentName = roadData.targetSegmentName,
                noteBeginIndex = roadData.noteBeginIndex,
                noteEndIndex = roadData.noteEndIndex,
                localPosition = SerializableVector3.FromVector3(roadData.loaclPosition),
                localRotation = SerializableQuaternion.FromQuaternion(roadData.loaclRotation),
                localScale = SerializableVector3.FromVector3(roadData.localScale),
                blocks = BlockDisplacementArchive.FromBlockList(roadData.blockDisplacementDataList)
            };
        }

        public RoadData ToRoadData()
        {
            var roadData = new RoadData(targetSegmentName, noteBeginIndex, noteEndIndex)
            {
                roadName = roadName,
                loaclPosition = localPosition.ToVector3(),
                loaclRotation = localRotation.ToQuaternion(),
                localScale = localScale.ToVector3(),
                blockDisplacementDataList = BlockDisplacementArchive.ToBlockList(blocks)
            };

            return roadData;
        }
    }

    [Serializable]
    public class BlockDisplacementArchive
    {
        public const string ClassicType = "ClassicBlockDisplacementData";

        public string type;
        public int blockIndexLocal;
        public ClassicBlockDisplacementArchive classic;

        public static List<BlockDisplacementArchive> FromBlockList(List<IBlockDisplacementData> blocks)
        {
            var result = new List<BlockDisplacementArchive>();
            if (blocks == null) return result;

            foreach (var block in blocks)
            {
                if (block is ClassicBlockDisplacementData classic)
                {
                    result.Add(new BlockDisplacementArchive
                    {
                        type = ClassicType,
                        blockIndexLocal = classic.BlockIndex_Local,
                        classic = ClassicBlockDisplacementArchive.FromClassic(classic)
                    });
                }
                else
                {
                    Debug.LogWarning($"[SceneDataJsonArchive] 未知 IBlockDisplacementData 类型: {block?.GetType().Name ?? "null"}");
                }
            }

            return result;
        }

        public static List<IBlockDisplacementData> ToBlockList(List<BlockDisplacementArchive> blocks)
        {
            var result = new List<IBlockDisplacementData>();
            if (blocks == null) return result;

            foreach (var block in blocks)
            {
                if (block == null) continue;
                if (block.type == ClassicType && block.classic != null)
                {
                    result.Add(block.classic.ToClassic(block.blockIndexLocal));
                }
                else
                {
                    Debug.LogWarning($"[SceneDataJsonArchive] 无法反序列化 BlockDisplacement 类型: {block.type}");
                }
            }

            return result;
        }
    }

    [Serializable]
    public class ClassicBlockDisplacementArchive
    {
        public string turnType;
        public string displacementType;

        public static ClassicBlockDisplacementArchive FromClassic(ClassicBlockDisplacementData classic)
        {
            return new ClassicBlockDisplacementArchive
            {
                turnType = classic.turnType.ToString(),
                displacementType = classic.displacementType.ToString()
            };
        }

        public ClassicBlockDisplacementData ToClassic(int blockIndexLocal)
        {
            var data = new ClassicBlockDisplacementData(blockIndexLocal);
            if (Enum.TryParse(turnType, out ClassicBlockDisplacementData.TurnType turn))
            {
                data.turnType = turn;
            }

            if (Enum.TryParse(displacementType, out ClassicBlockDisplacementData.DisplacementType displacement))
            {
                data.displacementType = displacement;
            }

            return data;
        }
    }

    [Serializable]
    public struct SerializableVector3
    {
        public float x;
        public float y;
        public float z;

        public static SerializableVector3 FromVector3(Vector3 vector)
        {
            return new SerializableVector3 { x = vector.x, y = vector.y, z = vector.z };
        }

        public Vector3 ToVector3() => new Vector3(x, y, z);
    }

    [Serializable]
    public struct SerializableQuaternion
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public static SerializableQuaternion FromQuaternion(Quaternion quaternion)
        {
            return new SerializableQuaternion
            {
                x = quaternion.x,
                y = quaternion.y,
                z = quaternion.z,
                w = quaternion.w
            };
        }

        public Quaternion ToQuaternion() => new Quaternion(x, y, z, w);
    }
}