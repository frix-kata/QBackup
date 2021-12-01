using System;
using System.Collections.Generic;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;



namespace QBackup
{

    class Program
    {

        static void Main(string[] args)
        {
            var log = new LogConsole();
            var example_loc = "example.json";
            System.IO.File.WriteAllText($"{Directory.GetCurrentDirectory()}\\{example_loc}", JsonConvert.SerializeObject(BackUp.CreateExample()));
            Console.WriteLine($"Enter name of file that contains operations to be performed (relative to this path, example has been generated at {example_loc}):");
            var backup_file = Console.ReadLine();
            var backups_file = $"{Directory.GetCurrentDirectory()}\\{backup_file}";
            if (!System.IO.File.Exists(backups_file))
            {
                log.WriteLine($"File with data on what to backup not found in : {backups_file}");
                return;
            }
            var backups_json = System.IO.File.ReadAllText(backups_file);
            var backups = JsonConvert.DeserializeObject<List<BackUp>>(backups_json);
            Console.WriteLine("Enter password:");
            var password = Console.ReadLine();
            ZipStrings.UseUnicode = true;
            foreach (var back_up in backups)
            {
                BackUp.ExecuteBackup(back_up, password, log);
            }
        }

    }
}
