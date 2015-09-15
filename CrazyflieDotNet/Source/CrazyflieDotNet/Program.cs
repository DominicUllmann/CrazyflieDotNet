﻿#region Imports

using System;
using System.Collections.Generic;
using System.Linq;
using CrazyflieDotNet.Crazyflie.TransferProtocol;
using CrazyflieDotNet.Crazyradio.Driver;
using log4net;
using log4net.Config;

#endregion

namespace CrazyflieDotNet
{
	/// <summary>
	///     Currently, this Program is only a small Test like executable for testing during development.
	/// </summary>
	internal class Program
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof (Program));

		private static void Main(string[] args)
		{
			SetUpLogging();

			// initial game pad testing. first, PS3 wired in via USB...
			Log.Debug("Running PS3 USB SlimDX tests...");
			var gamePadCrazyflieController = new GamePadCrazyflieController();
            gamePadCrazyflieController.Test();
            
            IEnumerable<ICrazyradioDriver> crazyradioDrivers = null;

			try
			{
				Log.Debug("Starting Crazyradio USB dongle tests.");

				crazyradioDrivers = CrazyradioDriver.GetCrazyradios();
			}
			catch (Exception ex)
			{
				Log.Error("Error getting Crazyradios.", ex);
			}

			if (crazyradioDrivers != null && crazyradioDrivers.Any())
			{
				var crazyradioDriver = crazyradioDrivers.First();
                var crazyRadioMessenger = new CrazyradioMessenger(crazyradioDriver);

                try
				{
					crazyradioDriver.Open();

					var scanResults = crazyradioDriver.ScanChannels(RadioChannel.Channel0, RadioChannel.Channel125);
					if (scanResults.Any())
					{
						var firstScanResult = scanResults.First();

						var dataRateWithCrazyflie = firstScanResult.DataRate;
						var channelWithCrazyflie = firstScanResult.Channels.First();

						crazyradioDriver.DataRate = dataRateWithCrazyflie;
						crazyradioDriver.Channel = channelWithCrazyflie;

                        IPacket ackPacket = null;
                        byte[] ackPacketBytes = null;

                        Log.InfoFormat("Ping Packet Request: {0}", PingPacket.Instance);
                        ackPacket = crazyRadioMessenger.SendMessage(PingPacket.Instance);
                        Log.InfoFormat("ACK Response: {0}", ackPacket);


                        ushort thrustIncrements = 1000;
                        float pitchIncrements = 5;
                        float yawIncrements = 2;
                        float rollIncrements = 5;
                        ushort thrust = 10000;
                        float pitch = 0;
                        float yaw = 0;
                        float roll = 0;

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
                                        var commanderPacket = new CommanderPacket(roll, pitch, yaw, thrust=10000);
                                        Log.InfoFormat("Commander Packet Request: {0}", commanderPacket);
                                        ackPacket = crazyRadioMessenger.SendMessage(commanderPacket);
                                        Log.InfoFormat("ACK Response: {0}", ackPacket);

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
                                                        pauseLoop = loop = false;
                                                        break;
                                                }
                                            }
                                        }
                                        break;
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

                            {
                                var commanderPacket = new CommanderPacket(roll, pitch, yaw, thrust);
                                Log.InfoFormat("Commander Packet Request: {0}", commanderPacket);
                                ackPacket = crazyRadioMessenger.SendMessage(commanderPacket);
                                Log.InfoFormat("ACK Response: {0}", ackPacket);
                            }
						}
					}
					else
					{
						Log.Warn("No Crazyflie Quadcopters found!");
					}
				}
				catch (Exception ex)
				{
					Log.Error("Error testing Crazyradio.", ex);
				}
				finally
				{
                    crazyRadioMessenger.SendMessage(new CommanderPacket(0, 0, 0, 0));
                    crazyradioDriver.Close();
				}
			}
			else
			{
				Log.Warn("No Crazyradio USB dongles found!");
			}

			Log.Info("Sleepy time...Hit ESC to quit.");

			var sleep = true;
			while (sleep)
			{
				if (Console.KeyAvailable && Console.ReadKey().Key == ConsoleKey.Escape)
				{
					sleep = false;
				}
			}
		}

		private static void SetUpLogging()
		{
			BasicConfigurator.Configure();
		}
	}
}
