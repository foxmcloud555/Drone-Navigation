# README #

This repo contains the following:

1. C# project for tracking coloured objects and sending instructional data to a python script
2. Python script that revives instructional data from the C# project and controls a Crazyflie quadcopter.  

### How do I get set up? ###

* Plug in your Kinect 2.0 and install the SDK
* ensure you have at least Python 3.4 installed
* you should use a solid blue object to track the Crazyflie, and a solid red object as your target. 

# Dependencies  
* You will need cflib from Bitcraze, simply move the folder to whatever Python directory you are working in. You can find it here: https://github.com/bitcraze/crazyflie-lib-python 
* You will also need Pyusb in the same directory. You can find that here: https://github.com/walac/pyusb


# Deployment -

* Make sure the crazyflie is turned on and the radio is plugged in.
* Run the Kinect project first. It will run but you will see nothing as nothing will launch until you have also run the associated python script. It must be done in this order. 
* Once you have run the Python script, the Crazyflie should connect to the radio and the Kinect project will now launch.
* It will begin in Crazyflie calibration mode. Calibrate the crazyflie tracking object (blue) using the sliders until your tracking object is the only white blob on screen.
* In the drop down menu, switch to target calibration mode. Repeat the process for this (red) object. 
* You can now switch to tracker output, but be aware this will begin the flight routine. 
* The crazyflie will now move on a 2D plane towards the target!

### Troubleshooting ###

* ensure your working environment does not have a source of strong natural light in the view of the Kinect (i.e a window).
* ensure that the crazyflie is flat and stationary during the project start up, as this may interfere with its internal calibration.
* ensure that the tracking objects do not get too close to the Kinect.