using SixLabors.ImageSharp.Formats.Bmp;
using System;
using System.IO;

namespace Fingerprint.NET.ConsoleSample;

class Program
{
    static FingerprintController _fingerprintController;

    static void Main(string[] args)
    {
        Console.WriteLine("Enter the name of the Serial Port used");
        string port = Console.ReadLine();

        _fingerprintController = FingerprintController.Instance;
        _fingerprintController.Initialise(port);
        bool loop = true;

        while (loop)
        {
            Console.WriteLine("What would you like to do.\n" +
                "Enter 1 to Enrol, 2 to Search, 3 to Delete, 4 to Download and 5 to quit");

            switch (Console.ReadLine())
            {
                case "1":
                    EnrolFingerprint();
                    break;
                case "2":
                    SearchFingerprint();
                    break;
                case "3":
                    DeleteFingerprint();
                    break;
                case "4":
                    DownloadFingerprintImage("./fingerprint.bmp");
                    break;
                case "5":
                    loop = false;
                    break;
                default:
                    Console.WriteLine("Invalid Input\n");
                    break;
            }
        }
    }

    static void EnrolFingerprint()
    {
        var storageInfo = _fingerprintController.GetStorageUsage();
        Console.WriteLine($"Currently used templates:  {storageInfo.Used} / {storageInfo.Total}");

        try
        {
            _fingerprintController.EnrolFingerprint(Console.WriteLine);
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Operation failed!");
            Console.WriteLine($"Exception message: {ex.Message}");
        }

    }
    static void SearchFingerprint()
    {
        var storageInfo = _fingerprintController.GetStorageUsage();
        Console.WriteLine($"Currently used templates:  {storageInfo.Used} / {storageInfo.Total}");

        try
        {
            _fingerprintController.SearchFingerprint(Console.WriteLine);
            Console.WriteLine();

        }
        catch (Exception ex)
        {
            Console.WriteLine("Operation failed!");
            Console.WriteLine($"Exception message: {ex.Message}");
        }
    }

    static void DeleteFingerprint()
    {
        var storageInfo = _fingerprintController.GetStorageUsage();
        Console.WriteLine($"Currently used templates:  {storageInfo.Used} / {storageInfo.Total}");

        try
        {
            Console.WriteLine("Please enter the template position you want to delete: ");
            int positionNumber = Convert.ToInt32(Console.ReadLine());

            _fingerprintController.DeleteFingerprint(positionNumber);
            Console.WriteLine("Deleted Successfully");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Operation failed!");
            Console.WriteLine($"Exception message: {ex.Message}");
        }
    }

    static void DownloadFingerprintImage(string imageDestinationPath)
    {
        var storageInfo = _fingerprintController.GetStorageUsage();
        Console.WriteLine($"Currently used templates:  {storageInfo.Used} / {storageInfo.Total}");

        try
        {
            var image = _fingerprintController.GetFingerprintImage(Console.WriteLine);
            using (var fs = new FileStream(imageDestinationPath, FileMode.Create))
            image.Save(fs, new BmpEncoder());


            Console.WriteLine($"The image has been saved to '{imageDestinationPath}'.");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Operation failed!");
            Console.WriteLine($"Exception message: {ex.Message}");
        }
    }
}
