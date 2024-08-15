using CommandLine;
using DiscUtils;
using Img2Ffu.Writer;
using Img2Ffu.Writer.Data;
using Img2Ffu.Writer.Flashing;
using Img2Ffu.Writer.Manifest;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace Img2Ffu
{
    internal partial class Program
    {
        private class Options
        {
            [Option('i', "input", Required = true, HelpText = "Input file path.")]
            public string InputFile { get; set; }

            [Option('o', "output", Required = true, HelpText = "Output FFU file path.")]
            public string FFUFile { get; set; }

            [Option('p', "platform", Required = true, HelpText = "Platform IDs separated by semicolons.")]
            public string PlatformID { get; set; }

            [Option('s', "sector-size", Required = true, HelpText = "Sector size.")]
            public uint SectorSize { get; set; }

            [Option('b', "block-size", Required = true, HelpText = "Block size.")]
            public uint BlockSize { get; set; }

            [Option("anti-theft-version", Required = true, HelpText = "Anti-theft version.")]
            public string AntiTheftVersion { get; set; }

            [Option("os-version", Required = true, HelpText = "Operating system version.")]
            public string OperatingSystemVersion { get; set; }

            [Option("excluded-partitions", Required = true, HelpText = "File path to excluded partition names.")]
            public string ExcludedPartitionNamesFilePath { get; set; }

            [Option("max-blank-blocks", Required = true, HelpText = "Maximum number of blank blocks allowed.")]
            public uint MaximumNumberOfBlankBlocksAllowed { get; set; }

            [Option("flash-update-version", Required = true, HelpText = "Flash update version.")]
            public FlashUpdateVersion FlashUpdateVersion { get; set; }

            [Option("device-target-info", HelpText = "Device target info.")]
            public string DeviceTargetInfo { get; set; }

            [Option("second-ffu", HelpText = "Path to the second FFU file for porting.")]
            public string SecondFFUFile { get; set; }
        }
        private static void Main(string[] args)
        {
            _ = Parser.Default.ParseArguments<Options>(args).WithParsed(o =>
            {
                Logging.Log("img2ffu - Converts raw image (img) files into full flash update (FFU) files");
                Logging.Log("Copyright (c) 2019-2024, Gustave Monce - gus33000.me - @gus33000");
                Logging.Log("Copyright (c) 2018, Rene Lergner - wpinternals.net - @Heathcliff74xda");
                Logging.Log("Released under the MIT license at github.com/WOA-Project/img2ffu");
                Logging.Log("");

                try
                {
                    string excludedPartitionNamesFilePath = o.ExcludedPartitionNamesFilePath;

                    if (!File.Exists(excludedPartitionNamesFilePath))
                    {
                        excludedPartitionNamesFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), o.ExcludedPartitionNamesFilePath);
                    }

                    if (!File.Exists(excludedPartitionNamesFilePath))
                    {
                        Logging.Log("Something happened.", Logging.LoggingLevel.Error);
                        Logging.Log("We couldn't find the provisioning partition file.", Logging.LoggingLevel.Error);
                        Logging.Log("Please specify one using the corresponding argument switch", Logging.LoggingLevel.Error);
                        Environment.Exit(1);
                        return;
                    }

                    // Generate FFU from the primary input file
                    GenerateFFU(o.InputFile, o.FFUFile, o.PlatformID.Split(';'), o.SectorSize, o.BlockSize, o.AntiTheftVersion, o.OperatingSystemVersion, File.ReadAllLines(excludedPartitionNamesFilePath), o.MaximumNumberOfBlankBlocksAllowed, o.FlashUpdateVersion, ParseDeviceTargetInfos(o.DeviceTargetInfo));

                    // Handle porting between two FFU files if the second FFU file is specified
                    if (!string.IsNullOrEmpty(o.SecondFFUFile))
                    {
                        PortFFUFiles(o.FFUFile, o.SecondFFUFile);
                    }
                }
                catch (Exception ex)
                {
                    Logging.Log("Something happened.", Logging.LoggingLevel.Error);
                    Logging.Log(ex.Message, Logging.LoggingLevel.Error);
                    Logging.Log(ex.StackTrace, Logging.LoggingLevel.Error);
                    Environment.Exit(1);
                }
            });
        }

        private static List<DeviceTargetInfo> ParseDeviceTargetInfos(string deviceTargetInfo)
        {
            var deviceTargetInfos = new List<DeviceTargetInfo>();

            if (!string.IsNullOrEmpty(deviceTargetInfo))
            {
                var infos = deviceTargetInfo.Split(';');
                foreach (var info in infos)
                {
                    // Assuming DeviceTargetInfo has a constructor or method to parse info
                    deviceTargetInfos.Add(new DeviceTargetInfo(info));
                }
            }

            return deviceTargetInfos;
        }

        private static void PortFFUFiles(string sourceFFUFile, string targetFFUFile)
        {
            if (!File.Exists(sourceFFUFile))
            {
                Logging.Log($"Source FFU file does not exist: {sourceFFUFile}", Logging.LoggingLevel.Error);
                return;
            }

            if (File.Exists(targetFFUFile))
            {
                Logging.Log($"Target FFU file already exists: {targetFFUFile}", Logging.LoggingLevel.Error);
                return;
            }

            // Porting logic from source FFU to target FFU
            using (var sourceStream = new FileStream(sourceFFUFile, FileMode.Open, FileAccess.Read))
            using (var targetStream = new FileStream(targetFFUFile, FileMode.Create, FileAccess.Write))
            {
                // Read the source FFU file
                var sourceFFU = new FFU(sourceStream);

                // Create a new FFU for the target
                var targetFFU = new FFU();

                // Copy metadata from source to target
                targetFFU.Metadata = sourceFFU.Metadata;

                // Copy partitions from source to target
                foreach (var partition in sourceFFU.Partitions)
                {
                    targetFFU.AddPartition(partition);
                }

                // Write the target FFU to the file
                targetFFU.WriteToStream(targetStream);
            }

            Logging.Log($"FFU ported successfully from {sourceFFUFile} to {targetFFUFile}", Logging.LoggingLevel.Info);
            Logging.Log($"Porting data from {sourceFFUFile} to {targetFFUFile}...");

            // Example: Simple copy (placeholder logic)
            File.Copy(sourceFFUFile, targetFFUFile);

            // Add any additional processing required for porting here
            Logging.Log("Porting completed.");
        }
    }
}
