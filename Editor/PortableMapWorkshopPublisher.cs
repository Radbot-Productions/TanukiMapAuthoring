using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
#if UNITY_EDITOR_WIN
using Radbot.MapAuthoring.Steamworks;
#endif

namespace Radbot.MapAuthoring.Editor
{
    /// <summary>Editor-owned Steam UGC publishing. No game process or credentials are used.</summary>
    [InitializeOnLoad]
    public static class PortableMapWorkshopPublisher
    {
        public const uint ConsumerAppId=5128560;
        [Serializable] public sealed class Request
        { public int version=1; public string jobId,action="upload",file,title,preview,itemId; }
        [Serializable] public sealed class Result
        { public int version=1; public string jobId,message,itemId,hash,file; public bool pending,success; }
        enum Stage { Create,Details,Submit,Subscribe }
        [Serializable] sealed class ActiveUpload
        {
            public int schema=2;
            public Request request;
            public string directory,mapId,item,call,query,owner,hash;
            public long started;
            public Stage stage;
        }
        static ActiveUpload active;
        static bool initialized;
        static string account="Steam: not connected";
        public static bool IsPending => active!=null;
        public static string Status { get; private set; }="";
        public static bool HasError { get; private set; }
        public static string LastItemId { get; private set; }="";
        public static string Account => account;
        static string ProjectKey => "Tanuki.Workshop."+Key(Application.dataPath);
        static string ActiveKey => ProjectKey+".DirectUpload";
#if UNITY_EDITOR_WIN
        static CallResult<CreateItemResult_t> created;
        static CallResult<SteamUGCQueryCompleted_t> details;
        static CallResult<SubmitItemUpdateResult_t> submitted;
        static CallResult<RemoteStorageSubscribePublishedFileResult_t> subscribed;
#endif
        static PortableMapWorkshopPublisher()
        {
            string saved=EditorPrefs.GetString(ActiveKey,"");
            if(saved.Length>0)
            {
                try
                {
                    active=JsonUtility.FromJson<ActiveUpload>(saved);
                    if(active==null || active.schema!=2 || active.request==null) throw new InvalidOperationException("Invalid saved upload state.");
                    Status="Reconnecting to the pending Steam upload...";
                }
                catch(Exception e) { active=null; HasError=true; Status=e.Message; EditorPrefs.DeleteKey(ActiveKey); }
            }
            EditorApplication.update+=Poll;
            AssemblyReloadEvents.beforeAssemblyReload+=DisposeBindings;
            EditorApplication.quitting+=()=>
            {
                if(active!=null) Finish(false,"Unity closed during an upload. The outcome is uncertain; update the recorded item ID after checking Steam.");
                ShutdownSteam();
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.ExitingEditMode) return;
                if(active!=null) Finish(false,"Play mode interrupted the editor upload. Check Steam before retrying the recorded item ID.");
                ShutdownSteam();
            };
        }
        static string Key(string value)
        { using(var hash=SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-",""); }
        public static string GetRememberedItem(string mapId) => EditorPrefs.GetString(ProjectKey+".Item."+Key(mapId??""),"");
        public static void ValidateConfiguration(string title,string preview,string itemId)
        {
            if(Application.platform!=RuntimePlatform.WindowsEditor) throw new InvalidOperationException("Workshop publishing currently supports Windows Editor.");
            if(string.IsNullOrWhiteSpace(title) || title.Length>128) throw new InvalidOperationException("Enter a Workshop title of 1-128 characters.");
            if(!string.IsNullOrEmpty(itemId) && (!ulong.TryParse(itemId,NumberStyles.None,CultureInfo.InvariantCulture,out ulong id) || id==0))
                throw new InvalidOperationException("Workshop item ID must be positive, or empty to create an item.");
            if(string.IsNullOrWhiteSpace(preview)) return;
            if(!File.Exists(preview) || !string.Equals(Path.GetExtension(preview),".png",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Choose a preview PNG, or leave it blank for a framed map view.");
            var bytes=File.ReadAllBytes(preview);
            if(bytes.Length>=1024*1024 || bytes.Length<8 || bytes[0]!=137 || bytes[1]!=80 || bytes[2]!=78 || bytes[3]!=71 || bytes[4]!=13 || bytes[5]!=10 || bytes[6]!=26 || bytes[7]!=10)
                throw new InvalidOperationException("The preview must be a PNG smaller than 1 MiB.");
        }
        public static bool Connect()
        {
            try
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Publish from Edit mode.");
#if UNITY_EDITOR_WIN
                if(initialized && !SteamUser.BLoggedOn())
                {
                    if(active!=null) throw new InvalidOperationException("Steam disconnected during an upload. Check the recorded item ID before reconnecting.");
                    ShutdownSteam();
                }
                if(!initialized)
                {
                    // Private bindings/DLL prevent accidental version binding. Refuse a competing SDK owner.
                    foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
                        if(assembly.GetType("Steamworks.SteamAPI",false)!=null || assembly.GetType("Steamworks.SteamClient",false)!=null)
                            throw new InvalidOperationException("Another Steam integration is loaded in this Unity project. Use a dedicated Tanuki authoring project to avoid changing its Steam session.");
                    string oldApp=Environment.GetEnvironmentVariable("SteamAppId"),oldGame=Environment.GetEnvironmentVariable("SteamGameId");
                    try
                    {
                        Environment.SetEnvironmentVariable("SteamAppId",ConsumerAppId.ToString(CultureInfo.InvariantCulture));
                        Environment.SetEnvironmentVariable("SteamGameId",ConsumerAppId.ToString(CultureInfo.InvariantCulture));
                        var result=SteamAPI.InitEx(out string error);
                        if(result!=ESteamAPIInitResult.k_ESteamAPIInitResult_OK) throw new InvalidOperationException("Run Steam and sign in with Tanuki access. "+error);
                        initialized=true;
                    }
                    finally { Environment.SetEnvironmentVariable("SteamAppId",oldApp); Environment.SetEnvironmentVariable("SteamGameId",oldGame); }
                }
                if(SteamUtils.GetAppID().m_AppId!=ConsumerAppId) throw new InvalidOperationException("Steam connected to another app; reconnect from a Tanuki authoring project.");
                if(!SteamUser.BLoggedOn() || !SteamApps.BIsSubscribedApp(new AppId_t(ConsumerAppId))) throw new InvalidOperationException("Sign into Steam with an account that has Tanuki access, then reconnect.");
                account="Steam: "+SteamFriends.GetPersonaName();
                if(active!=null)
                {
                    if(active.owner!=SteamUser.GetSteamID().ToString()) throw new InvalidOperationException("Steam account changed during this upload. Check the recorded item ID before retrying.");
                    BindCurrentCall();
                }
                HasError=false;
                if(active==null) Status="Connected to the developer-only Tanuki Workshop.";
                return true;
#else
                throw new InvalidOperationException("Workshop publishing currently supports Windows Editor.");
#endif
            }
            catch(Exception e) { Status=e.Message; HasError=true; return false; }
        }
        public static bool CanUpload(out string reason)
        {
            reason="";
            if(IsPending) reason="A Workshop upload is already pending.";
            else if(EditorApplication.isPlayingOrWillChangePlaymode) reason="Publish from Edit mode.";
            else if(!initialized) reason="Connect to the logged-in Steam client first.";
#if UNITY_EDITOR_WIN
            else if(!SteamUser.BLoggedOn() || SteamUtils.GetAppID().m_AppId!=ConsumerAppId) reason="Steam account/app context is no longer available.";
#endif
            return reason.Length==0;
        }
        public static Request PrepareUpload(PortableMapRoot map,string directory,string title,string preview,string itemId)
        {
            ValidateConfiguration(title,preview,itemId);
            var prepared=PortableMapExporter.Prepare(map);
            if(prepared.PlayerStartCount==0) throw new InvalidOperationException("Add a player start before uploading.");
            Directory.CreateDirectory(directory);
            var request=new Request { jobId=Guid.NewGuid().ToString("N"),file=Path.GetFullPath(Path.Combine(directory,"content/map.tanukimap")),
                preview=Path.GetFullPath(Path.Combine(directory,"preview.png")),title=title,itemId=itemId??"" };
            prepared.WriteFile(request.file);
            if(string.IsNullOrWhiteSpace(preview)) PortableMapThumbnail.Capture(map,request.preview); else File.Copy(preview,request.preview,false);
            File.WriteAllText(Path.Combine(directory,"request.json"),JsonUtility.ToJson(request,true),new UTF8Encoding(false));
            return request;
        }
        public static bool ExportAndUpload(PortableMapRoot map,string title,string preview,string itemId)
        {
            if(IsPending) return false;
            try
            {
                ValidateConfiguration(title,preview,itemId);
                if(!Connect() || !CanUpload(out string reason)) return false;
                string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../Library/TanukiWorkshop",Guid.NewGuid().ToString("N")));
                var request=PrepareUpload(map,directory,title,preview,itemId);
#if UNITY_EDITOR_WIN
                string hash;
                using(var sha=SHA1.Create()) using(var file=File.OpenRead(request.file)) hash=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","");
                active=new ActiveUpload { request=request,directory=directory,mapId=map.mapId,item=itemId??"",hash=hash,
                    owner=SteamUser.GetSteamID().ToString(),started=DateTime.UtcNow.Ticks,query="0" };
                if(string.IsNullOrEmpty(itemId)) BeginCall(Stage.Create,SteamUGC.CreateItem(new AppId_t(ConsumerAppId),EWorkshopFileType.k_EWorkshopFileTypeCommunity));
                else
                {
                    var query=SteamUGC.CreateQueryUGCDetailsRequest(new[]{new PublishedFileId_t(ulong.Parse(itemId,CultureInfo.InvariantCulture))},1);
                    active.query=query.m_UGCQueryHandle.ToString(CultureInfo.InvariantCulture);
                    BeginCall(Stage.Details,SteamUGC.SendQueryUGCRequest(query));
                }
                return active!=null;
#else
                return false;
#endif
            }
            catch(Exception e)
            { if(active!=null) Finish(false,e.Message); else { Status=e.Message; HasError=true; } return false; }
        }
#if UNITY_EDITOR_WIN
        static void BeginCall(Stage stage,SteamAPICall_t call)
        {
            if(call==SteamAPICall_t.Invalid) throw new InvalidOperationException("Steam refused the publishing operation.");
            active.stage=stage; active.call=call.m_SteamAPICall.ToString(CultureInfo.InvariantCulture);
            Status="Steam Workshop upload pending ("+stage+")"; Persist(); BindCurrentCall();
        }
        static void BindCurrentCall()
        {
            DisposeBindings(); var call=new SteamAPICall_t(ulong.Parse(active.call,CultureInfo.InvariantCulture));
            switch(active.stage)
            {
                case Stage.Create: created=CallResult<CreateItemResult_t>.Create((v,f)=>Dispatch(()=>OnCreated(v,f))); created.Set(call); break;
                case Stage.Details: details=CallResult<SteamUGCQueryCompleted_t>.Create((v,f)=>Dispatch(()=>OnDetails(v,f))); details.Set(call); break;
                case Stage.Submit: submitted=CallResult<SubmitItemUpdateResult_t>.Create((v,f)=>Dispatch(()=>OnSubmitted(v,f))); submitted.Set(call); break;
                case Stage.Subscribe: subscribed=CallResult<RemoteStorageSubscribePublishedFileResult_t>.Create((v,f)=>Dispatch(()=>OnSubscribed(v,f))); subscribed.Set(call); break;
            }
        }
        static void Dispatch(Action action)
        { try { action(); } catch(Exception e) { Finish(false,e.Message+" Check the recorded item ID before retrying."); } }
        static void OnCreated(CreateItemResult_t value,bool failed)
        {
            if(active==null) return;
            if(failed || value.m_eResult!=EResult.k_EResultOK) { Finish(false,"Steam item creation failed: "+value.m_eResult); return; }
            active.item=value.m_nPublishedFileId.m_PublishedFileId.ToString(CultureInfo.InvariantCulture); Persist();
            if(value.m_bUserNeedsToAcceptWorkshopLegalAgreement) { Finish(false,"Accept the Workshop agreement in Steam, then update item "+active.item); return; }
            Submit();
        }
        static void OnDetails(SteamUGCQueryCompleted_t value,bool failed)
        {
            if(active==null) return;
            var query=new UGCQueryHandle_t(ulong.Parse(active.query,CultureInfo.InvariantCulture));
            bool valid=!failed && value.m_eResult==EResult.k_EResultOK && value.m_unNumResultsReturned==1 && SteamUGC.GetQueryUGCResult(query,0,out var item);
            // Query result is read before release; all identity and ownership checks share this path.
            if(!valid) { Finish(false,"Steam cannot read the Workshop item: "+value.m_eResult); return; }
            SteamUGC.GetQueryUGCResult(query,0,out item);
            SteamUGC.ReleaseQueryUGCRequest(query); active.query="0";
            if(item.m_eResult!=EResult.k_EResultOK || item.m_nPublishedFileId.m_PublishedFileId.ToString(CultureInfo.InvariantCulture)!=active.item || item.m_nConsumerAppID.m_AppId!=ConsumerAppId || item.m_bBanned)
            { Finish(false,"Workshop item is inaccessible, banned or belongs to another game."); return; }
            if(item.m_ulSteamIDOwner.ToString(CultureInfo.InvariantCulture)!=active.owner) { Finish(false,"Only the Workshop item's owner can update it."); return; }
            Submit();
        }
        static void Submit()
        {
            var update=SteamUGC.StartItemUpdate(new AppId_t(ConsumerAppId),new PublishedFileId_t(ulong.Parse(active.item,CultureInfo.InvariantCulture)));
            bool configured=SteamUGC.SetItemTitle(update,active.request.title)
                && SteamUGC.SetItemContent(update,Path.GetDirectoryName(active.request.file))
                && SteamUGC.SetItemMetadata(update,"tanukimap/1:"+active.hash)
                && SteamUGC.SetItemVisibility(update,ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic)
                && SteamUGC.SetItemPreview(update,active.request.preview);
            if(!configured) { Finish(false,"Steam refused the map metadata/content/preview."); return; }
            Persist(); BeginCall(Stage.Submit,SteamUGC.SubmitItemUpdate(update,null));
        }
        static void OnSubmitted(SubmitItemUpdateResult_t value,bool failed)
        {
            if(active==null) return;
            if(failed || value.m_eResult!=EResult.k_EResultOK) { Finish(false,"Steam upload failed: "+value.m_eResult+". Update the recorded item ID after checking Steam."); return; }
            if(value.m_bUserNeedsToAcceptWorkshopLegalAgreement) { Finish(false,"Accept the Workshop agreement in Steam, then update the recorded item ID."); return; }
            var call=SteamUGC.SubscribeItem(new PublishedFileId_t(ulong.Parse(active.item,CultureInfo.InvariantCulture)));
            if(call==SteamAPICall_t.Invalid) { Finish(true,"Map uploaded; subscribe to the recorded item in Steam."); return; }
            BeginCall(Stage.Subscribe,call);
        }
        static void OnSubscribed(RemoteStorageSubscribePublishedFileResult_t value,bool failed)
        { if(active!=null) Finish(true,!failed && value.m_eResult==EResult.k_EResultOK ? "Map uploaded and subscribed in the developer-only Workshop." : "Map uploaded, but automatic subscription failed. Use the recorded item ID."); }
#endif
        static void Persist()
        {
            EditorPrefs.SetString(ActiveKey,JsonUtility.ToJson(active));
            if(!string.IsNullOrEmpty(active.item) && active.item!="0")
            { EditorPrefs.SetString(ProjectKey+".Item."+Key(active.mapId),active.item); LastItemId=active.item; }
            WriteResult(true,false,Status);
        }
        static void WriteResult(bool pending,bool success,string message)
        {
            var result=new Result {jobId=active.request.jobId,pending=pending,success=success,message=message,itemId=active.item,hash=active.hash,file=active.request.file};
            string temp=Path.Combine(active.directory,"result.tmp"),file=Path.Combine(active.directory,"result.json");
            File.WriteAllText(temp,JsonUtility.ToJson(result,true),new UTF8Encoding(false));
            if(File.Exists(file)) File.Replace(temp,file,null); else File.Move(temp,file);
        }
        static void Finish(bool success,string message)
        {
            if(active==null) return;
            DisposeBindings();
#if UNITY_EDITOR_WIN
            if(initialized && !string.IsNullOrEmpty(active.query) && active.query!="0") SteamUGC.ReleaseQueryUGCRequest(new UGCQueryHandle_t(ulong.Parse(active.query,CultureInfo.InvariantCulture)));
#endif
            if(!string.IsNullOrEmpty(active.item) && active.item!="0") { EditorPrefs.SetString(ProjectKey+".Item."+Key(active.mapId),active.item); LastItemId=active.item; }
            Status=message; HasError=!success;
            try { WriteResult(false,success,message); }
            catch(IOException e) { Status+=" Receipt could not be saved: "+e.Message; }
            if(!success) Status+="\nJob files: "+active.directory;
            active=null; EditorPrefs.DeleteKey(ActiveKey);
        }
        static void DisposeBindings()
        {
#if UNITY_EDITOR_WIN
            created?.Dispose(); details?.Dispose(); submitted?.Dispose(); subscribed?.Dispose();
            created=null; details=null; submitted=null; subscribed=null;
#endif
        }
        static void ShutdownSteam()
        {
            DisposeBindings();
#if UNITY_EDITOR_WIN
            if(initialized) SteamAPI.Shutdown();
#endif
            initialized=false; account="Steam: not connected";
        }
        static void Poll()
        {
            if(active==null) return;
            try
            {
                if(!initialized && !Connect()) { Finish(false,Status+" Upload outcome is uncertain; check the recorded item ID."); return; }
                if(new TimeSpan(DateTime.UtcNow.Ticks-active.started).TotalSeconds>180)
                { Finish(active.stage==Stage.Subscribe,active.stage==Stage.Subscribe ? "Map uploaded; subscription confirmation timed out." : "Steam upload timed out. Check the recorded item ID before retrying."); return; }
#if UNITY_EDITOR_WIN
                if(!SteamUser.BLoggedOn() || SteamUser.GetSteamID().ToString()!=active.owner) { Finish(false,"Steam account disconnected/changed during the upload. Check the recorded item ID before retrying."); return; }
                SteamAPI.RunCallbacks();
#endif
            }
            catch(Exception e) { Finish(false,e.Message+" Check Steam before retrying the recorded item ID."); }
        }
    }
}
