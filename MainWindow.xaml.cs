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
                    byte[] pixels = new byte[width * height * ((PixelFormats.Bgr32.BitsPerPixel + 7) / 8)];
                    colorFrame.CopyConvertedFrameDataToArray(pixels, ColorImageFormat.Rgba);

                    m_colourBitmap.WritePixels(
                      new Int32Rect(0, 0, m_colourBitmap.PixelWidth, m_colourBitmap.PixelHeight),
                      pixels,
                      m_colourBitmap.PixelWidth * sizeof(int),
                      0);

                    //Bitmap matrix
                    Image<Rgba, Byte> image = new Image<Rgba, Byte>(width, height); //specify the width and height here
                    image.Bytes = pixels; //your byte array

                    //            Mat bitmapMat = new Mat(m_colourBitmap.PixelHeight, m_colourBitmap.PixelWidth, Emgu.CV.CvEnum.DepthType.Cv32F, 4);
                    //            m_colourBitmap.CopyPixels(Int32Rect.Empty, bitmapMat.DataPointer, bitmapMat.Step * bitmapMat.Rows, bitmapMat.Step);

                    // convert frame from RGB to HSV colorspace
                    Bitmap bmp = image.ToBitmap();
                    Image<Hsv, Byte> hsvImage = new Image<Hsv, byte>(bmp);

                    // filter HSV imageand store filtered image to threshold matrix
                    //              Mat threshold;
                    //              CvInvoke.InRange(hsvImage, ,, threshold);

                    // eliminate noise to emphasize the filtered objects

                    // We can now pass this filtered result to the tracker
                    // we will only be able to get x,y data from this. but thats why we
                    // kept in the depth buffer, we may be able to cross reference and
                    // extract the depth value to get our z coordinate.

                    CvInvoke.Imshow("Output", hsvImage.Mat);
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
