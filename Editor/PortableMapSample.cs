using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Radbot.MapAuthoring.Editor
{
    public static class PortableMapSample
    {
        [MenuItem("Tanuki/Create Sample Map")]
        public static void Create()
        {
            const string folder="Assets/TanukiMapSample";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(folder+"/Room.unity"))
                throw new InvalidOperationException("Sample already exists. Open Assets/TanukiMapSample/Room.unity.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Directory.CreateDirectory(folder);
            var texture=new Texture2D(8,8,TextureFormat.RGBA32,false);
            for (int y=0;y<8;y++) for(int x=0;x<8;x++) texture.SetPixel(x,y,((x+y)%2==0) ? Color.white : new Color(.1f,.3f,.8f));
            texture.Apply(); File.WriteAllBytes(folder+"/Checker.png",texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.Refresh();
            var shader=Shader.Find(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline ? "Universal Render Pipeline/Lit" : "Standard");
            if(!shader) throw new InvalidOperationException("The sample requires the Built-in or Universal Render Pipeline.");
            var material=new Material(shader);
            material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/Checker.png");
            material.SetFloat(material.HasProperty("_Smoothness") ? "_Smoothness" : "_Glossiness",.2f); AssetDatabase.CreateAsset(material,folder+"/Checker.mat");
            var map=new GameObject("Unity Authored Room").AddComponent<PortableMapRoot>(); map.mapId="unity-prototype-room";
            Action<string,Vector3,Vector3> cube=(name,position,scale)=>
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(map.transform);
                go.transform.localPosition=position; go.transform.localScale=scale; go.GetComponent<Renderer>().sharedMaterial=material;
            };
            cube("Floor",new Vector3(0,-.25f,0),new Vector3(12,.5f,12));
            cube("Back Wall",new Vector3(0,1.5f,5.75f),new Vector3(12,3,.5f));
            cube("Left Wall",new Vector3(-5.75f,1.5f,0),new Vector3(.5f,3,12));
            cube("Door Left",new Vector3(-3.5f,1.5f,-5.75f),new Vector3(5,3,.5f));
            cube("Door Right",new Vector3(3.5f,1.5f,-5.75f),new Vector3(5,3,.5f));
            cube("Door Lintel",new Vector3(0,2.75f,-5.75f),new Vector3(2,.5f,.5f));
            cube("Mirrored Rotated Prop",new Vector3(2,.5f,1),new Vector3(-1,1,2));
            var prop=map.transform.Find("Mirrored Rotated Prop"); prop.localRotation=Quaternion.Euler(0,35,0);
            // A custom triangle collision mesh, independent of the primitive colliders.
            var ramp=GameObject.CreatePrimitive(PrimitiveType.Cube); ramp.name="Triangle Collision Ramp"; ramp.transform.SetParent(map.transform);
            ramp.transform.localPosition=new Vector3(-2,.4f,1); ramp.transform.localScale=new Vector3(2,.3f,3); ramp.transform.localRotation=Quaternion.Euler(12,0,0);
            ramp.GetComponent<Renderer>().sharedMaterial=material; Object.DestroyImmediate(ramp.GetComponent<BoxCollider>());
            ramp.AddComponent<MeshCollider>().sharedMesh=ramp.GetComponent<MeshFilter>().sharedMesh;
            var marker=new GameObject("Player Start"); marker.AddComponent<PortablePlayerStart>();
            var prefab=PrefabUtility.SaveAsPrefabAsset(marker,folder+"/PlayerStart.prefab"); Object.DestroyImmediate(marker);
            for(int i=0;i<2;i++)
            {
                var start=(GameObject)PrefabUtility.InstantiatePrefab(prefab); start.transform.SetParent(map.transform);
                start.transform.localPosition=new Vector3(i*2-1,1,-3); start.transform.localRotation=Quaternion.Euler(0,i*90,0);
            }
            EditorSceneManager.SaveScene(map.gameObject.scene,folder+"/Room.unity");
            Selection.activeGameObject=map.gameObject;
            Debug.Log("TANUKI_SAMPLE_CREATED");
        }

        /// <summary>Batch entry point: Unity -batchmode -projectPath ... -executeMethod ...BuildAndExport -tanukiExport ... -quit</summary>
        public static void BuildAndExport()
        {
            var args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"-tanukiExport");
            if(index<0 || index+1>=args.Length) throw new InvalidOperationException("Missing -tanukiExport directory.");
            const string sample="Assets/TanukiMapSample/Room.unity";
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(sample)) EditorSceneManager.OpenScene(sample);
            else Create();
            var root=Object.FindObjectOfType<PortableMapRoot>();
            PortableMapExporter.Export(root,args[index+1]);
            // Prove unsupported authoring fails instead of silently dropping gameplay collision.
            var box=root.GetComponentInChildren<BoxCollider>(); box.isTrigger=true;
            bool rejected=false;
            try { PortableMapExporter.Export(root,Path.Combine(args[index+1],"invalid-trigger")); }
            catch(InvalidOperationException) { rejected=true; }
            finally { box.isTrigger=false; }
            if(!rejected) throw new InvalidOperationException("Trigger rejection regression.");
            Debug.Log("TANUKI_UNITY_EXPORT_TESTS_PASSED");
        }
    }
}
