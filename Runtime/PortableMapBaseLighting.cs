using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace Radbot.MapAuthoring
{
    [ExecuteAlways,DisallowMultipleComponent,AddComponentMenu("Tanuki/Lighting Settings")]
    public sealed class PortableMapBaseLighting : MonoBehaviour
    {
        public enum LightingProfile { CartoonStreetDay,CartoonStreetNight }
        [Tooltip("Replaces base illumination only. Individually authored map lights are retained.")]
        public LightingProfile profile;
        public bool overrideSun;
        [Tooltip("Local sun rotation in degrees.")] public Vector3 sunRotation;
        [Min(0),Tooltip("Directional sunlight in lux.")] public float sunIntensity=6;
        [ColorUsage(false,false)] public Color sunColor=Color.white;
        public bool sunShadows=true;
        [SerializeField,HideInInspector] LightingProfile appliedProfile;
        Material previewSky,previousSky;
        AmbientMode previousAmbientMode;
        SphericalHarmonicsL2 previousAmbientProbe;
        float previousReflectionIntensity;
        public float CameraExposure
        {
            get{var p=Settings();float ev=Mathf.Approximately(p.minEV100,p.maxEV100)?p.minEV100:p.referenceEV100;return Mathf.Pow(2,p.exposureBias-ev+(GraphicsSettings.currentRenderPipeline?0:p.previewExposureBias))/1.2f;}
        }
        // Unity's built-in Standard BRDF absorbs Lambert's 1/pi into light units.
        // Exposure belongs to the HDR camera pass, including emissive materials and sky.
        public float PreviewLightFactor=>GraphicsSettings.currentRenderPipeline?ExposureFactor:1f/Mathf.PI;
        public string Key=>profile==LightingProfile.CartoonStreetNight?"cartoon-street-night":"cartoon-street-day";
        [Serializable] public sealed class Sun {public float intensity;public float[] color,quaternion;}
        [Serializable] public sealed class Profile {public string key,previewSkyResource;public Sun[] suns;public float skyIntensity,exposureBias,minEV100,maxEV100,referenceEV100,previewExposureBias;public float[] skyColor,skyZenith,skyHorizon,skyOverall,skyLuminanceGain;}
        [Serializable] sealed class Collection {public Profile[] profiles;}
        static Collection profiles;
        Profile cachedProfile;
        public Profile Settings()
        {
            if(cachedProfile!=null&&cachedProfile.key==Key)return cachedProfile;
            if(profiles==null)
            {
                var data=Resources.Load<TextAsset>("Tanuki/lighting-profiles");
                if(!data)throw new InvalidOperationException("The plugin lighting-profile data is missing.");
                profiles=JsonUtility.FromJson<Collection>(data.text);
            }
            cachedProfile=Array.Find(profiles.profiles,p=>p.key==Key)??throw new InvalidOperationException("Unknown base lighting profile.");
            return cachedProfile;
        }
        public float ExposureFactor
        {
            get{var p=Settings();float ev=Mathf.Approximately(p.minEV100,p.maxEV100)?p.minEV100:p.referenceEV100;return Mathf.Pow(2,p.exposureBias-ev)/(1.2f*Mathf.PI);}
        }
        public void SetKey(string key)
        {
            if(key!="cartoon-street-day"&&key!="cartoon-street-night")throw new InvalidOperationException("Unsupported base lighting profile.");
            profile=key=="cartoon-street-night"?LightingProfile.CartoonStreetNight:LightingProfile.CartoonStreetDay;overrideSun=false;Refresh();
        }
        void OnEnable()
        {
            previousSky=RenderSettings.skybox;previousAmbientMode=RenderSettings.ambientMode;
            previousAmbientProbe=RenderSettings.ambientProbe;previousReflectionIntensity=RenderSettings.reflectionIntensity;
            PortablePreviewTonemapping.Register(this);Schedule();
        }
        void OnDisable()
        {
            PortablePreviewTonemapping.Unregister(this);
            if(previewSky&&RenderSettings.skybox==previewSky)
            {
                RenderSettings.skybox=previousSky;RenderSettings.ambientMode=previousAmbientMode;
                RenderSettings.ambientProbe=previousAmbientProbe;RenderSettings.reflectionIntensity=previousReflectionIntensity;
            }
            if(previewSky){if(Application.isPlaying)Destroy(previewSky);else DestroyImmediate(previewSky);previewSky=null;}
            var helper=transform.Find("_TanukiBaseLighting");if(helper)helper.gameObject.SetActive(false);
        }
        void OnValidate(){if(appliedProfile!=profile)overrideSun=false;Schedule();}
        void Schedule()
        {
#if UNITY_EDITOR
            if(!Application.isPlaying){UnityEditor.EditorApplication.delayCall+=()=>{if(this&&isActiveAndEnabled&&!UnityEditor.EditorUtility.IsPersistent(this))Refresh();};return;}
#endif
            Refresh();
        }
        public void Refresh()
        {
            var p=Settings();var helper=transform.Find("_TanukiBaseLighting");
            if(!overrideSun){var sun=p.suns[0];var q=sun.quaternion;sunRotation=new Quaternion(q[1],q[2],q[0],q[3]).eulerAngles;sunIntensity=sun.intensity;sunColor=new Color(sun.color[0],sun.color[1],sun.color[2]).gamma;sunShadows=true;}
            appliedProfile=profile;
            if(!helper){var go=new GameObject("_TanukiBaseLighting");go.transform.SetParent(transform,false);go.AddComponent<PortableMapPreviewReference>();helper=go.transform;}
            helper.gameObject.SetActive(true);
            for(int i=0;i<Mathf.Max(helper.childCount,p.suns.Length);i++)
            {
                Light light;
                if(i<helper.childCount)light=helper.GetChild(i).GetComponent<Light>();
                else{var go=new GameObject("Base Sun");go.transform.SetParent(helper,false);light=go.AddComponent<Light>();}
                light.enabled=i<p.suns.Length;if(!light.enabled)continue;
                var sun=p.suns[i];light.type=LightType.Directional;light.shadows=LightShadows.Soft;
                light.color=new Color(sun.color[0],sun.color[1],sun.color[2]).gamma;
                var q=sun.quaternion;light.transform.localRotation=new Quaternion(q[1],q[2],q[0],q[3]);
                light.intensity=sun.intensity*PreviewLightFactor;
                if(overrideSun){light.transform.localRotation=Quaternion.Euler(sunRotation);light.color=sunColor;light.intensity=sunIntensity*PreviewLightFactor;light.shadows=sunShadows?LightShadows.Soft:LightShadows.None;}
            }
            var panorama=Resources.Load<Texture2D>("Tanuki/"+(string.IsNullOrEmpty(p.previewSkyResource)?Key:p.previewSkyResource));
            RenderSettings.ambientMode=AmbientMode.Custom;
            RenderSettings.ambientProbe=PortablePreviewSkyIrradiance.Get(panorama,p.skyColor,p.skyIntensity*(GraphicsSettings.currentRenderPipeline?CameraExposure:1));
            RenderSettings.reflectionIntensity=p.skyIntensity;
            var shader=Shader.Find("Hidden/Tanuki/AuthoringSky");
            if(shader)
            {
                if(!previewSky)previewSky=new Material(shader){name="Tanuki Base Sky",hideFlags=HideFlags.HideAndDontSave};
                var sky=previewSky;RenderSettings.skybox=sky;
                bool changed=sky.GetTexture("_Panorama")!=panorama;
                sky.SetTexture("_Panorama",panorama);sky.SetFloat("_Exposure",GraphicsSettings.currentRenderPipeline?CameraExposure:1);
                if(changed)DynamicGI.UpdateEnvironment();
            }
            foreach(var light in GetComponentsInChildren<PortableMapLight>(true))light.Refresh();
        }
    }
}
