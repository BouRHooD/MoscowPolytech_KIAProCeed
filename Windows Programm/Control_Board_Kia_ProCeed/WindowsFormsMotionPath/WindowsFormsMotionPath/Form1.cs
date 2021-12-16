using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsMotionPath
{
    public partial class MainForm : Form
    {
        // Диаметер окружностей Вектора
        float CircleDiameterOfVector = 50;
        ObservableCollection<Vector> VectorsCollection = new ObservableCollection<Vector>();

        public MainForm()
        {
            InitializeComponent();
            preLoadForm();
        }

        private void preLoadForm()
        {
            try
            {
                pictureBoxDraw.MouseMove += new MouseEventHandler(OnPictureBoxDrawMouseMove);
                buttonClearPictureBox.Click += new EventHandler(OnClickClear);
                buttonDrawVectors.Click += new EventHandler(OnClickDrawVectors);
                buttonDrawPath.Click += new EventHandler(OnClickDrawPath);
                buttonDrawWay.Click += new EventHandler(OnClickDrawWay);

            }
            catch (Exception ex)
            {
                MessageBox.Show(text: ex.Message, caption: "Программная ошибка");
            }
        }

        private void OnClickDrawWay(object sender, EventArgs e)
        {
            OnClickClear(null, null);
            OnClickDrawVectors(null, null);

            if (VectorsCollection.Count <= 0)
                return;

            Bitmap myBitmap = new Bitmap(pictureBoxDraw.Image, pictureBoxDraw.Width, pictureBoxDraw.Height);
            Graphics fig = Graphics.FromImage(myBitmap);

            for (int indexVector = 0; indexVector < VectorsCollection.Count - 1; indexVector++)
            {
                var circleVector1 = VectorsCollection[indexVector];
                var circleVector2 = VectorsCollection[indexVector + 1];
                DrawFigures.StartDrawIntersectionCircles(fig, circleVector1, circleVector2, CircleDiameterOfVector, DrawAllIntersection: false);
            }

            pictureBoxDraw.Image = myBitmap;
        }

        private void OnClickDrawPath(object sender, EventArgs e)
        {
            if (VectorsCollection.Count <= 0)
                return;

            Bitmap myBitmap = new Bitmap(pictureBoxDraw.Image, pictureBoxDraw.Width, pictureBoxDraw.Height);
            Graphics fig = Graphics.FromImage(myBitmap);

            for (int indexVector = 0; indexVector < VectorsCollection.Count - 1; indexVector++)
            {
                var circleVector1 = VectorsCollection[indexVector];
                var circleVector2 = VectorsCollection[indexVector + 1];
                DrawFigures.StartDrawIntersectionCircles(fig, circleVector1, circleVector2, CircleDiameterOfVector);
            }

            pictureBoxDraw.Image = myBitmap;
        }
        
        private void OnClickDrawVectors(object sender, EventArgs e)
        {
            Bitmap myBitmap = new Bitmap(pictureBoxDraw.Width, pictureBoxDraw.Height);
            Graphics fig = Graphics.FromImage(myBitmap);


            VectorsCollection.Add(new Vector { StartPoint = new PointF(400, 300), EndPoint = new PointF(400, 350) });
            VectorsCollection.Add(new Vector { StartPoint = new PointF(200, 200), EndPoint = new PointF(250, 150) });
            VectorsCollection.Add(new Vector { StartPoint = new PointF(300, 300), EndPoint = new PointF(250, 350) });
            //VectorsCollection.Add(new Vector { StartPoint = new PointF(400, 100), EndPoint = new PointF(450, 150) });
            VectorsCollection.Add(new Vector { StartPoint = new PointF(100, 100), EndPoint = new PointF(150, 100) });

            var VectorsEnumarable = VectorsCollection.Where(i => VectorsCollection.IndexOf(i) < numUpDownCountVectors.Value);
            VectorsCollection = new ObservableCollection<Vector>(VectorsEnumarable);

            foreach (var item in VectorsCollection)
            {
                // Рисуем вектор с стелкой направления
                DrawFigures.DrawArrow(fig, new Pen(Color.Black, 4), item.StartPoint, item.EndPoint, 10);
                // Рисуем вокруг векторов окружности
                DrawFigures.DrawElipsesOnVector(fig, new Pen(Color.Black, 1), item.StartPoint, item.EndPoint, CircleDiameterOfVector);
            }

            pictureBoxDraw.Image = myBitmap;
        }

        private void OnClickClear(object sender, EventArgs e)
        {
            if (pictureBoxDraw.Image != null)
            {
                pictureBoxDraw.Image.Dispose();
                pictureBoxDraw.Image = null;
                VectorsCollection.Clear();
            }
        }

        private void OnPictureBoxDrawMouseMove(object sender, MouseEventArgs e)
        {
            int X = e.X;
            int Y = e.Y;
            labelXY.Text = "Координаты:" + X + ":" + Y;
        }

        private void buttonDrawVectors_Click(object sender, EventArgs e)
        {
            return;
        }

        private void buttonDrawWay_Click(object sender, EventArgs e)
        {
            return;
        }
    }

    public class Vector
    {
        public PointF StartPoint { get; set; }
        public PointF EndPoint { get; set; }

        private PointF _centerVector;
        public PointF CenterVector
        {
            get 
            {
                var centerVectorX = (StartPoint.X + EndPoint.X) / 2;
                var centerVectorY = (StartPoint.Y + EndPoint.Y) / 2;
                _centerVector = new PointF(centerVectorX, centerVectorY);
                return _centerVector; 
            }
        }
    }
}
