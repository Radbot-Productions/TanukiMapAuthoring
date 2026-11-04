using System;
using UnityEditor;
using UnityEngine;

namespace Radbot.MapAuthoring.Editor
{
    public sealed class PortableMapMaterialEditor : EditorWindow
    {
        Material material;Vector2 scroll;string error="";
        [MenuItem("Tanuki/Materials/Material Editor",priority=0)]
        public static void Open(){var window=GetWindow<PortableMapMaterialEditor>("Tanuki Material");window.minSize=new Vector2(380,500);window.Follow();window.Show();}
        void OnEnable(){Selection.selectionChanged+=Follow;Undo.undoRedoPerformed+=UndoChanged;Follow();}
        void OnDisable(){Selection.selectionChanged-=Follow;Undo.undoRedoPerformed-=UndoChanged;}
        void Follow(){var selected=Selection.activeObject as Material;var renderer=Selection.activeGameObject?Selection.activeGameObject.GetComponent<Renderer>():null;if(selected)material=selected;else if(renderer&&renderer.sharedMaterial)material=renderer.sharedMaterial;Repaint();}
        void UndoChanged(){try{var data=PortableMapMaterialDefinition.Find(material);if(data)data.Apply();}catch(Exception e){error=e.Message;}Repaint();}
        void OnGUI()
        {
            material=(Material)EditorGUILayout.ObjectField("Material",material,typeof(Material),false);
            if(GUILayout.Button("New Tanuki Material"))
            {
                string path=EditorUtility.SaveFilePanelInProject("Create Tanuki Material","Tanuki PBR","mat","Choose the material asset location.");
                if(!string.IsNullOrEmpty(path)){material=PortableMapMaterials.Create(PortableMapMaterials.Profile.PBR);AssetDatabase.CreateAsset(material,path);var data=PortableMapMaterialDefinition.Attach(material,false);data.Apply();var renderer=Selection.activeGameObject?Selection.activeGameObject.GetComponent<Renderer>():null;if(renderer){Undo.RecordObject(renderer,"Assign Tanuki Material");renderer.sharedMaterial=material;}AssetDatabase.SaveAssetIfDirty(material);}
            }
            if(!material)return;
            var settings=PortableMapMaterialDefinition.Find(material);
            if(!settings)
            {
                EditorGUILayout.HelpBox("Use shared Tanuki inputs for texture slots and scalar fallback values. Conversion applies the shared material contract to this material.",MessageType.Info);
                if(GUILayout.Button("Set Up Shared Inputs")){try{PortableMapMaterials.Validate(material);settings=PortableMapMaterialDefinition.Attach(material);settings.Apply();error="";}catch(Exception e){error=e.Message;}}
                if(!settings){if(error.Length>0)EditorGUILayout.HelpBox(error,MessageType.Error);return;}
            }
            scroll=EditorGUILayout.BeginScrollView(scroll);
            var serialized=new SerializedObject(settings);serialized.Update();EditorGUI.BeginChangeCheck();
            void Field(string name,string label)=>EditorGUILayout.PropertyField(serialized.FindProperty(name),new GUIContent(label));
            Field("surface","Surface");Field("baseColor","Base Color");Field("baseColorTexture","Base Color Texture");
            using(new EditorGUI.DisabledScope(settings.roughnessTexture))Field("roughness","Roughness");Field("roughnessTexture","Roughness Texture");
            using(new EditorGUI.DisabledScope(settings.metallicTexture))Field("metallic","Metallic");Field("metallicTexture","Metallic Texture");
            Field("normalTexture","Normal Texture");Field("normalStrength","Normal Strength");Field("aoTexture","AO Texture");
            if(settings.surface==PortableMapMaterials.Profile.Emissive){Field("emissionColor","Emission Color");Field("emissionStrength","Emission Strength");Field("emissionTexture","Emission Texture");}
            if(settings.surface is PortableMapMaterials.Profile.Cutout or PortableMapMaterials.Profile.Transparent)Field("opacity","Opacity");
            if(settings.surface==PortableMapMaterials.Profile.Cutout)Field("alphaCutoff","Alpha Cutoff");
            Field("uvTiling","UV Tiling");Field("uvOffset","UV Offset");
            if(EditorGUI.EndChangeCheck()){serialized.ApplyModifiedProperties();try{Undo.RecordObject(material,"Edit Tanuki Material");settings.Apply();error="";}catch(Exception e){error=e.Message;}}
            EditorGUILayout.HelpBox("Data textures use their red channel. Empty slots use the scalar values. Texture packing is automatic.",MessageType.Info);
            if(error.Length>0)EditorGUILayout.HelpBox(error,MessageType.Error);
            if(GUILayout.Button("Rebuild Preview")){try{settings.Apply();error="";}catch(Exception e){error=e.Message;}}
            if(GUILayout.Button("Save Material"))AssetDatabase.SaveAssetIfDirty(material);
            EditorGUILayout.EndScrollView();
        }
    }
}
