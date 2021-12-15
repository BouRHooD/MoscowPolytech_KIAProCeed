using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RoboCross2018
{
    public partial class Form2 : Form
    {
        Form1 MainForm;

        Bitmap BackgroundMap;

        public Form2()
        {
            InitializeComponent();
        }

        public Form2(Form1 f)
        {
            InitializeComponent();
            MainForm = f;
        }

        private void Form2_Shown(object sender, EventArgs e)
        {
            this.Left = 0;
            this.Top = 0;
        }

        private void SelectBackgroundMap(int MapIndex)
        {
            if (BackgroundMap != null) // Фоновое изображение - уничтожаем, если оно есть
            {
                BackgroundMap.Dispose();
                BackgroundMap = null;
            }
            if ((MapIndex >= 0) && (MapIndex < SelectBackgoundMapComboBox.Items.Count))  // Же
            {
                try
                {
                    string sfile = "";
                    switch (MapIndex) // настраиваемся в зависимости от индекса
                    {
                        case 1: sfile = "map2.bmp";
                                break;
                        case 2: sfile = "map3.bmp";
                                break;
                        default:sfile = "map1.bmp";
                                break;
                    }
                    BackgroundMap = new Bitmap(sfile);

                }
                catch 
                {
                    BackgroundMap = new Bitmap(750,500);
                }
                
            }
            else
            {

            }
        }

    }
}
