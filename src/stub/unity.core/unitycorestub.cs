// Compile-time API stub for UnityEngine.CoreModule (identity: Version=0.0.0.0,
// unsigned). Only the members tmmod touches; runtime binds to the game's real modules.
// Member kinds and signatures must match real Unity (reflection-verified against the
// game's Managed folder): Rect.x is a property, KeyCode.Insert is const 277 (consts
// are inlined into our IL at compile time), FindObjectOfType takes System.Type.

namespace UnityEngine
{
    public class Object
    {
        public static Object FindObjectOfType(System.Type type) { return null; }
    }

    public class Component : Object
    {
    }

    public class Behaviour : Component
    {
    }

    public class MonoBehaviour : Behaviour
    {
    }

    public struct Rect
    {
        public float x { get; set; }
        public float y { get; set; }
        public float width { get; set; }
        public float height { get; set; }

        public Rect(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }
    }

    public enum KeyCode
    {
        Insert = 277
    }

    public static class Input
    {
        public static bool GetKeyDown(KeyCode key) { return false; }
    }
}
