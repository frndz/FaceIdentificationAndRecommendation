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

namespace MultiFaceRec
{
    /// <summary>
    /// Start of the app
    /// </summary>
    public partial class FrmPrincipal : Form
    {
        //Declaration of all variables, vectors and haarcascades
        Image<Bgr, Byte> currentFrame, uploadedImage;
        Capture grabber;
        HaarCascade face;
        MCvFont font = new MCvFont(FONT.CV_FONT_HERSHEY_TRIPLEX, 0.5d, 0.5d);
        Image<Gray, byte> result, resultUploadedFace, TrainedFace = null;
        Image<Gray, byte> UploadedFace = null;
        Image<Gray, byte> gray = null;
        Image<Gray, byte> grayUploadedFace = null;
        List<Image<Gray, byte>> trainingImages = new List<Image<Gray, byte>>();
        List<string> labels = new List<string>();
        int ContTrain, NumLabels;
        string name = string.Empty, names = string.Empty;
        Dictionary<string, List<string>> Config;
        string TrainedFacesFolder, UploadedFacesFolder, MiscFolder, MyStoreNameFilePath, MakeEntryInStoreFileAfterThisNoOfDay, VbsFolder, StoreFolder;
        string[] Labels;

        /// <summary>
        /// Constructor to initialize UI components and load previously trained images
        /// </summary>
        public FrmPrincipal()
        {
            InitializeComponent();
            //Load haarcascades for face detection
            face = new HaarCascade("haarcascade_frontalface_default.xml");

            try
            {
                //Get the config from Config.json
                Config = GetJsonFromFile("/Misc/Config.json");

                //Set the folder and file paths from Config.json
                TrainedFacesFolder = Config["TrainedFacesFolder"][0];
                UploadedFacesFolder = Config["UploadedFacesFolder"][0];
                MiscFolder = Config["MiscFolder"][0];
                MyStoreNameFilePath = Config["MyStoreNameFilePath"][0];
                MakeEntryInStoreFileAfterThisNoOfDay = Config["MakeEntryInStoreFileAfterThisNoOfDay"][0];
                VbsFolder = Config["VbsFolder"][0];
                StoreFolder = Config["StoreFolder"][0];

                //Load of previous trained faces and labels for each image
                var Labelsinfo = File.ReadAllText(Application.StartupPath + TrainedFacesFolder + "TrainedLabels.txt");
                Labels = Labelsinfo.Split('%');
                NumLabels = Convert.ToInt16(Labels[0]);
                ContTrain = NumLabels;
                string LoadFaces;

                for (int tf = 1; tf < NumLabels + 1; tf++)
                {
                    LoadFaces = "face" + tf + ".bmp";
                    trainingImages.Add(new Image<Gray, byte>(Application.StartupPath + TrainedFacesFolder + LoadFaces));
                    labels.Add(Labels[tf]);
                }

            }
            catch (Exception e)
            {
                MessageBox.Show("Nothing in the database, please add at least one face.", "Trained faces load", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
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
            SetLabels("0", "No match found");

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
                    label7.Text = name.Equals(string.Empty) ? "No match found" : name.Split('+')[0];

                    // Check the Webcam Mode (Itentification/Recording) and take action.
                    // Store name is being taken from Config.
                    var isStoreRecordingCameraOn = radioButton2.Checked;
                    if (isStoreRecordingCameraOn && !name.Equals(string.Empty))
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
            File.WriteAllText(Application.StartupPath + filePath, JsonConvert.SerializeObject(storeJsonData));
        }

        /// <summary>
        /// Convert date string into integer after removing name from it
        /// </summary>
        /// <param name="dateString"></param>
        /// <returns></returns>
        private int GetDateInInteger(string dateStringWithoutName)
        {
            var dateString = dateStringWithoutName.Split('/');
            var result = int.Parse(dateString[2].Split(' ')[0] + (dateString[0].Length == 2 ? dateString[0] : "0" + dateString[0]) + (dateString[1].Length == 2 ? dateString[1] : "0" + dateString[1]));
            return result;
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
                labels.Add(textBox1.Text + "+" + DateTime.Now);

                //Show face added in gray scale
                imageBox1.Image = result.Resize(162, 142, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);

                //Write the number of trained faces in a file text for further load
                File.WriteAllText(Application.StartupPath + TrainedFacesFolder + "TrainedLabels.txt", trainingImages.ToArray().Length.ToString() + "%");

                //Write the labels of trained faces in a file text for further load
                for (var i = 1; i < trainingImages.ToArray().Length + 1; i++)
                {
                    trainingImages.ToArray()[i - 1].Save(Application.StartupPath + TrainedFacesFolder + "face" + i + ".bmp");
                    File.AppendAllText(Application.StartupPath + TrainedFacesFolder + "TrainedLabels.txt", labels.ToArray()[i - 1] + "%");
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
        /// RunWindowsCommand
        /// </summary>
        /// <param name="command">RunWindowsCommand("/C copy " + Application.StartupPath + "\\Store\\*.* " + Application.StartupPath + "\\TempStore\\ /Z /Y");</param>
        public void RunWindowsCommand(string command)
        {
            var process = new Process();
            var startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = command;
            process.StartInfo = startInfo;
            process.Start();
        }

        /// <summary>
        /// Speak the text present in .vbs file
        /// </summary>
        private void Speak(string vbsFileName)
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

                //Face Detector
                //                MCvAvgComp[][] facesDetected = grayUploadedFace.DetectHaarCascade(
                //                  face,
                //                  1.2,
                //                  10,
                //                  Emgu.CV.CvEnum.HAAR_DETECTION_TYPE.DO_CANNY_PRUNING,
                //                  new Size(20, 20));

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
                    MessageBox.Show("No match found", "No match found", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
                else
                {
                    MessageBox.Show("Match found : " + name.Split('+')[0], "Match found", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            SetLabels("0", "No match found");

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
                //                fd.Title = "Choose image to upload";
                //                fd.Filter = "bmp files (*.bmp)|*.bmp";
                //                if (fd.ShowDialog() == DialogResult.OK)
                //                {
                //                    var userImage = new Image<Bgr, Byte>(new Bitmap(fd.FileName)).Resize(320, 240,
                //                        Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
                //                    //imageBoxForUserImage.Image = userImage;
                //
                //                    //Face Detector
                //                    MCvAvgComp[][] facesDetected = userImage.DetectHaarCascade(
                //                    face,
                //                    1.2,
                //                    10,
                //                    Emgu.CV.CvEnum.HAAR_DETECTION_TYPE.DO_CANNY_PRUNING,
                //                    new Size(20, 20));
                //
                //                    //Action for each element detected
                //                    foreach (MCvAvgComp f in facesDetected[0])
                //                    {
                //                        resultUploadedFace = userImage.Copy(f.rect).Convert<Gray, byte>();
                //                        break;
                //                    }
                //
                //                    //resize face detected image for force to compare the same size with the 
                //                    //test image with cubic interpolation type method
                //                    UploadedFace = resultUploadedFace.Resize(100, 100, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
                //
                //                    //Show face added in gray scale
                //                    imageBoxForUserImage.Image = UploadedFace;
                //
                //                    //Write the labels of triained faces in a file text for further load
                //                    UploadedFace.Save(Application.StartupPath + "/UploadedFaces/face.bmp");
                //                }
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
            var recommendation = string.Empty;

            //Get the recommendation from the JSON file
            var jsonRecommendation = GetJsonFromFile(MiscFolder + "Recommendation.json");
            var jsonRecommendationGeneral = jsonRecommendation["General"];
            var noOfStores = int.Parse(Config["NoOfStores"][0]);

            //Find the stores where the person (name) has visited
            for (var i = 0; i < noOfStores; ++i)
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

            label6.Text = recommendation;
        }

        /// <summary>
        /// Read the json file and return the deserialized json content
        /// </summary>
        /// <param name="filepath">Path of json file</param>
        /// <returns></returns>
        private static Dictionary<string, List<string>> GetJsonFromFile(string filepath)
        {
            var jsonData = File.ReadAllText(Application.StartupPath + filepath);
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