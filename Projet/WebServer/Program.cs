using System;
using System.IO;
using System.Net;
using System.Threading;

namespace WebServer
{
    class Program
    {
        static void Main(string[] args)
        {

            string url = "http://localhost:8080/";
            string webFolder = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\SiteWeb\web"));

            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();

            Console.WriteLine($"Front lancé");
            Console.WriteLine($"URL : {url}");

            while (true)
            {
                try
                {
                    var context = listener.GetContext();
                    ThreadPool.QueueUserWorkItem(o => ProcessRequest(context, webFolder));
                }
                catch { }
            }
        }

        static void ProcessRequest(HttpListenerContext context, string rootPath)
        {
            string filename = context.Request.Url.AbsolutePath.Substring(1);
            if (string.IsNullOrEmpty(filename)) filename = "index.html";

            string filePath = Path.Combine(rootPath, filename);

            if (File.Exists(filePath))
            {
                try
                {
                    byte[] content = File.ReadAllBytes(filePath);
                    context.Response.ContentType = GetContentType(filePath);
                    context.Response.ContentLength64 = content.Length;
                    context.Response.OutputStream.Write(content, 0, content.Length);
                }
                catch { context.Response.StatusCode = 500; }
            }
            else
            {
                context.Response.StatusCode = 404;
            }
            context.Response.OutputStream.Close();
        }

        static string GetContentType(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            return ext == ".html" ? "text/html" :
                   ext == ".js" ? "application/javascript" :
                   ext == ".css" ? "text/css" :
                   ext == ".png" ? "image/png" : "application/octet-stream";
        }
    }
}