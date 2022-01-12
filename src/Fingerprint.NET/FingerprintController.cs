using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Fingerprint.NET;

/// <summary>
/// Singleton class to provide access to fingerprint functionalities
/// </summary>
public sealed class FingerprintController : IDisposable
{
    private Fingerprint _fingerprint;
    private bool _initialised;

    private FingerprintController()
    {
    }

    public static FingerprintController Instance { get { return FingerprintControllerFactory._instance; } }

    /// <summary>
    /// Initialise the Fingerprint Sensor
    /// </summary>
    public void Initialise(string port, int baudRate = 57600, uint address = 0xFFFFFFFF, uint password = 0x00000000)
    {
        if (_fingerprint is not null)
            throw new Exception("Fingerprint sensor has already been initialised");

        _fingerprint = new Fingerprint(port, baudRate, address, password);

        if (!_fingerprint.VerifyPassword())
            throw new Exception("The given fingerprint sensor password is wrong!");

        _initialised = true;
    }

    /// <summary>
    /// Returns sensor's storage capacity usage
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public SensorStorageInfo GetStorageUsage()
    {
        if (!_initialised)
            throw new Exception("Fingerprint sensor not initialised");

        return new SensorStorageInfo
        {
            Total = _fingerprint.GetStorageCapacity(),
            Used = _fingerprint.GetTemplateCount()
        };
    }

    /// <summary>
    /// Enrols a fingerprint to the sensor and returns its template number
    /// Simply returns template number if fingerprint already exists
    /// </summary>
    public int EnrolFingerprint(Action<string> notify)
    {
        if (!_initialised)
            throw new Exception("Fingerprint sensor not initialised");

        notify("Waiting for finger...");

        // Wait that finger is read
        while (!_fingerprint.ReadImage())
            ;

        // Converts read image to characteristics and stores it in charbuffer 1
        _fingerprint.ConvertImage(FingerprintConstants.CharBuffer1);

        // Checks if finger is already enrolled
        var result = _fingerprint.SearchTemplate();
        int positionNumber = result.Item1;

        if (positionNumber >= 0)
        {
            notify($"Template already exists at position #{positionNumber}");
            return positionNumber;
        }


        notify("Remove finger...");
        Thread.Sleep(2000);

        notify("Waiting for same finger again...");

        // Wait that finger is read again
        while (!_fingerprint.ReadImage())
            ;

        // Converts read image to characteristics and stores it in charbuffer 2
        _fingerprint.ConvertImage(FingerprintConstants.CharBuffer2);

        // Compares the charbuffers
        if (_fingerprint.CompareCharacteristics() == 0)
            throw new Exception("Fingers do not match");

        // Creates a template
        _fingerprint.CreateTemplate();

        // Saves template at new position number
        positionNumber = _fingerprint.StoreTemplate();
        notify($"Finger enrolled successfully at template position {positionNumber}");

        return positionNumber;
    }

    /// <summary>
    /// Searches for a fingerprint, returns a tuple containing the template number and
    /// search accuracy respectively. Reurns -1 for both values if not found
    /// </summary>
    public Tuple<int, int> SearchFingerprint(Action<string> notify)
    {
        if (!_initialised)
            throw new Exception("Fingerprint sensor not initialised");

        notify("Waiting for finger...");

        while (!_fingerprint.ReadImage()) ;

        _fingerprint.ConvertImage(FingerprintConstants.CharBuffer1);
        var result = _fingerprint.SearchTemplate();

        int positionNumber = result.Item1;
        int accuracyScore = result.Item2;

        if (positionNumber == -1)
        {
            notify("No match found!");
            return Tuple.Create(-1, -1);
        }
        notify($"Found template at position #{positionNumber} witch accuracy score of: {accuracyScore}");
        return Tuple.Create(positionNumber, accuracyScore);
    }

    /// <summary>
    /// Deletes a fingerprint image stored in a particulat template number
    /// </summary>
    public void DeleteFingerprint(int templateNumber)
    {
        if (!_initialised)
            throw new Exception("Fingerprint sensor not initialised");

        _fingerprint.DeleteTemplate(templateNumber);
    }

    /// <summary>
    /// Returns an Image of a scanned fingerprint
    /// </summary>
    public Image GetFingerprintImage(Action<string> notify)
    {
        notify("Waiting for finger...");

        // Wait that finger is read
        while (!_fingerprint.ReadImage()) ;


        notify("Downloading image (this take a while)...");

        var image = _fingerprint.GetFingerprintImage();
        return image;
    }

    public void Dispose()
    {
        if (_fingerprint is not null)
        {
            _fingerprint.Dispose();
            _fingerprint = null;
            _initialised = false;
        }
    }

    private class FingerprintControllerFactory
    {
        /// <summary>
        /// Explicit static constructor to tell C# compiler not to mark type as beforefieldinit
        /// </summary>
        static FingerprintControllerFactory()
        {
        }

        internal static readonly FingerprintController _instance = new FingerprintController();
    }

}