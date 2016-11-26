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

            int numThreads = 1;
            for (int x=0; x < numThreads; x++)
            {
                //Task.Factory.StartNew(CopyFileDiskToDiskThread, TaskCreationOptions.LongRunning);
                Task.Factory.StartNew(CopyFileFromMemoryThread, TaskCreationOptions.LongRunning);
            }


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


                    //CopyFileNormal(file);
                    CopyFileViaStream(file);
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

        static void CopyFileFromMemoryThread()
        {
            while (true)
            {
                Tuple<string, byte[]> file;

                if (MemoryToDiskQueue.TryDequeue(out file))
                {
                    try
                    {
                        string filename = file.Item1;
                        string desFile = filename.Replace(SrcRoot, DesRoot);

                        using (MemoryStream memory = new MemoryStream(file.Item2))
                        using (FileStream fileStream = File.OpenWrite(desFile))
                        {
                            memory.CopyTo(fileStream, Mb * 4);
                        }

                        //File.WriteAllBytes(desFile, file.Item2);


                        Console.WriteLine(filename);
                    }
                    catch (Exception e)
                    {
                        ErrorFile.WriteLine(file + "," + e.Message);
                        NumErrors++;
                    }
                    finally
                    {
                        FilesBeingCopied--;
                    }

                }
                else
                {
                    Console.WriteLine(Watch.Elapsed + ": NO FILE IN QUEUE");
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
            }
        }

        static void CopyFileDiskToDiskThread()
        {
            while (true)
            {
                string filename;

                if (DiskToDiskQueue.TryDequeue(out filename))
                {
                    try
                    {
                        string desFile = filename.Replace(SrcRoot, DesRoot);
                        File.Copy(filename, desFile, true);
                        Console.WriteLine(filename);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        ErrorFile.WriteLine(filename + "," + e.Message);
                        NumErrors++;
                    }
                    finally
                    {
                        FilesBeingCopied--;
                    }
                }
                else
                {
                    Console.WriteLine(Watch.Elapsed + "NO FILE IN QUEUE");
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
            }
        }

        static void CopyFileNormal(string file)
        {
            try
            {
                string desFile = file.Replace(SrcRoot, DesRoot);
                File.Copy(file, desFile, true);
                Console.WriteLine(file);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                ErrorFile.WriteLine(file + "," + e.Message);
                NumErrors++;
            }
            finally
            {
                FilesBeingCopied--;
            }
        }

        static void CopyFileViaStream(string file)
        {
            try
            {
                string desFile = file.Replace(SrcRoot, DesRoot);

                using (FileStream srcStream = File.Open(file, FileMode.Open, FileAccess.Read))
                {
                    using (FileStream desStream = File.Open(desFile, FileMode.Create, FileAccess.Write))
                    {
                        //Console.WriteLine("Starting..." + file);

                        srcStream.CopyTo(desStream, 20 * Mb);
                        Console.WriteLine(file);

                        //double fileMb = fileSize / 1024.0 / 1024.0;
                        //double totalGb = TotalSize / 1024.0 / 1024.0 / 1024.0;

                        //double speedBytes = TotalSize / Watch.Elapsed.TotalSeconds;
                        //double speedMb = speedBytes / 1024.0 / 1024.0;

                        ////string line = Watch.Elapsed.Hours + "h " + Watch.Elapsed.Minutes + "m " + Watch.Elapsed.Seconds + "s" + ", " + NumFiles + " files, " + totalGb.ToString("0.000") + " Gb transferred - " + fileMb.ToString("0.000") + " Mb, " + file;
                        //string line = Watch.Elapsed.Hours + "h " + Watch.Elapsed.Minutes + "m " + Watch.Elapsed.Seconds + "s" + ", " +
                        //    NumFiles + ", " + totalGb.ToString("0.000") + " Gb, " + speedMb.ToString("0.0") + " MB/s | " + fileMb.ToString("0.000") + " Mb, " + file;

                        //Console.WriteLine(line);

                    }
                }
            }
            catch (Exception e)
            {
                //Console.WriteLine("Errore.Message);
                ErrorFile.WriteLine(file + "," + e.Message);
                NumErrors++;
            }
            finally
            {
                FilesBeingCopied--;
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

                lock (Lock)
                {
                    FilesBeingCopied--;
                    NumErrors++;
                }

            }
        }
    }
}
