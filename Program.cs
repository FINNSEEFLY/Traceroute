using System;
using System.Net;
using System.Net.Sockets;

namespace traceroute
{
    class Program
    {
        const int TYPE_REQUEST_ECHO = 8;
        const int TYPE_TIME_TO_LIVE_EXCEEDED = 11;
        const int TYPE_ECHO_REPLY = 0;
        const int MESSAGE_MAX_SIZE = 65500;
        const int MAX_NUM_OF_REQUESTS = 3;
        static int LAST_NUM_OF_REQUESTS;
        const int MAX_TTL = 55;
        const int MAXIMUM_WAITING_TIME = 2500;
        const int MAX_ERROR_COUNTER = 30;
        static int NUM_IN_ARGS_OF_ADDR;
        const string KEY_SHOW_NAMES = "-a";
        static int CalculatePing(DateTime beginmoment, DateTime endmoment)
        {
            var result = endmoment.Millisecond - beginmoment.Millisecond;
            if (result < 0)
            {
                result = 1000 - Math.Abs(result);
            }
            return result;
        }
        static void Main(string[] args)
        {
            LAST_NUM_OF_REQUESTS = MAX_NUM_OF_REQUESTS - 1;
            IPAddress targetIP;
            IPEndPoint targetIPEndPoint;
            int numOfArgs = args.Length;
            if (numOfArgs == 1)
            {
                NUM_IN_ARGS_OF_ADDR = 0;
            }
            else
            {
                if (numOfArgs == 2 && args[0] == KEY_SHOW_NAMES)
                    NUM_IN_ARGS_OF_ADDR = 1;
                else
                {
                    Console.WriteLine("Возможен ввод в формате: \ntraceroute URL/IP \ntraceroute -a URL/IP - для разрешения имён всех узлов");
                    return;
                }
            }
            // Введенная строка является допустимым IPv4 адресом
            if (IPAddress.TryParse(args[NUM_IN_ARGS_OF_ADDR], out targetIP))
            {
                if (targetIP.AddressFamily != AddressFamily.InterNetwork)
                {
                    Console.WriteLine("Целевой узел должен быть либо в виде URL, либо IPv4 IP");
                    return;
                }
                else
                {
                    targetIPEndPoint = new IPEndPoint(targetIP, 0);
                    try
                    {
                        var hostName = Dns.GetHostEntry(targetIP).HostName;
                        Console.WriteLine("\nТрассировка маршрута к {0} [{1}]\nс максимальным числом прыжков {2}:\n", hostName, targetIP, MAX_TTL);
                    }
                    catch
                    {
                        Console.WriteLine("\nТрассировка маршрута к {0} с максимальным числом прыжков {1}:\n", targetIP, MAX_TTL);
                    }

                }
            }
            // Введенная строка не является допустимым IPv4 адресом
            // и рассматривается как URL
            else
            {
                try
                {
                    IPHostEntry ipHostEntry = Dns.GetHostEntry(args[NUM_IN_ARGS_OF_ADDR]);
                    targetIPEndPoint = new IPEndPoint(ipHostEntry.AddressList[0], 0);
                    Console.WriteLine("\nТрассировка маршрута к {0} [{1}]\nс максимальным числом прыжков {2}:\n", args[NUM_IN_ARGS_OF_ADDR], targetIPEndPoint.Address, MAX_TTL);
                }
                catch (SocketException)
                {
                    Console.WriteLine("Не удается разрешить системное имя узла {0}.", args[NUM_IN_ARGS_OF_ADDR]);
                    return;
                }
            }

            var packet = new ICMP(TYPE_REQUEST_ECHO, "abcdefghijklmnopqrstuvwabcdefghi");
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp);
            Traceroute(socket, packet, targetIPEndPoint, args);

        }
        static void Traceroute(Socket socket, ICMP packet, IPEndPoint targetIPEndPoint, string[] consoleArgs)
        {
            int responseSize, errorCounter = 0;
            byte[] responseBytes;
            int numOfConsoleArgs = consoleArgs.Length;
            DateTime beginMoment, endMoment;

            EndPoint TargetEndPoint = targetIPEndPoint;
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, MAXIMUM_WAITING_TIME);
            for (int TTL = 1; TTL < MAX_TTL; TTL++)
            {
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, TTL);
                Console.Write("[ {0} ]\t", TTL);
                for (int numOFRequest = 0; numOFRequest < MAX_NUM_OF_REQUESTS; numOFRequest++)
                {

                    beginMoment = DateTime.Now;

                    responseBytes = new byte[MESSAGE_MAX_SIZE];
                    socket.SendTo(packet.ToBytes(), packet.PacketSize, SocketFlags.None, targetIPEndPoint);
                    try
                    { 
                        responseSize = socket.ReceiveFrom(responseBytes, ref TargetEndPoint);

                        endMoment = DateTime.Now;

                        ICMP responsePacket = new ICMP(responseBytes, responseSize);
                        if ((responsePacket.PacketType == TYPE_ECHO_REPLY) || (responsePacket.PacketType == TYPE_TIME_TO_LIVE_EXCEEDED))
                        {
                            if (CalculatePing(beginMoment, endMoment) == 0)
                            {
                                Console.Write("<1 мс\t");
                            }
                            else
                            {
                                Console.Write("{0} мс\t", CalculatePing(beginMoment, endMoment));
                            }
                            if (numOFRequest == LAST_NUM_OF_REQUESTS)
                            {
                                String hitIP = TargetEndPoint.ToString();
                                hitIP = hitIP.Replace(":0", "");
                                if (numOfConsoleArgs == 1)
                                {
                                    Console.WriteLine("{0}", hitIP);
                                }
                                else
                                {
                                    if (numOfConsoleArgs == 2 && consoleArgs[0] == KEY_SHOW_NAMES)
                                    {

                                        try
                                        {
                                            var hostName = Dns.GetHostEntry(IPAddress.Parse(hitIP)).HostName;
                                            Console.WriteLine("{0} [{1}]", hostName, hitIP);
                                        }
                                        catch
                                        {
                                            Console.WriteLine("{0}", hitIP);
                                        }
                                    }
                                }
                            }
                        }
                        if ((responsePacket.PacketType == TYPE_ECHO_REPLY) && (numOFRequest == LAST_NUM_OF_REQUESTS))
                        {
                            Console.WriteLine("\nТрассировка завершена.");
                            return;
                        }
                        errorCounter = 0;
                    }
                    catch (SocketException)
                    {
                        Console.Write("*\t");
                        if (numOFRequest == 2)
                        {
                            Console.WriteLine("Превышен интервал ожидания для запроса.");
                        }
                        errorCounter++;
                        if (errorCounter == MAX_ERROR_COUNTER)
                        {
                            Console.WriteLine("Невозможно связаться с удаленным хостом.");
                            return;
                        }
                    }
                }
            }
        }
    }
}
