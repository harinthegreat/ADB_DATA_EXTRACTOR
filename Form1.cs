using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using iTextSharp.text;
using iTextSharp.text.pdf;
//using Xceed.Words.NET;
using System.IO;
using System.Xml.Linq;
//using Novacode;


namespace ADBToolKit_1
{
    public partial class ADB_ToolKit : Form
    {
        private string selectedSection = "";
        private string connectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\harin\OneDrive\文件\ADB_Data_Extraction.mdf;Integrated Security=True;Connect Timeout=30";

        public ADB_ToolKit()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            selectedSection = "Contacts";
            MessageBox.Show("Contacts selected.");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            selectedSection = "Messages";
            MessageBox.Show("Messages selected.");
        }

        private void button3_Click(object sender, EventArgs e)
        {
            selectedSection = "CallLogs";
            MessageBox.Show("Call Logs selected.");
        }

        private void button4_Click(object sender, EventArgs e)
        {
            selectedSection = "DeviceInfo";
            MessageBox.Show("Device Info selected.");
        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (selectedSection == "")
            {
                MessageBox.Show("Please select a section first.");
                return;
            }

            string output = "";

            switch (selectedSection)
            {
                case "Contacts":
                    output = RunAdbCommand("shell content query --uri content://contacts/phones/ --projection display_name:number");
                    break;
                case "Messages":
                    output = RunAdbCommand("shell content query --uri content://sms/ --projection address:body:date");
                    break;
                case "CallLogs":
                    output = RunAdbCommand("shell content query --uri content://call_log/calls/ --projection number:type:duration");
                    break;
                case "DeviceInfo":
                    string cpu = RunAdbCommand("shell cat /proc/cpuinfo");
                    string mem = RunAdbCommand("shell cat /proc/meminfo");
                    output = $"CPU Info:\n{cpu}\n\nMemory Info:\n{mem}";
                    break;
            }

            MessageBox.Show($"Top Results:\n{TruncateOutput(output, 5)}");

        }

        private void button6_Click(object sender, EventArgs e)
        {
            if (selectedSection == "")
            {
                MessageBox.Show("Please select a section first.");
                return;
            }

            switch (selectedSection)
            {
                case "DeviceInfo":
                    SaveDeviceInfoToDb();
                    break;

                case "CallLogs":
                    SaveCallLogsToDb();
                    break;

                case "Messages":
                    SaveMessagesToDb();
                    break;

                case "Contacts":
                    SaveContactsToDb();
                    break;
            }

            MessageBox.Show($"{selectedSection} saved to database.");
        }

        private void button7_Click(object sender, EventArgs e)
        {
            /*string summary = "";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                SqlCommand countContacts = new SqlCommand("SELECT COUNT(*) FROM Contacts", conn);
                SqlCommand countMessages = new SqlCommand("SELECT COUNT(*) FROM Msgs", conn);
                SqlCommand countCalls = new SqlCommand("SELECT COUNT(*) FROM CallLog", conn);
                SqlCommand topCalls = new SqlCommand("SELECT TOP 5 phonenum, duration FROM CallLog ORDER BY ID DESC", conn);
                SqlCommand topMsgs = new SqlCommand("SELECT TOP 5 sender, content FROM Msgs ORDER BY ID DESC", conn);
                SqlCommand deviceInfo = new SqlCommand("SELECT TOP 1 cpuinfo,memoryinfo FROM DeviceInfo ORDER BY ID DESC", conn);

                summary += $"Total Contacts: {countContacts.ExecuteScalar()}\n";
                summary += $"Total Messages: {countMessages.ExecuteScalar()}\n";
                summary += $"Total Calls: {countCalls.ExecuteScalar()}\n\n";

                summary += "Top 5 Calls:\n";
                SqlDataReader reader = topCalls.ExecuteReader();
                while (reader.Read())
                    summary += $"- {reader[0]} ({reader[1]}s)\n";
                reader.Close();

                summary += "\nTop 5 Messages:\n";
                reader = topMsgs.ExecuteReader();
                while (reader.Read())
                    summary += $"- {reader[0]}: {reader[1]}\n";
                reader.Close();

                summary += "\nDevice Info:\n";
                reader = deviceInfo.ExecuteReader();
                if (reader.Read())
                    summary += $"CPU:\n{reader[0]}\n\nMemory:\n{reader[1]}\n";
                reader.Close();
            }+*/
            

            GenerateReport(true);
            //MessageBox.Show(summary, "Report Summary");
        }

        private string RunAdbCommand(string arguments)
        {
            ProcessStartInfo psi = new ProcessStartInfo("adb", arguments)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process p = Process.Start(psi))
            {
                return p.StandardOutput.ReadToEnd();
            }
        }

        private string TruncateOutput(string raw, int lines)
        {
            var split = raw.Split('\n');
            return string.Join("\n", split, 0, Math.Min(lines, split.Length));
        }

        private void SaveDeviceInfoToDb()
        {
            string cpu = RunAdbCommand("shell cat /proc/cpuinfo");
            string mem = RunAdbCommand("shell cat /proc/meminfo");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("INSERT INTO DeviceInfo (cpuinfo, memoryinfo) VALUES (@cpu, @mem)", conn);
                cmd.Parameters.AddWithValue("@cpu", cpu);
                cmd.Parameters.AddWithValue("@mem", mem);
                cmd.ExecuteNonQuery();
            }
        }

        private void SaveCallLogsToDb()
        {
            string data = RunAdbCommand("shell content query --uri content://call_log/calls/ --projection number:type:duration");
            var entries = data.Split(new[] { "Row " }, StringSplitOptions.RemoveEmptyEntries);

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                foreach (var entry in entries)
                {
                    string number = ExtractField(entry, "number=");
                    string type = ExtractField(entry, "type=");
                    string duration = ExtractField(entry, "duration=");

                    SqlCommand cmd = new SqlCommand("INSERT INTO CallLog (phonenum, calltype, duration) VALUES (@num, @type, @duration)", conn);
                    cmd.Parameters.AddWithValue("@num", number);
                    cmd.Parameters.AddWithValue("@type", GetCallTypeName(type));
                    cmd.Parameters.AddWithValue("@duration", duration);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void SaveMessagesToDb()
        {
            string data = RunAdbCommand("shell content query --uri content://sms/ --projection address:body:date");
            var entries = data.Split(new[] { "Row " }, StringSplitOptions.RemoveEmptyEntries);

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                foreach (var entry in entries)
                {
                    string address = ExtractField(entry, "address=");
                    string body = ExtractField(entry, "body=");
                    string date = ExtractField(entry, "date=");

                    SqlCommand cmd = new SqlCommand("INSERT INTO Msgs (sender, content, timestamp) VALUES (@addr, @body, @date)", conn);
                    cmd.Parameters.AddWithValue("@addr", address);
                    cmd.Parameters.AddWithValue("@body", body);
                    cmd.Parameters.AddWithValue("@date", ConvertTimestampToDateTime(date));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void SaveContactsToDb()
        {
            string data = RunAdbCommand("shell content query --uri content://contacts/phones/ --projection display_name:number");
            var entries = data.Split(new[] { "Row " }, StringSplitOptions.RemoveEmptyEntries);

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                foreach (var entry in entries)
                {
                    string name = ExtractField(entry, "display_name=");
                    string phone = ExtractField(entry, "number=");

                    SqlCommand cmd = new SqlCommand("INSERT INTO Contacts (name, phonenum) VALUES (@name, @num)", conn);
                    cmd.Parameters.AddWithValue("@name", name);
                    cmd.Parameters.AddWithValue("@num", phone);
                    cmd.ExecuteNonQuery();
                }
            }

        }

        private string ExtractField(string entry, string fieldName)
        {
            var lines = entry.Split('\n');
            foreach (var line in lines)
            {
                if (line.Trim().StartsWith(fieldName))
                {
                    return line.Trim().Substring(fieldName.Length).Trim();
                }
            }
            return "";
        }

        private string GetCallTypeName(string typeCode)
        {
            if (typeCode == "1") return "Incoming";
            if (typeCode == "2") return "Outgoing";
            if (typeCode == "3") return "Missed";
            return "Unknown";
        }

        private string ConvertTimestampToDateTime(string timestamp)
        {
            if (long.TryParse(timestamp, out long millis))
            {
                DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(millis);
                return dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss");
            }
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private void GenerateReport(bool asPdf)
        {
            string contacts = "", messages = "", calls = "", deviceInfo = "";
            
            string contactsRaw = RunAdbCommand("shell content query --uri content://contacts/phones/ --projection display_name:number");
            var contactEntries = contactsRaw.Split(new[] { "Row " }, StringSplitOptions.RemoveEmptyEntries)
                .Select(entry => entry.Trim())
                .Where(entry => !string.IsNullOrEmpty(entry))
                .ToArray();
            contacts = $"Total Contacts: {contactEntries.Length}\n\nFirst 5 Contacts:\n";
            contacts += string.Join("\n", contactEntries.Take(5).Select(e =>
                $"{ExtractField(e, "display_name=")} - {ExtractField(e, "number=")}"));

            string msgsRaw = RunAdbCommand("shell content query --uri content://sms/ --projection address:body:date");
            var msgEntries = msgsRaw.Split(new[] { "Row " }, StringSplitOptions.RemoveEmptyEntries);
            messages = $"Total Messages: {msgEntries.Length}\n\nTop 5 Messages:\n";
            var topMsgs = msgEntries.OrderByDescending(m => ExtractField(m, "date=")).Take(5);
            foreach (var m in topMsgs)
            {
                string sender = ExtractField(m, "address=");
                string content = ExtractField(m, "body=");
                messages += $"- {sender}: {content}\n";
            }

            string callRaw = RunAdbCommand("shell content query --uri content://call_log/calls/ --projection number:type:duration");
            var callEntries = callRaw.Split(new[] { "Row " }, StringSplitOptions.RemoveEmptyEntries);
            calls = $"Total Calls: {callEntries.Length}\n\nTop 5 Calls:\n";
            foreach (var c in callEntries.Take(5))
            {
                string number = ExtractField(c, "number=");
                string type = GetCallTypeName(ExtractField(c, "type="));
                string duration = ExtractField(c, "duration=");
                calls += $"- {number} [{type}] - {duration}s\n";
            }

            string cpu = RunAdbCommand("shell cat /proc/cpuinfo");
            string mem = RunAdbCommand("shell cat /proc/meminfo");
            deviceInfo = "Device Info:\n\nCPU:\n" + cpu + "\nMemory:\n" + mem;

            string reportText = $"{contacts}\n\n{messages}\n\n{calls}\n\n{deviceInfo}";
            SaveReportAsPdf(reportText);
            
        }

        private void SaveReportAsPdf(string reportText)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                Title = "Save Report as PDF"
            };

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                using (FileStream stream = new FileStream(saveFileDialog.FileName, FileMode.Create))
                {
                    Document pdfDoc = new Document(PageSize.A4, 25, 25, 30, 30);
                    PdfWriter writer = PdfWriter.GetInstance(pdfDoc, stream);
                    pdfDoc.Open();
                    pdfDoc.Add(new Paragraph("ADB Data Extraction Report", FontFactory.GetFont("Arial", 16)));
                    pdfDoc.Add(new Paragraph("\n"));
                    pdfDoc.Add(new Paragraph(reportText, FontFactory.GetFont("Courier", 10)));
                    pdfDoc.Close();
                    writer.Close();
                }
                MessageBox.Show("PDF Report saved successfully.");
            }
        }



    }
}

