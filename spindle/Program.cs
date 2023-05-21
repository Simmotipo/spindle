using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Security.Cryptography;

namespace Spindle
{
    class Program
    {
        static bool fileMode;
        static string fPath;
        static string srcUrl;
        static string dstUrl;
        static double megabitsPerSecond;
        static double pktDelay;
        static double pktSize = 1312d;
        

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
                }

                //Get FILE PATH if in FILE mode
                string temp = "";
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

                fileBufferReader fbr = new fileBufferReader(udpPeriod, fPath, (int)-(pktDelay * pktSize), (int)pktSize, dstUrl);
                fileBufferWriter fbw = new fileBufferWriter(fPath, (int)pktSize, srcUrl);

            }
            else
            {
                if (File.Exists(args[0])) { fPath = args[0]; fileMode = true; }
                else fileMode = false;

                srcUrl = args[1];
                dstUrl = args[2];
            }
        }
    }

    class fileBufferReader
    {
        static double millisPerPkt;
        static string fPath;
        static double fSize;
        static int ptrLoc;
        static int pktSize;
        static string target;
        static DateTime lastRead;

        public fileBufferReader(double udpPeriod, string filePath, int startIndex, int packetSize, string dstUrl)
        {
            millisPerPkt = udpPeriod * 1000;
            fPath = filePath;
            ptrLoc = startIndex;
            pktSize = packetSize;
            lastRead = DateTime.UtcNow;
            target = dstUrl;
            fSize = new FileInfo(fPath).Length;
            
            //Initiate this void in a background thread!!
            reader();
        }

        static void reader()
        {
            while (true)
            {
                if (DateTime.UtcNow > lastRead.AddMilliseconds(millisPerPkt))
                {
                    if (ptrLoc >= 0)
                    {
                        byte[] data = new byte[pktSize];
                        using (BinaryReader reader = new BinaryReader(new FileStream(fPath, FileMode.Open)))
                        {
                            reader.BaseStream.Seek(ptrLoc, SeekOrigin.Begin);
                            reader.Read(data, 0, pktSize);
                        }
                        send(data);
                    }
                    
                    ptrLoc += pktSize;
                    if (ptrLoc + pktSize > fSize) ptrLoc = 0;
                    lastRead = lastRead.AddMilliseconds(millisPerPkt);
                }
            }
        }

        static void send(byte[] data)
        {
            //Send byte[] data to string target
        }
    }

    class fileBufferWriter
    {
        static string fPath;
        static double fSize;
        static int ptrLoc;
        static int pktSize;
        static string source;

        public fileBufferWriter(string filePath, int packetSize, string srcUrl)
        {
            fPath = filePath;
            fSize = new FileInfo(fPath).Length;
            ptrLoc = 0;
            pktSize = packetSize;
            source = srcUrl;

            //Initiate this void in a background thread!!
            udpListen();
        }

        static void udpListen()
        {
            //do whatevers
            //on byte receive - write(data);
        }

        static void write(byte[] data)
        {
            using (FileStream fs = new FileStream(fPath, FileMode.Open))
            {
                fs.Position = ptrLoc;
                fs.Write(data);
                fs.Close();
            }
            ptrLoc += pktSize;
            if (ptrLoc + pktSize > fSize) ptrLoc = 0;
        }
    }
}