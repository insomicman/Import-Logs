using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace ImportLogs
{
    public partial class SettingsScreen : Form
    {
        private string settingsFile = String.Format(@"{0}\settings.txt", Directory.GetCurrentDirectory());

        public SettingsScreen()
        {
            InitializeComponent();
            StreamReader file = new StreamReader(settingsFile);
            textServer.Text = file.ReadLine();
            textDatabase.Text = file.ReadLine();
            textUser.Text = file.ReadLine();
            textPassword.Text = file.ReadLine();
            file.Close();
        }

        private void Form2_Load(object sender, EventArgs e)
        {

        }

        private void textServer_TextChanged(object sender, EventArgs e)
        {

        }

        private void textDatabase_TextChanged(object sender, EventArgs e)
        {

        }

        private void textUser_TextChanged(object sender, EventArgs e)
        {

        }

        private void textPassword_TextChanged(object sender, EventArgs e)
        {

        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            using (StreamWriter sw = File.CreateText(settingsFile))
            {
                sw.WriteLine(textServer.Text);
                sw.WriteLine(textDatabase.Text);
                sw.WriteLine(textUser.Text);
                sw.WriteLine(textPassword.Text);
            }
            this.Close();
        }
    }
}
