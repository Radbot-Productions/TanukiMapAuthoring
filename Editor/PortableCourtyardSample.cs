using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Radbot.MapAuthoring.Editor
{
    /// <summary>Original, self-contained courtyard art. No downloaded assets or shader dependencies.</summary>
    public static class PortableCourtyardSample
    {
        public const string ScenePath="Assets/TanukiCourtyard/Courtyard.unity";
        const string Folder="Assets/TanukiCourtyard";
        static Transform root;
        static Shader shader;
        static Mesh roof, foliage;
        static Material plaster, timber, roofRed, roofDark, stone, paving, wood, soil, leaves, lightLeaves, blue, paper;

        [MenuItem("Tanuki/Samples/Open Courtyard")]
        public static void OpenMenu() { OpenOrCreate(); }

        public static void BuildAndExport()
        {
            var args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"-tanukiExport");
            if(index<0 || index+1>=args.Length) throw new InvalidOperationException("Missing -tanukiExport folder.");
            var map=OpenOrCreate();
            if(!map) throw new InvalidOperationException("Courtyard creation cancelled.");
            Physics.SyncTransforms();
            foreach(var start in map.GetComponentsInChildren<PortablePlayerStart>())
            {
                var center=start.transform.position;
                foreach(var hit in Physics.OverlapCapsule(center+Vector3.up*.56f,center-Vector3.up*.56f,.4f,~0,QueryTriggerInteraction.Ignore))
                    if(hit.transform.IsChildOf(map.transform)) throw new InvalidOperationException("Courtyard start overlaps "+hit.name);
            }
            var data=PortableMapExporter.Prepare(map); data.Write(args[index+1]);
            RenderPreview(Path.Combine(args[index+1],"courtyard-unity.png"));
            File.WriteAllText(Path.Combine(args[index+1],"success.txt"),$"meshes={data.MeshCount} colliders={data.ColliderCount} starts={data.PlayerStartCount}");
            Debug.Log("TANUKI_COURTYARD_EXPORT_PASSED");
        }

        public static void RenderPreview(string filename)
        {
            var camera=Camera.main;
            if(!camera) throw new InvalidOperationException("Courtyard preview camera is missing.");
            var target=new RenderTexture(1440,1000,24);
            var previous=RenderTexture.active; var oldTarget=camera.targetTexture;
            Texture2D image=null;
            try
            {
                camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                image=new Texture2D(1440,1000,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1440,1000),0,0); image.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(filename));
                File.WriteAllBytes(filename,image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=oldTarget; RenderTexture.active=previous;
                if(image) Object.DestroyImmediate(image); target.Release(); Object.DestroyImmediate(target);
            }
        }

        public static PortableMapRoot OpenOrCreate()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return null;
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                var existing=Object.FindObjectOfType<PortableMapRoot>();
                Selection.activeGameObject=existing.gameObject;
                PortableMapAuthoringWindow.Open().SelectMap(existing);
                return existing;
            }
            if(Directory.Exists(Folder)) throw new InvalidOperationException("Assets/TanukiCourtyard already exists without its scene. Preserve/inspect that folder before creating the sample.");
            shader=Shader.Find(GraphicsSettings.currentRenderPipeline ? "Universal Render Pipeline/Lit" : "Standard");
            if(!shader) throw new InvalidOperationException("The courtyard requires Built-in or Universal Render Pipeline.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            plaster=Mat("Warm Plaster",new Color(.86f,.79f,.65f));
            timber=Mat("Dark Timber",new Color(.20f,.14f,.11f));
            roofRed=Mat("Terracotta",new Color(.66f,.22f,.13f));
            roofDark=Mat("Roof Trim",new Color(.38f,.10f,.07f));
            stone=Mat("Cool Stone",new Color(.41f,.48f,.49f));
            paving=Mat("Sandstone",new Color(.70f,.69f,.59f));
            wood=Mat("Honey Wood",new Color(.56f,.35f,.19f));
            soil=Mat("Planter Soil",new Color(.28f,.25f,.16f));
            leaves=Mat("Canopy Green",new Color(.20f,.43f,.25f));
            lightLeaves=Mat("Canopy Highlights",new Color(.39f,.57f,.29f));
            blue=Mat("Indigo Cloth",new Color(.19f,.32f,.46f));
            paper=Mat("Shoji Paper",new Color(.92f,.87f,.70f));
            roof=RoofMesh(); foliage=FoliageMesh();
            var map=new GameObject("Tanuki Courtyard").AddComponent<PortableMapRoot>();
            map.mapId="tanuki-courtyard"; root=map.transform;

            Box("Courtyard Foundation",new Vector3(0,-.22f,0),new Vector3(24,.44f,24),paving,true);
            Box("Entry Walk",new Vector3(0,.012f,-6),new Vector3(4.8f,.025f,12),stone,false);
            Box("Cross Walk",new Vector3(0,.014f,1),new Vector3(20,.025f,3),stone,false);
            for(int row=0;row<10;row++) for(int col=0;col<4;col++)
                Box("Entry Paver",new Vector3((col-1.5f)*1.04f,.035f,-10.8f+row*1.15f),new Vector3(.98f,.04f,1.08f),paving,false);
            for(int side=-1;side<=1;side+=2) for(int i=0;i<6;i++)
                Box("Walk Border",new Vector3(side*(2.6f+i*1.3f),.045f,-.5f),new Vector3(1.2f,.09f,.22f),paving,false);

            Building("Tea House",new Vector3(-4.8f,0,7.5f),7,4.4f,3.0f,0);
            Building("Storehouse",new Vector3(4.2f,0,7.7f),6,4.0f,2.7f,0);
            Building("West Shop",new Vector3(-8.1f,0,-1.0f),5.4f,4.2f,2.7f,-90);
            Building("East Shop",new Vector3(8.1f,0,-1.0f),5.4f,4.2f,2.7f,90);

            Cylinder("Central Planter",new Vector3(0,.18f,1.2f),new Vector3(3.8f,.18f,3.8f),stone,true);
            Cylinder("Central Soil",new Vector3(0,.38f,1.2f),new Vector3(3.25f,.04f,3.25f),soil,false);
            Tree("Courtyard Tree",new Vector3(0,.4f,1.2f),1.25f);
            Tree("North West Tree",new Vector3(-10.5f,0,9.8f),1.05f);
            Tree("North East Tree",new Vector3(10.5f,0,9.8f),1.10f);
            Tree("Entry Tree",new Vector3(-9.8f,0,-8.6f),.9f);

            for(int side=-1;side<=1;side+=2)
            {
                Box("Entry Gate Post",new Vector3(side*2.6f,1.65f,-9.6f),new Vector3(.38f,3.3f,.38f),roofRed,true);
                Box("Gate Stone Foot",new Vector3(side*2.6f,.20f,-9.6f),new Vector3(.65f,.4f,.65f),stone,true);
                Box("Gate Cap",new Vector3(side*2.9f,3.7f,-9.6f),new Vector3(1.2f,.18f,.65f),timber,false).transform.localRotation=Quaternion.Euler(0,0,side*8);
            }
            Box("Gate Main Beam",new Vector3(0,3.55f,-9.6f),new Vector3(6.4f,.32f,.6f),roofRed,true);
            Box("Gate Dark Ridge",new Vector3(0,3.78f,-9.6f),new Vector3(5.7f,.16f,.72f),timber,false);
            Box("Gate Lower Beam",new Vector3(0,2.85f,-9.6f),new Vector3(5.8f,.18f,.28f),roofRed,false);

            for(int i=0;i<9;i++)
            {
                float x=-10.8f+i*2.7f;
                Box("Back Fence Post",new Vector3(x,.72f,11.5f),new Vector3(.18f,1.44f,.18f),timber,true);
                if(i<8) for(int rail=0;rail<2;rail++) Box("Back Fence Rail",new Vector3(x+1.35f,.45f+rail*.55f,11.5f),new Vector3(2.7f,.12f,.12f),wood,false);
            }
            Bench(new Vector3(-3.3f,0,2),90); Bench(new Vector3(3.3f,0,2),-90);
            Crate(new Vector3(-6.3f,0,3.6f),.8f,12);
            Crate(new Vector3(-5.4f,0,3.8f),.65f,-8);
            Crate(new Vector3(5.5f,0,4.7f),.9f,-10);
            Crate(new Vector3(5.5f,.9f,4.7f),.6f,7);
            for(int i=0;i<3;i++) Cylinder("Barrel",new Vector3(6.8f+i*.75f,.48f,4.1f),new Vector3(.62f,.48f,.62f),wood,true);
            Planter(new Vector3(-5.8f,0,-5.2f)); Planter(new Vector3(5.8f,0,-5.2f));
            Lantern(new Vector3(-3.4f,0,-7.8f)); Lantern(new Vector3(3.4f,0,-7.8f));

            var starts=new[]{new Vector3(-1.3f,1,-6.5f),new Vector3(1.3f,1,-6.5f),new Vector3(-4.2f,1,.4f),new Vector3(4.2f,1,.4f)};
            foreach(var position in starts)
            {
                var go=new GameObject("Player Start"); go.transform.SetParent(root,false); go.transform.localPosition=position;
                var forward=new Vector3(-position.x,0,1-position.z); go.transform.localRotation=Quaternion.LookRotation(forward);
                go.AddComponent<PortablePlayerStart>();
            }

            // These support Unity authoring/preview and deliberately live outside the export root.
            var sun=new GameObject("Courtyard Preview Sun").AddComponent<Light>(); sun.type=LightType.Directional;
            sun.transform.rotation=Quaternion.Euler(48,-35,0); sun.color=new Color(1,.91f,.79f); sun.intensity=1.25f; sun.shadows=LightShadows.Soft;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.62f,.72f,.82f); RenderSettings.ambientEquatorColor=new Color(.53f,.57f,.57f); RenderSettings.ambientGroundColor=new Color(.32f,.29f,.24f);
            RenderSettings.fog=true; RenderSettings.fogColor=new Color(.76f,.83f,.85f); RenderSettings.fogMode=FogMode.Linear; RenderSettings.fogStartDistance=42; RenderSettings.fogEndDistance=85;
            var camera=new GameObject("Courtyard Preview Camera").AddComponent<Camera>(); camera.tag="MainCamera";
            camera.transform.position=new Vector3(24,21,-29); camera.transform.LookAt(new Vector3(0,1.2f,1));
            camera.orthographic=true; camera.orthographicSize=17; camera.nearClipPlane=.1f; camera.farClipPlane=120;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=RenderSettings.fogColor;
            EditorSceneManager.SaveScene(map.gameObject.scene,ScenePath);
            Selection.activeGameObject=map.gameObject;
            PortableMapAuthoringWindow.Open().SelectMap(map);
            if(SceneView.lastActiveSceneView) { SceneView.lastActiveSceneView.pivot=new Vector3(0,1,1); SceneView.lastActiveSceneView.rotation=camera.transform.rotation; SceneView.lastActiveSceneView.size=22; SceneView.lastActiveSceneView.orthographic=true; SceneView.lastActiveSceneView.Repaint(); }
            Debug.Log("TANUKI_COURTYARD_CREATED");
            return map;
        }

        static Material Mat(string name,Color color)
        {
            var material=new Material(shader) { name=name, color=color };
            material.SetFloat(material.HasProperty("_Smoothness") ? "_Smoothness" : "_Glossiness",.12f);
            AssetDatabase.CreateAsset(material,Folder+"/"+name+".mat"); return material;
        }
        static GameObject Box(string name,Vector3 position,Vector3 size,Material material,bool collision)
        {
            return Primitive(PrimitiveType.Cube,name,position,size,material,collision);
        }
        static GameObject Cylinder(string name,Vector3 position,Vector3 scale,Material material,bool collision)
        {
            var go=Primitive(PrimitiveType.Cylinder,name,position,scale,material,false);
            if(collision) { var mesh=go.AddComponent<MeshCollider>(); mesh.sharedMesh=go.GetComponent<MeshFilter>().sharedMesh; }
            return go;
        }
        static GameObject Primitive(PrimitiveType type,string name,Vector3 position,Vector3 scale,Material material,bool collision)
        {
            var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(root,false); go.transform.localPosition=position; go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=material;
            if(!collision) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        static GameObject MeshObject(string name,Mesh mesh,Vector3 position,Vector3 scale,Material material,bool collision)
        {
            var go=new GameObject(name); go.transform.SetParent(root,false); go.transform.localPosition=position; go.transform.localScale=scale;
            go.AddComponent<MeshFilter>().sharedMesh=mesh; go.AddComponent<MeshRenderer>().sharedMaterial=material;
            if(collision) go.AddComponent<MeshCollider>().sharedMesh=mesh;
            return go;
        }
        static void Building(string name,Vector3 position,float width,float depth,float height,float yaw)
        {
            Transform parent=root; var group=new GameObject(name); group.transform.SetParent(parent,false); group.transform.localPosition=position; group.transform.localRotation=Quaternion.Euler(0,yaw,0); root=group.transform;
            Box("Stone Plinth",new Vector3(0,.18f,0),new Vector3(width+.25f,.36f,depth+.25f),stone,true);
            Box("Plaster Walls",new Vector3(0,height*.5f+.3f,0),new Vector3(width,height,depth),plaster,true);
            foreach(float x in new[]{-width*.5f,width*.5f}) foreach(float z in new[]{-depth*.5f,depth*.5f})
                Box("Timber Corner",new Vector3(x,height*.5f+.3f,z),new Vector3(.19f,height+.25f,.19f),timber,false);
            foreach(float y in new[]{.4f,1.2f,height+.27f})
                Box("Front Timber Rail",new Vector3(0,y,-depth*.5f-.035f),new Vector3(width+.12f,.14f,.14f),timber,false);
            MeshObject("Gabled Tile Roof",roof,new Vector3(0,height+.3f,0),new Vector3(width+1.0f,1.35f,depth+1.0f),roofRed,true);
            Box("Roof Ridge",new Vector3(0,height+1.73f,0),new Vector3(.19f,.18f,depth+1.15f),roofDark,false);
            foreach(int side in new[]{-1,1}) Box("Roof Eave",new Vector3(side*(width*.5f+.5f),height+.3f,0),new Vector3(.16f,.18f,depth+1.12f),roofDark,false);
            Box("Door",new Vector3(0,1.3f,-depth*.5f-.09f),new Vector3(1.1f,1.85f,.15f),timber,false);
            Box("Door Inset",new Vector3(0,1.55f,-depth*.5f-.18f),new Vector3(.80f,1.12f,.06f),wood,false);
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*width*.29f;
                Box("Shoji Window",new Vector3(x,1.9f,-depth*.5f-.09f),new Vector3(1.35f,1.45f,.10f),paper,false);
                for(int i=-1;i<=1;i++) Box("Window Mullion",new Vector3(x+i*.4f,1.9f,-depth*.5f-.16f),new Vector3(.055f,1.52f,.06f),timber,false);
                Box("Window Crossbar",new Vector3(x,1.9f,-depth*.5f-.17f),new Vector3(1.44f,.065f,.065f),timber,false);
            }
            Box("Indigo Shop Curtain",new Vector3(0,2.55f,-depth*.5f-.27f),new Vector3(1.5f,.48f,.06f),blue,false);
            Box("Porch",new Vector3(0,.24f,-depth*.5f-.65f),new Vector3(width+.1f,.24f,1.3f),wood,true);
            Box("Porch Step",new Vector3(0,.08f,-depth*.5f-1.45f),new Vector3(2.1f,.16f,.52f),stone,true);
            root=parent;
        }
        static void Tree(string name,Vector3 position,float scale)
        {
            Box(name+" Trunk",position+new Vector3(0,1.0f*scale,0),new Vector3(.35f,2,.35f)*scale,timber,true);
            MeshObject(name+" Canopy",foliage,position+new Vector3(0,2.8f*scale,0),new Vector3(3.5f,2.4f,3.1f)*scale,leaves,false);
            MeshObject(name+" Crown",foliage,position+new Vector3(-.7f,3.35f,.25f)*scale,new Vector3(2.4f,1.8f,2.2f)*scale,lightLeaves,false);
            MeshObject(name+" Side Crown",foliage,position+new Vector3(.95f,2.7f,.15f)*scale,new Vector3(2.2f,1.9f,2.2f)*scale,lightLeaves,false);
        }
        static void Planter(Vector3 p)
        {
            Cylinder("Terracotta Pot",p+Vector3.up*.3f,new Vector3(.9f,.3f,.9f),roofRed,true);
            MeshObject("Planter Foliage",foliage,p+Vector3.up*.9f,new Vector3(1.25f,1.1f,1.1f),lightLeaves,false);
        }
        static void Bench(Vector3 p,float yaw)
        {
            Transform parent=root; var group=new GameObject("Courtyard Bench"); group.transform.SetParent(parent,false); group.transform.localPosition=p; group.transform.localRotation=Quaternion.Euler(0,yaw,0); root=group.transform;
            Box("Bench Seat",new Vector3(0,.52f,0),new Vector3(2.1f,.15f,.65f),wood,true);
            for(int side=-1;side<=1;side+=2) Box("Bench Support",new Vector3(side*.75f,.24f,0),new Vector3(.22f,.48f,.55f),timber,true);
            root=parent;
        }
        static void Crate(Vector3 p,float size,float yaw)
        {
            Transform parent=root; var group=new GameObject("Crate"); group.transform.SetParent(parent,false); group.transform.localPosition=p; group.transform.localRotation=Quaternion.Euler(0,yaw,0); root=group.transform;
            Box("Wooden Crate",Vector3.up*size*.5f,Vector3.one*size,wood,true);
            for(int side=-1;side<=1;side+=2) Box("Crate Band",new Vector3(0,size*.5f,side*size*.51f),new Vector3(size*.85f,.075f,.04f),timber,false);
            root=parent;
        }
        static void Lantern(Vector3 p)
        {
            Box("Lantern Stone Base",p+Vector3.up*.2f,new Vector3(.7f,.4f,.7f),stone,true);
            Box("Lantern Post",p+Vector3.up*.85f,new Vector3(.2f,1.1f,.2f),timber,true);
            Box("Lantern Paper",p+Vector3.up*1.55f,new Vector3(.55f,.65f,.55f),paper,false);
            MeshObject("Lantern Roof",roof,p+Vector3.up*1.9f,new Vector3(.9f,.3f,.9f),roofDark,false);
        }
        static Mesh RoofMesh()
        {
            var v=new[]{new Vector3(-.5f,0,-.5f),new Vector3(0,1,-.5f),new Vector3(.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(0,1,.5f),new Vector3(.5f,0,.5f)};
            return FlatMesh("Gable",v,new[]{0,1,2,3,5,4,0,3,4,0,4,1,1,4,5,1,5,2,0,2,5,0,5,3});
        }
        static Mesh FoliageMesh()
        {
            float t=(1+Mathf.Sqrt(5))*.5f;
            var v=new[]{new Vector3(-1,t,0),new Vector3(1,t,0),new Vector3(-1,-t,0),new Vector3(1,-t,0),new Vector3(0,-1,t),new Vector3(0,1,t),new Vector3(0,-1,-t),new Vector3(0,1,-t),new Vector3(t,0,-1),new Vector3(t,0,1),new Vector3(-t,0,-1),new Vector3(-t,0,1)};
            for(int i=0;i<v.Length;i++) v[i]=v[i].normalized*.5f;
            return FlatMesh("Faceted Canopy",v,new[]{0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,4,9,5,2,4,11,6,2,10,8,6,7,9,8,1});
        }
        static Mesh FlatMesh(string name,Vector3[] positions,int[] indices)
        {
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            foreach(int index in indices) { triangles.Add(vertices.Count); vertices.Add(positions[index]); }
            var mesh=new Mesh { name=name }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,Folder+"/"+name+".asset"); return mesh;
        }
    }
}
