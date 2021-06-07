using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace updater
{
    class Program
    {
        static void Main(string[] args)
        {


            string path = args[0]; //"IcingaTrayNotify.update";
            string path2 = args[1]; //"IcingaTrayNotify.exe";

            try
            {

                //Убиваем процесс вдруг он еще не закрыт.
                Process[] IcingaTrayNotify = Process.GetProcessesByName(path2);
                if (IcingaTrayNotify.Length > 0)
                {
                    foreach (Process IcingaTray in IcingaTrayNotify)
                    {
                        IcingaTray.Kill();
                    }
                }

                //Ждем 10 секунд пока приложение полностью закроется
                var timeover = true;

                Stopwatch sw = new Stopwatch();
                sw.Start();

                while (timeover)
                {
                    if (sw.ElapsedMilliseconds > 10000)
                        timeover = false;
                }

                if (File.Exists(path))
                {
                    if (File.Exists(path2)) File.Delete(path2);

                    File.Copy(path, path2);
                    File.Delete(path);

                    LogMessageToFile(path + " was moved to " + path2);

                    Process.Start(path2);
                    LogMessageToFile("Process: " + path2 + " was started");
                }

            }
            catch (Exception e)
            {
                LogMessageToFile("The process failed: " + e.ToString());
            }

        }

        static void LogMessageToFile(string msg)
        {
            System.IO.StreamWriter sw = System.IO.File.AppendText("updater-log.txt");
            try
            {
                string logLine = System.String.Format(
                        "{0:G}: {1}.", System.DateTime.Now, msg);
                sw.WriteLine(logLine);
            }
            finally
            {
                sw.Close();
            }
        }




    }
}
