using System;
using System.ServiceProcess;
using System.IO;
using System.Threading;
using System.Configuration;
using MultiFaceRec;

namespace WindowsServiceCS
{
    public partial class Service1 : ServiceBase
    {
        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            WriteToFile("FaceDetectionAndRecommendation Service started {0}");
            ScheduleService();
        }

        protected override void OnStop()
        {
            WriteToFile("FaceDetectionAndRecommendation Service stopped {0}");
            Schedular.Dispose();
        }

        private Timer Schedular;

        public void ScheduleService()
        {
            try
            {
                Schedular = new Timer(SchedularCallback);
                var mode = ConfigurationManager.AppSettings["Mode"].ToUpper();
                WriteToFile("FaceDetectionAndRecommendation Service Mode: " + mode + " {0}");

                //Set the Default Time.
                var scheduledTime = DateTime.MinValue;

                if (mode == "DAILY")
                {
                    //Get the Scheduled Time from AppSettings.
                    scheduledTime = DateTime.Parse(ConfigurationManager.AppSettings["ScheduledTime"]);
                    if (DateTime.Now > scheduledTime)
                    {
                        //If Scheduled Time is passed set Schedule for the next day.
                        scheduledTime = scheduledTime.AddDays(1);
                    }
                }

                if (mode.ToUpper() == "INTERVAL")
                {
                    //Get the Interval in Minutes from AppSettings.
                    var intervalMinutes = Convert.ToInt32(ConfigurationManager.AppSettings["IntervalMinutes"]);

                    //Set the Scheduled Time by adding the Interval to Current Time.
                    scheduledTime = DateTime.Now.AddMinutes(intervalMinutes);
                    if (DateTime.Now > scheduledTime)
                    {
                        //If Scheduled Time is passed set Schedule for the next Interval.
                        scheduledTime = scheduledTime.AddMinutes(intervalMinutes);
                    }
                }

                TimeSpan timeSpan = scheduledTime.Subtract(DateTime.Now);
                var schedule = string.Format("{0} day(s) {1} hour(s) {2} minute(s) {3} seconds(s)", timeSpan.Days, timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);

                WriteToFile("FaceDetectionAndRecommendation Service scheduled to run after: " + schedule + " {0}");

                //Get the difference in Minutes between the Scheduled and Current Time.
                var dueTime = Convert.ToInt32(timeSpan.TotalMilliseconds);

                //Change the Timer's Due Time.
                Schedular.Change(dueTime, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                WriteToFile("FaceDetectionAndRecommendation Service Error on: {0} " + ex.Message + ex.StackTrace);

                //Stop the Windows Service.
                using (var serviceController = new ServiceController("FaceDetectionAndRecommendation"))
                {
                    serviceController.Stop();
                }
            }
        }

        private void SchedularCallback(object e)
        {
            WriteToFile("FaceDetectionAndRecommendation Service Log: {0}");
            ScheduleService();
        }

        public void WriteToFile(string text)
        {
            //new FrmPrincipal().RunWindowsCommand();
            var path = "C:\\FaceDetectionAndRecommendation.txt";
            using (var writer = new StreamWriter(path, true))
            {
                writer.WriteLine(text, DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss tt"));
                writer.Close();
            }
        }
    }
}
