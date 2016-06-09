﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using HexapiBackground.Gps.Ntrip;
using HexapiBackground.Hardware;
using HexapiBackground.Helpers;

namespace HexapiBackground.Gps
{
    internal class NavSparkGps : IGps
    {
        private readonly List<double> _correctors = new List<double>();
        private readonly List<LatLon> _latLons = new List<LatLon>();
        private readonly List<LatLon> _latLonsAvg = new List<LatLon>();

        //So we don't have to add another USB/Serial adapter, we are not going to configure the GPS from here. 
        //We will pre-configure while connected to a PC. The RX will be connected to the TX pin of the GPS.
        //The TX will be connected to the RX2 pin on the GPS. NTRIP data will be sent over this 
        private readonly SerialPort _serialPortForRtkCorrectionData;
        private readonly SerialPort _serialPortForGps;
        private readonly bool _useRtk;

        private readonly SfSerial16X2Lcd _lcd;

        public NavSparkGps(bool useRtk, SfSerial16X2Lcd lcd)
        {
            _serialPortForRtkCorrectionData = new SerialPort();  //FTDIBUS\VID_0403+PID_6001+A104OHRXA\0000
            _serialPortForGps = new SerialPort();

            _useRtk = useRtk;

            _lcd = lcd;

            CurrentLatLon = new LatLon();
        }

        public LatLon CurrentLatLon { get; private set; }
        public double DeviationLon { get; private set; }
        public double DeviationLat { get; private set; }
        public double DriftCutoff { get; private set; }
        internal int SatellitesInView { get; set; }
        internal int SignalToNoiseRatio { get; set; }

        #region Serial Communication

        public async Task Start()
        {
            await _serialPortForRtkCorrectionData.Open("A104OHRX", 57600, 2000, 2000);
            await _serialPortForGps.Open("AH03F3RY", 57600, 2000, 2000);

            if (_useRtk)
            {
                var config = await "rtkGps.config".ReadStringFromFile();

                if (string.IsNullOrEmpty(config))
                {
                    Debug.WriteLine("rtkGps.config file is empty. Trying defaults.");
                    config = "69.44.86.36,2101,P041_RTCM,user,passw,serial";
                }

                try
                {
                    var ntripClient = new NtripClientTcp("172.16.0.225", 8000, "", "", "", _lcd);
                    ntripClient.NtripDataArrivedEvent += NtripClient_NtripDataArrivedEvent;
                    ntripClient.Start();

                    

                    //var settings = config.Split(',');

                    //if (settings[5] != null && settings[5].Equals("serial", StringComparison.CurrentCultureIgnoreCase))
                    //{
                    //    var ntripClient = new NtripClientFona(settings[0], int.Parse(settings[1]), settings[2], settings[3], settings[4], _serialPort);
                    //    ntripClient.Start();
                    //}
                    //else
                    //{
                    //    var ntripClient = new NtripClientTcp(settings[0], int.Parse(settings[1]), settings[2], settings[3], settings[4], _serialPort);
                    //    ntripClient.Start();
                    //}
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Creating NTRIP client failed - {e}");
                }
            }

#pragma warning disable 4014
            Task.Factory.StartNew(async() =>
#pragma warning restore 4014
            {
                if (_lcd != null)
                    await _lcd.Write("RTK GPS Started...");

                while (true)
                {
                    var sentences = await _serialPortForGps.ReadString();

                    foreach (var sentence in sentences.Split('$').Where(s => s.Contains('\r') && s.Length > 16))
                    {
                        var latLon = sentence.ParseNmea();

                        if (latLon == null)
                            continue;

                        if (CurrentLatLon.Quality != latLon.Quality && _lcd != null)
                            await _lcd.WriteToFirstLine(latLon.Quality.ToString());

                        CurrentLatLon = latLon;
                    }
                }
            }, TaskCreationOptions.LongRunning);
        }

        private void NtripClient_NtripDataArrivedEvent(object sender, NtripEventArgs e)
        {
            //Task.Factory.StartNew(async () =>
            //{
            //await _serialPortForRtkCorrectionData.Write(e.NtripBytes);
            //});
#pragma warning disable 4014
            _serialPortForRtkCorrectionData.Write(e.NtripBytes);
#pragma warning restore 4014
        }

        #endregion
    }
}