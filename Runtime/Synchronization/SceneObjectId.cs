using System;
using UnityEditor;
using UnityEngine;

namespace SimpleNet.Synchronization
{
    [ExecuteAlways]
    internal class SceneObjectId : MonoBehaviour
    {
        [SerializeField] private string sceneId;
        public string SceneId => sceneId;

        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                if (string.IsNullOrEmpty(sceneId) || HasDuplicateIdInScene())
                {
                    sceneId = Guid.NewGuid().ToString();
                    EditorUtility.SetDirty(this);
                }
            }
        }

        private bool HasDuplicateIdInScene()
        {
            var allObjects = FindObjectsOfType<SceneObjectId>();
            foreach (var obj in allObjects)
            {
                if (obj != this && obj.sceneId == this.sceneId)
                    return true;
            }
            return false;
        }
#endif
    }
}
