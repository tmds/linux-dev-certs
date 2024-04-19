using System;
using System.Diagnostics;
using System.Text;

namespace LinuxDevCerts
{
    static class ProcessHelper
    {
        public static void SudoExecute(string[] arguments, char[]? input = null)
            => Execute(arguments, input, sudo: true);

        public static void SudoExecute(params string[] arguments)
            => Execute(arguments, input: null, sudo: true);

        public static void Execute(params string[] arguments)
            => Execute(arguments, input: null, sudo: false);

        public static void Execute(string[] arguments, char[]? input = null, bool sudo = false)
        {
            Process process = new Process()
            {
                StartInfo =
                {
                    FileName = "sudo",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true
                }
            };
            int i = 0;
            process.StartInfo.FileName = sudo ? "sudo" : arguments[i++];
            for (; i < arguments.Length; i++)
            {
                process.StartInfo.ArgumentList.Add(arguments[i]);
            }
            StringBuilder stdErr = new StringBuilder();
            process.ErrorDataReceived += (o, e) => {
                if (e.Data != null)
                {
                    stdErr.AppendLine(e.Data);
                }
            };
            process.Start();
            process.BeginErrorReadLine(); // ignore stdout.
            process.BeginOutputReadLine();
            if (input != null)
            {
                process.StandardInput.Write(input);
            }
            process.StandardInput.Close();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new Exception($"Unable to excute 'sudo {string.Join(" ", arguments)}': {stdErr}.");
            }
        }
    }
}