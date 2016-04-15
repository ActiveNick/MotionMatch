# MotionMatch
MotionMatch is a smart sports training system that allows users to record motions, such as a baseball throw, a tennis topspin, a golf drive, bowling throw, etc. In recording mode, the data is captured using a Microsoft Band v2 and a Windows 10 UP app running on a Lumia phone. The data is sent to the cloud via an Azure IoT Hub, and then processed by Azure Streaming Analytics (ASA) before being stored in an Azure Storage Blob.

From there, the data was imported into Azure ML Studio to train a model based on the average and standard deviation of the accelerometer axes & gyroscope axes (X, Y, Z for each) and standard deviations of all 6 values. The system samples at approximately 32 Hz. The model is then exposed as a Web Service.

In training mode, the app lets the user go through the same motion, and then checks the recorded motion against the Azure ML model, telling the user if that was a “Good” or “Bad” motion, showing the results on the phone screen and using a text-to-speech voice prompt.

This demo application was built by Nick Landry (@ActiveNick), Jesus Aguilar (@giventocode), James Quick (@jamesqquick) and Dan Stolts (@ITProGuru) from Microsoft as part of a 2-day internal hackathon in NYC on April 13-14 2016.
