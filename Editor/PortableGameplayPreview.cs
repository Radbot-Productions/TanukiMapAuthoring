using System;
using System.Diagnostics;
using System.IO;

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
        static string Quote(string value){if(value.Contains("\""))throw new ArgumentException("Invalid quoted preview path.");return "\""+value+"\"";}
        public static string Resolve(string executable)
        {
            executable=Path.GetFullPath(executable);
            if(!File.Exists(executable)||Path.GetFileNameWithoutExtension(executable)!="TanukiMapPreview")throw new InvalidOperationException("Choose TanukiMapPreview.exe from the Tanuki gameplay preview package.");
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
            bool ready=File.Exists(LogFile)&&File.ReadAllText(LogFile).Contains(ReadyMarker);
            bool success=process!=null&&process.ExitCode==0&&ready;
            File.Delete(MapFile);
            if(success){File.Delete(LogFile);if(Directory.GetFileSystemEntries(DirectoryPath).Length==0)Directory.Delete(DirectoryPath);}
            process?.Dispose();return success?"Preview closed. Continue editing.":"Preview failed; diagnostics: "+LogFile;
        }
        public void Dispose(){if(!HasExited){process.Kill();process.WaitForExit();}File.Delete(MapFile);process?.Dispose();}
    }
}
