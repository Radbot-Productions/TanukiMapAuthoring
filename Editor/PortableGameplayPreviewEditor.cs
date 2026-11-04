using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Radbot.MapAuthoring.Editor
{
    [InitializeOnLoad]
    public static class PortableGameplayPreviewEditor
    {
        const string Preference="Tanuki.GameplayPreview.Executable";
        static readonly List<Tanuki.Authoring.PortableGameplayPreview> running=new();
        static PortableGameplayPreviewEditor(){EditorApplication.update+=Poll;AssemblyReloadEvents.beforeAssemblyReload+=StopOwned;EditorApplication.quitting+=StopOwned;}
        public static void Configure(){string path=EditorUtility.OpenFilePanel("Choose TanukiMapPreview.exe",EditorPrefs.GetString(Preference),"exe");if(path.Length>0){Tanuki.Authoring.PortableGameplayPreview.Resolve(path);EditorPrefs.SetString(Preference,path);}}
        public static bool Play(PortableMapRoot root,out string status)
        {
            try{string exe=EditorPrefs.GetString(Preference);if(!File.Exists(exe)){Configure();exe=EditorPrefs.GetString(Preference);}string remembered=root.exportFile;running.Add(new Tanuki.Authoring.PortableGameplayPreview(exe,Path.GetFullPath("Library/TanukiPreview"),file=>{try{PortableMapExporter.ExportFile(root,file);}finally{root.exportFile=remembered;}}));status="Starting Tanuki gameplay preview.";return true;}
            catch(Exception error){status=error.Message;return false;}
        }
        static void Poll(){for(int i=running.Count-1;i>=0;i--)if(running[i].HasExited){Debug.Log(running[i].Complete());running.RemoveAt(i);}}
        static void StopOwned(){foreach(var preview in running){preview.Dispose();}running.Clear();}
    }
}
