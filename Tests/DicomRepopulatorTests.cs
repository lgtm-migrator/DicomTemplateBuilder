﻿using System;
using System.Collections.Generic;
using System.IO;
using Dicom;
using NUnit.Framework;
using Repopulator;

namespace Tests
{
    [TestFixture]
    public class DicomRepopulatorTests
    {
        private readonly string _inputFileBase = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestInput");
        private readonly string _outputFileBase = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestOutput");
        private readonly string _seriesFilesBase = Path.Combine(TestContext.CurrentContext.TestDirectory, "MultipleSeriesTest");

        private const string IM_0001_0013_NAME = "IM_0001_0013.dcm";
        private const string IM_0001_0019_NAME = "IM_0001_0019.dcm";

        [SetUp]
        public void SetUp()
        {
            Directory.CreateDirectory(_inputFileBase);
            Directory.CreateDirectory(_outputFileBase);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_inputFileBase)) Directory.Delete(_inputFileBase, true);
            if (Directory.Exists(_outputFileBase)) Directory.Delete(_outputFileBase, true);
            if (Directory.Exists(_seriesFilesBase)) Directory.Delete(_seriesFilesBase, true);
        }

        [Test]
        public void Test_AbsolutePaths_InCsv()
        {
            //Create a dicom file in the input dir /subdir/IM_0001_0013.dcm
            var inputDicom = CreateInputFile(TestDicomFiles.IM_0001_0013,
                Path.Combine("subdir", nameof(TestDicomFiles.IM_0001_0013) +".dcm"));
            
            //Create a CSV with the full path to the image
            var inputCsv = CreateInputCsvFile(
                $@"File,PatientID
{inputDicom.FullName},ABC");

            //run repopulator
            var outDir = AssertRunsSuccesfully(1,inputCsv,null,inputDicom.Directory.Parent,(o)=>o.FileNameColumn = "File");
            
            //anonymous image should appear in the subdirectory of the out dir
            var expectedOutFile = new FileInfo(Path.Combine(outDir.FullName, "subdir", nameof(TestDicomFiles.IM_0001_0013) + ".dcm"));
            FileAssert.Exists(expectedOutFile);
        }
        
        [Test]
        public void SingleFileBasicOperationTest()
        {
            var inFile = CreateInputFile(TestDicomFiles.IM_0001_0013,nameof(TestDicomFiles.IM_0001_0013) +".dcm");

            var outDir = AssertRunsSuccesfully(1, null, 
                
                //Treat Csv column "ID" as a replacement for PatientID
                CreateExtraMappingsFile("ID:PatientID"), inFile.Directory,
                
                //Give it BasicTest.csv 
                (o) => o.InputCsv= Path.Combine(TestContext.CurrentContext.TestDirectory, "BasicTest.csv"));

            //Anonymous dicom image should exist
            var expectedFile = new FileInfo(Path.Combine(outDir.FullName, nameof(TestDicomFiles.IM_0001_0013) + ".dcm"));
            FileAssert.Exists(expectedFile);

            //it should have the patient ID from the csv
            DicomFile file = DicomFile.Open(expectedFile.FullName);
            Assert.AreEqual("NewPatientID1", file.Dataset.GetValue<string>(DicomTag.PatientID, 0));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void KeyNotFirstColumn(bool runSecondaryAnon)
        {
            string inputDirPath = Path.Combine(_inputFileBase, "KeyNotFirstColumn");
            const string testFileName = IM_0001_0013_NAME;

            Directory.CreateDirectory(inputDirPath);
            File.WriteAllBytes(Path.Combine(inputDirPath, testFileName), TestDicomFiles.IM_0001_0013);

            string outputDirPath = Path.Combine(_outputFileBase, "KeyNotFirstColumn");
            string expectedFile = Path.Combine(outputDirPath, testFileName);

            var options = new DicomRepopulatorOptions
            {
                InputCsv = Path.Combine(TestContext.CurrentContext.TestDirectory, "KeyNotFirstColumn.csv"),
                InputFolder = inputDirPath,
                OutputFolder = outputDirPath,
                Anonymise = runSecondaryAnon,
                InputExtraMappings = CreateExtraMappingsFile( "ID:PatientID","sopid:SOPInstanceUID" ).FullName,
                NumThreads = 4
            };

            int result = new DicomRepopulatorProcessor(TestContext.CurrentContext.TestDirectory).Process(options);
            Assert.AreEqual(0, result);

            Assert.True(File.Exists(expectedFile), "Expected output file {0} to exist", expectedFile);

            DicomFile file = DicomFile.Open(expectedFile);
            Assert.AreEqual("NewPatientID1", file.Dataset.GetValue<string>(DicomTag.PatientID, 0));
        }

        [Test]
        public void DateRepopulation()
        {
            string inputDirPath = Path.Combine(_inputFileBase, "DateRepopulation");
            const string testFileName = IM_0001_0013_NAME;

            Directory.CreateDirectory(inputDirPath);
            File.WriteAllBytes(Path.Combine(inputDirPath, testFileName), TestDicomFiles.IM_0001_0013);

            string outputDirPath = Path.Combine(_outputFileBase, "DateRepopulation");
            string expectedFile = Path.Combine(outputDirPath, testFileName);

            var options = new DicomRepopulatorOptions
            {
                InputCsv = Path.Combine(TestContext.CurrentContext.TestDirectory, "WithDate.csv"),
                InputFolder = inputDirPath,
                OutputFolder = outputDirPath,
                InputExtraMappings = CreateExtraMappingsFile( "ID:PatientID", "Date:StudyDate" ).FullName,
                NumThreads = 4
            };

            int result = new DicomRepopulatorProcessor(TestContext.CurrentContext.TestDirectory).Process(options);
            Assert.AreEqual(0, result);

            Assert.True(File.Exists(expectedFile), "Expected output file {0} to exist", expectedFile);

            DicomFile file = DicomFile.Open(expectedFile);

            Assert.AreEqual("NewPatientID1", file.Dataset.GetValue<string>(DicomTag.PatientID, 0));
            Assert.AreEqual("20180601", file.Dataset.GetValue<string>(DicomTag.StudyDate, 0));
        }

        [Test]
        public void OneCsvColumnToMultipleDicomTags()
        {
            string inputDirPath = Path.Combine(_inputFileBase, "OneCsvColumnToMultipleDicomTags");
            const string testFileName = IM_0001_0013_NAME;

            Directory.CreateDirectory(inputDirPath);
            File.WriteAllBytes(Path.Combine(inputDirPath, testFileName), TestDicomFiles.IM_0001_0013);

            string outputDirPath = Path.Combine(_outputFileBase, "OneCsvColumnToMultipleDicomTags");
            string expectedFile = Path.Combine(outputDirPath, testFileName);

            var options = new DicomRepopulatorOptions
            {
                InputCsv = Path.Combine(TestContext.CurrentContext.TestDirectory, "WithDate.csv"),
                InputFolder = inputDirPath,
                OutputFolder = outputDirPath,
                InputExtraMappings = CreateExtraMappingsFile( "ID:PatientID", "Date:StudyDate", "Date:SeriesDate" ).FullName,
                NumThreads = 1
            };

            int result = new DicomRepopulatorProcessor(TestContext.CurrentContext.TestDirectory).Process(options);
            Assert.AreEqual(0, result);

            Assert.True(File.Exists(expectedFile), "Expected output file {0} to exist", expectedFile);

            DicomFile file = DicomFile.Open(expectedFile);
            Assert.AreEqual("NewPatientID1", file.Dataset.GetValue<string>(DicomTag.PatientID, 0));
            Assert.AreEqual("20180601", file.Dataset.GetValue<string>(DicomTag.StudyDate, 0));
            Assert.AreEqual("20180601", file.Dataset.GetValue<string>(DicomTag.SeriesDate, 0));
        }

        [Test]
        public void SpacesInCsvHeaderTest()
        {
            string inputDirPath = Path.Combine(_inputFileBase, "SpacesInCsvHeaderTest");
            const string testFileName = IM_0001_0013_NAME;

            Directory.CreateDirectory(inputDirPath);
            File.WriteAllBytes(Path.Combine(inputDirPath, testFileName), TestDicomFiles.IM_0001_0013);

            string outputDirPath = Path.Combine(_outputFileBase, "SpacesInCsvHeaderTest");
            string expectedFile = Path.Combine(outputDirPath, testFileName);

            var options = new DicomRepopulatorOptions
            {
                InputCsv = Path.Combine(TestContext.CurrentContext.TestDirectory, "SpacesInCsvHeaderTest.csv"),
                InputFolder = inputDirPath,
                OutputFolder = outputDirPath,
                InputExtraMappings = CreateExtraMappingsFile( "ID:PatientID" ).FullName,
                NumThreads = 1
            };

            int result = new DicomRepopulatorProcessor(TestContext.CurrentContext.TestDirectory).Process(options);
            Assert.AreEqual(0, result);

            Assert.True(File.Exists(expectedFile), "Expected output file {0} to exist", expectedFile);

            DicomFile file = DicomFile.Open(expectedFile);
            Assert.AreEqual("NewPatientID1", file.Dataset.GetValue<string>(DicomTag.PatientID, 0));
        }

        [Test]
        public void MultipleFilesSameSeriesTest()
        {
            string inputDirPath = Path.Combine(_inputFileBase, "MultipleFilesSameSeriesTest");
            const string testFileName1 = IM_0001_0013_NAME;
            const string testFileName2 = IM_0001_0019_NAME;

            Directory.CreateDirectory(inputDirPath);
            File.WriteAllBytes(Path.Combine(inputDirPath, testFileName1), TestDicomFiles.IM_0001_0013);
            File.WriteAllBytes(Path.Combine(inputDirPath, testFileName2), TestDicomFiles.IM_0001_0013);

            string outputDirPath = Path.Combine(_outputFileBase, "MultipleFilesSameSeriesTest");
            string expectedFile1 = Path.Combine(outputDirPath, testFileName1);
            string expectedFile2 = Path.Combine(outputDirPath, testFileName2);

            var options = new DicomRepopulatorOptions
            {
                InputCsv = Path.Combine(TestContext.CurrentContext.TestDirectory, "BasicTest.csv"),
                InputFolder = inputDirPath,
                OutputFolder = outputDirPath,
                InputExtraMappings = CreateExtraMappingsFile( "ID:PatientID" ).FullName,
                NumThreads = 1
            };

            int result = new DicomRepopulatorProcessor(TestContext.CurrentContext.TestDirectory).Process(options);
            Assert.AreEqual(0, result);

            Assert.True(File.Exists(expectedFile1), "Expected output file {0} to exist", expectedFile1);
            Assert.True(File.Exists(expectedFile2), "Expected output file {0} to exist", expectedFile2);

            DicomFile file = DicomFile.Open(expectedFile1);
            Assert.AreEqual("NewPatientID1", file.Dataset.GetValue<string>(DicomTag.PatientID, 0));

            file = DicomFile.Open(expectedFile2);
            Assert.AreEqual("NewPatientID1", file.Dataset.GetValue<string>(DicomTag.PatientID, 0));
        }

        [Test]
        public void MultipleSeriesTest()
        {
            string inputDirPath = Path.Combine(_seriesFilesBase, "TestInput");
            const string testFileName1 = IM_0001_0013_NAME;
            const string testFileName2 = IM_0001_0019_NAME;

            Directory.CreateDirectory(Path.Combine(inputDirPath, "Series1"));
            Directory.CreateDirectory(Path.Combine(inputDirPath, "Series2"));
            File.WriteAllBytes(Path.Combine(inputDirPath, "Series1", testFileName1), TestDicomFiles.IM_0001_0013);
            File.WriteAllBytes(Path.Combine(inputDirPath, "Series1", testFileName2), TestDicomFiles.IM_0001_0013);
            File.WriteAllBytes(Path.Combine(inputDirPath, "Series2", testFileName1), TestDicomFiles.IM_0001_0019);
            File.WriteAllBytes(Path.Combine(inputDirPath, "Series2", testFileName2), TestDicomFiles.IM_0001_0019);

            string outputDirPath = Path.Combine(_seriesFilesBase, "TestOutput");
            string expectedFile1 = Path.Combine(outputDirPath, "Series1", testFileName1);
            string expectedFile2 = Path.Combine(outputDirPath, "Series1", testFileName2);
            string expectedFile3 = Path.Combine(outputDirPath, "Series2", testFileName1);
            string expectedFile4 = Path.Combine(outputDirPath, "Series2", testFileName2);

            var options = new DicomRepopulatorOptions
            {
                InputCsv = Path.Combine(TestContext.CurrentContext.TestDirectory, "TwoSeriesCsv.csv"),
                InputFolder = inputDirPath,
                OutputFolder = outputDirPath,
                InputExtraMappings = CreateExtraMappingsFile( "ID:PatientID" ).FullName,
                NumThreads = 4
            };

            int result = new DicomRepopulatorProcessor(TestContext.CurrentContext.TestDirectory).Process(options);
            Assert.AreEqual(0, result);

            Assert.True(File.Exists(expectedFile1), "Expected output file {0} to exist", expectedFile1);
            Assert.True(File.Exists(expectedFile2), "Expected output file {0} to exist", expectedFile2);
            Assert.True(File.Exists(expectedFile3), "Expected output file {0} to exist", expectedFile3);
            Assert.True(File.Exists(expectedFile4), "Expected output file {0} to exist", expectedFile4);

            DicomFile file = DicomFile.Open(expectedFile1);
            Assert.AreEqual("NewPatientID1", file.Dataset.GetValue<string>(DicomTag.PatientID, 0));

            file = DicomFile.Open(expectedFile2);
            Assert.AreEqual("NewPatientID1", file.Dataset.GetValue<string>(DicomTag.PatientID, 0));

            file = DicomFile.Open(expectedFile3);
            Assert.AreEqual("NewPatientID2", file.Dataset.GetValue<string>(DicomTag.PatientID, 0));

            file = DicomFile.Open(expectedFile4);
            Assert.AreEqual("NewPatientID2", file.Dataset.GetValue<string>(DicomTag.PatientID, 0));
        }
        /// <summary>
        /// Writes the supplied string to "ExtraMappings.txt" in the test directory and returns the path to the file
        /// </summary>
        /// <param name="contents"></param>
        /// <returns></returns>
        private FileInfo CreateExtraMappingsFile(params string[] contents)
        {
            return GenerateTextFile(contents, "ExtraMappings.txt");
        }
        
        /// <summary>
        /// Writes the supplied string to "Map.csv" in the test directory and returns the path to the file
        /// </summary>
        private FileInfo CreateInputCsvFile(params string[] contents)
        {
            return GenerateTextFile(contents, "Map.csv");
        }

        private FileInfo GenerateTextFile(string[] contents, string filename)
        {
            var filePath = Path.Combine(TestContext.CurrentContext.TestDirectory,filename);
            File.WriteAllLines(filePath,contents);

            return new FileInfo(filePath);
        }

        private FileInfo CreateInputFile(byte[] bytes,string filename,[System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            string inputDirPath = Path.Combine(_inputFileBase, memberName);
            
            Directory.CreateDirectory(inputDirPath);
            var toReturn = new FileInfo(Path.Combine(inputDirPath, filename));
            
            toReturn.Directory.Create();

            File.WriteAllBytes(toReturn.FullName, bytes);

            return toReturn;
        }
        /// <summary>
        /// Runs the <see cref="DicomRepopulatorProcessor"/> with the provided <paramref name="inputCsv"/> etc.  Asserts that there
        /// are no errors during the run and th
        /// </summary>
        /// <param name="expectedDone"></param>
        /// <param name="inputCsv"></param>
        /// <param name="inputExtraMapping"></param>
        /// <param name="inputDicomDirectory"></param>
        /// <param name="memberName"></param>
        /// <returns></returns>
        private DirectoryInfo AssertRunsSuccesfully(int expectedDone, FileInfo inputCsv, FileInfo inputExtraMapping,
            DirectoryInfo inputDicomDirectory,
            Action<DicomRepopulatorOptions> adjustOptions = null,
            [System.Runtime.CompilerServices.CallerMemberName]
            string memberName = "")
        {
            string outputDirPath = Path.Combine(_outputFileBase, memberName);

            Directory.CreateDirectory(outputDirPath);
            Directory.Delete(outputDirPath,true);

            var options = new DicomRepopulatorOptions
            {
                InputCsv = inputCsv?.FullName,
                InputFolder = inputDicomDirectory?.FullName,
                InputExtraMappings = inputExtraMapping?.FullName,
                OutputFolder = outputDirPath,
                NumThreads = 4
            };

            adjustOptions?.Invoke(options);

            var processor = new DicomRepopulatorProcessor(TestContext.CurrentContext.TestDirectory);
            int result = processor.Process(options);
            Assert.AreEqual(0, result);

            Assert.AreEqual(expectedDone,processor.Done);
            Assert.AreEqual(0,processor.Errors,"There were non fatal errors, examine processor.MemoryLogTarget for details");

            return new DirectoryInfo(outputDirPath);
        }
    }
}
