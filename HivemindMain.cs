// HivemindMain.cs
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

class HivemindMain
{
    static ConcurrentDictionary<int, TcpClient> clients = new ConcurrentDictionary<int, TcpClient>();
    static TcpListener listener;
    const int port = 5000;
    static int clientCounter = 0;
    static CancellationTokenSource cts = new CancellationTokenSource();
    static List<string> chatLog = new List<string>();
    static object logLock = new object();

    static async Task Main()
    {
        listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Console.WriteLine($"[+] Hive Mind Main Node listening on port {port}");

        _ = Task.Run(() => RenderUI());

        while (!cts.Token.IsCancellationRequested)
        {
            TcpClient client = await listener.AcceptTcpClientAsync();
            int clientId = Interlocked.Increment(ref clientCounter);
            clients[clientId] = client;
            AppendToLog($"[+] Client {clientId} connected");

            _ = Task.Run(() => HandleClientAsync(client, clientId));
        }
    }

    static async Task HandleClientAsync(TcpClient client, int clientId)
    {
        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[4096];

        try
        {
            while (true)
            {
                int byteCount = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (byteCount == 0) break;

                string json = Encoding.UTF8.GetString(buffer, 0, byteCount);
                var msg = JsonSerializer.Deserialize<JsonElement>(json);
                string user = msg.GetProperty("user").GetString();
                string text = msg.GetProperty("msg").GetString();
                AppendToLog($"[{user}] {text}");

                Broadcast(json, clientId);
            }
        }
        catch (Exception ex)
        {
            AppendToLog($"[!] Error with client {clientId}: {ex.Message}");
        }
        finally
        {
            clients.TryRemove(clientId, out _);
            client.Close();
            AppendToLog($"[x] Client {clientId} disconnected");
        }
    }

    static void Broadcast(string jsonMessage, int senderId)
    {
        byte[] data = Encoding.UTF8.GetBytes(jsonMessage);
        foreach (var kvp in clients)
        {
            if (kvp.Key == senderId) continue;

            try
            {
                kvp.Value.GetStream().WriteAsync(data, 0, data.Length);
            }
            catch
            {
                clients.TryRemove(kvp.Key, out _);
            }
        }
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
        while (true)
        {
            lock (logLock)
            {
                Console.Clear();
                foreach (var line in chatLog)
                {
                    Console.WriteLine(line);
                }
                Console.WriteLine("
[SERVER MONITORING - Hivemind]");
            }
            Thread.Sleep(500);
        }
    }
}
