using CrazyflieDotNet.Crazyflie;
using CrazyflieDotNet.Crazyradio;
using log4net;
using log4net.Config;
using SlimDX.DirectInput;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace CrazyflieDotNet.ConsoleJoystick
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
                for (int i = 0; i < 10; i++)
                {
                    crazyflie.Commander.SendSetPoint(0, 0, 0, 0);
                }
                Thread.Sleep(200);
                crazyflie.Commander.SendSetPoint(0, 0, 0, 15000);


                // Init
                float roll = 0;
                float pitch = 0;
                float yaw = 0;
                ushort thrust = 0;

                // Max/min values
                float rollRange = 50;
                float pitchRange = 50;
                float yawRange = 100;
                ushort thrustRange = 50000;

                // Stick ranges
                int stickRange = 1000;

                // Get first attached game controller found
                var directInput = new DirectInput();
                var attahcedGameControllerDevices = directInput.GetDevices(DeviceClass.GameController, DeviceEnumerationFlags.AttachedOnly);
                if (!attahcedGameControllerDevices.Any())
                {
                    throw new ApplicationException("No available game controllers found.");
                }
                var attachedDeviceInstance = attahcedGameControllerDevices.First();
                var joystick = new Joystick(directInput, attachedDeviceInstance.InstanceGuid);

                foreach (DeviceObjectInstance doi in joystick.GetObjects(ObjectDeviceType.Axis))
                {
                    joystick.GetObjectPropertiesById((int)doi.ObjectType).SetRange(-1 * stickRange, stickRange);
                }

                joystick.Properties.AxisMode = DeviceAxisMode.Absolute;
                joystick.Acquire();
                var joystickState = new JoystickState();

                var loop = true;
                while (loop)
                {
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
                            default:
                                Log.InfoFormat("Invalid key for action.");
                                break;
                        }
                    }

                    // Poll the device and get state
                    joystick.Poll();
                    joystick.GetCurrentState(ref joystickState);

                    // Get buttons pressed info
                    var stringWriter = new StringWriter();
                    var buttons = joystickState.GetButtons();
                    var anyButtonsPressed = buttons.Any(b => b == true);
                    if (anyButtonsPressed)
                    {
                        for (int buttonNumber = 0; buttonNumber < buttons.Length; buttonNumber++)
                        {
                            if (buttons[buttonNumber] == true)
                            {
                                stringWriter.Write(string.Format("{0}", buttonNumber));
                            }
                        }
                    }
                    var buttonsPressedString = stringWriter.ToString().Trim();

                    // Joystick info
                    var leftStickX = joystickState.X;
                    var leftStickY = joystickState.Y;
                    var rightStickX = joystickState.RotationX;
                    var rightStickY = joystickState.RotationY;

                    roll = rollRange * rightStickX / stickRange;
                    pitch = pitchRange * rightStickY / stickRange;
                    yaw = yawRange * leftStickX / stickRange;
                    thrust = (ushort)(leftStickY > 0 ? 0 : thrustRange * -1 * leftStickY / stickRange);

                    var infoString = String.Format("LX:{0,7}, LY:{1,7}, RX:{2,7}, RY:{3,7}, Buttons:{4,7}.\tRoll:{5, 7}, Pitch:{6, 7}, Yaw:{7, 7}, Thrust:{8, 7}.", leftStickX, leftStickY, rightStickX, rightStickY, buttonsPressedString, roll, pitch, yaw, thrust);
                    Console.WriteLine(infoString);

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
