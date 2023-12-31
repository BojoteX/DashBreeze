﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Bojote.DashBreeze
{
    public class DeviceAutoDetect
    {
        public static event Action<SerialConnection> ReadySerialPortFound;
        readonly SettingsControl settingsControl = new SettingsControl();

        public async Task CheckSerialPort(MainSettings Settings)
        {
            int BaudRate = int.Parse(Settings.SelectedBaudRate);

            int timeoutMilliseconds = 1000;  // 1 second timeout
            string[] actualPortNames = SerialPort.GetPortNames();

            var tasks = new List<Task>();

            foreach (var portName in actualPortNames)
            {
                tasks.Add(Task.Run(async () =>
                {
                    using (SerialConnection SC = new SerialConnection())
                    {
                        try
                        {
                            if (SC == null)
                                return;

                            await SC.Connect(portName, BaudRate, ResetCon: true);

                            if (!SC.IsConnected)
                                return;

                            SimHub.Logging.Current.Info($"Opened {portName}");

                            string HandShakeString = DashBreeze.Constants.HandShakeRcv;

                            byte[] command255 = { 255, 254 };
                            SC.SerialPort.Write(command255, 0, command255.Length);

                            SimHub.Logging.Current.Info($"Sent the Byte codes 255, 254, we should get {HandShakeString} if device is present");

                            StringBuilder response = new StringBuilder();
                            byte[] buffer = new byte[1024];
                            int bytesRead;
                            bool handshakeReceived = false;

                            using (var cts = new CancellationTokenSource(timeoutMilliseconds))
                            {
                                try
                                {
                                    while (!handshakeReceived)
                                    {
                                        if (SC.SerialPort.BytesToRead > 0)
                                        {
                                            bytesRead = SC.SerialPort.Read(buffer, 0, buffer.Length);
                                            response.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

                                            if (response.ToString().Contains(HandShakeString))
                                                handshakeReceived = true;

                                            SimHub.Logging.Current.Info($"I'm reading data now");
                                        }

                                        cts.Token.ThrowIfCancellationRequested();
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                    // Cancellation request has been made, handle it
                                    SimHub.Logging.Current.Info($"Operation on port {portName} was cancelled.");
                                }
                            }

                            if (handshakeReceived)
                            {
                                ReadySerialPortFound?.Invoke(SC);
                            }
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            SimHub.Logging.Current.Error($"Access to the port '{portName}' is denied. {ex.Message}");
                            // Handle the exception here...
                        }
                        catch (IOException ex)
                        {
                            SimHub.Logging.Current.Error($"Error opening or writing to {portName}: {ex.Message}");
                        }

                        catch (InvalidOperationException ex)
                        {
                            SimHub.Logging.Current.Error($"Error opening or writing to {portName}: {ex.Message}");
                        }

                        catch (ArgumentException ex)
                        {
                            SimHub.Logging.Current.Error($"Error opening or writing to {portName}: {ex.Message}");
                        }

                        finally
                        {
                            SimHub.Logging.Current.Info($"Closed {portName}");
                            await Application.Current.Dispatcher.InvokeAsync(() => settingsControl.OutputMsg($"Closed {portName}"));
                        }
                    }
                }));
            }
            await Task.WhenAll(tasks);  // Wait for all tasks to complete
        }
    }
}