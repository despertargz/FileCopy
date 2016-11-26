using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CopyFiles
{
    class Program
    {
        static StreamWriter ErrorFile;

        static StreamWriter SuccessFile;

        static string SrcRoot;

        static string DesRoot;

        static long NumFiles;

        static long TotalSize;

        static long NumErrors;

        static List<Task> Tasks = new List<Task>();

        static object Lock = new object();

        static Stopwatch Watch;

        static long FilesBeingCopied;

        static ConcurrentQueue<Tuple<string, byte[]>> Queue = new ConcurrentQueue<Tuple<string, byte[]>>();

        static void Main(string[] args)
        {
            //SrcRoot  = args[0];
            //DesRoot = args[1];
            string[] targets = File.ReadAllLines("targets.txt");
            SrcRoot = targets[0].Trim();
            DesRoot = targets[1].Trim();

            if (!SrcRoot.EndsWith("\\"))
            {
                SrcRoot += "\\";
            }

            if (!DesRoot.EndsWith("\\"))
            {
                DesRoot += "\\";
            }

            Console.WriteLine("Copying " + SrcRoot + " -> " + DesRoot);
            Console.WriteLine("Press Enter to begin copy:");
            Console.ReadLine();

            File.AppendAllText("session.txt", DateTime.Now + ": Started: " + SrcRoot + " -> " + DesRoot + "\r\n");
            Watch = Stopwatch.StartNew();

            using (ErrorFile = new StreamWriter("errors.txt"))
            using (SuccessFile = new StreamWriter("completed.txt"))
            {
                CopyDir(SrcRoot);
            }

            Console.WriteLine("Waiting for all files to finish...");
            while (FilesBeingCopied != 0)
            {
                Thread.Sleep(TimeSpan.FromSeconds(30));
            }

            //Task.WaitAll(Tasks.ToArray());

            double speedBytes = TotalSize / Watch.Elapsed.TotalSeconds;
            double speedMb = speedBytes / 1024.0 / 1024.0;

            double gb = TotalSize / 1024.0 / 1024.0 / 1024.0;
            string text = DateTime.Now + ": Completed: " + gb.ToString("0.000") + " Gb, " + TotalSize + " Bytes, " + NumFiles + " files, Speed: " + speedMb.ToString("0.0") +
                " MB/s, Elapsed:"  + Watch.Elapsed + ", Errors: " + NumErrors + ": " + SrcRoot + " -> " + DesRoot + "\r\n";
            Console.Write(text);
            File.AppendAllText("session.txt", text);

            Console.WriteLine("Done!");
        }

        static void CopyFile(string file)
        {
            try
            {
                string desFile = file.Replace(SrcRoot, DesRoot);

                using (FileStream srcStream = File.Open(file, FileMode.Open, FileAccess.Read))
                {
                    using (FileStream desStream = File.Open(desFile, FileMode.Create, FileAccess.Write))
                    {
                        //Console.WriteLine("Starting..." + file);

                        FilesBeingCopied++;
                        long fileSize = new FileInfo(file).Length;
                        srcStream.CopyTo(desStream);

                        ++NumFiles;
                        TotalSize += fileSize;
                        FilesBeingCopied--;
                        
                        double fileMb = fileSize / 1024.0 / 1024.0;
                        double totalGb = TotalSize / 1024.0 / 1024.0 / 1024.0;

                        double speedBytes = TotalSize / Watch.Elapsed.TotalSeconds;
                        double speedMb = speedBytes / 1024.0 / 1024.0;

                        //string line = Watch.Elapsed.Hours + "h " + Watch.Elapsed.Minutes + "m " + Watch.Elapsed.Seconds + "s" + ", " + NumFiles + " files, " + totalGb.ToString("0.000") + " Gb transferred - " + fileMb.ToString("0.000") + " Mb, " + file;
                        string line = Watch.Elapsed.Hours + "h " + Watch.Elapsed.Minutes + "m " + Watch.Elapsed.Seconds + "s" + ", " +
                            NumFiles + ", " + totalGb.ToString("0.000") + " Gb, " + speedMb.ToString("0.0") + " MB/s | " + fileMb.ToString("0.000") + " Mb, " + file;

                        Console.WriteLine(line);

                    }
                }
            }
            catch (Exception e)
            {
                //Console.WriteLine("Errore.Message);
                ErrorFile.WriteLine(file + "," + e.Message);
                NumErrors++;
            }
        }


        static async Task CopyFileAsync(string file)
        {
            try
            {
                FilesBeingCopied++;
                string desFile = file.Replace(SrcRoot, DesRoot);

                using (FileStream srcStream = File.Open(file, FileMode.Open, FileAccess.Read))
                {
                    using (FileStream desStream = File.Open(desFile, FileMode.Create, FileAccess.Write))
                    {
                        //Console.WriteLine("Starting..." + file);

                        long fileSize = new FileInfo(file).Length;
                        await srcStream.CopyToAsync(desStream);

                        lock (Lock)
                        {
                            ++NumFiles;
                            TotalSize += fileSize;
                            FilesBeingCopied--;
                        }

                        double fileMb = fileSize / 1024.0 / 1024.0;
                        double totalGb = TotalSize / 1024.0 / 1024.0 / 1024.0;

                        double speedBytes = TotalSize / Watch.Elapsed.TotalSeconds;
                        double speedMb = speedBytes / 1024.0 / 1024.0;

                        //string line = Watch.Elapsed.Hours + "h " + Watch.Elapsed.Minutes + "m " + Watch.Elapsed.Seconds + "s" + ", " + NumFiles + " files, " + totalGb.ToString("0.000") + " Gb transferred - " + fileMb.ToString("0.000") + " Mb, " + file;
                        string line = Watch.Elapsed.Hours + "h " + Watch.Elapsed.Minutes + "m " + Watch.Elapsed.Seconds + "s" + ", " +
                            NumFiles + ", " + totalGb.ToString("0.000") + " Gb, " + speedMb.ToString("0.0") + " MB/s | " + fileMb.ToString("0.000") + " Mb, " + file;

                        Console.WriteLine(line);

                        //SuccessFile.WriteLine(line);
                        
                    }
                }
            }
            catch (Exception e)
            {
                //Console.WriteLine("Errore.Message);
                ErrorFile.WriteLine(file + "," + e.Message);

                lock(Lock)
                {
                    FilesBeingCopied--;
                    NumErrors++;
                }
                
            }
        }

        static void CopyDir(string currentDir)
        {
            string desDir = currentDir.Replace(SrcRoot, DesRoot);
            //Console.WriteLine("Creating Dir: " + desDir);
            Directory.CreateDirectory(desDir);

            try
            {
                foreach (var file in Directory.EnumerateFiles(currentDir))
                {
                    CopyFile(file);



                    //var task = CopyFileAsync(file);
                    //Tasks.Add(task);
                }


                foreach (string dir in Directory.EnumerateDirectories(currentDir))
                {
                    CopyDir(dir);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error Dir: " + currentDir + ": " + e.Message);
                ErrorFile.WriteLine("Error Dir: " + currentDir + ": " + e.Message);
            }
        }
    }
}
