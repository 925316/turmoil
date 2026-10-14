// Compile-time API stub for UnityEngine.IMGUIModule (identity: Version=0.0.0.0,
// unsigned). Only the members tmmod touches. Real signatures verified by
// reflection: GUI.Window takes the nested GUI.WindowFunction delegate and returns
// Rect; GUILayout option parameters are params arrays.

using UnityEngine;

namespace UnityEngine
{
    public static class GUI
    {
        public delegate void WindowFunction(int windowID);

        public static Rect Window(int id, Rect screenRect, WindowFunction func, string text) { return default(Rect); }
        public static void DragWindow() { }
    }

    public sealed class GUILayoutOption
    {
    }

    public static class GUILayout
    {
        public static GUILayoutOption Width(float width) { return null; }

        public static bool Button(string text, params GUILayoutOption[] options) { return false; }
        public static bool Toggle(bool value, string text, params GUILayoutOption[] options) { return false; }
        public static string TextField(string text, params GUILayoutOption[] options) { return null; }
        public static void Label(string text, params GUILayoutOption[] options) { }
        public static void BeginHorizontal(params GUILayoutOption[] options) { }
        public static void EndHorizontal() { }
    }
}
