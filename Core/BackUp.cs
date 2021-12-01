using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;



namespace QBackup
{



    public class BackUp
    {

        [JsonConverter(typeof(StringEnumConverter))]
        public MainTypes Type;
        public string Source;

        public List<BackUpOperation> Operations;

        public static List<BackUp> CreateExample()
        {
            var r = new List<BackUp>();
            var dir = $"{Directory.GetCurrentDirectory()}\\";
            r.Add(new BackUp()
            {
                Type = MainTypes.Backup,
                Source = dir + "example backup\\",
                Operations = new List<BackUpOperation>()
                {
                    new BackUpOperation() {Type = OperationTypes.Compress, Arg1 = dir + "example archive\\", Arg2 = "256", Arg2Comment = "AES encryption key size, can be 128 or 256"},
                    new BackUpOperation() {Type = OperationTypes.CopyFile, Arg1 = dir + "example.json", Arg2 = "overwrite this.json"},
                    new BackUpOperation() {Type = OperationTypes.ValidateAgainstArchive, Arg1 = dir + "archive dir\\"},
                    new BackUpOperation() {Type = OperationTypes.ValidateAgainstBackup, Arg1 = dir + "backup dir\\"}
                }
            });
            r.Add(new BackUp()
            {
                Type = MainTypes.Archive,
                Source = dir + "example archive\\",
                Operations = new List<BackUpOperation>()
                {
                    new BackUpOperation() {Type = OperationTypes.Extract, Arg1 = dir + "dir to extract to\\"},
                    new BackUpOperation() {Type = OperationTypes.VerifyIntegrity, Arg1 = "true", Arg1Comment = "true - check password only, false - check archive integrity as well"}
                }
            });
            return r;
        }

        public static void ExecuteBackup(BackUp b, string password, ILog log)
        {
            var src_dir = new QualifiedPath(b.Source);
            log.WriteLine($"Opening {b.Type} in {src_dir.Path}");
            var sw = new Stopwatch();
            sw.Start();
            try
            {
                switch (b.Type)
                {
                    case MainTypes.Backup:
                        
                        var src = AnalyzedBackup.Create(src_dir, true, log);
                        sw.Stop();
                        log.WriteLine($"Opened in {sw.Elapsed:g}");
                        foreach (var op in b.Operations)
                        {

                            RunOnBackup(op, src, password, log);
                        }
                        break;
                    case MainTypes.Archive:
                        var archive = ArchivedBackup.CreateFromPath(src_dir, password, log);
                        sw.Stop();
                        log.WriteLine($"Opened in {sw.Elapsed:g}");
                        foreach (var op in b.Operations)
                        {
                            RunOnArchive(op, archive, password, log);
                        }
                        break;
                    default:
                        log.WriteLine($"Invalid backup type {b.Type}");
                        break;
                }
            }
            catch (Exception e)
            {
                log.WriteError(e.ToString());
            }
        }

        public static void Run(OpExtract op, ArchivedBackup src, string password, ILog log)
        {
            var dst_dir = new QualifiedPath(op.LocationToExtractTo);
            src.Extract(dst_dir, password, log);
        }

        public static void Run(OpVerify op, ArchivedBackup src, string password, ILog log)
        {
            var check_password_only = op.CheckPasswordOnly;
            src.Verify(password, check_password_only, log);
        }

        public static void RunOnArchive(BackUpOperation op, ArchivedBackup src, string password, ILog log)
        {
            log.WriteLine($"Initiating operation {op.Type} on {src.Root.Path}");
            switch (op.Type)
            {
                case OperationTypes.Extract:
                    var dst_dir = new QualifiedPath(op.Arg1);
                    src.Extract(dst_dir, password, log);
                    break;
                case OperationTypes.VerifyIntegrity:
                    var check_password_only = bool.Parse(op.Arg1);
                    src.Verify(password, check_password_only, log);
                    break;
                default:
                    log.WriteLine($"Invalid operation type {op.Type}");
                    break;
            }
        }

        public static void Run(OpCompress op, AnalyzedBackup src, string password, ILog log)
        {
            var dst_dir = new QualifiedPath(op.LocationToCompressTo);
            var aes_key = op.AES;
            if (aes_key != 128 && aes_key != 256) throw new InvalidOperationException($"Invalid AES key size {aes_key}, expected '128' or '256'");
            var dest_dir = src.GetDestinationDirForCompression(dst_dir);
            if (Directory.Exists(dest_dir))
            {
                var dst = ArchivedBackup.CreateFromPath(dest_dir, password, log);
                var diff = BackupDiff.Create(src, dst);
                diff.Compare(log);
                diff.Commit(password, aes_key, log);
            }
            else src.Compress(dst_dir, password, aes_key, log);
        }

        public static void Run(OpCopy op, AnalyzedBackup src, ILog log)
        {
            var file = System.IO.File.ReadAllText(op.FileToCopy);
            var name = op.NameOfFileInBackup;
            var count = 0;
            foreach (var f in src.Files)
            {
                if (f.Name == name)
                {
                    System.IO.File.WriteAllText(f.GetAbsolutePath(), file);
                    log.WriteLine($"Overwritten (count {++count}) {f.GetAbsolutePath()} with {op.FileToCopy}");
                }
            }
        }

        public static void Run(OpValidateAgainstArchive op, AnalyzedBackup src, string password, ILog log)
        {
            var arch = ArchivedBackup.CreateFromPath(op.ArchiveLocation, password, log);
            Validator.Validate(src, arch, log, false, false, true);
        }

        public static void Run(OpValidateAgainstBackup op, AnalyzedBackup src, ILog log)
        {
            var b2 = AnalyzedBackup.Create(op.BackupLocation, true, log);
            Validator.Validate(src, b2, log, false, false, true);
        }

        public static void RunOnBackup(BackUpOperation op, AnalyzedBackup src, string password, ILog log)
        {
            log.WriteLine($"Initiating operation {op.Type} on {src.Root.Path}");
            switch (op.Type)
            {
                case OperationTypes.Compress:
                    var dst_dir = new QualifiedPath(op.Arg1);
                    var aes_key = int.Parse(op.Arg2);
                    if (aes_key != 128 && aes_key != 256) throw new InvalidOperationException($"Invalid AES key size {aes_key}, expected '128' or '256'");
                    var dest_dir = src.GetDestinationDirForCompression(dst_dir);
                    if (Directory.Exists(dest_dir))
                    {
                        var dst = ArchivedBackup.CreateFromPath(dest_dir, password, log);
                        var diff = BackupDiff.Create(src, dst);
                        diff.Compare(log);
                        diff.Commit(password, aes_key, log);
                    }
                    else src.Compress(dst_dir, password, aes_key, log);
                    break;
                case OperationTypes.CopyFile:
                    var file = System.IO.File.ReadAllText(op.Arg1);
                    var name = op.Arg2;
                    var count = 0;
                    foreach (var f in src.Files)
                    {
                        if (f.Name == name)
                        {
                            System.IO.File.WriteAllText(f.GetAbsolutePath(), file);
                            log.WriteLine($"Overwritten (count {++count}) {f.GetAbsolutePath()} with {op.Arg1}");
                        }
                    }
                    break;
                case OperationTypes.ValidateAgainstArchive:
                    var arch = ArchivedBackup.CreateFromPath(op.Arg1, password, log);
                    Validator.Validate(src, arch, log, false, false, true);
                    break;
                case OperationTypes.ValidateAgainstBackup:
                    var b2 = AnalyzedBackup.Create(op.Arg1, true, log);
                    Validator.Validate(src, b2, log, false, false, true);
                    break;
                default:
                    log.WriteLine($"Invalid operation type {op.Type}");
                    break;
            }
        }

    }



    public class BackUpOperation
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public OperationTypes Type { get; set; }
        public string Arg1 { get; set; }
        public string Arg1Comment { get; set; }
        public string Arg2 { get; set; }
        public string Arg2Comment { get; set; }

    }



    public enum MainTypes
    {
        Backup,
        Archive
    }

    public enum OperationTypes
    {
        Compress,
        CopyFile,
        ValidateAgainstArchive,
        ValidateAgainstBackup,
        Extract,
        VerifyIntegrity
    }



    public enum ArchiveTypes
    {
        Extract,
        VerifyIntegrity
    }



    public class Op
    {



    }

    public class OpBackup
    {

        [JsonConverter(typeof(StringEnumConverter))]
        public OperationTypes Type { get; set; }

        public string LocationToBackup { get; set; }

    }

    public class OpCompress : OpBackup
    {

        public string LocationToCompressTo { get; set; }
        public int AES { get; set; }

    }



    public class OpCopy : OpBackup
    {

        public string FileToCopy { get; set; }
        public string NameOfFileInBackup { get; set; }

    }

    public class OpValidateAgainstArchive : OpBackup
    {

        public string ArchiveLocation { get; set; }

    }

    public class OpValidateAgainstBackup : OpBackup
    {

        public string BackupLocation { get; set; }

    }



    public class OpArchive
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public ArchiveTypes Type { get; set; }

        public string ArchiveLocation { get; set; }

    }

    public class OpExtract : OpArchive
    {

        public string LocationToExtractTo { get; set; }

    }

    public class OpVerify : OpArchive
    {

        public bool CheckPasswordOnly { get; set; }

    }
}