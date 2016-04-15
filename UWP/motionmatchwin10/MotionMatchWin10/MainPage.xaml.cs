using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.Azure.Devices.Client;
using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Microsoft.Band.Sensors;
using Microsoft.Band;
using Windows.Media.SpeechSynthesis;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Formatting;
using Windows.Data.Json;
using Newtonsoft.Json.Linq;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace MotionMatchWin10
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // For phone accelermeter support
        private Accelerometer _accelerometer;
        private uint _desiredReportInterval;

        // For Microsoft band support
        //BandHelper bandHelper = new BandHelper();
        private IBandClient bandClient;

        private Motion currentMotion;
        private List<Sample> samples;
        private bool isRecording = false;
        private bool isSpoken = false;

        private const int SamplingDelay = 32; // Allowed values: 16, 32, 128

        MediaElement mediaplayer;
        MediaElement mp;

        public MainPage()
        {
            this.InitializeComponent();

            InitTTS();

            this.Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            // If the switch is on, we use the Band (not the phone)
            if (switchInputMode.IsOn)
            {
                if (await InitMSBand())
                {
                    // Update UI if needed
                }
            }
            else
            {
                if (InitPhoneAccelerometer())
                {
                    // Update UI if needed
                }
            }
        }

        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (!isRecording)
            {
                isRecording = true;
                isSpoken = false;

                currentMotion = new Motion();
                samples = new List<Sample>();

                string guid = Guid.NewGuid().ToString();

                currentMotion.id = guid;
                currentMotion.User = "Nick with Band";
                currentMotion.ActivityName = "Tennis TopSpin";
                currentMotion.TrackedBodyPart = "Right Hand";

                // If the switch is on, we use the Band (not the phone)
                if (switchInputMode.IsOn)
                {
                    StartMSBand();
                }
                else
                {
                    StartAccelerometer();
                }

                lblStatus.Text = "Recording training motion...";
                btnStart.Content = "Stop Activity";
                btnStart.Background = new SolidColorBrush(Colors.Red);
            }
            else
            {
                // If the switch is on, we use the Band (not the phone)
                if (switchInputMode.IsOn)
                {
                    await StopMSBand();
                }
                else
                {
                    StopAccelerometer();
                }

                // If the MoCap Mode switch is on, we are in training, or "test" mode
                if (switchMoCapMode.IsOn)
                {
                    MotionStats mstats = new MotionStats();

                    mstats.avgX = samples.Average(x => x.X);
                    mstats.stddevX = Math.Sqrt(samples.Average(v => Math.Pow(v.X - mstats.avgX, 2)));
                    mstats.avgY = samples.Average(x => x.Y);
                    mstats.stddevY = Math.Sqrt(samples.Average(v => Math.Pow(v.Y - mstats.avgY, 2)));
                    mstats.avgZ = samples.Average(x => x.Z);
                    mstats.stddevZ = Math.Sqrt(samples.Average(v => Math.Pow(v.Z - mstats.avgZ, 2)));
                    mstats.avggX = samples.Average(x => x.gX);
                    mstats.stddevgX = Math.Sqrt(samples.Average(v => Math.Pow(v.gX - mstats.avggX, 2)));
                    mstats.avggY = samples.Average(x => x.gY);
                    mstats.stddevgY = Math.Sqrt(samples.Average(v => Math.Pow(v.gY - mstats.avggY, 2)));
                    mstats.avggZ = samples.Average(x => x.gZ);
                    mstats.stddevgZ = Math.Sqrt(samples.Average(v => Math.Pow(v.gZ - mstats.avggZ, 2)));

                    await InvokeMachineLearningService(mstats);
                }
                else // If the MoCap Mode switch is off, we are recording new training data
                {
                    currentMotion.Quality = switchQuality.IsOn ? "Good" : "Bad";

                    AzureHelper.SendBatchToAzure(samples);
                    AzureHelper.SendToAzure(currentMotion);

                    lblStatus.Text = "Training motion recorded and sent!";
                }
                btnStart.Content = "Start Activity";
                btnStart.Background = new SolidColorBrush(Colors.Green);

                //currentMotion = null;
                //samples = null;

                isRecording = false;
            }
        }

        private bool InitPhoneAccelerometer()
        {
            _accelerometer = Accelerometer.GetDefault();
            if (_accelerometer != null)
            {
                // Select a report interval that is both suitable for the purposes of the app and supported by the sensor.
                // This value will be used later to activate the sensor.
                uint minReportInterval = _accelerometer.MinimumReportInterval;
                _desiredReportInterval = minReportInterval > 16 ? minReportInterval : 16;
                lblStatus.Text = "Phone accelerometer ready.";

                return true;
            }
            else
            {
                lblStatus.Text = "No accelerometer found";
                return false;
            }
        }

        private async Task<bool> InitMSBand()
        {
            //Initialize bh, which connects to Band
            string response = await this.LoadBandAsync();
            if (response.Equals("Success"))
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    lblStatus.Text = "Microsoft Band sensors ready.";
                });
                return true;
            }
            else
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    lblStatus.Text = "Error initializing Microsoft Band.";
                });
                return false;
            }
        }

        public async Task<String> LoadBandAsync()
        {
            IBandInfo[] pairedBands = await BandClientManager.Instance.GetBandsAsync();
            string response = "";
            try
            {
                bandClient = await BandClientManager.Instance.ConnectAsync(pairedBands[0]);
                response = "Success";
            }
            catch (BandException ex)
            {
                response = "Band Error Caught: " + ex.Message;
            }
            Debug.WriteLine(response);
            //await new Windows.UI.Popups.MessageDialog(response).ShowAsync();
            return response;
        }

        private void StartAccelerometer()
        {
            if (_accelerometer != null)
            {
                // Establish the report interval
                _accelerometer.ReportInterval = _desiredReportInterval;

                //Window.Current.VisibilityChanged += new WindowVisibilityChangedEventHandler(VisibilityChanged);
                _accelerometer.ReadingChanged += new TypedEventHandler<Accelerometer, AccelerometerReadingChangedEventArgs>(ReadingChanged);
            }
            else
            {
                lblStatus.Text = "No accelerometer found";
            }
        }

        private async void StartMSBand()
        {
            string response = await this.StartReadingGyroscopeAsync();

        }

        public async Task<String> StartReadingGyroscopeAsync()
        {
            string response = "";
            if (bandClient.SensorManager.Gyroscope.GetCurrentUserConsent() != UserConsent.Granted)
            {
                try
                {
                    Debug.WriteLine("Prompting for user consent");
                    //get the user's consent
                    var consent = await bandClient.SensorManager.Gyroscope.RequestUserConsentAsync();
                    Debug.WriteLine("User consent received for gyroscope access.");

                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error trying to get gyroscope access user consent.");
                    response = ex.Message;
                }
            }

            bandClient.SensorManager.Gyroscope.ReadingChanged += Gyroscope_ReadingChanged;

            //Should have user consent, now add event handler and start reading
            try
            {
                Debug.WriteLine("Beginning to read gyroscope data.");

                bandClient.SensorManager.Gyroscope.ReportingInterval = TimeSpan.FromMilliseconds(SamplingDelay);
                bool isReading = await bandClient.SensorManager.Gyroscope.StartReadingsAsync();
                response = "Success";
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception attempting to read Gyroscope data");
                response = ex.Message;
            }

            return response;
        }

        private void StopAccelerometer()
        {
            //Window.Current.VisibilityChanged -= new WindowVisibilityChangedEventHandler(VisibilityChanged);
            _accelerometer.ReadingChanged -= new TypedEventHandler<Accelerometer, AccelerometerReadingChangedEventArgs>(ReadingChanged);

            // Restore the default report interval to release resources while the sensor is not in use
            _accelerometer.ReportInterval = 0;
        }

        private async Task StopMSBand()
        {
            string response = await this.StopReadingGyroscopeAsync();

            bandClient.SensorManager.Gyroscope.ReadingChanged -= Gyroscope_ReadingChanged;

            Task.WaitAll(actions.ToArray());

        }

        public async Task<String> StopReadingGyroscopeAsync()
        {
            string response = "";
            try
            {
                await bandClient.SensorManager.Gyroscope.StopReadingsAsync();
                response = "Success";
            }
            catch (Exception ex)
            {
                response = "Error: " + ex.Message;
            }

            return response;
        }

        // Phone accelerometer readings event handler
        private async void ReadingChanged(object sender, AccelerometerReadingChangedEventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Sample s = new Sample();
                s.guid = currentMotion.id;

                AccelerometerReading reading = e.Reading;

                s.X = reading.AccelerationX;
                s.Y = reading.AccelerationY;
                s.Z = reading.AccelerationX;

                lblAccX.Text = String.Format("{0,5:0.00}", reading.AccelerationX);
                lblAccY.Text = String.Format("{0,5:0.00}", reading.AccelerationY);
                lblAccZ.Text = String.Format("{0,5:0.00}", reading.AccelerationZ);

                string text = string.Format("X = {0}\nY = {1}\nZ = {2}", reading.AccelerationX, reading.AccelerationY, reading.AccelerationZ);
                Debug.WriteLine(text);

                AzureHelper.SendToAzure(s);
                //currentMotion.Samples.Add(s);
            });
        }

        private List<Task> actions = new List<Task>();

        //MS Band readings event handler
        private async void Gyroscope_ReadingChanged(object sender, BandSensorReadingEventArgs<IBandGyroscopeReading> e)
        {
            if (!isSpoken)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                { 
                    mediaplayer.Play();
                    isSpoken = true;
                });
            }

            Sample s = new Sample();
            s.guid = currentMotion.id;

            IBandGyroscopeReading reading = e.SensorReading;

            s.X = reading.AccelerationX;
            s.Y = reading.AccelerationY;
            s.Z = reading.AccelerationX;
            s.gX = reading.AngularVelocityX;
            s.gY = reading.AngularVelocityY;
            s.gZ = reading.AngularVelocityZ;

            //bandClient.SensorManager.Gyroscope.

            //AzureHelper.SendToAzure(s);

            samples.Add(s);

            //actions.Add( Task.Run(()=> Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            //{
            //    lblAccX.Text = String.Format("{0,5:0.00}", reading.AccelerationX);
            //    lblAccY.Text = String.Format("{0,5:0.00}", reading.AccelerationY);
            //    lblAccZ.Text = String.Format("{0,5:0.00}", reading.AccelerationZ);
            //    lblGyroX.Text = String.Format("{0,5:0.00}", reading.AngularVelocityX);
            //    lblGyroY.Text = String.Format("{0,5:0.00}", reading.AngularVelocityY);
            //    lblGyroZ.Text = String.Format("{0,5:0.00}", reading.AngularVelocityZ);

            //    //string text = string.Format("X = {0}\nY = {1}\nZ = {2}", reading.AccelerationX, reading.AccelerationY, reading.AccelerationZ);
            //    //Debug.WriteLine(text);

            //    //currentMotion.Samples.Add(s);
            //})));
        }

        // Initializes the media player to tell Cortana to say "GO!" at the beginning of captures
        private async void InitTTS()
        {
            //Reminder: You need to enable the Microphone capabilitiy in Windows Phone projects
            //Reminder: Add this namespace in your using statements
            //using Windows.Media.SpeechSynthesis;

            // The media object for controlling and playing audio.
            mediaplayer = new MediaElement();
            mediaplayer.AutoPlay = false;

            // The object for controlling the speech synthesis engine (voice).
            using (var speech = new SpeechSynthesizer())
            {
                //Retrieve the first female voice
                speech.Voice = SpeechSynthesizer.AllVoices
                    .First(i => (i.Gender == VoiceGender.Female && i.Description.Contains("United States")));
                // Generate the audio stream from plain text.
                SpeechSynthesisStream stream = await speech.SynthesizeTextToStreamAsync("Go!");

                // Send the stream to the media object.
                mediaplayer.SetSource(stream, stream.ContentType);
            }
        }

        // Quickly adds Text-to-Speech to the app using Cortana's default voice
        private async Task ReadText(string message)
        {
            //Reminder: You need to enable the Microphone capabilitiy in Windows Phone projects
            //Reminder: Add this namespace in your using statements
            //using Windows.Media.SpeechSynthesis;

            // The media object for controlling and playing audio.
            mp = new MediaElement();
            //mp.AutoPlay = false;

            // The object for controlling the speech synthesis engine (voice).
            using (var speech = new SpeechSynthesizer())
            {
                //Retrieve the first female voice
                speech.Voice = SpeechSynthesizer.AllVoices
                    .First(i => (i.Gender == VoiceGender.Female && i.Description.Contains("United States")));
                // Generate the audio stream from plain text.
                SpeechSynthesisStream stream = await speech.SynthesizeTextToStreamAsync(message);

                // Send the stream to the media object.
                mp.SetSource(stream, stream.ContentType);
                mp.Play();
            }
        }

        private async Task InvokeMachineLearningService(MotionStats mstats)
        {
            using (var client = new HttpClient())
            {
                var scoreRequest = new
                {

                    Inputs = new Dictionary<string, StringTable>() {
                        {
                            "input1",
                            new StringTable()
                            {
                                ColumnNames = new string[] {"avgx", "sdevx", "avgy", "sdevy", "avgz", "sdevz", "avggx", "sdevgx", "avggy", "sdevgy", "avggz", "sdevgz", "quality"},
                                Values = new string[,] {  { mstats.avgX.ToString(), mstats.stddevX.ToString(), mstats.avgY.ToString(), mstats.stddevY.ToString(), mstats.avgZ.ToString(), mstats.stddevZ.ToString(), mstats.avggX.ToString(), mstats.stddevgX.ToString(), mstats.avggY.ToString(), mstats.stddevgY.ToString(), mstats.avggZ.ToString(), mstats.stddevgZ.ToString(), "" } }
                            }
                        },
                    },
                    GlobalParameters = new Dictionary<string, string>()
                    {
                    }
                };
                const string apiKey = "xOF+evxnVtrZYdK7RAOvpYgr8ahr0h525BdjK/cbugJX+H1BHlRJ4eJGO6bZKz6pCYwPNChnuyTcka5aylFE1A=="; // Replace this with the API key for the web service
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                client.BaseAddress = new Uri("https://ussouthcentral.services.azureml.net/workspaces/2cacee563a594d83b31ee012c5dbc178/services/22cd2277983a495a9d98e1fb100c1f48/execute?api-version=2.0&details=true");

                // WARNING: The 'await' statement below can result in a deadlock if you are calling this code from the UI thread of an ASP.Net application.
                // One way to address this would be to call ConfigureAwait(false) so that the execution does not attempt to resume on the original context.
                // For instance, replace code such as:
                //      result = await DoSomeTask()
                // with the following:
                //      result = await DoSomeTask().ConfigureAwait(false)


                HttpResponseMessage response = await client.PostAsJsonAsync("", scoreRequest);

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<JObject>( await response.Content.ReadAsStringAsync());

                    var qty = result["Results"]["output1"]["value"]["Values"][0][13].ToString();
                    string msg = string.Format("That was a {0} motion.", qty);

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        lblStatus.Text = msg;
                        
                    });
                    await ReadText(msg);

                    Debug.WriteLine("Result: {0}", result);
                }
                else
                {
                    Debug.WriteLine(string.Format("The request failed with status code: {0}", response.StatusCode));

                    // Print the headers - they include the requert ID and the timestamp, which are useful for debugging the failure
                    Debug.WriteLine(response.Headers.ToString());

                    string responseContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine(responseContent);
                }
            }
        }
    }

    public class StringTable
    {
        public string[] ColumnNames { get; set; }
        public string[,] Values { get; set; }
    }
}
