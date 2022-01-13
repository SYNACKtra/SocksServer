//using Gtk;
using System;
using System.Globalization;
using System.Net;
 
class Hello
{
    public static IPEndPoint CreateIPEndPoint()
    {
        IPAddress ip;
        if(!IPAddress.TryParse("127.0.0.1", out ip))
        {
            throw new FormatException("Invalid ip-adress");
        }

        return new IPEndPoint(ip, 8080);
    }

    static void Main()
    {
        Server server = new Server (100, 255);
        server.Init();
        server.Start(CreateIPEndPoint());
    }
}