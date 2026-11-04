using System;
using UnityEditor;
using UnityEngine;

namespace Radbot.MapAuthoring.Editor
{
    public sealed class PortableMapMaterialEditor : EditorWindow
    {
        Material material; Vector2 scroll; string error="";
        [MenuItem("Tanuki/Materials/Material Editor",priority=20)]
        public static void Open(){var window=GetWindow<PortableMapMaterialEditor>("Tanuki Material");window.minSize=new Vector2(380,500);window.Follow();window.Show();}
        void OnEnable(){Selection.selectionChanged+=Follow;Undo.undoRedoPerformed+=Repaint;Follow();}
        void OnDisable(){Selection.selectionChanged-=Follow;Undo.undoRedoPerformed-=Repaint;}
        void Follow(){var selected=Selection.activeObject as Material;var renderer=Selection.activeGameObject?Selection.activeGameObject.GetComponent<Renderer>():null;if(selected)material=selected;else if(renderer&&renderer.sharedMaterial)material=renderer.sharedMaterial;Repaint();}
        void OnGUI()
        {
            material=(Material)EditorGUILayout.ObjectField("Material",material,typeof(Material),false);
            if(!material){EditorGUILayout.HelpBox("Create a material from Tanuki > Materials, or select an existing material.",MessageType.Info);return;}
            scroll=EditorGUILayout.BeginScrollView(scroll);DrawInputs(material,ref error);EditorGUILayout.EndScrollView();
        }
        public static void DrawInputs(Material material,ref string error)
        {
            bool sample=AssetDatabase.GetAssetPath(material).StartsWith("Packages/",StringComparison.Ordinal);
            var settings=PortableMapMaterialDefinition.Find(material);
            if(!settings)
            {
                if(sample)settings=PortableMapMaterialDefinition.Attach(material);
                else
                {
                    EditorGUILayout.HelpBox("Set up the supported Tanuki inputs before editing this material.",MessageType.Info);
                    if(GUILayout.Button("Set Up Shared Inputs")){try{PortableMapMaterials.Validate(material);settings=PortableMapMaterialDefinition.Attach(material);settings.Apply();error="";}catch(Exception e){error=e.Message;}}
                    if(!settings){if(error.Length>0)EditorGUILayout.HelpBox(error,MessageType.Error);return;}
                }
            }
            if(sample)EditorGUILayout.HelpBox("Plugin sample. Duplicate this material into your project, or use Tanuki > Materials to create an editable copy.",MessageType.Info);
            using(new EditorGUI.DisabledScope(sample))
            {
                var serialized=new SerializedObject(settings);serialized.Update();EditorGUI.BeginChangeCheck();
                int current=settings.surface==PortableMapMaterials.Profile.Cutout?1:settings.surface==PortableMapMaterials.Profile.Transparent?2:0;
                int selected=EditorGUILayout.Popup("Surface",current,new[]{"PBR","Cutout","Transparent"});
                serialized.FindProperty("surface").intValue=selected==1?2:selected==2?3:0;
                void Field(string name,string label)=>EditorGUILayout.PropertyField(serialized.FindProperty(name),new GUIContent(label));
                Field("baseColor","Base Color");Field("baseColorTexture","Base Color Texture");
                using(new EditorGUI.DisabledScope(settings.roughnessTexture))Field("roughness","Roughness");Field("roughnessTexture","Roughness Texture");
                using(new EditorGUI.DisabledScope(settings.metallicTexture))Field("metallic","Metallic");Field("metallicTexture","Metallic Texture");
                Field("normalTexture","Normal Texture");if(settings.normalTexture)Field("normalStrength","Normal Strength");Field("aoTexture","AO Texture");
                if(selected==0)
                {
                    Field("emissionEnabled","Emission");
                    if(serialized.FindProperty("emissionEnabled").boolValue){Field("emissionColor","Emission Color");Field("emissionStrength","Emission Strength");Field("emissionTexture","Emission Texture");}
                }
                if(selected!=0)Field("opacity","Opacity");if(selected==1)Field("alphaCutoff","Alpha Cutoff");
                Field("uvTiling","UV Tiling");Field("uvOffset","UV Offset");
                if(EditorGUI.EndChangeCheck()){Undo.RecordObject(material,"Edit Tanuki Material");serialized.ApplyModifiedProperties();try{settings.Apply();error="";}catch(Exception e){error=e.Message;}}
                EditorGUILayout.HelpBox("Data textures use their red channel. Empty slots use the scalar values. Texture packing is automatic.",MessageType.Info);
                if(GUILayout.Button("Rebuild Preview")){try{Undo.RecordObject(material,"Rebuild Tanuki Material");settings.Apply();error="";}catch(Exception e){error=e.Message;}}
                if(GUILayout.Button("Save Material"))AssetDatabase.SaveAssetIfDirty(material);
            }
            if(error.Length>0)EditorGUILayout.HelpBox(error,MessageType.Error);
        }
    }
    public sealed class PortableMapMaterialShaderGUI : ShaderGUI
    {
        string error="";
        public override void OnGUI(MaterialEditor editor,MaterialProperty[] properties)
        {
            if(editor.targets.Length!=1){EditorGUILayout.HelpBox("Select one material to edit its portable inputs.",MessageType.Info);return;}
            PortableMapMaterialEditor.DrawInputs((Material)editor.target,ref error);
        }
    }
}
