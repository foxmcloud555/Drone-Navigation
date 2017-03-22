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
    using Emgu.CV.CvEnum;
    using Emgu.CV.Util;
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
        
    }
}

