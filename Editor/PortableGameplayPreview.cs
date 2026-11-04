using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace Tanuki.Authoring
{
    /// <summary>Shared external Tanuki player protocol. No editor or game-engine dependency.</summary>
    public sealed class PortableGameplayPreview : IDisposable
    {
        public const string ReadyMarker="TANUKI_PREVIEW_READY";
        readonly Process process;
        public string DirectoryPath {get;}
        public string MapFile=>Path.Combine(DirectoryPath,"map.tanukimap");
        public string LogFile=>Path.Combine(DirectoryPath,"preview.log");
        public bool HasExited=>process==null||process.HasExited;
        public static string ReadLog(string path)
        {
            if(!File.Exists(path))return "";
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
            using(var reader=new StreamReader(stream))return reader.ReadToEnd();
        }
        static string Quote(string value){if(value.Contains("\""))throw new ArgumentException("Invalid quoted preview path.");return "\""+value+"\"";}
        public static string Resolve(string executable)
        {
            executable=Path.GetFullPath(executable);
            if(!File.Exists(executable)||Path.GetFileNameWithoutExtension(executable)!="TanukiMapPreview")throw new InvalidOperationException("Choose TanukiMapPreview.exe from the Tanuki gameplay preview package.");
            string descriptor=null;
            for(var parent=new DirectoryInfo(Path.GetDirectoryName(executable));parent!=null;parent=parent.Parent)
            {var candidate=Path.Combine(parent.FullName,"preview.json");if(File.Exists(candidate)){descriptor=candidate;break;}}
            var match=descriptor==null?Match.Empty:Regex.Match(File.ReadAllText(descriptor),"\"maxSchemaVersion\"\\s*:\\s*(\\d+)");
            if(!match.Success||!int.TryParse(match.Groups[1].Value,out int schema)||schema<4)throw new InvalidOperationException("Update the Tanuki gameplay preview player: this map requires schema 4.");
            string native=Path.Combine(Path.GetDirectoryName(executable),"Tanuki","Binaries","Win64","TanukiMapPreview.exe");return File.Exists(native)?native:executable;
        }
        public PortableGameplayPreview(string executable,string parent,Action<string> export)
        {
            executable=Resolve(executable);DirectoryPath=Path.Combine(parent,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(DirectoryPath);
            try{export(MapFile);process=Process.Start(new ProcessStartInfo{FileName=executable,Arguments=Quote("-TanukiPreviewMap="+MapFile)+" "+Quote("-abslog="+LogFile)+" -CustomConfig=MapPreview -windowed -ResX=1280 -ResY=720",WorkingDirectory=Path.GetDirectoryName(executable),UseShellExecute=false});if(process==null)throw new IOException("Tanuki preview did not start.");}
            catch{File.Delete(MapFile);if(Directory.Exists(DirectoryPath)&&Directory.GetFileSystemEntries(DirectoryPath).Length==0)Directory.Delete(DirectoryPath);throw;}
        }
        public string Complete()
        {
            if(!HasExited)throw new InvalidOperationException("Preview is still running.");
            bool ready=ReadLog(LogFile).Contains(ReadyMarker);
            bool success=process!=null&&process.ExitCode==0&&ready;
            File.Delete(MapFile);
            if(success){File.Delete(LogFile);if(Directory.GetFileSystemEntries(DirectoryPath).Length==0)Directory.Delete(DirectoryPath);}
            process?.Dispose();return success?"Preview closed. Continue editing.":"Preview failed; diagnostics: "+LogFile;
        }
        public void Dispose(){if(!HasExited){process.Kill();process.WaitForExit();}File.Delete(MapFile);process?.Dispose();}
    }
}
