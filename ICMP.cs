using System;
using System.Text;


namespace traceroute
{
    class ICMP
    {
        const int IP_HEADER_SIZE = 20;
        const int ICMP_HEADER_SIZE = 4;
        const int MESSAGE_MAX_SIZE = 65500;
        const int ICMP_IDENTIFIER = 2;
        const int ICMP_SEQUENCE_NUMBER = 2;

        private byte operationCode;
        private UInt16 checksum;
        private int messageSize;
        private byte[] message = new byte[MESSAGE_MAX_SIZE];

        public byte PacketType { get; set; }
        public int PacketSize { get; set; }

        // Конструктор объекта представления пакета ICMP по типу и сообщению
        public ICMP(byte packetype, string _message)
        {
            var messageBytes = Encoding.ASCII.GetBytes(_message);
            PacketType = packetype;
            operationCode = 0;
            Buffer.BlockCopy(messageBytes, 0, message, ICMP_HEADER_SIZE, messageBytes.Length);
            messageSize = messageBytes.Length + ICMP_IDENTIFIER + ICMP_SEQUENCE_NUMBER;
            PacketSize = messageSize + ICMP_HEADER_SIZE;
            checksum = GetChecksum();
        }

        // Конструктор объекта представления пакета ICMP по массиву данных и количеству байт принятых данных
        public ICMP(byte[] data, int responsesize)
        {
            PacketType = data[IP_HEADER_SIZE];    
            operationCode = data[IP_HEADER_SIZE + 1]; 
            checksum = BitConverter.ToUInt16(data, IP_HEADER_SIZE + 2);
            PacketSize = responsesize - IP_HEADER_SIZE;
            messageSize = PacketSize - ICMP_HEADER_SIZE;
            Buffer.BlockCopy(data, IP_HEADER_SIZE + ICMP_HEADER_SIZE, message, 0, messageSize);
        }

        public byte[] ToBytes()
        {
            byte[] data = new byte[PacketSize];
            data[0] = BitConverter.GetBytes(PacketType)[0];
            data[1] = BitConverter.GetBytes(operationCode)[0];
            Buffer.BlockCopy(BitConverter.GetBytes(checksum), 0, data, 2, 2);
            Buffer.BlockCopy(message, 0, data, ICMP_HEADER_SIZE, messageSize);
            return data;
        }

        public UInt16 GetChecksum()
        {
            UInt32 checksum = 0;
            byte[] data = ToBytes();

            for (int i = 0; i < PacketSize; i += 2)
            {
                checksum += Convert.ToUInt32(BitConverter.ToUInt16(data, i));
            }
            checksum = (checksum >> 16) + (checksum & 0xffff);
            checksum += (checksum >> 16);
            return (UInt16)(~checksum);
        }
    }
}