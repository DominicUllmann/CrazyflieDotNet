using CrazyflieDotNet.Crazyflie;
using log4net;
using log4net.Config;
using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace CrazyflieDotNet.ConsoleClient
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
                crazyflie.Connect().Wait();

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
            crazyflie.ParamConfigurator.SetValue("flightmode.posSet", (byte)1);
            try
            {
                float vIncrement = 0.1f;
                float yawIncrement = 5;
                float vx = 0;
                float vy = 0;
                float vz = -0.4f; // ensure that it doesn't takeoff directly.
                float yaw = 0;

                var loop = true;
                while (loop)
                {
                    Log.InfoFormat($"Vx: {vx}, Vy: {vy}, vz: {vz}, Yaw: {yaw}.");

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
                                break;
                            // move up
                            case ConsoleKey.UpArrow:
                                vz += vIncrement;
                                break;
                            // move down
                            case ConsoleKey.DownArrow:
                                vz -= vIncrement;
                                break;
                            // yaw right
                            case ConsoleKey.RightArrow:
                                yaw += yawIncrement;
                                break;
                            // yaw left
                            case ConsoleKey.LeftArrow:
                                yaw -= yawIncrement;
                                break;
                            // move in positive y direction
                            case ConsoleKey.S:
                                vy += vIncrement;
                                break;
                            // move in negative y direction
                            case ConsoleKey.W:
                                vy -= vIncrement;
                                break;
                            // move in positive x direction
                            case ConsoleKey.D:
                                vx += vIncrement;
                                break;
                            // roll left
                            case ConsoleKey.A:
                                vy -= vIncrement;
                                break;
                            default:
                                Log.InfoFormat("Invalid key for action.");
                                break;
                        }
                    }

                    crazyflie.Commander.SendVelocityWorldSetpoint(vx, vy, vz, yaw);
                    Thread.Sleep(50);
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
                crazyflie.Commander.SendVelocityWorldSetpoint(0, 0, -0.2f, 0);
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
            crazyflie.Commander.SendVelocityWorldSetpoint(0, 0, -0.4f, 0);
            return true;
        }
    }
}
