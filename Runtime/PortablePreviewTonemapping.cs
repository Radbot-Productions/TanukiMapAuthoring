using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Radbot.MapAuthoring
{
    [ExecuteAlways,ImageEffectAllowedInSceneView,RequireComponent(typeof(Camera)),AddComponentMenu("")]
    public sealed class PortablePreviewTonemapping : MonoBehaviour
    {
        static readonly List<PortableMapBaseLighting> environments=new();
        static readonly List<PortablePreviewTonemapping> effects=new();
        PortableMapBaseLighting environment;
        Material material;
        Camera view;
        bool previousHDR;

        public static void Register(PortableMapBaseLighting lighting)
        {
            if(!environments.Contains(lighting))environments.Add(lighting);
            Camera.onPreCull-=Prepare;Camera.onPreCull+=Prepare;
        }
        public static void Unregister(PortableMapBaseLighting lighting)
        {
            environments.Remove(lighting);
            for(int i=effects.Count-1;i>=0;i--)if(effects[i]&&effects[i].environment==lighting)Release(effects[i]);
            if(environments.Count==0)Camera.onPreCull-=Prepare;
        }
        static void Release(PortablePreviewTonemapping effect)
        {
            effects.Remove(effect);
            if(effect.view)effect.view.allowHDR=effect.previousHDR;
            if(Application.isPlaying)Destroy(effect);else DestroyImmediate(effect);
        }
        static void Prepare(Camera camera)
        {
            if(GraphicsSettings.currentRenderPipeline||camera.cameraType==CameraType.Reflection||camera.cameraType==CameraType.Preview)return;
            PortableMapBaseLighting owner=null;
            foreach(var lighting in environments)if(lighting&&lighting.isActiveAndEnabled&&
                (camera.cameraType==CameraType.SceneView||lighting.gameObject.scene==camera.gameObject.scene)){owner=lighting;break;}
            var effect=camera.GetComponent<PortablePreviewTonemapping>();
            if(!owner){if(effect)Release(effect);return;}
            if(!effect)
            {
                effect=camera.gameObject.AddComponent<PortablePreviewTonemapping>();effect.hideFlags=HideFlags.HideAndDontSave;
                effect.view=camera;effect.previousHDR=camera.allowHDR;effects.Add(effect);
            }
            effect.environment=owner;camera.allowHDR=true;
        }
        void OnRenderImage(RenderTexture source,RenderTexture destination)
        {
            if(!environment||!environment.isActiveAndEnabled||GraphicsSettings.currentRenderPipeline){Graphics.Blit(source,destination);return;}
            if(!material)
            {
                var shader=Resources.Load<Shader>("Tanuki/PreviewTonemapping");
                if(!shader||!shader.isSupported){Debug.LogError("Tanuki preview tone-mapping shader is unavailable.",this);Graphics.Blit(source,destination);return;}
                material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
            }
            material.SetFloat("_Exposure",environment.CameraExposure);
            Graphics.Blit(source,destination,material);
        }
        void OnDisable()
        {
            if(view)view.allowHDR=previousHDR;
            if(material){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
            effects.Remove(this);
        }
    }
}
