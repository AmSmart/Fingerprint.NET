using System;

namespace Fingerprint.NET
{
    public class FingerprintConstants
    {
        // Baotou start byte
        public const int StartCode = 0xEF01;

        // Packet identification        
        public const int CommandPacket = 0x01;

        public const int AckPacket = 0x07;
        public const int DataPacket = 0x02;
        public const int EndDataPacket = 0x08;

        // Instruction codes
        public const int VerifyPassword = 0x13;
        public const int SetPassword = 0x12;
        public const int SetAddress = 0x15;
        public const int SetSystemParameter = 0x0E;
        public const int GetSystemParameters = 0x0F;
        public const int TemplateIndex = 0x1F;
        public const int TemplateCount = 0x1D;

        public const int ReadImage = 0x01;

        // Note: The documentation mean upload to host computer.
        public const int DownloadImage = 0x0A;

        public const int ConvertImage = 0x02;

        public const int CreateTemplate = 0x05;
        public const int StoreTemplate = 0x06;
        public const int SearchTemplate = 0x04;
        public const int LoadTemplate = 0x07;
        public const int DeleteTemplate = 0x0C;

        public const int ClearDatabase = 0x0D;
        public const int GenerateRandomNumber = 0x14;
        public const int CompareCharacteristics = 0x03;

        // Note: The documentation mean download from host computer.
        public const int UploadCharacteristics = 0x09;

        // Note: The documentation mean upload to host computer.
        public const int DownloadCharacteristics = 0x08;

        // Parameters of setSystemParameter()
        public const int SetSystemParameterBaudRate = 4;
        public const int SetSystemParameterSecurityLevel = 5;
        public const int SetSystemParameterPackageSize = 6;

        // Packet reply confirmations
        public const int Ok = 0x00;
        public const int ErrorCommunication = 0x01;

        public const int ErrorWrongPassword = 0x13;

        public const int ErrorInvalidRegister = 0x1A;

        public const int ErrorNoFinger = 0x02;
        public const int ErrorReadImage = 0x03;

        public const int ErrorMessyImage = 0x06;
        public const int ErrorFewFeaturePoints = 0x07;
        public const int ErrorInvalidImage = 0x15;

        public const int ErrorCharacteristicsMismatch = 0x0A;

        public const int ErrorInvalidPosition = 0x0B;
        public const int ErrorFlash = 0x18;

        public const int ErrorNoTemplateFound = 0x09;

        public const int ERROR_LOADTEMPLATE = 0x0C;

        public const int ErrorDeleteTemplate = 0x10;

        public const int ErrorClearDatabase = 0x11;

        public const int ErrorNotMatching = 0x08;

        public const int ErrorDownloadImage = 0x0F;
        public const int ErrorDownloadCharacteristics = 0x0D;

        // Unknown error codes
        public const int AddrCode = 0x20;
        public const int PASSVERIFY = 0x21;

        public const int PacketResponseFail = 0x0E;

        public const int ERROR_TIMEOUT = 0xFF;
        public const int ERROR_BADPACKET = 0xFE;

        // Char buffers        
        public const int CharBuffer1 = 0x01;
        public const int CharBuffer2 = 0x02;

    }
}
