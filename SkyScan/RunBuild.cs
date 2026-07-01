using System;
using System.Diagnostics;
using System.IO;

class Program
{
    static void Main()
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "build",
                WorkingDirectory = @"d:\depi gp\fly-depi\SkyScan",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        
        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        
        File.WriteAllText(@"d:\depi gp\fly-depi\SkyScan\build_output.txt", output + "\n" + error);
    }
}
