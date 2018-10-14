﻿using CrazyflieDotNet.Crazyflie;
using CrazyflieDotNet.Crazyflie.Feature;
using CrazyflieDotNet.Crazyflie.Feature.Log;
using CrazyflieDotNet.CrazyMessaging;
using CrazyflieDotNet.Crazyradio.Driver;
using log4net;
using log4net.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace CrazyflieDotNet.Example
{
    class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        static void Main(string[] args)
        {
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

            try
            {
                var crazyflie = new CrazyflieCopter();
                crazyflie.Connect();

                try
                {

                    LoggingExample(crazyflie);

                    Console.WriteLine("Sleepy time...Hit ESC to quit.");

                    var sleep = true;
                    while (sleep)
                    {
                        if (Console.KeyAvailable && Console.ReadKey().Key == ConsoleKey.Escape)
                        {
                            sleep = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Error testing crazyfly.", ex);                    
                }
                crazyflie?.Disconnect();
            }
            catch (Exception ex)
            {
                Log.Error("Error setting up radio.", ex);
            }

            Console.WriteLine("ended.");
            Console.ReadLine();
        }

        private static void LoggingExample(CrazyflieCopter crazyflie)
        {
           
            var config = crazyflie.Logger.CreateEmptyLogConfigEntry("Stabilizer", 10);
            config.AddVariable("stabilizer.roll", "float");
            config.AddVariable("stabilizer.pitch", "float");
            config.AddVariable("stabilizer.yaw", "float");
            config.LogDataReceived += Config_LogDataReceived;
            crazyflie.Logger.AddConfig(config);
            crazyflie.Logger.StartConfig(config);

            Console.ReadLine();
        }

        private static void Config_LogDataReceived(object sender, LogDataReceivedEventArgs e)
        {
            Log.Info($"log received: {e.TimeStamp}  | " +
                $"roll: {e.GetVariable("stabilizer.roll") } ,pitch: { e.GetVariable("stabilizer.pitch") }, yaw: {e.GetVariable("stabilizer.yaw")}");
        }

    }
}
