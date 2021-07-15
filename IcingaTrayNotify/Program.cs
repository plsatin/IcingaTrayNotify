using System;
using System.Drawing;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Xml;

namespace IcingaTrayNotify
{
    public class SysTrayApp : Form
    {

        //Что бы запустить без прав администратора нужно выполнить команду:
        //netsh http add urlacl url=http://+:5668/message user=Пользователи

        //Если нужен доступ из сети соответственно необходимо открыть порт 5668 на firewall
        //New-NetFirewallRule -DisplayName "TrayNotify_TCP_5668" -Direction Inbound -LocalPort 5668 -Protocol TCP -Action Allow

        //Запускать при входе всех пользователей: [HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Run]

        //#Простой тест программы из powershell
        //$postParams = @{icon='info';message='Тестовое сообщение из powershell';encoding='none'}
        //$response = Invoke-WebRequest -Uri http://127.0.0.1:5668/message -Method POST -Body $postParams
        //$response.StatusDescription




        HttpListener server;
        bool flag = true;
        


        [STAThread]
        public static void Main()
        {
            Application.Run(new SysTrayApp());
        }

        private NotifyIcon trayIcon;
        //private ContextMenu trayMenu;

        public SysTrayApp()
        {


            trayIcon = new NotifyIcon();
            trayIcon.Text = "Уведомления Icinga2";
            trayIcon.Icon = (Icon)global::IcingaTrayNotify.Properties.Resources.IcingaTrayIcon;
            trayIcon.BalloonTipIcon = ToolTipIcon.Info;


            /*
            // Create a simple tray menu
            trayMenu = new ContextMenu();
            trayMenu.MenuItems.Add("О программе", OnAbout);
            trayMenu.MenuItems.Add("Exit", OnExit);
            
            trayIcon.ContextMenu = trayMenu;
            */


            trayIcon.Visible = true;

           


            string uri = @"http://+:5668/message/";

            StartServer(uri);


        }

        protected override void OnLoad(EventArgs e)
        {
            Visible = false; // Hide form window.
            ShowInTaskbar = false; // Remove from taskbar.

            base.OnLoad(e);
        }

        private void OnExit(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void OnAbout(object sender, EventArgs e)
        {
            trayIcon.BalloonTipText = "Программа оповещение о проблемах";
            trayIcon.BalloonTipTitle = "IcingaTrayNotify";
            trayIcon.BalloonTipIcon = ToolTipIcon.Info;
            trayIcon.ShowBalloonTip(10000);

        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                server.Stop();
                flag = false;

                // Release the icon resource.
                trayIcon.Dispose();
            }

            base.Dispose(isDisposing);
        }


        private void StartServer(string prefix)
        {
            server = new HttpListener();
            // текущая ос не поддерживается
            if (!HttpListener.IsSupported) return;
            //добавление префикса (message/)
            //обязательно в конце должна быть косая черта
            if (string.IsNullOrEmpty(prefix))
                throw new ArgumentException("prefix");
            server.Prefixes.Add(prefix);

            try
            {
                //запускаем север
                server.Start();

                /*
                trayIcon.BalloonTipText = "Сервер уведомлений Icinga2 запущен!";
                trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                trayIcon.BalloonTipTitle = "Уведомления Icinga2";
                trayIcon.ShowBalloonTip(5000);
                */

                //сервер запущен? Тогда слушаем входящие соединения
                while (server.IsListening)
                {
                    //ожидаем входящие запросы
                    HttpListenerContext context = server.GetContext();
                    //получаем входящий запрос
                    HttpListenerRequest request = context.Request;
                    //обрабатываем POST запрос




                    //запрос получен методом POST (пришли данные формы)
                    if (request.HttpMethod == "POST")
                    {
                        //показать, что пришло от клиента
                        

                        ShowRequestData(request);
                        //завершаем работу сервера
                        if (!flag) return;
                    }

                    //формируем ответ сервера:
                    //динамически создаём страницу
                    string responseString = @"<!DOCTYPE HTML>
                        <html>
                        <head>
                        <meta http-equiv=""content-type"" content=""text/html; charset=utf-8"">
                        </head>
                        <body>
                        <form method=""post"" action=""message"">
                        <p><b>Приоритет: </b><br>
                        <select size=""1"" name=""icon"">
                        <option value=""CRITICAL"">CRITICAL</option>
                        <option value=""WARNING"">WARNING</option>
                        <option value=""OK"">OK</option>
                        <option value=""UPDATE"">UPDATE</option>
                        </select></p>
                        <p><b>Сообщение: </b><br>
                        <textarea type=""text"" name=""message"" cols=""40"" rows=""5""></textarea></p>
                        <input type=""text"" name=""encoding"" hidden value=""none"">
                        <p><input type=""submit"" value=""Отправить""></p>
                        </form>
                        </body>
                        </html>";



                               //отправка данных клиенту
                               HttpListenerResponse response = context.Response;
                    response.ContentType = "text/html; charset=UTF-8";
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                    response.ContentLength64 = buffer.Length;
                    using (Stream output = response.OutputStream)
                    {
                        output.Write(buffer, 0, buffer.Length);
                    }
                }

            }
            catch (System.Net.HttpListenerException ex)
            {
                LogMessageToFile(ex.ToString());
                trayIcon.Dispose();
                System.Environment.Exit(2);
            }
            
        }

        public void ShowRequestData(HttpListenerRequest request)
        {
            //есть данные от клиента?
            if (!request.HasEntityBody) return;
            //смотрим, что пришло
            using (Stream body = request.InputStream)
            {
                using (StreamReader reader = new StreamReader(body))
                {
                    string text = reader.ReadToEnd();
                    LogMessageToFile(text);

                    //Разбираем строку POST запроса
                    System.Collections.Generic.Dictionary<string, string> postParams = new System.Collections.Generic.Dictionary<string, string>();
                    string[] rawParams = text.Split('&');
                    foreach (string param in rawParams)
                    {
                        string[] kvPair = param.Split('=');
                        string key = kvPair[0];
                        string value = System.Web.HttpUtility.UrlDecode(kvPair[1]);
                        postParams.Add(key, value);
                    }

                    if (!postParams.ContainsKey("encoding")) {
                        postParams.Add("encoding", "none");
                    }

                    trayIcon.BalloonTipText = text;
                    bool ShowBalloon = true;

                    string message = postParams["message"];

                    if (postParams["icon"] == "WARNING") {

                        trayIcon.BalloonTipIcon = ToolTipIcon.Warning;
                    } else if (postParams["icon"] == "OK") {

                        trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                    } else if (postParams["icon"] == "CRITICAL") {

                        trayIcon.BalloonTipIcon = ToolTipIcon.Error;
                    } else if (postParams["icon"] == "UPDATE") {

                        trayIcon.BalloonTipIcon = ToolTipIcon.Warning;
                        checkUpdates();
                    } else if (postParams["icon"] == "SCREEN") {
                        ShowBalloon = false;
                        trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                        LogMessageToFile("Запрос скриншота");
                        TakeScreenShot(message);
                    } else {
                        trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                    }


                    

                    trayIcon.BalloonTipTitle = "IcingaTrayNotify. Сообщение:";

                    var msgConverted = "";

                    if (postParams["encoding"] == "utf8") {
                        msgConverted = UtfConv(StripHtml(message));
                        trayIcon.BalloonTipText = msgConverted;
                        LogMessageToFile(msgConverted);
                    } else if (postParams["encoding"] == "ascii") {
                        msgConverted = AsciiConv(StripHtml(message));
                        trayIcon.BalloonTipText = msgConverted;
                        LogMessageToFile(msgConverted);
                    } else if (postParams["encoding"] == "none") {
                        msgConverted = StripHtml(message);
                        trayIcon.BalloonTipText = msgConverted;
                        LogMessageToFile(msgConverted);

                    } else {
                        msgConverted = StripHtml(message);
                        trayIcon.BalloonTipText = msgConverted;
                        LogMessageToFile(msgConverted);
                    }




                    if (ShowBalloon) {
                        trayIcon.ShowBalloonTip(100000);

                    } else {
                        LogMessageToFile("Не показали сообщение");
                    }


                    flag = true;


                    /*
                    //останавливаем сервер
                    if (postParams["message"] == "stop")
                    {

                        server.Stop();

                        trayIcon.BalloonTipText = "Сервер уведомлений Icinga2 остановлен!";
                        trayIcon.BalloonTipIcon = ToolTipIcon.Warning;
                        trayIcon.BalloonTipTitle = "IcingaTrayNotify. Сообщение:";
                        trayIcon.ShowBalloonTip(5000);

                        flag = false;
                    }
                    */


                }
            }
        }

        string UtfConv(string strToConvert)
        {
            return System.Text.Encoding.UTF8.GetString(System.Text.Encoding.Default.GetBytes(strToConvert));
        }

        string AsciiConv(string strToConvert)
        {
            return System.Text.Encoding.ASCII.GetString(System.Text.Encoding.Default.GetBytes(strToConvert));
        }

        private string StripHtml(string source)
        {
            string output;

            //get rid of HTML tags
            output = Regex.Replace(source, "<[^>]*>", string.Empty);

            //get rid of multiple blank lines
            output = Regex.Replace(output, @"^\s*$\n", string.Empty, RegexOptions.Multiline);

            return output;
        }

        public void checkUpdates()
        {
            try
            {
                if (File.Exists("IcingaTrayNotify.update") && new Version(FileVersionInfo.GetVersionInfo("IcingaTrayNotify.update").FileVersion) > new Version(Application.ProductVersion))
                {
                    trayIcon.ShowBalloonTip(10000, "IcingaTrayNotify. Обновление программы", "Запуск програиммы updater.exe", ToolTipIcon.Info);
                    LogMessageToFile("Локально обнаружено обновление. Запуск программы updater.exe");

                    Process.Start("updater.exe", "IcingaTrayNotify.update IcingaTrayNotify.exe");
                    //Process.GetCurrentProcess().CloseMainWindow();
                    System.Environment.Exit(2);
                }
                else
                {
                    if (File.Exists("IcingaTrayNotify.update")) { File.Delete("IcingaTrayNotify.update"); }
                    Download();
                }
            }
            catch (Exception)
            {
                if (File.Exists("IcingaTrayNotify.update")) { File.Delete("IcingaTrayNotify.update"); }
                Download();
            }
        }

        private void Download()
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(@"https://icinga2.satin-pl.com/icingatraynotify/version.xml");
                var remoteVersion = new Version(doc.GetElementsByTagName("version")[0].InnerText);
                var localVersion = new Version(Application.ProductVersion);
                if (localVersion < remoteVersion)
                {
                    trayIcon.ShowBalloonTip(10000, "IcingaTrayNotify. Обновление программы", "Локальная версия: " + localVersion.ToString() + " - Удаленная версия: " + remoteVersion.ToString(), ToolTipIcon.Info);
                    LogMessageToFile("Локальная версия: " + localVersion.ToString() + " - Удаленная версия: " + remoteVersion.ToString());
                    if (File.Exists("IcingaTrayNotify.update")) { File.Delete("IcingaTrayNotify.update"); }

                    WebClient client = new WebClient();

                    client.DownloadFile(new Uri(@"https://icinga2.satin-pl.com/icingatraynotify/IcingaTrayNotify.exe"), "IcingaTrayNotify.update");

                    LogMessageToFile("Загружено обновление. Запуск программы updater.exe");

                    Process.Start("updater.exe", "IcingaTrayNotify.update IcingaTrayNotify.exe");
                    //Process.GetCurrentProcess().CloseMainWindow();
                    System.Environment.Exit(2);

                }
            }
            catch (Exception ex)
            {
                LogMessageToFile(ex.ToString());
            }
        }

        static void LogMessageToFile(string msg)
        {
            System.IO.StreamWriter sw = System.IO.File.AppendText("IcingaTrayNotify-log.txt");
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

        private void TakeScreenShot(string screenshotPath)
        {
            Bitmap bmp = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(0, 0, 0, 0, Screen.PrimaryScreen.Bounds.Size);
                if (!String.IsNullOrEmpty(screenshotPath))
                {
                    bmp.Save(screenshotPath);
                } else
                {
                    bmp.Save("C:\\Windows\\Temp\\screenshot.png");
                }
                
            }
        }






    }
}