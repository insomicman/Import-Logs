using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SevenZipLib;
using System.Data.SqlClient;
using MySql.Data.MySqlClient;
using System.Threading;

namespace ImportLogs
{
    public partial class MainScreen : Form
    {

        private StringBuilder sb = new StringBuilder();
        private string settingsFile = String.Format(@"{0}\settings.txt", Directory.GetCurrentDirectory());
        private int primaryKey = 1;
        private string foldername;
        private string archivedFolder;
        private MySqlConnection conn;
        private string loadState = "";
        private string bedID = "";

        public MainScreen()
        {
            InitializeComponent();
            backgroundWorker1.ProgressChanged += new ProgressChangedEventHandler(backgroundWorker1_ProgressChanged);
            backgroundWorker1.RunWorkerCompleted += new RunWorkerCompletedEventHandler(backroundWorker1_RunWorkerCompleted);
            backgroundWorker1.WorkerReportsProgress = true;
            if (!File.Exists(settingsFile))
            {
                using (StreamWriter sw = File.CreateText(settingsFile))
                {
                    sw.WriteLine("localhost");
                    sw.WriteLine("nexthealth");
                    sw.WriteLine("root");
                    sw.WriteLine("");
                    sw.Close();
                }
            }
        }

        #region Folder Button
        private void button1_Click(object sender, EventArgs e)
        {
            try{conn = Connect();}
            catch { return; }

            DialogResult result = this.folderBrowserDialog1.ShowDialog();
            foldername = this.folderBrowserDialog1.SelectedPath + "\\log_files";
            string bedIDFile = foldername + "\\BedID.txt";
            if (result == DialogResult.OK && Path.GetFileName(foldername) == "log_files")
            {
                if (!File.Exists(bedIDFile) || new FileInfo(bedIDFile).Length == 0)
                {
                    BedName bedNameScreen = new BedName(bedIDFile);
                    bedNameScreen.ShowDialog();
                }
                backgroundWorker1.RunWorkerAsync();
                conn.Close();
            }
        }
        #endregion

        #region File Button
        private void button2_Click(object sender, EventArgs e)
        {
            MySqlConnection conn = Connect();

            DialogResult result = this.openFileDialog1.ShowDialog();
            if (result == DialogResult.OK)
            {
                string filename = this.openFileDialog1.FileName;
                string foldername = Path.GetDirectoryName(filename);


                string[] filenameParts = Path.GetFileNameWithoutExtension(filename).Split(new Char[] { ' ' }, 3);
                string prefix = filenameParts[0] + filenameParts[1];

                string dumpFolder = extract(filename, foldername, prefix);

                if (checkBox1.Checked)
                {
                    string archivedFolder = dumpFolder + "\\archived_log_files";
                    string subFolder = extract(archivedFolder);
                    if (checkBox2.Checked)
                    {
                        run(conn, "event", true, true, dumpFolder, subFolder, prefix);
                        run(conn, "alarm", true, true, dumpFolder, subFolder, prefix);
                        run(conn, "counter", true, true, dumpFolder, subFolder, prefix);
                        run(conn, "hourly", true, true, dumpFolder, subFolder, prefix);
                        run(conn, "seq", true, true, dumpFolder, subFolder, prefix);
                    }
                    else
                    {
                        run(conn, "event", true, false, dumpFolder, subFolder, prefix);
                        run(conn, "alarm", true, false, dumpFolder, subFolder, prefix);
                        run(conn, "counter", true, false, dumpFolder, subFolder, prefix);
                        run(conn, "hourly", true, false, dumpFolder, subFolder, prefix);
                        run(conn, "seq", true, false, dumpFolder, subFolder, prefix);
                    }
                }
                else if (checkBox2.Checked)
                {
                    run(conn, "event", false, true, dumpFolder, "", prefix);
                    run(conn, "alarm", false, true, dumpFolder, "", prefix);
                    run(conn, "counter", false, true, dumpFolder, "", prefix);
                    run(conn, "hourly", false, true, dumpFolder, "", prefix);
                    run(conn, "seq", false, true, dumpFolder, "", prefix);
                }
                else
                {
                    run(conn, "event", false, false, dumpFolder);
                    run(conn, "alarm", false, false, dumpFolder);
                    run(conn, "counter", false, false, dumpFolder);
                    run(conn, "hourly", false, false, dumpFolder);
                    run(conn, "seq", false, false, dumpFolder);
                }
            }
        }
        #endregion

        #region Combine CSV Files
        private string combine(string prefix, string temp, string table, bool archive = false, bool database = false)
        {
            string csvDir;
            string outPath;

            StreamReader file = new StreamReader(foldername + "\\BedID.txt");

            bedID = file.ReadLine();

            if (archive == true)
            {
                DirectoryInfo dir = new DirectoryInfo(temp);
                csvDir = temp + "\\media\\sda1\\log_files";
                outPath = Path.Combine(Directory.GetParent(temp).Parent.FullName.ToString(), table + ".csv");
            }
            else
            {
                csvDir = temp;
                outPath = Path.Combine(temp, table + ".csv");
            }

            DirectoryInfo csvInfo = new DirectoryInfo(csvDir);
            FileInfo[] csvFiles = csvInfo.GetFiles("*" + table + "*.csv");

            foreach (FileInfo csvFile in csvFiles)
                using (StreamReader sr = new StreamReader(csvFile.OpenRead()))
                {
                    sr.ReadLine(); // Discard header line
                    while (!sr.EndOfStream)
                    {
                        if (database)
                        {
                            string line = bedID + "-" + table[0] + "-" + primaryKey + "," + sr.ReadLine(); //+ "-" + table[0] + "-" + primaryKey + 
                            sb.AppendLine(line);
                            if (sb.Length > 50000)
                            {
                                flush(outPath);
                            }
                        }
                        else
                        {
                            sb.AppendLine(sr.ReadLine());
                            if (sb.Length > 50000)
                            {
                                flush(outPath);
                            }
                        }
                        primaryKey++;
                    }
                }
            
            flush(outPath);
            return outPath;
        }
        #endregion

        #region Flush Function (To be implemented in future if memory overhead too great)
        private void flush(string outPath)
        {
            File.AppendAllText(outPath, sb.ToString());
            sb.Clear();
        }
        #endregion

        #region Bulk Insert Method For Database
        private void bulkInsert(MySqlConnection conn, string filename, string table)
        {
            //string csvFile = @"";
            MySqlBulkLoader bulkLoader = new MySqlBulkLoader(conn);
            try
            {
                bulkLoader.Timeout = 10 * 60; //seconds
                bulkLoader.TableName = "nexthealth." + table + "Log";
                bulkLoader.Local = true;
                bulkLoader.LineTerminator = @"\n";
                bulkLoader.FileName = filename;
                bulkLoader.FieldTerminator = ",";
                bulkLoader.Load();
            }
            catch{}
        }
        #endregion

        #region 7zip Extraction For All Files In Folder
        private string extract(string currentDir, string prefix = "Temp")
        {
            string newFolderName = Path.Combine(currentDir, prefix);
            Directory.CreateDirectory(newFolderName);

            double incrementAmount = 100.0 / Directory.GetFiles(currentDir).Length;
            int progress = 0;

            foreach (string f in Directory.GetFiles(currentDir))
            {
                using (SevenZipArchive archive = new SevenZipArchive(f))
                {
                    archive.ExtractAll(newFolderName);
                }

                int test = Convert.ToInt32(incrementAmount * progress);
                backgroundWorker1.ReportProgress(test);

                progress++;

            }   
            return newFolderName;
        }
        #endregion

        #region 7zip Extraction For Single File
        private string extract(string filename, string currentDir, string prefix)
        {
            string newFolderName = Path.Combine(currentDir, prefix);
            Directory.CreateDirectory(newFolderName);
            using (SevenZipArchive archive = new SevenZipArchive(filename))
            {
                archive.ExtractAll(newFolderName);
            }
            string subDir = Directory.GetDirectories(newFolderName)[0] + "\\log_files";
            return subDir;
        }
        #endregion

        #region Database Connection
        private MySqlConnection Connect()
        {
            StreamReader file = new StreamReader(settingsFile);
            string server = file.ReadLine();
            string database = file.ReadLine();
            string uid = file.ReadLine();
            string password = file.ReadLine();
            file.Close();

            string connectionString = "SERVER=" + server + ";" + "DATABASE=" +
            database + ";" + "UID=" + uid + ";" + "PASSWORD=" + password + ";";

            MySqlConnection conn = new MySqlConnection(connectionString);

            conn.Open();

            return conn;
        }
        #endregion

        #region Main Function
        private void run(MySqlConnection conn, string table, bool archive, bool database, string newFolder, string archiveFolder = "", string prefix = "")
        {
            if (archive)
            {
                if (database)
                {
                    string csvLocation = combine(prefix, newFolder, table, false, true);
                    combine(prefix, archiveFolder, table, true, true);
                    bulkInsert(conn, csvLocation, table);
                    primaryKey = 1;
                }
                else
                {
                    combine(prefix, newFolder, table, false, false);
                    combine(prefix, archiveFolder, table, true, false);
                    primaryKey = 1;
                }
            }
            else if (database)
            {
                string csvLocation = combine(prefix, newFolder, table, false, true);
                bulkInsert(conn, csvLocation, table);
                primaryKey = 1;
            }
            else
            {
                combine(prefix, newFolder, table, false, false);
                primaryKey = 1;
            }
        }
        #endregion

        #region Cleanup Functions
        private void cleanup(string logFolder)
        {
            File.Delete(logFolder + "\\alarm.csv");
            File.Delete(logFolder + "\\event.csv");
            File.Delete(logFolder + "\\seq.csv");
            File.Delete(logFolder + "\\counter.csv");
            File.Delete(logFolder + "\\hourly.csv");
            File.Delete(logFolder + "\\transfer.csv");
        }

        private void cleanup(string tempFolder, string logFolder)
        {
            Directory.Delete(tempFolder, true);
            File.Delete(logFolder + "\\alarm.csv");
            File.Delete(logFolder + "\\event.csv");
            File.Delete(logFolder + "\\seq.csv");
            File.Delete(logFolder + "\\counter.csv");
            File.Delete(logFolder + "\\hourly.csv");
            File.Delete(logFolder + "\\transfer.csv");
        }
        #endregion

        #region Backround Processes
        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            if (checkBox1.Checked)
            {
                archivedFolder = foldername + "\\archived_log_files";
                string subFolder = archivedFolder + "\\Temp";

                loadState = "Extracting: ";
                extract(archivedFolder);
                
                if (checkBox2.Checked)
                {
                    run(conn, "event", true, true, foldername, subFolder);
                    loadState = "Loading to Database: ";
                    backgroundWorker1.ReportProgress(20);
                    run(conn, "alarm", true, true, foldername, subFolder);
                    backgroundWorker1.ReportProgress(40);
                    run(conn, "counter", true, true, foldername, subFolder);
                    backgroundWorker1.ReportProgress(60);
                    run(conn, "hourly", true, true, foldername, subFolder);
                    backgroundWorker1.ReportProgress(80);
                    run(conn, "seq", true, true, foldername, subFolder);
                    backgroundWorker1.ReportProgress(100);
                    loadState = "Calculating Transfers: ";
                    TransferParser();
                    loadState = "Cleaning Up: ";
                    cleanup(subFolder, foldername);
                }
                else
                {
                    run(conn, "event", true, false, foldername, subFolder);
                    backgroundWorker1.ReportProgress(20);
                    run(conn, "alarm", true, false, foldername, subFolder);
                    backgroundWorker1.ReportProgress(40);
                    run(conn, "counter", true, false, foldername, subFolder);
                    backgroundWorker1.ReportProgress(60);
                    run(conn, "hourly", true, false, foldername, subFolder);
                    backgroundWorker1.ReportProgress(80);
                    run(conn, "seq", true, false, foldername, subFolder);
                    backgroundWorker1.ReportProgress(100);
                    loadState = "Calculating Transfers: ";
                    TransferParser();
                    loadState = "Cleaning Up: ";
                    cleanup(subFolder, foldername);
                }
            }
            else if (checkBox2.Checked)
            {
                run(conn, "event", false, true, foldername);
                backgroundWorker1.ReportProgress(20);
                run(conn, "alarm", false, true, foldername);
                backgroundWorker1.ReportProgress(40);
                run(conn, "counter", false, true, foldername);
                backgroundWorker1.ReportProgress(60);
                run(conn, "hourly", false, true, foldername);
                backgroundWorker1.ReportProgress(80);
                run(conn, "seq", false, true, foldername);
                backgroundWorker1.ReportProgress(100);
                loadState = "Calculating Transfers: ";
                TransferParser();
                loadState = "Cleaning Up: ";
                cleanup(foldername);
            }
            else
            {
                run(conn, "event", false, false, foldername);
                backgroundWorker1.ReportProgress(20);
                run(conn, "alarm", false, false, foldername);
                backgroundWorker1.ReportProgress(40);
                run(conn, "counter", false, false, foldername);
                backgroundWorker1.ReportProgress(60);
                run(conn, "hourly", false, false, foldername);
                backgroundWorker1.ReportProgress(80);
                run(conn, "seq", false, false, foldername);
                backgroundWorker1.ReportProgress(100);
                loadState = "Calculating Transfers: ";
                TransferParser();
                loadState = "Cleaning Up: ";
                cleanup(foldername);
            }            
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            // Change the value of the ProgressBar to the BackgroundWorker progress. 
            progressBar1.Value = e.ProgressPercentage;
            label1.Text = loadState + e.ProgressPercentage + "% Complete";
        }

        private void backroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            label1.Text = "Complete!";
        }

        #endregion

        #region Parser Used to Calculate Complete Transfers
        private void TransferParser()
        {
            int transfers = 0;
            int transferState = -1;
            List<string> transferTimes = new List<string>();
            string csvLoc = foldername + "\\seq.csv";

            char[] delimiters = new char[] { ',' };
            using (StreamReader reader = new StreamReader(csvLoc))
            {
                while (true)
                {
                    string line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }
                    string[] parts = line.Split(delimiters);

                    if (transferState == -1 && parts[13] == "0" && parts[22] == "To Bed")
                    {
                        transferState = 0;
                    }
                    else if (transferState == 0 && parts[13] == "1" && parts[22] == "To Bed")
                    {
                        transferState = 1;
                    }
                    else if (transferState == 1 && parts[12] == "0" && parts[22] == "To Bed")
                    {
                        transferState = 2;
                    }
                    else if (transferState == 2 && parts[3] == "1" && parts[22] == "To Bed")
                    {
                        transferState = 3;
                    }
                    else if (transferState == 3 && parts[12] == "1" && parts[22] == "To Bed")
                    {
                        transfers++;
                        transferState = -1;
                        transferTimes.Add(parts[1]);
                    }

                    //Console.WriteLine(parts[0] + "," + parts[12]);
                }
                Console.WriteLine(transfers);
                foreach (string time in transferTimes)
                {
                    sb.AppendLine(bedID + "," + time + "," + "To Bed");
                    Console.WriteLine(time);
                }
                string transferCsvLoc = foldername + "\\transfer.csv";
                File.AppendAllText(transferCsvLoc, sb.ToString());
                bulkInsert(conn, transferCsvLoc, "Transfer");
            }
        }
        #endregion

        #region Additional Functions
        private void openFileDialog1_FileOk(object sender, CancelEventArgs e){}

        private void folderBrowserDialog1_HelpRequest(object sender, EventArgs e){}

        private void checkBox1_CheckedChanged(object sender, EventArgs e){}

        private void checkBox2_CheckedChanged(object sender, EventArgs e){ }

        private void progressBar1_Click(object sender, EventArgs e){ }

        private void textBox1_TextChanged(object sender, EventArgs e){}

        private void Form1_Load(object sender, System.EventArgs e){}

        private void Form1_Load_1(object sender, EventArgs e){ }
        #endregion

        private void settings_Click(object sender, EventArgs e)
        {
            SettingsScreen serverOps = new SettingsScreen();
            serverOps.Show();
        }

    }
}
