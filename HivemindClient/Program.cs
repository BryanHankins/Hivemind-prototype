// HivemindClient.cs
using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Collections.Generic;

class HivemindClient
{
    const string serverIP = "192.168.12.68";
 // <-- Update this to your server IP
  const int port = 5000;
    static TcpClient? client;
    static NetworkStream? stream;
    static bool running = true;
    static string username = "client" + new Random().Next(1000, 9999);
    static List<string> chatLog = new List<string>();
    static object logLock = new object();

    static void Main()
    {
        Console.CancelKeyPress += (sender, e) => { running = false; client?.Close(); };

        while (running)
        {
            try
            {
                client = new TcpClient();
                client.Connect(serverIP, port);
                stream = client.GetStream();
                AppendToLog($"[+] Connected to Hive as {username}");

                Thread receiveThread = new Thread(ReceiveLoop);
                receiveThread.Start();

                _ = Task.Run(() => RenderUI());

                while (running)
                {
                    string msg = Console.ReadLine();
                    if (msg.ToLower() == "/exit") break;

                    var packet = new
                    {
                        user = username,
                        timestamp = DateTime.UtcNow.ToString("o"),
                        msg
                    };

                    string json = JsonSerializer.Serialize(packet);
                    byte[] data = Encoding.UTF8.GetBytes(json);
                    stream!.Write(data, 0, data.Length);
                    AppendToLog($"[You] {msg}");
                }

                running = false;
                client.Close();
                AppendToLog("[x] Disconnected.");
            }
            catch (Exception ex)
            {
                AppendToLog($"[!] Connection error: {ex.Message}, retrying in 5s...");
                Thread.Sleep(5000);
            }
        }
    }

    static void ReceiveLoop()
    {
        byte[] buffer = new byte[4096];
        while (running)
        {
            try
            {
                int byteCount = stream!.Read(buffer, 0, buffer.Length);
                if (byteCount == 0) break;

                string json = Encoding.UTF8.GetString(buffer, 0, byteCount);
                var message = JsonSerializer.Deserialize<JsonElement>(json);
                string user = message.GetProperty("user").GetString()!;
                string text = message.GetProperty("msg").GetString()!;
                AppendToLog($"[{user}] {text}");
            }
            catch
            {
                AppendToLog("[!] Connection lost.");
                break;
            }
        }
        running = false;
    }

    static void AppendToLog(string message)
    {
        lock (logLock)
        {
            chatLog.Add(message);
            if (chatLog.Count > 20) chatLog.RemoveAt(0);
        }
    }

    static void RenderUI()
    {
        while (running)
        {
            lock (logLock)
            {
                Console.Clear();
                foreach (var line in chatLog)
                {
                    Console.WriteLine(line);
                }
                Console.WriteLine("\n[Type a message and press Enter - /exit to quit]");
            }
            Thread.Sleep(500);
        }
    }
}
