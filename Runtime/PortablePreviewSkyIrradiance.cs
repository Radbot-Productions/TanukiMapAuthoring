using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Radbot.MapAuthoring
{
    // Integrate the actual linear panorama, not one flat average. Cache the
    // unscaled radiance coefficients; profile intensity/color remain independent.
    public static class PortablePreviewSkyIrradiance
    {
        static readonly Dictionary<Texture2D,SphericalHarmonicsL2> cache=new();
        public static SphericalHarmonicsL2 Get(Texture2D panorama,float[] tint,float intensity)
        {
            if(!panorama)throw new InvalidOperationException("Missing Tanuki sky panorama.");
            if(!cache.TryGetValue(panorama,out var probe))
            {
                const int width=128,height=64;
                var target=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
                var previous=RenderTexture.active;Texture2D pixels=null;
                bool write=GL.sRGBWrite;
                try
                {
                    GL.sRGBWrite=false;Graphics.Blit(panorama,target);RenderTexture.active=target;
                    pixels=new Texture2D(width,height,TextureFormat.RGBAFloat,false,true);
                    pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
                    var colors=pixels.GetPixels();
                    var kernel=new SphericalHarmonicsL2();kernel.AddDirectionalLight(Vector3.up,Color.white,1);
                    // AddDirectionalLight normalizes its truncated cosine lobe at
                    // the peak. Normalize the sphere integral so a unit sky stays 1.
                    float kernelIntegral=4*Mathf.PI*kernel[0,0];
                    for(int y=0;y<height;y++)
                    {
                        float latitude=((y+.5f)/height-.5f)*Mathf.PI;
                        float solidAngle=2*Mathf.PI/width*(Mathf.Sin(((y+1f)/height-.5f)*Mathf.PI)-Mathf.Sin(((float)y/height-.5f)*Mathf.PI));
                        for(int x=0;x<width;x++)
                        {
                            float angle=((x+.5f)/width-.5f)*2*Mathf.PI;
                            // Unreal panorama direction (X,Y,Z) -> Unity (Y,Z,X).
                            float dx=Mathf.Cos(latitude)*Mathf.Sin(angle),dy=Mathf.Sin(latitude),dz=Mathf.Cos(latitude)*Mathf.Cos(angle);
                            // Unity stores its own pre-scaled polynomial basis. Let its
                            // convolution API construct that representation rather than
                            // assigning orthonormal SH coefficients into packed slots.
                            probe.AddDirectionalLight(new Vector3(dx,dy,dz),colors[y*width+x],solidAngle/kernelIntegral);
                        }
                    }
                    cache.Add(panorama,probe);
                }
                finally
                {
                    GL.sRGBWrite=write;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);
                    if(pixels){if(Application.isPlaying)UnityEngine.Object.Destroy(pixels);else UnityEngine.Object.DestroyImmediate(pixels);}
                }
            }
            for(int channel=0;channel<3;channel++)for(int coefficient=0;coefficient<9;coefficient++)probe[channel,coefficient]*=intensity*(tint==null?1:tint[channel]);
            return probe;
        }
    }
}
