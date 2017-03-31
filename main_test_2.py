import logging
import time
import threading
import struct
from threading import Thread

import cflib
from cflib.crazyflie import Crazyflie

logging.basicConfig(level=logging.ERROR)

class CrazyflieController:
    """Class that controls Crazyflies by recieving and parsing commands"""

    """ Initialize and run the example with the specified link_uri """

    # Constants to determine how the crazyflie will be controlled
    YAWRIGHT = -90
    YAWLEFT = 90
    UPTHRUST = 48000
    DOWNTHRUST = 40000
    HOVER = 40000
    FORWARDPITCH = -10
    BACKWARDPITCH = 10
    ROLLLEFT = 5
    ROLLRIGHT = -5

    INPUT_VAR = "mn"

    def __init__(self, link_uri):
        self.lock = threading.Lock()

        self._cf = Crazyflie()

        self._cf.connected.add_callback(self._connected)
        self._cf.disconnected.add_callback(self._disconnected)
        self._cf.connection_failed.add_callback(self._connection_failed)
        self._cf.connection_lost.add_callback(self._connection_lost)

        self._cf.open_link(link_uri)


        # Input variable to control the Crazyflie
        self._inputVar = ' '

        # Varaible to control the Crazyflie
        self._roll = 0
        self._yaw = 0
        self._pitch = 0
        self._thrust = 0

        print('Connecting to %s' % link_uri)

    def _connected(self, link_uri):
        """ This callback is called form the Crazyflie API when a Crazyflie
        has been connected and the TOCs have been downloaded."""

        # Start a separate thread to do the motor test.
        # Do not hijack the calling thread!
        Thread(target=self._flieLoop).start()

    def _connection_failed(self, link_uri, msg):
        """Callback when connection initial connection fails (i.e no Crazyflie
        at the specified address)"""
        print('Connection to %s failed: %s' % (link_uri, msg))

    def _connection_lost(self, link_uri, msg):
        """Callback when disconnected after a connection has been made (i.e
        Crazyflie moves out of range)"""
        print('Connection to %s lost: %s' % (link_uri, msg))

    # Callback when the Crazyflie is disconnected (called in all cases)
    def _disconnected(self, link_uri):
        print('Disconnected from %s' % link_uri)

    # Stop the crazyflie
    def _stop(self):
        self._roll = 0
        self._yaw = 0
        self._pitch = 0
        self._thrust = 0

        self._cf.commander.send_setpoint(self._roll, self._pitch, self._yaw, self._thrust)
        # Make sure that the last packet leaves before the link is closed
        # since the message queue is not flushed before closing
        time.sleep(0.1)
        self._cf.close_link()

    def _flieLoop(self):
        # Update the control variable
        self.lock.acquire()
        temp = self.INPUT_VAR
        self.lock.release()

        Thread(target=self._flieInputLoop).start()

        while temp != "ss":

            self._calibrate(temp)

            # Update the control variable
            self.lock.acquire()
            temp = self.INPUT_VAR
            self.lock.release()

        self._stop()

    def _calibrate(self, state):
        thrust_mult = 1
        thrust_step = 500
        yaw_mult = 1
        yaw_step = 10
        pitch_mult = 1
        pitch_step = 1
        roll_mult = 1
        roll_step = 0.5
        print(len(state))
        #print(state)

        udstate = state[0]
        lrstate = state[1]
        print(udstate)
        print(lrstate)
        # Hover state

        self._cf.commander.send_setpoint(self._roll, self._pitch, self._yaw, self._thrust)

        if udstate == 'h':
            print("hover")
            if self._thrust >= self.HOVER:
                thrust_mult = -1
            self._thrust += thrust_step * thrust_mult

        # Yaw Left
##        elif state == 'l':
##            if self ._yaw >= self.YAWLEFT:
##                yaw_mult = -1
##            self._yaw += yaw_step * yaw_mult

        # Yaw Right
##        elif state == 'r':
##            yaw_mult = -1
##            if self ._yaw <= self.YAWRIGHT:
##                yaw_mult = 1
##            self._yaw += yaw_step * yaw_mult

        # Rise Upwards
        elif udstate == 'u':
            print("up")
            if self._thrust >= self.UPTHRUST:
                thrust_mult = -1
            self._thrust += thrust_step * thrust_mult

        # Go Downwards
        elif udstate == 'd':
            print("down")
            thrust_mult = -1
            if self._thrust <= self.DOWNTHRUST:
                thrust_mult = 1
            self._thrust += thrust_step * thrust_mult

        # Pitch Forwards
##        elif state == 'f':
##            pitch_mult = -1
##            if self._pitch <= self.FORWARDPITCH:
##                pitch_mult = 1
##            self._pitch += pitch_step * pitch_mult

        # Pitch Backwards
##        elif state == 'b':
##            if self._pitch >= self.BACKWARDPITCH:
##                pitch_mult = -1
##            self._pitch += pitch_step * pitch_mult

        # Roll Left
        if lrstate == 'r':
            print("right")
            self._roll = -10
##            if self._thrust >= self.ROLLLEFT:
##                roll_mult = -1
##            self._roll += roll_step * roll_mu


        # Roll Right
        elif lrstate == 'l':
            print("LEFT")
            self._roll = 10
##            roll_mult = -1
##            if self._thrust <= self.ROLLRIGHT:
##                roll_mult = 1
##            self._roll += roll_step * roll_mult

        elif lrstate == 'b':
            print("balance")
            self._roll = 0
        self._cf.commander.send_setpoint(self._roll, self._pitch, self._yaw, self._thrust)

        time.sleep(0.1)

    def _flieInputLoop(self):
        #Establish a connection between the flie and the kinect
        kinectPipe = open(r'\\.\pipe\CrazyPipe', 'r+b', 0)

        while self.INPUT_VAR != 's':
            # Old code to read the input directly from the user
            #result = input("Enter command: ")

            temp = kinectPipe.read(2)                    # Read the characters
            kinectPipe.seek(0)

            # Convert from binary to ascii char
            result = temp.decode("utf-8")


          #  print(result);
            # If the data is different from the last command sent
            if result != self.INPUT_VAR:
                self.lock.acquire()
                self.INPUT_VAR = result
                self.lock.release()

if __name__ == '__main__':
    # Initialize the low-level drivers (don't list the debug drivers)
    cflib.crtp.init_drivers(enable_debug_driver=False)
    # Scan for Crazyflies and use the first one found
    print('Scanning interfaces for Crazyflies...')
    available = cflib.crtp.scan_interfaces()
    print('Crazyflies found:')
    for i in available:
        print(i[0])

    if len(available) > 0:
        le = CrazyflieController(available[0][0])
    else:
        print('No Crazyflies found, cannot run example')
