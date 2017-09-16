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
    public partial class BedName : Form
    {
        private string bedNameLocation;

        public BedName()
        {
            InitializeComponent();
        }

        public BedName(string fileName)
        {
            InitializeComponent();
            bedNameLocation = fileName;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void buttonSet_Click(object sender, EventArgs e)
        {
            using (StreamWriter sw = File.CreateText(bedNameLocation))
            {
                sw.WriteLine(textBedName.Text);
            }
            this.Close();
        }
    }
}
