using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace QuickBlueToothLE
{
    class Program
    {
        static DeviceInformation device = null;

        public static string HEART_RATE_SERVICE_ID = "180D";
        public static string FITNESSMACHINE = "1826";

        static async Task Main(string[] args)
        {
            // Query for extra properties you want returned
            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected" };

            DeviceWatcher deviceWatcher =
                        DeviceInformation.CreateWatcher(
                                BluetoothLEDevice.GetDeviceSelectorFromPairingState(false),
                                requestedProperties,
                                DeviceInformationKind.AssociationEndpoint);

            // Register event handlers before starting the watcher.
            // Added, Updated and Removed are required to get all nearby devices
            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Updated += DeviceWatcher_Updated;
            deviceWatcher.Removed += DeviceWatcher_Removed;

            // EnumerationCompleted and Stopped are optional to implement.
            deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
            deviceWatcher.Stopped += DeviceWatcher_Stopped;

            // Start the watcher.
            deviceWatcher.Start();
            while (true)
            {
                if (device == null)
                {
                    Thread.Sleep(200);
                }
                else
                {
                    Console.WriteLine("Press Any to pair with device");
                    Console.ReadKey();
                    BluetoothLEDevice bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(device.Id);
                    Console.WriteLine("Attempting to pair with device");
                    GattDeviceServicesResult result = await bluetoothLeDevice.GetGattServicesAsync();

                    if (result.Status == GattCommunicationStatus.Success)
                    {
                        Console.WriteLine("Pairing succeeded");
                        var services = result.Services;
                        foreach (var service in services)
                        {
                            Console.WriteLine($"{service.Uuid}");
                            Console.WriteLine("---------------");
                            GattCharacteristicsResult charactiristicResult = await service.GetCharacteristicsAsync();

                            if (service.Uuid.ToString("N").Substring(4, 4).ToUpper() == FITNESSMACHINE)
                            {

                                if (charactiristicResult.Status == GattCommunicationStatus.Success)
                                {
                                    var characteristics = charactiristicResult.Characteristics;
                                    foreach (var characteristic in characteristics)
                                    {
                                        var characteristicKey = characteristic.Uuid.ToString("N").Substring(4, 4).ToUpper();
                                        string characteristicName = "Unknown";
                                        if (GattServiceUUID.Lookup.ContainsKey(characteristicKey))
                                            characteristicName = GattServiceUUID.Lookup[characteristicKey].Item2;


                                        Console.WriteLine($"\t [{characteristicName}] - [{characteristic.Uuid}]");

                                        GattCharacteristicProperties properties = characteristic.CharacteristicProperties;

                                        if (properties.HasFlag(GattCharacteristicProperties.Read))
                                        {
                                            if(characteristicName == "Supported Resistance Level Range")
                                            {
                                                var supportedResitanceLevelRange = await characteristic.ReadValueAsync();
                                                if (supportedResitanceLevelRange.Status == GattCommunicationStatus.Success)
                                                {
                                                    var reader = DataReader.FromBuffer(supportedResitanceLevelRange.Value);
                                                    byte[] input = new byte[reader.UnconsumedBufferLength];
                                                    reader.ReadBytes(input);
                                                    // Utilize the data as needed
                                                }
                                            }

                                            Console.WriteLine("\t\t [{characteristic.Uuid}] Read property found");

                                        }
                                        else if (properties.HasFlag(GattCharacteristicProperties.Notify))
                                        {
                                            Console.WriteLine("\t\t [{characteristic.Uuid}] Notify property found");
                                            GattCommunicationStatus status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                                            GattClientCharacteristicConfigurationDescriptorValue.Notify);
                                            if (status == GattCommunicationStatus.Success)
                                            {
                                                characteristic.ValueChanged += Characteristic_ValueChanged;
                                                // Server has been informed of clients interest.
                                            }
                                        }
                                        else if (properties.HasFlag(GattCharacteristicProperties.Write))
                                        {
                                            Console.WriteLine($"\t\t [{characteristic.Uuid}] Write property found");
                                            var writer = new DataWriter();
                                            // WriteByte used for simplicity. Other common functions - WriteInt16 and WriteSingle
                                            writer.WriteByte(0x00);

                                            GattCommunicationStatus resultWrite = await characteristic.WriteValueAsync(writer.DetachBuffer());
                                            if (resultWrite == GattCommunicationStatus.Success)
                                            {
                                                Console.WriteLine("Control request success");
                                            }

                                            writer = new DataWriter();
                                            // WriteByte used for simplicity. Other common functions - WriteInt16 and WriteSingle
                                            writer.WriteByte(0x01);

                                            resultWrite = await characteristic.WriteValueAsync(writer.DetachBuffer());
                                            if (resultWrite == GattCommunicationStatus.Success)
                                            {
                                                Console.WriteLine("Reset request success");
                                            }

                                            writer = new DataWriter();
                                            // WriteByte used for simplicity. Other common functions - WriteInt16 and WriteSingle
                                            writer.WriteByte(0x04);
                                            writer.WriteByte(0);

                                            resultWrite = await characteristic.WriteValueAsync(writer.DetachBuffer());
                                            if (resultWrite == GattCommunicationStatus.Success)
                                            {
                                                Console.WriteLine("Resistance request success");
                                            }




                                        }
                                        else if (properties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse))
                                        {
                                            Console.WriteLine($"\t\t [{characteristic.Uuid}] WriteWithoutResponse property found");
                                        }


                                    }
                                }
                            }

                        }
                    }

                    Console.WriteLine("Press Any Key to Exit application");
                    Console.ReadKey();
                    break;
                }
            }
            deviceWatcher.Stop();
        }

        private static void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            /*
            var reader = DataReader.FromBuffer(args.CharacteristicValue);
            var flags = reader.ReadByte();
            var value = reader.ReadByte();
            Console.WriteLine($"{flags} - {value}");
            */
        }

        private static void DeviceWatcher_Stopped(DeviceWatcher sender, object args)
        {
            //throw new NotImplementedException();
        }

        private static void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            //throw new NotImplementedException();
        }

        private static void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            //throw new NotImplementedException();
        }

        private static void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            //throw new NotImplementedException();
        }

        private static void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {

            Console.WriteLine(args.Name);
            if (args.Name == "TICKR 2966")
                device = args;
            else if (args.Name == "KICKR CORE 802B")
                device = args;
            //throw new NotImplementedException();
        }
    }
}
