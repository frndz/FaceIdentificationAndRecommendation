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
        private const string NoMatchFound = "";
        private const string MatchFound = "Match found";
        private const string False = "False";
        private const string ConfigFilePath = "/Misc/Config.json";
        private const string TrainedLabelsFilePath = "TrainedLabels.txt";
        private const string HaarcascadeFace = "haarcascade_frontalface_default.xml";
        private const string ErrorInFaceDetection = "Error in face detection";

        //Declaration of all variables, vectors and haarcascades
        Image<Bgr, Byte> currentFrame, uploadedImage;
        Capture grabber;
        HaarCascade face;
        MCvFont font = new MCvFont(FONT.CV_FONT_HERSHEY_TRIPLEX, 0.5d, 0.5d);
        Image<Gray, byte> result, resultUploadedFace, TrainedFace, gray, grayUploadedFace;
        //Image<Gray, byte> UploadedFace = null;
        List<Image<Gray, byte>> trainingImages = new List<Image<Gray, byte>>();
        List<string> labels = new List<string>();
        int ContTrain, NumLabels, NoOfStores, EigenThresholdConstant, NoOfPicsForEachPerson, TimeIntervalForMultiplePics, counter = 0;
        string name = string.Empty, names = string.Empty;
        string TrainedFacesFolder, UploadedFacesFolder, RecommendationFilePath, MyStoreNameFilePath, MakeEntryInStoreFileAfterThisNoOfDay, VbsFolder, StoreFolder, AccountSid, AuthToken, SmsFrom, ShoppingMallName, isStoreWebcam, mainSystemFolder, SysAdmin, MyStoreName;
        string[] Labels;
        Timer tmr = new Timer();
        Timer addNewFaceTimer = new Timer();

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
                LoadNewlyAddedFaces();
            }
            catch (Exception)
            {
                NotifyUserAndSysAdmin();
            }
        }

        /// <summary>
        /// Reload training image
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LoadTrainedImages(object sender, EventArgs e)
        {
            // Load newly added training images
            LoadNewlyAddedFaces();
        }

        /// <summary>
        /// Load the trained images
        /// </summary>
        private void LoadNewlyAddedFaces()
        {
            Labels = File.ReadAllText(TrainedFacesFolder + TrainedLabelsFilePath).Split('%');
            NumLabels = Convert.ToInt16(Labels[0]);
            ContTrain = NumLabels;

            //Populate loaded images in trainingImages list and update labels also
            for (var tf = trainingImages.Count + 1; tf < NumLabels + 1; ++tf)
            {
                trainingImages.Add(new Image<Gray, byte>(TrainedFacesFolder + "face" + tf + ".bmp"));
                labels.Add(Labels[tf]);
            }
        }

        /// <summary>
        /// Notify User and System Admin in case of any connection issue with the main system
        /// </summary>
        private void NotifyUserAndSysAdmin()
        {
            Speak("NetworkIssue.vbs");
            MessageBox.Show("Unable to connect to Main system", "Network Problem", MessageBoxButtons.OK,
                MessageBoxIcon.Exclamation);
            SendMsg(SmsFrom, SysAdmin, "Network issue\n" + MyStoreName + " is unable to connect to Main system");
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
            if (isStoreWebcam == False)
            {
                VbsFolder = Application.StartupPath + config["VbsFolder"][0];
                TrainedFacesFolder = Application.StartupPath + config["TrainedFacesFolder"][0];
                StoreFolder = Application.StartupPath + config["StoreFolder"][0];
                RecommendationFilePath = Application.StartupPath + config["RecommendationFilePath"][0];
            }
            else
            {
                VbsFolder = mainSystemFolder + config["VbsFolder"][0];
                TrainedFacesFolder = mainSystemFolder + config["TrainedFacesFolder"][0];
                StoreFolder = mainSystemFolder + config["StoreFolder"][0];
                RecommendationFilePath = mainSystemFolder + config["RecommendationFilePath"][0];
                groupBox1.Visible = false;
                groupBox3.Visible = false;
            }
            MyStoreName = config["MyStoreName"][0].Split('.')[0];
            MyStoreNameFilePath = StoreFolder + config["MyStoreName"][0];
            NoOfStores = int.Parse(config["NoOfStores"][0]);
            NoOfPicsForEachPerson = int.Parse(config["NoOfPicsForEachPerson"][0]);
            EigenThresholdConstant = int.Parse(config["EigenThresholdConstant"][0]);
            TimeIntervalForMultiplePics = int.Parse(config["TimeIntervalForMultiplePics"][0]);
            AccountSid = config["AccountSid"][0];
            AuthToken = config["AuthToken"][0];
            SmsFrom = config["SmsFrom"][0];
            ShoppingMallName = config["ShoppingMallName"][0];
            SysAdmin = config["SysAdmin"][0];
            tmr.Interval = int.Parse(config["TimerInterval"][0]);
            tmr.Tick += LoadTrainedImages;
            tmr.Start();
            addNewFaceTimer.Interval = TimeIntervalForMultiplePics;
            addNewFaceTimer.Tick += AddMultipleFaceHandler;
        }

        /// <summary>
        /// Capture from Webcam
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FrameGrabber(object sender, EventArgs e)
        {
            //Set initial label texts
            label3.Text = "0";
            label7.Text = NoMatchFound;

            //Get the current frame form capture device
            currentFrame = grabber.QueryFrame().Resize(320, 240, INTER.CV_INTER_CUBIC);

            //Convert it to Grayscale
            gray = currentFrame.Convert<Gray, Byte>();

            //Face Detector
            MCvAvgComp[][] facesDetected = gray.DetectHaarCascade(
               face,
               1.2,
               10,
               HAAR_DETECTION_TYPE.DO_CANNY_PRUNING,
               new Size(20, 20));

            //Action for each element detected
            foreach (MCvAvgComp f in facesDetected[0])
            {
                result = currentFrame.Copy(f.rect).Convert<Gray, byte>().Resize(100, 100, INTER.CV_INTER_CUBIC);
                
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
                      EigenThresholdConstant,
                      ref termCrit);

                    // Name of the person identified
                    name = recognizer.Recognize(result);

                    //Draw the frame for each face detected and recognized and show the names
                    currentFrame.Draw(name.Split('+')[0], ref font, new Point(f.rect.X - 2, f.rect.Y - 2), new Bgr(Color.LightGreen));
                    label7.Text += name.Equals(string.Empty) ? NoMatchFound : name.Split('+')[0] + " ";

                    // Make an entry in corresponding json file if face is identified
                    if (!name.Equals(string.Empty))
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
            label3.Text = facesDetected[0].Length.ToString();
            names = string.Empty;
        }

        /// <summary>
        /// Write to file
        /// </summary>
        /// <param name="storeJsonData">Json data</param>
        /// <param name="filePath">Path to file</param>
        private void WriteJsonToFile(Dictionary<string, List<string>> storeJsonData, string filePath)
        {
            try
            {
                File.WriteAllText(filePath, JsonConvert.SerializeObject(storeJsonData));
            }
            catch (Exception)
            {
                NotifyUserAndSysAdmin();
            }
        }

        /// <summary>
        /// Convert date string into integer after removing name from it
        /// </summary>
        /// <param name="dateStringWithoutName"></param>
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

            //Initialize the FrameGrabber event
            Application.Idle += FrameGrabber;
            button1.Enabled = false;
        }

        /// <summary>
        /// Add new face for training
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e)
        {
            if (textBox1.Text.Equals(string.Empty))
            {
                Speak("EmptyAddFace.vbs");
                MessageBox.Show("Please provide your name and then try adding your face.", "Add Face", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Start the timer to capture 10 (configurable) pics
                addNewFaceTimer.Start();
            }
            catch (IOException)
            {
                NotifyUserAndSysAdmin();
                ClearTextFields();
            }
            catch (Exception)
            {
                MessageBox.Show("Enable the face detection first, click Start button", "Training Fail", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        /// <summary>
        /// Add new face and update it in system
        /// </summary>
        private void AddNewFace()
        {
            //Show face added in gray scale
            imageBox1.Image = result.Resize(162, 142, INTER.CV_INTER_CUBIC);

            //Write the number of trained faces in a file text for further load
            File.WriteAllText(TrainedFacesFolder + TrainedLabelsFilePath, trainingImages.ToArray().Length + "%");

            //Write names of trained faces in the text file
            for (var i = 1; i < trainingImages.ToArray().Length + 1; i++)
            {
                File.AppendAllText(TrainedFacesFolder + TrainedLabelsFilePath, labels.ToArray()[i - 1] + "%");
            }

            // Speak the message "Thank you for registering your face"
            Speak("register.vbs");

            // Show the Thank You msg
            MessageBox.Show(textBox1.Text + "´s face detected and added", "Training OK", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // Clear the text fields after registration
            ClearTextFields();
        }

        /// <summary>
        /// Handler to capture 10 (configurable) pics of a new person
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddMultipleFaceHandler(object sender, EventArgs e)
        {
            var uniqueName = textBox1.Text + "+" + DateTime.Now + "+" + textBox2.Text;
            TakeMultiplePics(uniqueName);
        }

        /// <summary>
        /// Take multiple pic for a new person for registering his face
        /// </summary>
        /// <param name="uniqueName"></param>
        private void TakeMultiplePics(string uniqueName)
        {
            if (++counter > NoOfPicsForEachPerson)
            {
                counter = 0;
                addNewFaceTimer.Stop();
                AddNewFace();
                return;
            }
            //Trained face counter
            ++ContTrain;

            //Get a gray frame from capture device
            gray = grabber.QueryGrayFrame().Resize(320, 240, INTER.CV_INTER_CUBIC);

            //Face Detector
            MCvAvgComp[][] facesDetected = gray.DetectHaarCascade(
                face,
                1.2,
                10,
                HAAR_DETECTION_TYPE.DO_CANNY_PRUNING,
                new Size(20, 20));

            //Action for each element detected
            foreach (MCvAvgComp f in facesDetected[0])
            {
                TrainedFace = currentFrame.Copy(f.rect).Convert<Gray, byte>();
                break;
            }

            //resize face detected image for force to compare the same size with the test image with cubic interpolation type method
            TrainedFace = result.Resize(100, 100, INTER.CV_INTER_CUBIC);

            // Add new Trained face to trainingImages object
            trainingImages.Add(TrainedFace);

            // Update the labels for the newly added face
            labels.Add(uniqueName);

            // Save the newly added face
            trainingImages.ToArray()[trainingImages.Count - 1].Save(TrainedFacesFolder + "face" + trainingImages.Count + ".bmp");
        }

        /// <summary>
        /// Clear the text fields
        /// </summary>
        private void ClearTextFields()
        {
            textBox1.Text = string.Empty;
            textBox2.Text = string.Empty;
            textBox1.Focus();
        }

        /// <summary>
        /// Speak the text present in .vbs file
        /// </summary>
        public void Speak(string vbsFileName)
        {
            var scriptProc = new Process();
            scriptProc.StartInfo.FileName = @VbsFolder + vbsFileName;
            scriptProc.Start();
        }

        /// <summary>
        /// Find the best match for user uploaded image
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button3_Click(object sender, EventArgs e)
        {
            var person = string.Empty;
            try
            {
                //Get the uploaded image, resize it and convert it to Grayscale.
                uploadedImage = new Image<Bgr, byte>(Application.StartupPath + UploadedFacesFolder + "face.bmp").Resize(320, 240,
                        INTER.CV_INTER_CUBIC);

                grayUploadedFace = uploadedImage.Convert<Gray, Byte>();

                //Action for each element detected
                resultUploadedFace = grayUploadedFace.Resize(100, 100, INTER.CV_INTER_CUBIC);

                if (trainingImages.ToArray().Length != 0)
                {
                    //TermCriteria for face recognition with numbers of trained images like maxIteration
                    MCvTermCriteria termCrit = new MCvTermCriteria(ContTrain, 0.001);

                    //Eigen face recognizer
                    EigenObjectRecognizer eigenObjectRecognizer = new EigenObjectRecognizer(
                       trainingImages.ToArray(),
                       labels.ToArray(),
                       EigenThresholdConstant,
                       ref termCrit);

                    person = eigenObjectRecognizer.Recognize(resultUploadedFace);
                }

                //Show the faces processed and recognized
                if (person.Equals(string.Empty))
                {
                    MessageBox.Show(NoMatchFound, NoMatchFound, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
                else
                {
                    MessageBox.Show(MatchFound + "\n" + person.Split('+')[0] + "\n" + person.Split('+')[2], MatchFound, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch
            {
                MessageBox.Show(ErrorInFaceDetection, ErrorInFaceDetection, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Upload image from local system
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button4_Click(object sender, EventArgs e)
        {
            // Set No of person as 0
            label3.Text = "0";

            // Choose image file to upload from local system *.bmp only
            using (var fd = new OpenFileDialog())
            {
                fd.Title = "Choose image to upload";
                fd.Filter = "bmp files (*.bmp)|*.bmp";
                if (fd.ShowDialog() == DialogResult.OK)
                {
                    var ResizedImage = new Image<Bgr, Byte>(new Bitmap(fd.FileName)).Resize(240, 240, INTER.CV_INTER_CUBIC);
                    imageBoxForUserImage.Image = ResizedImage;

                    var ResizedGrayImage = ResizedImage.Resize(100, 100, INTER.CV_INTER_CUBIC).Convert<Gray, Byte>();
                    ResizedGrayImage.Save(Application.StartupPath + UploadedFacesFolder + "face.bmp");
                }
            }
        }

        /// <summary>
        /// View recommendation for user visited stores
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button5_Click(object sender, EventArgs e)
        {
            var visitedStores = new List<string>();
            var recommendation = "Hi " + name.Split('+')[0] + ",\nLatest offers for you in " + ShoppingMallName + "\n";

            //Generate recommendation for user visited stores
            try
            {
                recommendation = GenerateRecommendation(visitedStores, recommendation, false);

                //Send recommendation by SMS
                SendMsg(SmsFrom, "+91" + name.Split('+')[2], recommendation);
            }
            catch (IOException)
            {
                NotifyUserAndSysAdmin();
            }
            catch (Exception)
            {
                Speak("RegisterForMsg.vbs");
            }
        }

        /// <summary>
        /// Generates recommendation
        /// </summary>
        /// <param name="visitedStores"></param>
        /// <param name="recommendation"></param>
        /// <returns></returns>
        private string GenerateRecommendation(ICollection<string> visitedStores, string recommendation, bool allOffers)
        {
            label6.Text = string.Empty;
            var jsonRecommendation = GetJsonFromFile(RecommendationFilePath);
            var jsonRecommendationGeneral = jsonRecommendation["General"];

            for (var i = 1; i <= NoOfStores; ++i)
            {
                if (allOffers)
                {
                    //Add all stores for all offers
                    visitedStores.Add("Store" + i);
                }
                else
                {
                    //Find the stores where the person (name) has visited
                    var jsonStore = GetJsonFromFile(StoreFolder + "Store" + i + ".json");
                    if (jsonStore.ContainsKey(name))
                    {
                        visitedStores.Add("Store" + i);
                    }
                }
            }

            //Add recommendation on the basis of store visited
            recommendation = visitedStores.Aggregate(recommendation,
                (current1, t1) => jsonRecommendation[t1].Aggregate(current1, (current, t) => current + ("\u2713 " + t + "\n")));

            //Add general recommendation
            recommendation = jsonRecommendationGeneral.Aggregate(recommendation,
                (current, t) => current + ("\u2713 " + t + "\n"));

            //Show recommendation in the label in UI
            label6.Text = recommendation;
            return recommendation;
        }

        /// <summary>
        /// Show all the offers currently running in the mall
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button6_Click(object sender, EventArgs e)
        {
            var visitedStores = new List<string>();
            var recommendation = "All offers in " + ShoppingMallName + "\n";

            //Generate recommendation from the customer, all offers
            try
            {
                GenerateRecommendation(visitedStores, recommendation, true);
            }
            catch (Exception)
            {
                NotifyUserAndSysAdmin();
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
    }
}