// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  DisableLeakDetection.cs — apaga la detección de fugas de
//  NativeContainers de Unity (Collections). Al conectar/desconectar
//  mucho en Multiplayer Play Mode, el Transport reporta "leaks"
//  (allocations no liberadas) y dumpea la consola con hex/stack.
//  Es ruido de librería (no de nuestro código), así que lo apagamos
//  en el editor para poder leer la consola.
//  Se puede reactivar comentando esto o poniendo Enabled.
// ============================================================
using Unity.Collections;
using UnityEditor;

namespace FolkloreArchives.Editor
{
    [InitializeOnLoad]
    static class DisableLeakDetection
    {
        static DisableLeakDetection()
        {
            NativeLeakDetection.Mode = NativeLeakDetectionMode.Disabled;
        }
    }
}
