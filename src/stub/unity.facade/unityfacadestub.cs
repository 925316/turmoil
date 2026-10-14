// Compile-time facade stub for UnityEngine (identity: Version=0.0.0.0, unsigned).
// Real Unity's UnityEngine.dll is a pure facade that type-forwards to the module
// assemblies. BepInEx.dll was compiled against THIS facade, so its base-type chains
// (BaseUnityPlugin -> MonoBehaviour) name assembly "UnityEngine". Forward the types
// the compiler needs here; at runtime the game's real facade answers the same way.

using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: TypeForwardedTo(typeof(MonoBehaviour))]
