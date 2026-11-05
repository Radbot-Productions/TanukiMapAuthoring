using UnityEditor;
using UnityEngine;
namespace Radbot.MapAuthoring.Editor
{
    [CustomEditor(typeof(PortableMapSkybox))]
    public sealed class PortableMapSkyboxInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var lighting=((PortableMapSkybox)target).GetComponent<PortableMapBaseLighting>();
            var settings=new SerializedObject(lighting);settings.Update();
            EditorGUILayout.PropertyField(settings.FindProperty("profile"),new GUIContent("Sky Preset"));settings.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("Skybox and Base Lighting share this preset. The local sky uses a static HDR reference; Play in Tanuki uses the live game sky, fog and postprocessing.",MessageType.Info);
        }
    }
}
