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
        public static PortablePreviewPlayer Ensure(PortableMapRoot map)
        {
            bool light=false;foreach(var sceneRoot in map.gameObject.scene.GetRootGameObjects()) if(sceneRoot.GetComponentInChildren<Light>()) light=true;
            if(!light){var sun=new GameObject("Preview Light").AddComponent<Light>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(sun.gameObject,map.gameObject.scene);sun.type=LightType.Directional;sun.intensity=1;sun.transform.rotation=Quaternion.Euler(50,-30,0);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.65f);}
            foreach(var root in map.gameObject.scene.GetRootGameObjects())
                foreach(var player in root.GetComponentsInChildren<PortablePreviewPlayer>(true)) if(player.map==map) return player;
            var go=new GameObject("Player Character");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,map.gameObject.scene);
            var result=go.AddComponent<PortablePreviewPlayer>();result.map=map;
            var start=map.GetComponentInChildren<PortablePlayerStart>();go.transform.SetPositionAndRotation(start?start.transform.position:map.transform.position+Vector3.up,start?start.transform.rotation:map.transform.rotation);
            Undo.RegisterCreatedObjectUndo(go,"Create Player Character");return result;
        }
    }
}
