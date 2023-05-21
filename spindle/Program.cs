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

                FileBufferReader fbr = new(udpPeriod, fPath, (int)-(pktDelay * pktSize), (int)pktSize, dstUrl);
                FileBufferWriter fbw = new(fPath, (int)pktSize, srcUrl);

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

            _ = new byte[10];
            //Send byte[] data to string target
        }
    }

    class FileBufferWriter
    {
        static string fPath = "";
        static double fSize;
        static int ptrLoc;
        static int pktSize;
        static string source = "";

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

        static void UdpListen()
        {
            //do whatevers
            //on byte receive - write(data);
            Write(new byte[10]);
        }

        static void Write(byte[] data)
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
}