using CrazyflieDotNet.Crazyflie;
using CrazyflieDotNet.Crazyflie.Feature;
using CrazyflieDotNet.Crazyflie.Feature.Localization;
using CrazyflieDotNet.Crazyflie.Feature.Log;
using CrazyflieDotNet.Crazyradio;
using log4net;
using log4net.Config;
using System;
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
                var radioManager = CrazyRadioManager.Instance;
                var uri = radioManager.Scan().FirstOrDefault();
                if (uri == null)
                {
                    throw new ApplicationException("no crazyflie detected");
                }
                var crazyflie = new CrazyflieCopter(radioManager);
                crazyflie.Connect(uri).Wait();

                try
                {
                    LoggingExample(crazyflie);
                    ParameterExample(crazyflie);

                    Console.WriteLine("Sleepy time...Wait for takeoff demo Press ENTER.");
                    WaitForKey(ConsoleKey.Enter);

                    CommanderExample(crazyflie);

                    Console.WriteLine("Sleepy time...Wait for high level navigator demo Press ENTER.");
                    WaitForKey(ConsoleKey.Enter);

                    NavigationExample(crazyflie);

                    Console.WriteLine("Sleepy time...Wait for high level demo Press ENTER.");
                    WaitForKey(ConsoleKey.Enter);

                    HighLevelCommandExample(crazyflie);

                    Console.WriteLine("Sleepy time...Hit ESC to quit.");
                    WaitForKey(ConsoleKey.Escape);

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

        private static void WaitForKey(ConsoleKey key)
        {
            var sleep = true;
            while (sleep)
            {
                if (Console.KeyAvailable && Console.ReadKey().Key == key)
                {
                    sleep = false;
                }
            }
        }

        private static void NavigationExample(CrazyflieCopter crazyflie)
        {
            var task = crazyflie.ParamConfigurator.RefreshParameterValue("flightmode.posSet");
            task.Wait();
            Log.Info("flightmode.posSet before: " + task.Result);

            task = crazyflie.ParamConfigurator.RefreshParameterValue("stabilizer.controller");
            task.Wait();
            Log.Info("stabilizer.controller before: " + task.Result);

            //crazyflie.ParamConfigurator.SetValue("stabilizer.controller", (byte)2).Wait();

            //crazyflie.ParamConfigurator.SetValue("flightmode.posSet", (byte)1).Wait();
            //crazyflie.ParamConfigurator.SetValue("stabilizer.controller", (byte)1);
            var navigator = new Navigator(crazyflie);
            try
            {
                navigator.PositionUpdate += Navigator_PositionUpdate;
                navigator.Start(100);

                try
                {
                    navigator.WaitForCalibratedPosition(TimeSpan.FromSeconds(15)).Wait();
                }
                catch (Exception ex)
                {
                    Log.Error("didn't found a calibration; abort demo", ex);
                    return;
                }

                navigator.Takeoff(1f).Wait();
                Log.Warn("Takeoff now done");
                navigator.NavigateTo(0.4f, 0.4f, 1f).Wait();
                navigator.NavigateTo(1.6f, 0.4f, 1f).Wait();
                navigator.NavigateTo(1.6f, 1.1f, 1f).Wait();
                navigator.NavigateTo(0.4f, 1.1f, 1f).Wait();
                navigator.NavigateTo(0.4f, 0.4f, 1f).Wait();

                navigator.Land(-0.3f).Wait();

            }
            finally
            {

                navigator.Stop().Wait();
            }
        }                         

        private static void Navigator_PositionUpdate(object sender, PositionUpdateEventArgs args)
        {
            Log.Info("current estimated position: " + args.CurrentPosition);
            Log.Info($"current variance: {((Navigator)sender).VarianceX} / {((Navigator)sender).VarianceY} / {((Navigator)sender).VarianceZ}");
        }

        private static void HighLevelCommandExample(CrazyflieCopter crazyflie)
        {
            if (crazyflie.ParamConfigurator.IsParameterKnown("commander.enHighLevel"))
            {
                crazyflie.HighLevelCommander.Enable().Wait();
                // To enable the mellinger controller:
                //crazyflie.ParamConfigurator.SetValue("stabilizer.controller", (byte)2).Wait();
                // To enable the default controller:
                //crazyflie.ParamConfigurator.SetValue("stabilizer.controller", (byte)1).Wait();

                crazyflie.ParamConfigurator.SetValue("kalman.resetEstimation", (byte)1).Wait();
                Thread.Sleep(10);
                crazyflie.ParamConfigurator.SetValue("kalman.resetEstimation", (byte)0).Wait();
                Thread.Sleep(1000);

                try
                {
                    crazyflie.HighLevelCommander.Takeoff(0.3f, 2f);
                    Thread.Sleep(2000);
                    crazyflie.HighLevelCommander.Land(0, 2f);
                    Thread.Sleep(2100);
                }
                finally
                {
                    crazyflie.HighLevelCommander.Stop();
                    crazyflie.HighLevelCommander.Disable().Wait();
                }

            }
            else
            {
                Log.Error("Highlevel commander not available. Update Crazyflie firmware.");
            }
        }

        private static void ParameterExample(CrazyflieCopter crazyflie)
        {
            crazyflie.ParamConfigurator.RequestUpdateOfAllParams().Wait();
            // alternatively you can also use event AllParametersUpdated
            var result = crazyflie.ParamConfigurator.GetLoadedParameterValue("system.selftestPassed");
            Log.Info($"self test passed: {Convert.ToBoolean(result)}");
        }

        private static void ParamConfigurator_AllParametersUpdated(object sender, AllParamsUpdatedEventArgs args)
        {
            var result = ((ICrazyflieParamConfigurator)sender).GetLoadedParameterValue("system.selftestPassed");
            Log.Info($"self test passed: {Convert.ToBoolean(result)}");
        }

        private static void CommanderExample(CrazyflieCopter crazyflie)
        {
            // use a velocity of 0.2m/sec for 2 seconds in z direction to start.
            for (int i = 0; i < 10; i++)
            {                
                crazyflie.Commander.SendVelocityWorldSetpoint(0, 0, 0.2f, 0);
                Thread.Sleep(200);
            }
            // use a velocity of -0.2m/sec for 2 seconds in z direction to land.
            for (int i = 0; i < 10; i++)
            {
                crazyflie.Commander.SendVelocityWorldSetpoint(0, 0, -0.2f, 0);
                Thread.Sleep(200);
            }
        }

        private static void LoggingExample(CrazyflieCopter crazyflie)
        {

            if (!crazyflie.Logger.IsLogVariableKnown("stabilizer.roll"))
            {
                Log.Warn("stabilizer.roll not a known log variable");
            }
            var config = crazyflie.Logger.CreateEmptyLogConfigEntry("Stabilizer", 100);
            config.AddVariable("stabilizer.roll", "float");
            config.AddVariable("stabilizer.pitch", "float");
            config.AddVariable("stabilizer.yaw", "float");
            config.LogDataReceived += Config_LogDataReceived;
            crazyflie.Logger.AddConfig(config);
            crazyflie.Logger.StartConfig(config);

            Thread.Sleep(1000);

            crazyflie.Logger.StopConfig(config);
            crazyflie.Logger.DeleteConfig(config);

            Thread.Sleep(1000);
        }

        private static void Config_LogDataReceived(object sender, LogDataReceivedEventArgs e)
        {
            Log.Info($"log received: {e.TimeStamp}  | " +
                $"roll: {e.GetVariable("stabilizer.roll") } ,pitch: { e.GetVariable("stabilizer.pitch") }, yaw: {e.GetVariable("stabilizer.yaw")}");
        }

    }
}
