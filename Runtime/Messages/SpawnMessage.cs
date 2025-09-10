using System.Collections.Generic;
using MessagePack;
using UnityEngine;

namespace SimpleNet.Messages
{
    /// <summary>
    /// Message for spawning a network object.
    /// </summary>
    [MessagePackObject]
    public class SpawnMessage : NetMessage
    {
        [Key(1)]public string prefabName;
        [Key(2)]public Vector3 position;
        [Key(3)]public Quaternion rotation;
        [Key(4)]public int requesterId;
        [Key(5)]public int owner;
        [Key(6)]public int netObjectId;
        [Key(7)]public string sceneId;

        public SpawnMessage(){}
        /// <param name="requesterId">The ID of the client requesting the spawn.</param>
        /// <param name="prefabName">The name of the prefab to spawn.</param>
        /// <param name="position">The position where the object should be spawned.</param>
        /// <param name="rotation">The rotation of the spawned object. Defaults to Quaternion.identity.</param>
        /// <param name="owner">The ID of the client that will own the spawned object. Defaults to -1 (no owner).</param>
        /// <param name="sceneId">The ID of the scene where the object should be spawned. Defaults to empty string.</param>
        /// <param name="target">Optional list of target client IDs to receive this message.</param>
        public SpawnMessage(int requesterId, string prefabName, Vector3 position, Quaternion rotation = default, int owner = -1, string sceneId = "", List<int> target = null) : base(target)
        {
            this.requesterId = requesterId;
            this.prefabName = prefabName;
            this.position = position;
            this.rotation = rotation;
            this.owner = owner;
            this.sceneId = sceneId;
            netObjectId = -1;
        }

        public override string ToString()
        {
            return $"{base.ToString()}, Requester:{requesterId} Prefab:{prefabName}, ID:{netObjectId}, Own by :{owner}, Pos:{position}, Rot: {rotation} SceneID:{sceneId}";
        }
    }
}
