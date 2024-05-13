using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MineStat
{
    /// <summary>
    /// Represents a Minecraft server.
    /// </summary>
    public class MinecraftServer
    {
        /// <summary>
        /// Gets or sets the address of the Minecraft server.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// Gets or sets the port of the Minecraft server.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MinecraftServer"/> class.
        /// </summary>
        /// <param name="address">The address of the Minecraft server.</param>
        /// <param name="port">The port of the Minecraft server. Default is 25565.</param>
        public MinecraftServer(string address, int port = 25565)
        {
            Address = address;
            Port = port;
        }

        /// <summary>
        /// Gets information about the Minecraft server.
        /// </summary>
        /// <returns>A dynamic object containing the server information.</returns>
        public dynamic GetServerInfo()
        {
            using (TcpClient client = new TcpClient(Address, Port))
            {
                client.ReceiveTimeout = 10000; // Set timeout to 10 seconds

                using (NetworkStream stream = client.GetStream())
                {
                    // Send handshake
                    byte[] handshake = { 0x0F, 0x00, 0x04, 0x01, 0x00, 0x63, 0x00, 0x32, 0x00, 0x63, 0x00, 0x00, 0x01 };
                    stream.Write(handshake, 0, handshake.Length);

                    // Send server list ping
                    byte[] ping = { 0x01, 0x00 };
                    stream.Write(ping, 0, ping.Length);

                    // Read response
                    byte[] response = new byte[4096];
                    int bytesRead = stream.Read(response, 0, response.Length);
                    string jsonResponse = Encoding.UTF8.GetString(response, 0, bytesRead);

                    // Parse JSON response
                    dynamic serverInfo = JsonSerializer.Deserialize<dynamic>(jsonResponse);

                    return serverInfo;
                }
            }
        }

        public static async Task<Dictionary<string, object>>  QueryAsync(string serverAddress, int serverPort, int timeout = 10)
        {
            using (TcpClient client = new TcpClient())
            {
                await client.ConnectAsync(serverAddress, serverPort);
                client.ReceiveTimeout = timeout;

                using (NetworkStream stream = client.GetStream())
                using (BinaryWriter writer = new BinaryWriter(stream))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    // Send handshake
                    writer.WriteVarInt(0); // Packet ID = 0
                    writer.WriteVarInt(4); // Protocol version
                    writer.WriteVarInt(serverAddress.Length); // Length of server address
                    writer.Write(Encoding.UTF8.GetBytes(serverAddress)); // Server address
                    writer.Write((ushort)serverPort); // Server port
                    writer.WriteVarInt(1); // Next state: status

                    // Send status request
                    writer.WriteVarInt(1); // Packet ID = 0

                    // Read response
                    int length = reader.ReadVarInt(); // Length of response
                    int packetId = reader.ReadVarInt(); // Packet ID

                    if (packetId != 0)
                    {
                        throw new Exception("Invalid packet ID");
                    }

                    string jsonString = reader.ReadString(); // JSON string

                    // Parse JSON
                    Dictionary<string, object> data = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString);

                    return data;
                }
            }
        }
    }
    public static class BinaryReaderWriterExtensions
    {
        public static void WriteVarInt(this BinaryWriter writer, int value)
        {
            while ((value & -128) != 0)
            {
                writer.Write((byte)(value & 127 | 128));
                value = (int)((uint)value) >> 7;
            }

            writer.Write((byte)value);
        }

        public static int ReadVarInt(this BinaryReader reader)
        {
            int numRead = 0;
            int result = 0;
            byte read;
            do
            {
                read = reader.ReadByte();
                int value = (read & 0b01111111);
                result |= (value << (7 * numRead));

                numRead++;
                if (numRead > 5)
                {
                    throw new InvalidOperationException("VarInt is too big");
                }
            } while ((read & 0b10000000) != 0);

            return result;
        }

        public static string ReadString(this BinaryReader reader)
        {
            int length = reader.ReadVarInt();
            return new string(reader.ReadChars(length));
        }
    }
}
