using System.Collections.Generic;
using UnityEngine;
using SimpleNet.Messages;
using SimpleNet.Network;
using SimpleNet.Transport;
using SimpleNet.Transport.UDP;
using SimpleNet.Utilities;

namespace SimpleNet.Synchronization
{
    [CreateAssetMenu(fileName = "NetPrefabs", menuName = "ScriptableObjects/PrefabList", order = 1)]
    public class NetPrefabRegistry : ScriptableObject
    {
        /// <summary>
        /// List of networked GameObjects that will be instantiated at runtime
        /// </summary>
        public List<GameObject> prefabs = new List<GameObject>();
    }
}
