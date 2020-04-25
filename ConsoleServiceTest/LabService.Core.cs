using NationalInstruments.Analysis.SignalGeneration;
using NationalInstruments.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
//using NationalInstruments.DAQmx.ComponentModel;
using Microsoft.Owin.Hosting;
using System.Xml.Linq;
using System.Security.Permissions;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Security.Principal;
using System.Net.NetworkInformation;
using System.IO.Pipes;

namespace LabServiceLibrary
{
    public partial class LabService: IDisposable
    {
        public delegate void LabServiceEventHandler(object sender, LabServiceEventArgs e);

        public event LabServiceEventHandler LabServiceStarted;
        public event LabServiceEventHandler LabServiceStopped;

        private System.ComponentModel.IContainer components = null;
        //private NationalInstruments.DAQmx.Task myTask;
        private DataSocketServer dataSocketServer;
        

        //public static System.Threading.ManualResetEvent allDone = 
        //new System.Threading.ManualResetEvent(false);  

        // Command file watcher
        FileSystemWatcher commandWatcher;
        
        


        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        //protected void Dispose(bool disposing)
        //{
        //    //components.Dispose();
        //    DisposeLab();
        //}

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            // Setup dataSocketServer
            this.dataSocketServer = new DataSocketServer(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.dataSocketServer)).BeginInit(); 
           
            ((System.ComponentModel.ISupportInitialize)(this.dataSocketServer)).EndInit();

   
            AppendLog("Components initialized");
        }

        public static string GenerateWave(string sourceDevice = "Dev1", double freq = 100, double amp = 2,
                         string waveType = "sine")
        {
            return "OK";
        }

        private void CreateScopeTask(string device, List<Channel> channels,
            int samplesPerChannel = 1000,
            int rate = 10000,  //sample rate
            double minimumValue = -10,
            double maximumValue = 10)
        {

           
            AppendLog("Scope Task Created");
        }


        public static string TurnSwitch(int line, int value, string sourceDevice = "Dev1")
        {

            return "OK";
        }

        private void StartWebServer()
        {
            if (PortInUse(APIPort))  //default port used by another app
            {
                for (int i = 0; i <= 10; i++) // try other ports 9000 - 9010
                {
                    if (!PortInUse(9000+i))
                    {
                        APIPort = 9000 + i;
                        break;
                    }
                }
                LoadParamConfig(true);
            }
            string baseAddress = "http://*:" + APIPort.ToString() + "/";
            if (IsAdministrator())
            {
                // Start OWIN host 
                apiServer = WebApp.Start<Startup>(baseAddress);
            }

            else
            {
                MessageBox.Show("Api Server not Started\nYou are not admin", "Permisiion Error");

            }
            
            
        }

        private void LogStatus(string labName, string state, string message){
            XDocument statusDoc =
                new XDocument(
                    new XDeclaration("1.0", "utf-8", "yes"),
                    new XComment("The xml data required for starting the lab!"),
                    new XElement("LabStatus",
                        new XElement("LabName", labName),
                        new XElement("State", state),
                        new XElement("Message", message)
                        )
                    );
            if (File.Exists(System.IO.Path.Combine(workingPath, configFile)))
            {
                File.Delete(System.IO.Path.Combine(workingPath, configFile));
            }
            using (var fileStream = new FileStream(System.IO.Path.Combine(workingPath, configFile),
                    FileMode.Create, FileAccess.Write))
            {
                statusDoc.Save(fileStream);
                
            }
              
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        private void WatchCmdFile()
        {

            // Create a new FileSystemWatcher and set its properties.
            //using(commandWatcher = new FileSystemWatcher())
            //{
                commandWatcher.Path = workingPath;

                // Watch for changes in LastAccess and LastWrite times, and
                // the renaming of files or directories.
                commandWatcher.NotifyFilter = NotifyFilters.LastWrite;
                //| NotifyFilters.LastAccess;
                // | NotifyFilters.FileName
                // | NotifyFilters.DirectoryName;

                // Only watch file below
                commandWatcher.Filter = "cmd.config";


                // Add event handlers.
                commandWatcher.Changed += OnChanged;
                //watcher.Created += OnChanged;
                //watcher.Deleted += OnChanged;
                //watcher.Renamed += OnRenamed;

                // Begin watching.
                commandWatcher.EnableRaisingEvents = true;
            //}
      
         }
        

        // Define the event handler for changed event.
        private void OnChanged(object source, FileSystemEventArgs e)
        {
            //MessageBox.Show("Updated");
            // Ensure Only one event is handled for the file changed
            commandWatcher.EnableRaisingEvents = false;
            System.Timers.Timer t = new System.Timers.Timer();

            t.Interval = 1000;
            t.Enabled = true;
            t.Elapsed += (object sender, System.Timers.ElapsedEventArgs ev) =>
                       {
                           ((System.Timers.Timer)sender).Stop();
                           // Restore event handler
                           commandWatcher.EnableRaisingEvents = true;                               
                       };
            ProcessCommand();
        }

        public void ProcessCommand()
        {
            //Todo, update config file and restart. / Stop and start again
            try
            {
                using (FileStream fs = new FileStream(Path.Combine(workingPath, "cmd.config"),
                    FileMode.Open, FileAccess.Read))
                {

                    XDocument cmdFile = XDocument.Load(fs); //load xml from command file
                    string labName = (from dev in cmdFile.Descendants("LabName")
                                      select (string)dev.Value).FirstOrDefault();
                    string labState = (from dev in cmdFile.Descendants("State")
                                       select (string)dev.Value).FirstOrDefault();

                    if (labState.Equals("Stop"))
                    {
                        
                        if (LabServiceStopped != null)
                        {
                            LabServiceStopped(this, new LabServiceEventArgs("Lab Stopped"));
                        }
                    }
                    else if (labState.Equals("Run"))
                    {
                        LogStatus(labName, "Running", "Lab Started");
                        if (LabServiceStopped != null)
                        {
                            LabServiceStopped(this, new LabServiceEventArgs("Lab Stopped"));
                        }
                        LogStatus(labName, "Running", "Start Lab");
                        if (LabServiceStarted != null)
                        {
                            DeleteLog();
                            LabServiceStarted(this, new LabServiceEventArgs("Lab Started"));
                        }
                    }
                    else if (labState.Equals("Change"))
                    {
                        //MessageBox.Show("Changing Lab");
                        //Stop();
                        //LogStatus(labName, "Runnning", "Lab Changed");
                        //Start();
                    }

                }
                
                
            }
            catch (Exception ex)
            {
                AppendLog("Service file load exception: "+ ex.Message);
            }
            
        }

        public void Dispose()
        {
            DisposeLab();
        }

        private void AppendLog(string logMessage)
        {
            try
            {
                using (StreamWriter w = File.AppendText(Path.Combine(workingPath, "log.txt")))
                {
                    w.Write("\r\nLog Entry : ");
                    w.WriteLine(string.Format("{0} {1}", DateTime.Now.ToLongTimeString(), DateTime.Now.ToLongDateString()));
                    w.WriteLine("  :");
                    w.WriteLine(string.Format("  :{0}", logMessage));
                    w.WriteLine("-------------------------------");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
           
        }

        private bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public bool PortInUse(int port)
        {
            bool inUse = false;
            IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] ipEndPoints = ipProperties.GetActiveTcpListeners();
            foreach (IPEndPoint endPoint in ipEndPoints)
            {
                if (endPoint.Port == port)
                {
                    inUse = true;
                    break;
                }
            }
            return inUse;
        }

        public void DeleteLog()
        {
            if (File.Exists(Path.Combine(workingPath, "log.txt"))) // Delete Log and Start again
            {
                File.Delete(Path.Combine(workingPath, "log.txt"));
            }
        }

        private void StartPipeServer()
        {
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                var server = new NamedPipeServerStream("iLabServicePipe");
                server.WaitForConnection();
                StreamReader reader = new StreamReader(server);
                StreamWriter writer = new StreamWriter(server);
                while (true)
                {
                    var line = reader.ReadLine();
                    writer.WriteLine(String.Join("", line.Reverse()));
                    writer.Flush();
                }
            });
        }
    
     }

    public class LabServiceEventArgs : EventArgs
    {
        public readonly string Message;
        public LabServiceEventArgs(string msg)
        {
            Message = msg;
        }
    }
}

/**
 * Enabling Port Using netsh
 * 
 netsh advfirewall firewall add rule name="Open 
       Port 80" dir=in action=allow protocol=TCP localport=80
 * 
 * =>netsh advfirewall firewall add rule name="LabService" 
    dir=in action=allow protocol=TCP localport=9000
 * Enabling app 
 * 
 * 
 * 
 * checking if admin
    using System.Security.Principal;

    public static bool IsAdministrator()
    {
        using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
        {
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
 * 
 * Task kill =>taskkill /pid 8680 /f
 * 
 * 
 */