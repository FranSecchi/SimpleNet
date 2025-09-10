using MessagePack;

namespace SimpleNet.Messages
{
    /// <summary>
    /// Message for scene loading synchronization.
    /// </summary>
    [MessagePackObject]
    public class SceneLoadMessage : NetMessage
    {
        [Key(1)] public string sceneName;
        [Key(2)] public bool isLoaded;
        [Key(3)] public int requesterId;

        public SceneLoadMessage(){}
        /// <param name="sceneName">The name of the scene being loaded or unloaded.</param>
        /// <param name="requesterId">The ID of the client requesting the scene operation.</param>
        /// <param name="isLoaded">Indicates whether the scene is loaded (true) or unloaded (false). Defaults to false.</param>
        public SceneLoadMessage(string sceneName, int requesterId, bool isLoaded = false)
        {
            this.sceneName = sceneName;
            this.requesterId = requesterId;
            this.isLoaded = isLoaded;
        }

        public override string ToString()
        {
            return $"{base.ToString()} Scene:{sceneName}, Loaded:{isLoaded}, Requester:{requesterId}";
        }
    }
} 