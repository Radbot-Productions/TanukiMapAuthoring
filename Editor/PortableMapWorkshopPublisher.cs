using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Radbot.MapAuthoring.Editor
{
    /// <summary>The Tanuki game owns Steam. Editors submit immutable, typed file jobs.</summary>
    [InitializeOnLoad]
    public static class PortableMapWorkshopPublisher
    {
        [Serializable] public sealed class Request
        {
            public int version=1;
            public string jobId,action="upload",file,title,preview,itemId;
        }
        [Serializable] public sealed class Result
        {
            public int version;
            public string jobId,message,itemId,hash,file;
            public bool pending,success;
        }
        [Serializable] sealed class ActiveJob
        {
            public string directory,id,mapId;
            public int processId;
            public long started;
        }
        static ActiveJob active;
        public static bool IsPending => active!=null;
        public static string Status { get; private set; }="";
        public static bool HasError { get; private set; }
        public static string LastItemId { get; private set; }="";
        static string ProjectKey => "Tanuki.Workshop."+Key(Application.dataPath);
        public static string GameExecutable { get => EditorPrefs.GetString(ProjectKey+".Executable",""); set => EditorPrefs.SetString(ProjectKey+".Executable",value); }
        public static string UnrealProject { get => EditorPrefs.GetString(ProjectKey+".Project",""); set => EditorPrefs.SetString(ProjectKey+".Project",value); }

        static PortableMapWorkshopPublisher()
        {
            string saved=EditorPrefs.GetString(ProjectKey+".Active","");
            if(!string.IsNullOrEmpty(saved))
            {
                try { active=JsonUtility.FromJson<ActiveJob>(saved); Status="Reconnecting to the Tanuki Workshop uploader..."; }
                catch { EditorPrefs.DeleteKey(ProjectKey+".Active"); }
            }
            EditorApplication.update+=Poll;
        }
        static string Key(string value)
        {
            using(var hash=SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");
        }
        public static string GetRememberedItem(string mapId) => EditorPrefs.GetString(ProjectKey+".Item."+Key(mapId??""),"");

        public static void ValidateConfiguration(string executable,string project,string title,string preview,string itemId)
        {
            if(Application.platform!=RuntimePlatform.WindowsEditor) throw new InvalidOperationException("The prototype Workshop uploader currently supports Windows.");
            if(!File.Exists(executable) || !string.Equals(Path.GetExtension(executable),".exe",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Choose the Tanuki game executable with Workshop upload support.");
            if(!string.IsNullOrWhiteSpace(project) && (!File.Exists(project) || !string.Equals(Path.GetExtension(project),".uproject",StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Choose a valid Unreal project, or leave it empty for a packaged Tanuki game.");
            if(string.IsNullOrWhiteSpace(title) || title.Length>128) throw new InvalidOperationException("Enter a Workshop title of 1-128 characters.");
            if(!string.IsNullOrEmpty(itemId) && (!ulong.TryParse(itemId,NumberStyles.None,CultureInfo.InvariantCulture,out ulong id) || id==0))
                throw new InvalidOperationException("Workshop item ID must be a positive number, or empty to create an item.");
            if(string.IsNullOrWhiteSpace(preview)) return; // Blank means generate the framed map thumbnail.
            if(!File.Exists(preview) || !string.Equals(Path.GetExtension(preview),".png",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Choose a preview PNG for this map.");
            // Steam's preview limit is below 1 MiB. Check the signature as well as the extension.
            var bytes=File.ReadAllBytes(preview);
            if(bytes.Length>=1024*1024 || bytes.Length<8 || bytes[0]!=137 || bytes[1]!=80 || bytes[2]!=78 || bytes[3]!=71 || bytes[4]!=13 || bytes[5]!=10 || bytes[6]!=26 || bytes[7]!=10)
                throw new InvalidOperationException("The preview must be a PNG smaller than 1 MiB.");
        }

        /// <summary>Same snapshot preparation used by the UI and API fixtures; never touches Steam.</summary>
        public static Request PrepareUpload(PortableMapRoot map,string directory,string title,string preview,string itemId)
        {
            var prepared=PortableMapExporter.Prepare(map);
            if(prepared.PlayerStartCount==0) throw new InvalidOperationException("Add a player start before uploading.");
            Directory.CreateDirectory(directory);
            var request=new Request { jobId=Guid.NewGuid().ToString("N"),file=Path.GetFullPath(Path.Combine(directory,"map.tanukimap")),
                preview=Path.GetFullPath(Path.Combine(directory,"preview.png")),title=title,itemId=itemId??"" };
            prepared.WriteFile(request.file);
            if(string.IsNullOrWhiteSpace(preview)) PortableMapThumbnail.Capture(map,request.preview);
            else File.Copy(preview,request.preview,false);
            File.WriteAllText(Path.Combine(directory,"request.json"),JsonUtility.ToJson(request,true),new UTF8Encoding(false));
            return request;
        }

        public static bool ExportAndUpload(PortableMapRoot map,string title,string preview,string itemId)
        {
            if(IsPending) { Status="A Workshop upload is already pending."; return false; }
            try
            {
                ValidateConfiguration(GameExecutable,UnrealProject,title,preview,itemId);
                string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../Library/TanukiWorkshop",Guid.NewGuid().ToString("N")));
                var request=PrepareUpload(map,directory,title,preview,itemId);
                string args=string.IsNullOrWhiteSpace(UnrealProject) ? "" : Quote(Path.GetFullPath(UnrealProject))+" /Engine/Maps/Entry -game ";
                args+="-ini:Engine:[OnlineSubsystem]:DefaultPlatformService=Steam -ini:Engine:[OnlineSubsystemSteam]:bEnabled=true ";
                args+="-nullrhi -LiveCoding=false -nosound -nosplash -PortableMapEditorJob="+Quote(Path.Combine(directory,"request.json"))+" -abslog="+Quote(Path.Combine(directory,"uploader.log"));
                using(var process=Process.Start(new ProcessStartInfo(Path.GetFullPath(GameExecutable),args) {
                    UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(Path.GetFullPath(GameExecutable)) }))
                {
                    if(process==null) throw new InvalidOperationException("Cannot start the Tanuki uploader.");
                    active=new ActiveJob { directory=directory,id=request.jobId,mapId=map.mapId,processId=process.Id,started=DateTime.UtcNow.Ticks };
                }
                EditorPrefs.SetString(ProjectKey+".Active",JsonUtility.ToJson(active));
                Status="Map exported. Starting Tanuki's Steam uploader..."; HasError=false; return true;
            }
            catch(Exception error) { Status=error.Message; HasError=true; return false; }
        }
        // Windows CommandLineToArgvW quoting. Titles and file contents never enter a shell.
        public static string Quote(string value)
        {
            var result=new StringBuilder("\""); int slashes=0;
            foreach(char c in value)
            {
                if(c=='\\') { ++slashes; continue; }
                result.Append('\\',c=='"' ? slashes*2+1 : slashes); result.Append(c); slashes=0;
            }
            result.Append('\\',slashes*2); result.Append('"'); return result.ToString();
        }
        static void Finish(bool success,string message,string item)
        {
            if(!string.IsNullOrEmpty(item) && ulong.TryParse(item,out ulong id) && id!=0)
            { EditorPrefs.SetString(ProjectKey+".Item."+Key(active.mapId),item); LastItemId=item; }
            Status=message; HasError=!success;
            if(!success) Status+="\nJob files: "+active.directory;
            active=null; EditorPrefs.DeleteKey(ProjectKey+".Active");
        }
        static void Poll()
        {
            if(active==null) return;
            if(new TimeSpan(DateTime.UtcNow.Ticks-active.started).TotalSeconds>240)
            { Finish(false,"Uploader timed out. The outcome is unknown; inspect the log before creating another item.",""); return; }
            try
            {
                string file=Path.Combine(active.directory,"result.json");
                if(File.Exists(file))
                {
                    var result=JsonUtility.FromJson<Result>(File.ReadAllText(file));
                    if(result.version!=1 || result.jobId!=active.id) { Finish(false,"Uploader response identity does not match this job. Inspect the log before retrying.",""); return; }
                    Status=result.message;
                    if(!result.pending) { Finish(result.success,result.message,result.itemId); return; }
                }
                bool running;
                try { using(var process=Process.GetProcessById(active.processId)) running=!process.HasExited; }
                catch(ArgumentException) { running=false; }
                if(!running || new TimeSpan(DateTime.UtcNow.Ticks-active.started).TotalSeconds>240)
                    Finish(false,"Uploader exited or timed out without a final response. The upload outcome is unknown; inspect the log before creating another item.","");
            }
            catch(IOException) { /* Atomic result replacement may briefly share-lock the file. Read on the next update. */ }
            catch(Exception error) { Finish(false,"Cannot read the uploader response: "+error.Message,""); }
        }
    }
}
