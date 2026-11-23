using UnityEngine;

namespace HexFall.World
{
    // Keeps a ground-mist particle emitter under the camera so any map size is covered: the emitter
    // slides along the ground with the view while its world-space particles stay where they were born.
    [ExecuteAlways]
    public class GroundMist : MonoBehaviour
    {
        [Tooltip("Height of the emitter above the world origin (the floor of the map).")]
        [SerializeField] private float height = 0.2f;

        public static Vector3 Anchor(Vector3 target, float height) => new Vector3(target.x, height, target.z);

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.position = Anchor(cam.transform.position, height);
        }
    }
}
