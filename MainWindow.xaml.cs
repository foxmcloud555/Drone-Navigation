//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.DepthBasics
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Drawing;
    using Microsoft.Kinect;
    using System.IO.Pipes;
    using System.Text;
    using System.Threading;

    //using ZeroMQ;
    // Image processing libraries
    using Emgu.CV;
    using Emgu.CV.Structure;
    using Emgu.CV.CvEnum;
    using Emgu.CV.Util;

    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor m_kinectSensor = null;

        /// <summary>
        /// Readers for sensor frames
        /// </summary>
        private DepthFrameReader m_depthFrameReader = null;
        private ColorFrameReader m_rgbFrameReader = null;
        /*
            Originally we were going to go with infrared due to it not being affected by light 
            in the same way as colour is, however the object colour is what we are filtering 
            by to track the objects as it is the more intuitive solution.
        */

        // Colour bitmap to be recieved by the kinect
        private FrameDescription m_colourFrameDescription = null;
        private WriteableBitmap m_colourBitmap = null;

        // Depth for when we need it
        private FrameDescription m_depthFrameDescription = null;

        // The named pipe to communicate with the crazyflie
        private NamedPipeServerStream kinectCrazyFliePipe = new NamedPipeServerStream("CrazyPipe");

        private BinaryWriter kinectCrazyFlieWriter = null;
        // private BinaryWriter kinectCrazyFlieWriter2 = null;

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string m_statusText = null;

        char command, command2;
        bool started = false;
        /// <summary>
        /// Initialises a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            // get the kinectSensor object and open the frame readers
            this.m_kinectSensor = KinectSensor.GetDefault();
            this.m_depthFrameReader = this.m_kinectSensor.DepthFrameSource.OpenReader();
            this.m_rgbFrameReader = this.m_kinectSensor.ColorFrameSource.OpenReader();

            // wire handler for frame arrival
            this.m_depthFrameReader.FrameArrived += this.Reader_DepthFrameArrived;
            this.m_rgbFrameReader.FrameArrived += this.Reader_ColourFrameArrived;

            // set up the FrameDescriptions
            this.m_depthFrameDescription = this.m_kinectSensor.DepthFrameSource.FrameDescription;
            this.m_colourFrameDescription = this.m_kinectSensor.ColorFrameSource.FrameDescription;

            // create the bitmap
            this.m_colourBitmap = new WriteableBitmap(this.m_colourFrameDescription.Width, this.m_colourFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);

            // configer sensor
            this.m_kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;
            this.m_kinectSensor.Open();

            // set the status text
            this.StatusText = this.m_kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.NoSensorStatusText;


            this.DataContext = this;
            this.InitializeComponent();

            // Wait for a connection from the python script
            //Debug.Content = "Waiting For Connection";
            kinectCrazyFliePipe.WaitForConnection();

            // Connection established; open the pipeline to write
            // Debug.Content = "Connected";
            kinectCrazyFlieWriter = new BinaryWriter(kinectCrazyFliePipe);
            //   kinectCrazyFlieWriter2 = new BinaryWriter(kinectCrazyFliePipe);
        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get { return this.m_statusText; }

            set
            {
                if (this.m_statusText != value)
                {
                    this.m_statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.m_depthFrameReader != null)
            {
                this.m_depthFrameReader.Dispose();
                this.m_depthFrameReader = null;
            }
            if (this.m_rgbFrameReader != null)
            {
                this.m_rgbFrameReader.Dispose();
                this.m_rgbFrameReader = null;
            }
            if (this.m_kinectSensor != null)
            {
                this.m_kinectSensor.Close();
                this.m_kinectSensor = null;
            }

            // Shutdown the crazyflie
            SendCommandToCrazyFlie('s', 's');

        }

        /// <summary>
        /// Handles the depth frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_DepthFrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            using (DepthFrame depthFrame = e.FrameReference.AcquireFrame())
            {
                if (depthFrame != null)
                {
                    // the fastest way to process the body index data is to directly access 
                    // the underlying buffer
                    using (Microsoft.Kinect.KinectBuffer depthBuffer = depthFrame.LockImageBuffer())
                    {
                        // verify data
                        if (((this.m_depthFrameDescription.Width * this.m_depthFrameDescription.Height) == (depthBuffer.Size / this.m_depthFrameDescription.BytesPerPixel)) &&
                            (this.m_depthFrameDescription.Width == this.m_colourBitmap.PixelWidth) && (this.m_depthFrameDescription.Height == this.m_colourBitmap.PixelHeight))
                        {
                            // Note: In order to see the full range of depth (including the less reliable far field depth)
                            // we are setting maxDepth to the extreme potential depth threshold
                            ushort maxDepth = ushort.MaxValue;

                            // If you wish to filter by reliable depth distance, uncomment the following line:
                            maxDepth = depthFrame.DepthMaxReliableDistance;

                            // process depth...
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handles the colour frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_ColourFrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            using (ColorFrame colorFrame = e.FrameReference.AcquireFrame())
            {
                if (colorFrame != null)
                {
                    // Process the colour frame to get an rgba and hsv image for us to use
                    Image<Rgba, Byte> rgbImage = null;
                    Image<Hsv, Byte> hsvImage = null;
                    ProcessColourFrame(colorFrame, ref rgbImage, ref hsvImage);

                    // Filter the image to the calibration values of the Target Object
                    Mat targetThresholdMat = FilterHsvImage(hsvImage,
                        new Hsv(THueMinSlider.Value, TSatMinSlider.Value, TValMinSlider.Value),
                        new Hsv(THueMaxSlider.Value, TSatMaxSlider.Value, TValMaxSlider.Value));

                    // Filter the image to the calibration values of the Target Object
                    Mat crazyFlyThresholdMat = FilterHsvImage(hsvImage,
                        new Hsv(CFHueMinSlider.Value, CFSatMinSlider.Value, CFValMinSlider.Value),
                        new Hsv(CFHueMaxSlider.Value, CFSatMaxSlider.Value, CFValMaxSlider.Value));

                    // No need to process tracking during calibration...
                    if (ViewTypeComboBox.SelectedIndex == 0)
                    {
                        // Track the target object
                        // coordinates of largest contour
                        int tx = 0;
                        int ty = 0;
                        // Track the target object retrieving the x and y coordinates
                        bool tObjectFound = TrackObjectOnImage(targetThresholdMat, ref tx, ref ty);
                        // found something then output it...
                        if (tObjectFound == true)
                        {
                            CvInvoke.Circle(rgbImage, new System.Drawing.Point(tx, ty), 20, new MCvScalar(0, 0, 255), 2);
                            CvInvoke.PutText(rgbImage, "Tracking Target", new System.Drawing.Point(tx, ty + 40), FontFace.HersheySimplex, 1, new MCvScalar(0, 0, 255), 2);
                            CvInvoke.PutText(rgbImage, tx + "," + ty, new System.Drawing.Point(tx, ty + 30), FontFace.HersheySimplex, 1, new MCvScalar(0, 0, 255), 2);
                        }

                        // Track the crazyfly object
                        int cfx = 0;
                        int cfy = 0;
                        bool cgObjectFound = TrackObjectOnImage(crazyFlyThresholdMat, ref cfx, ref cfy);
                        if (cgObjectFound == true)
                        {
                            CvInvoke.Circle(rgbImage, new System.Drawing.Point(cfx, cfy), 20, new MCvScalar(0, 255, 0), 2);
                            CvInvoke.PutText(rgbImage, "Tracking Crazyfly", new System.Drawing.Point(cfx, cfy + 40), FontFace.HersheySimplex, 1, new MCvScalar(0, 255, 0), 2);
                            CvInvoke.PutText(rgbImage, cfx + "," + cfy, new System.Drawing.Point(cfx, cfy + 30), FontFace.HersheySimplex, 1, new MCvScalar(0, 255, 0), 2);
                        }

                        // output results
                        CvInvoke.Imshow("Output", rgbImage);

                        // CRAZYFLY COMMUNICATION HERE - at this point we will lose track of local scope, alt we could store it...

                        // Navigate the craxyflie to the target
                        if (!started)
                        {
                            // SendCommandToCrazyFlie('u');
                            //  Thread.Sleep(1000);
                            SendCommandToCrazyFlie('h', 'b');
                            //Thread.Sleep(500);

                            started = true;
                        }
                        else
                        {
                            MoveCrazyFlie(cfx, cfy, tx, ty);
                        }
                    }
                    else if (ViewTypeComboBox.SelectedIndex == 1)
                    {
                        CvInvoke.Imshow("Output", crazyFlyThresholdMat);
                    }
                    else
                    {
                        CvInvoke.Imshow("Output", targetThresholdMat);
                    }
                }
            }
        }

        private bool MoveCrazyFlie(int cfx, int cfy, int tx, int ty)
        {
            // Test using roll commands
            //// Move to the left
            //if (cfx < tx)
            //{
            //    if (command != 'p')
            //    {
            //        command = 'p';
            //        SendCommandToCrazyFlie(command);
            //    }
            //}
            //// Move to the right
            //else if (cfx > tx)
            //{
            //    if (command != 's')
            //    {
            //        command = 's';
            //        SendCommandToCrazyFlie(command);
            //    }
            //}

            // Test using thrust
            if (cfy < ty)
            {
                if (command != 'd')
                    command = 'd';
            }
            else if (cfy > ty)
            {
                if (command != 'u')
                    command = 'u';
            }
            else
            {
                if (command != 'h')
                    command = 'h';
            }

            if (cfx > tx)
            {
                if (command2 != 'r')
                    command2 = 'r';
            }

            else if (cfx < tx)
            {
                if (command2 != 'l')
                    command2 = 'l';
            }
            else
            {
                if (command2 != 'b')
                    command2 = 'b';
            }


            SendCommandToCrazyFlie(command, command2);

            // Craxyflie had reached the target
            return true;
        }

        // Process the provided colour frame and populate the given rgba and hsv images for us to use
        private void ProcessColourFrame(ColorFrame colorFrame, ref Image<Rgba, Byte> rgbaImage, ref Image<Hsv, Byte> hsvImage)
        {
            int width = colorFrame.FrameDescription.Width;
            int height = colorFrame.FrameDescription.Height;
            PixelFormat format = PixelFormats.Bgr32;

            byte[] pixels = new byte[width * height * ((format.BitsPerPixel + 7) / 8)];
            colorFrame.CopyConvertedFrameDataToArray(pixels, ColorImageFormat.Bgra);

            // Create an image from the frame at its native scale
            Image<Rgba, Byte> image = new Image<Rgba, Byte>(width, height);
            image.Bytes = pixels;

            // Rescale the frame by turning it to a bitmap
            Bitmap bmp = image.ToBitmap(width / 2, height / 2);

            // "Save" the rescaled bitmap as a modifyable image
            rgbaImage = new Image<Rgba, byte>(bmp);

            // convert frame from RGB to HSV colorspace
            hsvImage = new Image<Hsv, byte>(bmp);
        }

        // Filter the image to the calibration values returning a threshold mat
        // Takes an image and lower / upper threshold values to filter by, returning the resulting threshold mat
        private Mat FilterHsvImage(Image<Hsv, Byte> hsvImage, Hsv lower, Hsv upper)
        {
            // filter HSV image using calibration values from the GUI
            // http://docs.opencv.org/master/da/d97/tutorial_threshold_inRange.html
            // http://www.emgu.com/wiki/files/2.0.0.0/html/07eff70b-f81f-6313-98a9-02d508f7c7e0.htm
            //
            // using the upper and lower threshholds, store the resulting filtered image into threshold matrix
            Mat thresholdMat = hsvImage.InRange(lower, upper).Mat;

            // eliminate noise to emphasize the filtered objects
            // through testing, 2 iterations is sufficient number of passes to get desired results whilst
            // no lower the framerate significantly, 4 saw significantly slower times. Bright lights from
            // windows also are unavoidable, so minimise that in the test environments. not an outdoor solution.
            const int iterations = 2; // the number of iterations in which want to erode and dilate the mat.
            CvInvoke.Erode(thresholdMat, thresholdMat, null, new System.Drawing.Point(-1, -1), iterations, BorderType.Constant, CvInvoke.MorphologyDefaultBorderValue);
            CvInvoke.Dilate(thresholdMat, thresholdMat, null, new System.Drawing.Point(-1, -1), iterations, BorderType.Constant, CvInvoke.MorphologyDefaultBorderValue);

            return thresholdMat;
        }

        // Track the object using a threshold matrix, retrieve the x and y coordinates, return if any objects have been found.
        private bool TrackObjectOnImage(Mat threshold, ref int x, ref int y)
        {
            // we will only be able to get x,y data from this. but thats why we
            // kept in the depth buffer, we may be able to cross reference and
            // extract the depth value to get our z coordinate.

            //find contours of filtered image using openCV findContours function
            Mat hierarchy = threshold;
            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(hierarchy, contours, hierarchy, RetrType.Ccomp, ChainApproxMethod.ChainApproxSimple);

            //use moments method to find our filtered object
            // current largest area...
            double largestArea = 0;
            // only output if object found...
            bool objectFound = false;

            // iterate all the contours to find the largest area one.
            for (int i = 0; i < contours.Size; i++)
            {
                // This is what we use to get the centroid and outline...
                MCvMoments moment = CvInvoke.Moments(contours[i]);
                // Find the area of contour
                // This is what we will use to work out if we have a valid object...
                double area = moment.M00;
                if (area > largestArea)
                {
                    x = (int)(moment.M10 / area);
                    y = (int)(moment.M01 / area);
                    largestArea = area;
                    objectFound = true;
                }
                else objectFound = false;
            }
            return objectFound;
        }

        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            this.StatusText = this.m_kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.SensorNotAvailableStatusText;
        }

        // Send commands from the kinect program to the crazyflie program
        private void SendCommandToCrazyFlie(char command, char command2)
        {
            var buffer = Encoding.ASCII.GetBytes(command.ToString() + command2.ToString());   // Get the ASCII byte array
                                                                        // var buffer2 = Encoding.ASCII.GetBytes(yVal);
            kinectCrazyFlieWriter.Write(buffer);                        // Write the command

        }
    }
}
