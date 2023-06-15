using GameReaderCommon;
using SimHub.Plugins;
using System;
using System.Reflection;
using System.Windows.Media;

namespace Bojote.DashBreeze
{
    [PluginDescription("Controls fans based on your simulator's speed telemetry data to simulate the feeling of wind rushing against your face")]
    [PluginAuthor("Bojote")]
    [PluginName("DashBreeze")]

    public class Main : IPlugin, IDataPlugin, IWPFSettingsV2
    {
        public static class Constants
        {
            public const string HandShakeSnd        = "DashBreeze_OK";
            public const string HandShakeRcv        = "BINGO";
            public const string uniqueID            = "MADUROESGAY";
            public const string uniqueIDresponse    = "ACK";
            public const string QueryStatus         = "KNOCK-KNOCK";
        }

        public SerialConnection SerialConnection { get; set; }

        // Declared for gameData
        public static bool SerialOK = false;

        public MainSettings Settings;

        /// <summary>
        /// Instance of the current plugin manager
        /// </summary>
        public PluginManager PluginManager { get; set; }

        /// <summary>
        /// Gets the left menu icon. Icon must be 24x24 and compatible with black and white display.
        /// </summary>
        public ImageSource PictureIcon => this.ToIcon(Properties.Resources.pluginicon);

        /// <summary>
        /// Gets a short plugin title to show in left menu. Return null if you want to use the title as defined in PluginName attribute.
        /// </summary>
        public string LeftMenuTitle => "DashBreeze Settings";

        /// <summary>
        /// Called one time per game data update, contains all normalized game data,
        /// raw data are intentionnally "hidden" under a generic object type (A plugin SHOULD NOT USE IT)
        ///
        /// This method is on the critical path, it must execute as fast as possible and avoid throwing any error
        ///
        /// </summary>
        /// <param name="pluginManager"></param>
        /// <param name="data">Current game data, including current and previous data frame.</param>
        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            // Define the value of our property (declared in init)
            if (data.GameRunning)
            {
                if (data.OldData != null && data.NewData != null)
                {
                    if (SerialOK)
                    {
                        // this.TriggerEvent("MaxSpeed");
                    }
                }
            }
        }

        /// <summary>
        /// Called at plugin manager stop, close/dispose anything needed here !
        /// Plugins are rebuilt at game change
        /// </summary>
        /// <param name="pluginManager"></param>
        public void End(PluginManager pluginManager)
        {
            SimHub.Logging.Current.Info("BEGIN -> End");

            // Save settings
            this.SaveCommonSettings("DashBreeze", Settings);

            SimHub.Logging.Current.Info("END -> End");
        }

        /// <summary>
        /// Returns the settings control, return null if no settings control is required
        /// </summary>
        /// <param name="pluginManager"></param>
        /// <returns></returns>
        public System.Windows.Controls.Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            return new SettingsControl(this);
        }

        /// <summary>
        /// Called once after plugins startup
        /// Plugins are rebuilt at game change
        /// </summary>
        /// <param name="pluginManager"></param>
        public void Init(PluginManager pluginManager)
        {
            // Start Logging
            SimHub.Logging.Current.Info("Starting DashBreeze Plugin...");

            SimHub.Logging.Current.Info("BEGIN -> Init");

            // Load settings
            Settings = this.ReadCommonSettings<MainSettings>("DashBreeze", () => CreateDefaultSettings() );

            if (SerialConnection == null)
            {
                SimHub.Logging.Current.Info($"Will create a new SerialConnection() object");
                SerialConnection = new SerialConnection();
            }
            else
            {
                SimHub.Logging.Current.Info($"A SerialConnection() already existed, we'll use that one");
            }

            // Declare a property available in the property list, this gets evaluated "on demand" (when shown or used in formulas)
            this.AttachDelegate("CurrentDateTime", () => DateTime.Now);

            // Declare an action which can be called
            this.AddAction("ToggleLiveFan", (a, b) =>
            {
                if (SerialOK)
                    SerialOK = false;
                else
                    SerialOK = true;
            });

            // Declare an event
            this.AddEvent("MaxSpeed");

            // Declare an action which can be called
            this.AddAction("IncrementFanSpeed",(a, b) =>
            {
            Settings.FanSpeed++;
            });

            // Declare an action which can be called
            this.AddAction("DecrementFanSpeed", (a, b) =>
            {
                Settings.FanSpeed--;
            });

            SimHub.Logging.Current.Info("END -> Init");
        }

        // When loading for the first time if not settings exists we load the following
        private MainSettings CreateDefaultSettings()
        {
            // Create a new instance of MainSettings and set default values
            var settings = new MainSettings
            {
                SelectedSerialDevice = "None",
                ConnectToSerialDevice = false,
                SelectedBaudRate = "115200",
                FanSpeed = 10,
                FanIntensity = 15
            };
            return settings;
        }
        
        public static string PrintObjectProperties(object obj)
        {
            Type objType = obj.GetType();
            PropertyInfo[] properties = objType.GetProperties();

            string _value = null;
            foreach (PropertyInfo property in properties)
            {
                object value = property.GetValue(obj, null);
                _value += ($"{property.Name} => {value}\n");
            }
            return _value;
        }

    }
}