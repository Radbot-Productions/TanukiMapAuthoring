using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Tanuki.Authoring
{
    /// <summary>Offline preparation finishes before any Workshop create/update call.</summary>
    public static class PortableMapPreparation
    {
        static string Quote(string s) => "\""+s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n")+"\"";
        static string Argument(string s) => "\""+s.Replace("\"","\\\"")+"\"";
        static byte[] Sha(byte[] bytes){using(var sha=SHA1.Create())return sha.ComputeHash(bytes);}
        static bool Equal(byte[] a,int offset,byte[] b){if(a.Length-offset<b.Length)return false;for(int i=0;i<b.Length;i++)if(a[offset+i]!=b[i])return false;return true;}
        public static bool IsPrepared(string file)
        {
            string sidecar=file+".prepared";
            if(!File.Exists(sidecar)||new FileInfo(sidecar).Length<64||new FileInfo(sidecar).Length>512L*1024*1024)return false;
            byte[] bytes=File.ReadAllBytes(sidecar);if(BitConverter.ToUInt32(bytes,0)!=2)return false;
            string hash=BitConverter.ToString(Sha(File.ReadAllBytes(file))).Replace("-","").ToLowerInvariant();
            if(!Equal(bytes,24,Sha(Encoding.UTF8.GetBytes(hash))))return false;
            using(var sha=SHA1.Create())return Equal(bytes,4,sha.ComputeHash(bytes,64,bytes.Length-64));
        }
        public static async Task Prepare(string file,string setup,string jobRoot,CancellationToken cancel=default(CancellationToken))
        {
            file=Path.GetFullPath(file);setup=Path.GetFullPath(setup);
            if(!File.Exists(file))throw new IOException("The exported map is missing.");
            if(!File.Exists(setup))throw new IOException("The map preparation helper is missing. Install the current Tanuki authoring kit.");
            // Old helpers must fail this probe before they can launch an interactive player.
            using(var probe=Process.Start(new ProcessStartInfo{FileName=setup,Arguments="--capabilities",UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,WorkingDirectory=Path.GetDirectoryName(setup)}))
            {
                if(probe==null)throw new IOException("Could not start the map preparation helper.");
                var probeOutput=probe.StandardOutput.ReadToEndAsync();var probeDeadline=DateTime.UtcNow.AddSeconds(10);
                try{while(!probe.HasExited){cancel.ThrowIfCancellationRequested();if(DateTime.UtcNow>probeDeadline)throw new IOException("The map preparation helper did not respond.");await Task.Delay(50,cancel).ConfigureAwait(false);}}
                finally{if(!probe.HasExited){probe.Kill();probe.WaitForExit();}}
                string capabilities=await probeOutput.ConfigureAwait(false);
                if(probe.ExitCode!=0||!Regex.IsMatch(capabilities,"\"preparedGeometryVersion\"\\s*:\\s*2"))
                    throw new IOException("Update the Tanuki authoring kit: this preview helper cannot prepare Workshop geometry.");
            }
            string job=Path.Combine(Path.GetFullPath(jobRoot),"prepare-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(job);
            string output=file+".prepared",request=Path.Combine(job,"setup-request.json");
            File.WriteAllText(request,"{\"requiredSchema\":5,\"mapFile\":"+Quote(file)+",\"logFile\":"+Quote(Path.Combine(job,"prepare.log"))+",\"preparedOutput\":"+Quote(output)+"}",new UTF8Encoding(false));
            using(var process=Process.Start(new ProcessStartInfo{FileName=setup,Arguments=Argument(request),UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(setup)}))
            {
                if(process==null)throw new IOException("Could not start offline map preparation.");
                var deadline=DateTime.UtcNow.AddMinutes(20);
                try
                {
                    while(!process.HasExited)
                    {
                        cancel.ThrowIfCancellationRequested();if(DateTime.UtcNow>deadline)throw new TimeoutException("Offline map preparation timed out. Diagnostics: "+job);
                        await Task.Delay(100,cancel).ConfigureAwait(false);
                    }
                }
                finally
                {
                    if(!process.HasExited)
                    {
                        File.WriteAllText(Path.Combine(job,"cancel"),"");
                        if(!process.WaitForExit(30000))
                        {
                            // This PID belongs to the process above; the handle remains open.
                            using(var stop=Process.Start(new ProcessStartInfo{FileName="taskkill.exe",Arguments="/PID "+process.Id+" /T /F",UseShellExecute=false,CreateNoWindow=true}))stop?.WaitForExit();
                            process.WaitForExit();
                        }
                    }
                }
                string resultPath=Path.Combine(job,"setup-result.json"),result=File.Exists(resultPath)?File.ReadAllText(resultPath):"No preparation receipt.";
                if(process.ExitCode!=0||!Regex.IsMatch(result,"\"success\"\\s*:\\s*true")||!IsPrepared(file))
                    throw new IOException("Workshop geometry preparation failed. "+result+" Diagnostics: "+job);
            }
        }
    }
}
