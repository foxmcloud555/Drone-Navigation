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
        private FrameDescription    m_colourFrameDescription = null;
        private WriteableBitmap     m_colourBitmap = null;

        // Depth for when we need it
        private FrameDescription m_depthFrameDescription = null;

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string m_statusText = null;

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
        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
 //       public ImageSource ImageSource
//        {
 //           get { return this.m_colourBitmap; }
 //       }

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
                    int width = colorFrame.FrameDescription.Width;
                    int height = colorFrame.FrameDescription.Height;
                    PixelFormat format = PixelFormats.Bgr32;

                    byte[] pixels = new byte[width * height * ((format.BitsPerPixel + 7) / 8)];
                    colorFrame.CopyConvertedFrameDataToArray(pixels, ColorImageFormat.Bgra);

                    // The code to load the unmodified image into bitmap
                    //m_colourBitmap.WritePixels(
                    //  new Int32Rect(0, 0, m_colourBitmap.PixelWidth, m_colourBitmap.PixelHeight),
                    //  pixels,
                    //  m_colourBitmap.PixelWidth * sizeof(int),
                    //  0);

                    //Bitmap matrix
                    Image<Rgba, Byte> image = new Image<Rgba, Byte>(width, height); //specify the width and height here
                    image.Bytes = pixels; //your byte array

                    //            Mat bitmapMat = new Mat(m_colourBitmap.PixelHeight, m_colourBitmap.PixelWidth, Emgu.CV.CvEnum.DepthType.Cv32F, 4);
                    //            m_colourBitmap.CopyPixels(Int32Rect.Empty, bitmapMat.DataPointer, bitmapMat.Step * bitmapMat.Rows, bitmapMat.Step);

                    // convert frame from RGB to HSV colorspace
                    Bitmap bmp = image.ToBitmap(width / 2, height / 2);
                    Image<Hsv, Byte> hsvImage = new Image<Hsv, byte>(bmp);

                    // filter HSV image using calibration values from the GUI
                    // http://docs.opencv.org/master/da/d97/tutorial_threshold_inRange.html
                    // http://www.emgu.com/wiki/files/2.0.0.0/html/07eff70b-f81f-6313-98a9-02d508f7c7e0.htm
                    //
                    // get the upper and lower threshholds
                    Hsv lower = new Hsv(THueMinSlider.Value, TSatMinSlider.Value, TValMinSlider.Value);
                    Hsv upper = new Hsv(THueMaxSlider.Value, TSatMaxSlider.Value, TValMaxSlider.Value);
                    // store the resulting filtered image into threshold matrix
                    Mat thresholdMat = hsvImage.InRange(lower, upper).Mat;

                    // eliminate noise to emphasize the filtered objects
                    // through testing, 2 iterations is sufficient number of passes to get desired results whilst
                    // no lower the framerate significantly, 4 saw significantly slower times. Bright lights from
                    // windows also are unavoidable, so minimise that in the test environments. not an outdoor solution.
                    const int iterations = 2; // the number of iterations in which want to erode and dilate the mat.
                    CvInvoke.Erode(thresholdMat, thresholdMat, null, new System.Drawing.Point(-1, -1), iterations, BorderType.Constant, CvInvoke.MorphologyDefaultBorderValue);
                    CvInvoke.Dilate(thresholdMat, thresholdMat, null, new System.Drawing.Point(-1, -1), iterations, BorderType.Constant, CvInvoke.MorphologyDefaultBorderValue);

                    // We can now pass this filtered result to the tracker
                    // we will only be able to get x,y data from this. but thats why we
                    // kept in the depth buffer, we may be able to cross reference and
                    // extract the depth value to get our z coordinate.

                    //find contours of filtered image using openCV findContours function
                    Mat hierarchy = new Mat();
                    VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
                    CvInvoke.FindContours(thresholdMat, contours, hierarchy, RetrType.Ccomp, ChainApproxMethod.ChainApproxSimple);
                    
                    //use moments method to find our filtered object
                    // current largest area...
                    double refArea = 0;
                    // only output if object found...
                    bool objectFound = false;
                    // could store the index of the largest so only get the moment once, will
                    // save time as long as we onyl want to track one obj. prefering the largest
                    // to isolate from any remnant noise...
                    // iterate all the contours to find the largest area one.
                    for (int index = 0; index >= contours.Size; index++)
                    {

                        // This is what we use to get the centroid and outline...
                        Moments moment = moments((cv::Mat)contours[index]);
                        // This is what we will use to work out if we have a valid object...
                        double area = moment.m00;
                        
                        if (area > MIN_OBJECT_AREA && area < MAX_OBJECT_AREA && area > refArea)
                        {
                            x = moment.m10 / area;
                            y = moment.m01 / area;
                            objectFound = true;
                            refArea = area;
                        }
                        else objectFound = false;
                    }
                    // Display the tracking info on the screen.
                    // also at this point we should probably notify the crazyfly of the positional data.
                    // that is if we want a ping every frame. alternatively could hold the data and send
                    // it upon request...
                    if (objectFound == true)
                    {
                        // camera feed is the original image so we will have to change that...
                        putText(cameraFeed, "Tracking Object", Point(0, 50), 2, 1, Scalar(0, 255, 0), 2);
                        drawObject(x, y, cameraFeed);
                    }

                    CvInvoke.Imshow("Output", thresholdMat);
                }
            }
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
    }
}
