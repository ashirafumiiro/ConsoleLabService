using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using NationalInstruments.Net;
using System.Windows.Forms;
using System.Media;
using System.IO;
using System.Xml.Linq;
using Topshelf;
using System.Reflection;

namespace LabServiceLibrary
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "Service1" in both code and config file together.
    public partial class LabService : ServiceControl
    {
       
        //int numberOfChannels = 2;
        System.Timers.Timer timer;
        private string selectedLab = "None";

        // parameters
        private int samplesPerChannel = 1000;
        private int rate = 10000;  //sample rate
        private double minimumValue = -10;
        private double maximumValue = 10;
        private int APIPort = 9000; //default port is 9000
        private int timerInterval = 500;  //timer interval in milliseconds

        string workingPath;
        string labsDir;
        string configFile = "current.config";
        IDisposable apiServer;
        bool isRunning = false;
        

        public LabService()
        {
            
            var uri = new Uri(Assembly.GetExecutingAssembly().GetName().CodeBase);
            string execPath = Path.GetDirectoryName(uri.LocalPath);

            workingPath = System.IO.Path.Combine(execPath, "LabServer");
            labsDir = System.IO.Path.Combine(execPath, "LabServer", "Labs");

            if (!Directory.Exists(workingPath))
            {   
                try 
	            {
                    Directory.CreateDirectory(workingPath);
	            }
	            catch (Exception)
	            {
	            	Console.WriteLine("Failed to create working directory");
	            }
                
            }
            LabServiceStarted += LabService_LabServiceStarted;
            LabServiceStopped += LabService_LabServiceStopped;
            // enitialise time
            timer = new System.Timers.Timer();
            timer.Interval =timerInterval; // in milli seconds
            timer.Elapsed += new System.Timers.ElapsedEventHandler(this.OnTimer);
            timer.Enabled = false;
            commandWatcher = new FileSystemWatcher();
                   
            
        }

        void LabService_LabServiceStopped(object sender, LabServiceEventArgs e)
        {
            timer.Enabled = false;
            if (apiServer != null)
            {
                apiServer.Dispose();
            }

            DisposeLab();
            LogStatus(selectedLab, "Stopped", "Lab Stopped");
        }

        void LabService_LabServiceStarted(object sender, LabServiceEventArgs e)
        {

            //AppendLog("Starting......");
            LoadParamConfig();
            AppendLog("Attempting Start");
            if (CreatedComponents())
            {
                AppendLog("Started lab successfully");
                StartWebServer();   //testing purpose
                timer.Enabled = true;
                isRunning = true;
            }
            else
            {
                LogStatus(selectedLab, "Stopped", "Failed to Start " + selectedLab + " in Service\nCheck log");
                AppendLog("Failed to start lab in start service");
            }
        }

        public bool CreatedComponents()
        {
            InitializeComponent();      

            return true;          
        }


        public bool Start(HostControl hostControl)
        {
            hostControl.RequestAdditionalTime(TimeSpan.FromSeconds(30));
            WatchCmdFile();
            if (LabServiceStarted != null)
            {
                LabServiceStarted(this, new LabServiceEventArgs("Lab Starting"));
            }
            timer.Enabled = true;
            return true;
        }

        public bool Stop(HostControl hostControl)
        {
            hostControl.RequestAdditionalTime(TimeSpan.FromSeconds(60));
            if (LabServiceStopped != null)
            {
                LabServiceStopped(this, new LabServiceEventArgs("Lab Stopped"));
            }
            return true;
        }

        public void OnTimer(object sender, System.Timers.ElapsedEventArgs args)
        {
            
        }

        public void DisposeLab()
        {
            try
            {
                dataSocketServer.Shutdown();
                dataSocketServer.Dispose();
                dataSocketServer = null;
                components = null;
                if (apiServer != null)
                {
                    apiServer.Dispose();
                }
            }
            catch (Exception ex)
            {
                AppendLog("Lab Dispose error: " + ex.Message);
            }
            
        }

        public bool LoadCurrentFile(string path, string labName)
        {
            return true;           
        }

        private void LoadParamConfig(bool update=false)
        {
            string paramFile = Path.Combine(workingPath, "defaults.config");
            if (File.Exists(paramFile))
            {
                //private int samplesPerChannel = 1000;
                //private int rate = 10000;  //sample rate
                //private double minimumValue = -10;
                //private double maximumValue = 10;
                //private int APIPort = 9000; //default port is 9000
                int samples = 0, rat = 0, port=0, tInterval=0;
                double minVal = 0, maxVal = 0;

                XDocument paramDoc = XDocument.Load(paramFile);

                string samp = (from param in paramDoc.Descendants("SamplesPerChannel")
                                     select (string)param.Value).FirstOrDefault();
                if (int.TryParse(samp, out samples))
                {
                    this.samplesPerChannel = samples;
                }
                
                string rt = (from param in paramDoc.Descendants("Rate")
                                  select (string)param.Value).FirstOrDefault();
                if (int.TryParse(rt, out rat))
                {
                    this.rate = rat;
                }

                string min = (from param in paramDoc.Descendants("MinimumValue")
                                 select (string)param.Value).FirstOrDefault();
                if (double.TryParse(min, out minVal))
                {
                    this.minimumValue = minVal;
                }

                string max = (from param in paramDoc.Descendants("MaximumValue")
                                 select (string)param.Value).FirstOrDefault();

                if (double.TryParse(max, out maxVal))
                {
                    this.maximumValue = maxVal;
                }

                string pt = (from param in paramDoc.Descendants("APIPort")
                                 select (string)param.Value).FirstOrDefault();
                if (int.TryParse(pt, out port))
                {
                    this.APIPort = port;
                }

                string interval = (from param in paramDoc.Descendants("TimerInterval")
                             select (string)param.Value).FirstOrDefault();
                if (int.TryParse(pt, out tInterval))
                {
                    this.timerInterval = tInterval;
                }  

            }
            else if (update)
            {
                SaveParams();
            }
            else
            {
                SaveParams();
            }
        }

        private void SaveParams()
        {
            string paramFile = Path.Combine(workingPath, "defaults.config");
            XDocument parameters =
                new XDocument(
                    new XDeclaration("1.0", "utf-8", "yes"),
                    new XElement("Parameters",
                        new XElement("SamplesPerChannel", samplesPerChannel),
                        new XElement("Rate", rate),
                        new XElement("MinimumValue", minimumValue),
                        new XElement("MaximumValue", maximumValue),
                        new XElement("APIPort", 9000),
                        new XElement("TimerInterval", timerInterval)
                        )
                    );
            if (File.Exists(System.IO.Path.Combine(workingPath, paramFile)))
            {
                File.Delete(System.IO.Path.Combine(workingPath, paramFile));
            }
            using (var fileStream = new FileStream(System.IO.Path.Combine(workingPath, paramFile),
                    FileMode.Create, FileAccess.Write))
            {
                parameters.Save(fileStream);

            }
        }
    }

    public class Channel
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string DevicePath { get; set; }
    }

}
