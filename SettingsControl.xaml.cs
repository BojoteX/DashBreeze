using System;
using System.ComponentModel;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Bojote.DashBreeze
{
    /// <summary>
    /// Logic for SettingsControl.xaml
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        // Needed for internal stuff
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1);
        private ManagementEventWatcher _watcher;

        // Some custom variables 
        public bool isReady = false; // Declare the shared variable

        // Static variables
        private static bool watcherStarted = false;
        private static string prevDevicePermanent; // Declare the shared variable
        private static bool prevDeviceStatePermanent; // Declare the shared variable

        public SerialConnection SerialConnection { get; set; }

        public DashBreeze Plugin { get; }

        public SettingsControl()
        {
            SimHub.Logging.Current.Info("BEGIN -> SettingsControl1");

            // Initialization
            InitializeComponent();

            SimHub.Logging.Current.Info("END -> SettingsControl1");
        }

        public SettingsControl(DashBreeze plugin) : this()
        {
            SimHub.Logging.Current.Info("BEGIN -> SettingsControl2");

            this.Plugin = plugin;
            DataContext = Plugin.Settings;

            // Initialize SerialConnection if none was set
            if (Plugin.SerialConnection != null)
                SerialConnection = Plugin.SerialConnection;

            // Initialize SerialConnection for gTenxor (if none was set)
            if (Plugin.SerialConnection == null && SerialConnection != null)
                Plugin.SerialConnection = SerialConnection;

            // If after the above checks, both are still null, create a new SerialConnection object.
            if (Plugin.SerialConnection == null && SerialConnection == null)
            {
                SerialConnection = new SerialConnection();
                Plugin.SerialConnection = SerialConnection;
                SimHub.Logging.Current.Info("Instantiated new SerialConnection");
            }

            // Load the initial list of serial devices
            SerialConnection.LoadSerialDevices();

            // Bind the devices to the ComboBox
            SerialDevicesComboBox.ItemsSource = SerialConnection.SerialDevices;

            // Bind the device speeds to the ComboBox
            BaudRateComboBox.ItemsSource = SerialConnection.BaudRates;

            // Need to Load some of my events
            Loaded -= SettingsControl_Loaded;
            Loaded += SettingsControl_Loaded;

            // And we also need to be able to unload them
            Unloaded -= SettingsControl_Unloaded;
            Unloaded += SettingsControl_Unloaded;

            // We need to make sure there is only one event always active
            SerialConnection.OnDataReceived -= HandleDataReceived;
            SerialConnection.OnDataReceived += HandleDataReceived;

            // Load Events from SerialConnection class
            SerialConnection.PropertyChanged -= SerialConnection_PropertyChanged;
            SerialConnection.PropertyChanged += SerialConnection_PropertyChanged;

            // Load Events from for combo box changes
            SerialDevicesComboBox.SelectionChanged -= ComboBox_SelectionChanged;
            SerialDevicesComboBox.SelectionChanged += ComboBox_SelectionChanged;

            // Connect automatically if setting is active
            Task.Run(() => TryConnect(null, 0));

            // Need to figure out a more efficient way to do this and limit it to com ports only..
            WatchForUSBChanges();

            isReady = true;

            SimHub.Logging.Current.Info("END -> SettingsControl2");
        }

        public void WatchForUSBChanges()
        {
            SimHub.Logging.Current.Info($"Will try to watch for USB events...");
            if (!watcherStarted)
            {
                string sqlQuery = "SELECT * FROM __InstanceOperationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity' AND TargetInstance.PNPClass = 'Ports'";

                _watcher = new ManagementEventWatcher();
                _watcher.EventArrived += new EventArrivedEventHandler(USBChangedEvent);
                _watcher.Query = new WqlEventQuery(sqlQuery);
                _watcher.Start();

                watcherStarted = true;
                SimHub.Logging.Current.Info($"Confirmed! watching for USB events");
            }
            else
            {
                SimHub.Logging.Current.Info($"Already doing it! there was no need to load");
            }
        }

        void USBChangedEvent(object sender, EventArrivedEventArgs e)
        {
            SimHub.Logging.Current.Info($"BEGIN -> USBChanged for {DashBreeze.PluginName} from {sender}");

            string eventType = e.NewEvent.ClassPath.ClassName;
            ManagementBaseObject targetInstance = (ManagementBaseObject)e.NewEvent["TargetInstance"];

            switch (eventType)
            {
                case "__InstanceDeletionEvent":
                    Dispatcher.Invoke(async () =>
                    {
                        var oldDevices = SerialConnection.SerialDevices.ToList(); // Create a copy of the old device list
                        string prevSelection = SerialDevicesComboBox.SelectedItem as string;

                        if (ConnectCheckBox.IsChecked == true)
                            prevDeviceStatePermanent = true;
                        else
                            prevDeviceStatePermanent = false;

                        if (prevDevicePermanent == null)
                            prevDevicePermanent = prevSelection;

                        // Allow time after disconnection to refresh the list
                        await Task.Delay(500);

                        for (int i = 1; i < 6; i++) // Attempt to update the list 5 times
                        {
                            Plugin.SerialConnection.LoadSerialDevices();
                            var newDevices = SerialConnection.SerialDevices;

                            if (!oldDevices.SequenceEqual(newDevices))
                            {
                                SimHub.Logging.Current.Info($"All good! no need to iterate more as a USB change was indeed detected and updated in our list of devices");
                                // The device lists are different, we can stop retrying
                                break;
                            }
                            else
                            {
                                // Interrupt all serial comunication when about to disconnect
                                DashBreeze.SerialOK = false;
                                await Task.Delay(500); // Delay to allow for the above variable to update

                                Plugin.SerialConnection.ForcedDisconnect();
                                // Wait for a while before the next try
                                await Task.Delay(1000); // Delay for one second
                                GC.Collect();
                                GC.WaitForPendingFinalizers();
                                await Task.Delay(500); // Delay to allow for garbage collector to do its thing
                                SimHub.Logging.Current.Info($"Serial devices list was not refreshed for {DashBreeze.PluginName}, will try again... Attempt Number {i}, let me also force a Disconnect");

                                if (i == 5) // I tried 5 times...
                                {
                                    SimHub.Logging.Current.Info($"Find some extreme methods for {DashBreeze.PluginName} as nothing works!");
                                    Plugin.SerialConnection.LoadSerialDevices();
                                }
                            }
                        }

                        SimHub.Logging.Current.Info($"Previous selection was {prevSelection} for {DashBreeze.PluginName} (also have {prevDevicePermanent}) and auto connect is {prevDeviceStatePermanent}");

                        SerialDevicesComboBox.ItemsSource = SerialConnection.SerialDevices;
                        string currentSelection = SerialDevicesComboBox.SelectedItem as string;

                        if (currentSelection != prevSelection)
                        {
                            SimHub.Logging.Current.Info($"InstanceDeletionEvent: Here is where we disconnect {DashBreeze.PluginName} as it disappeared from my list while being the selected option");
                            Plugin.Settings.SelectedSerialDevice = "None";
                            SerialDevicesComboBox.SelectedItem = "None";
                            ConnectCheckBox.IsChecked = false;
                        }
                        else
                        {
                            SimHub.Logging.Current.Info($"Do we have to re-sconnect {DashBreeze.PluginName}?");
                            ConnectCheckBox.IsChecked = false;
                            await Task.Delay(500); // Delay to allow for the above variable to update
                            ConnectCheckBox.IsChecked = true;
                            await Task.Delay(500); // Delay to allow for the above variable to update
                            DashBreeze.SerialOK = true;
                        }
                        SimHub.Logging.Current.Info($"DISCONNECT: The last connected device was {prevDevicePermanent} and Connect Checkbox was set to {prevDeviceStatePermanent}");
                    });
                    break;
                case "__InstanceCreationEvent":
                    Dispatcher.Invoke(async () =>
                    {
                        bool isChecked;
                        // Now load the devices
                        if (prevDeviceStatePermanent == true)
                            isChecked = true;
                        else
                            isChecked = false;

                        SimHub.Logging.Current.Info($"InstanceCreationEvent: Here is where we re-connect {DashBreeze.PluginName}");

                        // Allow time after re-connection to refresh the list
                        await Task.Delay(500);

                        SerialConnection.LoadSerialDevices();
                        SerialDevicesComboBox.ItemsSource = SerialConnection.SerialDevices;

                        bool SelectedDev = SerialDevicesComboBox.SelectedItem is string selectedDevice;
                        if (SelectedDev)
                        {
                            if (prevDevicePermanent != null)
                            {
                                SerialDevicesComboBox.SelectedItem = prevDevicePermanent;
                            }
                            ConnectCheckBox.IsChecked = isChecked;
                            Plugin.Settings.ConnectToSerialDevice = true;
                        }
                        else
                        {
                            SimHub.Logging.Current.Info($"InstanceCreationEvent: Here is where we disconnect {DashBreeze.PluginName}");
                            Plugin.Settings.SelectedSerialDevice = "None";
                            SerialDevicesComboBox.SelectedItem = "None";
                        }
                        SimHub.Logging.Current.Info($"CONNECT: The last connected device was {prevDevicePermanent} and Connect Checkbox was set to {prevDeviceStatePermanent}");
                    });
                    break;
            }
            SimHub.Logging.Current.Info("END -> USBChanged");
        }

        private void SerialConnection_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            SimHub.Logging.Current.Info($"BEGIN -> PropertyChanged for {DashBreeze.PluginName} from {sender}");

            if (e.PropertyName == nameof(SerialConnection.SerialDevices))
            {
                string prevDevice;
                bool prevDeviceState;

                // Set my current values
                prevDevice = SerialDevicesComboBox.SelectedItem as string;
                if (ConnectCheckBox.IsChecked == true)
                    prevDeviceState = true;
                else
                    prevDeviceState = false;

                // Update the ComboBox items when SerialDevices property changes
                // Now load the devices
                SerialDevicesComboBox.ItemsSource = SerialConnection.SerialDevices;

                if (SerialDevicesComboBox.SelectedItem is string)
                {
                    // The selected item is of type string, no need to assign it to a variable
                }
                else
                {
                    Plugin.Settings.SelectedSerialDevice = "None";
                    SerialDevicesComboBox.SelectedItem = "None";
                    if (prevDevice != null && prevDevice != "None")
                    {
                        // Set PrevDevice as a shared value so that you can later attempt a reconnect to the same COM port
                        prevDevicePermanent = prevDevice;
                    }
                    prevDeviceStatePermanent = prevDeviceState;
                }
            }
            if (e.PropertyName == nameof(SerialConnection.SelectedBaudRate))
            {
                if (BaudRateComboBox.SelectedItem is string _selectedBaudRate)
                {
                    Plugin.Settings.SelectedBaudRate = _selectedBaudRate;
                    SimHub.Logging.Current.Info($"Updated Plugin.Settings.SelectedBaudRate with {_selectedBaudRate}");
                    SerialConnection.SelectedBaudRate = Plugin.Settings.SelectedBaudRate;
                }
            }
            SimHub.Logging.Current.Info("END -> SerialConnection_PropertyChanged");
        }

        private void SettingsControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Load events from SettingsControl class
            ConnectCheckBox.Checked += Connect_Checked;
            ConnectCheckBox.Unchecked += Connect_Unchecked;
            BaudRateComboBox.SelectionChanged += ComboBox_SelectionChanged;

            SimHub.Logging.Current.Info("BEGIN -> SettingsControl_Loaded");
            if (SerialConnection != null)
            {
                if (SerialConnection.IsConnected)
                {
                    SimHub.Logging.Current.Info("Already connected!");
                }
                else
                {
                    AutoDetectDevice(Plugin.Settings);
                }
            }
            SimHub.Logging.Current.Info("END -> SettingsControl_Loaded");
        }
        private void SettingsControl_Unloaded(object sender, RoutedEventArgs e)
        {
            SimHub.Logging.Current.Info("BEGIN -> SettingsControl_UnLoaded");

            // UnLoad events from SettingsControl class
            ConnectCheckBox.Checked -= Connect_Checked;
            ConnectCheckBox.Unchecked -= Connect_Unchecked;
            BaudRateComboBox.SelectionChanged -= ComboBox_SelectionChanged;

            SimHub.Logging.Current.Info("END -> SettingsControl_UnLoaded");
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            await Task.Delay(1);
            string portName = Plugin.Settings.SelectedSerialDevice;
            SimHub.Logging.Current.Info($"Currently selected port is {portName}");

            if (sender == AutodetectButton)
            {
                if (AlreadyConnectedError())
                    return;

                AutoDetectDevice(Plugin.Settings);
            }

            else if (sender == ToggleButton)
            {
                // This is just to send the string to change the device state
                // await ChangeDeviceState(Main.Constants.HandShakeSnd, portName);

                Plugin.SerialConnection.ForcedDisconnect();
                Dispatcher.Invoke(() =>
                {
                    // Plugin.SerialConnection.ForcedDisconnect();
                });

            }
            else if (sender == ToggleButton2)
            {
                // This is just to send the string to change the device state
                // await ChangeDeviceState(Main.Constants.uniqueID, portName);

                Plugin.SerialConnection.LoadSerialDevices();
                Dispatcher.Invoke(() =>
                {
                    // Plugin.SerialConnection.LoadSerialDevices();
                });

            }
            else if (sender == Debugeador)
            {
                // This is just to send the string to Query device state
                // await ChangeDeviceState(Main.Constants.QueryStatus, portName);

                // Plugin.SerialConnection.Dispose();

                // This is just to send the string to change the device state
                string _data = DashBreeze.PrintObjectProperties(Plugin.Settings);
                OutputMsg(_data);
            }

        }

        // The following are the actual events we just created above in settings control
        // Event handler for SerialDevicesComboBox selection change
        private async void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender == SerialDevicesComboBox)
            {
                ConnectCheckBox.IsChecked = false;
                Plugin.Settings.ConnectToSerialDevice = false;
                await SerialConnection.Disconnect(withoutDelay: true);
            }
            if (sender == BaudRateComboBox)
            {
                ConnectCheckBox.IsChecked = false;
                await SerialConnection.Disconnect(withoutDelay: true);
            }
        }

        // Event handler for ConnectCheckBox checked
        private async void Connect_Checked(object sender, EventArgs e)
        {
            SimHub.Logging.Current.Info("BEGIN -> Connect_Checked from sender: " + sender);

            string currentSelection = SerialDevicesComboBox.SelectedItem as string;
            int BaudRate = int.Parse(Plugin.Settings.SelectedBaudRate);

            SimHub.Logging.Current.Info($"Read the BaudRate from Settings as {BaudRate}");

            try
            {
                if (currentSelection == "None")
                {
                    ConnectCheckBox.IsChecked = false;
                    string _data = "You need to select a port";
                    OutputMsg(_data);
                }
                else
                {
                    // Try Connecting using the same method from main...    
                    if (await TryConnect(currentSelection,BaudRate))
                    {
                        Plugin.Settings.ConnectToSerialDevice = true;
                        ConnectCheckBox.IsChecked = true;
                    }
                    else
                    {
                        Plugin.Settings.ConnectToSerialDevice = false;
                        ConnectCheckBox.IsChecked = false;
                        string _data = "Device already in use.\nRestart SimHub and try again";
                        OutputMsg(_data);
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                SimHub.Logging.Current.Error($"Access to the port '{currentSelection}' is denied. " + ex.Message);
            }
            catch (Exception)
            {
                SimHub.Logging.Current.Error($"Could not connect to {currentSelection}");
            }

            SimHub.Logging.Current.Info("END -> Connect_Checked");
        }

        // Event handler for ConnectCheckBox unchecked
        private async void Connect_Unchecked(object sender, EventArgs e)
        {
            // Disconnect from the selected serial device.
            Plugin.Settings.ConnectToSerialDevice = false;
            await SerialConnection.Disconnect(withoutDelay: true);
        }

        // Event handler for ConnectCheckBox checked
        private void CheckBox_Checked(object sender, EventArgs e)
        {
        }

        // Event handler for ConnectCheckBox unchecked
        private void CheckBox_Unchecked(object sender, EventArgs e)
        {
        }

        private void Slider_ValueChanged(object sender, EventArgs e)
        {

        }

        public async Task<bool> TryConnect(string currentSelection, int BaudRate)
        {
            SimHub.Logging.Current.Info("BEGIN -> TryConnect");
            bool ConnectToDevice = Plugin.Settings.ConnectToSerialDevice;
            string selectedPort = currentSelection ?? Plugin.Settings.SelectedSerialDevice;

            // Fucking Comboboxes los odio
            int SelectedBaudRate;
            string _SelectedBaudRate = Plugin.Settings.SelectedBaudRate;

            if (BaudRate == 0)
                SelectedBaudRate = int.Parse(_SelectedBaudRate);
            else
                SelectedBaudRate = BaudRate;

            SimHub.Logging.Current.Info($"Autoconnect is {ConnectToDevice} on port {selectedPort} using the following BaudRate {SelectedBaudRate}");

            if (currentSelection != null) {
                if (!string.IsNullOrEmpty(currentSelection) && currentSelection != "None")
                {
                    selectedPort = currentSelection;
                    ConnectToDevice = true;
                }
            }
            bool isChecked = false;
                try
                {
                    if (ConnectToDevice && !string.IsNullOrEmpty(selectedPort) && selectedPort != "None") {
                        SimHub.Logging.Current.Info($"Will run TryConnect() now and connect at {SelectedBaudRate}");

                        // Disconnect and dispose of the old connection if it exists.
                        if (SerialConnection == null)
                        {
                            SerialConnection = new SerialConnection();
                        }

                        if(Plugin.SerialConnection == null)
                            Plugin.SerialConnection = SerialConnection;

                        if (SerialConnection.IsConnected)
                        {
                            SimHub.Logging.Current.Info("And it was a success (was already connected)");
                            SerialConnection.ChangeDeviceStateAsync();
                            SimHub.Logging.Current.Info("Our device should be ready to receive data...");
                            isChecked = true;
                            int CurrentbaudRate = SerialConnection.GetBaudRate();
                            SimHub.Logging.Current.Info($"Connected at {CurrentbaudRate} baud rate");
                        }
                        else
                        {
                        // Now connect to the new port
                            await SerialConnection.Connect(selectedPort, SelectedBaudRate, ResetCon: true);
                            SimHub.Logging.Current.Info("Will try to connect to: " + selectedPort);
                            if(SerialConnection.IsConnected) {
                                SimHub.Logging.Current.Info("And it was a success (Created a new connection)");
                                SerialConnection.ChangeDeviceStateAsync();
                                SimHub.Logging.Current.Info("Our device should be ready to receive data...");
                                isChecked = true;
                                int CurrentbaudRate = SerialConnection.GetBaudRate();
                                SimHub.Logging.Current.Info($"Connected at {SelectedBaudRate} baud rate");
                                InitFans();
                        }
                            else {
                                SimHub.Logging.Current.Info("But failed...");
                                // Display a modal dialog with a message
                                Plugin.Settings.ConnectToSerialDevice = false;
                                Plugin.Settings.SelectedSerialDevice = "None";
                                ConnectCheckBox.IsChecked = false;
                                SimHub.Logging.Current.Info("Not Connected");
                                isChecked = false;
                                InitFans();
                        }
                        }
                    }
                    else { 
                        SimHub.Logging.Current.Info($"Did NOT run TryConnect(), and value for ConnectToDevice is {ConnectToDevice} and port is {selectedPort} and speed was {SelectedBaudRate}");
                }
                }
                catch (UnauthorizedAccessException ex)
                {
                    SimHub.Logging.Current.Error($"Access to the port '{selectedPort}' is denied. " + ex.Message);

                    Plugin.Settings.ConnectToSerialDevice = false;
                    Plugin.Settings.SelectedSerialDevice = "None";
                    ConnectCheckBox.IsChecked = false;
                    isChecked = false;
                }
                catch (Exception ex)
                {
                    // Handle the exception
                    // Display a message, log an error, or perform any necessary actions
                    SimHub.Logging.Current.Error("An error occurred during serial connection: " + ex.Message);

                    Plugin.Settings.ConnectToSerialDevice = false;
                    Plugin.Settings.SelectedSerialDevice = "None";
                    ConnectCheckBox.IsChecked = false;
                    isChecked = false;
                }
            SimHub.Logging.Current.Info("END -> TryConnect");
            return isChecked;
        }

        public void InitFans()
        {
            Thread.Sleep(1000);

            // Initialize the Fans to 0% Duty cycle
            byte[] serialData = new byte[] { 10, 10 };

            SerialConnection.SerialPort.Write(serialData, 0, serialData.Length);
        }

        public void HandleDataReceived(string data)
        {
            SimHub.Logging.Current.Info($"Received: {data.Trim()}");

            // Process the received data, e.g., check if it meets your criteria
            if (data.Trim() == DashBreeze.Constants.uniqueIDresponse)
            {
                SimHub.Logging.Current.Info($"Received ACK! We are ready to send via serial");
                // Set SerialOK 
                DashBreeze.SerialOK = true;
            }
            Application.Current.Dispatcher.Invoke(() => { OutputMsg(data); });
        }
        private async void HandleReadySerialPortFound(SerialConnection readySerialConnection)
        {
            await semaphore.WaitAsync();
            try
            {
                if (readySerialConnection.SerialPort != null)
                {
                    string _data = "Found DashBreeze on " + readySerialConnection.SerialPort.PortName;
                    // Perform necessary actions with the ready serial port
                    Application.Current.Dispatcher.Invoke(() => {
                        OutputMsg(_data);
                        SerialDevicesComboBox.SelectedItem = readySerialConnection.SerialPort.PortName;
                    }); 
                }
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error($"Error handling ready port: {ex.Message}");
            }
            finally
            {
                // Always release the semaphore.
                semaphore.Release();
            }

            // If we successfully processed the port, disconnect and inform the user.
            if (readySerialConnection.SerialPort != null && SerialConnection.IsConnected)
            {
                try
                {
                    await readySerialConnection.Disconnect();
                    OutputMsg("Closed port " + readySerialConnection.SerialPort.PortName);
                    SimHub.Logging.Current.Info("Closed port " + readySerialConnection.SerialPort.PortName);
                }
                catch (Exception ex)
                {
                    SimHub.Logging.Current.Error($"Error disconnecting port: {ex.Message}");
                }
            }
        }
        public void OutputMsg(string data)
        {
            DebugOutputTextBox.AppendText(data);
            DebugOutputTextBox.AppendText(Environment.NewLine); // Optional: Add a new line after each new data
            DebugOutputTextBox.ScrollToEnd(); // Optional: Scroll to the end of the text box
        }
        private bool AlreadyConnectedError()
        {
            bool isError = false;
            if (ConnectCheckBox.IsChecked == true)
            {
                isError = true;
                string _data = "A Device is already connected!\nDisconnect it, reset your device and try again\n";
                OutputMsg(_data);
            }
            return isError;
        }
        public async void AutoDetectDevice(MainSettings Settings)
        {
            // Check all ports syncronously and fast
            DeviceAutoDetect autoDetect = new DeviceAutoDetect();

            try
            {
                AutodetectButton.IsEnabled = false;
                DeviceAutoDetect.ReadySerialPortFound += HandleReadySerialPortFound;
                await autoDetect.CheckSerialPort(Settings);
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error($"Process was terminated: {ex.Message}");
            }
            finally
            {
                AutodetectButton.IsEnabled = true;
                DeviceAutoDetect.ReadySerialPortFound -= HandleReadySerialPortFound;
            }
        }
    }
}