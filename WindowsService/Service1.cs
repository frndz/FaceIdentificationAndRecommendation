using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.IO;
using System.Threading;
using System.Configuration;
using System.Linq;
using Newtonsoft.Json;
using Twilio;
using Timer = System.Threading.Timer;

namespace WindowsServiceCS
{
    public partial class Service1 : ServiceBase
    {
        private string MainSystemDebugFolder, DebugFolder, LogFilePath, AccountSid, AuthToken, SmsFrom, ShoppingMallName, RecommendationFilePath, StoreFolder, TrainedFacesFolder;
        private int NoOfStores;
        private Timer Schedular;
        private const string TrainedLabelsFilePath = "TrainedLabels.txt";

        /// <summary>
        /// Constructor
        /// </summary>
        public Service1()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Set config values to global variables
        /// </summary>
        private void SetConfigValues()
        {
            DebugFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location) +
                          "\\..\\..\\..\\bin\\Debug";
            var config = GetJsonFromFile(DebugFolder + "\\Misc\\Config.json");
            LogFilePath = DebugFolder + config["LogFilePath"][0];
            AccountSid = config["AccountSid"][0];
            AuthToken = config["AuthToken"][0];
            SmsFrom = config["SmsFrom"][0];
            ShoppingMallName = config["ShoppingMallName"][0];
            RecommendationFilePath = DebugFolder + config["RecommendationFilePath"][0];
            StoreFolder = DebugFolder + config["StoreFolder"][0];
            TrainedFacesFolder = DebugFolder + config["TrainedFacesFolder"][0];
            NoOfStores = Convert.ToInt32(config["NoOfStores"][0]);
        }

        /// <summary>
        /// Gets called when the service starts
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStart(string[] args)
        {
            SetConfigValues();
            GenerateRecommendation();
            ScheduleService();
        }

        /// <summary>
        /// Gets called when the service stops
        /// </summary>
        protected override void OnStop()
        {
            Schedular.Dispose();
        }

        /// <summary>
        /// Schedules the task of copying data and writing to the log file in main system
        /// </summary>
        public void ScheduleService()
        {
            try
            {
                Schedular = new Timer(SchedularCallback);
                GenerateRecommendation();

                //Get the Scheduled Time from AppSettings.
                var scheduledTime = DateTime.MinValue;

                //Get the Interval in Minutes from AppSettings.
                var intervalMinutes = Convert.ToInt32(ConfigurationManager.AppSettings["IntervalInMinutes"]);

                //Set the Scheduled Time by adding the Interval to Current Time.
                scheduledTime = DateTime.Now.AddMinutes(intervalMinutes);
                if (DateTime.Now > scheduledTime)
                {
                    //If Scheduled Time is passed set Schedule for the next Interval.
                    scheduledTime = scheduledTime.AddMinutes(intervalMinutes);
                }

                var timeSpan = scheduledTime.Subtract(DateTime.Now);

                //Get the difference in Minutes between the Scheduled and Current Time.
                var dueTime = Convert.ToInt32(timeSpan.TotalMilliseconds);

                //Change the Timer's Due Time.
                Schedular.Change(dueTime, Timeout.Infinite);
            }
            catch (Exception)
            {
                //Stop the Windows Service.
                using (var serviceController = new ServiceController("FaceDetectionAndRecommendation"))
                {
                    serviceController.Stop();
                }
            }
        }

        /// <summary>
        /// Schedular callback
        /// </summary>
        /// <param name="e"></param>
        private void SchedularCallback(object e)
        {
            ScheduleService();
        }

        /// <summary>
        /// Send Sms
        /// </summary>
        /// <param name="smsFrom"></param>
        /// <param name="smsTo"></param>
        /// <param name="text"></param>
        public void SendMsg(string smsFrom, string smsTo, string text)
        {
            try
            {
                new TwilioRestClient(AccountSid, AuthToken).SendMessage(smsFrom, smsTo, text);
                WriteToLogFile("Successfully sent Msg to " + smsTo + "\n" + text + " {0}");
            }
            catch (Exception)
            {
                WriteToLogFile("Error while sending Msg to " + smsTo + "\n" + text + " {0}");
            }
        }

        /// <summary>
        /// Read the json file and return the deserialized json content
        /// </summary>
        /// <param name="filepath">Path of json file</param>
        /// <returns></returns>
        private static Dictionary<string, List<string>> GetJsonFromFile(string filepath)
        {
            var jsonData = File.ReadAllText(filepath);
            return JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(jsonData);
        }

        /// <summary>
        /// Write status to LogFile.txt in Main system
        /// </summary>
        /// <param name="text"></param>
        private void WriteToLogFile(string text)
        {
            using (var writer = new StreamWriter(LogFilePath, true))
            {
                writer.WriteLine(text, DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss tt"));
                writer.Close();
            }
        }

        /// <summary>
        /// Generate recommendation for registered users
        /// </summary>
        private void GenerateRecommendation()
        {
            //Load previous trained faces and labels for each image
            var Labels = File.ReadAllText(TrainedFacesFolder + TrainedLabelsFilePath).Split('%');
            var NoOfFaces = Convert.ToInt16(Labels[0]);

            for (var j = 1; j < NoOfFaces; ++j)
            {
                var name = Labels[j];
                var visitedStores = new List<string>();
                var recommendation = "Hi " + name.Split('+')[0] + ",\nLatest offers in " + ShoppingMallName + "\n";

                //Get the recommendation from the JSON file
                var jsonRecommendation = GetJsonFromFile(RecommendationFilePath);
                var jsonRecommendationGeneral = jsonRecommendation["General"];

                //Find the stores where the person (name) has visited
                for (var i = 1; i <= NoOfStores; ++i)
                {
                    var jsonStore = GetJsonFromFile(StoreFolder + "Store" + i + ".json");
                    if (jsonStore.ContainsKey(name))
                    {
                        visitedStores.Add("Store" + i);
                    }
                }

                //Add recommendation on the basis of store visited
                recommendation = visitedStores.Aggregate(recommendation, (current1, t1) => jsonRecommendation[t1].Aggregate(current1, (current, t) => current + ("\u2713 " + t + "\n")));

                //Add general recommendation
                recommendation = jsonRecommendationGeneral.Aggregate(recommendation, (current, t) => current + ("\u2713 " + t + "\n"));

                //Send recommendation by SMS
                SendMsg(SmsFrom, "+91" + name.Split('+')[2], recommendation);
                break;
            }
        }
    }
}
