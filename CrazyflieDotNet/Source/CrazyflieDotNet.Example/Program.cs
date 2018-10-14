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
                var crazyradioDriver = SetupCrazyflieDriver();
                try
                {

                    LoggingExample(crazyradioDriver);

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
                    crazyradioDriver?.Close();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error setting up radio.", ex);
            }

            Console.WriteLine("ended.");
            Console.ReadLine();
        }

        private static void LoggingExample(ICrazyradioDriver crazyradioDriver)
        {
            var communicator = new CrtpCommunicator(crazyradioDriver);
            communicator.Start();
            var commander = new Commander(communicator);
            var logger = new Logger(communicator);
            logger.RefreshToc();

            Thread.Sleep(12000);

            var config = new LogConfig(communicator, logger, "Stabilizer", 10);
            config.AddVariable("stabilizer.roll", "float");
            config.AddVariable("stabilizer.pitch", "float");
            config.AddVariable("stabilizer.yaw", "float");
            logger.AddConfig(config);
            config.LogDataReceived += Config_LogDataReceived;
            config.Start();

            Console.ReadLine();
        }

        private static void Config_LogDataReceived(object sender, LogDataReceivedEventArgs e)
        {
            Console.WriteLine("log received: " + e.TimeStamp);
            Console.WriteLine("roll: " + e.GetVariable("stabilizer.roll"));
            Console.WriteLine("pitch: " + e.GetVariable("stabilizer.pitch"));
            Console.WriteLine("yaw: " + e.GetVariable("stabilizer.yaw"));
        }

        private static ICrazyradioDriver SetupCrazyflieDriver()
        {
            IEnumerable<ICrazyradioDriver> crazyradioDrivers = null;

            try
            {
                // Scan for connected Crazyradio USB dongles
                crazyradioDrivers = CrazyradioDriver.GetCrazyradios();
            }
            catch (Exception ex)
            {
                var msg = "Error getting Crazyradio USB dongle devices connected to computer.";
                Log.Error(msg, ex);
                throw new ApplicationException(msg, ex);
            }

            // If we found any
            if (crazyradioDrivers != null && crazyradioDrivers.Any())
            {
                // Use first available Crazyradio dongle
                var crazyradioDriver = crazyradioDrivers.First();

                try
                {
                    // Initialize driver
                    crazyradioDriver.Open();

                    // Scan for any Crazyflie quadcopters ready for communication
                    var scanResults = crazyradioDriver.ScanChannels();
                    if (scanResults.Any())
                    {
                        // Use first online Crazyflie quadcopter found
                        var firstScanResult = scanResults.First();

                        // Set CrazyradioDriver's DataRate and Channel to that of online Crazyflie
                        var dataRateWithCrazyflie = firstScanResult.DataRate;
                        var channelWithCrazyflie = firstScanResult.Channels.First();
                        crazyradioDriver.DataRate = dataRateWithCrazyflie;
                        crazyradioDriver.Channel = channelWithCrazyflie;

                        return crazyradioDriver;
                    }
                    else
                    {
                        Log.Warn("No Crazyflie quadcopters available for communication.");
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    var msg = "Error initializing Crazyradio USB dongle for communication with a Crazyflie quadcopter.";
                    Log.Error(msg, ex);
                    throw new ApplicationException(msg, ex);
                }
            }
            else
            {
                Log.Warn("No Crazyradio USB dongles found!");
                return null;
            }
        }
    }
}
