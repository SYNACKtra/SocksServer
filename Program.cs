//using Gtk;
using System;
using System.Globalization;
using System.Net;
using System.Threading;
 
namespace SocksServer
{
    class Program
    {
        private static long m_StartTime;
        public static long StartTime
        {
            get
            {
                return m_StartTime;
            }
            set
            {
                m_StartTime = value;
            }
        }

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
            DateTime start = DateTime.Now;
            long unixTime = ((DateTimeOffset)start).ToUnixTimeMilliseconds();
            m_StartTime = unixTime;
            Thread.Sleep(29);
            Console.WriteLine(new avg(2^5, 0));

            Server server = new Server (100, 512);
            server.Init();
            server.Start(CreateIPEndPoint());
        }
    }
}