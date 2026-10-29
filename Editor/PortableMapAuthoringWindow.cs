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
        [SerializeField] string exportFile="";
        [SerializeField] string message="Choose a map root, or create a new map.";
        [SerializeField] bool hasError;
        [SerializeField] string workshopTitle="",previewImage="",workshopItem="";
        [SerializeField] string uploadMapId="";
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
            workshopTitle=root ? root.name : "";
            workshopItem=root ? PortableMapWorkshopPublisher.GetRememberedItem(root.mapId) : "";
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
                PortableMapExporter.ExportFile(map,destination);
                exportFile=Path.GetFullPath(destination);
                Changed("Exported " + exportFile);
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
                    if(EditorGUI.EndChangeCheck()) { serialized.ApplyModifiedProperties(); workshopItem=PortableMapWorkshopPublisher.GetRememberedItem(map.mapId); Changed("Map identity changed. Keep the same ID for revisions of this map."); }
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
                    exportFile=EditorGUILayout.TextField("Map File",exportFile);
                    if(GUILayout.Button("Browse",GUILayout.Width(65)))
                    {
                        string filename=ChooseExportFile();
                        if(!string.IsNullOrEmpty(filename)) exportFile=filename;
                    }
                }
                using(new EditorGUI.DisabledScope(!map))
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button("Validate Map")) ValidateMap();
                    if(GUILayout.Button("Export .tanukimap"))
                    {
                        if(string.IsNullOrWhiteSpace(exportFile))
                        {
                            string filename=ChooseExportFile();
                            if(!string.IsNullOrEmpty(filename)) ExportMap(filename);
                        }
                        else ExportMap(exportFile);
                    }
                }
                if(!string.IsNullOrEmpty(exportFile) && File.Exists(exportFile) && GUILayout.Button("Show Exported File"))
                    EditorUtility.RevealInFinder(exportFile);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Steam Workshop",EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Developer-only testing. Run Steam and use the Tanuki game with Workshop upload support. Export & Upload includes the current scene changes.",MessageType.Info);
                using(new EditorGUI.DisabledScope(PortableMapWorkshopPublisher.IsPending))
                {
                    workshopTitle=EditorGUILayout.TextField("Title",workshopTitle);
                    EditorGUILayout.LabelField("Leave the preview blank to generate a framed map view.",EditorStyles.wordWrappedMiniLabel);
                    using(new EditorGUILayout.HorizontalScope())
                    {
                        previewImage=EditorGUILayout.TextField("Preview PNG",previewImage);
                        if(GUILayout.Button("Browse",GUILayout.Width(65)))
                        { var image=EditorUtility.OpenFilePanel("Workshop preview PNG",Path.GetDirectoryName(previewImage)??"","png"); if(image.Length>0) previewImage=image; }
                    }
                    workshopItem=EditorGUILayout.TextField(new GUIContent("Item ID","Leave empty for a new item. Keep the returned ID to update that item."),workshopItem);
                    string remembered=map ? PortableMapWorkshopPublisher.GetRememberedItem(map.mapId) : "";
                    if(remembered.Length>0 && workshopItem!=remembered && GUILayout.Button("Use Last Uploaded Item ("+remembered+")")) workshopItem=remembered;
                    using(new EditorGUILayout.HorizontalScope())
                    {
                        string executable=EditorGUILayout.TextField("Tanuki Executable",PortableMapWorkshopPublisher.GameExecutable);
                        if(executable!=PortableMapWorkshopPublisher.GameExecutable) PortableMapWorkshopPublisher.GameExecutable=executable;
                        if(GUILayout.Button("Browse",GUILayout.Width(65)))
                        { var executableFile=EditorUtility.OpenFilePanel("Tanuki game executable","","exe"); if(executableFile.Length>0) PortableMapWorkshopPublisher.GameExecutable=executableFile; }
                    }
                    string project=EditorGUILayout.TextField(new GUIContent("Development Project","Optional .uproject when using UnrealEditor.exe instead of a packaged Tanuki game."),PortableMapWorkshopPublisher.UnrealProject);
                    if(project!=PortableMapWorkshopPublisher.UnrealProject) PortableMapWorkshopPublisher.UnrealProject=project;
                    using(new EditorGUI.DisabledScope(!map))
                        if(GUILayout.Button(string.IsNullOrEmpty(workshopItem)?"Export & Upload to Workshop":"Export & Update Workshop Item"))
                        {
                            if(PortableMapWorkshopPublisher.ExportAndUpload(map,workshopTitle,previewImage,workshopItem)) uploadMapId=map.mapId;
                        }
                }
                if(!string.IsNullOrEmpty(PortableMapWorkshopPublisher.Status))
                    EditorGUILayout.HelpBox(PortableMapWorkshopPublisher.Status,PortableMapWorkshopPublisher.HasError ? MessageType.Error : MessageType.Info);
            }
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(message,hasError ? MessageType.Error : MessageType.Info);
            EditorGUILayout.HelpBox("Prototype: static meshes, basic textured materials, primitive/static mesh collision and player starts. Workshop uploads use Tanuki's Steam backend. Gameplay scripting and embedded Tanuki gameplay preview are not implemented.",MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        void OnInspectorUpdate()
        {
            if(!PortableMapWorkshopPublisher.IsPending && !string.IsNullOrEmpty(uploadMapId))
            {
                if(map && map.mapId==uploadMapId)
                {
                    string item=PortableMapWorkshopPublisher.GetRememberedItem(uploadMapId);
                    if(!string.IsNullOrEmpty(item)) workshopItem=item;
                }
                uploadMapId=""; Repaint();
            }
            if(PortableMapWorkshopPublisher.IsPending) Repaint();
        }

        public static void OpenSampleAndShow()
        {
            var window=Open(); window.OpenSample();
            if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.FrameSelected();
        }
        string ChooseExportFile()
        {
            return EditorUtility.SaveFilePanel("Export Tanuki map",string.IsNullOrEmpty(exportFile) ? "" : Path.GetDirectoryName(exportFile),
                string.IsNullOrEmpty(exportFile) ? (map ? map.mapId : "TanukiMap") : Path.GetFileNameWithoutExtension(exportFile),"tanukimap");
        }
        void OpenSample()
        {
            try
            {
                var root=PortableCourtyardSample.OpenOrCreate();
                if(!root) return;
                SelectMap(root);
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
