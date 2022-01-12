using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using static Fingerprint.NET.FingerprintConstants;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Bmp;
using System.Collections;

namespace Fingerprint.NET;

/// <summary>
/// Manages ZhianTec fingerprint sensors
/// </summary>
public class Fingerprint : IDisposable
{
    private static SerialPort _serialPort;
    private uint _address;
    private uint _password;
    /// <summary>
    /// Constructor
    /// </summary>                   
    /// <param name="port">The port to use</param>
    /// <param name="baudRate">The baud rate to use. Must be a multiple of 9600!</param>
    /// <param name="address">The sensor address</param>
    /// <param name="password">The sensor password</param>
    public Fingerprint(string port = "/dev/ttyUSB0", int baudRate = 57600, uint address = 0xFFFFFFFF, uint password = 0x00000000)
    {
        //string[] ports = SerialPort.GetPortNames();
        if (baudRate < 9600 || baudRate > 115200 || baudRate % 9600 != 0)
        {
            throw new Exception("The given baud rate is invalid!");
        }

        if (address < 0x00000000 || address > 0xFFFFFFFF)
        {
            throw new Exception("The given address is invalid!");
        }

        if (password < 0x00000000 || password > 0xFFFFFFFF)
        {
            throw new Exception("The given password is invalid!");
        }

        _address = address;
        _password = password;

        // Initialize Serial connection
        _serialPort = new SerialPort(port, baudRate, Parity.None, 8);
        if (_serialPort.IsOpen)
            _serialPort.Close();

        _serialPort.Open();
    }

    public void Dispose()
    {
        _serialPort?.Dispose();
    }

    /// <summary>
    /// Receives a packet from the sensor
    /// </summary>
    /// <returns></returns>
    public Tuple<byte, List<byte>> ReadPacket()
    {
        var receivedPacketData = new List<byte>();
        int i = 0;

        while (true)
        {
            // Read one byte
            int fragement = (byte)_serialPort.ReadByte();
            if (fragement != -1)
            {
                // Insert byte if packet seems valid
                receivedPacketData.Add((byte)fragement);
                i++;
            }
            else
            {
                continue;
            }

            // Packet could be complete (the minimal packet size is 12 bytes)
            if (i >= 12)
            {
                // Check the packet header
                if (receivedPacketData[0] != RightShift(FingerprintConstants.StartCode, 8)
                    || receivedPacketData[1] != RightShift(FingerprintConstants.StartCode, 0))
                {
                    throw new Exception("The received packet do not begin with a valid header!");
                }

                // Calculate packet payload length (combine the 2 length bytes)
                int packetPayloadLength = LeftShift(receivedPacketData[7], 8);
                packetPayloadLength = packetPayloadLength | LeftShift(receivedPacketData[8], 0);

                // Check if the packet is still fully received
                // Condition index counter < packet payload length + packet frame
                if (i < packetPayloadLength + 9)
                    continue;

                // At this point the packet should be fully received

                byte packetType = receivedPacketData[6];

                // Calculate checksum
                // checksum = packet type (1 byte) + packet length (2 bytes) + packet payload (n bytes)

                int packetChecksum = packetType + receivedPacketData[7] + receivedPacketData[8];

                var packetPayload = new List<byte>();

                // Collect package payload (ignore the last 2 checksum bytes)
                for (int j = 9; j < 9 + packetPayloadLength - 2; j++)
                {
                    packetPayload.Add(receivedPacketData[j]);
                    packetChecksum += receivedPacketData[j];
                }

                // Calculate full checksum of the 2 separate checksum bytes
                int receivedChecksum = LeftShift(receivedPacketData[i - 2], 8);
                receivedChecksum = receivedChecksum | LeftShift(receivedPacketData[i - 1], 0);


                if (receivedChecksum != packetChecksum)
                    throw new Exception("The received packet is corrupted (the checksum is wrong)!");

                return Tuple.Create(packetType, packetPayload);
            }
        }
    }

    /// <summary>
    /// Sets the password of the sensor
    /// </summary>
    /// <param name="newPassword"></param>
    /// <returns></returns>
    public bool SetPassword(uint newPassword)
    {
        var packetPayload = new List<byte>
            {
                FingerprintConstants.SetPassword,
                (byte) RightShift(newPassword, 24),
                (byte) RightShift(newPassword, 16),
                (byte) RightShift(newPassword, 8),
                (byte) RightShift(newPassword, 0)
            };

        WritePacket(FingerprintConstants.CommandPacket, packetPayload.ToArray());
        var receivedPacket = ReadPacket();

        byte receivedPacketType = receivedPacket.Item1;
        List<byte> receivedPacketPayload = receivedPacket.Item2;

        if (receivedPacketType != FingerprintConstants.AckPacket)
            throw new Exception("The received packet is no ack packet!");

        // DEBUG Password set was successful
        if (receivedPacketPayload[0] == FingerprintConstants.Ok)
        {
            _password = newPassword;
            return true;
        }

        else if (receivedPacketPayload[0] == FingerprintConstants.ErrorCommunication)
            throw new Exception("Communication error");

        else
            throw new Exception("Unknown error " + receivedPacketPayload[0].ToString("X"));
    }

    /// <summary>
    /// Verifies password of the sensor.
    /// </summary>
    /// <returns></returns>
    public bool VerifyPassword()
    {
        var packetPayload = new List<byte>
            {
                FingerprintConstants.VerifyPassword,
                (byte) RightShift(_password, 24),
                (byte) RightShift(_password, 16),
                (byte) RightShift(_password, 8),
                (byte) RightShift(_password, 0),
            };
        WritePacket(FingerprintConstants.CommandPacket, packetPayload.ToArray());
        var receivedPacket = ReadPacket();

        byte receivedPacketType = receivedPacket.Item1;
        List<byte> receivedPacketPayload = receivedPacket.Item2;

        if (receivedPacketType != FingerprintConstants.AckPacket)
            throw new Exception("The received packet is no ack packet!");

        // DEBUG Sensor password is correct
        if (receivedPacketPayload[0] == FingerprintConstants.Ok)
            return true;

        else if (receivedPacketPayload[0] == FingerprintConstants.ErrorCommunication)
            throw new Exception("Communication error");

        else if (receivedPacketPayload[0] == FingerprintConstants.AddrCode)
            throw new Exception("The address is wrong");

        // DEBUG Sensor password is wrong
        else if (receivedPacketPayload[0] == FingerprintConstants.ErrorWrongPassword)
            return false;

        else
            throw new Exception("Unknown error " + receivedPacketPayload[0].ToString("X"));
    }

    /// <summary>
    /// Write a packet to the sensor
    /// </summary>
    /// <param name="packetType"></param>
    /// <param name="packetPayload"></param>
    public void WritePacket(int packetType, byte[] packetPayload)
    {
        // Write header (one byte at once)
        _serialPort.Write(new byte[] { Convert.ToByte(RightShift(FingerprintConstants.StartCode, 8)) }, 0, 1);
        _serialPort.Write(new byte[] { Convert.ToByte(RightShift(FingerprintConstants.StartCode, 0)) }, 0, 1);

        _serialPort.Write(new byte[] { Convert.ToByte(RightShift(_address, 24)) }, 0, 1);
        _serialPort.Write(new byte[] { Convert.ToByte(RightShift(_address, 16)) }, 0, 1);
        _serialPort.Write(new byte[] { Convert.ToByte(RightShift(_address, 8)) }, 0, 1);
        _serialPort.Write(new byte[] { Convert.ToByte(RightShift(_address, 0)) }, 0, 1);

        _serialPort.Write(new byte[] { Convert.ToByte(packetType) }, 0, 1);

        // The packet length = package payload (n bytes) + checksum (2 bytes)
        int packetLength = packetPayload.Length + 2;

        _serialPort.Write(new byte[] { Convert.ToByte(RightShift(packetLength, 8)) }, 0, 1);
        _serialPort.Write(new byte[] { Convert.ToByte(RightShift(packetLength, 0)) }, 0, 1);

        // The packet checksum = packet type (1 byte) + packet length (2 bytes) + payload (n bytes)
        int packetChecksum = packetType + RightShift(packetLength, 8) + RightShift(packetLength, 0);

        // Write payload
        for (int i = 0; i < packetPayload.Length; i++)
        {
            _serialPort.Write(new byte[] { Convert.ToByte(packetPayload[i]) }, 0, 1);
            packetChecksum += packetPayload[i];
        }

        // Write checksum (2 bytes)
        _serialPort.Write(new byte[] { Convert.ToByte(RightShift(packetChecksum, 8)) }, 0, 1);
        _serialPort.Write(new byte[] { Convert.ToByte(RightShift(packetChecksum, 0)) }, 0, 1);
    }

    /// <summary>
    /// Sets the sensor address
    /// </summary>
    /// <param name="newAddress"></param>
    /// <returns></returns>
    public bool SetAddress(uint newAddress)
    {

        // Validate the address (maximum 4 bytes)
        if (newAddress < 0x00000000 || newAddress > 0xFFFFFFFF)
            throw new Exception("The given address is invalid!");

        var packetPayload = new List<byte>
            {
                FingerprintConstants.SetAddress,
                (byte) RightShift(newAddress, 24),
                (byte) RightShift(newAddress, 16),
                (byte) RightShift(newAddress, 8),
                (byte) RightShift(newAddress, 0)
            };

        WritePacket(FingerprintConstants.CommandPacket, packetPayload.ToArray());
        var receivedPacket = ReadPacket();

        var receivedPacketType = receivedPacket.Item1;
        var receivedPacketPayload = receivedPacket.Item2;

        if (receivedPacketType != FingerprintConstants.AckPacket)
            throw new Exception("The received packet is no ack packet!");

        // DEBUG Address set was successful
        if (receivedPacketPayload[0] == FingerprintConstants.Ok)
        {
            _address = newAddress;
            return true;
        }

        else if (receivedPacketPayload[0] == FingerprintConstants.ErrorCommunication)
            throw new Exception("Communication error");

        else
            throw new Exception("Unknown error " + receivedPacketPayload[0].ToString("X"));
    }

    /// <summary>
    /// Set a system parameter of the sensor
    /// </summary>
    /// <param name="parameterNumber"></param>
    /// <param name="parameterValue"></param>
    /// <returns></returns>
    bool SetSystemParameter(int parameterNumber, int parameterValue)
    {
        // Validate the baud rate parameter
        if (parameterNumber == SetSystemParameterBaudRate)
        {
            if (parameterValue < 1 || parameterValue > 12)
                throw new Exception("The given baud rate parameter is invalid!");
        }

        // Validate the security level parameter
        else if (parameterNumber == SetSystemParameterSecurityLevel)
        {
            if (parameterValue < 1 || parameterValue > 5)
                throw new Exception("The given security level parameter is invalid!");
        }

        // Validate the package length parameter
        else if (parameterNumber == SetSystemParameterPackageSize)
        {
            if (parameterValue < 0 || parameterValue > 3)
                throw new Exception("The given package length parameter is invalid!");
        }

        // The parameter number is not valid
        else
            throw new Exception("The given parameter number is invalid!");

        var packetPayload = new List<byte>
            {
                FingerprintConstants.SetSystemParameter,
                (byte) parameterNumber,
                (byte) parameterValue,
            };


        WritePacket(CommandPacket, packetPayload.ToArray());
        var receivedPacket = ReadPacket();

        var receivedPacketType = receivedPacket.Item1;
        var receivedPacketPayload = receivedPacket.Item2;

        if (receivedPacketType != AckPacket)
            throw new Exception("The received packet is no ack packet!");

        // DEBUG Parameter set was successful
        if (receivedPacketPayload[0] == Ok)
            return true;

        else if (receivedPacketPayload[0] == ErrorCommunication)
            throw new Exception("Communication error");

        else if (receivedPacketPayload[0] == ErrorInvalidRegister)
            throw new Exception("Invalid register number");

        else
            throw new Exception("Unknown error " + receivedPacketPayload[0].ToString("X"));
    }

    /// <summary>
    /// Sets the baud rate
    /// </summary>
    /// <param name="baudRate"></param>
    public void SetBaudRate(int baudRate)
    {
        if (baudRate % 9600 != 0)
            throw new Exception("Invalid baud rate");
        SetSystemParameter(SetSystemParameterBaudRate, baudRate);
    }

    /// <summary>
    /// Sets the security level of the sensor
    /// </summary>
    /// <param name="securityLevel"></param>
    public void SetSecurityLevel(int securityLevel)
    {
        SetSystemParameter(SetSystemParameterSecurityLevel, securityLevel);
    }

    /// <summary>
    /// Sets the maximum packet size of sensor
    /// 32, 64, 128 and 256 are supported
    /// </summary>
    /// <param name="packetSize"></param>
    public void SetMaxPacketSize(int packetSize)
    {
        int packetMaxSizeType = 0;
        try
        {
            var packetSizes = new Dictionary<int, int> { { 32, 0 }, { 64, 1 }, { 128, 2 }, { 256, 3 } };
            packetMaxSizeType = packetSizes[packetSize];
        }
        catch
        {
            throw new Exception("Invalid packet size");
        }

        SetSystemParameter(SetSystemParameterPackageSize, packetMaxSizeType);

    }

    /// <summary>
    /// Gets all available system information of the sensor
    /// </summary>
    /// <returns></returns>
    FingerprintSystemParameters GetSystemParameters()
    {
        var packetPayload = new List<byte> { FingerprintConstants.GetSystemParameters };

        WritePacket(CommandPacket, packetPayload.ToArray());
        var receivedPacket = ReadPacket();

        var receivedPacketType = receivedPacket.Item1;
        var receivedPacketPayload = receivedPacket.Item2;

        if (receivedPacketType != AckPacket)
            throw new Exception("The received packet is no ack packet!");

        // DEBUG Read successfully
        if (receivedPacketPayload[0] == Ok)
        {
            var fingerprintParams = new FingerprintSystemParameters();
            fingerprintParams.StatusRegister = LeftShift(receivedPacketPayload[1], 8) | LeftShift(receivedPacketPayload[2], 0);
            fingerprintParams.SystemId = LeftShift(receivedPacketPayload[3], 8) | LeftShift(receivedPacketPayload[4], 0);
            fingerprintParams.StorageCapacity = LeftShift(receivedPacketPayload[5], 8) | LeftShift(receivedPacketPayload[6], 0);
            fingerprintParams.SecurityLevel = LeftShift(receivedPacketPayload[7], 8) | LeftShift(receivedPacketPayload[8], 0);
            fingerprintParams.SensorAddress = (uint) ((receivedPacketPayload[9] << 8 | receivedPacketPayload[10]) << 8 | receivedPacketPayload[11])
                << 8 | receivedPacketPayload[12];
            fingerprintParams.PacketLength = LeftShift(receivedPacketPayload[13], 8) | LeftShift(receivedPacketPayload[14], 0);
            fingerprintParams.BaudRate = LeftShift(receivedPacketPayload[15], 8) | LeftShift(receivedPacketPayload[16], 0);

            return fingerprintParams;
        }

        else if (receivedPacketPayload[0] == ErrorCommunication)
            throw new Exception("Communication error");

        else
            throw new Exception("Unknown error " + receivedPacketPayload[0].ToString("X"));
    }

    /// <summary>
    /// Gets the sensor storage capacity
    /// </summary>
    /// <returns></returns>
    public int GetStorageCapacity() => GetSystemParameters().StorageCapacity;

    /// <summary>
    /// Gets the security level of the sensor
    /// </summary>
    /// <returns></returns>
    public int GetSecurityLevel() => GetSystemParameters().SecurityLevel;

    /// <summary>
    /// Gets the maximum allowed size of a single packet
    /// </summary>
    /// <returns></returns>
    public int GetMaxPacketSize()
    {
        int packetMaxSizeType = GetSystemParameters().PacketLength;

        try
        {
            var packetSizes = new int[] { 32, 64, 128, 256 };
            int packetSize = packetSizes[packetMaxSizeType];
            return packetSize;
        }
        catch
        {
            throw new Exception("Invalid packet size");
        }
    }

    /// <summary>
    /// Gets the baud rate
    /// </summary>
    /// <returns></returns>
    public int GetBaudRate() => GetSystemParameters().BaudRate * 9600;

    /// <summary>
    /// Get the bit at a particular position
    /// </summary>
    /// <param name="n"></param>
    /// <param name="x"></param>
    /// <returns></returns>
    int GetBitAtPoition(int n, int x)
    {
        int twoP = 1 << x;

        // Binary AND composition (on both positions must be a 1)
        // This can only happen at position p
        int result = n & twoP;
        return Convert.ToInt32(result > 0);
    }

    /// <summary>
    /// Left Shifting for int
    /// </summary>
    /// <param name="n"></param>
    /// <param name="x"></param>
    /// <returns></returns>
    int LeftShift(int n, int x) => n << x;
    
    /// <summary>
    /// Left Shifting for uint
    /// </summary>
    /// <param name="n"></param>
    /// <param name="x"></param>
    /// <returns></returns>
    uint LeftShift(uint n, int x) => n << x;

    /// <summary>
    /// Right Shifting for int
    /// </summary>
    /// <param name="n"></param>
    /// <param name="x"></param>
    /// <returns></returns>
    int RightShift(int n, int x) => n >> x & 0xFF;
    
    /// <summary>
    /// Right Shifting for uint
    /// </summary>
    /// <param name="n"></param>
    /// <param name="x"></param>
    /// <returns></returns>
    uint RightShift(uint n, int x) => n >> x & 0xFF;

    /// <summary>
    /// Gets a list of the template positions with usage indicator
    /// </summary>
    /// <param name="page">The page (value between 0 and 3)</param>
    /// <returns></returns>
    public List<int> GetTemplateIndex(int page)
    {
        if (page < 0 || page > 3)
            throw new Exception("The given index page is invalid!");

        var packetPayload = new List<byte>
            {
                TemplateIndex,
                (byte) page
            };

        WritePacket(CommandPacket, packetPayload.ToArray());
        var receivedPacket = ReadPacket();

        var receivedPacketType = receivedPacket.Item1;
        var receivedPacketPayload = receivedPacket.Item2;

        if (receivedPacketType != AckPacket)
            throw new Exception("The received packet is no ack packet!");

        // DEBUG Read index table successfully
        if (receivedPacketPayload[0] == Ok)
        {
            var templateIndex = new List<int>();
            // Contain the table page bytes (skip the first status byte)
            var pageElements = receivedPacketPayload.ToArray()[1..^0];

            foreach (var pageElement in pageElements)
            {
                // Test every bit (bit = template position is used indicator) of a table page element
                for (int p = 0; p < 7 + 1; p++)
                {
                    int positionIsUsed = Convert.ToInt32(GetBitAtPoition(pageElement, p) == 1);
                    templateIndex.Add(positionIsUsed);
                }
            }
            return templateIndex;
        }

        else if (receivedPacketPayload[0] == ErrorCommunication)
            throw new Exception("Communication error");

        else
            throw new Exception("Unknown error " + receivedPacketPayload[0].ToString("X"));
    }

    /// <summary>
    /// Gets the number of stored templates
    /// </summary>
    /// <returns></returns>
    public int GetTemplateCount()
    {
        var packetPayload = new List<byte> { TemplateCount };

        WritePacket(CommandPacket, packetPayload.ToArray());
        var receivedPacket = ReadPacket();

        var receivedPacketType = receivedPacket.Item1;
        var receivedPacketPayload = receivedPacket.Item2;

        if (receivedPacketType != AckPacket)
            throw new Exception("The received packet is no ack packet!");

        // DEBUG Read successfully
        if (receivedPacketPayload[0] == Ok)
        {
            int templateCount = LeftShift(receivedPacketPayload[1], 8);
            templateCount = templateCount | LeftShift(receivedPacketPayload[2], 0);
            return templateCount;
        }

        else if (receivedPacketPayload[0] == ErrorCommunication)
            throw new Exception("Communication error");

        else
            throw new Exception("Unknown error " + receivedPacketPayload[0].ToString("X"));
    }

    /// <summary>
    /// Reads the image of a finger and stores it in image buffer
    /// </summary>
    /// <returns></returns>
    public bool ReadImage()
    {
        var packetPayload = new List<byte> { FingerprintConstants.ReadImage };

        WritePacket(CommandPacket, packetPayload.ToArray());
        var receivedPacket = ReadPacket();

        var receivedPacketType = receivedPacket.Item1;
        var receivedPacketPayload = receivedPacket.Item2;

        if (receivedPacketType != AckPacket)
            throw new Exception("The received packet is no ack packet!");

        // DEBUG Image read successful
        if (receivedPacketPayload[0] == Ok)
            return true;

        else if (receivedPacketPayload[0] == ErrorCommunication)
            throw new Exception("Communication error");

        // DEBUG No finger found
        else if (receivedPacketPayload[0] == ErrorNoFinger)
            return false;

        else if (receivedPacketPayload[0] == ErrorReadImage)
            throw new Exception("Could not read image");

        else
            throw new Exception("Unknown error " + receivedPacketPayload[0].ToString("X"));
    }

    /// <summary>
    /// Downloads the image from image buffer
    /// </summary>
    /// <param name="imageDestinationPath">Path to store image</param>
    public Image GetFingerprintImage()
    {
        var packetPayload = new List<byte> { FingerprintConstants.DownloadImage };

        WritePacket(CommandPacket, packetPayload.ToArray());
        var receivedPacket = ReadPacket();

        var receivedPacketType = receivedPacket.Item1;
        var receivedPacketPayload = receivedPacket.Item2;

        if (receivedPacketType != AckPacket)
            throw new Exception("The received packet is no ack packet!");

        // DEBUG The sensor will sent follow-up packets
        if (receivedPacketPayload[0] == Ok) { }

        else if (receivedPacketPayload[0] == ErrorCommunication)
            throw new Exception("Communication error");

        else if (receivedPacketPayload[0] == ErrorDownloadImage)
            throw new Exception("Could not download image");

        else
            throw new Exception("Unknown error " + receivedPacketPayload[0].ToString("X"));

        var imageData = new List<byte>();

        // Get follow-up data packets until the last data packet is received
        while (receivedPacketType != EndDataPacket)
        {
            receivedPacket = ReadPacket();

            receivedPacketType = receivedPacket.Item1;
            receivedPacketPayload = receivedPacket.Item2;

            if (receivedPacketType != DataPacket && receivedPacketType != EndDataPacket)
                throw new Exception("The received packet is no data packet!");

            imageData.AddRange(receivedPacketPayload);
        }

        var bitArray = new BitArray(imageData.ToArray());
        var pixelList = new List<Bgra4444>();

        for (int i = 0;i < bitArray.Count; i+=4)
        {
            float x = bitArray.Get(i) ? 1 : 0;
            float y = bitArray.Get(i+1) ? 1 : 0;
            float z = bitArray.Get(i+2) ? 1 : 0;
            float w = bitArray.Get(i+3) ? 1 : 0;

            pixelList.Add(new Bgra4444(x,y,z,w));
        }

        var image = Image.LoadPixelData(pixelList.ToArray(), 256, 288);
        return image;

    }

    /// <summary>
    /// Converts the image in image buffer to characteristics and stores it in specified char buffer
    /// </summary>
    /// <param name="charBufferNumber">The char buffer</param>
    /// <returns></returns>
    public bool ConvertImage(int charBufferNumber = CharBuffer1)
    {
        var packetPayload = new List<byte>
            {
                FingerprintConstants.ConvertImage,
                (byte) charBufferNumber
            };

        WritePacket(CommandPacket, packetPayload.ToArray());
        var receivedPacket = ReadPacket();

        var receivedPacketType = receivedPacket.Item1;
        var receivedPacketPayload = receivedPacket.Item2;

        if (receivedPacketType != AckPacket)
            throw new Exception("The received packet is no ack packet!");

        // DEBUG Image converted
        if (receivedPacketPayload[0] == Ok)
            return true;

        else if (receivedPacketPayload[0] == ErrorCommunication)
            throw new Exception("Communication error");

        else if (receivedPacketPayload[0] == ErrorMessyImage)
            throw new Exception("The image is too messy");

        else if (receivedPacketPayload[0] == ErrorFewFeaturePoints)
            throw new Exception("The image contains too few feature points");

        else if (receivedPacketPayload[0] == ErrorInvalidImage)
            throw new Exception("The image is invalid");

        else
            throw new Exception("Unknown error " + receivedPacketPayload[0].ToString("X"));
    }

    /// <summary>
    /// Combines the characteristics which are stored in char buffer 1 and char buffer 2 into one template
    /// The created template will be stored again in char buffer 1 and char buffer 2 as the same
    /// </summary>
    /// <returns></returns>
    public bool CreateTemplate()
    {
        var packetPayload = new List<byte> { FingerprintConstants.CreateTemplate };

        WritePacket(CommandPacket, packetPayload.ToArray());
        var receivedPacket = ReadPacket();

        var receivedPacketType = receivedPacket.Item1;
        var receivedPacketPayload = receivedPacket.Item2;

        if (receivedPacketType != AckPacket)
            throw new Exception("The received packet is no ack packet!");

        // DEBUG Template created successful
        if (receivedPacketPayload[0] == Ok)
            return true;

        else if (receivedPacketPayload[0] == ErrorCommunication)
            throw new Exception("Communication error");

        // DEBUG The characteristics not matching
        else if (receivedPacketPayload[0] == ErrorCharacteristicsMismatch)
            return false;

        else
            throw new Exception("Unknown error " + receivedPacketPayload[0].ToString("X"));

    }

    /// <summary>
    /// Stores a template from the specified char buffer at the given position
    /// </summary>
    /// <param name="positionNumber"></param>
    /// <param name="charBufferNumber"></param>
    /// <returns></returns>
    public int StoreTemplate(int positionNumber = -1, int charBufferNumber = CharBuffer1)
    {
        // Find a free index
        if (positionNumber == -1)
        {
            for (int page = 0; page < 4; page++)
            {
                // Free index found?
                if (positionNumber >= 0)
                    break;
                var templateIndex = GetTemplateIndex(page);

                for (int i = 0; i < templateIndex.Count; i++)
                {
                    // Index not used?
                    if (Convert.ToBoolean(templateIndex[i]) == false)
                    {
                        positionNumber = (templateIndex.Count * page) + i;
                        break;
                    }
                }
            }
        }

        if (positionNumber < 0x0000 || positionNumber >= GetStorageCapacity())
            throw new Exception("The given position number is invalid!");

        if (charBufferNumber != CharBuffer1 && charBufferNumber != CharBuffer2)
            throw new Exception("The given char buffer number is invalid!");

        var packetPayload = new List<byte>
            {
                FingerprintConstants.StoreTemplate,
                (byte) charBufferNumber,
                (byte) RightShift(positionNumber, 8),
                (byte) RightShift(positionNumber, 0)
            };

        WritePacket(CommandPacket, packetPayload.ToArray());
        var receivedPacket = ReadPacket();

        var receivedPacketType = receivedPacket.Item1;
        var receivedPacketPayload = receivedPacket.Item2;

        if (receivedPacketType != AckPacket)
            throw new Exception("The received packet is no ack packet!");

        // DEBUG Template stored successful
        if (receivedPacketPayload[0] == Ok)
            return positionNumber;

        else if (receivedPacketPayload[0] == ErrorCommunication)
            throw new Exception("Communication error");

        else if (receivedPacketPayload[0] == ErrorInvalidPosition)
            throw new Exception("Could not store template in that position");

        else if (receivedPacketPayload[0] == ErrorFlash)
            throw new Exception("Error writing to flash");

        else
            throw new Exception("Unknown error " + receivedPacketPayload[0].ToString("X"));
    }

    /// <summary>
    /// Searches inside the database for the characteristics in char buffer
    /// </summary>
    /// <param name="charBufferNumber"></param>
    /// <param name="positionStart"></param>
    /// <param name="count"></param>
    /// <returns>
    /// A tuple that contain the following information
    /// The position number of found template - integer(2 bytes)
    /// The accuracy score of found template - integer(2 bytes)
    /// </returns>
    public Tuple<int, int> SearchTemplate(int charBufferNumber = CharBuffer1, int positionStart = 0, int count = -1)
    {
        int templatesCount, positionNumber, accuracyScore;
        if (charBufferNumber != CharBuffer1 && charBufferNumber != CharBuffer2)
            throw new Exception("The given charbuffer number is invalid!");

        if (count > 0)
            templatesCount = count;
        else
            templatesCount = (int)GetStorageCapacity();

        var packetPayload = new List<byte>
            {
                FingerprintConstants.SearchTemplate,
                (byte) charBufferNumber,
                (byte) RightShift(positionStart, 8),
                (byte) RightShift(positionStart, 0),
                (byte) RightShift(templatesCount, 8),
                (byte) RightShift(templatesCount, 0),
            };

        WritePacket(CommandPacket, packetPayload.ToArray());
        var receivedPacket = ReadPacket();

        var receivedPacketType = receivedPacket.Item1;
        var receivedPacketPayload = receivedPacket.Item2;

        if (receivedPacketType != AckPacket)
            throw new Exception("The received packet is no ack packet!");

        // DEBUG Found template
        if (receivedPacketPayload[0] == Ok)
        {
            positionNumber = LeftShift(receivedPacketPayload[1], 8);
            positionNumber = positionNumber | LeftShift(receivedPacketPayload[2], 0);

            accuracyScore = LeftShift(receivedPacketPayload[3], 8);
            accuracyScore = accuracyScore | LeftShift(receivedPacketPayload[4], 0);

            return Tuple.Create(positionNumber, accuracyScore);
        }

        else if (receivedPacketPayload[0] == ErrorCommunication)
            throw new Exception("Communication error");

        // DEBUG Did not found a matching template
        else if (receivedPacketPayload[0] == ErrorNoTemplateFound)
            return Tuple.Create(-1, -1);

        else
            throw new Exception("Unknown error " + receivedPacketPayload[0].ToString("X"));
    }

    /// <summary>
    /// Loads an existing template specified by position number to specified char buffer
    /// </summary>
    /// <param name="positionNumber"></param>
    /// <param name="charBufferNumber"></param>
    /// <returns></returns>
    public bool LoadTemplate(int positionNumber, int charBufferNumber = CharBuffer1)
    {
        if (positionNumber < 0x0000 || positionNumber >= GetStorageCapacity())
            throw new Exception("The given position number is invalid!");

        if (charBufferNumber != CharBuffer1 && charBufferNumber != CharBuffer2)
            throw new Exception("The given char buffer number is invalid!");

        var packetPayload = new List<byte>
            {
                FingerprintConstants.LoadTemplate,
                (byte) charBufferNumber,
                (byte) RightShift(positionNumber, 8),
                (byte) RightShift(positionNumber, 0),
            };

        WritePacket(CommandPacket, packetPayload.ToArray());
        var receivedPacket = ReadPacket();

        var receivedPacketType = receivedPacket.Item1;
        var receivedPacketPayload = receivedPacket.Item2;

        if (receivedPacketType != AckPacket)
            throw new Exception("The received packet is no ack packet!");

        // DEBUG Template loaded successful
        if (receivedPacketPayload[0] == Ok)
            return true;

        else if (receivedPacketPayload[0] == ErrorCommunication)
            throw new Exception("Communication error");

        else if (receivedPacketPayload[0] == ERROR_LOADTEMPLATE)
            throw new Exception("The template could not be read");

        else if (receivedPacketPayload[0] == ErrorInvalidPosition)
            throw new Exception("Could not load template from that position");

        else
            throw new Exception("Unknown error " + receivedPacketPayload[0].ToString("X"));
    }

    /// <summary>
    /// Deletes templates from fingerprint database.Per default one
    /// </summary>
    /// <param name="positionNumber">The starting position</param>
    /// <param name="count">The number of templates to be deleted</param>
    /// <returns></returns>
    public bool DeleteTemplate(int positionNumber, int count = 1)
    {
        int capacity = GetStorageCapacity();

        if (positionNumber < 0x0000 || positionNumber >= capacity)
            throw new Exception("The given position number is invalid!");

        if (count < 0x0000 || count > capacity - positionNumber)
            throw new Exception("The given count is invalid!");

        var packetPayload = new List<byte>
            {
                FingerprintConstants.DeleteTemplate,
                (byte) RightShift(positionNumber, 8),
                (byte) RightShift(positionNumber, 0),
                (byte) RightShift(count, 8),
                (byte) RightShift(count, 0)
            };

        WritePacket(CommandPacket, packetPayload.ToArray());
        var receivedPacket = ReadPacket();

        var receivedPacketType = receivedPacket.Item1;
        var receivedPacketPayload = receivedPacket.Item2;

        if (receivedPacketType != AckPacket)
            throw new Exception("The received packet is no ack packet!");

        // DEBUG Template deleted successful
        if (receivedPacketPayload[0] == Ok)
            return true;

        else if (receivedPacketPayload[0] == ErrorCommunication)
            throw new Exception("Communication error");

        else if (receivedPacketPayload[0] == ErrorInvalidPosition)
            throw new Exception("Invalid position");

        // DEBUG Could not delete template
        else if (receivedPacketPayload[0] == ErrorDeleteTemplate)
            return false;

        else
            throw new Exception("Unknown error " + receivedPacketPayload[0].ToString("X"));
    }

    /// <summary>
    /// Deletes all templates from the fingeprint database
    /// </summary>
    /// <returns></returns>
    public bool ClearDatabase()
    {
        var packetPayload = new List<byte> { FingerprintConstants.ClearDatabase };

        WritePacket(CommandPacket, packetPayload.ToArray());
        var receivedPacket = ReadPacket();

        var receivedPacketType = receivedPacket.Item1;
        var receivedPacketPayload = receivedPacket.Item2;

        if (receivedPacketType != AckPacket)
            throw new Exception("The received packet is no ack packet!");

        // DEBUG Database cleared successful
        if (receivedPacketPayload[0] == Ok)
            return true;

        else if (receivedPacketPayload[0] == ErrorCommunication)
            throw new Exception("Communication error");

        // DEBUG Could not clear database
        else if (receivedPacketPayload[0] == ErrorClearDatabase)
            return false;

        else
            throw new Exception("Unknown error " + receivedPacketPayload[0].ToString("X"));
    }

    /// <summary>
    /// Compare the finger characteristics of char buffer 1 with char buffer 2 and returns the accuracy score
    /// </summary>
    /// <returns>The accuracy score. 0 means fingers are not the same</returns>
    public int CompareCharacteristics()
    {
        int accuracyScore;
        var packetPayload = new List<byte> { FingerprintConstants.CompareCharacteristics };

        WritePacket(CommandPacket, packetPayload.ToArray());
        var receivedPacket = ReadPacket();

        var receivedPacketType = receivedPacket.Item1;
        var receivedPacketPayload = receivedPacket.Item2;

        if (receivedPacketType != AckPacket)
            throw new Exception("The received packet is no ack packet!");

        // DEBUG Comparison successful
        if (receivedPacketPayload[0] == Ok)
        {
            accuracyScore = LeftShift(receivedPacketPayload[1], 8);
            accuracyScore = accuracyScore | LeftShift(receivedPacketPayload[2], 0);
            return accuracyScore;
        }


        else if (receivedPacketPayload[0] == ErrorCommunication)
            throw new Exception("Communication error");

        // DEBUG The characteristics do not matching
        else if (receivedPacketPayload[0] == ErrorNotMatching)
            return 0;

        else
            throw new Exception("Unknown error " + receivedPacketPayload[0].ToString("X"));
    }

    /// <summary>
    /// Uploads finger characteristics to specified char buffer
    /// </summary>
    /// <param name="charBufferNumber"></param>
    /// <param name="characteristicsData"></param>
    /// <returns></returns>
    public bool UploadCharacteristics(byte[] characteristicsData, int charBufferNumber = CharBuffer1)
    {
        if (charBufferNumber != CharBuffer1 && charBufferNumber != CharBuffer2)
            throw new Exception("The given char buffer number is invalid!");

        if (characteristicsData.Length < 1)
            throw new Exception("The characteristics data is required!");

        int maxPacketSize = GetMaxPacketSize();

        // Upload command
        var packetPayload = new List<byte>
            {
                FingerprintConstants.UploadCharacteristics,
                (byte) charBufferNumber
            };

        WritePacket(CommandPacket, packetPayload.ToArray());
        var receivedPacket = ReadPacket();

        var receivedPacketType = receivedPacket.Item1;
        var receivedPacketPayload = receivedPacket.Item2;

        if (receivedPacketType != AckPacket)
            throw new Exception("The received packet is no ack packet!");

        // DEBUG The sensor will sent follow-up packets
        if (receivedPacketPayload[0] == Ok) { }

        else if (receivedPacketPayload[0] == ErrorCommunication)
            throw new Exception("Communication error");

        else if (receivedPacketPayload[0] == PacketResponseFail)
            throw new Exception("Could not upload characteristics");

        else
            throw new Exception("Unknown error " + receivedPacketPayload[0].ToString("X"));

        // Upload data packets
        int packetNumber = characteristicsData.Length / maxPacketSize;

        if (packetNumber <= 1)
            WritePacket(EndDataPacket, characteristicsData);
        else
        {
            int lfrom, lto;
            int i = 1;
            while (i < packetNumber)
            {
                lfrom = (int)((i - 1) * maxPacketSize);
                lto = (int)(lfrom + maxPacketSize);
                WritePacket(DataPacket, characteristicsData[lfrom..lto]);
                i += 1;
            }

            lfrom = (int)((i - 1) * maxPacketSize);
            lto = characteristicsData.Length;
            WritePacket(EndDataPacket, characteristicsData[lfrom..lto]);
        }


        // Verify uploaded characteristics
        var characterics = DownloadCharacteristics(charBufferNumber);
        return characterics == characteristicsData;

    }

    /// <summary>
    /// Generates a random 32-bit decimal number
    /// </summary>
    /// <returns></returns>
    public uint GenerateRandomNumber()
    {
        var packetPayload = new List<byte> { FingerprintConstants.GenerateRandomNumber };

        WritePacket(CommandPacket, packetPayload.ToArray());
        var receivedPacket = ReadPacket();

        var receivedPacketType = receivedPacket.Item1;
        var receivedPacketPayload = receivedPacket.Item2;

        if (receivedPacketType != AckPacket)
            throw new Exception("The received packet is no ack packet!");

        if (receivedPacketPayload[0] == Ok) { }

        else if (receivedPacketPayload[0] == ErrorCommunication)
            throw new Exception("Communication error");

        else
            throw new Exception("Unknown error " + receivedPacketPayload[0].ToString("X"));

        uint number = 0;
        number = number | (uint) LeftShift(receivedPacketPayload[1], 24);
        number = number | (uint) LeftShift(receivedPacketPayload[2], 16);
        number = number | (uint) LeftShift(receivedPacketPayload[3], 8);
        number = number | (uint)LeftShift(receivedPacketPayload[4], 0);
        return number;
    }

    /// <summary>
    /// Downloads the finger characteristics from the specified char buffer
    /// </summary>
    /// <param name="charBufferNumber"></param>
    /// <returns></returns>
    public byte[] DownloadCharacteristics(int charBufferNumber = CharBuffer1)
    {
        if (charBufferNumber != CharBuffer1 && charBufferNumber != CharBuffer2)
            throw new Exception("The given char buffer number is invalid!");

        var packetPayload = new List<byte>
            {
                FingerprintConstants.DownloadCharacteristics,
                (byte) charBufferNumber
            };

        WritePacket(CommandPacket, packetPayload.ToArray());
        var receivedPacket = ReadPacket();

        var receivedPacketType = receivedPacket.Item1;
        var receivedPacketPayload = receivedPacket.Item2;

        if (receivedPacketType != AckPacket)
            throw new Exception("The received packet is no ack packet!");

        // DEBUG The sensor will sent follow-up packets
        if (receivedPacketPayload[0] == Ok) { }

        else if (receivedPacketPayload[0] == ErrorCommunication)
            throw new Exception("Communication error");

        else if (receivedPacketPayload[0] == ErrorDownloadCharacteristics)
            throw new Exception("Could not download characteristics");

        else
            throw new Exception("Unknown error " + receivedPacketPayload[0].ToString("X"));

        var completePayload = new List<byte>();

        // Get follow-up data packets until the last data packet is received
        while (receivedPacketType != EndDataPacket)
        {
            receivedPacket = ReadPacket();

            receivedPacketType = receivedPacket.Item1;
            receivedPacketPayload = receivedPacket.Item2;

            if (receivedPacketType != DataPacket && receivedPacketType != EndDataPacket)
                throw new Exception("The received packet is no data packet!");

            for (int i = 0; i < receivedPacketPayload.Count; i++)
            {
                completePayload.Add(receivedPacketPayload[i]);
            }
        }

        return completePayload.ToArray();
    }
}
public record struct FingerprintSystemParameters
{
    // The status register (2 bytes)
    public int StatusRegister { get; set; }

    // The system id (2 bytes)
    public int SystemId { get; set; }

    // The storage capacity (2 bytes)
    public int StorageCapacity { get; set; }

    // The security level (2 bytes)
    public int SecurityLevel { get; set; }

    // The sensor address (4 bytes)
    public uint SensorAddress { get; set; }

    // The packet length (2 bytes)
    public int PacketLength { get; set; }

    // The baud rate (2 bytes)
    public int BaudRate { get; set; }
}

public record struct SensorStorageInfo
{
    public int Used { get; set; }
    public int Total { get; set; }
}