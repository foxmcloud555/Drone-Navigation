namespace Microsoft.Samples.Kinect.DepthBasics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    // To be abel to use the bitmap in this class.
    using System.Windows.Media.Imaging;

    // Image processing libraries
    using Emgu.CV;
    using Emgu.CV.Structure;
    /*
        OpenCV is an image processing library that we can use with c++ to process the image data collected 
        from the kinect. EMgu is a .NET wrapper that lets us use OpenCV funtions with our c# application.
    */

    /// <summary>
    /// Image manipulation and tracker processing
    /// </summary>
    class KinectTracker
    {
        /*
            Tracking objects via colour.

            Convert from RGB to Hue Saturation Value:
            - The converted colour range allow for easier filtering of different objects by colour.
            
            MinMax Threshold:
            - Filter the colours of interest to show only the range that will show the objects we are tracking.

            FilterNoise:
            - isolate results from any remaining artefacts we dont want.
        */

        // Filter values - maybe add some gui elements to add some calibration functionality to it.
        int HUE_MIN = 0;
        int HUE_MAX = 256;
        int SAT_MIN = 0;
        int SAT_MAX = 256;
        int VAL_MIN = 0;
        int VAL_MAX = 256;
        // max number of objects to be tracked
        const int MAX_TRACKED_OBJECTS = 50;

        /// <summary>
        /// Wrapper function to convert an image from RGB to Hue Saturation Value
        /// </summary>
        /// <param name="RGB_Bitmap">The RGB bitmap to convert</param>
        /// <param name="HSV_Bitmap">The resulting HSV bitmap to populate</param>
        public void Convert_RGB_HSV(WriteableBitmap RGB_Bitmap, WriteableBitmap HSV_Bitmap)
        {
            //Image<Gray, Byte> depthImage = new Image<Gray, Byte>([depthBitmap.PixelHeight, depthBitmap.pixelWidth, depthPixelData]);
            //Image<Hsv, Byte> imgHSV = imgRGB.Convert<Hsv, Byte>();
        }
        
    }
}

