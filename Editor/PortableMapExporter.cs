using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Radbot.MapAuthoring.Editor
{
    /// <summary>Deliberately small static-scene GLB writer. No dependency on a Unity runtime or AssetBundle.</summary>
    public static class PortableMapExporter
    {
        [MenuItem("Tanuki/Export Selected Map")]
        public static void ExportSelected()
        {
            var root = Selection.activeGameObject ? Selection.activeGameObject.GetComponentInParent<PortableMapRoot>() : null;
            if (!root) throw new InvalidOperationException("Select a Portable Map Root or one of its children.");
            string destination = EditorUtility.SaveFolderPanel("Export Tanuki map", "", root.mapId);
            if (destination.Length > 0) Export(root, destination);
        }

        public static void Export(PortableMapRoot root, string destination)
        {
            Prepare(root).Write(destination);
        }

        /// <summary>The editor's validation and export use the same complete conversion, including materials and collision.</summary>
        public static PreparedMap Prepare(PortableMapRoot root)
        {
            if (!root || string.IsNullOrWhiteSpace(root.mapId) || root.mapId.Length > 128) throw new InvalidOperationException("Set a map ID (1-128 characters).");
            if ((root.transform.lossyScale - Vector3.one).sqrMagnitude > .000001f) throw new InvalidOperationException("Map root must have unit scale.");
            if (root.GetComponentsInChildren<PortableMapRoot>(true).Length != 1) throw new InvalidOperationException("Nested map roots are unsupported.");
            if (root.GetComponentsInChildren<Terrain>().Length != 0 || root.GetComponentsInChildren<SkinnedMeshRenderer>().Length != 0)
                throw new InvalidOperationException("Prototype supports static MeshRenderers. Bake Terrain to meshes; remove skinned geometry.");
            if (root.GetComponentsInChildren<Rigidbody>().Length != 0) throw new InvalidOperationException("Dynamic rigidbodies are unsupported in static map exports.");
            if (root.GetComponentsInChildren<LODGroup>().Length != 0) throw new InvalidOperationException("Select/bake one mesh LOD and remove LODGroup before prototype export.");
            var glb = new Glb(root.transform);
            var meshes = new List<object>(); var colliders = new List<object>(); var markers = new List<object>();
            int serial = 0;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) throw new InvalidOperationException("Missing MeshFilter: " + renderer.name);
                if (renderer.sharedMaterials.Length != filter.sharedMesh.subMeshCount) throw new InvalidOperationException("Each submesh needs exactly one material; layered extra materials are unsupported: " + renderer.name);
                int index = glb.AddMesh(filter.sharedMesh, renderer.transform, renderer.sharedMaterials, false);
                meshes.Add(D("id", "mesh-" + serial++, "mesh", index, "visible", true, "collision", false));
            }
            foreach (var collider in root.GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled) continue;
                if (collider.isTrigger) throw new InvalidOperationException("Trigger colliders need a supported gameplay marker: " + collider.name);
                string id = "collider-" + serial++;
                if (collider is MeshCollider mc)
                {
                    if (mc.convex) throw new InvalidOperationException("Convex MeshColliders are unsupported; use primitive or static triangle collision: " + mc.name);
                    int index = glb.AddMesh(mc.sharedMesh, mc.transform, Array.Empty<Material>(), true);
                    meshes.Add(D("id", id, "mesh", index, "visible", false, "collision", true));
                    continue;
                }
                var t = collider.transform;
                // A primitive cannot represent shear from a rotated child under non-uniform parent scale.
                var m = root.transform.worldToLocalMatrix * t.localToWorldMatrix;
                Vector3 x=m.GetColumn(0), y=m.GetColumn(1), z=m.GetColumn(2);
                if (Mathf.Abs(Vector3.Dot(x.normalized,y.normalized)) > .0001f || Mathf.Abs(Vector3.Dot(x.normalized,z.normalized)) > .0001f || Mathf.Abs(Vector3.Dot(y.normalized,z.normalized)) > .0001f)
                    throw new InvalidOperationException("Sheared primitive collider; use a MeshCollider: " + collider.name);
                var scale = new Vector3(x.magnitude,y.magnitude,z.magnitude);
                if (scale.x <= .00001f || scale.y <= .00001f || scale.z <= .00001f) throw new InvalidOperationException("Degenerate collider scale: " + collider.name);
                Vector3 center; Quaternion rotation = Quaternion.Inverse(root.transform.rotation) * t.rotation;
                Dictionary<string,object> record;
                if (collider is BoxCollider box)
                {
                    center=box.center;
                    record=D("id",id,"type","box","size",Position(Vector3.Scale(box.size,scale)));
                }
                else if (collider is SphereCollider sphere)
                {
                    center=sphere.center;
                    record=D("id",id,"type","sphere","radius",sphere.radius*Mathf.Max(scale.x,Mathf.Max(scale.y,scale.z))*100f);
                }
                else if (collider is CapsuleCollider capsule)
                {
                    center=capsule.center;
                    int axis=capsule.direction;
                    Vector3 direction=axis==0 ? Vector3.right : axis==1 ? Vector3.up : Vector3.forward;
                    rotation *= Quaternion.FromToRotation(Vector3.up,direction);
                    float radius=capsule.radius*Mathf.Max(scale[(axis+1)%3],scale[(axis+2)%3]);
                    record=D("id",id,"type","capsule","radius",radius*100f,"halfHeight",Mathf.Max(radius,capsule.height*scale[axis]*.5f)*100f);
                }
                else throw new InvalidOperationException("Unsupported collider: " + collider.GetType().Name);
                record["position"]=Position(root.transform.InverseTransformPoint(t.TransformPoint(center)));
                record["rotation"]=Rotation(rotation);
                colliders.Add(record);
            }
            foreach (var marker in root.GetComponentsInChildren<PortablePlayerStart>())
            {
                if (!marker.enabled) continue;
                if (marker.spawnTag.Length>128) throw new InvalidOperationException("Spawn tag exceeds 128 characters.");
                markers.Add(D("id","start-"+serial++,"type","playerStart","spawnTag",marker.spawnTag,
                    "position",Position(root.transform.InverseTransformPoint(marker.transform.position)),
                    "rotation",Rotation(Quaternion.Inverse(root.transform.rotation)*marker.transform.rotation)));
            }
            if (meshes.Count==0 || markers.Count==0) throw new InvalidOperationException("A map needs static geometry and at least one Portable Player Start.");
            if (meshes.Count>10000 || colliders.Count>10000 || markers.Count>10000) throw new InvalidOperationException("Prototype limit: 10,000 entries per category.");
            byte[] bytes=glb.Build();
            if (bytes.Length>256*1024*1024) throw new InvalidOperationException("Prototype GLB limit is 256 MiB.");
            string hash; using (var sha=SHA1.Create()) hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            string json=Json(D("schemaVersion",1,"mapId",root.mapId,"sceneSha1",hash,"meshes",meshes,"colliders",colliders,"markers",markers));
            return new PreparedMap(bytes,json,meshes.Count,colliders.Count,markers.Count,hash);
        }

        public sealed class PreparedMap
        {
            readonly byte[] scene;
            readonly string json;
            public readonly int MeshCount, ColliderCount, PlayerStartCount;
            public readonly string SceneHash;
            internal PreparedMap(byte[] scene,string json,int meshes,int colliders,int starts,string hash)
            {
                this.scene=scene; this.json=json; MeshCount=meshes; ColliderCount=colliders; PlayerStartCount=starts; SceneHash=hash;
            }

            public void Write(string destination)
            {
            if (string.IsNullOrWhiteSpace(destination)) throw new ArgumentException("Choose an export folder.",nameof(destination));
            Directory.CreateDirectory(destination);
            // Manifest is the final commit marker. A failed write produces a detectable hash mismatch.
            File.WriteAllBytes(Path.Combine(destination,"scene.glb"),scene);
            File.WriteAllText(Path.Combine(destination,"map.json"),json,new UTF8Encoding(false));
            byte[] manifest=Encoding.UTF8.GetBytes(json);
            string package=Path.Combine(destination,"map.tanukimap");
            string temporary=Path.Combine(destination,".map-"+Guid.NewGuid().ToString("N")+".tmp");
            try
            {
                using(var output=File.Create(temporary))
                using(var writer=new BinaryWriter(output))
                {
                    writer.Write(0x50414d54); writer.Write(1); writer.Write(manifest.Length); writer.Write(scene.Length);
                    writer.Write(manifest); writer.Write(scene);
                }
                if(File.Exists(package)) File.Replace(temporary,package,null);
                else File.Move(temporary,package);
            }
            finally { if(File.Exists(temporary)) File.Delete(temporary); }
            Debug.Log($"TANUKI_MAP_EXPORTED path={destination} meshes={MeshCount} starts={PlayerStartCount} sha1={SceneHash}");
            }
        }

        // UE X=Unity Z, UE Y=Unity X, UE Z=Unity Y. Units in manifest are centimetres.
        static float[] Position(Vector3 v) => new[] {v.z*100f,v.x*100f,v.y*100f};
        static float[] Rotation(Quaternion q) => new[] {q.z,q.x,q.y,q.w};
        internal static Dictionary<string,object> D(params object[] pairs)
        {
            var d=new Dictionary<string,object>(); for (int i=0;i<pairs.Length;i+=2) d.Add((string)pairs[i],pairs[i+1]); return d;
        }
        internal static string Json(object value)
        {
            if (value==null) return "null";
            if (value is string s) return "\""+s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\n","\\n").Replace("\r","\\r").Replace("\t","\\t")+"\"";
            if (value is bool b) return b ? "true" : "false";
            if (value is IDictionary dictionary)
            {
                var parts=new List<string>(); foreach(DictionaryEntry e in dictionary) parts.Add(Json(e.Key)+":"+Json(e.Value)); return "{"+string.Join(",",parts)+"}";
            }
            if (value is IEnumerable enumerable) { var parts=new List<string>(); foreach(var item in enumerable) parts.Add(Json(item)); return "["+string.Join(",",parts)+"]"; }
            double n=Convert.ToDouble(value,CultureInfo.InvariantCulture);
            if (double.IsNaN(n)||double.IsInfinity(n)) throw new InvalidOperationException("Non-finite map value.");
            if (value is float f) return f.ToString("R",CultureInfo.InvariantCulture);
            if (value is double d) return d.ToString("R",CultureInfo.InvariantCulture);
            return Convert.ToString(value,CultureInfo.InvariantCulture);
        }

        sealed class Glb
        {
            readonly Transform root;
            readonly MemoryStream binary=new MemoryStream();
            readonly List<object> views=new List<object>(), accessors=new List<object>(), meshes=new List<object>(), nodes=new List<object>(), materials=new List<object>(), images=new List<object>(), textures=new List<object>();
            readonly Dictionary<Material,int> materialIds=new Dictionary<Material,int>();
            readonly Dictionary<Texture,int> textureIds=new Dictionary<Texture,int>();
            public Glb(Transform root) { this.root=root; }
            int View(byte[] data)
            {
                while(binary.Length%4!=0) binary.WriteByte(0);
                int offset=(int)binary.Length; binary.Write(data,0,data.Length);
                views.Add(D("buffer",0,"byteOffset",offset,"byteLength",data.Length)); return views.Count-1;
            }
            int Accessor(byte[] data,int componentType,int count,string type,float[] min=null,float[] max=null)
            {
                var a=D("bufferView",View(data),"componentType",componentType,"count",count,"type",type);
                if(min!=null) { a["min"]=min; a["max"]=max; }
                accessors.Add(a); return accessors.Count-1;
            }
            static byte[] Floats(IEnumerable<float> values) { using(var m=new MemoryStream()) { using(var w=new BinaryWriter(m)) foreach(float v in values) w.Write(v); return m.ToArray(); } }
            static Vector3 Gltf(Vector3 v) => new Vector3(-v.x,v.y,v.z);
            public int AddMesh(Mesh mesh,Transform transform,Material[] assigned,bool collision)
            {
                if(!mesh || !mesh.isReadable) throw new InvalidOperationException("Mesh must be readable for export: "+(mesh ? mesh.name : "null"));
                var frame=root.worldToLocalMatrix*transform.localToWorldMatrix;
                if(Mathf.Abs(frame.determinant)<.000001f) throw new InvalidOperationException("Degenerate mesh transform: "+transform.name);
                var vertices=mesh.vertices.Select(v=>Gltf(frame.MultiplyPoint3x4(v))).ToArray();
                if(vertices.Length==0) throw new InvalidOperationException("Empty mesh: "+mesh.name);
                Vector3 min=vertices[0],max=vertices[0]; foreach(var v in vertices) { min=Vector3.Min(min,v); max=Vector3.Max(max,v); }
                int positions=Accessor(Floats(vertices.SelectMany(v=>new[]{v.x,v.y,v.z})),5126,vertices.Length,"VEC3",new[]{min.x,min.y,min.z},new[]{max.x,max.y,max.z});
                var attributes=D("POSITION",positions);
                var normals=mesh.normals;
                if(!collision && normals.Length==vertices.Length)
                {
                    var normalMatrix=frame.inverse.transpose;
                    attributes["NORMAL"]=Accessor(Floats(normals.Select(v=>Gltf(normalMatrix.MultiplyVector(v)).normalized).SelectMany(v=>new[]{v.x,v.y,v.z})),5126,normals.Length,"VEC3");
                }
                var uv=mesh.uv;
                if(!collision && uv.Length==vertices.Length) attributes["TEXCOORD_0"]=Accessor(Floats(uv.SelectMany(v=>new[]{v.x,1-v.y})),5126,uv.Length,"VEC2");
                var primitives=new List<object>();
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    if(mesh.GetTopology(sub)!=MeshTopology.Triangles) throw new InvalidOperationException("Only triangle meshes are supported.");
                    var indices=mesh.GetTriangles(sub);
                    if(indices.Length==0) continue;
                    // Reverse winding across the handedness conversion; an authored mirror cancels that reversal.
                    if(frame.determinant>0) for(int i=0;i<indices.Length;i+=3) { int t=indices[i]; indices[i]=indices[i+2]; indices[i+2]=t; }
                    byte[] data; using(var m=new MemoryStream()) { using(var w=new BinaryWriter(m)) foreach(int i in indices) w.Write((uint)i); data=m.ToArray(); }
                    var p=D("attributes",attributes,"indices",Accessor(data,5125,indices.Length,"SCALAR"),"mode",4);
                    if(!collision)
                    {
                        if(sub>=assigned.Length || !assigned[sub]) throw new InvalidOperationException("Missing material: "+transform.name);
                        if(assigned[sub].mainTexture && uv.Length!=vertices.Length) throw new InvalidOperationException("Textured mesh has no UVs: "+transform.name);
                        p["material"]=Material(assigned[sub]);
                    }
                    primitives.Add(p);
                }
                int index=meshes.Count;
                meshes.Add(D("name",transform.name,"primitives",primitives)); nodes.Add(D("mesh",index)); return index;
            }
            int Material(Material material)
            {
                if(materialIds.TryGetValue(material,out int existing)) return existing;
                string shader=material.shader.name;
                if(shader!="Standard" && shader!="Universal Render Pipeline/Lit") throw new InvalidOperationException("Unsupported shader: "+shader+". Use Standard or URP/Lit.");
                if(material.IsKeywordEnabled("_NORMALMAP") || material.IsKeywordEnabled("_METALLICGLOSSMAP") || material.IsKeywordEnabled("_PARALLAXMAP") || material.IsKeywordEnabled("_DETAIL_MULX2"))
                    throw new InvalidOperationException("Prototype supports base-color textures and scalar metallic/smoothness; bake or remove extra maps: "+material.name);
                string colorKey=material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                Color color=material.GetColor(colorKey).linear;
                var pbr=D("baseColorFactor",new[]{color.r,color.g,color.b,color.a},"metallicFactor",material.GetFloat("_Metallic"),
                    "roughnessFactor",1f-material.GetFloat(material.HasProperty("_Smoothness") ? "_Smoothness" : "_Glossiness"));
                Texture texture=material.mainTexture;
                if(texture)
                {
                    if(material.mainTextureOffset!=Vector2.zero || material.mainTextureScale!=Vector2.one) throw new InvalidOperationException("Bake texture tiling/offset into UVs: "+material.name);
                    pbr["baseColorTexture"]=D("index",Texture(texture));
                }
                var result=D("name",material.name,"pbrMetallicRoughness",pbr,"doubleSided",false);
                bool cutout=material.IsKeywordEnabled("_ALPHATEST_ON");
                bool transparent=shader=="Standard" ? material.GetFloat("_Mode")>=2 : material.GetFloat("_Surface")>0;
                if(transparent) throw new InvalidOperationException("Translucent materials are unsupported in this prototype: "+material.name);
                if(cutout) { result["alphaMode"]="MASK"; result["alphaCutoff"]=material.GetFloat("_Cutoff"); }
                if(material.IsKeywordEnabled("_EMISSION")) throw new InvalidOperationException("Emissive materials are unsupported in this prototype: "+material.name);
                int index=materials.Count; materials.Add(result); materialIds.Add(material,index); return index;
            }
            int Texture(Texture source)
            {
                if(textureIds.TryGetValue(source,out int existing)) return existing;
                if(source.dimension!=TextureDimension.Tex2D || source.width>8192 || source.height>8192) throw new InvalidOperationException("Only 2D textures up to 8192 pixels are supported.");
                var rt=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
                RenderTexture previous=RenderTexture.active; Texture2D copy=null;
                try
                {
                    Graphics.Blit(source,rt); RenderTexture.active=rt;
                    copy=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false,false);
                    copy.ReadPixels(new Rect(0,0,source.width,source.height),0,0); copy.Apply();
                    images.Add(D("bufferView",View(copy.EncodeToPNG()),"mimeType","image/png"));
                }
                finally { RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt); if(copy) Object.DestroyImmediate(copy); }
                int index=textures.Count; textures.Add(D("source",images.Count-1)); textureIds.Add(source,index); return index;
            }
            public byte[] Build()
            {
                var doc=D("asset",D("version","2.0","generator","Tanuki Unity Map Authoring 0.1"),"scene",0,
                    "scenes",new[]{D("nodes",Enumerable.Range(0,nodes.Count).ToArray())},"nodes",nodes,"meshes",meshes,
                    "accessors",accessors,"bufferViews",views,"buffers",new[]{D("byteLength",binary.Length)});
                if(materials.Count>0) doc["materials"]=materials;
                if(images.Count>0) { doc["images"]=images; doc["textures"]=textures; }
                byte[] json=Encoding.UTF8.GetBytes(Json(doc)); int jsonLength=(json.Length+3)&~3;
                while(binary.Length%4!=0) binary.WriteByte(0);
                using(var output=new MemoryStream())
                {
                    using(var w=new BinaryWriter(output))
                    {
                        w.Write(0x46546c67); w.Write(2); w.Write(12+8+jsonLength+8+(int)binary.Length);
                        w.Write(jsonLength); w.Write(0x4e4f534a); w.Write(json); for(int i=json.Length;i<jsonLength;i++) w.Write((byte)32);
                        w.Write((int)binary.Length); w.Write(0x004e4942); w.Write(binary.ToArray());
                    }
                    return output.ToArray();
                }
            }
        }
    }
}
