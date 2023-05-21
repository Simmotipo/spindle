using System.Net.Sockets;
using System.Net;
using System.Text;
using static Spindle.UdpServer;

namespace Spindle
{
    class Program
    {
        static bool fileMode;
        static string fPath = "";
        static string? srcUrl = "";
        static string? dstUrl = "";
        static double megabitsPerSecond;
        static double pktDelay;
        static readonly double pktSize = 1312d;
        

        public static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                //Get FILE or MEM Mode
                char input = 'X';
                while (input != 'F' && input != 'f' && input != 'M' && input != 'm')
                {
                    Console.Write("FILE or MEM mode? ");
                    input = Console.ReadKey().KeyChar;
                }
                Console.SetCursorPosition(0, 0);
                if (input == 'f' || input == 'F')
                {
                    fileMode = true;
                    Console.WriteLine("FILE or MEM mode? FILE");
                }
                else
                {
                    fileMode = false;
                    Console.WriteLine("FILE or MEM mode? MEM");
                    Console.WriteLine("MEM mode is not yet implemented. Sorry.");
                    Console.ReadLine();
                    Environment.Exit(0);
                }

                //Get FILE PATH if in FILE mode
                string? temp = "";
                if (fileMode)
                {
                    while (!Directory.Exists(temp))
                    {
                        Console.Write("Directory path: ");
                        temp = Console.ReadLine();
                    }
                }

                //Get source URL
                Console.Write("Enter Source URL: ");
                srcUrl = Console.ReadLine();

                //Get destination URL
                Console.Write("Enter Destination URL: ");
                dstUrl = Console.ReadLine();

                //Get datarate
                Console.Write("Enter CBR Datarate: ");
                megabitsPerSecond = Convert.ToDouble(Console.ReadLine());

                //Calculate time between UDP pkts.
                double udpPeriod = (megabitsPerSecond * 1000000) / pktSize;

                //Get desired delay
                Console.Write("Enter time delay (in seconds): ");
                pktDelay = udpPeriod * Convert.ToDouble(Console.ReadLine());

                Console.WriteLine($"Delay transmission of {megabitsPerSecond}Mbps stream from {srcUrl} to {dstUrl} by {Math.Round(pktDelay)} packets");
                Console.ReadLine();

                int fSize = (int)(pktDelay * pktSize) + (int)(pktSize * 100);
                fPath += srcUrl.Replace(".", "-") + ".buf";

                byte[] buffer = new byte[fSize];

                File.WriteAllBytes(fPath, buffer);


                FileBufferReader fbr = new(udpPeriod, fPath, (int)-(pktDelay * pktSize), (int)pktSize, dstUrl);
                FileBufferWriter fbw = new(fPath, (int)pktSize, srcUrl);
                fbw.me = fbw;

            }
            else
            {
                /*if (File.Exists(args[0])) { fPath = args[0]; fileMode = true; }
                else fileMode = false;

                srcUrl = args[1];
                dstUrl = args[2];*/
                Console.WriteLine("Please re-run without args and use the prompter instead for now; sorry!");
            }
        }
    }

    class FileBufferReader
    {
        static double millisPerPkt;
        static string fPath = "";
        static double fSize;
        static int ptrLoc;
        static int pktSize;
        static string target = "";
        static DateTime lastRead;

        public FileBufferReader(double udpPeriod, string filePath, int startIndex, int packetSize, string? dstUrl)
        {
            if (dstUrl is null)
            {
                throw new ArgumentNullException(nameof(dstUrl));
            }
            millisPerPkt = udpPeriod * 1000;
            fPath = filePath;
            ptrLoc = startIndex;
            pktSize = packetSize;
            lastRead = DateTime.UtcNow;
            target = dstUrl;
            fSize = new FileInfo(fPath).Length;
            
            //Initiate this void in a background thread!!
            Reader();
        }

        static void Reader()
        {
            while (true)
            {
                if (DateTime.UtcNow > lastRead.AddMilliseconds(millisPerPkt))
                {
                    if (ptrLoc >= 0)
                    {
                        byte[] data = new byte[pktSize];
                        using (BinaryReader reader = new(new FileStream(fPath, FileMode.Open)))
                        {
                            reader.BaseStream.Seek(ptrLoc, SeekOrigin.Begin);
                            reader.Read(data, 0, pktSize);
                        }
                        Send(data);
                    }
                    
                    ptrLoc += pktSize;
                    if (ptrLoc + pktSize > fSize) ptrLoc = 0;
                    lastRead = lastRead.AddMilliseconds(millisPerPkt);
                }
            }
        }

        static void Send(byte[] data)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            UDPSocket c = new UDPSocket();
            c.Client(target.Split(':')[0], Convert.ToInt32(target.Split(':')[1]));
            c.Send(data);
        }
    }

    class FileBufferWriter
    {
        static string fPath = "";
        static double fSize;
        static int ptrLoc;
        static int pktSize;
        static string source = "";
        public FileBufferWriter? me = null;

        public FileBufferWriter(string filePath, int packetSize, string? srcUrl)
        {
            if (srcUrl is null)
            {
                throw new ArgumentNullException(nameof(srcUrl));
            }
            fPath = filePath;
            fSize = new FileInfo(fPath).Length;
            ptrLoc = 0;
            pktSize = packetSize;
            source = srcUrl;

            //Initiate this void in a background thread!!
            UdpListen();
        }

        void UdpListen()
        {
            while (me == null) Thread.Sleep(10);
            UDPSocket s = new UDPSocket();
            s.Server(source.Split(':')[0], Convert.ToInt32(source.Split(':')[1]), me);
        }

        public void Write(byte[] data)
        {
            using (FileStream fs = new(fPath, FileMode.Open))
            {
                fs.Position = ptrLoc;
                fs.Write(data);
                fs.Close();
            }
            ptrLoc += pktSize;
            if (ptrLoc + pktSize > fSize) ptrLoc = 0;
        }
    }



    class UdpServer
    {
        public class UDPSocket
        {
            private Socket _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            private const int bufSize = 8 * 1024;
            private State state = new State();
            private EndPoint epFrom = new IPEndPoint(IPAddress.Any, 0);
            private AsyncCallback? recv = null;
            FileBufferWriter fbw;

            public class State
            {
                public byte[] buffer = new byte[bufSize];
            }

            public void Server(string address, int port, FileBufferWriter f)
            {
                _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);
                _socket.Bind(new IPEndPoint(IPAddress.Parse(address), port));
                fbw = f;
                Receive();
            }

            public void Client(string address, int port)
            {
                _socket.Connect(IPAddress.Parse(address), port);
                Receive();
            }

            public void Send(byte[] data)
            {
                _socket.BeginSend(data, 0, data.Length, SocketFlags.None, (ar) =>
                {
                    State? so = (State?)ar.AsyncState;
                    int bytes = _socket.EndSend(ar);
                    Console.WriteLine("SEND: {0}", bytes);
                }, state);
            }

            private void Receive()
            {
                _socket.BeginReceiveFrom(state.buffer, 0, bufSize, SocketFlags.None, ref epFrom, recv = (ar) =>
                {
                    State? so = (State?)ar.AsyncState;
                    int bytes = _socket.EndReceiveFrom(ar, ref epFrom);
                    _socket.BeginReceiveFrom(so.buffer, 0, bufSize, SocketFlags.None, ref epFrom, recv, so);
                    fbw.Write(so.buffer);
                    Console.WriteLine("RECV: {0}: {1}, {2}", epFrom.ToString(), bytes, Encoding.ASCII.GetString(so.buffer, 0, bytes));
                }, state);
            }
        }
    }
}