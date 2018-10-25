using CrazyflieDotNet.Crazyflie;
using CrazyflieDotNet.Crazyradio;
using log4net;
using log4net.Config;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace CrarzyflieDotNet.ConsoleClient.LowLevel
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
                var radioManager = CrazyradioManager.Instance;
                var uri = radioManager.Scan().FirstOrDefault();
                if (uri == null)
                {
                    throw new ApplicationException("no crazyflie detected");
                }
                var crazyflie = new CrazyflieCopter(radioManager);
                
                crazyflie.Connect(uri).Wait();

                try
                {
                    Fly(crazyflie);

                }
                catch (Exception ex)
                {
                    Log.Error("Error flying crazyfly.", ex);
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

        private static void ResetPositionEstimator(CrazyflieCopter crazyflie)
        {
            crazyflie.ParamConfigurator.SetValue("kalman.resetEstimation", (byte)1).Wait();
            Thread.Sleep(100);
            crazyflie.ParamConfigurator.SetValue("kalman.resetEstimation", (byte)0).Wait();
            Thread.Sleep(2000);
        }

        private static void Fly(CrazyflieCopter crazyflie)
        {
            ResetPositionEstimator(crazyflie);
            crazyflie.ParamConfigurator.SetValue("flightmode.posSet", (byte)0);
            try
            {
                ushort thrustIncrements = 1000;
                float pitchIncrements = 5;
                float yawIncrements = 2;
                float rollIncrements = 5;
                ushort thrust = 15000;
                float pitch = 0;
                float yaw = 0;
                float roll = 0;

                for (int i = 0; i < 10; i++)
                {
                    crazyflie.Commander.SendSetPoint(0, 0, 0, 0);
                }
                Thread.Sleep(200);
                crazyflie.Commander.SendSetPoint(0, 0, 0, 15000);


                var loop = true;
                while (loop)
                {
                    Log.InfoFormat("Thrust: {0}, Pitch: {1}, Roll: {2}, Yaw: {3}.", thrust, pitch, roll, yaw);

                    if (Console.KeyAvailable)
                    {
                        switch (Console.ReadKey().Key)
                        {
                            // end
                            case ConsoleKey.Escape:
                                loop = false;
                                break;
                            // pause
                            case ConsoleKey.Spacebar:
                                loop = LandAndPause(crazyflie);
                                continue;
                            // thrust up
                            case ConsoleKey.UpArrow:
                                thrust += thrustIncrements;
                                break;
                            // thrust down
                            case ConsoleKey.DownArrow:
                                thrust -= thrustIncrements;
                                break;
                            // yaw right
                            case ConsoleKey.RightArrow:
                                yaw += yawIncrements;
                                break;
                            // yaw left
                            case ConsoleKey.LeftArrow:
                                yaw -= yawIncrements;
                                break;
                            // pitch backward
                            case ConsoleKey.S:
                                pitch += pitchIncrements;
                                break;
                            // pitch forward
                            case ConsoleKey.W:
                                pitch -= pitchIncrements;
                                break;
                            // roll right
                            case ConsoleKey.D:
                                roll += rollIncrements;
                                break;
                            // roll left
                            case ConsoleKey.A:
                                roll -= rollIncrements;
                                break;
                            default:
                                Log.InfoFormat("Invalid key for action.");
                                break;
                        }
                    }

                    Thread.Sleep(20);
                    crazyflie.Commander.SendSetPoint(roll, pitch, yaw, thrust);

                }
            }
            catch (Exception)
            {
                try
                {
                    crazyflie.Commander.SendStopSetPoint();
                }
                catch (Exception)
                {
                }

                throw;
            }

            crazyflie.Commander.SendStopSetPoint();
        }

        private static bool LandAndPause(CrazyflieCopter crazyflie)
        {
            for (int i = 0; i < 10; i++)
            {
                crazyflie.Commander.SendSetPoint(0, 0, 0, (ushort)(15000 - (10 * i)));
                Thread.Sleep(200);
            }
            crazyflie.Commander.SendStopSetPoint();

            Log.InfoFormat("Paused...Hit SPACE to resume, ESC to quit.");

            var pauseLoop = true;
            while (pauseLoop)
            {
                if (Console.KeyAvailable)
                {
                    switch (Console.ReadKey().Key)
                    {
                        // resume
                        case ConsoleKey.Spacebar:
                            pauseLoop = false;
                            break;
                        // end
                        case ConsoleKey.Escape:
                            pauseLoop = false;
                            return false;
                    }
                }
            }

            ResetPositionEstimator(crazyflie);
            return true;
        }
    }

}
