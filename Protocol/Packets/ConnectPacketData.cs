﻿using System;
using System.Net;
using System.Text;
using Protocol.Util;

namespace Protocol.Packets
{
    public class ConnectPacketData : IPacketData
    {
        public byte AddressTypeByte;   // combine with RouteCount?

        public byte[] DestAddress;

        public ushort DestPort;

        public uint RouteCount;    // must be at least 1. can be greater than the number of routes (in which case, expect another route packet)

        public PacketRoute[] ReturnRoutes;


        public IPAddress DestIpAddress
        {
            get
            {
                if (AddressType == DestAddressType.DomainName || DestAddress == null)
                    return null;
                return new IPAddress(DestAddress);
            }
            set
            {
                DestAddress = value.GetAddressBytes();
                AddressTypeByte = (byte)(DestAddress.Length == 4 ? DestAddressType.IPv4 : DestAddressType.IPv6);
            }
        }

        public string DestDomainName
        {
            get
            {
                if (AddressType != DestAddressType.DomainName || DestAddress == null)
                    return null;
                return Encoding.ASCII.GetString(DestAddress);
            }
            set
            {
                DestAddress = Encoding.ASCII.GetBytes(value);
                AddressTypeByte = (byte)DestAddressType.DomainName;

                if(DestAddress.Length > 255)
                    throw new Exception(string.Format("ConnectPacketData.DestDomainName(set): {0} is greater than 255 bytes (length = {1})", value, DestAddress.Length));
            }
        }

        public DestAddressType AddressType
        {
            get
            {
                return (DestAddressType)AddressTypeByte;
            }
            set
            {
                AddressTypeByte = (byte)value;
            }
        }


        public void Read(ReadBuffer buffer, int length)
        {
            buffer.Read(out AddressTypeByte);
            
            switch (AddressType)
            {
                case DestAddressType.IPv4:
                    DestAddress = buffer.ReadBytes(4);
                    break;
                case DestAddressType.IPv6:
                    DestAddress = buffer.ReadBytes(16);
                    break;
                case DestAddressType.DomainName:
                    byte destLen;
                    buffer.Read(out destLen);
                    var addressBytes = buffer.ReadBytes(destLen);
                    DestDomainName = Encoding.ASCII.GetString(addressBytes);
                    break;
                default:
                    throw new Exception(string.Format("RouteListPacketData.ReadBytes: invalid address type {0}.", AddressTypeByte));
            }

            buffer.Read(out DestPort);
            buffer.Read(out RouteCount);  // total route count

            var remainingLength = length - buffer.TotalRead;

            var routesInPacket = remainingLength / PacketRoute.EncryptedSize;

            if (routesInPacket * PacketRoute.EncryptedSize != remainingLength)
                throw new Exception(string.Format("RouteListPacketData.ReadBytes: remaining length {0} is not a multiple of {1}.", remainingLength, PacketRoute.EncryptedSize));

            ReturnRoutes = new PacketRoute[routesInPacket];

            for (var r = 0; r < routesInPacket; r++)
                ReturnRoutes[r] = new PacketRoute { EncryptedBytes = buffer.ReadBytes(PacketRoute.EncryptedSize) };
        }


        public void Write(WriteBuffer buffer)
        {
            buffer.Write(AddressTypeByte);

            if (AddressType == DestAddressType.DomainName)
                buffer.Write((byte)DestAddress.Length);

            buffer.Write(DestAddress);
            buffer.Write(DestPort);
            buffer.Write(RouteCount);

            foreach (var route in ReturnRoutes)
                buffer.Write(route.EncryptedBytes);
        }
    }
}
