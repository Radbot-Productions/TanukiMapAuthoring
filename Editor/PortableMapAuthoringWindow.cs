using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Radbot.MapAuthoring.Editor
{
    /// <summary>Native Unity authoring UI. Validation/export delegate to the production exporter.</summary>
    public sealed class PortableMapAuthoringWindow : EditorWindow
    {
        [SerializeField] PortableMapRoot map;
        [SerializeField] string exportFolder="";
        [SerializeField] string message="Choose a map root, or create a new map.";
        [SerializeField] bool hasError;
        Vector2 scroll;
        bool busy;

        public PortableMapRoot SelectedMap => map;
        public string LastMessage => message;
        public bool HasError => hasError;

        [MenuItem("Tanuki/Map Authoring",priority=0)]
        static void OpenMenu() { Open(); }

        public static PortableMapAuthoringWindow Open()
        {
            var window=GetWindow<PortableMapAuthoringWindow>("Tanuki Maps");
            window.minSize=new Vector2(390,540);
            window.Show(); return window;
        }

        [MenuItem("GameObject/Tanuki/Map Root",false,10)]
        static void CreateMapRootMenu() { CreateMapRoot(); }

        public static PortableMapRoot CreateMapRoot()
        {
            var go=new GameObject("Tanuki Map");
            var root=go.AddComponent<PortableMapRoot>(); root.mapId=Guid.NewGuid().ToString("N");
            Undo.RegisterCreatedObjectUndo(go,"Create Tanuki Map");
            Selection.activeGameObject=go;
            var window=Open(); window.SelectMap(root);
            return root;
        }

        [MenuItem("GameObject/Tanuki/Player Start",false,11)]
        static void CreateSelectedStart()
        {
            var window=Open(); window.FollowSelection();
            if (!window.map) { window.Fail("Choose a map root before adding a player start."); return; }
            window.AddPlayerStart();
        }

        public PortablePlayerStart AddPlayerStart()
        {
            if (!map) throw new InvalidOperationException("Choose a map root first.");
            var go=new GameObject("Player Start");
            go.transform.SetParent(map.transform,false); go.transform.localPosition=Vector3.up;
            var start=go.AddComponent<PortablePlayerStart>();
            Undo.RegisterCreatedObjectUndo(go,"Add Tanuki Player Start");
            Selection.activeGameObject=go;
            Changed("Player start added. Position its center above the floor and rotate its forward arrow.");
            SceneView.RepaintAll(); return start;
        }

        public void SelectMap(PortableMapRoot root)
        {
            if (root && (EditorUtility.IsPersistent(root) || !root.gameObject.scene.IsValid()))
                throw new InvalidOperationException("Select a map root in an open scene, not a prefab asset.");
            map=root; Changed(root ? "Map selected. Validate before exporting." : "Choose a map root.");
        }

        void OnEnable()
        {
            Selection.selectionChanged+=FollowSelection;
            Undo.undoRedoPerformed+=OnEdited;
            EditorApplication.hierarchyChanged+=OnEdited;
            EditorApplication.projectChanged+=OnEdited;
            FollowSelection();
        }
        void OnDisable()
        {
            Selection.selectionChanged-=FollowSelection;
            Undo.undoRedoPerformed-=OnEdited;
            EditorApplication.hierarchyChanged-=OnEdited;
            EditorApplication.projectChanged-=OnEdited;
        }
        void FollowSelection()
        {
            var root=Selection.activeGameObject ? Selection.activeGameObject.GetComponentInParent<PortableMapRoot>() : null;
            if (root && root!=map && !EditorUtility.IsPersistent(root)) SelectMap(root);
            Repaint();
        }
        void OnEdited() { Repaint(); }
        void Changed(string text) { message=text; hasError=false; Repaint(); }
        void Fail(string text) { message=text; hasError=true; Repaint(); }

        public bool ValidateMap()
        {
            return Run("Validating map",()=>
            {
                var result=PortableMapExporter.Prepare(map);
                Changed($"Validation passed: {result.MeshCount} mesh entries, {result.ColliderCount} primitive colliders, {result.PlayerStartCount} player starts. Export always validates the current scene again.");
            });
        }
        public bool ExportMap(string destination)
        {
            return Run("Exporting map",()=>
            {
                // Always rebuild from current authored state; a previous validation is not export admission.
                PortableMapExporter.Export(map,destination);
                exportFolder=Path.GetFullPath(destination);
                Changed("Exported " + Path.Combine(exportFolder,"map.tanukimap"));
            });
        }
        bool Run(string title,Action action)
        {
            if (busy) return false;
            busy=true;
            try
            {
                EditorUtility.DisplayProgressBar(title,"Converting geometry, materials, collision and markers...",.5f);
                action(); return true;
            }
            catch(Exception error) { Fail(error.Message); return false; }
            finally { busy=false; EditorUtility.ClearProgressBar(); Repaint(); }
        }

        void OnGUI()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Label("Tanuki Map Authoring",EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Author here. Export one .tanukimap file.",EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();
            using(new EditorGUI.DisabledScope(busy || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUI.BeginChangeCheck();
                var chosen=(PortableMapRoot)EditorGUILayout.ObjectField("Map Root",map,typeof(PortableMapRoot),true);
                if(EditorGUI.EndChangeCheck()) { try { SelectMap(chosen); } catch(Exception e) { Fail(e.Message); } }
                var roots=Resources.FindObjectsOfTypeAll<PortableMapRoot>().Where(r=>r && r.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(r)).ToArray();
                if(roots.Length>1 && GUILayout.Button("Choose Map in Open Scenes"))
                {
                    var menu=new GenericMenu();
                    foreach(var root in roots) { var item=root; menu.AddItem(new GUIContent(root.gameObject.scene.name+" / "+root.name),root==map,()=>SelectMap(item)); }
                    menu.ShowAsContext();
                }
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button("Create Map Root")) CreateMapRoot();
                    if(GUILayout.Button("Open Sample Map")) OpenSample();
                }
                if(map)
                {
                    EditorGUILayout.Space();
                    var serialized=new SerializedObject(map); serialized.Update();
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(serialized.FindProperty("mapId"),new GUIContent("Map ID"));
                    if(EditorGUI.EndChangeCheck()) { serialized.ApplyModifiedProperties(); Changed("Map identity changed. Keep the same ID for revisions of this map."); }
                    else serialized.ApplyModifiedProperties();
                    EditorGUILayout.HelpBox("Keep this ID for future revisions. A different map should have a different ID.",MessageType.Info);
                    EditorGUILayout.LabelField("Player Starts",map.GetComponentsInChildren<PortablePlayerStart>().Count(s=>s.enabled).ToString());
                    EditorGUILayout.LabelField("Visible Meshes",map.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled).ToString());
                    using(new EditorGUILayout.HorizontalScope())
                    {
                        if(GUILayout.Button("Add Player Start")) AddPlayerStart();
                        using(new EditorGUI.DisabledScope(!Selection.activeGameObject || !Selection.activeGameObject.transform.IsChildOf(map.transform)))
                        {
                            if(GUILayout.Button("Add Box Collider"))
                            {
                                if(Selection.activeGameObject.GetComponent<Collider>()) Fail("The selected object already has a collider. Edit its collider in the Inspector.");
                                else { Undo.AddComponent<BoxCollider>(Selection.activeGameObject); Changed("Box collider added to the selected map object."); }
                            }
                        }
                    }
                    if(GUILayout.Button("Frame Map in Scene View") && SceneView.lastActiveSceneView)
                    {
                        Selection.activeGameObject=map.gameObject; SceneView.lastActiveSceneView.FrameSelected();
                    }
                }
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Export",EditorStyles.boldLabel);
                using(new EditorGUILayout.HorizontalScope())
                {
                    exportFolder=EditorGUILayout.TextField("Folder",exportFolder);
                    if(GUILayout.Button("Browse",GUILayout.Width(65)))
                    {
                        string folder=EditorUtility.SaveFolderPanel("Export Tanuki map",exportFolder,map ? map.name : "TanukiMap");
                        if(!string.IsNullOrEmpty(folder)) exportFolder=folder;
                    }
                }
                using(new EditorGUI.DisabledScope(!map))
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button("Validate Map")) ValidateMap();
                    if(GUILayout.Button("Export .tanukimap"))
                    {
                        if(string.IsNullOrWhiteSpace(exportFolder))
                        {
                            string folder=EditorUtility.SaveFolderPanel("Export Tanuki map","",map.name);
                            if(!string.IsNullOrEmpty(folder)) ExportMap(folder);
                        }
                        else ExportMap(exportFolder);
                    }
                }
                if(!string.IsNullOrEmpty(exportFolder) && File.Exists(Path.Combine(exportFolder,"map.tanukimap")) && GUILayout.Button("Show Exported File"))
                    EditorUtility.RevealInFinder(Path.Combine(exportFolder,"map.tanukimap"));
            }
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(message,hasError ? MessageType.Error : MessageType.Info);
            EditorGUILayout.HelpBox("Prototype: static meshes, basic textured materials, primitive/static mesh collision and player starts. Gameplay scripting, Steam Workshop publishing and embedded Tanuki gameplay preview are not implemented.",MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        public static void OpenSampleAndShow()
        {
            var window=Open(); window.OpenSample();
            if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.FrameSelected();
        }
        void OpenSample()
        {
            try
            {
                const string path="Assets/TanukiMapSample/Room.unity";
                if(AssetDatabase.LoadAssetAtPath<SceneAsset>(path))
                {
                    if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                    EditorSceneManager.OpenScene(path);
                    var root=Object.FindObjectOfType<PortableMapRoot>(); SelectMap(root); Selection.activeGameObject=root.gameObject;
                }
                else PortableMapSample.Create();
                FollowSelection();
            }
            catch(Exception error) { Fail(error.Message); }
        }
    }

    [CustomEditor(typeof(PortableMapRoot))]
    public sealed class PortableMapRootInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if(GUILayout.Button("Open Tanuki Map Authoring")) PortableMapAuthoringWindow.Open().SelectMap((PortableMapRoot)target);
        }
    }
}
