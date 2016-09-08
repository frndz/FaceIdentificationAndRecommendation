using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Twilio;
using Application = System.Windows.Forms.Application;

namespace MultiFaceRec
{
    /// <summary>
    /// Start of the app
    /// </summary>
    public partial class FrmPrincipal : Form
    {
        private const string NoMatchFound = "No match found";
        private const string MatchFound = "Match found";
        private const string False = "False";
        private const string ConfigFilePath = "/Misc/Config.json";
        private const string TrainedLabelsFilePath = "TrainedLabels.txt";
        private const string HaarcascadeFace = "haarcascade_frontalface_default.xml";

        //Declaration of all variables, vectors and haarcascades
        Image<Bgr, Byte> currentFrame, uploadedImage;
        Capture grabber;
        HaarCascade face;
        MCvFont font = new MCvFont(FONT.CV_FONT_HERSHEY_TRIPLEX, 0.5d, 0.5d);
        Image<Gray, byte> result, resultUploadedFace, TrainedFace, gray, grayUploadedFace;
        //Image<Gray, byte> UploadedFace = null;
        List<Image<Gray, byte>> trainingImages = new List<Image<Gray, byte>>();
        List<string> labels = new List<string>();
        int ContTrain, NumLabels, NoOfStores;
        string name = string.Empty, names = string.Empty;
        string TrainedFacesFolder, UploadedFacesFolder, RecommendationFilePath, MyStoreNameFilePath, MakeEntryInStoreFileAfterThisNoOfDay, VbsFolder, StoreFolder, AccountSid, AuthToken, SmsFrom, ShoppingMallName, isStoreWebcam, mainSystemFolder, SysAdmin;
        string[] Labels;

        /// <summary>
        /// Constructor to initialize UI components and load previously trained images
        /// </summary>
        public FrmPrincipal()
        {
            InitializeComponent();
            //Load haarcascades for face detection
            face = new HaarCascade(HaarcascadeFace);

            try
            {
                //Get the config from Config.json
                var Config = GetJsonFromFile(Application.StartupPath + ConfigFilePath);

                //Set the folder and file paths from Config.json
                SetConfigValuesIntoGlobalVariables(Config);

                //Load previous trained faces and labels for each image
                Labels = File.ReadAllText(TrainedFacesFolder + TrainedLabelsFilePath).Split('%');
                NumLabels = Convert.ToInt16(Labels[0]);
                ContTrain = NumLabels;

                //Populate loaded images in trainingImages list and update labels also
                for (var tf = 1; tf < NumLabels + 1; ++tf)
                {
                    trainingImages.Add(new Image<Gray, byte>(TrainedFacesFolder + "face" + tf + ".bmp"));
                    labels.Add(Labels[tf]);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Something went wrong while connecting to database", "Network Problem", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        /// <summary>
        /// Takes the value from Config and set the global variables
        /// </summary>
        /// <param name="config"></param>
        private void SetConfigValuesIntoGlobalVariables(IDictionary<string, List<string>> config)
        {
            isStoreWebcam = config["IsStoreWebcam"][0];
            mainSystemFolder = config["MainSystemFolder"][0];
            UploadedFacesFolder = config["UploadedFacesFolder"][0];
            MakeEntryInStoreFileAfterThisNoOfDay = config["MakeEntryInStoreFileAfterThisNoOfDay"][0];
            VbsFolder = config["VbsFolder"][0];
            TrainedFacesFolder = isStoreWebcam == False
                                 ? Application.StartupPath + config["TrainedFacesFolder"][0]
                                 : mainSystemFolder + config["TrainedFacesFolder"][0];
            StoreFolder = isStoreWebcam == False
                          ? Application.StartupPath + config["StoreFolder"][0]
                          : mainSystemFolder + config["StoreFolder"][0];
            MyStoreNameFilePath = StoreFolder + config["MyStoreName"][0];
            RecommendationFilePath = isStoreWebcam == False
                                     ? Application.StartupPath + config["RecommendationFilePath"][0]
                                     : mainSystemFolder + config["RecommendationFilePath"][0];
            NoOfStores = int.Parse(config["NoOfStores"][0]);
            AccountSid = config["AccountSid"][0];
            AuthToken = config["AuthToken"][0];
            SmsFrom = config["SmsFrom"][0];
            ShoppingMallName = config["ShoppingMallName"][0];
            SysAdmin = config["SysAdmin"][0];

            if (!isStoreWebcam.Equals(False))
            {
                groupBox1.Visible = false;
                groupBox3.Visible = false;
            }
        }

        /// <summary>
        /// Capture from Webcam
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void FrameGrabber(object sender, EventArgs e)
        {
            //Set initial label texts
            SetLabels("0", NoMatchFound);

            //Get the current frame form capture device
            currentFrame = grabber.QueryFrame().Resize(320, 240, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);

            //Convert it to Grayscale
            gray = currentFrame.Convert<Gray, Byte>();

            //Face Detector
            MCvAvgComp[][] facesDetected = gray.DetectHaarCascade(
               face,
               1.2,
               10,
               Emgu.CV.CvEnum.HAAR_DETECTION_TYPE.DO_CANNY_PRUNING,
               new Size(20, 20));

            //Action for each element detected
            foreach (MCvAvgComp f in facesDetected[0])
            {
                result = currentFrame.Copy(f.rect).Convert<Gray, byte>().Resize(100, 100, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
                //draw the face detected in the 0th (gray) channel with red color
                currentFrame.Draw(f.rect, new Bgr(Color.Red), 2);

                if (trainingImages.ToArray().Length != 0)
                {
                    //TermCriteria for face recognition with numbers of trained images like maxIteration
                    MCvTermCriteria termCrit = new MCvTermCriteria(ContTrain, 0.001);

                    //Eigen face recognizer
                    EigenObjectRecognizer recognizer = new EigenObjectRecognizer(
                      trainingImages.ToArray(),
                      labels.ToArray(),
                      4000,
                      ref termCrit);

                    // Name of the person identified
                    name = recognizer.Recognize(result);

                    //Draw the frame for each face detected and recognized and show the names
                    currentFrame.Draw(name.Split('+')[0], ref font, new Point(f.rect.X - 2, f.rect.Y - 2), new Bgr(Color.LightGreen));
                    label7.Text = name.Equals(string.Empty) ? NoMatchFound : name.Split('+')[0];

                    // Check the Webcam Mode (Itentification/Recording) and take action.
                    if (!isStoreWebcam.Equals(False) && !name.Equals(string.Empty))
                    {
                        var storeJsonData = GetJsonFromFile(MyStoreNameFilePath);

                        if (!storeJsonData.ContainsKey(name))
                        {
                            // Make new entry (Name & Date) for the person visiting the store first time
                            storeJsonData.Add(name, new List<string> { DateTime.Now.ToString() });
                            WriteJsonToFile(storeJsonData, MyStoreNameFilePath);
                        }
                        else
                        {
                            // Append only the current date for the person who has already visited this store
                            var storeJsonDataForIdentifiedPerson = storeJsonData[name];
                            var lastEntryDate = storeJsonDataForIdentifiedPerson[storeJsonDataForIdentifiedPerson.Count - 1];
                            var diffInDays = GetDateInInteger(DateTime.Now.ToString()) - GetDateInInteger(lastEntryDate);
                            if (diffInDays >= int.Parse(MakeEntryInStoreFileAfterThisNoOfDay))
                            {
                                storeJsonData[name].Add(DateTime.Now.ToString());
                                WriteJsonToFile(storeJsonData, MyStoreNameFilePath);
                            }
                        }
                    }
                }

                // Name of faces detected on the scene
                names += " ";
            }

            //Show the faces processed and recognized
            imageBoxFrameGrabber.Image = currentFrame;

            //Set label texts
            SetLabels(facesDetected[0].Length.ToString(), names);
            names = string.Empty;
        }

        /// <summary>
        /// Write to file
        /// </summary>
        /// <param name="storeJsonData">Json data</param>
        /// <param name="filePath">Path to file</param>
        private void WriteJsonToFile(Dictionary<string, List<string>> storeJsonData, string filePath)
        {
            File.WriteAllText(filePath, JsonConvert.SerializeObject(storeJsonData));
        }

        /// <summary>
        /// Convert date string into integer after removing name from it
        /// </summary>
        /// <param name="dateString"></param>
        /// <returns></returns>
        private int GetDateInInteger(string dateStringWithoutName)
        {
            var dateString = dateStringWithoutName.Split('/');
            return int.Parse(dateString[2].Split(' ')[0] + (dateString[0].Length == 2 ? dateString[0] : "0" + dateString[0]) + (dateString[1].Length == 2 ? dateString[1] : "0" + dateString[1]));
        }

        /// <summary>
        /// Detect and recognize face
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            //Initialize the capture device
            grabber = new Capture();
            grabber.QueryFrame();

            //Initialize the FrameGraber event
            Application.Idle += new EventHandler(FrameGrabber);
            button1.Enabled = false;
            //SetButtonsState(false, true, false, true);
        }

        /// <summary>
        /// Add new face for training
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, System.EventArgs e)
        {
            if (textBox1.Text.Equals(string.Empty))
            {
                Speak("EmptyAddFace.vbs");
                MessageBox.Show("Please provide your name and then try adding your face.", "Add Face", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            //SetButtonsState(false, true, false, true);
            try
            {
                //Trained face counter
                ++ContTrain;

                //Get a gray frame from capture device
                gray = grabber.QueryGrayFrame().Resize(320, 240, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);

                //Face Detector
                MCvAvgComp[][] facesDetected = gray.DetectHaarCascade(
                face,
                1.2,
                10,
                Emgu.CV.CvEnum.HAAR_DETECTION_TYPE.DO_CANNY_PRUNING,
                new Size(20, 20));

                //Action for each element detected
                foreach (MCvAvgComp f in facesDetected[0])
                {
                    TrainedFace = currentFrame.Copy(f.rect).Convert<Gray, byte>();
                    break;
                }

                //resize face detected image for force to compare the same size with the 
                //test image with cubic interpolation type method
                TrainedFace = result.Resize(100, 100, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
                trainingImages.Add(TrainedFace);
                labels.Add(textBox1.Text + "+" + DateTime.Now + "+" + textBox2.Text);

                //Show face added in gray scale
                imageBox1.Image = result.Resize(162, 142, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);

                //Write the number of trained faces in a file text for further load
                File.WriteAllText(TrainedFacesFolder + TrainedLabelsFilePath, trainingImages.ToArray().Length.ToString() + "%");

                //Write the labels of trained faces in a file text for further load
                for (var i = 1; i < trainingImages.ToArray().Length + 1; i++)
                {
                    trainingImages.ToArray()[i - 1].Save(TrainedFacesFolder + "face" + i + ".bmp");
                    File.AppendAllText(TrainedFacesFolder + TrainedLabelsFilePath, labels.ToArray()[i - 1] + "%");
                }

                // Speak the message "Thank you for registering your face"
                Speak("register.vbs");
                MessageBox.Show(textBox1.Text + "´s face detected and added", "Training OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch
            {
                MessageBox.Show("Enable the face detection first", "Training Fail", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        /// <summary>
        /// Speak the text present in .vbs file
        /// </summary>
        public void Speak(string vbsFileName)
        {
            var scriptProc = new Process();
            scriptProc.StartInfo.FileName = @Application.StartupPath + VbsFolder + vbsFileName;
            scriptProc.Start();
        }

        /// <summary>
        /// Find the best match for user uploaded image
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button3_Click(object sender, EventArgs e)
        {
            //SetButtonsState(false, true, true, true);
            try
            {
                //Get the uploaded image, resize it and convert it to Grayscale.
                uploadedImage = new Image<Bgr, byte>(Application.StartupPath + UploadedFacesFolder + "face.bmp").Resize(320, 240,
                        Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);

                grayUploadedFace = uploadedImage.Convert<Gray, Byte>();

                //Action for each element detected
                resultUploadedFace = grayUploadedFace.Resize(100, 100, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);

                if (trainingImages.ToArray().Length != 0)
                {
                    //TermCriteria for face recognition with numbers of trained images like maxIteration
                    MCvTermCriteria termCrit = new MCvTermCriteria(ContTrain, 0.001);

                    //Eigen face recognizer
                    EigenObjectRecognizer eigenObjectRecognizer = new EigenObjectRecognizer(
                       trainingImages.ToArray(),
                       labels.ToArray(),
                       4000,
                       ref termCrit);

                    name = eigenObjectRecognizer.Recognize(resultUploadedFace);
                }

                //Show the faces processed and recognized
                if (name.Equals(string.Empty))
                {
                    MessageBox.Show(NoMatchFound, NoMatchFound, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
                else
                {
                    MessageBox.Show(MatchFound + " : " + name.Split('+')[0], MatchFound, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch
            {
                MessageBox.Show("Error in face detection", "Error in face detection", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Upload image from local system
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button4_Click(object sender, EventArgs e)
        {
            //SetButtonsState(false, false, true, true);
            SetLabels("0", NoMatchFound);

            //Choose image file to upload from local system *.bmp only
            using (var fd = new OpenFileDialog())
            {
                fd.Title = "Choose image to upload";
                fd.Filter = "bmp files (*.bmp)|*.bmp";
                if (fd.ShowDialog() == DialogResult.OK)
                {
                    var ResizedImage = new Image<Bgr, Byte>(new Bitmap(fd.FileName)).Resize(240, 240, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
                    imageBoxForUserImage.Image = ResizedImage;

                    var ResizedGrayImage = ResizedImage.Resize(100, 100, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC).Convert<Gray, Byte>();
                    ResizedGrayImage.Save(Application.StartupPath + UploadedFacesFolder + "face.bmp");
                }
            }
            //button3.Enabled = true;
        }

        /// <summary>
        /// View recommendation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button5_Click(object sender, EventArgs e)
        {
            var visitedStores = new List<string>();
            var recommendation = "Hi " + name.Split('+')[0] + ",\nLatest offers in " + ShoppingMallName + "\n";

            //Get the recommendation from the JSON file
            var jsonRecommendation = GetJsonFromFile(RecommendationFilePath);
            var jsonRecommendationGeneral = jsonRecommendation["General"];

            //Find the stores where the person (name) has visited
            for (var i = 0; i < NoOfStores; ++i)
            {
                var jsonStore = GetJsonFromFile(StoreFolder + "Store" + (i + 1) + ".json");
                if (jsonStore.ContainsKey(name))
                {
                    visitedStores.Add("Store" + (i + 1));
                }
            }

            //Add recommendation on the basis of store visited
            recommendation = visitedStores.Aggregate(recommendation, (current1, t1) => jsonRecommendation[t1].Aggregate(current1, (current, t) => current + ("\u2713 " + t + "\n")));

            //Add general recommendation
            recommendation = jsonRecommendationGeneral.Aggregate(recommendation, (current, t) => current + ("\u2713 " + t + "\n"));

            //Show recommendation in the label in UI
            label6.Text = recommendation;

            //Send recommendation by SMS
            try
            {
                SendMsg(SmsFrom, "+91" + name.Split('+')[2], recommendation);
            }
            catch (Exception)
            {
                Speak("RegisterForMsg.vbs");
            }
        }

        /// <summary>
        /// Send SMS
        /// </summary>
        /// <param name="SmsFrom"></param>
        /// <param name="SmsTo"></param>
        /// <param name="text"></param>
        private void SendMsg(string SmsFrom, string SmsTo, string text)
        {
            new TwilioRestClient(AccountSid, AuthToken).SendMessage(SmsFrom, SmsTo, text);
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
        /// Enable or Disable buttons
        /// </summary>
        /// <param name="stateBtn1">Detect button</param>
        /// <param name="stateBtn2">Add Face button</param>
        /// <param name="stateBtn3">Find best match button</param>
        /// <param name="stateBtn4">Upload button</param>
        private void SetButtonsState(bool stateBtn1, bool stateBtn2, bool stateBtn3, bool stateBtn4)
        {
            button1.Enabled = stateBtn1;
            button2.Enabled = stateBtn2;
            button3.Enabled = stateBtn3;
            button4.Enabled = stateBtn4;
        }

        /// <summary>
        /// Set label values for names and count
        /// </summary>
        /// <param name="lbl3">Count of person</param>
        /// <param name="lbl4">Name(s) of person</param>
        private void SetLabels(string lbl3, string lbl4)
        {
            label3.Text = lbl3;
        }
    }
}