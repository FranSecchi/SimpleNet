using UnityEngine;
using SimpleNet.Synchronization;
using UnityEditor;

namespace SimpleNet.Editor
{
    public class NetTransformEditor
    {
        [MenuItem("GameObject/SimpleNet/NetTransform", false, 0)]
        public static void CreateNetTransform()
        {
            GameObject go = new GameObject("NetTransform");
            go.AddComponent<NetTransform>();
            
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }
        [MenuItem("GameObject/SimpleNet/NetRigidbody", false, 1)]
        public static void CreateNetRigidbody()
        {
            GameObject go = new GameObject("NetRigidbody");
            go.AddComponent<NetRigidbody>();
            
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }
        [MenuItem("GameObject/SimpleNet/NetRigidbody2D", false, 2)]
        public static void CreateNetRigidbody2D()
        {
            GameObject go = new GameObject("NetRigidbody2D");
            go.AddComponent<NetRigidbody2D>();
            
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }
    }
} 