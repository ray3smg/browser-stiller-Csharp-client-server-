using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

class Program
{
    static async Task Main()
    {
        int port = 5555;
        
        TcpListener server = new TcpListener(IPAddress.Any, port);
        server.Start();

        Console.WriteLine($"Сервер запущен на порту {port}");
        Console.WriteLine("Ожидание клиента...");

        while (true)
        {
            TcpClient client = await server.AcceptTcpClientAsync();
            Console.WriteLine("Клиент подключился.");
            _ = Task.Run(() => ReceiveFile(client));
        }
    }

    static async Task ReceiveFile(TcpClient client)
    {
        using (client)
        using (NetworkStream stream = client.GetStream())
        {
            // Генерируем имя файла
            string fileName = $"file_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.zip";
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string filePath = Path.Combine(desktop, fileName);

            Console.WriteLine($"Сохранение в: {filePath}");

            using FileStream file = new FileStream(
                filePath,
                FileMode.Create,
                FileAccess.Write
            );

            byte[] buffer = new byte[1024 * 1024];
            int bytesRead;
            long totalBytes = 0;

            
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await file.WriteAsync(buffer, 0, bytesRead);
                totalBytes += bytesRead;
                Console.Write($" Получено: {totalBytes / (1024.0 * 1024.0):F2} МБ");
            }

            Console.WriteLine();
            Console.WriteLine($"Файл сохранён: {filePath}");
        }
    }
}