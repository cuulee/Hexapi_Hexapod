﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using HexapiBackground.Enums;
using HexapiBackground.Hardware;

// ReSharper disable FunctionNeverReturns
#pragma warning disable 4014

namespace HexapiBackground.IK
{
    internal class IkController
    {
        private int _perimeterInInches; 
        private int _leftInches;
        private int _centerInches;
        private int _rightInches;

        private double _yaw;
        private double _pitch;
        private double _roll;

        private Behavior _behavior = Behavior.Avoid;
        private bool _behaviorStarted;

        private readonly InverseKinematics _inverseKinematics;
        

        

        public static event EventHandler<PingEventData> CollisionEvent;

        private bool _isCollisionEvent;

        internal IkController(InverseKinematics inverseKinematics)
        {
            _inverseKinematics = inverseKinematics;
            _perimeterInInches = 18;

            _leftInches = _perimeterInInches + 5;
            _centerInches = _perimeterInInches + 5;
            _rightInches = _perimeterInInches + 5;
        }

        internal async Task Start()
        {
            _inverseKinematics.Start().ConfigureAwait(false);

            var arduino = await SerialDeviceHelper.GetSerialDevice("AH03FK33", 57600);
            var arduinoDataReader = new DataReader(arduino.InputStream) { InputStreamOptions = InputStreamOptions.Partial };

            while (true)
            {
                if (_behavior != Behavior.None)
                {
                    try
                    {
                        var incoming = await arduinoDataReader.LoadAsync(30);

                        if (incoming <= 0)
                            return;

                        var pingData = arduinoDataReader.ReadString(incoming);

                        if (string.IsNullOrEmpty(pingData))
                            return;

                        if (ParseRanges(pingData.Split('!')) <= 0)
                            return;

                        if (_leftInches <= _perimeterInInches || _centerInches <= _perimeterInInches || _rightInches <= _perimeterInInches)
                        {
                            await Display.Write($"{_leftInches} {_centerInches} {_rightInches}", 2);

                            _isCollisionEvent = true;

                            if (_behavior == Behavior.Avoid)
                            {
                                var e = CollisionEvent;
                                e?.Invoke(null, new PingEventData(_perimeterInInches, _leftInches, _centerInches, _rightInches));
                            }
                        }
                        else
                        {
                            if (!_isCollisionEvent)
                                return;

                            _isCollisionEvent = false;

                            if (_behavior != Behavior.Avoid)
                                return;

                            var e = CollisionEvent;
                            e?.Invoke(null, new PingEventData(_perimeterInInches, _leftInches, _centerInches, _rightInches));
                        }
                    }
                    catch
                    {
                        //
                    }
                }
            }
        }

        internal async Task StartReadYawPitchRoll()
        {
            var sparkFunRazorMpu = await SerialDeviceHelper.GetSerialDevice("DN01E09J", 57600);

            if (sparkFunRazorMpu == null)
                return;

            var razorDataWriter = new DataWriter(sparkFunRazorMpu.OutputStream);

            razorDataWriter.WriteString("#o0\r\n");
            await razorDataWriter.StoreAsync();

            await Task.Delay(1000);

            var razorDataReader = new DataReader(sparkFunRazorMpu.InputStream) { InputStreamOptions = InputStreamOptions.Partial };

            while (true)
            {
                try
                {
                    // #f - Request one output frame - useful when continuous output is disabled and updates are
                    // required in larger intervals only.Though #f only requests one reply, replies are still
                    // bound to the internal 20ms(50Hz) time raster.So worst case delay that #f can add is 19.99ms.
                    razorDataWriter.WriteString("#f\r\n");
                    await razorDataWriter.StoreAsync();
                   
                    //while (true)
                    //{
                    //    var inB = await razorDataReader.LoadAsync(1);

                    //    if (inB > 0)
                    //    {
                    //        if (razorDataReader.ReadString(1).Equals("#"))
                    //            break;
                    //    }
                    //}

                    var incoming = await razorDataReader.LoadAsync(30);

                    if (incoming <= 0)
                        return;

                    var yprData = razorDataReader.ReadString(incoming);

                    var i = yprData.IndexOf('Y');
                    var cri = yprData.LastIndexOf('\r');

                    if (i == -1 || cri == -1)
                        return;

                    var length = cri - i;

                    if (length < 20)
                        continue;

                    var substr = yprData.Substring(i, length);

                    yprData = substr.Replace("YPR=", "");

                    var yprArray = yprData.Split(',');

                    if (yprArray.Length >= 1)
                        double.TryParse(yprArray[0], out _yaw);

                    if (yprArray.Length >= 2)
                        double.TryParse(yprArray[1], out _pitch);

                    if (yprArray.Length >= 3)
                        double.TryParse(yprArray[2], out _roll);

                    Debug.WriteLine($"{_yaw} {_pitch} {_roll}", 1);
                }
                catch
                {
                    //
                }
            }
        }
      
        internal int ParseRanges(string[] ranges)
        {
            var success = ranges.Length;

            foreach (var d in ranges)
            {
                if (string.IsNullOrEmpty(d) || !d.Contains('?'))
                    continue;

                var data = d.Replace("?", "");

                try
                {
                    int ping;

                    if (data.Contains('L'))
                    {
                        data = data.Replace("L", "");

                        if (int.TryParse(data, out ping))
                            _leftInches = GetInchesFromPingDuration(ping);

                        continue;
                    }
                    if (data.Contains('C'))
                    {
                        data = data.Replace("C", "");

                        if (int.TryParse(data, out ping))
                            _centerInches = GetInchesFromPingDuration(ping);

                        continue;
                    }
                    if (data.Contains('R'))
                    {
                        data = d.Replace("R", "");

                        if (int.TryParse(data, out ping))
                            _rightInches = GetInchesFromPingDuration(ping);
                    }
                }
                catch
                {
                    success--;
                }
            }

            return success;
        }

        internal async void RequestBehavior(Behavior behavior, bool start)
        {
            _behavior = behavior;
            _behaviorStarted = start;

            switch (behavior)
            {
                case Behavior.Offensive:
                    break;
                case Behavior.Defensive:
                    break;
                case Behavior.Bounce:
                    await BehaviorBounce().ConfigureAwait(false);
                    break;
                default:
                    _behaviorStarted = false;

                    break;
            }
        }

        private async Task BehaviorBounce()
        {
            await Display.Write("Bounce started", 2);

            RequestSetMovement(true);
            RequestSetGaitType(GaitType.Tripod8);

            double travelLengthZ = 40;
            double travelLengthX = 0;
            double travelRotationY = 0;
            double gaitSpeed = 45;

            while (_behaviorStarted)
            {
                await Task.Delay(100);

                if (_leftInches > _perimeterInInches && _centerInches > _perimeterInInches && _rightInches > _perimeterInInches)
                {
                    await Display.Write("Forward", 2);

                    travelLengthZ = -50;
                    travelLengthX = 0;
                    travelRotationY = 0;
                }

                if (_leftInches <= _perimeterInInches && _rightInches > _perimeterInInches)
                {
                    await Display.Write("Turn Right", 2);

                    travelLengthZ = _centerInches > _perimeterInInches ? -20 : 0;

                    travelLengthX = 0;
                    travelRotationY = -30;
                }

                if (_leftInches > _perimeterInInches && _rightInches <= _perimeterInInches)
                {
                    await Display.Write("Turn Left", 2);

                    travelLengthZ = _centerInches > _perimeterInInches ? -20 : 0;

                    travelLengthX = 0;
                    travelRotationY = 30;
                }

                if (_leftInches <= _perimeterInInches && _rightInches <= _perimeterInInches)
                {
                    travelLengthX = 0;
                    travelRotationY = 0;

                    if (_centerInches < _perimeterInInches)
                    {
                        await Display.Write("Reverse", 2);

                        travelLengthZ = 30; //Reverse
                        RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);

                        await Task.Delay(2000);

                        await Display.Write("Turn Left", 2);
                        travelLengthZ = 0;
                        travelRotationY = 30;
                        RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);

                        await Task.Delay(2000);

                        await Display.Write("Stop", 2);

                        travelLengthZ = 0;
                        travelLengthX = 0;
                        travelRotationY = 0;

                        RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);
                        
                        continue;
                    }


                }
                
                RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gaitSpeed"></param>
        /// <param name="travelLengthX"></param>
        /// <param name="travelLengthZ">Negative numbers equals forward movement</param>
        /// <param name="travelRotationY"></param>
        internal void RequestMovement(double gaitSpeed, double travelLengthX, double travelLengthZ, double travelRotationY)
        {
            _inverseKinematics.RequestMovement(gaitSpeed, travelLengthX, travelLengthZ, travelRotationY);
        }

        internal void RequestBodyPosition(double bodyRotX1, double bodyRotZ1, double bodyPosX, double bodyPosZ, double bodyPosY, double bodyRotY1)
        {
            _inverseKinematics.RequestBodyPosition(bodyRotX1, bodyRotZ1, bodyPosX, bodyPosZ, bodyPosY, bodyRotY1);
        }

        internal void RequestSetGaitOptions(double gaitSpeed, double legLiftHeight)
        {
            _inverseKinematics.RequestSetGaitOptions(gaitSpeed, legLiftHeight);
        }

        internal void RequestSetGaitType(GaitType gaitType)
        {
            _inverseKinematics.RequestSetGaitType(gaitType);
        }

        internal async void RequestSetMovement(bool enabled)
        {
            _inverseKinematics.RequestSetMovement(enabled);
            await Display.Write(enabled ? "Servos on" : "Servos off", 2);
        }

        internal void RequestSetFunction(SelectedIkFunction selectedIkFunction)
        {
            _inverseKinematics.RequestSetFunction(selectedIkFunction);
        }

        internal async void RequestLegYHeight(int leg, double yPos)
        {
            _inverseKinematics.RequestLegYHeight(leg, yPos);
            await Display.Write($"Leg {leg} - {yPos}", 2);
        }

        internal async void RequestNewPerimeter(bool increase)
        {
            if (increase)
                _perimeterInInches++;
            else
                _perimeterInInches--;

            if (_perimeterInInches < 1)
                _perimeterInInches = 1;

            await Display.Write($"Perimeter {_perimeterInInches}", 1);
            await Display.Write($"{_leftInches} {_centerInches} {_rightInches}", 2);
        }

        private static int GetInchesFromPingDuration(int duration) //73.746 microseconds per inch
        {
            return Convert.ToInt32(Math.Round(duration / 73.746 / 2, 1));
        }
    }
}