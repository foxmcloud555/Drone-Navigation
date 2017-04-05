# README #

This repo contains a C# project for tracking coloured objects and sending data to a python script, as well as a python script that revives data from the C# project and controls a Crazyflie quadcopter.  

### How do I get set up? ###

* Plug in your Kinect 2.0 and install the SDK
* ensure you have at least Python 3.4 installed
* you should use a solid blue object to track the Crazyflie, and a solid red object as your target. 

# Dependencies - 
*You will need cflib from Bitcraze, simply move the folder to whatever Python directory you are working in. You can find it here: https://github.com/bitcraze/crazyflie-lib-python 
*You will also need Pyusb in the same directory. You can find that here: https://github.com/walac/pyusb


# Deployment -

* Make sure the crazyflie is turned on and the radio is plugged in.
* Run the Kinect project first. It should run, but you will see nothing. This is normal, as nothing will launch until you have also run the associated python script. It must be done in this order. 
* Once you have run the Python script, the Crazyflie should connect to the radio 
*The Kinect user interface will now launch.
*It will begin in Crazyflie tracking mode. Adjust the crazyflie tracking sliders (the ones on the left) until your blue tracking object is the only white blob on screen, and everything else is black.
* In the drop down menu, switch to target tracking mode. Adjust the tracking sliders (on the right this time) until the target is the only white blob on screen. 
* You can now switch to tracker output, but be aware this will begin the flight routine. 
*The crazyflie will now move on a 2D plane towards the target!

### Troubleshooting ###

* ensure your working environment does not have a source of strong natural light in the view of the Kinect (i.e a window)