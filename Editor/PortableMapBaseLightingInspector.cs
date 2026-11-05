using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Radbot.MapAuthoring.Editor
{
    [CustomEditor(typeof(PortableMapBaseLighting))]
    public sealed class PortableMapBaseLightingInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Enable Lighting and Effects in the Scene view to see the HDR lighting preview. Play in Tanuki remains the reference for final rendering.",MessageType.Info);
            if(!GraphicsSettings.currentRenderPipeline&&(PlayerSettings.colorSpace!=ColorSpace.Linear||!GraphicsSettings.lightsUseLinearIntensity))
            {
                EditorGUILayout.HelpBox("The Tanuki lighting preview needs Linear color space for consistent material colors and light response.",MessageType.Warning);
                if(GUILayout.Button("Use Linear Color Space")){PlayerSettings.colorSpace=ColorSpace.Linear;GraphicsSettings.lightsUseLinearIntensity=true;}
            }
        }
    }
}
