﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Enumeration;
using Windows.Devices.HumanInterfaceDevice;
using Windows.Devices.SerialCommunication;
using Windows.Media.SpeechSynthesis;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;

#pragma warning disable 4014

namespace HexapiBackground
{
    internal sealed class Hexapi
    {
        private XboxController _xboxController;
        private SerialDevice _serialPort;
        private DataWriter _dataWriter;
        
        private readonly Stopwatch _sw = new Stopwatch(); 
        private SelectedFunction _selectedFunction;
        private bool _isMovementStarted;

        private enum SelectedFunction
        {
            GaitSpeed,
            LegHeight,
            TranslateHorizontal,
            Translate3D
        }
        
        #region Inverse Kinematics setup
        private const short CRrFemurHornOffset1 = 0;
        private const short CRmFemurHornOffset1 = 0;
        private const short CRfFemurHornOffset1 = 0;
        private const short CLrFemurHornOffset1 = 0;
        private const short CLmFemurHornOffset1 = 0;
        private const short CLfFemurHornOffset1 = 0;

        private const int CPfConst = 592; //old 650 ; 900*(1000/cPwmDiv)+cPFConst must always be 1500
        private const int CPwmDiv = 991; //old 1059, new 991;

        private const double CTravelDeadZone = 1;

        private const double C2Dec = 100;
        private const double C4Dec = 10000;
        private const double C6Dec = 1000000;

        private const int CLf = 5;
        private const int CLm = 4;
        private const int CLr = 3;
        private const int CRf = 2;
        private const int CRm = 1;
        private const int CRr = 0;

        //All legs being equal, all legs will have the same values
        private const int CoxaMin = -610; //-650 
        private const int CoxaMax = 610; //650
        private const int FemurMin = -2900; //-1050
        private const int FemurMax = 2900; //150
        private const int TibiaMin = -2900; //-450
        private const int TibiaMax = 2900; //350

        private const int CRrCoxaMin1 = CoxaMin; //Mechanical limits of the Right Rear Leg
        private const int CRrCoxaMax1 = CoxaMax;
        private const int CRrFemurMin1 = FemurMin;
        private const int CRrFemurMax1 = FemurMax;
        private const int CRrTibiaMin1 = TibiaMin;
        private const int CRrTibiaMax1 = TibiaMax;
        private const int CRmCoxaMin1 = CoxaMin; //Mechanical limits of the Right Middle Leg
        private const int CRmCoxaMax1 = CoxaMax;
        private const int CRmFemurMin1 = FemurMin;
        private const int CRmFemurMax1 = FemurMax;
        private const int CRmTibiaMin1 = TibiaMin;
        private const int CRmTibiaMax1 = TibiaMax;
        private const int CRfCoxaMin1 = CoxaMin; //Mechanical limits of the Right Front Leg
        private const int CRfCoxaMax1 = CoxaMax;
        private const int CRfFemurMin1 = FemurMin;
        private const int CRfFemurMax1 = FemurMax;
        private const int CRfTibiaMin1 = TibiaMin;
        private const int CRfTibiaMax1 = TibiaMax;
        private const int CLrCoxaMin1 = CoxaMin; //Mechanical limits of the Left Rear Leg
        private const int CLrCoxaMax1 = CoxaMax;
        private const int CLrFemurMin1 = FemurMin;
        private const int CLrFemurMax1 = FemurMax;
        private const int CLrTibiaMin1 = TibiaMin;
        private const int CLrTibiaMax1 = TibiaMax;
        private const int CLmCoxaMin1 = CoxaMin; //Mechanical limits of the Left Middle Leg
        private const int CLmCoxaMax1 = CoxaMax;
        private const int CLmFemurMin1 = FemurMin;
        private const int CLmFemurMax1 = FemurMax;
        private const int CLmTibiaMin1 = TibiaMin;
        private const int CLmTibiaMax1 = TibiaMax;
        private const int CLfCoxaMin1 = CoxaMin; //Mechanical limits of the Left Front Leg
        private const int CLfCoxaMax1 = CoxaMax;
        private const int CLfFemurMin1 = FemurMin;
        private const int CLfFemurMax1 = FemurMax;
        private const int CLfTibiaMin1 = TibiaMin;
        private const int CLfTibiaMax1 = TibiaMax;

        private const double CRrCoxaAngle1 = -450;
        private const double CRmCoxaAngle1 = 0;
        private const double CRfCoxaAngle1 = 450;
        private const double CLrCoxaAngle1 = -450;
        private const double CLmCoxaAngle1 = 0;
        private const double CLfCoxaAngle1 = 450;

        private const double CRfOffsetX = -70;
        private const double CRfOffsetZ = -120;
        private const double CLfOffsetZ = -120;
        private const double CLfOffsetX = 70;
        private const double CRrOffsetZ = 120;
        private const double CRrOffsetX = -70;
        private const double CLrOffsetZ = 120;
        private const double CLrOffsetX = 70;
        private const double CRmOffsetX = -120;
        private const double CRmOffsetZ = 0;
        private const double CLmOffsetX = 120;
        private const double CLmOffsetZ = 0;

        private const double CoxaLength = 30; //30
        private const double FemurLength = 74; //60
        private const double TibiaLength = 140; //70
        private const double CRrCoxaLength = CoxaLength;
        private const double CRmCoxaLength = CoxaLength;
        private const double CRfCoxaLength = CoxaLength;
        private const double CLrCoxaLength = CoxaLength;
        private const double CLmCoxaLength = CoxaLength;
        private const double CLfCoxaLength = CoxaLength;
        private const double CRrFemurLength = FemurLength;
        private const double CRmFemurLength = FemurLength;
        private const double CRfFemurLength = FemurLength;
        private const double CLrFemurLength = FemurLength;
        private const double CLmFemurLength = FemurLength;
        private const double CLfFemurLength = FemurLength;
        private const double CLfTibiaLength = TibiaLength;
        private const double CLmTibiaLength = TibiaLength;
        private const double CLrTibiaLength = TibiaLength;
        private const double CRfTibiaLength = TibiaLength;
        private const double CRmTibiaLength = TibiaLength;
        private const double CRrTibiaLength = TibiaLength;

        private const double CHexInitXz = 105;
        private const double CHexInitXzCos45 = 74; // COS(45) = .7071
        private const double CHexInitXzSin45 = 94; // sin(45) = .7071
        private const double CHexInitY = 36;

        private const double CRfInitPosX = CHexInitXzCos45;
        private const double CRfInitPosY = CHexInitY;
        private const double CRfInitPosZ = -CHexInitXzSin45;
        private const double CLrInitPosX = CHexInitXzCos45;
        private const double CLrInitPosY = CHexInitY;
        private const double CLrInitPosZ = CHexInitXzCos45;
        private const double CLmInitPosX = CHexInitXz;
        private const double CLmInitPosY = CHexInitY;
        private const double CLmInitPosZ = 0;
        private const double CLfInitPosX = CHexInitXzCos45;
        private const double CLfInitPosY = CHexInitY;
        private const double CLfInitPosZ = -CHexInitXzSin45;
        private const double CRmInitPosX = CHexInitXz;
        private const double CRmInitPosY = CHexInitY;
        private const double CRmInitPosZ = 0;
        private const double CRrInitPosX = CHexInitXzCos45;
        private const double CRrInitPosY = CHexInitY;
        private const double CRrInitPosZ = CHexInitXzSin45;

        private readonly double[] _cCoxaAngle1 =
        {
            CRrCoxaAngle1, CRmCoxaAngle1, CRfCoxaAngle1, CLrCoxaAngle1, CLmCoxaAngle1, CLfCoxaAngle1
        };

        private readonly double[] _cCoxaLength =
        {
            CRrCoxaLength, CRmCoxaLength, CRfCoxaLength, CLrCoxaLength, CLmCoxaLength, CLfCoxaLength
        };

        private readonly int[] _cCoxaMax1 =
        {
            CRrCoxaMax1, CRmCoxaMax1, CRfCoxaMax1, CLrCoxaMax1, CLmCoxaMax1, CLfCoxaMax1
        };

        private readonly int[] _cCoxaMin1 =
        {
            CRrCoxaMin1, CRmCoxaMin1, CRfCoxaMin1, CLrCoxaMin1, CLmCoxaMin1, CLfCoxaMin1
        };

        private readonly short[] _cFemurHornOffset1 =
        {
            CRrFemurHornOffset1, CRmFemurHornOffset1, CRfFemurHornOffset1, CLrFemurHornOffset1, CLmFemurHornOffset1, CLfFemurHornOffset1
        };

        private readonly double[] _cFemurLength =
        {
            CRrFemurLength, CRmFemurLength, CRfFemurLength, CLrFemurLength, CLmFemurLength, CLfFemurLength
        };

        private readonly int[] _cFemurMax1 =
        {
            CRrFemurMax1, CRmFemurMax1, CRfFemurMax1, CLrFemurMax1, CLmFemurMax1, CLfFemurMax1
        };

        private readonly int[] _cFemurMin1 =
        {
            CRrFemurMin1, CRmFemurMin1, CRfFemurMin1, CLrFemurMin1, CLmFemurMin1, CLfFemurMin1
        };

        private readonly double[] _cInitPosX =
        {
            CRrInitPosX, CRmInitPosX, CRfInitPosX, CLrInitPosX, CLmInitPosX, CLfInitPosX
        };

        private readonly double[] _cInitPosY =
        {
            CRrInitPosY, CRmInitPosY, CRfInitPosY, CLrInitPosY, CLmInitPosY, CLfInitPosY
        };

        private readonly double[] _cInitPosZ =
        {
            CRrInitPosZ, CRmInitPosZ, CRfInitPosZ, CLrInitPosZ, CLmInitPosZ, CLfInitPosZ
        };

        private readonly double[] _cOffsetX = { CRrOffsetX, CRmOffsetX, CRfOffsetX, CLrOffsetX, CLmOffsetX, CLfOffsetX };
        private readonly double[] _cOffsetZ = { CRrOffsetZ, CRmOffsetZ, CRfOffsetZ, CLrOffsetZ, CLmOffsetZ, CLfOffsetZ };
        private readonly double[] _coxaAngle1 = new double[6];

        private readonly double[] _cTibiaLength =
        {
            CRrTibiaLength, CRmTibiaLength, CRfTibiaLength, CLrTibiaLength, CLmTibiaLength, CLfTibiaLength
        };

        private readonly int[] _cTibiaMax1 =
        {
            CRrTibiaMax1, CRmTibiaMax1, CRfTibiaMax1, CLrTibiaMax1, CLmTibiaMax1, CLfTibiaMax1
        };

        private readonly int[] _cTibiaMin1 =
        {
            CRrTibiaMin1, CRmTibiaMin1, CRfTibiaMin1, CLrTibiaMin1, CLmTibiaMin1, CLfTibiaMin1
        };

        private readonly double[] _femurAngle1 = new double[6]; //Actual Angle of the vertical hip, decimals = 1
        private readonly byte[] _gaitLegNr = new byte[6]; //Init position of the leg
        private readonly double[] _gaitPosX = new double[6]; //Array containing Relative X position corresponding to the Gait
        private readonly double[] _gaitPosY = new double[6]; //Array containing Relative Y position corresponding to the Gait
        private readonly double[] _gaitPosZ = new double[6]; //Array containing Relative Z position corresponding to the Gait
        private readonly double[] _gaitRotY = new double[6]; //Array containing Relative Y rotation corresponding to the Gait
        private readonly double[] _legPosX = new double[6]; //Actual X Position of the Leg should be length of 6
        private readonly double[] _legPosY = new double[6]; //Actual Y Position of the Leg
        private readonly double[] _legPosZ = new double[6]; //Actual Z Position of the Leg
        private readonly double[] _tibiaAngle1 = new double[6]; //Actual Angle of the knee, decimals = 1
        
        private int _ikSolution; //Output true if the solution is possible
        private int _ikSolutionError; //Output true if the solution is NOT possible
        private int _ikSolutionWarning; //Output true if the solution is NEARLY possible

        private int _lastLeg; //TRUE when the current leg is the last leg of the sequence
        private byte _liftDivFactor; //Normaly: 2, when NrLiftedPos=5: 4
        private long _nrLiftedPos; //Number of positions that a single leg is lifted [1-3]
        private byte _stepsInGait; //Number of steps in gait
        private double _tlDivFactor; //Number of steps that a leg is on the floor while walking
        private bool _travelRequest; //Temp to check if the gait is in motion
        private double _bodyPosX; //Global Input for the position of the body
        private double _bodyPosY; //Controls height of the body from the ground
        private double[] _bodyPosYPerLeg = {0, 0, 0, 0, 0, 0}; //Controls height of the body from the ground per leg. 
        private double _bodyPosZ;

        private double _bodyRotOffsetX; //Input X offset value to adjust centerpoint of rotation
        private double _bodyRotOffsetY; //Input Y offset value to adjust centerpoint of rotation
        private double _bodyRotOffsetZ; //Input Z offset value to adjust centerpoint of rotation

        private double _bodyRotX1; //Global Input pitch of the body
        private double _bodyRotY1; //Global Input rotation of the body
        private double _bodyRotZ1; //Global Input roll of the body

        private int _halfLiftHeight; //If TRUE the outer positions of the ligted legs will be half height    
        private double _legLiftHeight; //Current Travel height

        private int _gaitStep;
        private int _gaitType;
        private int _nomGaitSpeed = 65; //Nominal speed of the gait, equates to MS between servo commands

        private double _travelLengthX; //Current Travel length X
        private double _travelLengthZ; //Current Travel length Z
        private double _travelRotationY; //Current Travel Rotation Y

        private readonly int[] _legOneServos = new int[3]; //coxa, femur, tibia
        private readonly int[] _legTwoServos = new int[3];
        private readonly int[] _legThreeServos = new int[3];
        private readonly int[] _legFourServos = new int[3];
        private readonly int[] _legFiveServos = new int[3];
        private readonly int[] _legSixServos = new int[3];
        #endregion

        #region Main logic loop 
        public bool Run()
        {
            LoadLegDefaults();
            OpenSsc();
            XboxControllerInit();

            _bodyPosY = 70;

            for (var legIndex = 0; legIndex <= 5; legIndex++)
            {
                _legPosX[legIndex] = (_cInitPosX[legIndex]); //Set start positions for each leg
                _legPosY[legIndex] = (_cInitPosY[legIndex]);
                _legPosZ[legIndex] = (_cInitPosZ[legIndex]);

                _bodyPosYPerLeg[legIndex] = _bodyPosY; //Eventually this will be set by foot switches
            }

            _gaitStep = 0;
            _nomGaitSpeed = 60;
            _legLiftHeight = 15;

            _gaitType = 1;

            GaitSelect();

            while (true)
            {
                if (!_isMovementStarted)
                {
                    Task.Delay(500).Wait();
                    continue;
                }

                GaitSeq();

                _ikSolution = 0;
                _ikSolutionWarning = 0;
                _ikSolutionError = 0;

                double bodyFkPosX;
                double bodyFkPosY;
                double bodyFkPosZ;

                //Attach events to each ground sensor switch on the feet. When that triggers, it sets that legs _bodyPosYPerLeg to a new value. This for loop will catch it.
                for (var legIndex = 0; legIndex <= 2; legIndex++)
                {
                    BodyFk(-_legPosX[legIndex] + _bodyPosX + _gaitPosX[legIndex],
                        _legPosZ[legIndex] + _bodyPosZ + _gaitPosZ[legIndex],
                        _legPosY[legIndex] + _bodyPosY + _gaitPosY[legIndex],
                        _gaitRotY[legIndex], legIndex,
                        out bodyFkPosX, out bodyFkPosZ, out bodyFkPosY);

                    LegIk(_legPosX[legIndex] - _bodyPosX + bodyFkPosX - (_gaitPosX[legIndex]),
                        _legPosY[legIndex] + _bodyPosY - bodyFkPosY + _gaitPosY[legIndex],
                        _legPosZ[legIndex] + _bodyPosZ - bodyFkPosZ + _gaitPosZ[legIndex],
                        legIndex);
                }

                for (var legIndex = 3; legIndex <= 5; legIndex++)
                {
                    BodyFk(_legPosX[legIndex] - _bodyPosX + _gaitPosX[legIndex],
                        _legPosZ[legIndex] + _bodyPosZ + _gaitPosZ[legIndex],
                        _legPosY[legIndex] + _bodyPosY + _gaitPosY[legIndex],
                        _gaitRotY[legIndex], legIndex,
                        out bodyFkPosX, out bodyFkPosZ, out bodyFkPosY);

                    LegIk(_legPosX[legIndex] + _bodyPosX - bodyFkPosX + _gaitPosX[legIndex],
                        _legPosY[legIndex] + _bodyPosY - bodyFkPosY + _gaitPosY[legIndex],
                        _legPosZ[legIndex] + _bodyPosZ - bodyFkPosZ + _gaitPosZ[legIndex],
                        legIndex);
                }

                if (_ikSolutionError == 1 || _ikSolution == 0)
                {
                    Debug.WriteLine($"Skipping solution - Error {_ikSolutionError}, Warning {_ikSolutionWarning} ");
                    Debug.WriteLine($"_gaitType {_gaitType}, legLiftHeight {_legLiftHeight}, _bodyPosZ {_bodyPosZ}, _travelLengthZ {_travelLengthZ}, _travelLengthX {_travelLengthX}, _travelRotationY {_travelRotationY}");
                    continue;
                }

                //if (!CheckAngles())//This last part IS magic.
                //    continue;

                var toWrite = UpdateServoDriver();

                WriteSerial(toWrite);

                _mre.Wait(250);
                _mre.Reset();

                CheckMoveComplete();

                _mre.Wait(250);
                _mre.Reset();
            }

            return true;
        } 
        #endregion

        /// <summary>
        /// This is called from the GPS class after data is received and then validated. 
        /// </summary>
        /// <param name="latLon"></param>
        public void GpsData(LatLon latLon)
        {
            //ToDo : Logic to steer mr hexapi
        }

        #region XBox 360 Controller related...

        public void XboxControllerInit()
        {
            bool c = true;

            while (c)
            {
                Task.Factory.StartNew(async () =>
                {
                    _xboxController = null;

                    //USB\VID_045E&PID_0719\E02F1950 - receiver
                    //USB\VID_045E & PID_02A1 & IG_00\6 & F079888 & 0 & 00  - XboxController
                    //0x01, 0x05 = game controllers

                    var deviceInformationCollection = await DeviceInformation.FindAllAsync(HidDevice.GetDeviceSelector(0x01, 0x05));

                    if (deviceInformationCollection.Count == 0)
                    {
                        Debug.WriteLine("No Xbox360 XboxController found! Perhaps an appxmanifest issue?");
                        return;
                    }

                    foreach (var d in deviceInformationCollection)
                    {
                        Debug.WriteLine("Device ID: " + d.Id);

                        var hidDevice = await HidDevice.FromIdAsync(d.Id, Windows.Storage.FileAccessMode.Read);

                        if (hidDevice == null)
                        {
                            Debug.WriteLine("Failed to connect to the XboxController");
                            return;
                        }

                        c = false;

                        try
                        {
                            _xboxController = new XboxController(hidDevice);

                            _xboxController.LeftDirectionChanged += XboxControllerLeftDirectionChanged;
                            _xboxController.RightDirectionChanged += XboxControllerRightDirectionChanged;
                            _xboxController.DpadDirectionChanged += _xboxController_DpadDirectionChanged;
                            _xboxController.LeftTriggerChanged += _xboxController_LeftTriggerChanged;
                            _xboxController.RightTriggerChanged += _xboxController_RightTriggerChanged;
                            _xboxController.FunctionButtonChanged += XboxControllerFunctionButtonChanged;
                            _xboxController.BumperButtonChanged += _xboxController_BumperButtonChanged;
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine("Failed to connect to the XboxController " + e);
                        }

                        break;
                    }
                }).Wait();

                Debug.WriteLine("Waiting 2 seconds and trying again...");

                Task.Delay(2000).Wait();
            }
        }

        //4 = Left bumper, 5 = Right bumper
        private void _xboxController_BumperButtonChanged(int button)
        {
            switch (_selectedFunction)
            {
                case SelectedFunction.TranslateHorizontal:
                case SelectedFunction.Translate3D:
                case SelectedFunction.GaitSpeed: //A
                    if (button == 5)
                    {
                        if (_nomGaitSpeed < 150)
                            _nomGaitSpeed = _nomGaitSpeed + 1;
                    }
                    else
                    {
                        if (_nomGaitSpeed > 60)
                            _nomGaitSpeed = _nomGaitSpeed - 1;
                    }
                    break;
                case SelectedFunction.LegHeight: //B
                    if (button == 5)
                    {
                        if (_legLiftHeight < 90)
                            _legLiftHeight = _legLiftHeight + 1;
                    }
                    else
                    {
                        if (_legLiftHeight > 25)
                            _legLiftHeight = _legLiftHeight - 1;
                    }
                    break;
                default:
                    break;
            }
        }

        private void XboxControllerFunctionButtonChanged(int button)
        {
            switch (button)
            {
                case 0: //A
                    _selectedFunction = SelectedFunction.GaitSpeed;
                    break;
                case 1: //B
                    _selectedFunction = SelectedFunction.LegHeight;
                    break;
                case 2: //X
                    _selectedFunction = SelectedFunction.TranslateHorizontal;
                    break;
                case 3: //Y
                    _selectedFunction = SelectedFunction.Translate3D;
                    break;
                case 7: //Start button
                    if (_isMovementStarted)
                    {
                        _isMovementStarted = false;
                        TurnOffServos();
                    }
                    else
                        _isMovementStarted = true;

                    Debug.WriteLine("setting movement to  " + _isMovementStarted);
                    break;
                case 6://back button
                    //This will save the current lat/lon as a waypoint
                    break;
                default:
                    Debug.WriteLine("button? " + button);
                    break;
            }
        }

        private void _xboxController_RightTriggerChanged(int trigger)
        {
            _travelLengthX = Map(trigger, 0, 10000, 0, 70);
        }

        private void _xboxController_LeftTriggerChanged(int trigger)
        {
            _travelLengthX = -Map(trigger, 0, 10000, 0, 70);
        }

        private void _xboxController_DpadDirectionChanged(ControllerVector sender)
        {
            switch (sender.Direction)
            {
                case ControllerDirection.Left:
                    if (_gaitType > 0)
                    {
                        _gaitType--;
                        GaitSelect();
                    }
                    break;
                case ControllerDirection.Right:
                    if (_gaitType < 4)
                    {
                        _gaitType++;
                        GaitSelect();
                    }
                    break;
                case ControllerDirection.Up:
                    if (_bodyPosY < 115)
                    {
                        _bodyPosY = _bodyPosY + 5;
                    }
                    break;
                case ControllerDirection.Down:
                    if (_bodyPosY > 25)
                        _bodyPosY = _bodyPosY - 5;
                    break;
            }

            //for (int i = 0; i < 5; i++)
            //    _bodyPosYPerLeg[i] = _bodyPosY;
        }

        private void XboxControllerRightDirectionChanged(ControllerVector sender)
        {
            //Speak(sender.Direction.ToString());

            switch (sender.Direction)
            {
                case ControllerDirection.Left:
                    _travelRotationY = -Map(sender.Magnitude, 0, 10000, 0, 3);
                    _travelLengthZ = 0;
                    break;
                case ControllerDirection.UpLeft:
                    _travelRotationY = -Map(sender.Magnitude, 0, 10000, 0, 3);
                    _travelLengthZ = -Map(sender.Magnitude, 0, 10000, 0, 80);
                    break;
                case ControllerDirection.DownLeft:
                    _travelRotationY = -Map(sender.Magnitude, 0, 10000, 0, 3);
                    _travelLengthZ = Map(sender.Magnitude, 0, 10000, 0, 80);
                    break;
                case ControllerDirection.Right:
                    _travelRotationY = Map(sender.Magnitude, 0, 10000, 0, 3);
                    _travelLengthZ = 0;
                    break;
                case ControllerDirection.UpRight:
                    _travelRotationY = Map(sender.Magnitude, 0, 10000, 0, 3);
                    _travelLengthZ = -Map(sender.Magnitude, 0, 10000, 0, 80);
                    break;
                case ControllerDirection.DownRight:
                    _travelRotationY = Map(sender.Magnitude, 0, 10000, 0, 3);
                    _travelLengthZ = Map(sender.Magnitude, 0, 10000, 0, 80);
                    break;
                case ControllerDirection.Up:
                    _travelLengthZ = -Map(sender.Magnitude, 0, 10000, 0, 130);
                    _travelRotationY = 0;
                    break;
                case ControllerDirection.Down:
                    _travelLengthZ = Map(sender.Magnitude, 0, 10000, 0, 130);
                    _travelRotationY = 0;
                    break;
            }
        }

        void SetBodyRot(ControllerVector sender)
        {
            switch (sender.Direction)
            {
                case ControllerDirection.Left:
                    _bodyRotX1 = 0;
                    _bodyRotZ1 = -Map(sender.Magnitude, 0, 10000, 0, 7);
                    break;
                case ControllerDirection.UpLeft:
                    _bodyRotX1 = Map(sender.Magnitude, 0, 10000, 0, 7);
                    _bodyRotZ1 = -Map(sender.Magnitude, 0, 10000, 0, 7);
                    break;
                case ControllerDirection.UpRight:
                    _bodyRotX1 = Map(sender.Magnitude, 0, 10000, 0, 7);
                    _bodyRotZ1 = Map(sender.Magnitude, 0, 10000, 0, 7);
                    break;
                case ControllerDirection.Right:
                    _bodyRotX1 = 0;
                    _bodyRotZ1 = Map(sender.Magnitude, 0, 10000, 0, 7);
                    break;
                case ControllerDirection.Up:
                    _bodyRotX1 = Map(sender.Magnitude, 0, 10000, 0, 7);
                    _bodyRotZ1 = 0;

                    break;
                case ControllerDirection.Down:
                    _bodyRotX1 = -Map(sender.Magnitude, 0, 10000, 0, 7);
                    _bodyRotZ1 = 0;
                    break;
                case ControllerDirection.DownLeft:
                    _bodyRotZ1 = -Map(sender.Magnitude, 0, 10000, 0, 7);
                    _bodyRotX1 = -Map(sender.Magnitude, 0, 10000, 0, 7);
                    break;
                case ControllerDirection.DownRight:
                    _bodyRotZ1 = Map(sender.Magnitude, 0, 10000, 0, 7);
                    _bodyRotX1 = -Map(sender.Magnitude, 0, 10000, 0, 7);
                    break;
            }
        }

        void SetBodyRotOffset(ControllerVector sender)
        {
            switch (sender.Direction)
            {
                case ControllerDirection.Left:
                    _bodyPosX = 0;
                    _bodyPosZ = -Map(sender.Magnitude, 0, 10000, 0, 40);
                    break;
                case ControllerDirection.UpLeft:
                    _bodyPosX = Map(sender.Magnitude, 0, 10000, 0, 40);
                    _bodyPosZ = -Map(sender.Magnitude, 0, 10000, 0, 40);
                    break;
                case ControllerDirection.UpRight:
                    _bodyPosX = Map(sender.Magnitude, 0, 10000, 0, 40);
                    _bodyPosZ = Map(sender.Magnitude, 0, 10000, 0, 40);
                    break;
                case ControllerDirection.Right:
                    _bodyPosX = 0;
                    _bodyPosZ = Map(sender.Magnitude, 0, 10000, 0, 40);
                    break;
                case ControllerDirection.Up:
                    _bodyPosX = Map(sender.Magnitude, 0, 10000, 0, 40);
                    _bodyPosZ = 0;
                    break;
                case ControllerDirection.Down:
                    _bodyPosX = -Map(sender.Magnitude, 0, 10000, 0, 40);
                    _bodyPosZ = 0;
                    break;
                case ControllerDirection.DownLeft:
                    _bodyPosZ = -Map(sender.Magnitude, 0, 10000, 0, 40);
                    _bodyPosX = -Map(sender.Magnitude, 0, 10000, 0, 40);
                    break;
                case ControllerDirection.DownRight:
                    _bodyPosZ = Map(sender.Magnitude, 0, 10000, 0, 40);
                    _bodyPosX = -Map(sender.Magnitude, 0, 10000, 0, 40);
                    break;
            }
        }

        private void XboxControllerLeftDirectionChanged(ControllerVector sender)
        {
            switch (_selectedFunction)
            {
                case SelectedFunction.TranslateHorizontal:
                    SetBodyRotOffset(sender);
                    break;
                case SelectedFunction.Translate3D:
                    SetBodyRot(sender);
                    break;
                default:
                    SetBodyRot(sender);
                    break;
            }
        }
        #endregion

        private DataWriter dataWriter;
        private DataReader dataReader;

        #region Serial port code
        internal async void WriteSerial(string data)
        {
            using (var writer = new DataWriter(_serialPort.OutputStream))
            {
                writer.WriteString(data);

                using (var cts = new CancellationTokenSource(_serialPort.WriteTimeout))
                {
                    await writer.StoreAsync().AsTask(cts.Token);
                }

                writer.DetachStream();
            }

            _mre.Set();
        }

        readonly ManualResetEventSlim _mre = new ManualResetEventSlim(false);

        internal async void CheckMoveComplete()
        {
            var r = string.Empty;
            //var dataReader = new DataReader(_serialPort.InputStream) { InputStreamOptions = InputStreamOptions.Partial };
            //var dataWriter = new DataWriter(_serialPort.OutputStream);

            //while (!r.Equals("."))
            //{
            //    dataWriter.WriteString("Q" + Convert.ToChar(13));
            //    var sa = await dataWriter.StoreAsync().AsTask();

            //    var br = await dataReader.LoadAsync(1).AsTask();
            //    r = dataReader.ReadString(br);

            //    if (!string.IsNullOrEmpty(r))
            //        continue;

            //    Debug.WriteLine($"Null string returned from SSC?");
            //    break;
            //}

            //dataWriter.DetachStream();
            //dataReader.DetachStream();

            while (!r.Equals("."))
            {
                using (var writer = new DataWriter(_serialPort.OutputStream))
                {
                    writer.WriteString(("Q" + Convert.ToChar(13)));

                    using (var cts = new CancellationTokenSource(_serialPort.WriteTimeout))
                    {
                        await writer.StoreAsync().AsTask(cts.Token);
                    }

                    writer.DetachStream();
                }

                using (var reader = new DataReader(_serialPort.InputStream))
                {
                    using (var cts = new CancellationTokenSource(_serialPort.ReadTimeout))
                    {
                        var read = await reader.LoadAsync(1).AsTask(cts.Token);

                        if (read >= 1)
                        {
                            r = reader.ReadString(read);
                            reader.DetachStream();
                        }
                    }
                }
            }

            _mre.Set();
        }

        private void OpenSsc()
        {
            while (_serialPort == null)
            {
                var dis = DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector()).GetAwaiter().GetResult();
                var selectedPort = dis.FirstOrDefault(d => d.Name.Equals("Hexapi")); //If using the direct memory mapped driver the uart name is the same as the machine name.

                if (selectedPort == null)
                {
                    Debug.WriteLine("Could not find onboard UART. Retrying, though there is not much hope at this point. Check the d.Name.Equals( statement");

                    Task.Delay(2000).Wait();
                    return;
                }

                _serialPort = SerialDevice.FromIdAsync(selectedPort.Id).GetAwaiter().GetResult();

                _serialPort.ReadTimeout = TimeSpan.FromMilliseconds(300);
                _serialPort.WriteTimeout = TimeSpan.FromMilliseconds(300);
                _serialPort.BaudRate = 38400;
                _serialPort.Parity = SerialParity.None;
                _serialPort.StopBits = SerialStopBitCount.One;
                _serialPort.DataBits = 8;
                _serialPort.Handshake = SerialHandshake.None;

                dataReader = new DataReader(_serialPort.InputStream) { InputStreamOptions = InputStreamOptions.Partial };
                dataWriter = new DataWriter(_serialPort.OutputStream);
            }
        } 
        #endregion

        #region Gait calculations and logic
        public void GaitSelect()
        {
            //Gait selector
            switch (_gaitType)
            {
                case 0:
                    //Ripple Gait 12 steps
                    _gaitLegNr[CLr] = 1;
                    _gaitLegNr[CRf] = 3;
                    _gaitLegNr[CLm] = 5;
                    _gaitLegNr[CRr] = 7;
                    _gaitLegNr[CLf] = 9;
                    _gaitLegNr[CRm] = 11;

                    _nrLiftedPos = 3;
                    _halfLiftHeight = 3;
                    _tlDivFactor = 8;
                    _stepsInGait = 12;
                    //NomGaitSpeed = 110;
                    break;
                case 1:
                    //Tripod 8 steps
                    _gaitLegNr[CLr] = 5;
                    _gaitLegNr[CRf] = 1;
                    _gaitLegNr[CLm] = 1;
                    _gaitLegNr[CRr] = 1;
                    _gaitLegNr[CLf] = 5;
                    _gaitLegNr[CRm] = 5;

                    _nrLiftedPos = 3;
                    _halfLiftHeight = 3;
                    _tlDivFactor = 4;
                    _stepsInGait = 8;
                    //NomGaitSpeed = 80;
                    break;
                case 2:
                    //Triple Tripod 12 step
                    _gaitLegNr[CRf] = 3;
                    _gaitLegNr[CLm] = 4;
                    _gaitLegNr[CRr] = 5;
                    _gaitLegNr[CLf] = 9;
                    _gaitLegNr[CRm] = 10;
                    _gaitLegNr[CLr] = 11;

                    _nrLiftedPos = 3;
                    _halfLiftHeight = 3;
                    _tlDivFactor = 8;
                    _stepsInGait = 12;
                    //NomGaitSpeed = 100;
                    break;
                case 3:
                    // Triple Tripod 16 steps, use 5 lifted positions
                    _gaitLegNr[CRf] = 4;
                    _gaitLegNr[CLm] = 5;
                    _gaitLegNr[CRr] = 6;
                    _gaitLegNr[CLf] = 12;
                    _gaitLegNr[CRm] = 13;
                    _gaitLegNr[CLr] = 14;

                    _nrLiftedPos = 5;
                    _halfLiftHeight = 1;
                    _tlDivFactor = 10;
                    _stepsInGait = 16;
                    //NomGaitSpeed = 100;
                    break;
                case 4:
                    //Wave 24 steps
                    _gaitLegNr[CLr] = 1;
                    _gaitLegNr[CRf] = 21;
                    _gaitLegNr[CLm] = 5;

                    _gaitLegNr[CRr] = 13;
                    _gaitLegNr[CLf] = 9;
                    _gaitLegNr[CRm] = 17;

                    _nrLiftedPos = 3;
                    _halfLiftHeight = 3;
                    _tlDivFactor = 20;
                    _stepsInGait = 24;
                    //NomGaitSpeed = 110;
                    break;
            }
        }

        private void GaitSeq()
        {
            //Check if the Gait is in motion
            _travelRequest = ((Math.Abs(_travelLengthX) > CTravelDeadZone) || (Math.Abs(_travelLengthZ) > CTravelDeadZone) || (Math.Abs(_travelRotationY) > CTravelDeadZone));

            if (_nrLiftedPos == 5)
                _liftDivFactor = 4;
            else
                _liftDivFactor = 2;

            //Calculate Gait sequence
            _lastLeg = 0;
            for (var legIndex = 0; legIndex <= 5; legIndex++)
            {
                // for all legs
                if (legIndex == 5) // last leg
                    _lastLeg = 1;

                Gait(legIndex);
            } // next leg
        }

        private void Gait(int gaitCurrentLegNr)
        {
            //Clear values under the cTravelDeadZone
            if (!_travelRequest)
            {
                _travelLengthX = 0;
                _travelLengthZ = 0;
                _travelRotationY = 0;
            }
            //Leg middle up position
            //Gait in motion														  									
            //Gait NOT in motion, return to home position
            if ((_travelRequest && (_nrLiftedPos == 1 || _nrLiftedPos == 3 || _nrLiftedPos == 5) && _gaitStep == _gaitLegNr[gaitCurrentLegNr]) || (!_travelRequest && _gaitStep == _gaitLegNr[gaitCurrentLegNr] && ((Math.Abs(_gaitPosX[gaitCurrentLegNr]) > 2) || (Math.Abs(_gaitPosZ[gaitCurrentLegNr]) > 2) || (Math.Abs(_gaitRotY[gaitCurrentLegNr]) > 2))))
            {
                //Up
                _gaitPosX[gaitCurrentLegNr] = 0;
                _gaitPosY[gaitCurrentLegNr] = -_legLiftHeight;
                _gaitPosZ[gaitCurrentLegNr] = 0;
                _gaitRotY[gaitCurrentLegNr] = 0;
            }
            //Optional Half height Rear (2, 3, 5 lifted positions)
            else if (((_nrLiftedPos == 2 && _gaitStep == _gaitLegNr[gaitCurrentLegNr]) ||
                    (_nrLiftedPos >= 3 && 
                    (_gaitStep == _gaitLegNr[gaitCurrentLegNr] - 1 || _gaitStep == _gaitLegNr[gaitCurrentLegNr] + (_stepsInGait - 1)))) && _travelRequest)
            {
                _gaitPosX[gaitCurrentLegNr] = -_travelLengthX / _liftDivFactor;
                _gaitPosY[gaitCurrentLegNr] = -3 * _legLiftHeight / (3 + _halfLiftHeight);
                //Easier to shift between div factor: /1 (3/3), /2 (3/6) and 3/4
                _gaitPosZ[gaitCurrentLegNr] = -_travelLengthZ / _liftDivFactor;
                _gaitRotY[gaitCurrentLegNr] = -_travelRotationY / _liftDivFactor;
            }

            // Optional Half height front (2, 3, 5 lifted positions)
            else if ((_nrLiftedPos >= 2) && (_gaitStep == _gaitLegNr[gaitCurrentLegNr] + 1 || _gaitStep == _gaitLegNr[gaitCurrentLegNr] - (_stepsInGait - 1)) && _travelRequest)
            {
                _gaitPosX[gaitCurrentLegNr] = _travelLengthX / _liftDivFactor;
                _gaitPosY[gaitCurrentLegNr] = -3 * _legLiftHeight / (3 + _halfLiftHeight);
                // Easier to shift between div factor: /1 (3/3), /2 (3/6) and 3/4
                _gaitPosZ[gaitCurrentLegNr] = _travelLengthZ / _liftDivFactor;
                _gaitRotY[gaitCurrentLegNr] = _travelRotationY / _liftDivFactor;
            }

            //Optional Half heigth Rear 5 LiftedPos (5 lifted positions)
            else if (((_nrLiftedPos == 5 && (_gaitStep == _gaitLegNr[gaitCurrentLegNr] - 2))) && _travelRequest)
            {
                _gaitPosX[gaitCurrentLegNr] = -_travelLengthX / 2;
                _gaitPosY[gaitCurrentLegNr] = -_legLiftHeight / 2;
                _gaitPosZ[gaitCurrentLegNr] = -_travelLengthZ / 2;
                _gaitRotY[gaitCurrentLegNr] = -_travelRotationY / 2;
            }

            //Optional Half heigth Front 5 LiftedPos (5 lifted positions)
            else if ((_nrLiftedPos == 5) && (_gaitStep == _gaitLegNr[gaitCurrentLegNr] + 2 || _gaitStep == _gaitLegNr[gaitCurrentLegNr] - (_stepsInGait - 2)) && _travelRequest)
            {
                _gaitPosX[gaitCurrentLegNr] = _travelLengthX / 2;
                _gaitPosY[gaitCurrentLegNr] = -_legLiftHeight / 2;
                _gaitPosZ[gaitCurrentLegNr] = _travelLengthZ / 2;
                _gaitRotY[gaitCurrentLegNr] = _travelRotationY / 2;
            }

            //Leg front down position
            else if ((_gaitStep == _gaitLegNr[gaitCurrentLegNr] + _nrLiftedPos || _gaitStep == _gaitLegNr[gaitCurrentLegNr] - (_stepsInGait - _nrLiftedPos)) && _gaitPosY[gaitCurrentLegNr] < 0)
            {
                _gaitPosX[gaitCurrentLegNr] = _travelLengthX / 2;
                _gaitPosZ[gaitCurrentLegNr] = _travelLengthZ / 2;
                _gaitRotY[gaitCurrentLegNr] = _travelRotationY / 2;
                _gaitPosY[gaitCurrentLegNr] = 0;
                //Only move leg down at once if terrain adaption is turned off
            }

            //Move body forward      
            else
            {
                _gaitPosX[gaitCurrentLegNr] = _gaitPosX[gaitCurrentLegNr] - (_travelLengthX / _tlDivFactor);
                _gaitPosY[gaitCurrentLegNr] = 0;
                _gaitPosZ[gaitCurrentLegNr] = _gaitPosZ[gaitCurrentLegNr] - (_travelLengthZ / _tlDivFactor);
                _gaitRotY[gaitCurrentLegNr] = _gaitRotY[gaitCurrentLegNr] - (_travelRotationY / _tlDivFactor);
            }

            //Advance to the next step
            if (_lastLeg != 1) return;

            //The last leg in this step
            _gaitStep = _gaitStep + 1;
            if (_gaitStep > _stepsInGait)
                _gaitStep = 1;
        }
        #endregion

        #region Body and Leg Inverse Kinematics
        //(BODY INVERSE KINEMATICS) 
        //BodyRotX         - Global Input pitch of the body 
        //BodyRotY         - Global Input rotation of the body 
        //BodyRotZ         - Global Input roll of the body 
        //RotationY         - Input Rotation for the gait 
        //PosX            - Input position of the feet X 
        //PosZ            - Input position of the feet Z 
        //SinB                  - Sin buffer for BodyRotX
        //CosB               - Cos buffer for BodyRotX 
        //SinG                  - Sin buffer for BodyRotZ
        //CosG               - Cos buffer for BodyRotZ
        //BodyFKPosX         - Output Position X of feet with Rotation 
        //BodyFKPosY         - Output Position Y of feet with Rotation 
        //BodyFKPosZ         - Output Position Z of feet with Rotation
        private void BodyFk(double posX, double posZ, double posY, double rotationY, int bodyIkLeg, out double bodyFkPosX, out double bodyFkPosZ, out double bodyFkPosY)
        {
            double sinA4; //Sin buffer for BodyRotX calculations
            double cosA4; //Cos buffer for BodyRotX calculations
            double sinB4; //Sin buffer for BodyRotX calculations
            double cosB4; //Cos buffer for BodyRotX calculations
            double sinG4; //Sin buffer for BodyRotZ calculations
            double cosG4; //Cos buffer for BodyRotZ calculations

            //Calculating totals from center of the body to the feet 
            var cprX = (_cOffsetX[bodyIkLeg]) + posX + _bodyRotOffsetX;
            var cprY = posY + _bodyRotOffsetY;
            var cprZ = (_cOffsetZ[bodyIkLeg]) + posZ + _bodyRotOffsetZ;

            //Successive global rotation matrix: 
            //Math shorts for rotation: Alfa [A] = Xrotate, Beta [B] = Zrotate, Gamma [G] = Yrotate 
            //Sinus Alfa = SinA, cosinus Alfa = cosA. and so on... 

            //First calculate sinus and cosinus for each rotation: 
            GetSinCos(_bodyRotX1, out sinG4, out cosG4);

            GetSinCos(_bodyRotZ1, out sinB4, out cosB4);

            GetSinCos(_bodyRotY1 + (rotationY * 10), out sinA4, out cosA4);

            //Calculation of rotation matrix: 
            bodyFkPosX = (cprX * C2Dec - ((cprX * C2Dec * cosA4 / C4Dec * cosB4 / C4Dec) - (cprZ * C2Dec * cosB4 / C4Dec * sinA4 / C4Dec) + (cprY * C2Dec * sinB4 / C4Dec))) / C2Dec;
            bodyFkPosZ = (cprZ * C2Dec - ((cprX * C2Dec * cosG4 / C4Dec * sinA4 / C4Dec) + (cprX * C2Dec * cosA4 / C4Dec * sinB4 / C4Dec * sinG4 / C4Dec) + (cprZ * C2Dec * cosA4 / C4Dec * cosG4 / C4Dec) - (cprZ * C2Dec * sinA4 / C4Dec * sinB4 / C4Dec * sinG4 / C4Dec) - (cprY * C2Dec * cosB4 / C4Dec * sinG4 / C4Dec))) / C2Dec;
            bodyFkPosY = (cprY * C2Dec - ((cprX * C2Dec * sinA4 / C4Dec * sinG4 / C4Dec) - (cprX * C2Dec * cosA4 / C4Dec * cosG4 / C4Dec * sinB4 / C4Dec) + (cprZ * C2Dec * cosA4 / C4Dec * sinG4 / C4Dec) + (cprZ * C2Dec * cosG4 / C4Dec * sinA4 / C4Dec * sinB4 / C4Dec) + (cprY * C2Dec * cosB4 / C4Dec * cosG4 / C4Dec))) / C2Dec;
        }

        //[LEG INVERSE KINEMATICS] 
        //Calculates the angles of the coxa, femur and tibia for the given position of the feet
        //IKFeetPosX            - Input position of the Feet X
        //IKFeetPosY            - Input position of the Feet Y
        //IKFeetPosZ            - Input Position of the Feet Z
        //IKSolution            - Output true if the solution is possible
        //IKSolutionWarning     - Output true if the solution is NEARLY possible
        //IKSolutionError    - Output true if the solution is NOT possible
        //FemurAngle1           - Output Angle of Femur in degrees
        //TibiaAngle1           - Output Angle of Tibia in degrees
        //CoxaAngle1            - Output Angle of Coxa in degrees
        private void LegIk(double ikFeetPosX, double ikFeetPosY, double ikFeetPosZ, int legIkLegNr)
        {
            double xyhyp2;

            //Calculate IKCoxaAngle and IKFeetPosXZ
            var getatan = GetATan2(ikFeetPosX, ikFeetPosZ, out xyhyp2);
            _coxaAngle1[legIkLegNr] = ((getatan * 180) / 3141) + (_cCoxaAngle1[legIkLegNr]);

            var ikFeetPosXz = xyhyp2 / C2Dec;
            var ika14 = GetATan2(ikFeetPosY, ikFeetPosXz - (_cCoxaLength[legIkLegNr]), out xyhyp2);
            var iksw2 = xyhyp2;
            var temp1 = ((((_cFemurLength[legIkLegNr]) * (_cFemurLength[legIkLegNr])) - ((_cTibiaLength[legIkLegNr]) * (_cTibiaLength[legIkLegNr]))) * C4Dec + (iksw2 * iksw2));
            var temp2 = 2 * (_cFemurLength[legIkLegNr]) * C2Dec * iksw2;
            var ika24 = GetArcCos(temp1 / (temp2 / C4Dec));

            _femurAngle1[legIkLegNr] = -(ika14 + ika24) * 180 / 3141 + 900 + _cFemurHornOffset1[legIkLegNr];

            temp1 = ((((_cFemurLength[legIkLegNr]) * (_cFemurLength[legIkLegNr])) + ((_cTibiaLength[legIkLegNr]) * (_cTibiaLength[legIkLegNr]))) * C4Dec - (iksw2 * iksw2));
            temp2 = (2 * (_cFemurLength[legIkLegNr]) * (_cTibiaLength[legIkLegNr]));

            _tibiaAngle1[legIkLegNr] = -(900 - GetArcCos(temp1 / temp2) * 180 / 3141);

            if (iksw2 < ((_cFemurLength[legIkLegNr]) + (_cTibiaLength[legIkLegNr]) - 30) * C2Dec)
                _ikSolution = 1;
            else
            {
                if (iksw2 < ((_cFemurLength[legIkLegNr]) + (_cTibiaLength[legIkLegNr])) * C2Dec)
                    _ikSolutionWarning = 1;
                else
                    _ikSolutionError = 1;
            }
        }
        #endregion

        #region Servo related, build various servo controller strings and read values
        private string UpdateServoDriver()
        {
            var stringBuilder = new StringBuilder();

            for (var legIndex = 0; legIndex <= 5; legIndex++)
            {
                if (legIndex < 3)
                {
                    var wCoxaSscv = Math.Round((-_coxaAngle1[legIndex] + 900) * 1000 / CPwmDiv + CPfConst);
                    var wFemurSscv = Math.Round((-_femurAngle1[legIndex] + 900) * 1000 / CPwmDiv + CPfConst);
                    var wTibiaSscv = Math.Round((-_tibiaAngle1[legIndex] + 900) * 1000 / CPwmDiv + CPfConst);

                    if (legIndex == 0)
                    {
                        stringBuilder.Append(string.Format("#{0}P{1}", _legOneServos[0], wCoxaSscv));
                        stringBuilder.Append(string.Format("#{0}P{1}", _legOneServos[1], wFemurSscv));
                        stringBuilder.Append(string.Format("#{0}P{1}", _legOneServos[2], wTibiaSscv));
                    }
                    if (legIndex == 1)
                    {
                        stringBuilder.Append(string.Format("#{0}P{1}", _legTwoServos[0], wCoxaSscv));
                        stringBuilder.Append(string.Format("#{0}P{1}", _legTwoServos[1], wFemurSscv));
                        stringBuilder.Append(string.Format("#{0}P{1}", _legTwoServos[2], wTibiaSscv));
                    }
                    if (legIndex == 2)
                    {
                        stringBuilder.Append(string.Format("#{0}P{1}", _legThreeServos[0], wCoxaSscv));
                        stringBuilder.Append(string.Format("#{0}P{1}", _legThreeServos[1], wFemurSscv));
                        stringBuilder.Append(string.Format("#{0}P{1}", _legThreeServos[2], wTibiaSscv));
                    }
                }
                else
                {
                    var wCoxaSscv = Math.Round((_coxaAngle1[legIndex] + 900) * 1000 / CPwmDiv + CPfConst);
                    var wFemurSscv = Math.Round(((_femurAngle1[legIndex] + 900) * 1000 / CPwmDiv + CPfConst));
                    var wTibiaSscv = Math.Round((_tibiaAngle1[legIndex] + 900) * 1000 / CPwmDiv + CPfConst);

                    if (legIndex == 3)
                    {
                        stringBuilder.Append(string.Format("#{0}P{1}", _legSixServos[0], wCoxaSscv));
                        stringBuilder.Append(string.Format("#{0}P{1}", _legSixServos[1], wFemurSscv));
                        stringBuilder.Append(string.Format("#{0}P{1}", _legSixServos[2], wTibiaSscv));
                    }
                    if (legIndex == 4)
                    {
                        stringBuilder.Append(string.Format("#{0}P{1}", _legFiveServos[0], wCoxaSscv));
                        stringBuilder.Append(string.Format("#{0}P{1}", _legFiveServos[1], wFemurSscv));
                        stringBuilder.Append(string.Format("#{0}P{1}", _legFiveServos[2], wTibiaSscv));
                    }
                    if (legIndex == 5)
                    {
                        stringBuilder.Append(string.Format("#{0}P{1}", _legFourServos[0], wCoxaSscv));
                        stringBuilder.Append(string.Format("#{0}P{1}", _legFourServos[1], wFemurSscv));
                        stringBuilder.Append(string.Format("#{0}P{1}", _legFourServos[2], wTibiaSscv));
                    }
                }
            }

            stringBuilder.Append(string.Format("T{0}{1}", _nomGaitSpeed, Convert.ToChar(13)));

            return stringBuilder.ToString();
        }

        private void TurnOffServos()
        {
            var stringBuilder = new StringBuilder();

            for (var legIndex = 0; legIndex <= 5; legIndex++)
            {
                if (legIndex < 3)
                {
                    var wCoxaSscv = 0;
                    var wFemurSscv = 0;
                    var wTibiaSscv = 0;

                    if (legIndex == 0)
                    {
                        stringBuilder.Append(string.Format("#{0}P{1}", _legOneServos[0], wCoxaSscv));
                        stringBuilder.Append(string.Format("#{0}P{1}", _legOneServos[1], wFemurSscv));
                        stringBuilder.Append(string.Format("#{0}P{1}", _legOneServos[2], wTibiaSscv));
                    }
                    if (legIndex == 1)
                    {
                        stringBuilder.Append(string.Format("#{0}P{1}", _legTwoServos[0], wCoxaSscv));
                        stringBuilder.Append(string.Format("#{0}P{1}", _legTwoServos[1], wFemurSscv));
                        stringBuilder.Append(string.Format("#{0}P{1}", _legTwoServos[2], wTibiaSscv));
                    }
                    if (legIndex == 2)
                    {
                        stringBuilder.Append(string.Format("#{0}P{1}", _legThreeServos[0], wCoxaSscv));
                        stringBuilder.Append(string.Format("#{0}P{1}", _legThreeServos[1], wFemurSscv));
                        stringBuilder.Append(string.Format("#{0}P{1}", _legThreeServos[2], wTibiaSscv));
                    }
                }
                else
                {
                    var wCoxaSscv = 0;
                    var wFemurSscv = 0;
                    var wTibiaSscv = 0;

                    if (legIndex == 3)
                    {
                        stringBuilder.Append(string.Format("#{0}P{1}", _legSixServos[0], wCoxaSscv));
                        stringBuilder.Append(string.Format("#{0}P{1}", _legSixServos[1], wFemurSscv));
                        stringBuilder.Append(string.Format("#{0}P{1}", _legSixServos[2], wTibiaSscv));
                    }
                    if (legIndex == 4)
                    {
                        stringBuilder.Append(string.Format("#{0}P{1}", _legFiveServos[0], wCoxaSscv));
                        stringBuilder.Append(string.Format("#{0}P{1}", _legFiveServos[1], wFemurSscv));
                        stringBuilder.Append(string.Format("#{0}P{1}", _legFiveServos[2], wTibiaSscv));
                    }
                    if (legIndex == 5)
                    {
                        stringBuilder.Append(string.Format("#{0}P{1}", _legFourServos[0], wCoxaSscv));
                        stringBuilder.Append(string.Format("#{0}P{1}", _legFourServos[1], wFemurSscv));
                        stringBuilder.Append(string.Format("#{0}P{1}", _legFourServos[2], wTibiaSscv));
                    }
                }
            }

            stringBuilder.Append(string.Format("T{0}{1}", 0, Convert.ToChar(13)));

            WriteSerial(stringBuilder.ToString());
        }

        public async void LoadLegDefaults()
        {
            var config = string.Empty;

            try
            {
                var folder = Package.Current.InstalledLocation;
                var file = await folder.GetFileAsync("hexapod.config");
                config = await FileIO.ReadTextAsync(file);
            }
            catch (Exception e)
            {
                Debug.WriteLine("Cannot read walkerDefaults.config " + e);
                return;
            }

            if (string.IsNullOrEmpty(config))
            {
                Debug.WriteLine("Empty config file. walkerDefaults.config");
                return;
            }

            try
            {
                var allLegDefaults = config.Split('\n');

                for (var i = 0; i < 6; i++)
                {
                    var t = allLegDefaults[i].Replace('\r'.ToString(), "");

                    var jointDefaults = t.Split('|');

                    if (i == 0)
                    {
                        _legOneServos[0] = Convert.ToInt32(jointDefaults[0].Split(',')[0]);
                        _legOneServos[1] = Convert.ToInt32(jointDefaults[1].Split(',')[0]);
                        _legOneServos[2] = Convert.ToInt32(jointDefaults[2].Split(',')[0]);
                    }
                    if (i == 1)
                    {
                        _legTwoServos[0] = Convert.ToInt32(jointDefaults[0].Split(',')[0]);
                        _legTwoServos[1] = Convert.ToInt32(jointDefaults[1].Split(',')[0]);
                        _legTwoServos[2] = Convert.ToInt32(jointDefaults[2].Split(',')[0]);
                    }
                    if (i == 2)
                    {
                        _legThreeServos[0] = Convert.ToInt32(jointDefaults[0].Split(',')[0]);
                        _legThreeServos[1] = Convert.ToInt32(jointDefaults[1].Split(',')[0]);
                        _legThreeServos[2] = Convert.ToInt32(jointDefaults[2].Split(',')[0]);
                    }
                    if (i == 3)
                    {
                        _legFourServos[0] = Convert.ToInt32(jointDefaults[0].Split(',')[0]);
                        _legFourServos[1] = Convert.ToInt32(jointDefaults[1].Split(',')[0]);
                        _legFourServos[2] = Convert.ToInt32(jointDefaults[2].Split(',')[0]);
                    }
                    if (i == 4)
                    {
                        _legFiveServos[0] = Convert.ToInt32(jointDefaults[0].Split(',')[0]);
                        _legFiveServos[1] = Convert.ToInt32(jointDefaults[1].Split(',')[0]);
                        _legFiveServos[2] = Convert.ToInt32(jointDefaults[2].Split(',')[0]);
                    }
                    if (i == 5)
                    {
                        _legSixServos[0] = Convert.ToInt32(jointDefaults[0].Split(',')[0]);
                        _legSixServos[1] = Convert.ToInt32(jointDefaults[1].Split(',')[0]);
                        _legSixServos[2] = Convert.ToInt32(jointDefaults[2].Split(',')[0]);
                    }

                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }
        #endregion

        #region Helpers, and static methods
        private static double Map(double x, double inMin, double inMax, double outMin, double outMax)
        {
            var r = (x - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;
            return r;
        }

        //Checks the mechanical limits of the servos
        private bool CheckAngles()
        {
            for (var legIndex = 0; legIndex <= 5; legIndex++)
            {
                _coxaAngle1[legIndex] = Math.Min(Math.Max(_coxaAngle1[legIndex], (_cCoxaMin1[legIndex])), (_cCoxaMax1[legIndex]));
                _femurAngle1[legIndex] = Math.Min(Math.Max(_femurAngle1[legIndex], (_cFemurMin1[legIndex])), (_cFemurMax1[legIndex]));
                _tibiaAngle1[legIndex] = Math.Min(Math.Max(_tibiaAngle1[legIndex], (_cTibiaMin1[legIndex])), (_cTibiaMax1[legIndex]));
            }

            return true;
        }

        //Get the sinus and cosinus from the angle +/- multiple circles
        //AngleDeg1     - Input Angle in degrees
        //sin4        - Output Sinus of AngleDeg
        //cos4          - Output Cosinus of AngleDeg
        private static void GetSinCos(double angleDeg1, out double sin, out double cos)
        {
            var angle = Math.PI * angleDeg1 / 180.0; //Convert to raidans

            sin = Math.Sin(angle) * C4Dec;
            cos = Math.Cos(angle) * C4Dec;
        }

        //(Get the sinus and cosinus from the angle +/- multiple circles
        //cos4        - Input Cosinus
        //AngleRad4     - Output Angle in AngleRad4
        private static double GetArcCos(double cos4)
        {
            var c = cos4 / C4Dec; //Wont work right unless you do / 10000 then * 10000
            return (Math.Abs(Math.Abs(c) - 1.0) < .001 ? (1 - c) * Math.PI / 2.0 : Math.Atan(-c / Math.Sqrt(1 - c * c)) + 2 * Math.Atan(1)) * C4Dec; ;
        }

        //ArcTan2 function based on fixed point ArcCos
        //ArcTanX         - Input X
        //ArcTanY         - Input Y
        //ArcTan4          - Output ARCTAN2(X/Y)
        //XYhyp2            - Output presenting Hypotenuse of X and Y
        private static double GetATan2(double atanX, double atanY, out double xyhyp2)
        {
            double atan4;

            xyhyp2 = Math.Sqrt((atanX * atanX * C4Dec) + (atanY * atanY * C4Dec));

            var angleRad4 = GetArcCos((atanX * C6Dec) / xyhyp2);

            if (atanY < 0) // removed overhead... Atan4 = AngleRad4 * (AtanY/abs(AtanY));  
                atan4 = -angleRad4;
            else
                atan4 = angleRad4;

            return atan4;
        } 
        #endregion

        private async void Speak(string text)
        {
            try
            {
                var mediaElement = new MediaElement();
                var synth = new SpeechSynthesizer();

                foreach (var voice in SpeechSynthesizer.AllVoices)
                {
                    Debug.WriteLine(voice.DisplayName + ", " + voice.Description);
                }

                // Initialize a new instance of the SpeechSynthesizer.
                var stream = await synth.SynthesizeTextToStreamAsync(text);

                // Send the stream to the media object.
                mediaElement.SetSource(stream, stream.ContentType);
                mediaElement.Play();

                mediaElement.Stop();
                synth.Dispose();
            }
            catch (Exception e)
            {
                //
            }
        }
    }
}