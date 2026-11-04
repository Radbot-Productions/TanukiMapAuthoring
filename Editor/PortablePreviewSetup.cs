using UnityEditor;
using UnityEngine;

namespace Radbot.MapAuthoring.Editor
{
    public static class PortablePreviewSetup
    {
        [MenuItem("Tanuki/Components/Player Character",priority=22)]
        static void AddMenu()
        {
            var selected=Selection.activeGameObject?Selection.activeGameObject.GetComponentInParent<PortableMapRoot>():null;
            if(!selected) foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) {selected=root.GetComponent<PortableMapRoot>();if(selected)break;}
            if(selected) Selection.activeGameObject=Ensure(selected).gameObject;
        }
        static void EnsureReferences(PortableMapRoot map)
        {
            if(map.transform.Find("Character Scale References"))return;
            var group=new GameObject("Character Scale References");group.transform.SetParent(map.transform,false);group.AddComponent<PortableMapPreviewReference>();
            string[] names={"Tanuki","Tengu"};
            for(int i=0;i<names.Length;i++)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.radbot.tanuki-map-authoring/Runtime/References/"+names[i]+".fbx");
                if(!prefab)throw new System.InvalidOperationException("Missing character scale reference: "+names[i]);
                var copy=(GameObject)PrefabUtility.InstantiatePrefab(prefab,group.transform);copy.name=names[i]+" (scale reference)";copy.transform.localPosition=new Vector3(-3+i*3,0,-5);
                foreach(var animator in copy.GetComponentsInChildren<Animator>())animator.enabled=false;
            }
        }
        public static PortablePreviewPlayer Ensure(PortableMapRoot map)
        {
            var lighting=map.GetComponent<PortableMapBaseLighting>()??map.gameObject.AddComponent<PortableMapBaseLighting>();lighting.Refresh();
            if(!map.GetComponent<PortableMapSkybox>())map.gameObject.AddComponent<PortableMapSkybox>();
            EnsureReferences(map);
            foreach(var root in map.gameObject.scene.GetRootGameObjects())
                foreach(var player in root.GetComponentsInChildren<PortablePreviewPlayer>(true)) if(player.map==map) return player;
            var go=new GameObject("Player Character");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,map.gameObject.scene);
            var result=go.AddComponent<PortablePreviewPlayer>();result.map=map;
            var start=map.GetComponentInChildren<PortablePlayerStart>();go.transform.SetPositionAndRotation(start?start.transform.position:map.transform.position+Vector3.up,start?start.transform.rotation:map.transform.rotation);
            Undo.RegisterCreatedObjectUndo(go,"Create Player Character");return result;
        }
    }
}
