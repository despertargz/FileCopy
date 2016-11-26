using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ArchiveFiles
{
    class Program
    {
        #region Data

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

        static ConcurrentQueue<string> DiskToDiskQueue = new ConcurrentQueue<string>();

        static ConcurrentQueue<Tuple<string, byte[]>> MemoryToDiskQueue = new ConcurrentQueue<Tuple<string, byte[]>>();

        //static long MemoryToDiskQueueSize;

        static int Kb = 1024;

        static int Mb = 1024 * 1024;

        static FileStream Archive;

        #endregion

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

                int numThreads = 3;
                for (int x = 0; x < numThreads; x++)
                {
                    //Task.Factory.StartNew(CopyFileDiskToDiskThread, TaskCreationOptions.LongRunning);
                    //Task.Factory.StartNew(CopyFileFromMemoryThread, TaskCreationOptions.LongRunning);
                }


                using (ErrorFile = new StreamWriter("errors.txt"))
                using (SuccessFile = new StreamWriter("completed.txt"))
                using (Ar)
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
                    " MB/s, Elapsed:" + Watch.Elapsed + ", Errors: " + NumErrors + ": " + SrcRoot + " -> " + DesRoot + "\r\n";
                Console.Write(text);
                File.AppendAllText("session.txt", text);

                Console.WriteLine("Done!");
            }

            static void CopyDir(string currentDir)
            {
                string desDir = currentDir.Replace(SrcRoot, DesRoot);
                //Console.WriteLine("Creating Dir: " + desDir);
                Directory.CreateDirectory(desDir);

                try
                {
                    foreach (string file in Directory.EnumerateFiles(currentDir))
                    {
                        long fileSize = new FileInfo(file).Length;
                        FilesBeingCopied++;
                        ++NumFiles;
                        TotalSize += fileSize;


                        CopyFileNormal(file);
                        //CopyFileViaStream(file);
                        //MemoryToDiskQueue.Enqueue(Tuple.Create(file, File.ReadAllBytes(file)));
                        //DiskToDiskQueue.Enqueue(file);

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

}
}
