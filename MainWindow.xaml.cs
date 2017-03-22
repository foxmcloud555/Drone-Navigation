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
        private MultiSourceFrameReader m_FrameReader = null;
        /*
            Opted for multi source frame reader to allow us to process depth and colour at the
            same time rather than seperatly as it simplifies the issue greatly.

            Originally we were going to go with infrared over colour due to it not being affected 
            by light in the same way as colour is, however the object colour is what we are 
            filtering by to track the objects as it is the more intuitive solution for our needs.
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
            m_kinectSensor = KinectSensor.GetDefault();
            m_FrameReader = m_kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color | FrameSourceTypes.Depth);

            // wire handler for frame arrival
            m_FrameReader.MultiSourceFrameArrived += this.Reader_FrameArrived;

            // set up the FrameDescriptions
            m_depthFrameDescription = this.m_kinectSensor.DepthFrameSource.FrameDescription;
            m_colourFrameDescription = this.m_kinectSensor.ColorFrameSource.FrameDescription;

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

        // INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        public event PropertyChangedEventHandler PropertyChanged;
        
        // Gets or sets the current status text to display
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
        
        // Execute shutdown tasks
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (m_FrameReader != null)
            {
                m_FrameReader.Dispose();
                m_FrameReader = null;
            }
            if (this.m_kinectSensor != null)
            {
                this.m_kinectSensor.Close();
                this.m_kinectSensor = null;
            }
        }

        // Handles the colour frame data arriving from the sensor
        private void Reader_FrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            // cannot use using with multisource frame
            MultiSourceFrame multiSourceFrame = e.FrameReference.AcquireFrame();
            if (multiSourceFrame != null)
            {
                using (ColorFrame colorFrame = multiSourceFrame.ColorFrameReference.AcquireFrame())
                {
                    if (colorFrame != null)
                    {
                        // Process the colour frame to get an rgba and hsv image for us to use
                        Image<Rgba, Byte> rgbImage = null;
                        Image<Hsv, Byte> hsvImage = null;
                        ProcessColourFrame(colorFrame, ref rgbImage, ref hsvImage);

                        // Filter the image to the calibration values of the Target Object
                        Mat thresholdMat = FilterHsvImage(hsvImage,
                            new Hsv(THueMinSlider.Value, TSatMinSlider.Value, TValMinSlider.Value),
                            new Hsv(THueMaxSlider.Value, TSatMaxSlider.Value, TValMaxSlider.Value));

                        // No need to process tracking during calibration...
                        if (ViewTypeComboBox.SelectedIndex == 0)
                        {
                            // coordinates of largest contour
                            int x = 0;
                            int y = 0;
                            // Track the target object retrieving the x and y coordinates
                            bool objectFound = TrackObjectOnImage(thresholdMat, ref x, ref y);
                            // found something then output it...
                            if (objectFound == true)
                            {
                                CvInvoke.Circle(rgbImage, new System.Drawing.Point(x, y), 20, new MCvScalar(0, 0, 255), 2);
                                CvInvoke.PutText(rgbImage, "Tracking Target", new System.Drawing.Point(x, y + 40), FontFace.HersheySimplex, 1, new MCvScalar(0, 0, 255), 2);
                                CvInvoke.PutText(rgbImage, x + "," + y, new System.Drawing.Point(x, y + 30), FontFace.HersheySimplex, 1, new MCvScalar(0, 0, 255), 2);
                            }

                            CvInvoke.Imshow("Output", rgbImage);
                        }
                        else if (ViewTypeComboBox.SelectedIndex == 1)
                        {
                            CvInvoke.Imshow("Output", thresholdMat);
                        }
                        else
                        {
                            CvInvoke.Imshow("Output", thresholdMat);
                        }
                    }
                }
            }   // end of multi-source frame.
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
        private Mat FilterHsvImage(Image<Hsv, Byte> hsvImage, Hsv lower, Hsv upper )
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
    }
}
