using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.IO.Ports;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Globalization;

using SharpDX.DirectInput;

using SCIP_library;

using MyIniTestApplication;

namespace RoboCross2018
{
    public struct MiniMapMissionPoint // структура для отображении миникарты
    {
        public long pointid; // идентификатор путевой точки
        public int pointtype; // тип путевой точки - 0 (нет точки), 1 - обычная точка, 2 - точка S
        public int task; // задача, связана с локацией на квалификации "Зимнего города"  (алгоритм спецобработки)
        public double longitude; // долгота
        public double lattitude; // широта
        public double cource; // курс
        public double pointoffset; // выступание точки стремления вперед (в метрах)
        public double pointshift; // сдвиг точки стремления вправо (в метрах) или влево (при отрицательных значениях)
    }

    public struct MiniMapRotateMaitix  // матрица трансляции и поворота для миникарты
    {
        public double m11, m12;  //   cos T    -sin T
        public double m21, m22;  //   sin T     cos T
    }

    public struct VectorMapBaseElement // элемент векторной карты (отрезок)
    {
        public double longitude1; // долгота1
        public double lattitude1; // широта1
        public double longitude2; // долгота2
        public double lattitude2; // широта2
        public int colorid; // идентификатор цвета: 1 - красная, 2 - желтая, 3 - зеленая, 4 - синяя
        public int objectid; // идентификатор объекта: если проводится группировка
    }

    public partial class Form1 : Form
    {

        // UDP
        bool aliveSteering = false, alivePropulsive = false, aliveEncoders = false; // будет ли работать поток для приема

        UdpClient SteeringUdpClient, PropulsiveUdpClient, EncodersUdpClient; // = new UdpClient(11000);
        Thread SteeringThread, PropulsiveThread, EncodersThread;

        IPAddress SteeringRemoteIP;    // хост для отправки данных на руль
        int SteeringRemotePort;     // порт для отправки данных на руль
        int SteeringLocalPort;      // локальный порт для прослушивания входящих подключений от руля

        IPAddress PropulsiveRemoteIP;    // хост для отправки данных
        int PropulsiveRemotePort;     // порт для отправки данных
        int PropulsiveLocalPort;      // локальный порт для прослушивания входящих подключений

        IPAddress EncodersRemoteIP;    // хост для отправки данных
        int EncodersRemotePort;     // порт для отправки данных
        int EncodersLocalPort;      // локальный порт для прослушивания входящих подключений

        // RTK
        string UDPReceiveBuffer = "";

        string remoteAddress; // хост для отправки данных
        int remotePort; // порт для отправки данных
        int localPort; // локальный порт для прослушивания входящих подключений

        public delegate void ShowUDPMessage(string message);
        public ShowUDPMessage myDelegate;

        UdpClient udpClient; // = new UdpClient(11000);
        Thread thread;
        // <---------------------- UDP ---------------------->

        // файл настроек
        TTIniFile myIniFile;
        
        // настройка локали преобразования форматов 
        CultureInfo invC = CultureInfo.InvariantCulture;
        const double CGrad2Rad = Math.PI / 180;

        // настройки логирования
        const int CMaxVisibleLogLines = 50;
        // Настройки карт
        public int ActiveMapIndex = 0;

        int VectorMapLength = 0; // количество елементов (отрезков) в векторной карте

        // Миссии
        //bool MissionActive = false;
        int MissionLength = 0; // количество шагов миссии
        double MissionTime = 0; // длина миссии
        double MissionManevrTimer = 0; // отсечка времени начала маневра
        int MissionPointID = 0;
        double MissionPointLongitude = 0;
        double MissionPointLattitude = 0;
        double MissionPointCourse = 0;
        double MissionPointDistance = 0;

        double MissionLastBrakingTimeStamp = 0; // время когда начинается отчет времени (был нажат тормоз)
        double MissionEnableMovingTimeStamp = 0; // время с которого можно начать двигаться
        double MissionMovingTimeOut = 5; // задержка времени от старта для начала движения
        double MissionPointBrakingTimeOut = 5; // задержка по остновке в записи миссии
        double MissionSafeBrakingTimeOut = 4; // задержка по остновке у препятствия
        double MissionLongBrakingTimeOut = 10; // задержка по долгой остновке


        // Режимы управления
        bool MissionActive = false;
        bool STOPModeActive = false;
        bool PAUSEModeActive = false;
        bool RUNModeActive = false;

        string ControlBoardCommandBuffer = "";
        string SteeringBoardCommandBuffer = "";

        public int JoystrickToSteeringMissionCorrectionValue = 0; // значение поправки по рулению в режиме миссии, получаемые от джойстика

        public MiniMapMissionPoint [] MissionPointsData = new MiniMapMissionPoint[1]; // массив данных точек для отрисовки миссии
        public VectorMapBaseElement[] VectorMapElementsData = new VectorMapBaseElement[1]; // массив элементов отрисовки векторной карты

        public double MiniMapViewportScale = 1; // умножитель разрешения экрана, может применяться для "растягивания" изображения
        public int MiniMapScaleFactor = 0;  // масшаб миникарты
                                            //    5 px = 1 m
                                            //    2 px = 1 m
                                            //    1 px = 1 m
                                            //    1 px = 5 m
                                            //    1 px = 10 m
                                            //    1 px = 25 m


        double CourceMapMarkerPositionX = 0;  // смещение позиции маркера машины от центра изображения карты (вправо)
        double CourceMapMarkerPositionY = 50;  // (вниз)

        double MiniMapCenterLongitude = 37.3131025;  // Координаты условного центра миникарты, от которого будет рисоваться всё. Там обычно машинка стоит
        double MiniMapCenterLattitude = 56.349174;
        double MiniMapCenterCource = 179.823;
        double MiniMapCenterSinC = 0.00308922786242266419972259837556;
        double MiniMapCenterCosC = -0.99999522832422257088600398796898;
        double MiniMapCourseVectorLength = 15; // длина вектора курса (в пикселях) для отрисовки. Настраивается в при настройка масштаба.


        public const double GlobalMapXScale_GPS2Meters = 62394; //61592.95299;   //   62394 м
        public const double GlobalMapYScale_GPS2Meters = 111321; // 111111.111111;  //  40008,5 км : 360 = 111321
        public double GlobalMapXScale_Meter2GPS = 1 / GlobalMapXScale_GPS2Meters;
        public double GlobalMapYScale_Meter2GPS = 1 / GlobalMapYScale_GPS2Meters;
        public double GlobalMapXScale_Meter2Pixels = 1;
        public double GlobalMapYScale_Meter2Pixels = 1;
        public double GlobalMapXScale_GPS2Pixels = GlobalMapXScale_GPS2Meters; // * GlobalMapXScale_Meter2Pixels;
        public double GlobalMapYScale_GPS2Pixels = GlobalMapYScale_GPS2Meters; // * GlobalMapYScale_Meter2Pixels;
        

        // Буферы для обмена данными
        string SteeringBoardComPortDataReciveBuffer = "";
        string SteeringBoardComPortErrorReciveBuffer = "";
        string ControlBoardComPortDataReciveBuffer = "";
        string ControlBoardComPortErrorReciveBuffer = "";
        string WheelEncodersCOMPortDataReciveBuffer = "";
        string WheelEncodersCOMPortErrorReciveBuffer = "";
        string UltrasonicCOMPortDataReciveBuffer = "";
        string UltrasonicCOMPortErrorReciveBuffer = "";
        string TerminalCOMPortDataReciveBuffer = "";
        string TerminalCOMPortErrorReciveBuffer = "";
        string IndicatorsCOMPortDataReciveBuffer = "";
        string IndicatorsCOMPortErrorReciveBuffer = "";

        // Hokyo lidar
        string URGPortDataReciveBuffer = "";  // чистим буфер чтения
        string URGPortErrorReciveBuffer = "";

        const int URG_StartStep = 0;
        const int URG_EndStep = 768;
        const int URG_PointCount = URG_EndStep - URG_StartStep;
        const int URG_DropLineCount = 2;
        int URG_DropLineIndex = 0;
        int[] URG_Data = new int[URG_PointCount];


        // Файл для хранения лога RTK
        StreamWriter RTKLogFile;
        string RTKLogFileName = "";
        bool isRTKLogging = false;  // по умолчанию лог не пишем

        // Переменные для хранения маршрутных данных
           // Отсчёт по энкодерам
        long lastwheelencodervalue = 0;
        double lastwheelencoderdatatick = 0; // последнее время прихода данных
        double currentwheelencoderspeed = 0;  // текущее значение скорости
        double lastwheelencoderspeed = 0;  // предыдущее значение скорости
        DateTime WheelEncoderCurrentTimePoint = DateTime.Now;
        DateTime WheelEncoderLastTimePoint = DateTime.Now;
        //double ts = (double)dt.Hour * 3600 + (double)dt.Minute * 60 + (double)dt.Second + (double)dt.Millisecond / 1000; 

        // Техническое зрение
        // Счетчик секунд таймаута
        long TimeOutSecondsCounter = 0;

        // Работа с проводным геймпадом
        DirectInput directInput;  // Объект DirectInput
        Guid joystickGuid; // Joystick Guid
        Joystick joystick;

        double ManualSteeringTimeStamp = 0; // отсечка времени команды руления с джойстика. Используется для подруливания в процессе езды в автоматическом режиме. В секундах
        double ManualSteeringTimeOut = 0.5;  // период времени, который после подруливания с джойстика, автоматическая система не будет пытаться крутить руль. В секундах

        int [] AccurateSteeringPositions = new int [10];
        int[] AccurateSteeringChartXs = new int[10]; // позиции для отрисовки углов
        int[] AccurateSteeringChartYs = new int[10]; // позиции для отрисовки углов

        const bool CStartWithoutJoystick = false;

        // точное руление
        int[] SteeringFastSelectPositions = new int[10];
        int JoystickLowBoundary = 3000;   // нижняя граница чувствительности стика джойстика
        int JoystickHighBoundary = 62500; // верхняя граница чувствительности стика джойстика
        int JoystickLowDeadZone = 30767; // нижняя граница мертвой зоны джойстика (у центрального значения 32768)
        int JoystickHighDeadZone = 34767; // верхняя граница мертвой зоны джойстика (у центрального значения 32768)


        public Form1()
        {
            InitializeComponent();

            SteeringTunePanel.Visible = false;
            UDPConnectsPanel.Visible = false;

            // Установим ссылку на объект джойстика как пустую, чтобы не проверять джойстик при старте
            joystickGuid = Guid.Empty;

            //выбор масштаба отображения миссии на карте
            MissionMapScaleComboBox.SelectedIndex = 0;

            // настройка быстрого руления
            SteeringFastSelectPositions[0] = 500;
            SteeringFastSelectPositions[1] = 330;
            SteeringFastSelectPositions[2] = 350;
            SteeringFastSelectPositions[3] = 400;
            SteeringFastSelectPositions[4] = 475;
            SteeringFastSelectPositions[5] = 500;
            SteeringFastSelectPositions[6] = 525;
            SteeringFastSelectPositions[7] = 600;
            SteeringFastSelectPositions[8] = 650;
            SteeringFastSelectPositions[9] = 670;

            // настройка аккуратного руления
            AccurateSteeringPositions[0] = 500;
            AccurateSteeringPositions[1] = 330;
            AccurateSteeringPositions[2] = 350;
            AccurateSteeringPositions[3] = 400;
            AccurateSteeringPositions[4] = 475;
            AccurateSteeringPositions[5] = 500;
            AccurateSteeringPositions[6] = 525;
            AccurateSteeringPositions[7] = 600;
            AccurateSteeringPositions[8] = 650;
            AccurateSteeringPositions[9] = 670;

            AccurateSteeringChartXs[0] = 0;
            AccurateSteeringChartXs[1] = 30;
            AccurateSteeringChartXs[2] = 92;
            AccurateSteeringChartXs[3] = 154;
            AccurateSteeringChartXs[4] = 216;
            AccurateSteeringChartXs[5] = 278;
            AccurateSteeringChartXs[6] = 340;
            AccurateSteeringChartXs[7] = 402;
            AccurateSteeringChartXs[8] = 464;
            AccurateSteeringChartXs[9] = 528;

            AccurateSteeringChartYs[0] = 0;
            AccurateSteeringChartYs[1] = 0;
            AccurateSteeringChartYs[2] = 0;
            AccurateSteeringChartYs[3] = 0;
            AccurateSteeringChartYs[4] = 0;
            AccurateSteeringChartYs[5] = 0;
            AccurateSteeringChartYs[6] = 0;
            AccurateSteeringChartYs[7] = 0;
            AccurateSteeringChartYs[8] = 0;
            AccurateSteeringChartYs[9] = 0;




            // Обработка Ini-файла      
            string spath = Path.GetFileNameWithoutExtension(Application.ExecutablePath);
            myIniFile = new TTIniFile(spath + ".ini");

            RTKLogFileName = Path.GetFileNameWithoutExtension(Application.ExecutablePath) + "_rtklog.txt";
            // Загружаем настройки
            LoadIniData();

            // Стартовая позиция формы на экране
            //this.Left = Screen.PrimaryScreen.WorkingArea.Left;
            //this.Top = Screen.PrimaryScreen.WorkingArea.Top;
            //this.Left = 0;
            //this.Top = 0;
            this.Location = new Point(0, 0);

            //this.WindowState = FormWindowState.Maximized;
            //this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;

            // Screen.PrimaryScreen.WorkingArea;

            ShowMiniMapScaleFactor();
            
        }

        private void CloseAppButton_Click(object sender, EventArgs e)
        {
            Application.Exit(); // завершаем программу
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (myIniFile != null)
            {
                // Сохранить настройки
                int n = 0;
                bool b = false;
                string s = ControlBoardCOMTextBox.Text;
                myIniFile.WriteString("ControlBoardCOMPort", s);
                s = SteeringCOMComboBox.Text;
                myIniFile.WriteString("SteeringCOMPort", s);
                s = WheelEncodersCOMTextBox.Text;
                myIniFile.WriteString("WheelEncodersCOMPort", s);
                s = TerminalCOMTextBox.Text;
                myIniFile.WriteString("TerminalCOMPort", s);
                s = IndicatorsComboBox.Text;
                myIniFile.WriteString("IndicatorsCOMPort", s);
                s = HoykoLidarCOMComboBox.Text;
                myIniFile.WriteString("HoykoLidarCOMPort", s);
                s = RTKUDPLocalPortTextBox.Text;
                myIniFile.WriteString("LocalRTKUDPPort", s);
                s = MissionFileNameTextBox.Text;
                myIniFile.WriteString("MissionFile", s);
                s = VectorMapFileNameTextBox.Text;
                myIniFile.WriteString("VectorMapFile", s);                
                s = MissionControlRadiusTextBox.Text;
                myIniFile.WriteString("PointRadius", s);

                s = RTKRAWLongitudeFieldTextBox.Text;
                myIniFile.WriteString("LongitudeField", s);
                s = RTKRAWLattitudeFieldTextBox.Text;
                myIniFile.WriteString("LattitudeField", s);
                s = RTKRAWCourceFieldTextBox.Text;
                myIniFile.WriteString("CourceField", s);
                
                if (UseManevrCheckBox.Checked)
                {
                    myIniFile.WriteString("Manevrirovanie", "1");
                }
                else
                {
                    myIniFile.WriteString("Manevrirovanie", "0");
                }

                if (StartAfterManevrCheckBox.Checked)
                {
                    myIniFile.WriteString("ManevrirovanieRestart", "1");
                }
                else
                {
                    myIniFile.WriteString("ManevrirovanieRestart", "0");
                }

                myIniFile.WriteString("EnableAutoStartMoving", EnableAutoStartMovingCheckBox.Checked ? "1" : "0");

                n = MissionMapScaleComboBox.SelectedIndex;
                myIniFile.WriteInteger("MissionMapScale", n);

                myIniFile.WriteInteger("MiniMapScale", MiniMapScaleFactor);

                n = MiniMapRenderTypeComboBox.SelectedIndex;
                myIniFile.WriteInteger("MissionMapRenderType", n);

                n = MiniMapMarkerPosComboBox.SelectedIndex;
                myIniFile.WriteInteger("MissionMarkerPos", n);

                myIniFile.WriteString("AccurateSteering", AccurateSteeringModeCheckBox.Checked ? "1" : "0") ;

                // настройка быстрого руления
                myIniFile.WriteInteger("SteeringFSP1", SteeringFastSelectPositions[1]);
                myIniFile.WriteInteger("SteeringFSP2", SteeringFastSelectPositions[2]);
                myIniFile.WriteInteger("SteeringFSP3", SteeringFastSelectPositions[3]);
                myIniFile.WriteInteger("SteeringFSP4", SteeringFastSelectPositions[4]);
                myIniFile.WriteInteger("SteeringFSP5", SteeringFastSelectPositions[5]);
                myIniFile.WriteInteger("SteeringFSP6", SteeringFastSelectPositions[6]);
                myIniFile.WriteInteger("SteeringFSP7", SteeringFastSelectPositions[7]);
                myIniFile.WriteInteger("SteeringFSP8", SteeringFastSelectPositions[8]);
                myIniFile.WriteInteger("SteeringFSP9", SteeringFastSelectPositions[9]);

                myIniFile.WriteString("AccurateLeft90", AccurateSteeringLeft90TextBox.Text);
                myIniFile.WriteString("AccurateLeft60", AccurateSteeringLeft60TextBox.Text);
                myIniFile.WriteString("AccurateLeft30", AccurateSteeringLeft30TextBox.Text);
                myIniFile.WriteString("AccurateLeft15", AccurateSteeringLeft15TextBox.Text);
                myIniFile.WriteString("AccurateForward", AccurateSteeringForwardTextBox.Text);
                myIniFile.WriteString("AccurateRight15", AccurateSteeringRight15TextBox.Text);
                myIniFile.WriteString("AccurateRight30", AccurateSteeringRight30TextBox.Text);
                myIniFile.WriteString("AccurateRight60", AccurateSteeringRight60TextBox.Text);
                myIniFile.WriteString("AccurateRight90", AccurateSteeringRight90TextBox.Text);

                myIniFile.SaveToFile();

            }
 
            // Отключение UDP клиента для RTK #########################################
            StopUDPClient();

        }

        private void LoadIniData()
        {
            // загрузка настроек
            int n = 0;
            string s = myIniFile.ReadString("ControlBoardCOMPort", "5");
            ControlBoardCOMTextBox.Text = s;            
            s = myIniFile.ReadString("SteeringCOMPort", "6");
            SteeringCOMComboBox.Text = s;
            s = myIniFile.ReadString("WheelEncodersCOMPort", "7");
            WheelEncodersCOMTextBox.Text = s;
            s = myIniFile.ReadString("IndicatorsCOMPort", "12");
            TerminalCOMTextBox.Text = s;
            s = myIniFile.ReadString("IndicatorsCOMPort", "11");
            IndicatorsComboBox.Text = s;
            s = myIniFile.ReadString("HoykoLidarCOMPort", "13");
            HoykoLidarCOMComboBox.Text = s;
            s = myIniFile.ReadString("LocalRTKUDPPort", "4000");
            RTKUDPLocalPortTextBox.Text = s;
            s = myIniFile.ReadString("MissionFile",@"D:\RoboCrossMission.txt");
            MissionFileNameTextBox.Text = s;
            s = myIniFile.ReadString("VectorMapFile", @"D:\RoboCrossVectorMap.txt");
            VectorMapFileNameTextBox.Text = s;
            s = myIniFile.ReadString("Manevrirovanie", "1");
            UseManevrCheckBox.Checked = (s == "1");
            s = myIniFile.ReadString("ManevrirovanieRestart", "1");
            StartAfterManevrCheckBox.Checked = (s == "1");
            s = myIniFile.ReadString("EnableAutoStartMoving", "0");
            EnableAutoStartMovingCheckBox.Checked = (s == "1");            

            s = myIniFile.ReadString("PointRadius", "10");
            MissionControlRadiusTextBox.Text = s;

            s = myIniFile.ReadString("LongitudeField", "2");
            RTKRAWLongitudeFieldTextBox.Text = s;
            s = myIniFile.ReadString("LattitudeField", "4");
            RTKRAWLattitudeFieldTextBox.Text = s;
            s = myIniFile.ReadString("CourceField", "15");
            RTKRAWCourceFieldTextBox.Text = s;

            //n = myIniFile.ReadIntegerInRange("MissionMapScale", 0, 0, MissionMapScaleComboBox.Items.Count - 1);
            n = myIniFile.ReadInteger("MissionMapScale", 0);
            if (n > MissionMapScaleComboBox.Items.Count - 1)
            {
                n = MissionMapScaleComboBox.Items.Count - 1;
            }
            MissionMapScaleComboBox.SelectedIndex = n;

            n = myIniFile.ReadInteger("MiniMapScale", 0);
            if ((MiniMapScaleFactor >= 0) && (MiniMapScaleFactor <= 8))
            {
                MiniMapScaleFactor = n;
            }

            n = myIniFile.ReadInteger("MissionMapRenderType", 0);
            MiniMapRenderTypeComboBox.SelectedIndex = n;

            n = myIniFile.ReadInteger("MissionMarkerPos", 0);
            MiniMapMarkerPosComboBox.SelectedIndex = n;

           s =  myIniFile.ReadString("AccurateSteering", "0");
           AccurateSteeringModeCheckBox.Checked = (s == "1");

            // настройка быстрого руления
            SteeringFastSelectPositions[1] = myIniFile.ReadIntegerInRange("SteeringFSP1", 330, 100, 999);
            SteeringFastSelectPosition1TextBox.Text = SteeringFastSelectPositions[1].ToString();
            SteeringFastSelectPositions[2] = myIniFile.ReadIntegerInRange("SteeringFSP2", 350, 100, 999);
            SteeringFastSelectPosition2TextBox.Text = SteeringFastSelectPositions[2].ToString();
            SteeringFastSelectPositions[3] = myIniFile.ReadIntegerInRange("SteeringFSP3", 400, 100, 999);
            SteeringFastSelectPosition3TextBox.Text = SteeringFastSelectPositions[3].ToString();
            SteeringFastSelectPositions[4] = myIniFile.ReadIntegerInRange("SteeringFSP4", 475, 100, 999);
            SteeringFastSelectPosition4TextBox.Text = SteeringFastSelectPositions[4].ToString();
            SteeringFastSelectPositions[5] = myIniFile.ReadIntegerInRange("SteeringFSP5", 500, 100, 999);
            SteeringFastSelectPosition5TextBox.Text = SteeringFastSelectPositions[5].ToString();
            SteeringFastSelectPositions[6] = myIniFile.ReadIntegerInRange("SteeringFSP6", 525, 100, 999);
            SteeringFastSelectPosition6TextBox.Text = SteeringFastSelectPositions[6].ToString();
            SteeringFastSelectPositions[7] = myIniFile.ReadIntegerInRange("SteeringFSP7", 600, 100, 999);
            SteeringFastSelectPosition7TextBox.Text = SteeringFastSelectPositions[7].ToString();
            SteeringFastSelectPositions[8] = myIniFile.ReadIntegerInRange("SteeringFSP8", 650, 100, 999);
            SteeringFastSelectPosition8TextBox.Text = SteeringFastSelectPositions[8].ToString();
            SteeringFastSelectPositions[9] = myIniFile.ReadIntegerInRange("SteeringFSP9", 670, 100, 999);
            SteeringFastSelectPosition9TextBox.Text = SteeringFastSelectPositions[9].ToString();
            SteeringFastSelectPositions[0] = SteeringFastSelectPositions[5];


            AccurateSteeringLeft90TextBox.Text = myIniFile.ReadString("AccurateLeft90", "330");
            AccurateSteeringLeft60TextBox.Text = myIniFile.ReadString("AccurateLeft60", "350");
            AccurateSteeringLeft30TextBox.Text = myIniFile.ReadString("AccurateLeft30", "400");
            AccurateSteeringLeft15TextBox.Text = myIniFile.ReadString("AccurateLeft15", "475");
            AccurateSteeringForwardTextBox.Text = myIniFile.ReadString("AccurateForward", "500");
            AccurateSteeringRight15TextBox.Text = myIniFile.ReadString("AccurateRight15", "525");
            AccurateSteeringRight30TextBox.Text = myIniFile.ReadString("AccurateRight30", "600");
            AccurateSteeringRight60TextBox.Text = myIniFile.ReadString("AccurateRight60", "650");
            AccurateSteeringRight90TextBox.Text = myIniFile.ReadString("AccurateRight90", "670");

            CheckAndProcessAcurateSteeringPositions();


        }

        private string TruncateStringByDot(string s)
        {
            int n = s.IndexOf('.');
            if (n >= 0)
            {
                s = s.Substring(0, n);
            }
            return s;
        }

        private int ConvertStrToIntOrZero(string s)
        {
            int n;
            s = TruncateStringByDot(s);
            if (!int.TryParse(s, out n))
            {
                n = 0;
            }
            return n;
        }

        private long ConvertStrToLongOrZero(string s)
        {
            long n;
            s = TruncateStringByDot(s);
            if (!long.TryParse(s, out n))
            {
                n = 0;
            }
            return n;
        }

        private string StringDotToComma(string s)
        {
            string r = s.Replace(".", ",");
            return r;
        }

        private string StringCommaToDot(string s)
        {
            string r = s.Replace(",", ".");
            return r;
        }

        private double ConvertStrToDoubleOrZero(string s)
        {
            //string sf = s; // StringDotToComma(s);  // поставить если локаль - русская, то есть запятая вместо точки
            string sf = StringCommaToDot(s);          // для "руглицкой локали - заменяем запятые на точки 
            double f;
            //s = TruncateStringByDot(s);
            if (!double.TryParse(sf, NumberStyles.Number, invC, out f))
            {
                f = 0;
            }
            return f;
        }

        private int DataScaling(int RawPoint, int LowPoint, int HighPoint, int MinBoundary, int MaxBoundary) // функция растяжения значения пропорционально соотношениям 
        {
            int n = 0;            
            if (LowPoint < HighPoint)
            {
                if (RawPoint < LowPoint)
                {
                    n = MinBoundary;
                }
                else if (RawPoint > HighPoint)
                {
                    n = MaxBoundary;
                }
                else
                {
                    double DRawPoint = (double)RawPoint;
                    double DLowPoint = (double)LowPoint;
                    double DHighPoint = (double)HighPoint;
                    double DMinBoundary = (double)MinBoundary;
                    double DMaxBoundary = (double)MaxBoundary;
                    double DValue = DRawPoint - DLowPoint;
                    double DScale = (DMaxBoundary - DMinBoundary) / (DHighPoint - DLowPoint);
                    double DResult = DMinBoundary + DValue * DScale;
                    n = (int)(DResult);
                }
            }
            else
            {
                n = MinBoundary;
            }
            return n;
        }

        private double CourseCycleCorrection(double acource)  // переводим углы в диапазон (0 - 360)
        {
            double f;
            if ((acource >= 0) && (acource < 360))
            {
                f = acource;
            }
            else
            {
                int n = (int)(acource / 360); // считаем циклы
                if (acource < 0)
                {
                    f = acource + (double)((1 - n)*360);  // n<0, acource<0
                }
                else 
                {
                    f = acource - (double)(n * 360); // вычтем целое количество 360-ток
                }
            };            
            return f;
        }

        private double GetNowInSeconds()
        {
            DateTime dt = DateTime.Now;
            return (double)dt.Hour * 3600 + (double)dt.Minute * 60 + (double)dt.Second + (double)dt.Millisecond / 1000;
        }

        private void PrintLog(string s)  // <--------------- временно --------------!!!
        {
            ReportListBox.Items.Add(s);
            while (ReportListBox.Items.Count > CMaxVisibleLogLines)
            {
                ReportListBox.Items.RemoveAt(0);
            }
            ReportListBox.SelectedIndex = ReportListBox.Items.Count - 1;
            ReportListBox.SelectedIndex = -1;
            //PrintControlBoardLog(s);
          
            // вывод сообщения завершен      
        }

        private void PrintControlBoardLog(string s)
        {
            // CMaxVisibleLogLines
            ControlBoardReportListBox.Items.Add(s);
            while (ControlBoardReportListBox.Items.Count > CMaxVisibleLogLines)
            {
                ControlBoardReportListBox.Items.RemoveAt(0);
            }
            ControlBoardReportListBox.SelectedIndex = ControlBoardReportListBox.Items.Count - 1;
            ControlBoardReportListBox.SelectedIndex = -1;
        }

        private void PrintSteeringBoardLog(string s)
        {
            // CMaxVisibleLogLines
            SteeringReportListBox.Items.Add(s);
            while (SteeringReportListBox.Items.Count > CMaxVisibleLogLines)
            {
                SteeringReportListBox.Items.RemoveAt(0);
            }
            SteeringReportListBox.SelectedIndex = SteeringReportListBox.Items.Count - 1;
            SteeringReportListBox.SelectedIndex = -1;
        }

        private void PrintWheelEncodersReportLog(string s)
        {
            // CMaxVisibleLogLines
            WheelEncodersReportListBox.Items.Add(s);
            while (WheelEncodersReportListBox.Items.Count > CMaxVisibleLogLines)
            {
                WheelEncodersReportListBox.Items.RemoveAt(0);
            }
            WheelEncodersReportListBox.SelectedIndex = WheelEncodersReportListBox.Items.Count - 1;
            WheelEncodersReportListBox.SelectedIndex = -1;
        }

        private void PrintTerminalLog(string s)
        {
            // CMaxVisibleLogLines
            TerminalReportListBox.Items.Add(s);
            while (TerminalReportListBox.Items.Count > CMaxVisibleLogLines)
            {
                TerminalReportListBox.Items.RemoveAt(0);
            }
            TerminalReportListBox.SelectedIndex = TerminalReportListBox.Items.Count - 1;
            TerminalReportListBox.SelectedIndex = -1;
        }

        private void PrintIndicatorsLog(string s)
        {
            // CMaxVisibleLogLines
            IndicatorsListBox.Items.Add(s);
            while (IndicatorsListBox.Items.Count > CMaxVisibleLogLines)
            {
                IndicatorsListBox.Items.RemoveAt(0);
            }
            IndicatorsListBox.SelectedIndex = IndicatorsListBox.Items.Count - 1;
            IndicatorsListBox.SelectedIndex = -1;
        }

        private void PrintUltrasonicLog(string s)
        {
            // CMaxVisibleLogLines
            UltrasonicsReportListBox.Items.Add(s);
            while (UltrasonicsReportListBox.Items.Count > CMaxVisibleLogLines)
            {
                UltrasonicsReportListBox.Items.RemoveAt(0);
            }
            UltrasonicsReportListBox.SelectedIndex = UltrasonicsReportListBox.Items.Count - 1;
            UltrasonicsReportListBox.SelectedIndex = -1;
        }

        private void PrintMissionReportLog(string s)
        {
            // CMaxVisibleLogLines
            MissionReportListBox.Items.Add(s);
            while (MissionReportListBox.Items.Count > CMaxVisibleLogLines)
            {
                MissionReportListBox.Items.RemoveAt(0);
            }
            MissionReportListBox.SelectedIndex = MissionReportListBox.Items.Count - 1;
            MissionReportListBox.SelectedIndex = -1;
        }

        private void PrintRTKDataLog(string s)
        {
            // CMaxVisibleLogLines
            RTKCurrentDataListBox.Items.Add(s);
            while (RTKCurrentDataListBox.Items.Count > CMaxVisibleLogLines)
            {
                RTKCurrentDataListBox.Items.RemoveAt(0);
            }
            RTKCurrentDataListBox.SelectedIndex = RTKCurrentDataListBox.Items.Count - 1;
            RTKCurrentDataListBox.SelectedIndex = -1;
            // Запись лога
            if (isRTKLogging) // идет логгирование в файл - останавливаем
            {
                if (RTKLogFile != null)
                {
                    DateTime ts = DateTime.Now;//stopWatch.Elapsed;
                    string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hour, ts.Minute, ts.Second, ts.Millisecond / 10);
                    try
                    {
                        RTKLogFile.WriteLine(elapsedTime + "> " + s);
                    }
                    catch (Exception ex)
                    {
                        PrintLog("Ошибка записи лога RTK (" + ex.Message + ")");
                    }
                }
            }
            // вывод сообщения завершен             
        }


        private void ControlBoardSerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            // Внимание, порт работает в асинхронном режиме!  Нельзя писать данные в контролы!!!
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting();
            ControlBoardComPortDataReciveBuffer += indata;
        }

        private void ControlBoardSerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            // Внимание, порт работает в асинхронном режиме!  Нельзя писать данные в контролы!!!
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting();
            ControlBoardComPortErrorReciveBuffer += "Ошибка \n" + indata;
        }

        private void SteeringBoardSerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            // Внимание, порт работает в асинхронном режиме!  Нельзя писать данные в контролы!!!
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting();
            SteeringBoardComPortDataReciveBuffer += indata;
        }

        private void SteeringBoardSerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            // Внимание, порт работает в асинхронном режиме!  Нельзя писать данные в контролы!!!
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting();
            SteeringBoardComPortErrorReciveBuffer += "Ошибка \n" + indata;
        }

        private void WheelEncodersSerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            // Внимание, порт работает в асинхронном режиме!  Нельзя писать данные в контролы!!!
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting();
            WheelEncodersCOMPortDataReciveBuffer += indata;
        }

        private void WheelEncodersSerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            // Внимание, порт работает в асинхронном режиме!  Нельзя писать данные в контролы!!!
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting();
            WheelEncodersCOMPortErrorReciveBuffer += indata;
        }

        private void FrontSensorsSerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            // Внимание, порт работает в асинхронном режиме!  Нельзя писать данные в контролы!!!
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting();
            TerminalCOMPortDataReciveBuffer += indata;
        }

        private void FrontSensorsSerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            // Внимание, порт работает в асинхронном режиме!  Нельзя писать данные в контролы!!!
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting();
            TerminalCOMPortErrorReciveBuffer += indata;
        }

        private void UltrasonicSerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            // Внимание, порт работает в асинхронном режиме!  Нельзя писать данные в контролы!!!
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting();
            UltrasonicCOMPortDataReciveBuffer += indata;
        }

        private void UltrasonicSerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            // Внимание, порт работает в асинхронном режиме!  Нельзя писать данные в контролы!!!
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting();
            UltrasonicCOMPortErrorReciveBuffer += indata;
        }

        private void CheckAndViewControlBoardConnect()  // функция включает-отключает контролы, ответственные за настройку порта основного контроллера
        {
            if (ControlBoardSerialPort.IsOpen)
            {
                ControlBoardCOMTextBox.Enabled = false;
                ControlBoardCOMTextBox.BackColor = Color.LightGray;
                ControlBoardCOMConnectButton.Text = "Отключить";
                //ControlBoardTimer.Enabled = true;
            }
            else
            {
                ControlBoardCOMTextBox.Enabled = true;
                ControlBoardCOMTextBox.BackColor = Color.White;
                ControlBoardCOMConnectButton.Text = "Подключить";
                //MainControlTimer.Enabled = false;
            }
        }

        private void CheckAndViewSteeringBoardConnect()  // функция включает-отключает контролы, ответственные за настройку порта рулевой системы
        {
            if (SteeringBoardSerialPort.IsOpen)
            {
                SteeringCOMComboBox.Enabled = false;
                SteeringCOMComboBox.BackColor = Color.LightGray;
                SteeringBoardCOMConnectButton.Text = "Отключить";
                //ControlBoardTimer.Enabled = true;
            }
            else
            {
                SteeringCOMComboBox.Enabled = true;
                SteeringCOMComboBox.BackColor = Color.White;
                SteeringBoardCOMConnectButton.Text = "Подключить";
                //MainControlTimer.Enabled = false;
            }
        }

        private void CheckAndViewWheelEncodersConnect()  // функция включает-отключает контролы, ответственные за настройку порта основного контроллера
        {
            if (WheelEncodersSerialPort.IsOpen)
            {
                WheelEncodersCOMTextBox.Enabled = false;
                WheelEncodersCOMTextBox.BackColor = Color.LightGray;
                WheelEncodersCOMConnectButton.Text = "Отключить";
                //ControlBoardTimer.Enabled = true;
            }
            else
            {
                WheelEncodersCOMTextBox.Enabled = true;
                WheelEncodersCOMTextBox.BackColor = Color.White;
                WheelEncodersCOMConnectButton.Text = "Подключить";
                //MainControlTimer.Enabled = false;
            }
        }


        private void CheckAndViewTerminalConnect()  // функция включает-отключает контролы, ответственные за настройку порта основного контроллера
        {
            if (TerminalSerialPort.IsOpen)
            {
                TerminalCOMTextBox.Enabled = false;
                TerminalCOMTextBox.BackColor = Color.LightGray;
                FrontSensorsCOMConnectButton.Text = "Отключить";
                //ControlBoardTimer.Enabled = true;
            }
            else
            {
                TerminalCOMTextBox.Enabled = true;
                TerminalCOMTextBox.BackColor = Color.White;
                FrontSensorsCOMConnectButton.Text = "Подключить";
                //MainControlTimer.Enabled = false;
            }
        }

        private void CheckAndViewIndicatorsConnect()  // функция включает-отключает контролы, ответственные за настройку порта основного контроллера
        {
            if (IndicatorsSerialPort.IsOpen)
            {
                IndicatorsComboBox.Enabled = false;
                IndicatorsComboBox.BackColor = Color.LightGray;
                IndicatorsCOMConnectButton.Text = "Отключить";
                //ControlBoardTimer.Enabled = true;
            }
            else
            {
                IndicatorsComboBox.Enabled = true;
                IndicatorsComboBox.BackColor = Color.White;
                IndicatorsCOMConnectButton.Text = "Подключить";
                //MainControlTimer.Enabled = false;
            }
        }


        private void CheckAndViewUltrasonicConnect() // функция включает-отключает контролы, ответственные за настройку порта основного контроллера
        {
            if (UltrasonicSerialPort.IsOpen)
            {
                UltrasonicCOMComboBox.Enabled = false;
                UltrasonicCOMComboBox.BackColor = Color.LightGray;
                UltrasonicCOMConnectButton.Text = "Отключить";
                //ControlBoardTimer.Enabled = true;
            }
            else
            {
                UltrasonicCOMComboBox.Enabled = true;
                UltrasonicCOMComboBox.BackColor = Color.White;
                UltrasonicCOMConnectButton.Text = "Подключить";
                //MainControlTimer.Enabled = false;
            }
        }

        private void ArduinoTimer_Tick(object sender, EventArgs e)
        {
            // (Полезная нагрузка)
            LastControlBoardCommandTextBox.Text = ControlBoardCommandBuffer;
            LastSteeringPosTextBox.Text = SteeringBoardCommandBuffer;
            // Контрольная панель
            // читаем буфер данных и выводим, если есть полные строки
            string answer;
            string s;
            string[] separators = { ":" };
            string[] separators_udp = { "#" };
            double LCurrentTime = GetNowInSeconds(); // текущее время
            int i = ControlBoardComPortDataReciveBuffer.IndexOf("\n");
            while (i >= 0)
            {
                answer = (ControlBoardComPortDataReciveBuffer.Substring(0, i)).Trim();
                ControlBoardComPortDataReciveBuffer = ControlBoardComPortDataReciveBuffer.Substring(i + 1, ControlBoardComPortDataReciveBuffer.Length - i - 1);
                PrintControlBoardLog(answer);

                // Обработка строки данных от Serial, "сырые" данные содержатся в answer;
                // "CVL:auto::0# 150-> 400# 150-> 260#"
                //  CVL:режим управления "свободный" или "автоматический":сигнал работы ходового двигателя (кнопка прерывателя ключа зажигания)# 150-> 400# 150-> 260#
                string[] scells_udp = answer.Split('#');
                if (scells_udp.Length >= 3)
                {
                    string[] scells = scells_udp[0].Split(':');
                    if (scells.Length >= 3)
                    {
                        CBValue01TextBox.Text = scells[0]; //
                        CBValue02TextBox.Text = scells[1];
                        CBValue03TextBox.Text = scells[2];
                        CBValue04TextBox.Text = scells[3];
                        CBValue04TextBox.Text = scells_udp[1];
                        CBValue05TextBox.Text = scells_udp[2];
                        //CBValue06TextBox.Text = scells_udp[4];
                        //CBValue07TextBox.Text = scells[6];  // Руль (RAW)
                        //CBValue08TextBox.Text = scells[7];  // 
                        //CBValue09TextBox.Text = scells[8];  // Аварийный режим (если 1)
                        
                        ControlBoardRecognizeStatusTextBox.Text = "Ок";
                    }
                    else
                    {
                        //PrintControlBoardLog("Неверный формат данных от контроллера");  // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                        ControlBoardRecognizeStatusTextBox.Text = "Ошибка данных";
                    }
                }
                else
                {
                    //PrintControlBoardLog("Неверный формат данных от контроллера");  // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                    ControlBoardRecognizeStatusTextBox.Text = "Ошибка данных";
                }

                // к следующей записи
                i = ControlBoardComPortDataReciveBuffer.IndexOf("\n");

            }
            // Руль ----------------------------------------------------------------
            i = SteeringBoardComPortDataReciveBuffer.IndexOf("\n");
            while (i >= 0)
            {
                answer = (SteeringBoardComPortDataReciveBuffer.Substring(0, i)).Trim();
                SteeringBoardComPortDataReciveBuffer = SteeringBoardComPortDataReciveBuffer.Substring(i + 1, SteeringBoardComPortDataReciveBuffer.Length - i - 1);
                PrintSteeringBoardLog(answer);

                // Обрезаем строку до комментариев
                i = answer.IndexOf("#");
                if (i >=0)
                {
                    answer = (answer.Substring(0, i)).Trim();
                }
                
                // Обработка строки данных от Serial, "сырые" данные содержатся в answer;

                string[] scells = answer.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                if (scells.Length >= 4)
                {

                    s = scells[0].Trim();
                    if (s == "CVLST")
                    {
                        SteeringModeTextBox.Text = scells[1];
                        SteeringValueTextBox.Text = scells[2];
                        SteeringDestValueTextBox.Text = scells[3];
                        if (SteeringModeTextBox.Text == "crit")
                        {
                            SteeringModeTextBox.BackColor = Color.Coral;
                        }
                        else
                        {
                            SteeringModeTextBox.BackColor = SystemColors.ButtonFace;
                        }
                    }
                }
                else
                {
                    //PrintSteeringBoardLog("Неверный формат данных от контроллера");  // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                    //SteeringBoardRecognizeStatusTextBox.Text = "Ошибка данных";
                }

                // к следующей записи
                i = SteeringBoardComPortDataReciveBuffer.IndexOf("\n");

            }
            // Дальномеры ----------------------------------------------------------------
            i = UltrasonicCOMPortDataReciveBuffer.IndexOf("\n");   
            while (i >= 0)
            {
                answer = (UltrasonicCOMPortDataReciveBuffer.Substring(0, i)).Trim();
                UltrasonicCOMPortDataReciveBuffer = UltrasonicCOMPortDataReciveBuffer.Substring(i + 1, UltrasonicCOMPortDataReciveBuffer.Length - i - 1);
                PrintUltrasonicLog(answer);

                // Обрезаем строку до комментариев
                i = answer.IndexOf("#");
                if (i >=0)
                {
                    answer = (answer.Substring(0, i)).Trim();
                }
                
                // Обработка строки данных от Serial, "сырые" данные содержатся в answer;

                string[] scells = answer.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                if (scells.Length >= 4)
                {

                    s = scells[0].Trim();
                    if (s == "CVLDST")
                    {
                        UltrasonicLeftDistanceTextBox.Text = scells[1];
                        UltrasonicCenterDistanceTextBox.Text = scells[2];
                        UltrasonicRightDistanceTextBox.Text = scells[3];                        
                    }
                }
                else
                {
                    //PrintUltrasonicLog("Неверный формат данных от контроллера");  // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!                    
                }

                // к следующей записи
                i = UltrasonicCOMPortDataReciveBuffer.IndexOf("\n");

            }
            // Пульт консоль ----------------------------------------------------------
            i = TerminalCOMPortDataReciveBuffer.IndexOf("\n");
            while (i >= 0)
            {
                answer = (TerminalCOMPortDataReciveBuffer.Substring(0, i)).Trim();
                TerminalCOMPortDataReciveBuffer = TerminalCOMPortDataReciveBuffer.Substring(i + 1, TerminalCOMPortDataReciveBuffer.Length - i - 1);
                PrintTerminalLog(answer);

                
                /*
                int SP = TerminalCOMPortDataReciveBuffer.IndexOf("S");  // ищем символ S
                if (SP >= 0) {  TerminalSValueTextBox.Text = "S"; };
                SP = TerminalCOMPortDataReciveBuffer.IndexOf("s");  // ищем символ s
                if (SP >= 0) { TerminalSValueTextBox.Text = "S"; };
                SP = TerminalCOMPortDataReciveBuffer.IndexOf("P");  // ищем символ P
                if (SP >= 0) { TerminalPValueTextBox.Text = "P"; };
                SP = TerminalCOMPortDataReciveBuffer.IndexOf("p");  // ищем символ p
                if (SP >= 0) { TerminalPValueTextBox.Text = "P"; };
                SP = TerminalCOMPortDataReciveBuffer.IndexOf("R");  // ищем символ R
                if (SP >= 0) { TerminalRValueTextBox.Text = "R"; };
                SP = TerminalCOMPortDataReciveBuffer.IndexOf("r");  // ищем символ r
                if (SP >= 0) { TerminalRValueTextBox.Text = "R"; };
                
                 */
                if ((TerminalCOMPortDataReciveBuffer.IndexOf("S") >= 0)||(TerminalCOMPortDataReciveBuffer.IndexOf("s") >= 0))
                {
                    TerminalSValueTextBox.Text = "S";
                    SetSTOPMode();
                }
                else
                {
                    TerminalSValueTextBox.Text = "";
                }
                if ((TerminalCOMPortDataReciveBuffer.IndexOf("R") >= 0)||(TerminalCOMPortDataReciveBuffer.IndexOf("r") >= 0))
                {
                    TerminalRValueTextBox.Text = "R";
                    SetRUNMode();
                }
                else
                {
                    TerminalRValueTextBox.Text = "";
                }
                if ((TerminalCOMPortDataReciveBuffer.IndexOf("P") >= 0)||(TerminalCOMPortDataReciveBuffer.IndexOf("p") >= 0))
                {
                    TerminalPValueTextBox.Text = "P";
                    SetPAUSEMode();
                }
                else
                {
                    TerminalPValueTextBox.Text = "";
                }
                if (TerminalCOMPortDataReciveBuffer.IndexOf("A") >= 0)  // ищем символ A
                { TerminalAValueTextBox.Text = "A"; } else { TerminalAValueTextBox.Text = ""; };
                if (TerminalCOMPortDataReciveBuffer.IndexOf("F") >= 0)  // ищем символ F
                { TerminalFValueTextBox.Text = "F"; } else { TerminalFValueTextBox.Text = ""; };

                // Обработка строки данных от Serial, "сырые" данные содержатся в answer;
                /*
                string[] scells = answer.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                if (scells.Length >= 4)
                {
                    s = scells[0].Trim();
                    if (s == "FDST")
                    { 
                        TerminalSValueTextBox.Text = scells[1];
                        TerminalPValueTextBox.Text = scells[2];
                        TerminalRValueTextBox.Text = scells[3];
                    }
                }
                 */
                i = TerminalCOMPortDataReciveBuffer.IndexOf("\n"); // Ищем следуюющий конец строки
            }
            // Энкодеры
            i = WheelEncodersCOMPortDataReciveBuffer.IndexOf("\n");
            while (i >= 0)
            {
                answer = (WheelEncodersCOMPortDataReciveBuffer.Substring(0, i)).Trim();
                WheelEncodersCOMPortDataReciveBuffer = WheelEncodersCOMPortDataReciveBuffer.Substring(i + 1, WheelEncodersCOMPortDataReciveBuffer.Length - i - 1);
                PrintWheelEncodersReportLog(answer);

                // Обработка строки данных от Serial, "сырые" данные содержатся в answer;

                string[] scells = answer.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                if (scells.Length >= 2)
                {
                    s = scells[0].Trim();
                    if (s == "ENC" || s=="CVLENC")
                    {                        
                        s = scells[1].Trim();
                        EncoderLeftWheelValueTextBox.Text = s;

                        // Пытаемся расчитать мгновенную скорость.
                        // Для этого берем старое значение таймера и старое значение энкодеров и выделяем

                        
                        //    EncoderSpeedWheelValueTextBox.Text = s;
                        
                        //  



                        if (LCurrentTime > lastwheelencoderdatatick + 1) // прошло больше секунды, можно рассчитать скорость
                        {
                            long currentwheelencodervalue = ConvertStrToIntOrZero(s);
                            // вычисляем скорость, считая, что на один оборот колеса приходится 30*2 тиков (поскольку плата присылает сумму по двум колесам)
                            currentwheelencoderspeed = ((double)(currentwheelencodervalue - lastwheelencodervalue)) / ((double)(LCurrentTime - lastwheelencoderdatatick)) / 60;
                            EncoderSpeedWheelValueTextBox.Text = currentwheelencoderspeed.ToString();
                            // можно как-то обработать скорость


                            lastwheelencodervalue = currentwheelencodervalue;
                        }
                    }
                    
                   // lastwheelencodervalue = ConvertStrTodoubleOrZero(s);
                   // lastwheelencoderdatatick = 0; // последнее время прихода данных
                }
                else
                {

                }
                i = WheelEncodersCOMPortDataReciveBuffer.IndexOf("\n");
            }

        }



        private void ControlBoardCMDTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter)
            {
                string s_com = ControlBoardCMDTextBox.Text;
                if (ControlBoardSerialPort.IsOpen)
                {
                    if (ControlBoardCommandBuffer != s_com)
                    {
                        ControlBoardSerialPort.Write(s_com);                    
                        PrintControlBoardLog("Send To CB:" + s_com);
                        ControlBoardCommandBuffer = s_com;
                    }
                }

                ControlBoardCMDTextBox.Text = "";
            }
        }

        //private void SendCommandToControlBoardUDP(string cmd)  // отправка команд на панель управления ---------------------
        //{
        //    if (ControlBoardSerialPort.IsOpen)
        //    {
        //        if (ControlBoardCommandBuffer != cmd)
        //        {
        //            ControlBoardSerialPort.Write(cmd);
        //            PrintControlBoardLog("Send To CB:" + cmd);
        //            ControlBoardCommandBuffer = cmd;
        //        }
        //    }
        //    else
        //    {
        //        PrintControlBoardLog("ОШИБКА: Порт закрыт!");
        //        // ControlBoardCommandBuffer = "";
        //        // ОТЛАДКА!!!!
        //        ControlBoardCommandBuffer = cmd;
        //    }
        //}

        private void SendCommandToIndicatorsBoard(string cmd)  // отправка команд на панель индикаторов ---------------------
        {
            if (IndicatorsSerialPort.IsOpen)
            {
                IndicatorsSerialPort.Write(cmd);
                PrintIndicatorsLog("Send To boar:" + cmd);
            }
            else
            {
                PrintIndicatorsLog("ОШИБКА: Порт закрыт!");
            }
        }

        //private void SendTextCommandToSteeringBoard(int direction)  // отправка команды на модуль руления ---------------------
        //{
        //    int destpos = 500;
        //    if (SteeringBoardSerialPort.IsOpen)
        //    {
        //        if ((direction >= 0) && (direction <= 9))
        //        {
        //            destpos = SteeringFastSelectPositions[direction];
        //        }
        //        else if ((direction >= 100) && (direction <= 999))
        //        {
        //            destpos = direction;
        //        }
        //        string cmd = destpos.ToString() + "#\n";

        //        if (SteeringBoardCommandBuffer != cmd)
        //        {
        //            SteeringBoardSerialPort.Write(cmd);
        //            PrintSteeringBoardLog("Send To SB:" + cmd);
        //            SteeringBoardCommandBuffer = cmd;
        //        }
        //    }
        //    else
        //    {
        //        PrintSteeringBoardLog("ОШИБКА: Порт закрыт!");
        //        SteeringBoardCommandBuffer = "";
        //    }
        //}

        //private void SendTextCommandToSteeringBoard(string cmd) // отправка текстовой команды
        //{
        //    if (SteeringBoardSerialPort.IsOpen)
        //    {
        //        if (SteeringBoardCommandBuffer != cmd)
        //        {
        //            SteeringBoardSerialPort.Write(cmd);
        //            PrintSteeringBoardLog("Send To SB:" + cmd);
        //            SteeringBoardCommandBuffer = cmd;
        //        }
        //    }
        //    else
        //    {
        //        PrintSteeringBoardLog("ОШИБКА: Порт закрыт!");
        //        SteeringBoardCommandBuffer = cmd;
        //    }
        //}

        private void SendTextCommandToSteeringBoardUDP(string command)
        {
            try
            {
                IPEndPoint ipEndPoint = new IPEndPoint(SteeringRemoteIP, SteeringRemotePort);

                string message = command;
                byte[] data = Encoding.UTF8.GetBytes(message);
                SteeringUdpClient.Send(data, data.Length, ipEndPoint);
                string time = DateTime.Now.ToShortTimeString();             // Время в системе, для отображения в чате
                PrintLog(time + " " + message, SteeringReportListBox);
            }
            catch (Exception ex)
            {
                PrintSteeringBoardLog(ex.Message);
            }
        }

        private void SendCommandToControlBoardUDP(string command)
        {
            try
            {
                // Отправляем команду start
                IPEndPoint ipEndPoint = new IPEndPoint(PropulsiveRemoteIP, PropulsiveRemotePort);

                byte[] data = Encoding.UTF8.GetBytes(command);
                PropulsiveUdpClient.Send(data, data.Length, ipEndPoint);
                string time = DateTime.Now.ToShortTimeString();             // Время в системе, для отображения в чате
                PrintLog(time + " " + command, ControlBoardReportListBox);
            }
            catch (Exception ex)
            {
                PrintSteeringBoardLog(ex.Message);
            }
        }

        private void SendTextCommandToEncodersgBoardUDP(string command)
        {
            try
            {
                // Отправляем команду start
                IPAddress ip = IPAddress.Parse(SteeringRemoteIP_textBox.Text.Trim());
                IPEndPoint ipEndPoint = new IPEndPoint(EncodersRemoteIP, EncodersRemotePort);

                string message = command;
                byte[] data = Encoding.UTF8.GetBytes(message);
                EncodersUdpClient.Send(data, data.Length, ipEndPoint);
                string time = DateTime.Now.ToShortTimeString();             // Время в системе, для отображения в чате
                PrintLog(time + " " + message, WheelEncodersReportListBox);
            }
            catch (Exception ex)
            {
                PrintSteeringBoardLog(ex.Message);
            }
        }

        private void SendCommandToWheelEncoders(string cmd)  // отправка команд на панель энкодеров ---------------------
        {
            if (WheelEncodersSerialPort.IsOpen)
            {
                WheelEncodersSerialPort.Write(cmd);
                PrintWheelEncodersReportLog("Send To WE:" + cmd);
            }
            else
            {
                PrintWheelEncodersReportLog("ОШИБКА: Порт закрыт!");
            }
        }

        private void ControlBoardSendCommandButton_Click(object sender, EventArgs e)
        {
            string s_com = ControlBoardCMDTextBox.Text;
            //SendCommandToControlBoardUDP(s_com); // + "\n");
            SendCommandToControlBoardUDP(s_com);
            // Отладка
            //PrintControlBoardLog("Send To CB:" + s_com);
            ControlBoardCMDTextBox.Text = "";
        }

        private void ControlBoardCOMConnectButton_Click(object sender, EventArgs e)
        {
            if (!ControlBoardSerialPort.IsOpen)
            {
                string s = ControlBoardCOMTextBox.Text;
                try
                {
                    int i = int.Parse(s);
                    if ((i >= 1) && (i < 255))
                    {
                        s = "COM" + i.ToString();
                        ControlBoardSerialPort.PortName = s;
                        ControlBoardSerialPort.DtrEnable = true; // ОБЯЗАТЕЛЬНО УСТАНАВЛИВАТЬ!  ИНАЧЕ ДАННЫЕ НЕ БУДУТ ПРИХОДИТЬ!!!!
                        //MainControlSerialPort.Encoding = Encoding.UTF8;
                        ControlBoardSerialPort.Open();
                        if (ControlBoardSerialPort.IsOpen)
                        {
                            PrintControlBoardLog("Соединение по " + s + " с контроллером установлено");
                        };
                    }
                }
                catch
                {
                    PrintControlBoardLog("Ошибка соединения с контроллером");
                    MessageBox.Show("Не удалось открыть соединение. Проверьте идентификатор порта и подключение контроллера");

                }
            }
            else // Порт был открыт, закрываем
            {
                ControlBoardSerialPort.Close();
                PrintControlBoardLog("Соединение с контроллером закрыто");
            }
            // Проверяем включённость соединения и настраиваем контролы
            CheckAndViewControlBoardConnect();
        }

        private void SteeringBoardCOMConnectButton_Click(object sender, EventArgs e)  // подключение-отключение рулевого мотора
        {
            if (!SteeringBoardSerialPort.IsOpen)
            {
                string s = SteeringCOMComboBox.Text;
                try
                {
                    int i = int.Parse(s);
                    if ((i >= 1) && (i < 255))
                    {
                        s = "COM" + i.ToString();
                        SteeringBoardSerialPort.PortName = s;
                        SteeringBoardSerialPort.DtrEnable = true; // ОБЯЗАТЕЛЬНО УСТАНАВЛИВАТЬ!  ИНАЧЕ ДАННЫЕ НЕ БУДУТ ПРИХОДИТЬ!!!!
                        //MainControlSerialPort.Encoding = Encoding.UTF8;
                        SteeringBoardSerialPort.Open();
                        if (SteeringBoardSerialPort.IsOpen)
                        {
                            PrintSteeringBoardLog("Соединение по " + s + " с контроллером установлено");
                        };
                    }
                }
                catch
                {
                    PrintSteeringBoardLog("Ошибка соединения с контроллером");
                    MessageBox.Show("Не удалось открыть соединение. Проверьте идентификатор порта и подключение контроллера");

                }
            }
            else // Порт был открыт, закрываем
            {
                SteeringBoardSerialPort.Close();
                PrintSteeringBoardLog("Соединение с контроллером закрыто");
            }
            // Проверяем включённость соединения и настраиваем контролы
            CheckAndViewSteeringBoardConnect();
        }


        private void FrontSensorsCOMConnectButton_Click(object sender, EventArgs e)
        {
            if (!TerminalSerialPort.IsOpen)
            {
                string s = TerminalCOMTextBox.Text;
                try
                {
                    int i = int.Parse(s);
                    if ((i >= 1) && (i < 255))
                    {
                        s = "COM" + i.ToString();
                        TerminalSerialPort.PortName = s;
                        TerminalSerialPort.DtrEnable = true; // ОБЯЗАТЕЛЬНО УСТАНАВЛИВАТЬ!  ИНАЧЕ ДАННЫЕ НЕ БУДУТ ПРИХОДИТЬ!!!!
                        //MainControlSerialPort.Encoding = Encoding.UTF8;
                        TerminalSerialPort.Open();
                        if (TerminalSerialPort.IsOpen)
                        {
                            PrintTerminalLog("Соединение по " + s + " с контроллером установлено");
                        };
                    }
                }
                catch
                {
                    PrintTerminalLog("Ошибка соединения с контроллером");
                    MessageBox.Show("Не удалось открыть соединение. Проверьте идентификатор порта и подключение контроллера");

                }
            }
            else // Порт был открыт, закрываем
            {
                TerminalSerialPort.Close();
                PrintTerminalLog("Соединение с контроллером закрыто");
            }
            // Проверяем включённость соединения и настраиваем контролы
            CheckAndViewTerminalConnect();
        }



        private void IndicatorsCOMConnectButton_Click(object sender, EventArgs e)
        {
            if (!IndicatorsSerialPort.IsOpen)
            {
                string s = IndicatorsComboBox.Text;
                try
                {
                    int i = int.Parse(s);
                    if ((i >= 1) && (i < 255))
                    {
                        s = "COM" + i.ToString();
                        IndicatorsSerialPort.PortName = s;
                        IndicatorsSerialPort.DtrEnable = true; // ОБЯЗАТЕЛЬНО УСТАНАВЛИВАТЬ!  ИНАЧЕ ДАННЫЕ НЕ БУДУТ ПРИХОДИТЬ!!!!
                        //MainControlSerialPort.Encoding = Encoding.UTF8;
                        IndicatorsSerialPort.Open();
                        if (IndicatorsSerialPort.IsOpen)
                        {
                            PrintIndicatorsLog("Соединение по " + s + " с контроллером установлено");
                        };
                    }
                }
                catch
                {
                    PrintIndicatorsLog("Ошибка соединения с контроллером");
                    MessageBox.Show("Не удалось открыть соединение. Проверьте идентификатор порта и подключение контроллера");

                }
            }
            else // Порт был открыт, закрываем
            {
                IndicatorsSerialPort.Close();
                PrintIndicatorsLog("Соединение с контроллером закрыто");
            }
            // Проверяем включённость соединения и настраиваем контролы
            CheckAndViewIndicatorsConnect();
        }


        private void UltrasonicCOMConnectButton_Click(object sender, EventArgs e)
        {
            if (!UltrasonicSerialPort.IsOpen)
            {
                string s = UltrasonicCOMComboBox.Text;
                try
                {
                    int i = int.Parse(s);
                    if ((i >= 1) && (i < 255))
                    {
                        s = "COM" + i.ToString();
                        UltrasonicSerialPort.PortName = s;
                        UltrasonicSerialPort.DtrEnable = true; // ОБЯЗАТЕЛЬНО УСТАНАВЛИВАТЬ!  ИНАЧЕ ДАННЫЕ НЕ БУДУТ ПРИХОДИТЬ!!!!
                        //MainControlSerialPort.Encoding = Encoding.UTF8;
                        UltrasonicSerialPort.Open();
                        if (UltrasonicSerialPort.IsOpen)
                        {
                            PrintUltrasonicLog("Соединение по " + s + " с контроллером установлено");
                        };
                    }
                }
                catch
                {
                    PrintUltrasonicLog("Ошибка соединения с контроллером");
                    MessageBox.Show("Не удалось открыть соединение. Проверьте идентификатор порта и подключение контроллера");

                }
            }
            else // Порт был открыт, закрываем
            {
                IndicatorsSerialPort.Close();
                PrintUltrasonicLog("Соединение с контроллером закрыто");
            }
            // Проверяем включённость соединения и настраиваем контролы
            CheckAndViewUltrasonicConnect();
        }

        private void SteeringBoardSendCommandButton_Click(object sender, EventArgs e)
        {
            string s_com = SteeringBoardCMDTextBox.Text;
            //SendTextCommandToSteeringBoard(s_com); // + "\n");
            SendTextCommandToSteeringBoardUDP(s_com);
            // Отладка
            //PrintControlBoardLog("Send To CB:" + s_com);
            SteeringBoardCMDTextBox.Text = "";
        }

        private void SteeringBoardCMDTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter)
            {
                string s_com = SteeringBoardCMDTextBox.Text;
                SendTextCommandToSteeringBoardUDP(s_com);
                SteeringBoardCMDTextBox.Text = "";
            }
        }

        private void WheelEncodersSendCommandButton_Click(object sender, EventArgs e)
        {
            string s_com = WheelEncodersCMDTextBox.Text;
            //WheelEncodersSerialPort.Write(s_com); // + "\n");
            //WheelEncodersSerialPort.Write(s_com); // + "\n");
            //WheelEncodersSerialPort.Write(s_com); // + "\n");
            SendTextCommandToEncodersgBoardUDP(s_com);
            // Отладка
            PrintWheelEncodersReportLog("Send To CB:" + s_com);
            WheelEncodersCMDTextBox.Text = "";
        }

        private void WheelEncodersCMDTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter)
            {
                string s_com = WheelEncodersCMDTextBox.Text;
                if (WheelEncodersSerialPort.IsOpen)
                {
                    WheelEncodersSerialPort.Write(s_com); // + "\n");
                    // Отладка
                    PrintWheelEncodersReportLog("Send To CB:" + s_com);
                }

                WheelEncodersCMDTextBox.Text = "";
            }
        }

        private void WheelEncodersCOMConnectButton_Click(object sender, EventArgs e)
        {
            if (!WheelEncodersSerialPort.IsOpen)
            {
                string s = WheelEncodersCOMTextBox.Text;
                try
                {
                    int i = int.Parse(s);
                    if ((i >= 1) && (i < 255))
                    {
                        s = "COM" + i.ToString();
                        WheelEncodersSerialPort.PortName = s;
                        WheelEncodersSerialPort.DtrEnable = true; // ОБЯЗАТЕЛЬНО УСТАНАВЛИВАТЬ!  ИНАЧЕ ДАННЫЕ НЕ БУДУТ ПРИХОДИТЬ!!!!
                        //MainControlSerialPort.Encoding = Encoding.UTF8;
                        WheelEncodersSerialPort.Open();
                        if (WheelEncodersSerialPort.IsOpen)
                        {
                            PrintWheelEncodersReportLog("Соединение по " + s + " с контроллером установлено");
                        };
                    }
                }
                catch
                {
                    PrintWheelEncodersReportLog("Ошибка соединения с контроллером");
                    MessageBox.Show("Не удалось открыть соединение. Проверьте идентификатор порта и подключение контроллера");

                }
            }
            else // Порт был открыт, закрываем
            {
                WheelEncodersSerialPort.Close();
                PrintWheelEncodersReportLog("Соединение с контроллером закрыто");
            }
            // Проверяем включённость соединения и настраиваем контролы
            CheckAndViewWheelEncodersConnect();
        }

        private void ProcessNewMissionPoint(int id)  // обрабатываем указанную точку миссии как текущую
        {
            if (MissionPointsData != null)
            {
                if ((id >= 0) && (id < MissionPointsData.Length))               
                {
                    if (MissionPointsData[id].pointtype == 2) // эквивалентно требованию остановки. Тип точки = 2
                    {
                        MissionControlPointLongitudeTextBox.Text = "S";
                        MissionControlPointLattitudeTextBox.Text = "S";
                        TargetPointLongitudeTextBox.Text = "S";
                        TargetPointLattitudeTextBox.Text = "S";
                        MissionControlPointIDTextBox.Text = id.ToString();
                        // Прекращаем движение
                        string s_com = "S";
                        SendCommandToControlBoardUDP(s_com);
                        PrintMissionReportLog("Остановка миссии по команде");
                    }
                    else
                    {
                        MissionPointLongitude = MissionPointsData[id].longitude;
                        MissionControlPointLongitudeTextBox.Text = MissionPointLongitude.ToString();

                        MissionPointLattitude = MissionPointsData[id].lattitude;
                        MissionControlPointLattitudeTextBox.Text = MissionPointLattitude.ToString();

                        MissionPointCourse = MissionPointsData[id].cource;
                        MissionControlPointCourceTextBox.Text = MissionPointCourse.ToString();

                        MissionPointTypeTextBox.Text = MissionPointsData[id].pointtype.ToString();

                        MissionPointTaskTextBox.Text = MissionPointsData[id].task.ToString();

                        MissionControlPointIDTextBox.Text = id.ToString();
                        MissionPointID = id;
                    }

                }
                else
                {
                    PrintMissionReportLog("Bad control point data: " + id.ToString());
                }
            }
            else
            {
                PrintMissionReportLog("No mission data");
            }
        }

        private void NextMissionPoint(bool neednextpoint) // анализируем следующую контольную точку
        {
            int L = MissionLength;
            if (neednextpoint)
            { 
                MissionPointID++; // к 
            }
            if (MissionPointID < L)
            {                
                ProcessNewMissionPoint(MissionPointID);
            }
            else
            {
                string s_com = "S";
                SendCommandToControlBoardUDP(s_com);
                MissionActive = false;
                PrintMissionReportLog("Миссия завершена");
            }
        }

        private void StartMission()
        {
            if (!STOPModeActive)  // Если не активирован режим СТОП
            {
                SendTextCommandToSteeringBoardUDP("c"); // <--- сбросить энкодеры, если это возможно
                // Миссия стартет только в режиме паузы
                PAUSEModeActive = true;
                SendCommandToControlBoardUDP("S");  // Можно стартовать миссию только на остановленной машине
                // Обновляем отсечку времени
                MissionLastBrakingTimeStamp = GetNowInSeconds();
                MissionEnableMovingTimeStamp = GetNowInSeconds() + MissionMovingTimeOut;  // прибавляем 5 секунд к текущему времени

                // Загрузка параметров мисси
                if (MissionPointsData != null)
                {
                    MissionLength = MissionPointsData.Length;                

                    if (MissionLength > 0)
                    {
                        MissionTimer.Enabled = true;

                        PrintMissionReportLog("Start mission");
                        PrintMissionReportLog("Параметры миссии: " + MissionLength.ToString() + " шагов");
                        PrintMissionReportLog("Нажмите R для начала движения");
                        StartMissionButton.Enabled = false;
                        LoadMissionButton.Enabled = false;
                        // физически стартуем миссию
                        MissionActive = true;

                        MissionTime = GetNowInSeconds();
                        MissionManevrTimer = 0; // бесконечное давно
                        MissionPointID = 0;
                        // s = CurrentMissionListBox.Items[MissionPointID].ToString();
                        ProcessNewMissionPoint(MissionPointID);                    
                    }
                    else
                    {
                        PrintMissionReportLog("Нет данных миссии");
                    }
                }
                else
                {
                    PrintMissionReportLog("Миссия не загружена");
                }
            }
            else
            {
                PrintMissionReportLog("Активен режим СТОП. Запустить миссию нельзя");
            }
        }

        private void StopMission()
        {
            //MissionTimer.Enabled = false;            
            string s_com = "A";
            SendCommandToControlBoardUDP(s_com); // + "\n");            
            PrintMissionReportLog("Send To CB:" + s_com);
            PrintMissionReportLog("Stop mission");
            MissionActive = false;
            StartMissionButton.Enabled = true;
            LoadMissionButton.Enabled = true;
        }

        private void StartMissionButton_Click(object sender, EventArgs e)
        {
            StartMission();
        }

        private void StopMissionButton_Click(object sender, EventArgs e)
        {
            StopMission();
        }


        private void RightSonarsValueTextBox_TextChanged(object sender, EventArgs e)
        {

        }


        void FinishMission() // финализация миссии
        {
            string s_com = "";
            s_com = "S";
            SendCommandToControlBoardUDP(s_com); // + "\n");            
            PrintMissionReportLog("Send To CB:" + s_com);
            PrintMissionReportLog("Finish mission");

            MissionActive = false;            
            toolStripStatusLabel1.Text = "Mission stopped";
        }


        private void MissionTimer_Tick(object sender, EventArgs e)
        {
            // ConvertStrTodoubleOrZero(s_point);
            string sp_Longitude = ""; double fp_Longitude = 0;
            string sp_Lattitude = ""; double fp_Lattitude = 0;
            string cp_Longitude = ""; double cfp_Longitude = 0;
            string cp_Lattitude = ""; double cfp_Lattitude = 0;
            double r = 0;
            if (MissionActive)
            {
                if (MissionControlPointLongitudeTextBox.Text == "S") // остановка миссии
                {

                    //MissionActive = false;
                    //string s_com = "S";
                    //SendCommandToControlBoardUDP(s_com);

                    StopMission();  // более современное решение

                }
                else // Продолжение обработки миссии
                {
                    sp_Longitude = MissionControlPointLongitudeTextBox.Text;
                    fp_Longitude = ConvertStrToDoubleOrZero(sp_Longitude);
                    sp_Lattitude = MissionControlPointLattitudeTextBox.Text;
                    fp_Lattitude = ConvertStrToDoubleOrZero(sp_Lattitude);
                    cp_Longitude = RTKCurrentLongitudeTextBox.Text;
                    cfp_Longitude = ConvertStrToDoubleOrZero(cp_Longitude);
                    cp_Lattitude = RTKCurrentLattitudeTextBox.Text;
                    cfp_Lattitude = ConvertStrToDoubleOrZero(cp_Lattitude);
                    double d_Longitude = fp_Longitude - cfp_Longitude;
                    double d_Lattitude = fp_Lattitude - cfp_Lattitude;
                    // Вычисляем расстояние до точки

                    // Вычисляем расстояние 
                    double l_Longitude = d_Longitude * ConvertStrToDoubleOrZero(LongitudeCoefTextBox.Text);
                    double l_Lattitude = d_Lattitude * ConvertStrToDoubleOrZero(LattitudeCoefTextBox.Text);
                    double ToPointDistance = (double)Math.Sqrt(l_Longitude * l_Longitude + l_Lattitude * l_Lattitude);
                    MissionControDistanceTextBox.Text = ToPointDistance.ToString();
                    // Вычисляем требуемый курс на точку
                    if (ToPointDistance > 0) // Если рассояние > 0
                    {
                        r = (Math.Acos(l_Longitude / ToPointDistance)) * 180 / Math.PI; //((double)Math.Atan(l_Longitude / l_Lattitude)) * 180 / (double)Math.PI;
                        // Получили угол по отношению к востоку, пересчитываем с поправкой "на север"
                        if (l_Lattitude > 0)
                        {
                            if (r <= 90)  //  угол в верхней правой четверти
                            {
                                r = 90 - r;
                            }
                            else // угол в верхней левой четверти
                            {
                                r = 360 - (r - 90); // нужно отнять лишние 90 градусов
                            }
                        }
                        else
                        {
                            if (r <= 90) // угол в нижней правой четверти
                            {
                                r = 90 + r; // "доворачиваем" на 90 градусов
                            }
                            else // угол в нижней левой четверти
                            {
                                r = 90 + r; // "доворачиваем" на 90 градусов
                            }
                            //
                        }
                    }
                    else  // если расстояние - 0, то курс "на восток"
                    {
                        r = 90;
                    }
                    MissionControlVectorTextBox.Text = r.ToString();
                    // Рассчитываем доворот руля
                    double currentcource = ConvertStrToDoubleOrZero(RTKCurrentCourceTextBox.Text);
                    double dcource = r - currentcource; // вычислили "сырую" разность
                    if (dcource < -180) // коррекция на "полный круг стрелки", бывает, например, если одно значение 350, а другое 5
                    {
                        dcource = dcource + 360;
                    }
                    else if (dcource > 180)
                    {
                        dcource = dcource - 360;
                    }
                    MissionControlTargetAngleTextBox.Text = dcource.ToString();
                    // проверка препятствий

                    double distL = ConvertStrToDoubleOrZero(FrontObstacleDistanceTextBox.Text);  // Дистанция от лидара 1
                    double distC = ConvertStrToDoubleOrZero(PedestrianObstacleDistanceTextBox.Text);  // Дистанция от лидара 2
                    double distR = ConvertStrToDoubleOrZero(CarObstacleDistanceTextBox.Text);  // Дистанция от лидара 3
                    bool isObstacleDetected = (((distL > 200) && (distL < 4500)) || ((distC > 200) && (distC < 4500)) || ((distR > 200) && (distR < 4500))); // проверяем датчики
                    
                    if (UseManevrCheckBox.Checked)
                    {
                        if (isObstacleDetected)  // если есть препятствие
                        {
                            SendCommandToControlBoardUDP("S");          // останавливаемся                          
                            // активируем секундомер остановки
                            MissionEnableMovingTimeStamp = GetNowInSeconds() + MissionSafeBrakingTimeOut;  // прибавляем 4 секунд к текущему времен
                        }
                    }
                        /*
                        // контроль маневра
                        double nt = GetNowInSeconds(); // текущее время
                        MissionTimerTextBox.Text = (nt - MissionTime).ToString("N1", CultureInfo.InvariantCulture);
                        double dtmanevt = nt - MissionManevrTimer;
                        double dtmanevrMax = ConvertStrToDoubleOrZero(MissionManevrMaxTimerTextBox.Text);
                        if ((ManualSteeringTimeStamp + ManualSteeringTimeOut < nt) && (dtmanevt > dtmanevrMax)) // Таймаут остановки закончился, проверяем возможность движения
                        {
                            if (StartAfterManevrCheckBox.Checked) // если разрешено возобновить движение
                            {
                                if (!(isObstacleDetected))
                                {
                                    SendTextCommandToSteeringBoardUDP("F");  // вперед
                                }
                            }
                        }
                        else
                        {
                            if (isObstacleDetected)  // если есть препятствие
                            {
                                SendTextCommandToSteeringBoardUDP("S"); // резко стоп
                                MissionManevrTimer = GetNowInSeconds(); // текущее время
                                MissionManevrTimerTextBox.Text = MissionManevrTimer.ToString();
                            }
                        } 
                        */


                    // Контроль движения (руление) -----------------------------------------------
                    //    вход:  dcource  - требуемый угол доворота руля
                    if (SendDriveTargetCommandСheckBox.Checked) // Если можно рулить, то рулим
                    {
                        int SteeringPosition = 5;
                        // ################################################### Рулим в зависимости от разницы требуемого и текущего курса
                        if (!AccurateSteeringModeCheckBox.Checked)  // "грубое" руление (шагами)
                        {
                            if (dcource < -20)
                            {
                                SteeringPosition = SteeringFastSelectPositions[1];
                                //SendTextCommandToSteeringBoardUDP("1");
                            }
                            else if (dcource < -15)
                            {
                                SteeringPosition = SteeringFastSelectPositions[2];
                                //SendTextCommandToSteeringBoardUDP("2");
                            }
                            else if (dcource < -8)
                            {
                                SteeringPosition = SteeringFastSelectPositions[3];
                                //SendTextCommandToSteeringBoardUDP("3");
                            }
                            else if (dcource < -4)
                            {
                                SteeringPosition = SteeringFastSelectPositions[4];
                                //SendTextCommandToSteeringBoardUDP("4");
                            }
                            else if (dcource < 4)
                            {
                                SteeringPosition = SteeringFastSelectPositions[5];
                                //SendTextCommandToSteeringBoardUDP("5");
                            }
                            else if (dcource < 8)
                            {
                                SteeringPosition = SteeringFastSelectPositions[6];
                                //SendTextCommandToSteeringBoardUDP("6");
                            }
                            else if (dcource < 15)
                            {
                                SteeringPosition = SteeringFastSelectPositions[7];
                                //SendTextCommandToSteeringBoardUDP("7");
                            }
                            else if (dcource < 20)
                            {
                                SteeringPosition = SteeringFastSelectPositions[8];
                                //SendTextCommandToSteeringBoardUDP("8");
                            }
                            else
                            {
                                SteeringPosition = SteeringFastSelectPositions[9];
                                //SendTextCommandToSteeringBoardUDP("9");
                            }
                        }
                        else  // плавное (точное) руление
                        {
                            SteeringPosition = GetAccurateSteeringValue((int)dcource); 
                        }

                        // Подруливание джойстиком

                        if (UseJoystickForCorrectionTrackInMissionCheckBox.Checked) // Если включен режим подруливания джойстиком в миссии, то используем поправку
                        {
                            // не будем использовать "упругую" конструкцию и просто довернем руль и обрежем лишнее
                            SteeringPosition += JoystrickToSteeringMissionCorrectionValue;
                        }
                        // Обрезаем "перекрут" из-за коррекции джойстиком
                        if (SteeringPosition < SteeringFastSelectPositions[1])
                        {
                            SteeringPosition = SteeringFastSelectPositions[1];
                        }
                        if (SteeringPosition > SteeringFastSelectPositions[9])
                        {
                            SteeringPosition = SteeringFastSelectPositions[9];
                        }
                        // Отправляем на плату
                        SendTextCommandToSteeringBoardUDP(SteeringPosition.ToString());  // Отправляем на плату
                    }


                    // Контроль расстояния до контрольной точки и переключение на следующую
                    string s = MissionControlRadiusTextBox.Text;
                    double pointradius = ConvertStrToDoubleOrZero(s);
                    if (pointradius < 2) 
                    {
                        pointradius = 2;
                    }
                    if (ToPointDistance < pointradius) // если расстояние до точки меньше 6 метров, переключаемся на следующую
                    {
                        // Анализируем активность достигнутой точки
                        if (MissionPointTypeTextBox.Text == "3") // Точка паузы
                        {
                            if (ControlBoardCommandBuffer == "F")  // если ранее машина ехала
                            {
                                SendCommandToControlBoardUDP("S");          // останавливаемся                          
                                // активируем секундомер остановки
                                MissionEnableMovingTimeStamp = GetNowInSeconds() + MissionPointBrakingTimeOut;  // прибавляем 5 секунд к текущему времени                                
                            }
                        }
                        if ((MissionPointTypeTextBox.Text == "1")&&((MissionPointTaskTextBox.Text == "2")||(MissionPointTaskTextBox.Text == "4")))
                        {
                            if (ControlBoardCommandBuffer == "F")  // если ранее машина ехала
                            {
                                SendCommandToControlBoardUDP("S");          // останавливаемся                          
                                // активируем секундомер остановки
                                MissionEnableMovingTimeStamp = GetNowInSeconds() + MissionLongBrakingTimeOut;  // прибавляем много секунд к текущему времени                                
                            }
                        }                            
                        // Загружаем новую точку
                        NextMissionPoint(true);                        
                    }

                    // Контроль возможности начать движение
                    // MissionActive
                    // STOPModeActive
                    // PAUSEModeActive
                    // RUNModeActive
                    if (ControlBoardCommandBuffer == "S")  // ранее был нажат тормоз, двигаться можно только после тормоза
                    {
                        if (MissionActive) // если миссия активна
                        {
                            if ((!STOPModeActive) && (!PAUSEModeActive) && (RUNModeActive)) // если ничего не запрещено и ехать разрешено
                            {
                                double CurrentTime = GetNowInSeconds();
                                if (CurrentTime > MissionEnableMovingTimeStamp)
                                {
                                    SendCommandToControlBoardUDP("F");
                                }
                            }
                            //MissionLastBrakingTimeStamp = GetNowInSeconds();
                            //MissionEnableMovingTimeStamp = GetNowInSeconds() + MissionMovingTimeOut;  // прибавляем 5 секунд к текущему времени
                        }
                    }
                }
            }
            else  // Миссия не активна
            {
                // Ничего не деламе
            }
            // рисуем счетчик
            double LCurrentTime = GetNowInSeconds();
            double LBrakingTimeOut = MissionEnableMovingTimeStamp - LCurrentTime;
            if (LBrakingTimeOut >= 0)
            {
                BrakePauseTimerTextBox.Text = LBrakingTimeOut.ToString("#");
            }
            else
            {
                BrakePauseTimerTextBox.Text = "";
            }
            
        }

        private void label9_Click(object sender, EventArgs e)
        {

        }

        private void LeftSonarValueTextBox_TextChanged(object sender, EventArgs e)
        {

        }

        private void CenterSonarValueTextBox_TextChanged(object sender, EventArgs e)
        {

        }

        private void SwitchToEscapeButton_Click(object sender, EventArgs e)
        {
            SendCommandToControlBoardUDP("E");
        }

        private void PressClutchButton_Click(object sender, EventArgs e)
        {
            SendCommandToControlBoardUDP("X");
        }

        private void PressStopButton_Click(object sender, EventArgs e)
        {
            SendCommandToControlBoardUDP("S");
        }

        private void MonitoringSwitchButton_Click(object sender, EventArgs e)
        {
            SendCommandToControlBoardUDP("M");
        }

        private void SwitchLockModeButton_Click(object sender, EventArgs e)
        {
            SendCommandToControlBoardUDP("l");
        }

        private void SteeringTrackBar_Scroll(object sender, EventArgs e)
        {
            // можно потом реализовать "перетаскивание", но пока именно изменение значения
        }

        private void SteeringTrackBar_ValueChanged(object sender, EventArgs e)
        {

        }


        private void DropSTOPModeStateButton_Click(object sender, EventArgs e)   // Нажата кнопка сброса Аларм-режима (СТОП) 
        {
            SendCommandToControlBoardUDP("Q");
            STOPModeActive = false;
            PAUSEModeActive = true;  // Сброс выводит в режим паузы
            RUNModeActive = false;   // Всё равно нельзя ехать
        }

        private void SwitchToBackwardButton_Click(object sender, EventArgs e)
        {
            SendCommandToControlBoardUDP("R");
        }

        private void SwitchToDStateButton_Click(object sender, EventArgs e)
        {
            SendCommandToControlBoardUDP("D");
        }

        private void SwitchToCStateButton_Click(object sender, EventArgs e)
        {
            SendCommandToControlBoardUDP("P");
        }


        private void SwitchToAStateButton_Click(object sender, EventArgs e)
        {
            SendCommandToControlBoardUDP("A");
        }

        private void SwitchToBStateButton_Click(object sender, EventArgs e)
        {
            SendCommandToControlBoardUDP("B");
        }

        private void SwitchToForwardButton_Click(object sender, EventArgs e)
        {
            SendCommandToControlBoardUDP("F");
        }

        private void PressThrottleButton_Click(object sender, EventArgs e)
        {
            SendCommandToControlBoardUDP("T");
        }

        private void ReleaseThrottleButton_Click(object sender, EventArgs e)
        {
            SendCommandToControlBoardUDP("C");
        }


        private void LoadMissionButton_Click(object sender, EventArgs e)
        {
            string sPath = MissionFileNameTextBox.Text; // @"D:\RoboCrossMission.txt";
            
            // <-- нужно остановить миссию

            MissionLength = 0;
            // очищаем список позиций миссии 
            CurrentMissionListBox.Items.Clear();
            try
            {
                string[] slist = File.ReadAllLines(sPath);
                foreach (string sline in slist)
                {
                    if (sline.Trim() != "") 
                    {
                        CurrentMissionListBox.Items.Add(sline.Trim());
                    }
                    
                } // foreach
                PrintMissionReportLog("Миссия загружена");
                
            }
            catch
            {
                PrintMissionReportLog("Ошибка загрузки миссии");
            }
            //   Загрузка мисиии в массив рабочий массив
            int t = 0;
            string s = MissionFileNameTextBox.Text;
            s = Path.GetExtension(s);
            if ((s == ".CSV") || (s == ".csv"))
            {
                t = 1;
            }
            LoadMissionDataFromFile(MissionFileNameTextBox.Text, t);
            if (!MissionActive)  // 
            {
                MissionPointID = 0;
                NextMissionPoint(false); // загружаем базовую точку 
            }
        }

        private void SaveMissionButton_Click(object sender, EventArgs e)
        {
            if (CurrentMissionListBox.Items.Count > 0)
            {
                string sPath = MissionFileNameTextBox.Text;
                string s = "";
                string ss = "";
                for (int i = 0; i<CurrentMissionListBox.Items.Count; i++)
                {
                    ss = CurrentMissionListBox.Items[i].ToString();
                    s += ss.Trim() + "\n";
                }
                try
                {
                    File.WriteAllText(sPath, s);
                    PrintMissionReportLog("Миссия сохранена");
                }
                catch
                {
                    PrintMissionReportLog("Ошибка сохранения миссии");
                }
            }   
            else
            {
                PrintMissionReportLog("Нельзя сохранить пустую миссию");
            }
        }

        private void SteerToState1Button_Click(object sender, EventArgs e)
        {
            SendTextCommandToSteeringBoardUDP(1.ToString());
        }

        private void SteerToState2Button_Click(object sender, EventArgs e)
        {
            SendTextCommandToSteeringBoardUDP(2.ToString());
        }

        private void SteerToState3Button_Click(object sender, EventArgs e)
        {
            SendTextCommandToSteeringBoardUDP(3.ToString());
        }

        private void SteerToState4Button_Click(object sender, EventArgs e)
        {
            SendTextCommandToSteeringBoardUDP(4.ToString());
        }

        private void SteerToState5Button_Click(object sender, EventArgs e)
        {
            SendTextCommandToSteeringBoardUDP(5.ToString());
        }

        private void SteerToState6Button_Click(object sender, EventArgs e)
        {
            SendTextCommandToSteeringBoardUDP(6.ToString());
        }

        private void SteerToState7Button_Click(object sender, EventArgs e)
        {
            SendTextCommandToSteeringBoardUDP(7.ToString());
        }

        private void SteerToState8Button_Click(object sender, EventArgs e)
        {
            SendTextCommandToSteeringBoardUDP(8.ToString());
        }

        private void SteerToState9Button_Click(object sender, EventArgs e)
        {
            SendTextCommandToSteeringBoardUDP(9.ToString());
        }

        private void SteeringCOMComboBox_DropDown(object sender, EventArgs e)
        {
            char[] charsToTrim = { 'C', 'O', 'M', 'c', 'o', 'm' };
            string[] ports = SerialPort.GetPortNames();
            string portnum = "";
            SteeringCOMComboBox.Items.Clear();
            foreach (string port in ports)
            {
                portnum = port.Trim(charsToTrim);
                SteeringCOMComboBox.Items.Add(portnum);
            }
        }

        private void ControlBoardCOMTextBox_DropDown(object sender, EventArgs e)
        {
            char[] charsToTrim = { 'C', 'O', 'M', 'c', 'o', 'm' };
            string[] ports = SerialPort.GetPortNames();
            string portnum = "";
            ControlBoardCOMTextBox.Items.Clear();
            foreach (string port in ports)
            {
                portnum = port.Trim(charsToTrim);
                ControlBoardCOMTextBox.Items.Add(portnum);
            }
        }

        private void WheelEncodersCOMTextBox_DropDown(object sender, EventArgs e)
        {
            char[] charsToTrim = { 'C', 'O', 'M', 'c', 'o', 'm' };
            string[] ports = SerialPort.GetPortNames();
            string portnum = "";
            WheelEncodersCOMTextBox.Items.Clear();
            foreach (string port in ports)
            {
                portnum = port.Trim(charsToTrim);
                WheelEncodersCOMTextBox.Items.Add(portnum);
            }
        }


        private void UltrasonicCOMComboBox_DropDown(object sender, EventArgs e)
        {
            char[] charsToTrim = { 'C', 'O', 'M', 'c', 'o', 'm' };
            string[] ports = SerialPort.GetPortNames();
            string portnum = "";
            UltrasonicCOMComboBox.Items.Clear();
            foreach (string port in ports)
            {
                portnum = port.Trim(charsToTrim);
                UltrasonicCOMComboBox.Items.Add(portnum);
            }
        }

        private void FrontSensorsCOMTextBox_DropDown(object sender, EventArgs e)
        {
            char[] charsToTrim = { 'C', 'O', 'M', 'c', 'o', 'm' };
            string[] ports = SerialPort.GetPortNames();
            string portnum = "";
            TerminalCOMTextBox.Items.Clear();
            foreach (string port in ports)
            {
                portnum = port.Trim(charsToTrim);
                TerminalCOMTextBox.Items.Add(portnum);
            }
        }


        private void IndicatorsComboBox_DropDown(object sender, EventArgs e)
        {
            char[] charsToTrim = { 'C', 'O', 'M', 'c', 'o', 'm' };
            string[] ports = SerialPort.GetPortNames();
            string portnum = "";
            IndicatorsComboBox.Items.Clear();
            foreach (string port in ports)
            {
                portnum = port.Trim(charsToTrim);
                IndicatorsComboBox.Items.Add(portnum);
            }
        }

        //  Работа с камерой!!!!!
        private void CameraIDConnectButton_Click(object sender, EventArgs e)
        {
            
        }

        private void FrameCaptureTimer_Tick(object sender, EventArgs e)
        {
            
        }

        private void TimeOutTimer_Tick(object sender, EventArgs e)
        {
            if (TimeOutSecondsCounter > 0)
            {
                TimeOutSecondsCounter--;
            }
            else
            {
                //TimeOutSecondsCounter = 0;
                TimeOutTimer.Enabled = false;
            }

        }

        private void StartTimeOutTimer()
        {
            TimeOutSecondsCounter = 5;
            TimeOutTimer.Enabled = true;
        }

        private void StartTimeOutTimer(int ATimeOut)
        {
            if (ATimeOut > 0)
            {
                TimeOutSecondsCounter = ATimeOut;
                TimeOutTimer.Enabled = true;
            }
            else
            {
                TimeOutSecondsCounter = 0;
                TimeOutTimer.Enabled = false;
            }
        }

        private bool CheckTimeOut()
        {
            return (TimeOutSecondsCounter > 0);
        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void CameraIDTextBox_TextChanged(object sender, EventArgs e)
        {

        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            this.Left = 0;
            this.Top = 0;
        }


        // Работа с RTK через UDP ######################################################

        private void Form1_Load(object sender, EventArgs e)
        {
            // Создадим делегата метода распечатки сообщения от удаленного сервера
            myDelegate = new ShowUDPMessage(ShowUDPMessageMethod);
        }

        private void CheckStartStopUDPClient()
        {
            if (udpClient != null)
            {
                RTKUDPStartStopButton.Text = "Отключить";
                RTKUDPLocalIPTextBox.Enabled = false;
                RTKUDPLocalIPTextBox.BackColor = Color.LightGray;
                RTKUDPLocalPortTextBox.Enabled = false;
                RTKUDPLocalPortTextBox.BackColor = Color.LightGray;
            }
            else
            {
                RTKUDPStartStopButton.Text = "Подключить";
                RTKUDPLocalIPTextBox.Enabled = true;
                RTKUDPLocalIPTextBox.BackColor = Color.White;
                RTKUDPLocalPortTextBox.Enabled = true;
                RTKUDPLocalPortTextBox.BackColor = Color.White;
            }
        }

        private void StopUDPClient()
        {
            if ((thread != null) && (udpClient != null))
            {
                thread.Abort();
                udpClient.Close();
                thread = null;
                udpClient = null;
            }
            PrintLog("UDPClient stopped");
            CheckStartStopUDPClient();
        }

        private void StartUDPClient()
        {
            if (thread != null)
            {
                thread.Abort();
            }
            if (udpClient != null)
            {
                udpClient.Close();
            }

            localPort = Int32.Parse(RTKUDPLocalPortTextBox.Text);
            try
            {
                udpClient = new UdpClient(localPort);
                thread = new Thread(new ThreadStart(ReceiveUDPMessage));
                thread.IsBackground = true;
                thread.Start();
                PrintLog("UDPClient started");
            }
            catch
            {
                PrintLog("UDPClient's start failed");
            }
            CheckStartStopUDPClient();
        }


        private void RTKUDPStartStopButton_Click(object sender, EventArgs e)
        {
            if (udpClient == null)
            {
                StartUDPClient();
            }
            else
            {
                StopUDPClient();
            }

        }

        private void ReceiveUDPMessage()
        {
            while (true)
            {
                try
                {

                    IPEndPoint remoteIPEndPoint = new IPEndPoint(IPAddress.Any, 0); // port);
                    byte[] content = udpClient.Receive(ref remoteIPEndPoint);
                    if (content.Length > 0)
                    {
                        string message = Encoding.ASCII.GetString(content);
                        this.Invoke(myDelegate, new object[] { message });
                    }
                }
                catch
                {
                    string errmessage = "RemoteHost lost";
                    this.Invoke(myDelegate, new object[] { errmessage });
                }
            }
        }

        private void ShowUDPMessageMethod(string message)
        {
            string encoderdata = "#" + SteeringValueTextBox.Text.ToString() + "#" + EncoderLeftWheelValueTextBox.Text.ToString() + "#" + EncoderSpeedWheelValueTextBox.Text.ToString() + "#" + CurrentTimeTextBox.Text.ToString();
            PrintRTKDataLog(message + encoderdata);
            // PrintRTKDataLog("Remote >" + message + encoderdata);
            int n = 0;
            double d = 0;
            string[] separators = { "," };
            try
            {
                string sline = message;

                if (sline.Trim() != "")
                {
                    string[] scells = sline.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                    // здесь была бы загрузка программы  :)
                    if (scells.Length >= 16)
                    {
                        string s1 = RTKRAWLongitudeFieldTextBox.Text;
                        int LongitudeFieldN = ConvertStrToIntOrZero(s1);
                        if ((LongitudeFieldN <= 0) || (LongitudeFieldN > 15)) { LongitudeFieldN = 2; };
                        s1 = RTKRAWLattitudeFieldTextBox.Text;
                        int LattitudeFieldN = ConvertStrToIntOrZero(s1);
                        if ((LattitudeFieldN <= 0) || (LattitudeFieldN > 15)) { LattitudeFieldN = 4; };
                        // считываем номер ячейки, если "А", то выбираем автоматически по контенту между 15 и 14
                        s1 = (RTKRAWCourceFieldTextBox.Text).Trim();
                        int CourceFieldN = 15;
                        if (("A" == s1) || ("А" == s1))
                        {
                            CourceFieldN = -1;
                        }
                        else
                        {
                            CourceFieldN = ConvertStrToIntOrZero(s1);
                            if ((CourceFieldN <= 0) || (CourceFieldN > 15)) { CourceFieldN = 15; };
                        }                        
                        string s = scells[LongitudeFieldN];
                        string a = s.Substring(0, 2);
                        string m = s.Substring(2, s.Length-2);
                        double fa = ConvertStrToDoubleOrZero(a);
                        double f = ConvertStrToDoubleOrZero(m) / 60;
                        RTKRAWLattitudeTextBox.Text = s;
                        RTKCurrentLattitudeTextBox.Text = (fa + f).ToString();

                        s = scells[LattitudeFieldN];
                        a = s.Substring(0, 3);
                        m = s.Substring(3, s.Length - 3);
                        fa = ConvertStrToDoubleOrZero(a);
                        f = ConvertStrToDoubleOrZero(m) / 60;
                        
                        RTKRAWLongitudeTextBox.Text = s;
                        RTKCurrentLongitudeTextBox.Text = (fa + f).ToString();

                        if (CourceFieldN < 0)  //  - меньше нуля, это скорее всего "автоматический" режим
                        {
                            s = scells[15]; // StringDotToComma(scells[15]).Trim();
                            if (double.TryParse(s, NumberStyles.Number, invC, out f))
                            {
                                // f = ConvertStrTodoubleOrZero(s);  // <-- не нужно преобразовывать      
                                RTKCurrentCourceTextBox.BackColor = System.Drawing.SystemColors.Control;
                                RTKCurrentCourceTextBox.Text = f.ToString();
                            }
                            else
                            {
                                s = scells[14].Trim(); // StringDotToComma(scells[14].Trim());
                                if (double.TryParse(s, NumberStyles.Number, invC, out f))
                                {
                                    // не нужно преобразовывать
                                    RTKCurrentCourceTextBox.BackColor = Color.LightYellow;  // желтым обозначаем то, что значение взято из альтернативной ячейки
                                    RTKCurrentCourceTextBox.Text = f.ToString();
                                }
                                else
                                {
                                    RTKCurrentCourceTextBox.BackColor = Color.Red;
                                    // RTKCurrentCourceTextBox.Text = ""; // не изменяем значение
                                }
                            }
                        }
                        else  // if (CourceFieldN >= 0) // - не меньше нуля, это выбранное пользователем значение
                        {
                            s = (scells[CourceFieldN]).Trim(); // StringDotToComma((scells[CourceFieldN]).Trim());
                            if (double.TryParse(s, NumberStyles.Number, invC, out f))
                            {
                                if ((f >= 0) && (f <= 360))
                                {
                                    RTKCurrentCourceTextBox.BackColor = System.Drawing.SystemColors.Control;
                                    RTKCurrentCourceTextBox.Text = f.ToString();
                                }     
                                else
                                {
                                    RTKCurrentCourceTextBox.BackColor = Color.LightCoral;
                                    // RTKCurrentCourceTextBox.Text = ""; // не изменяем значение
                                }
                            }
                            else 
                            { 
                                RTKCurrentCourceTextBox.BackColor = Color.LightCoral;
                                // RTKCurrentCourceTextBox.Text = ""; // не изменяем значение                                
                            }                            
                        }
                        RTKRAWCourceTextBox.Text = s;
                    }
                }
            }
            catch
            {
                PrintLog("RTK data parsing fail");
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {

        }

        private void button3_Click_1(object sender, EventArgs e)
        {
            // Копируем долготу
            RTKRAWLongitudeTextBox.Text = "Demo";
            RTKCurrentLongitudeTextBox.Text = RTKStartLongitudeTextBox.Text;
            // Копируем широту
            RTKRAWLattitudeTextBox.Text = "Demo";
            RTKCurrentLattitudeTextBox.Text = RTKStartLattitudeTextBox.Text;
            // Копируем курс
            RTKRAWCourceTextBox.Text = "Demo";
            RTKCurrentCourceTextBox.Text = RTKStartCourseTextBox.Text;


        }

        private void RouteTimer_Tick(object sender, EventArgs e)
        {
            /*  // лучше заполнять таймер значениями только при обработке скорости
            DateTime dt = DateTime.Now;
            double ts = (double)dt.Hour * 3600 + (double)dt.Minute * 60 + (double)dt.Second + (double)dt.Millisecond / 1000; 
            CurrentTimeTextBox.Text = ts.ToString();
             */
        }

        private void CurrentMissionListBox_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void NextMissionPointButton_Click(object sender, EventArgs e)
        {
            NextMissionPoint(true);
        }

        private void RTKCurrentLongitudeTextBox_TextChanged(object sender, EventArgs e)
        {
          
        }

        private void SaveCheckPointButton_Click(object sender, EventArgs e)
        {
            //string s = StringCommaToDot(RTKCurrentLongitudeTextBox.Text) + "," + StringCommaToDot(RTKCurrentLattitudeTextBox.Text) + "," + StringCommaToDot(RTKCurrentCourceTextBox.Text);
            string s = "0;1;0;" + RTKCurrentLongitudeTextBox.Text + ";" + RTKCurrentLattitudeTextBox.Text + ";" + RTKCurrentCourceTextBox.Text + ";5;0";
            CurrentMissionListBox.Items.Add(s);
        }

        private void ShowMissionOnMapButton_Click(object sender, EventArgs e)
        {
            int t = 0;
            string s = MissionFileNameTextBox.Text;
            s = Path.GetExtension(s);
            if ((s == ".CSV")||(s == ".csv"))
            {
                t = 1;
            }
            LoadMissionDataFromFile(MissionFileNameTextBox.Text, t); 
        }

        private void CourceVisualizationTimer_Tick(object sender, EventArgs e)
        {
            
            //  Отображаем курс и направление на точку
            string scource,svec,slen;
            double visscale = CourceVisualizationPictureBox.Width / 20;

            Bitmap WorkingImage = new Bitmap(CourceVisualizationPictureBox.Width, CourceVisualizationPictureBox.Height);
            Graphics g = Graphics.FromImage(WorkingImage);

            Pen yellowpen = new Pen(Color.Yellow, 2);
            Pen bluepen = new Pen(Color.Blue, 2);
            Pen redpen = new Pen(Color.Red, 2);

            Pen blackpen = new Pen(Color.Black, 2);
            Pen graypen = new Pen(Color.LightGray, 1);
            Brush blackbrush = new SolidBrush(Color.Black);
            Brush redbrush = new SolidBrush(Color.Red);

            double mapscale = 1;  // 1 px = 1 m
            int mapscaleid = MissionMapScaleComboBox.SelectedIndex;
            switch (mapscaleid)  
            {
                case -1:
                    break;
                case 0:
                    mapscale = 5;  //  5 px = 1 m
                    break;
                case 1:
                    mapscale = 2;  //  2 px = 1 m
                    break;
                case 2:
                    mapscale = 1; // 1 px = 1 m
                    break;
                case 3:
                    mapscale = 0.2; // 1 px = 5 m
                    break;
                case 4:
                    mapscale = 0.1; // 1 px = 10 m
                    break;
                case 5:
                    mapscale = 0.04; // 1 px = 25 m
                    break;
            }

            // Рисуем курс -------------------
            int centerx = CourceVisualizationPictureBox.Width / 2;
            int centery = CourceVisualizationPictureBox.Height / 2;
            int x = 0;
            int y = 0;
            int x1 = 0;
            int y1 = 0;

            g.Clear(Color.White);
            /*
            scource = RTKRAWCourceTextBox.Text;
            if (scource == "Demo") { scource = RTKCurrentCourceTextBox.Text; }  // если деморежим, то данные курса берем из другого поля
             */
            scource = RTKCurrentCourceTextBox.Text; //  <-- будем брать всегда обработанные данные

            svec = MissionControlVectorTextBox.Text;
            slen = MissionControDistanceTextBox.Text;
            if ((scource != "")&&(svec != "")&&(slen != ""))
            {                
                double dcource = ConvertStrToDoubleOrZero(scource);
                double dvec = ConvertStrToDoubleOrZero(svec);
                double dlen = ConvertStrToDoubleOrZero(slen);
                

                // рисуем курс
                y = centery - (int)(60 * (Math.Cos(dcource * Math.PI / 180)));
                x = centerx + (int)(60 * (Math.Sin(dcource * Math.PI / 180)));
                g.DrawLine(bluepen, centerx, centery, x, y);
                // рисуем целевой вектор
                y = centery - (int)(mapscale * dlen * (Math.Cos(dvec * Math.PI / 180)));
                x = centerx + (int)(mapscale * dlen * (Math.Sin(dvec * Math.PI / 180)));
                g.DrawLine(redpen, centerx, centery, x, y);
                // рисуем целевую точку
                g.FillEllipse(redbrush, x - 4, y - 4, 8, 8);
            //    g.FillEllipse(orangebrush, StartPointX - 4, StartPointY - 4, 8, 8);
            //    g.DrawEllipse(yellowpen, StartPointX - 4, StartPointY - 4, 8, 8);
            }
            
            // Заканчиваем рисование ------------------
            g.Dispose();
            
            // Отрисовываем в PictureBox
            CourceVisualizationPictureBox.Image = WorkingImage;
                        
            // Рисуем миссию на отдельной карте -----------------------
            if (RedrawMissionCheckBox.Checked)
            { 
                DrawMissionAtCourceMap();
            }
             
        }

        private void ClearMissionButton_Click(object sender, EventArgs e)
        {
            if (MissionActive)
            {
                StopMissionButton_Click(sender, e); // если миссия активна, то остановим её
            };
            CurrentMissionListBox.Items.Clear();  // очищаем миссию
        }


        #region Joystick's test procedures

        public void MainForJoystick()
        {
            if ((directInput == null) && (joystick == null))  // если контекст устройства не создавался, тогда активируем
            {
                // Initialize DirectInput
                directInput = new DirectInput();

                // Find a Joystick Guid
                joystickGuid = Guid.Empty;

                foreach (var deviceInstance in directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices))
                    joystickGuid = deviceInstance.InstanceGuid;

                // If Gamepad not found, look for a Joystick
                if (joystickGuid == Guid.Empty)
                    foreach (var deviceInstance in directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AllDevices))
                        joystickGuid = deviceInstance.InstanceGuid;

                // If Joystick not found, throws an error
                if (joystickGuid == Guid.Empty)
                {
                    PrintLog("No joystick/Gamepad found.");
                    //Console.ReadKey();
                    //Environment.Exit(1);
                }

                // Instantiate the joystick
                joystick = new Joystick(directInput, joystickGuid);

                //Console.WriteLine("Found Joystick/Gamepad with GUID: {0}", joystickGuid);
                PrintLog("Found Joystick/Gamepad with GUID: " + Convert.ToString(joystickGuid));

                // Query all suported ForceFeedback effects
                var allEffects = joystick.GetEffects();
                foreach (var effectInfo in allEffects)
                    //Console.WriteLine("Effect available {0}", effectInfo.Name);
                    PrintLog("Effect available " + effectInfo.Name);

                // Set BufferSize in order to use buffered data.
                joystick.Properties.BufferSize = 128;

                // Acquire the joystick
                joystick.Acquire();
            }
        }

        public void StopForJoystick()
        {
            if (joystick != null)
            {
                joystick.Dispose();
                joystick = null; // нужно или нет?
            }
            if (directInput != null)
            {
                directInput.Dispose();
                directInput = null; // нужно или нет?
            }
            joystickGuid = Guid.Empty; // очищаем идентификатор устройства
            PrintLog("Joystick/Gamepad Switch Off");
        }

        private void CheckAndViewJoystickConnect() // функция включает-отключает контролы, ответственные за настройку джойстика
        {
            if (joystickGuid != Guid.Empty)
            {
                JoystickStateTextBox.Text = "Enabled";
                JoystickConnectButton.Text = "Отключить";
            }
            else
            {
                JoystickStateTextBox.Text = "Disabled";
                JoystickConnectButton.Text = "Подключить";
            }
        }

        

        private void JoystickTimer_Tick(object sender, EventArgs e)
        {
            int j_value = 0;  // значение с датчика
            int jc_value = 0; // значение относительно центра
            int steering_com = 500;
            string propulsion_com = "";

            // UpdateStatus();
            // Poll events from joystick
            joystick.Poll();
            var datas = joystick.GetBufferedData();
            foreach (var state in datas)
            {

                // Если включен режим передачи на ардуину, то не выводим отладочные данные
                if (SendJoystickCheckBox.Checked)
                {
                    //  Подумать как и что отправлять на систему управления от джойстика!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                    //
                    //  SendTextCommandToSteeringBoardUDP("5");
                    //  SendCommandToControlBoardUDP("F");
                    //  
                    //  

                    j_value = 0; // нуль - ключ к тому, чтобы не отправлять значения.
                    steering_com = 500; // пустая строка к отправке   - руление
                    propulsion_com = ""; // пустая строка к отправке - движение

                    JoystickOffset jof = state.Offset; // считываем данные из буфера джойстика

                    if (jof == JoystickOffset.Buttons2) // кнопка X  - переключение в режим "В" - предстартовый вперед
                    {
                        j_value = state.Value;
                        if (j_value > 64) { propulsion_com = "B"; } else { propulsion_com = ""; }
                        
                    }
                    else if (jof == JoystickOffset.Buttons3) // кнопка Y  - переключение в режим "F" - движение вперед
                    {
                        j_value = state.Value;
                        if (j_value > 64) { propulsion_com = "F"; } else { propulsion_com = ""; }                         
                    }
                    else if (jof == JoystickOffset.Buttons1) // кнопка B  - переключение в режим "D" - предстартовый назад
                    {
                        j_value = state.Value;
                        if (j_value > 64) { propulsion_com = "D"; } else { propulsion_com = ""; }                         
                    }
                    else if (jof == JoystickOffset.Buttons0) // кнопка A - переключение в режим "R" - движение назад
                    {
                        j_value = state.Value;
                        if (j_value > 64) { propulsion_com = "R"; } else { propulsion_com = ""; }    
                    }
                    else if (jof == JoystickOffset.Buttons5) // кнопка RB - переключение в режим "N" - нейтраль
                    {
                        j_value = state.Value;
                        if (j_value > 64) { propulsion_com = "N"; } else { propulsion_com = ""; }    
                    }
                    else if (jof == JoystickOffset.Buttons4) // кнопка LB - отправка "Release"
                    {
                        j_value = state.Value;
                        if (j_value > 64) { propulsion_com = "X"; } else { propulsion_com = ""; }    
                    }                    
                    else if (jof == JoystickOffset.Buttons6) // кнопка LT - отправка "СТОП" (тормоз)
                    {
                        j_value = state.Value;
                        if (j_value > 64) { propulsion_com = "S"; } else { propulsion_com = ""; } 
                    }
                    else if (jof == JoystickOffset.Buttons7) // кнопка RT - отправка "СТОП" (тормоз)
                    {
                        j_value = state.Value;
                        if (j_value > 64) { propulsion_com = "S"; } else { propulsion_com = ""; } 
                    }
                    else if (jof == JoystickOffset.Buttons9)  // кнопка "start" джойстика  // <--------------- контроль запуска миссии с джойстика
                    {
                        j_value = state.Value;
                        if (AllowJoystickMissionControlCheckBox.Checked)
                        {
                            if (j_value > 64) {
                                StartMissionButton_Click(sender, e);  // виртуально нажимаем кнопку "Запустить миссию" (StartMission) на форме
                            };
                        }
                    }
                    else if (jof == JoystickOffset.Buttons8) // кнопка Back                // <--------------- контроль остановки миссии с джойстика
                    {
                        j_value = state.Value;
                        if (AllowJoystickMissionControlCheckBox.Checked)
                        {
                            if (j_value > 64)
                            {
                                StopMissionButton_Click(sender, e);  // виртуально нажимаем кнопку "Остановить миссию" (StopMission) на форме
                            };
                        }
                    }
                    
                    

                        
                    //else if (jof == JoystickOffset.PointOfViewControllers0)  // контроллер точки зрения
                    
                   
                    else if (jof == JoystickOffset.X)  // стик влево-вправо на левом на джойстике
                    {
                        steering_com = -1;  // по-умолчанию - нет команды
                        JoystrickToSteeringMissionCorrectionValue = 0;  // по умолчанию - нет подруливания
                        j_value = state.Value;
                        jc_value = j_value - 32768; // относитель но центра‬

                        /*
                         
                         DataScaling()
                          
                        int JoystickLowBoundary = 3000;   // нижняя граница чувствительности стика джойстика
                        int JoystickHighBoundary = 62500; // верхняя граница чувствительности стика джойстика
                        int JoystickLowDeadZone = 30767; // нижняя граница мертвой зоны джойстика (у центрального значения 32768)
                        int JoystickHighDeadZone = 34767; // верхняя граница мертвой зоны джойстика (у центрального значения 32768)
                         
 
                        SteeringFastSelectPositions[0] = 500;
                        SteeringFastSelectPositions[1] = 330;
                        SteeringFastSelectPositions[2] = 350;
                        SteeringFastSelectPositions[3] = 400;
                        SteeringFastSelectPositions[4] = 475;
                        SteeringFastSelectPositions[5] = 500;
                        SteeringFastSelectPositions[6] = 525;
                        SteeringFastSelectPositions[7] = 600;
                        SteeringFastSelectPositions[8] = 650;
                        SteeringFastSelectPositions[9] = 670;
                        */
                        
                        if (j_value <= JoystickLowBoundary)
                        {
                            steering_com = SteeringFastSelectPositions[1];    // до упора влево
                            JoystrickToSteeringMissionCorrectionValue = -100; // коррекция на 100% влево
                            ManualSteeringTimeStamp = GetNowInSeconds();
                        }
                        else if (j_value < JoystickLowDeadZone)
                        {
                            steering_com = DataScaling(j_value, JoystickLowBoundary, JoystickLowDeadZone, SteeringFastSelectPositions[1], SteeringFastSelectPositions[5]);  // пересчитываем
                            JoystrickToSteeringMissionCorrectionValue = DataScaling(j_value, JoystickLowBoundary, JoystickLowDeadZone, -100, 0); // коррекция влево
                            ManualSteeringTimeStamp = GetNowInSeconds();
                        }
                        else if (j_value < JoystickHighDeadZone) // мертвая зона
                        {
                            steering_com = SteeringFastSelectPositions[5];  // руль прямо
                            JoystrickToSteeringMissionCorrectionValue = 0; // нет коррекции
                            ManualSteeringTimeStamp = GetNowInSeconds();
                        }
                        else if (j_value < JoystickHighBoundary)
                        {
                            steering_com = DataScaling(j_value, JoystickHighDeadZone, JoystickHighBoundary, SteeringFastSelectPositions[5], SteeringFastSelectPositions[9]);  // пересчитываем
                            JoystrickToSteeringMissionCorrectionValue = DataScaling(j_value, JoystickLowBoundary, JoystickLowDeadZone, 0, 100); // коррекция вправо
                            ManualSteeringTimeStamp = GetNowInSeconds();
                        }
                        else if (j_value >= JoystickHighBoundary)
                        {
                            steering_com = SteeringFastSelectPositions[9];   // до упора вправо
                            JoystrickToSteeringMissionCorrectionValue = 100; // коррекция на 100% вправо
                            ManualSteeringTimeStamp = GetNowInSeconds();
                        }
                        else
                        {
                            j_value = 1;
                            steering_com = SteeringFastSelectPositions[0];  // прямо
                            JoystrickToSteeringMissionCorrectionValue = 0;  // нет коррекции
                            ManualSteeringTimeStamp = GetNowInSeconds();
                        }
                    }                

                    // Отладочный вывод
                    PrintLog(state.ToString());

                    


                    //   Обработали джойстик, теперь нужно отправить на ардуину -----------------------------------                  
                    if (!CStartWithoutJoystick) {
                        if (propulsion_com != "")
                        {
                            SendCommandToControlBoardUDP(propulsion_com + "\n");
                            // Отладка
                            PrintLog("Send To Propulsion Board:" + propulsion_com);                        
                        }
                        if (steering_com > 0)
                        {
                            if (UseJoystickForCorrectionTrackInMissionCheckBox.Checked) // Если включен режим подруливания джойстиком в миссии, то не выполняем руление, а сохраняем поправку
                            {
                                // Отладка
                                PrintLog("Steering correction:" + JoystrickToSteeringMissionCorrectionValue.ToString());
                            } 
                            else
                            {
                                //
                                SendTextCommandToSteeringBoardUDP(steering_com.ToString());

                                //SendTextCommandToSteeringBoardUDP(steering_com + "\n");
                                // Отладка
                                PrintLog("Send To Steering Board:" + steering_com);
                            }                            
                        }
                    }
                    else // Отладочный вывод
                    {
                        if (propulsion_com != "")
                        {                            
                            PrintLog("Drop Propulsion command:" + propulsion_com);                        
                        }
                        if (steering_com != 0)
                        {
                            PrintLog("Drop Steering command:" + steering_com);
                        }
                    }
                }
                else
                {
                    // Отладочный вывод
                    PrintLog(state.ToString());
                    // Обработка осей и кнопок
                    // Offset: X, Value: 32767 Timestamp: 183543444 Sequence: 24
                    //JoystickOffset jof = state.Offset;
                    //string res = Convert.ToString(state.Value);
                }
            }
        }

        private void JoystickConnectButton_Click(object sender, EventArgs e)
        {
            if (joystickGuid == Guid.Empty)
            {
                MainForJoystick();  // пробуем активировать джойстик
                if (joystickGuid != Guid.Empty)
                {
                    JoystickTimer.Enabled = true;
                }
                else
                {
                    JoystickTimer.Enabled = false;
                }
            }
            else
            {
                StopForJoystick();
                JoystickTimer.Enabled = false;
            }
            CheckAndViewJoystickConnect();
        }

        #endregion


        // Процедуры дополнительной отрисовки данных #########################################
        private void MissionFileNameBrowseButton_Click(object sender, EventArgs e)
        {
            MissionOpenFileDialog.FileName = MissionFileNameTextBox.Text;
            MissionOpenFileDialog.Filter = "CSV files (*.csv)|*.CSV|Text files (*.txt)|*.TXT|Any files (*.*)|*.*";  //"Image Files(*.BMP;*.JPG;*.GIF;*.PNG)|*.BMP;*.JPG;*.GIF;*.PNG|All files (*.*)|*.*";
            if ((MissionOpenFileDialog.ShowDialog() == DialogResult.OK)) //если в окне была нажата кнопка "ОК"
            {
                MissionFileNameTextBox.Text = MissionOpenFileDialog.FileName;
            }
        }

        private void CopyMissionPointToCurrentButton_Click(object sender, EventArgs e)
        {
            // копируем данные выбранной точки машрута как текущую точку автомобиля
            string sx = (MissionControlPointLongitudeTextBox.Text).Trim();
            string sy = (MissionControlPointLattitudeTextBox.Text).Trim();
            string sc = (MissionControlPointCourceTextBox.Text).Trim();
            if ((sx != "")&&(sy != "")&&(sc != ""))
            {
                RTKCurrentLongitudeTextBox.Text = sx;
                RTKCurrentLattitudeTextBox.Text = sy;
                RTKCurrentCourceTextBox.Text = sc;
            }
        }

        private void InverseCourceButton_Click(object sender, EventArgs e)
        {
            // Инвертируем курс в записи миссии
            string sline, ss;
            string[] separators = { "," };
            string sc = ""; // курс           
            double CurrentC = 0; 
            //  Заполнить массив данных точек из списка путевых точек миссии
            int n = CurrentMissionListBox.Items.Count;
            if (n>0)
            {
                string[] slines = new string[n];
                for (int i = 0; i<n; i++)
                {
                    slines[i] = CurrentMissionListBox.Items[i].ToString();
                }
                CurrentMissionListBox.Items.Clear();
                for (int i = 0; i < n; i++)
                {
                    // считываем и преобразовываем строку из списка путевых точек
                    sline = slines[i];
                    try
                    {
                        if (sline.Trim() != "")
                        {
                            string[] scells = sline.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                            if (scells.Length >= 6)  // курс - шестое значение в расширенном формате точки ---------------------------------------
                            {
                                sc = scells[5]; // берем значение третьей ячейки
                                CurrentC = ConvertStrToDoubleOrZero(sc);
                                CurrentC = CurrentC + 180;
                                if (CurrentC >= 360)
                                {
                                    CurrentC = CurrentC - 360; // вычитаем цикл
                                }
                                sc = CurrentC.ToString(invC);
                                scells[5] = sc;
                            }
                            sline = String.Join(",", scells);
                            CurrentMissionListBox.Items.Add(sline);
                        }
                        else
                        {
                            CurrentMissionListBox.Items.Add(sline);
                        }
                    }
                    catch
                    {
                        // PrintMissionReportLog("Cource invertion error");
                    }
                }
            }
            
        }

        private void StartStopRTKDataLoggingToFileButton_Click(object sender, EventArgs e)
        {
            if (isRTKLogging) // идет логгирование в файл - останавливаем
            {
                if (RTKLogFile != null)
                {                    
                    try
                    {
                        RTKLogFile.Flush();
                        RTKLogFile.Close();
                        RTKLogFile = null;  // <-- на всякий случай
                        PrintLog("Отключена запись лога RTK");
                    }
                    catch (Exception ex)
                    {
                        PrintLog("Ошибка закрытия файла лога RTK (" + ex.Message + ")");
                    }
                }
                RTKLogFile = null;  // <-- на всякий случай
                isRTKLogging = false;
                StartStopRTKDataLoggingToFileButton.Text = "Включить";
            }
            else // логирование не идет, включаем
            {
                if (RTKLogFile == null)
                {
                    if (System.IO.File.Exists(RTKLogFileName))
                    {
                        try
                        {
                            RTKLogFile = File.AppendText(RTKLogFileName);//new StreamWriter(spath,true);
                            PrintLog("Включена запись лога RTK");
                        }
                        catch (Exception ex)
                        {
                            PrintLog("Ошибка открытия файла лога RTK (" + ex.Message + ")");
                        }
                    }
                    else // файла нет, создаем
                    {
                        try
                        {
                            RTKLogFile = new StreamWriter(RTKLogFileName, true);
                        }
                        catch (Exception ex)
                        {
                            PrintLog("Ошибка открытия файла лога RTK (" + ex.Message + ")");
                        }
                    }

                }
                if (RTKLogFile != null) // проверка, что поток создался
                {
                    isRTKLogging = true;
                    StartStopRTKDataLoggingToFileButton.Text = "Завершить";
                }
                else
                {
                    isRTKLogging = false;
                    StartStopRTKDataLoggingToFileButton.Text = "Включить";
                }
            }
        }

        private void ControlBoardCMDTextBox_TextChanged(object sender, EventArgs e)
        {

        }





        // Процедуры отслеживания движения по энкодерам ######################################


        // Работаем с миникартой #############################################################
        public void ProcessMiniMapScaleFactor(int AScaleFactor)
        {
            MiniMapCourseVectorLength = 15;
            double f = 1;
            if (AScaleFactor <= 0) // 20px = 1m
            {
                f = 20;
                MiniMapCourseVectorLength = 30;
            }
            else if (AScaleFactor == 1) // 10px = 1m
            {
                f = 10;
                MiniMapCourseVectorLength = 20;
            }
            else if (AScaleFactor == 2) // 5px = 1m
            {
                f = 5;
                MiniMapCourseVectorLength = 30;
            }
            else if (AScaleFactor == 3)
            {
                f = 2;
                MiniMapCourseVectorLength = 20;
            }
            else if (AScaleFactor == 4) // 1px = 1m
            {
                f = 1;
                MiniMapCourseVectorLength = 15;
            }
            else if (AScaleFactor == 5) // 1px = 2m
            {
                f = 0.5;
                MiniMapCourseVectorLength = 10;
            }
            else if (AScaleFactor == 6) // 1px = 5m
            {
                f = 0.2;
                MiniMapCourseVectorLength = 8;
            }
            else if (AScaleFactor == 7) // 1px = 10m
            {
                f = 0.1;
                MiniMapCourseVectorLength = 8;
            }
            else                        // 1px = 25m
            {
                f = 0.04;
                MiniMapCourseVectorLength = 8;
            }
            // готовим для отображения
            f *= MiniMapViewportScale;
            // Считаем коэффициенты для рисования миникарты
            GlobalMapXScale_Meter2Pixels = f;
            GlobalMapYScale_Meter2Pixels = f;
            GlobalMapXScale_GPS2Pixels = GlobalMapXScale_GPS2Meters * f;
            GlobalMapYScale_GPS2Pixels = GlobalMapYScale_GPS2Meters * f;
        }

        public void SetMiniMapCenterGlobalPosition(double ALongitude, double ALattitude, double ACourse) // установка координат центра карты, попутно рассчитывает матрицу поворота. Это важно!
        {
            double ARadians = ACourse * Math.PI / 180;
            MiniMapCenterLongitude = ALongitude;
            MiniMapCenterLattitude = ALattitude;
            MiniMapCenterSinC = Math.Sin(ARadians);
            MiniMapCenterCosC = Math.Cos(ARadians);
        }

        public void CalcMiniMapPointCoordsFromGPS(double ALongitude, double ALattitude, out int RX, out int RY) // вычисление экранных координат для точки по глобальным координатам
        {
            // вычитаем глобальные координаты, поворачиваем на нужный угол, затем масштабируем
            ALongitude = ALongitude - MiniMapCenterLongitude;  // Координаты условного центра миникарты, от которого будет рисоваться всё. Там обычно машинка стоит
            ALattitude = ALattitude - MiniMapCenterLattitude;
            double AMX = ALongitude * GlobalMapXScale_GPS2Pixels;
            double AMY = ALattitude * GlobalMapYScale_GPS2Pixels;
            // Матрица поворта 
             double AX = (AMX * MiniMapCenterCosC) - (AMY * MiniMapCenterSinC);
             double AY = (AMX * MiniMapCenterSinC) + (AMY * MiniMapCenterCosC);
            // double AX = (ALongitude) * GlobalMapXScale_GPS2Pixels;
            // double AY = (ALattitude) * GlobalMapYScale_GPS2Pixels;
            // Округляем и выводим
            RX = (int)AX;
            RY = (int)AY;
        }

        public void CalcMiniMapPointCoordsFromGPSFullData(MiniMapMissionPoint APointData, out int RX, out int RY, out int RCX, out int RCY, out int RSX, out int RSY) // вычисление экранных координат для точки, курса и точки стремления по глобальным координатам
        {
        
            // вычитаем глобальные координаты, поворачиваем на нужный угол, затем масштабируем
            double AMX = (APointData.longitude - MiniMapCenterLongitude) * GlobalMapXScale_GPS2Pixels;  // Координаты условного центра миникарты, от которого будет рисоваться всё. Там обычно машинка стоит
            double AMY = (APointData.lattitude - MiniMapCenterLattitude) * GlobalMapYScale_GPS2Pixels;
            double AMCX = AMX - MiniMapCourseVectorLength * Math.Cos(APointData.cource * CGrad2Rad); // нулевой курс смотрит на север, поэтому x - ордината и смотрит в другую сторону
            double AMCY = AMY + MiniMapCourseVectorLength * Math.Sin(APointData.cource * CGrad2Rad); // нулевой курс смотрит на север, поэтому 
            // Матрица поворта 
            double AX = (AMX * MiniMapCenterCosC) - (AMY * MiniMapCenterSinC);
            double AY = (AMX * MiniMapCenterSinC) + (AMY * MiniMapCenterCosC);
            double ACX = (AMCX * MiniMapCenterCosC) - (AMCY * MiniMapCenterSinC);
            double ACY = (AMCX * MiniMapCenterSinC) + (AMCY * MiniMapCenterCosC);
            // double AX = (ALongitude) * GlobalMapXScale_GPS2Pixels;
            // double AY = (ALattitude) * GlobalMapYScale_GPS2Pixels;
            // Округляем и выводим
            RX = (int)AX;
            RY = (int)AY;
            RCX = (int)ACX;
            RCY = (int)ACY;
            RSX = 0;
            RSY = 0;
        }

        public void DrawMissionAtCourceMap()  // отрисовка миникарты
        {

            ProcessMiniMapScaleFactor(MiniMapScaleFactor);  // масштабирование карты

            int LMapRenderType = MiniMapRenderTypeComboBox.SelectedIndex;
            if (LMapRenderType < 0)
            {
                LMapRenderType = 0;
            }

            //  Получаем координаты и подсчитываем матрицу поворота
            bool scorrect = false;
            double dcource = 0;
            double dlattitude = 0;
            double dlongitude = 0;

            string slongitude;
            string slattitude;
            string scource;
            if (MiniMapMarkerPosComboBox.SelectedIndex == 0)
            {
                slongitude = RTKCurrentLongitudeTextBox.Text; //  <-- будем брать всегда обработанные данные
                slattitude = RTKCurrentLattitudeTextBox.Text;
                scource = RTKCurrentCourceTextBox.Text;
            }
            else
            {
                slongitude = MissionControlPointLongitudeTextBox.Text; //  <-- будем брать всегда обработанные данные
                slattitude = MissionControlPointLattitudeTextBox.Text;
                scource = MissionControlPointCourceTextBox.Text;
                scource = MissionControlPointCourceTextBox.Text;
                scource = MissionControlPointCourceTextBox.Text;
            }

            if ((scource != "") && (slongitude != "") && (slattitude != ""))
            {
                dcource = ConvertStrToDoubleOrZero(scource);
                dlattitude = ConvertStrToDoubleOrZero(slattitude);
                dlongitude = ConvertStrToDoubleOrZero(slongitude);
                SetMiniMapCenterGlobalPosition(dlongitude, dlattitude, dcource); // вычисляем сдвиг и матрицу поворота
                scorrect = true; // данные корректны
            }
            
            // Получим данные курсовой точки к которой едем (выбрана в логике движения)
            bool ShowDirectionPoint = false;
            slongitude = MissionControlPointLongitudeTextBox.Text; //  <-- будем брать всегда обработанные данные
            slattitude = MissionControlPointLattitudeTextBox.Text;
            scource = MissionControlPointCourceTextBox.Text;
            double directioncource = ConvertStrToDoubleOrZero(scource);
            double directionlattitude = ConvertStrToDoubleOrZero(slattitude);
            double directionlongitude = ConvertStrToDoubleOrZero(slongitude);
            if ((directionlongitude != 0) && (directionlattitude != 0))
            {
                ShowDirectionPoint = true;
            }
            // Непосредственно рисование
            Bitmap MissionImage = new Bitmap(DrawMissionPictureBox.Width, DrawMissionPictureBox.Height);
            Graphics MissionG = Graphics.FromImage(MissionImage);

            Pen yellowpen = new Pen(Color.Yellow, 2);
            Pen bluepen = new Pen(Color.Blue, 2);
            Pen redpen = new Pen(Color.Red, 2);
            Pen greenpen = new Pen(Color.Green, 2);

            Pen blackpen = new Pen(Color.Black, 2);
            Pen graypen = new Pen(Color.LightGray, 1);
            Brush blackbrush = new SolidBrush(Color.Black);
            Brush redbrush = new SolidBrush(Color.Red);

            Font basefont = new Font("Arial", 8);

            // Рисуем путевые точки миссии -------------
            MissionG.Clear(Color.White);
            
            int x = 0;
            int y = 0;
            int x1 = 0;
            int y1 = 0;
            double lx = 0;
            double ly = 0;
            int CourceMapWidth = DrawMissionPictureBox.Width;
            int CourceMapHeight = DrawMissionPictureBox.Height;
            int centerx = CourceMapWidth / 2;
            int centery = CourceMapHeight / 2;
            int markerCenterX = centerx + (int)CourceMapMarkerPositionX;
            int markerCenterY = centery + (int)CourceMapMarkerPositionY;

            if (scorrect) // если GPS координаты корректны, то можно рисовать
            {
                // Рисуем миссию ------------------------------

                if (MissionPointsData != null)  // проверяем наличие массива путевых точек
                {
                    int MissionPointsCount = MissionPointsData.Length;
                    if (MissionLength <= MissionPointsCount)
                    {
                        for (int i = 0; i < MissionLength; i++)
                        {
                            if (LMapRenderType == 0)
                            { 
                                lx = MissionPointsData[i].longitude;
                                ly = MissionPointsData[i].lattitude;
                                CalcMiniMapPointCoordsFromGPS(lx, ly, out x1, out y1);
                                x = markerCenterX + x1;
                                y = markerCenterY - y1;  // У координата растет вниз на экране!!!
                                MissionG.DrawEllipse(redpen, x - 4, y - 4, 8, 8);
                            }
                            else if (LMapRenderType == 1) // коды точек
                            {
                                lx = MissionPointsData[i].longitude;
                                ly = MissionPointsData[i].lattitude;
                                CalcMiniMapPointCoordsFromGPS(lx, ly, out x1, out y1);
                                x = markerCenterX + x1;
                                y = markerCenterY - y1;  // У координата растет вниз на экране!!!
                                MissionG.DrawString(MissionPointsData[i].pointtype.ToString(), basefont, blackbrush, x - 4, y - 4);
                            }
                            else if (LMapRenderType == 2) // номера задач
                            {
                                lx = MissionPointsData[i].longitude;
                                ly = MissionPointsData[i].lattitude;
                                CalcMiniMapPointCoordsFromGPS(lx, ly, out x1, out y1);
                                x = markerCenterX + x1;
                                y = markerCenterY - y1;  // У координата растет вниз на экране!!!
                                MissionG.DrawString(MissionPointsData[i].task.ToString(), basefont, blackbrush, x - 4, y - 4);
                            }

                        }
                    }
                }
                if (ShowDirectionPoint)
                {
                    CalcMiniMapPointCoordsFromGPS(directionlongitude, directionlattitude, out x1, out y1);
                    x = markerCenterX + x1;
                    y = markerCenterY - y1;  // У координата растет вниз на экране!!!
                    MissionG.DrawEllipse(greenpen, x - 6, y - 6, 12, 12);

                }

            }  // заканчиваем рисование
            // Отрисовываем маркер положения машины -----------------------
            
            x = markerCenterX; y = markerCenterY;
            x1 = markerCenterX - 10; y1 = markerCenterY;
            MissionG.DrawLine(redpen, x, y, x1, y1);
            x = markerCenterX - 10; y = markerCenterY;
            x1 = markerCenterX; y1 = markerCenterY - 25;
            MissionG.DrawLine(redpen, x, y, x1, y1);
            x = markerCenterX; y = markerCenterY - 25;
            x1 = markerCenterX + 10; y1 = markerCenterY;
            MissionG.DrawLine(redpen, x, y, x1, y1);
            x = markerCenterX + 10; y = markerCenterY;
            x1 = markerCenterX; y1 = markerCenterY;
            MissionG.DrawLine(redpen, x, y, x1, y1);
            
            // Координатная система -------------------
            if (scorrect)
            {
                MissionG.DrawString(dlongitude.ToString(), basefont, blackbrush, new PointF(10, 10));
                MissionG.DrawString(dlattitude.ToString(), basefont, blackbrush, new PointF(10, 20));
                MissionG.DrawString(dcource.ToString(), basefont, blackbrush, new PointF(10, 30));
            }
            else
            {
                MissionG.DrawString("No position", basefont, blackbrush, new PointF(10, 10));
            }
            // Заканчиваем рисование ------------------            
            MissionG.Dispose();
            // Отрисовываем в PictureBox
            DrawMissionPictureBox.Image = MissionImage;

             
        }

        public void ShowMiniMapScaleFactor()
        {
            if (MiniMapScaleFactor<0)
            {
                MiniMapScaleFactor = 0;
            }
            else if (MiniMapScaleFactor>8)
            {
                MiniMapScaleFactor = 8;
            }
            switch (MiniMapScaleFactor)
            {
                case 0: ScaleFactorViewTextBox.Text = "20px= 1m";
                    break;
                case 1: ScaleFactorViewTextBox.Text = "10px= 1m";
                    break;
                case 2: ScaleFactorViewTextBox.Text = "5px = 1m";
                    break;
                case 3: ScaleFactorViewTextBox.Text = "2px = 1m";
                    break;
                case 4: ScaleFactorViewTextBox.Text = "1px = 1m";
                    break;
                case 5: ScaleFactorViewTextBox.Text = "1px = 2m";
                    break;
                case 6: ScaleFactorViewTextBox.Text = "1px = 5m";
                    break;
                case 7: ScaleFactorViewTextBox.Text = "1px =10m";
                    break;
                case 8: ScaleFactorViewTextBox.Text = "1px =25m";
                    break;
                default: ScaleFactorViewTextBox.Text = "undefined";
                    break;
            }  // switch                            
        }

        private void MapScaleUpButton_Click(object sender, EventArgs e)
        {
            MiniMapScaleFactor--;
            if (MiniMapScaleFactor<0)
            {
                MiniMapScaleFactor = 0;
            }
            ShowMiniMapScaleFactor();
        }

        private void MapScaleDownButton_Click(object sender, EventArgs e)
        {
            MiniMapScaleFactor++;
            if (MiniMapScaleFactor > 8)
            {
                MiniMapScaleFactor = 8;
            }
            ShowMiniMapScaleFactor();
        }

        private void LoadMissionDataFromFile(string sFileName, int sDataFormat)  // загружаем миссию из файла,  sDataFormat == 0 - текстовый (запятая-разделитель, точка-десятичная), sDataFormat == 1 (точка с запятой - разделитель, запятая-десятичная)
        {
            int n1 = 0; string cmd1 = "";
            int n = 0;
            string sline, ss;
            string[] separatorsTXT = { "," };
            string[] separatorsCSV = { ";" };
            string[] scells;
            if (sDataFormat <=0 )
            {
                sDataFormat = 0;
            }
            else
            {
                sDataFormat = 1;
            }
            MissionLength = 0;
            MissionPointsData = null; // Очищаем массив данных миссии
            // Загружаем данные, если они есть
            if (File.Exists(sFileName))
            {
                // Пытаемся считать файл
                try
                {
                    string[] slist = File.ReadAllLines(sFileName);
                    n = slist.Length;

                    if (n > 0)
                    {
                        MissionPointsData = new MiniMapMissionPoint[n]; // массив данных точек для отрисовки миссии
                        for (int i = 0; i < n; i++)
                        {
                            // считываем и преобразовываем строку из списка путевых точек
                            sline = slist[i];
                            if (sline.Trim() != "")  // строка не пустая, обрабатываем её
                            {
                                // обрезаем по комментарий
                                int l = sline.IndexOf("#");
                                if (l >= 0)
                                {
                                    sline = (sline.Substring(0, i)).Trim();
                                }
                                // разбиваем на элементы
                                if (sDataFormat == 0)
                                { 
                                    scells = sline.Split(separatorsTXT, StringSplitOptions.RemoveEmptyEntries);
                                }
                                else
                                {
                                    scells = sline.Split(separatorsCSV, StringSplitOptions.RemoveEmptyEntries);
                                }
                                /*
                                        0 long pointid; // идентификатор путевой точки
                                        1 int pointtype; // тип путевой точки - 0 (нет точки), 1 - обычная точка, 2 - точка S
                                        2 int task; // задача, связана с локацией на квалификации "Зимнего города"  (алгоритм спецобработки)
                                        3 double longitude; // долгота
                                        4 double lattitude; // широта
                                        5 double cource; // курс
                                        6 double pointoffset; // выступание точки стремления вперед (в метрах)
                                        7 double pointshift; // сдвиг точки стремления вправо (в метрах) или влево (при отрицательных значениях)
                                 */
                                if (scells.Length == 3) // данные в старом формате
                                {

                                    MissionPointsData[MissionLength].pointid = (long)MissionLength;
                                    MissionPointsData[MissionLength].pointtype = 1;  
                                    MissionPointsData[MissionLength].task = 0;    
                                    MissionPointsData[MissionLength].longitude = ConvertStrToDoubleOrZero(scells[0].Trim());
                                    MissionPointsData[MissionLength].lattitude = ConvertStrToDoubleOrZero(scells[1].Trim());
                                    MissionPointsData[MissionLength].cource = CourseCycleCorrection(ConvertStrToDoubleOrZero(scells[2].Trim()));
                                    // берем сдвиги по умолчанию
                                    MissionPointsData[MissionLength].pointoffset = 5; // вперед на 5 м
                                    MissionPointsData[MissionLength].pointshift = 0;  // без смещения вбок
                                    // наращиваем счетчик
                                    MissionLength++;
                                }
                                else if (scells.Length >= 6)
                                {
                                    MissionPointsData[MissionLength].pointid = ConvertStrToLongOrZero(scells[0].Trim());
                                    MissionPointsData[MissionLength].pointtype = ConvertStrToIntOrZero(scells[1].Trim());
                                    MissionPointsData[MissionLength].task = ConvertStrToIntOrZero(scells[2].Trim());
                                    MissionPointsData[MissionLength].longitude = ConvertStrToDoubleOrZero(scells[3].Trim());
                                    MissionPointsData[MissionLength].lattitude = ConvertStrToDoubleOrZero(scells[4].Trim());
                                    MissionPointsData[MissionLength].cource = CourseCycleCorrection(ConvertStrToDoubleOrZero(scells[5].Trim()));
                                    if (scells.Length >= 8) // есть поправк на точки стремления
                                    {
                                        MissionPointsData[MissionLength].pointoffset = ConvertStrToDoubleOrZero(scells[6].Trim());
                                        MissionPointsData[MissionLength].pointshift = ConvertStrToDoubleOrZero(scells[7].Trim());
                                        // проверяем и кооректируем сдвиги
                                        if (MissionPointsData[MissionLength].pointoffset < 1) // слишком близко для коррекции
                                        {
                                            MissionPointsData[MissionLength].pointoffset = 1;
                                        }
                                        else if (MissionPointsData[MissionLength].pointoffset > 10) // слишком далеко для коррекции
                                        {
                                            MissionPointsData[MissionLength].pointoffset = 10;
                                        }
                                        if (MissionPointsData[MissionLength].pointshift < -10) // слишком сильно влево
                                        {
                                            MissionPointsData[MissionLength].pointshift = -10;
                                        }
                                        else if (MissionPointsData[MissionLength].pointshift > 10) // слишком сильно вправо
                                        {
                                            MissionPointsData[MissionLength].pointshift = 10;
                                        }
                                    }

                                    // наращиваем счетчик
                                    MissionLength++;
                                }
                                else // нет данных вообще, ничего не вносим
                                {
                                    // Do nothing
                                }
                                
                            }
                        } // for

                    } // if
                    PrintLog("Миссия загружена");
                }
                catch
                {
                    PrintLog("Ошибка загрузки миссии");
                }
            }
            else
            {
                PrintLog("Файл миссии не найден");
            }
        }

        private void MissionOpenFileDialog_FileOk(object sender, CancelEventArgs e)
        {

        }

        private void CheckIndicatorsTimer_Tick(object sender, EventArgs e)
        {
            /*
            P-включить паузу
            W-выключить
            R-включить вперед
            Е-выключить
            S-включить стоп
            Q-выключить
            */
            // Контроль режимов и отображение их на индикаторов
            if (MissionActive)
            {
                MissionIndicatorPanel.BackColor = Color.PaleGreen;
            }
            else
            {
                MissionIndicatorPanel.BackColor = Color.DarkGray;
            }
            if (STOPModeActive)
            {
                STOPIndicatorPanel.BackColor = Color.Red;
                SendCommandToIndicatorsBoard("S");
            }
            else
            {
                STOPIndicatorPanel.BackColor = Color.DarkGray;
                SendCommandToIndicatorsBoard("Q");
            }
            if (PAUSEModeActive)
            {
                PAUSEIndicatorPanel.BackColor = Color.Yellow;
                SendCommandToIndicatorsBoard("P");
            }
            else
            {
                PAUSEIndicatorPanel.BackColor = Color.DarkGray;
                SendCommandToIndicatorsBoard("W");
            }
            if (RUNModeActive)
            {
                RUNIndicatorPanel.BackColor = Color.Green;
                SendCommandToIndicatorsBoard("R");
            }
            else
            {
                RUNIndicatorPanel.BackColor = Color.DarkGray;
                SendCommandToIndicatorsBoard("E");
            }            
        }




        public void SetSTOPMode()  // Применить режим СТОП
        {
            PrintLog("Аварийная остановка");
            STOPModeActive = true;
            PAUSEModeActive = false;
            RUNModeActive = false;
            SendCommandToControlBoardUDP("S");
            SendCommandToControlBoardUDP("T");
            if (MissionActive)
            {
                PrintMissionReportLog("Stop mission");
                MissionActive = false;
                StartMissionButton.Enabled = true;
                LoadMissionButton.Enabled = true;
            }           
        }

        public void SetPAUSEMode()  // Применить режим ПАУЗА
        {
            if (STOPModeActive)
            {
                PrintLog("Активирован режим СТОП. Пауза невозможна");
            }
            else
            { 
                PrintLog("Включена пауза");
                PAUSEModeActive = true;
                RUNModeActive = false;
                SendCommandToControlBoardUDP("S");            
            }
        }

        public void SetRUNMode()  // Применить режим движения
        {
            if (STOPModeActive)
            {
                PrintLog("Активирован режим СТОП. Пуск невозможен");
            }
            else
            {
                if (ControlBoardCommandBuffer == "S")  // ранее был нажат тормоз, двигаться можно только после тормоза
                {
                    if ((MissionActive) && (PAUSEModeActive)) // если миссия активна и если ранее был режим паузы
                    {
                        // активируем секундомер начала движения
                        MissionEnableMovingTimeStamp = GetNowInSeconds() + MissionMovingTimeOut;  // прибавляем 5 секунд к текущему времени
                    }
                }                            
                PrintLog("Включен режим движения");
                PAUSEModeActive = false;
                RUNModeActive = true;

                //SendCommandToControlBoardUDP("S");
            }
        }

        private void TerminalSTOPButton_Click(object sender, EventArgs e)
        {
            SetSTOPMode();
        }

        private void TerminalPAUSEButton_Click(object sender, EventArgs e)
        {
            SetPAUSEMode();
        }

        private void TerminalRUNButton_Click(object sender, EventArgs e)
        {
            SetRUNMode();
        }

        private void HoykoLidarCOMConnectButton_Click(object sender, EventArgs e)
        {
            string URGPortBaudrate = "115200";
            if (!URGSerialPort.IsOpen)  // если нет соединения
            {
                ClearLidarData();  // очищаем данные лидара

                URGPortDataReciveBuffer = "";  // чистим буфер чтения
                
                string s = HoykoLidarCOMComboBox.Text; //URGPortComboBox.Text;
                string s1 = URGPortBaudrate;
                try
                {
                    int i = int.Parse(s);
                    int br = int.Parse(s1);
                    if ((i >= 1) && (i < 255) && (br >= 9600) && (br <= 115200))
                    {
                        s = "COM" + i.ToString();
                        URGSerialPort.PortName = s;
                        URGSerialPort.BaudRate = br; 
                        URGSerialPort.DtrEnable = true; // ОБЯЗАТЕЛЬНО УСТАНАВЛИВАТЬ!  ИНАЧЕ ДАННЫЕ НЕ БУДУТ ПРИХОДИТЬ!!!!
                        //MainControlSerialPort.Encoding = Encoding.UTF8;

                        URGSerialPort.NewLine = "\n\n";
                        URGSerialPort.Open();
                        if (URGSerialPort.IsOpen)
                        {
                            PrintLog("Connection to " + s + " accepted");
                            
                            URG_DropLineIndex = URG_DropLineCount;
                            
                            URGSerialPort.Write(SCIP_Writer.SCIP2());
                            URGSerialPort.ReadLine(); // ?
                            URGSerialPort.Write(SCIP_Writer.MD(URG_StartStep, URG_EndStep));
                            URGSerialPort.ReadLine(); // ?

                            URGReadTimer.Enabled = true;
                        };
                    }
                }
                catch
                {
                    PrintLog("Connection error");
                    //MessageBox.Show("Не удалось открыть соединение. Проверьте идентификатор порта и подключение контроллера");
                }
            }
            else // порт открыт, закрываем
            {
                URGSerialPort.Close();
                PrintLog("Connection closed");
            }
            // Проверяем включённость соединения и настраиваем контролы
            CheckURGSerialPort();

        }

        private void ClearLidarData()
        {
            for (int i = 0; i < URG_EndStep; i++)
            {
                URG_Data[i] = 0;
            }
        }

        private void CheckURGSerialPort()
        {
            if (URGSerialPort.IsOpen)
            {
                HoykoLidarCOMComboBox.Enabled = false;
                HoykoLidarCOMComboBox.BackColor = Color.LightGray;
                //UGRPortBaudrateComboBox.Enabled = false;
                //UGRPortBaudrateComboBox.BackColor = Color.LightGray;
                HoykoLidarCOMConnectButton.Text = "Disconnect";

            }
            else
            {
                URGReadTimer.Enabled = false;

                HoykoLidarCOMComboBox.Enabled = true;
                HoykoLidarCOMComboBox.BackColor = Color.White;
                //UGRPortBaudrateComboBox.Enabled = true;
                //UGRPortBaudrateComboBox.BackColor = Color.White;
                HoykoLidarCOMConnectButton.Text = "Connect";
            }
    }

        private void URGReadTimer_Tick(object sender, EventArgs e)
        {
            // Обработка препятствий
            //FrontObstacleDistanceTextBox
            //PedestrianObstacleDistanceTextBox
            //CarObstacleDistanceTextBox
            // читаем буфер данных и выводим, если есть полные строки
            string answer;
            long s_res = 0;
            string SerialDelim = "\n\n";
            int SerialDelimLength = SerialDelim.Length;

            List<long> distances = new List<long>();
            long time_stamp = 0;

            int i = URGPortDataReciveBuffer.IndexOf(SerialDelim);
            while (i >= 0)
            {
                answer = (URGPortDataReciveBuffer.Substring(0, i)).Trim();
                URGPortDataReciveBuffer = URGPortDataReciveBuffer.Substring(i + SerialDelimLength, URGPortDataReciveBuffer.Length - i - SerialDelimLength);
                //PrintLog(answer);
                if (URG_DropLineIndex > 0)
                {
                    URG_DropLineIndex--;
                }
                else
                {
                    // Обработка строки данных от Serial, "сырые" данные содержатся в answer;

                    int LValue = 0;
                    int LPoint = 0;
                    double LPointStep = 0;
                    int x_center = LidarDataPictureBox.Width / 2;
                    int y_center = LidarDataPictureBox.Height / 2;
                    double DistanceScale = (LidarDataPictureBox.Width / 2) / 6;  // рассчитываем на 6 метров
                    double r_coef = 0.2;
                    double p_sin = 0;
                    double p_cos = 1;
                    int x_point = 200;
                    int y_point = 200;

                    string receive_data = answer;


                    if (receive_data != "")
                    {
                        int LCellCount = URG_Data.Length;

                        if (SCIP_Reader.MD(receive_data, ref time_stamp, ref distances))
                        {

                            int n = distances.Count;
                            if (n == 0) // no data
                            {
                                LidarMessageTextBox.Text = time_stamp.ToString() + "> No points";
                            }
                            else
                            {
                                LidarMessageTextBox.Text = time_stamp.ToString() + "> " + n.ToString() + " points";
                                LPointStep = 20 * (((double)n) / 360); // 
                                // Заполняем грид
                                for (int LIndex = 0; LIndex < LCellCount; LIndex++)
                                {
                                    LPoint = (int)(LIndex * LPointStep);
                                    if (LPoint < n)
                                    {
                                        LValue = (int)distances[LPoint];
                                    }
                                    else
                                    {
                                        LValue = 0;
                                    }
                                    URG_Data[LIndex] = LValue;
                                }

                                

                                
                                //int LStartAngle = TTString.ConvertStrToIntOrZero(DistancesStartAngleTextBox.Text);
                                //int LStopAngle = TTString.ConvertStrToIntOrZero(DistancesStopAngleTextBox.Text);
                                //int LStepAngle = TTString.ConvertStrToIntOrZero(DistancesStepAngleTextBox.Text);

                                int LStartAngle = 0;
                                int LStopAngle = distances.Count - 1;
                                int LStepAngle = 1;
                                double DStartAngle = (double)LStartAngle;
                                double LOneStepAngleResolution = 360 / (LStopAngle + 1);
                                double LOneStepRadianResolution = 2 * Math.PI / (LStopAngle + 1);
                                double LRealAngle = 0;
                                double LRealRadian = 0;
                                double d_res;
                                
                                
                                // Обработка препятствий
                                int LMinDistance = 7000; // семь метров
                                int LFrontObstacleDistance = LMinDistance;
                                int LPedestrianObstacleDistance = LMinDistance;
                                int LCarObstacleDistance = LMinDistance;

                                int LidarCellCount = URG_Data.Length;
                                for (int LIndex = 90; LIndex < 600; LIndex++)
                                {
                                    s_res = URG_Data[LIndex];
                                    d_res = ((double)s_res) * DistanceScale / 1000;  // лидар измеряет в миллиметрах
                                    LRealAngle = (DStartAngle + (LIndex * LOneStepAngleResolution));
                                    //LRealRadian = (DStartAngle + (LIndex * LOneStepRadianResolution));
                                    if ((LRealAngle > 170)&&(LRealAngle < 190)) // дальность вперед
                                    {
                                        if (d_res < LFrontObstacleDistance)
                                        {
                                            LFrontObstacleDistance = (int)d_res;
                                        }
                                    }
                                    if ((LRealAngle > 170) && (LRealAngle < 200)) // дальность до пешеходов
                                    {
                                        if (d_res < LPedestrianObstacleDistance)
                                        {
                                            LPedestrianObstacleDistance = (int)d_res;
                                        }
                                    }
                                    if ((LRealAngle > 180) && (LRealAngle < 220)) // дальность до машины
                                    {
                                        if (d_res < LCarObstacleDistance)
                                        {
                                            LCarObstacleDistance = (int)d_res;
                                        }
                                    }
                                }

                                FrontObstacleDistanceTextBox.Text = LFrontObstacleDistance.ToString();
                                PedestrianObstacleDistanceTextBox.Text = LPedestrianObstacleDistance.ToString();
                                CarObstacleDistanceTextBox.Text = LCarObstacleDistance.ToString();

                                // Рисование

                                if (LStopAngle > n)
                                {
                                    LStopAngle = n;
                                }
                                if (DrawLidarDataCheckBox.Checked)
                                {
                                    Bitmap img = new Bitmap(LidarDataPictureBox.Size.Width, LidarDataPictureBox.Size.Height);
                                    Graphics g = Graphics.FromImage(img);
                                    g.Clear(Color.White);

                                    //  if ((LStartAngle >= 0) && (LStartAngle >= URG_StartStep) && (LStopAngle <= URG_EndStep) && (LStartAngle < LStopAngle) && (LStepAngle > 0))
                                    {
                                        Pen pen = new Pen(Color.Red, 3);

                                        for (int LAngle = LStartAngle; LAngle < LStopAngle; LAngle += LStepAngle)
                                        {
                                            s_res = URG_Data[LAngle];
                                            d_res = ((double)s_res) * DistanceScale / 1000;  // лидар измеряет в миллиметрах
                                            LRealAngle = (DStartAngle + (LAngle * LOneStepAngleResolution));
                                            LRealRadian = (DStartAngle + (LAngle * LOneStepRadianResolution));
                                            //MeasurementsDataGridView

                                            p_sin = Math.Sin(LRealRadian);
                                            p_cos = Math.Cos(LRealRadian);
                                            x_point = x_center + (int)(d_res * p_sin);  // нулевой вектор смотрит вниз!
                                            y_point = y_center + (int)(d_res * p_cos); // нулевой вектор смотрит вниз!

                                            /*
                                            x_center = 0;
                                            y_center = LAngle;
                                            x_point = ((int)s_res) / 10 + 100;
                                            y_point = LAngle;
                                             */
                                            g.DrawLine(pen, x_center, y_center, x_point, y_point);
                                        }
                                    }

                                    g.Dispose();
                                    LidarDataPictureBox.Image = img;
                                }

                            }


                        }
                        else
                        {
                            PrintLog("Bad Lidar data > " + receive_data);
                        }


                    }
                }
                //PrintLog("Raw Lidar data > " + answer);

                // к следующей записи
                i = URGPortDataReciveBuffer.IndexOf(SerialDelim);
            }
        }

        private void ShowMapFormButton_Click(object sender, EventArgs e)
        {
            // Показ всей карты полигона на второй форме
            // (Не реализовано)
        }

        private void button1_Click(object sender, EventArgs e)
        {
            FrontObstacleDistanceTextBox.Text = "3000";
            PedestrianObstacleDistanceTextBox.Text = "3000";
            CarObstacleDistanceTextBox.Text = "3000";
        }

        private void button2_Click(object sender, EventArgs e)
        {
            FrontObstacleDistanceTextBox.Text = "7000";
            PedestrianObstacleDistanceTextBox.Text = "7000";
            CarObstacleDistanceTextBox.Text = "7000";
        }

        private void button4_Click(object sender, EventArgs e)
        {
            SteeringTunePanel.Visible = !SteeringTunePanel.Visible;
            if (SteeringTunePanel.Visible)
            {
                AccurateSteeringLeft90TextBox_Leave(sender, e); // тут отрисовка линии точного руления

                ShowHideSteeringTuneButton.Text = "Скрыть панель настройки руления";
            }
            else
            {

                ShowHideSteeringTuneButton.Text = "Открыть панель настройки руления";
            }
        }

        private void SteeringFastSelectPosition1TextBox_Leave(object sender, EventArgs e) // контроль значения на момент потери фокуса
        {
            int n = ConvertStrToIntOrZero(SteeringFastSelectPosition1TextBox.Text);
            if (n < 100)
            {
                n = 100;
            }
            else if (n > 999)
            {
                n = 999;
            }
            SteeringFastSelectPositions[1] = n;
            SteeringFastSelectPosition1TextBox.Text = n.ToString();
        }

        private void SteeringFastSelectPosition2TextBox_Leave(object sender, EventArgs e)
        {
            int n = ConvertStrToIntOrZero(SteeringFastSelectPosition2TextBox.Text);
            if (n < 100)
            {
                n = 100;
            }
            else if (n > 999)
            {
                n = 999;
            }
            SteeringFastSelectPositions[2] = n;
            SteeringFastSelectPosition2TextBox.Text = n.ToString();
        }

        private void SteeringFastSelectPosition3TextBox_Leave(object sender, EventArgs e)
        {
            int n = ConvertStrToIntOrZero(SteeringFastSelectPosition3TextBox.Text);
            if (n < 100)
            {
                n = 100;
            }
            else if (n > 999)
            {
                n = 999;
            }
            SteeringFastSelectPositions[3] = n;
            SteeringFastSelectPosition3TextBox.Text = n.ToString();
        }

        private void SteeringFastSelectPosition4TextBox_Leave(object sender, EventArgs e)
        {
            int n = ConvertStrToIntOrZero(SteeringFastSelectPosition4TextBox.Text);
            if (n < 100)
            {
                n = 100;
            }
            else if (n > 999)
            {
                n = 999;
            }
            SteeringFastSelectPositions[4] = n;
            SteeringFastSelectPosition4TextBox.Text = n.ToString();
        }

        private void SteeringFastSelectPosition5TextBox_Leave(object sender, EventArgs e)
        {
            int n = ConvertStrToIntOrZero(SteeringFastSelectPosition5TextBox.Text);
            if (n < 100)
            {
                n = 100;
            }
            else if (n > 999)
            {
                n = 999;
            }
            SteeringFastSelectPositions[5] = n;
            SteeringFastSelectPositions[0] = n;  // нулевая позиция - тоже "по центру"
            SteeringFastSelectPosition5TextBox.Text = n.ToString();
        }

        private void SteeringFastSelectPosition6TextBox_Leave(object sender, EventArgs e)
        {
            int n = ConvertStrToIntOrZero(SteeringFastSelectPosition6TextBox.Text);
            if (n < 100)
            {
                n = 100;
            }
            else if (n > 999)
            {
                n = 999;
            }
            SteeringFastSelectPositions[6] = n;
            SteeringFastSelectPosition6TextBox.Text = n.ToString();
        }

        private void SteeringFastSelectPosition7TextBox_Leave(object sender, EventArgs e)
        {
            int n = ConvertStrToIntOrZero(SteeringFastSelectPosition7TextBox.Text);
            if (n < 100)
            {
                n = 100;
            }
            else if (n > 999)
            {
                n = 999;
            }
            SteeringFastSelectPositions[7] = n;
            SteeringFastSelectPosition7TextBox.Text = n.ToString();
        }

        private void SteeringFastSelectPosition8TextBox_Leave(object sender, EventArgs e)
        {
            int n = ConvertStrToIntOrZero(SteeringFastSelectPosition8TextBox.Text);
            if (n < 100)
            {
                n = 100;
            }
            else if (n > 999)
            {
                n = 999;
            }
            SteeringFastSelectPositions[8] = n;
            SteeringFastSelectPosition8TextBox.Text = n.ToString();
        }

        private void SteeringFastSelectPosition9TextBox_Leave(object sender, EventArgs e)
        {
            int n = ConvertStrToIntOrZero(SteeringFastSelectPosition9TextBox.Text);
            if (n < 100)
            {
                n = 100;
            }
            else if (n > 999)
            {
                n = 999;
            }
            SteeringFastSelectPositions[9] = n;
            SteeringFastSelectPosition9TextBox.Text = n.ToString();
        }

        private void VectorMapFileNameBrowseButton_Click(object sender, EventArgs e)
        {
            VectorMapOpenFileDialog.FileName = VectorMapFileNameTextBox.Text;
            VectorMapOpenFileDialog.Filter = "CSV files (*.csv)|*.CSV|Text files (*.txt)|*.TXT|Any files (*.*)|*.*";  //"Image Files(*.BMP;*.JPG;*.GIF;*.PNG)|*.BMP;*.JPG;*.GIF;*.PNG|All files (*.*)|*.*";
            if ((VectorMapOpenFileDialog.ShowDialog() == DialogResult.OK)) //если в окне была нажата кнопка "ОК"
            {
                VectorMapFileNameTextBox.Text = VectorMapOpenFileDialog.FileName;
            }
        }

        private void LoadVectorMapButton_Click(object sender, EventArgs e)
        {
            int n1 = 0; string cmd1 = "";
            int n = 0;
            string sline, ss;
            string[] separatorsCSV = { ";" };
            string[] separatorsTXT = { "," };
            string[] scells;
            string sFileName = VectorMapFileNameTextBox.Text.Trim();
            VectorMapLength = 0;
            VectorMapElementsData = null; // Очищаем массив данных элементов векторной карты
            int sDataFormat = 1;  // обрабатываем CSV

            //
            //
            //
            //
            //


            // Загружаем данные, если они есть
            if (File.Exists(sFileName))
            {
                // Пытаемся считать файл
                try
                {
                    string[] slist = File.ReadAllLines(sFileName);
                    n = slist.Length;

                    if (n > 0)
                    {
                        MissionPointsData = new MiniMapMissionPoint[n]; // массив данных точек для отрисовки миссии
                        for (int i = 0; i < n; i++)
                        {
                            // считываем и преобразовываем строку из списка путевых точек
                            sline = slist[i];
                            if (sline.Trim() != "")  // строка не пустая, обрабатываем её
                            {
                                // обрезаем по комментарий
                                int l = sline.IndexOf("#");
                                if (l >= 0)
                                {
                                    sline = (sline.Substring(0, i)).Trim();
                                }
                                // разбиваем на элементы
                                if (sDataFormat == 0)
                                {
                                    scells = sline.Split(separatorsTXT, StringSplitOptions.RemoveEmptyEntries);
                                }
                                else
                                {
                                    scells = sline.Split(separatorsCSV, StringSplitOptions.RemoveEmptyEntries);
                                }
                                /*
                                        0 long pointid; // идентификатор путевой точки
                                        1 int pointtype; // тип путевой точки - 0 (нет точки), 1 - обычная точка, 2 - точка S
                                        2 int task; // задача, связана с локацией на квалификации "Зимнего города"  (алгоритм спецобработки)
                                        3 double longitude; // долгота
                                        4 double lattitude; // широта
                                        5 double cource; // курс
                                        6 double pointoffset; // выступание точки стремления вперед (в метрах)
                                        7 double pointshift; // сдвиг точки стремления вправо (в метрах) или влево (при отрицательных значениях)
                                 */
                                if (scells.Length == 3) // данные в старом формате
                                {

                                    MissionPointsData[MissionLength].pointid = (long)MissionLength;
                                    MissionPointsData[MissionLength].pointtype = 1;
                                    MissionPointsData[MissionLength].task = 0;
                                    MissionPointsData[MissionLength].longitude = ConvertStrToDoubleOrZero(scells[0].Trim());
                                    MissionPointsData[MissionLength].lattitude = ConvertStrToDoubleOrZero(scells[1].Trim());
                                    MissionPointsData[MissionLength].cource = CourseCycleCorrection(ConvertStrToDoubleOrZero(scells[2].Trim()));
                                    // берем сдвиги по умолчанию
                                    MissionPointsData[MissionLength].pointoffset = 5; // вперед на 5 м
                                    MissionPointsData[MissionLength].pointshift = 0;  // без смещения вбок
                                    // наращиваем счетчик
                                    MissionLength++;
                                }
                                else if (scells.Length >= 6)
                                {
                                    MissionPointsData[MissionLength].pointid = ConvertStrToLongOrZero(scells[0].Trim());
                                    MissionPointsData[MissionLength].pointtype = ConvertStrToIntOrZero(scells[1].Trim());
                                    MissionPointsData[MissionLength].task = ConvertStrToIntOrZero(scells[2].Trim());
                                    MissionPointsData[MissionLength].longitude = ConvertStrToDoubleOrZero(scells[3].Trim());
                                    MissionPointsData[MissionLength].lattitude = ConvertStrToDoubleOrZero(scells[4].Trim());
                                    MissionPointsData[MissionLength].cource = CourseCycleCorrection(ConvertStrToDoubleOrZero(scells[5].Trim()));
                                    if (scells.Length >= 8) // есть поправк на точки стремления
                                    {
                                        MissionPointsData[MissionLength].pointoffset = ConvertStrToDoubleOrZero(scells[6].Trim());
                                        MissionPointsData[MissionLength].pointshift = ConvertStrToDoubleOrZero(scells[7].Trim());
                                        // проверяем и кооректируем сдвиги
                                        if (MissionPointsData[MissionLength].pointoffset < 1) // слишком близко для коррекции
                                        {
                                            MissionPointsData[MissionLength].pointoffset = 1;
                                        }
                                        else if (MissionPointsData[MissionLength].pointoffset > 10) // слишком далеко для коррекции
                                        {
                                            MissionPointsData[MissionLength].pointoffset = 10;
                                        }
                                        if (MissionPointsData[MissionLength].pointshift < -10) // слишком сильно влево
                                        {
                                            MissionPointsData[MissionLength].pointshift = -10;
                                        }
                                        else if (MissionPointsData[MissionLength].pointshift > 10) // слишком сильно вправо
                                        {
                                            MissionPointsData[MissionLength].pointshift = 10;
                                        }
                                    }

                                    // наращиваем счетчик
                                    MissionLength++;
                                }
                                else // нет данных вообще, ничего не вносим
                                {
                                    // Do nothing
                                }

                            }
                        } // for

                    } // if
                    PrintLog("Векторная карта загружена");
                }
                catch
                {
                    PrintLog("Ошибка загрузки векторной карты");
                }
            }
            else
            {
                PrintLog("Файл векторной карты не найден");
            }
        }

        private void CheckAndProcessAcurateSteeringPositions()  // пересчет последовательности для аккуратного руления
        {
            string s = AccurateSteeringLeft90TextBox.Text;
            int NMin = ConvertStrToIntOrZero(s);
            if (NMin < 300) { NMin = 300; }
            s = AccurateSteeringRight90TextBox.Text;
            int NMax = ConvertStrToIntOrZero(s);
            if (NMax > 700) { NMax = 700; }
            double DN = 80 / (double)(700 - 300);
            AccurateSteeringChartYs[0] = 90; // высота по нижней линии
            AccurateSteeringChartYs[1] = 90 - (int)(DN * (NMin - 300));
            AccurateSteeringChartYs[9] = 90 - (int)(DN * (NMax - 300));
            // перебираем значения 
            AccurateSteeringPositions[0] = 500;
            AccurateSteeringPositions[1] = NMin;
            AccurateSteeringPositions[9] = NMax;

            int N = ConvertStrToIntOrZero(AccurateSteeringLeft60TextBox.Text); // влево 60
            if (N < NMin) { N = NMin; }
            if (N > NMax) { N = NMax; }
            AccurateSteeringLeft60TextBox.Text = N.ToString();
            AccurateSteeringPositions[2] = N;
            AccurateSteeringChartYs[2] = 90 - (int)(DN * (N - 300));
            int NLast = N;
            N = ConvertStrToIntOrZero(AccurateSteeringLeft30TextBox.Text); // влево 30
            if (N < NLast) { N = NLast; }
            if (N > NMax) { N = NMax; }
            AccurateSteeringLeft30TextBox.Text = N.ToString();
            AccurateSteeringPositions[3] = N;
            AccurateSteeringChartYs[3] = 90 - (int)(DN * (N - 300));
            NLast = N;
            N = ConvertStrToIntOrZero(AccurateSteeringLeft15TextBox.Text); // влево 15
            if (N < NLast) { N = NLast; }
            if (N > NMax) { N = NMax; }
            AccurateSteeringLeft15TextBox.Text = N.ToString();
            AccurateSteeringPositions[4] = N;
            AccurateSteeringChartYs[4] = 90 - (int)(DN * (N - 300));
            NLast = N;
            N = ConvertStrToIntOrZero(AccurateSteeringForwardTextBox.Text); // прямо
            if (N < NLast) { N = NLast; }
            if (N > NMax) { N = NMax; }
            AccurateSteeringForwardTextBox.Text = N.ToString();
            AccurateSteeringPositions[5] = N;
            AccurateSteeringChartYs[5] = 90 - (int)(DN * (N - 300));
            NLast = N;
            N = ConvertStrToIntOrZero(AccurateSteeringRight15TextBox.Text); // вправо 15
            if (N < NLast) { N = NLast; }
            if (N > NMax) { N = NMax; }
            AccurateSteeringRight15TextBox.Text = N.ToString();
            AccurateSteeringPositions[6] = N;
            AccurateSteeringChartYs[6] = 90 - (int)(DN * (N - 300));
            NLast = N;
            N = ConvertStrToIntOrZero(AccurateSteeringRight30TextBox.Text); // вправо 30
            if (N < NLast) { N = NLast; }
            if (N > NMax) { N = NMax; }
            AccurateSteeringRight30TextBox.Text = N.ToString();
            AccurateSteeringPositions[7] = N;
            AccurateSteeringChartYs[7] = 90 - (int)(DN * (N - 300));
            NLast = N;
            N = ConvertStrToIntOrZero(AccurateSteeringRight60TextBox.Text); // вправо 60
            if (N < NLast) { N = NLast; }
            if (N > NMax) { N = NMax; }
            AccurateSteeringRight60TextBox.Text = N.ToString();
            AccurateSteeringPositions[8] = N;
            AccurateSteeringChartYs[8] = 90 - (int)(DN * (N - 300));
            NLast = N;
            // Теперь отрисовка на чарте
            Bitmap img = new Bitmap(AccurateSteeringChartPictureBox.Width, AccurateSteeringChartPictureBox.Height);
            Graphics g = Graphics.FromImage(img);
            g.Clear(Color.White);
            Pen redpen = new Pen(Color.Red, 2);
            Pen blackpen = new Pen(Color.Black, 2);
            Pen graypen = new Pen(Color.LightGray, 1);
            g.DrawLine(graypen, 30, 10, 530, 10);
            g.DrawLine(graypen, 30, 90, 530, 90);
            //AccurateSteeringChartYs[ 
            for (int i = 2; i < 10; i++)
            {
                g.DrawLine(redpen, AccurateSteeringChartXs[i - 1], AccurateSteeringChartYs[i - 1], AccurateSteeringChartXs[i], AccurateSteeringChartYs[i]);
            }

            g.Dispose();

            AccurateSteeringChartPictureBox.Image = img;
        }

        private void AccurateSteeringLeft90TextBox_Leave(object sender, EventArgs e)
        {
            // пользователь вышел из режима редактирования, нужно пересчитать и сохранить последовательность
            CheckAndProcessAcurateSteeringPositions();
        }

        private int GetAccurateSteeringValue(int Angle) // Возвращается значение поворота руля в зависимости от желаемой коррекции курса в градусах
        {
            int n = 500;
            if (Angle <= -90)
            {
                n = AccurateSteeringPositions[1];
            }
            else if (Angle <= -60)
            {
                n = (Angle - (-90)) * (AccurateSteeringPositions[2] - AccurateSteeringPositions[1]) / 30 + AccurateSteeringPositions[1];  // (90 - 60)
            }
            else if (Angle <= -30)
            {
                n = (Angle - (-60)) * (AccurateSteeringPositions[3] - AccurateSteeringPositions[2]) / 30 + AccurateSteeringPositions[2];  // (60 - 30)
            }
            else if (Angle <= -15)
            {
                n = (Angle - (-30)) * (AccurateSteeringPositions[4] - AccurateSteeringPositions[3]) / 15 + AccurateSteeringPositions[3];  // (30 - 15)
            }
            else if (Angle <= -0)
            {
                n = (Angle - (-15)) * (AccurateSteeringPositions[5] - AccurateSteeringPositions[4]) / 15 + AccurateSteeringPositions[4];  // (0 - 15)
            }
            else if (Angle <= 15)
            {
                n = (Angle) * (AccurateSteeringPositions[6] - AccurateSteeringPositions[5]) / 15 + AccurateSteeringPositions[5];  // (0 - 15)
            }
            else if (Angle <= 30)
            {
                n = (Angle - 15) * (AccurateSteeringPositions[7] - AccurateSteeringPositions[6]) / 15 + AccurateSteeringPositions[6];  // (15 - 30)
            }
            else if (Angle <= 60)
            {
                n = (Angle - 30) * (AccurateSteeringPositions[8] - AccurateSteeringPositions[7]) / 30 + AccurateSteeringPositions[7];  // (30 - 60)
            }
            else if (Angle <= 90)
            {
                n = (Angle - 60) * (AccurateSteeringPositions[9] - AccurateSteeringPositions[8]) / 30 + AccurateSteeringPositions[8];  // (60 - 90)
            }
            else 
            {
                n = AccurateSteeringPositions[9];
            }

            return n;
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            int n = AccurateSteeringTestTrackBar.Value;
            label89.Text = "Угол: " + n.ToString();
            label90.Text = "Значение: " + GetAccurateSteeringValue(n).ToString();
        }

        private void SteeringUDPConnectButton_Click(object sender, EventArgs e)
        {
            if (SteeringUdpClient == null)
            {
                StartSteeringUDPClient();
            }
            else
            {
                StopSteeringUDPClient();
            }
        }

        private void StopSteeringUDPClient()
        {
            if ((SteeringThread != null) && (SteeringUdpClient != null))
            {
                SteeringThread.Abort();
                SteeringUdpClient.Close();
                SteeringThread = null;
                SteeringUdpClient = null;
                aliveSteering = false;
            }

            PrintLog("SteeringUDPClient stopped");
            PrintLog("UDPClient stopped", SteeringReportListBox);
            CheckStartStopUDPClient(SteeringUdpClient, SteeringLocalPort_textBox, SteeringRemoteIP_textBox, SteeringRemotePort_textBox, SteeringBoardCOMConnectButton);
        }

        private void label93_Click(object sender, EventArgs e)
        {

        }

        private void UDPConnectsButton_Click(object sender, EventArgs e)
        {
            UDPConnectsPanel.Visible = !UDPConnectsPanel.Visible;
            if (UDPConnectsPanel.Visible)
            {
                UDPConnectsButton.Text = "Скрыть панель подключения оборудования UDP";
            }
            else
            {

                ShowHideSteeringTuneButton.Text = "Открыть панель подключения оборудования UDP";
            }
        }

        private void EncodersUDPConnectButton_Click(object sender, EventArgs e)
        {
            if (EncodersUdpClient == null)
            {
                StartEncodersUDPClient();
            }
            else
            {
                StopEncodersUDPClient();
            }
        }

        private void StopEncodersUDPClient()
        {
            if ((EncodersThread != null) && (EncodersUdpClient != null))
            {
                EncodersThread.Abort();
                EncodersUdpClient.Close();
                EncodersThread = null;
                EncodersUdpClient = null;
                aliveEncoders = false;
            }

            PrintLog("EncodersUDPClient stopped");
            PrintLog("UDPClient stopped", WheelEncodersReportListBox);
            CheckStartStopUDPClient(EncodersUdpClient, EncodersLocalPort_textBox, EncodersRemoteIP_textBox, EncodersRemotePort_textBox, EncodersUDPConnectButton);
        }

        private void StartEncodersUDPClient()
        {
            if (EncodersThread != null)
            {
                EncodersThread.Abort();
            }
            if (EncodersUdpClient != null)
            {
                EncodersUdpClient.Close();
            }

            EncodersLocalPort = Int32.Parse(EncodersLocalPort_textBox.Text);
            EncodersRemoteIP = IPAddress.Parse(EncodersRemoteIP_textBox.Text);
            EncodersRemotePort = Int32.Parse(EncodersRemotePort_textBox.Text);

            try
            {
                EncodersUdpClient = new UdpClient(EncodersLocalPort);
                EncodersThread = new Thread(new ThreadStart(EncodersReceiveUDPMessage));
                EncodersThread.IsBackground = true;
                aliveEncoders = true;
                EncodersThread.Start();
                PrintLog("EncodersUDPClient started");
                PrintLog("EncodersUDPClient started", WheelEncodersReportListBox);

                SendTextCommandToEncodersgBoardUDP("start");
            }
            catch
            {
                PrintLog("EncodersUDPClient's start failed");
                PrintLog("EncodersUDPClient's start failed", WheelEncodersReportListBox);
            }

            CheckStartStopUDPClient(EncodersUdpClient, EncodersLocalPort_textBox, EncodersRemoteIP_textBox, EncodersRemotePort_textBox, EncodersUDPConnectButton);
        }

        private void EncodersReceiveUDPMessage()
        {
            while (aliveEncoders)
            {
                try
                {
                    IPEndPoint remoteIPEndPoint = new IPEndPoint(EncodersRemoteIP, EncodersRemotePort); // port);
                    byte[] content = EncodersUdpClient.Receive(ref remoteIPEndPoint);
                    if (content.Length > 0)
                    {
                        // "ENCr:4"
                        string message = Encoding.ASCII.GetString(content);
                        BeginInvoke(new Action(() =>
                        {
                            PrintLog(message, WheelEncodersReportListBox);
                        }));
                        WheelEncodersCOMPortDataReciveBuffer += message + "\n";
                    }
                }
                catch
                {
                    string errmessage = "RemoteHost lost";
                    BeginInvoke(new Action(() =>
                    {
                        PrintLog(errmessage, WheelEncodersReportListBox);
                    }));
                }
            }
        }

        private void PropulsiveUDPConnectButton_Click(object sender, EventArgs e)
        {
            if (PropulsiveUdpClient == null)
            {
                StartPropulsiveUDPClient();
            }
            else
            {
                StopPropulsiveUDPClient();
            }
        }

        private void SteeringReceiveUDPMessage()
        {
            while (aliveSteering)
            {
                try
                {
                    IPEndPoint remoteIPEndPoint = new IPEndPoint(SteeringRemoteIP, SteeringRemotePort); // port);
                    byte[] content = SteeringUdpClient.Receive(ref remoteIPEndPoint);
                    if (content.Length > 0)
                    {
                        // "CVLST:free:555:555#"
                        string message = Encoding.ASCII.GetString(content);
                        BeginInvoke(new Action(() =>
                        {
                            PrintLog(message, SteeringReportListBox);
                        }));
                        SteeringBoardComPortDataReciveBuffer += message + "\n";
                    }
                }
                catch
                {
                    string errmessage = "RemoteHost lost";
                    BeginInvoke(new Action(() =>
                    {
                        PrintLog(errmessage, SteeringReportListBox);
                    }));
                }
            }
        }

        private void StartSteeringUDPClient()
        {
            if (SteeringThread != null)
            {
                SteeringThread.Abort();
            }
            if (SteeringUdpClient != null)
            {
                SteeringUdpClient.Close();
            }

            SteeringLocalPort = Int32.Parse(SteeringLocalPort_textBox.Text);
            SteeringRemoteIP = IPAddress.Parse(SteeringRemoteIP_textBox.Text);
            SteeringRemotePort = Int32.Parse(SteeringRemotePort_textBox.Text);

            try
            {
                SteeringUdpClient = new UdpClient(SteeringLocalPort);
                SteeringThread = new Thread(new ThreadStart(SteeringReceiveUDPMessage));
                SteeringThread.IsBackground = true;
                aliveSteering = true;
                SteeringThread.Start();
                PrintLog("SteeringUDPClient started");
                PrintLog("SteeringUDPClient started", SteeringReportListBox);

                SendTextCommandToSteeringBoardUDP("start");
            }
            catch
            {
                PrintLog("SteeringUDPClient's start failed");
                PrintLog("SteeringUDPClient's start failed", SteeringReportListBox);
            }

            CheckStartStopUDPClient(SteeringUdpClient, SteeringLocalPort_textBox, SteeringRemoteIP_textBox, SteeringRemotePort_textBox, SteeringUDPConnectButton);
        }

        private void StopPropulsiveUDPClient()
        {
            if ((PropulsiveThread != null) && (PropulsiveUdpClient != null))
            {
                PropulsiveThread.Abort();
                PropulsiveUdpClient.Close();
                PropulsiveThread = null;
                PropulsiveUdpClient = null;
                alivePropulsive = false;
            }

            PrintLog("PropulsiveUDPClient stopped");
            PrintLog("UDPClient stopped", ControlBoardReportListBox);
            CheckStartStopUDPClient(PropulsiveUdpClient, PropulsiveLocalPort_textBox, PropulsiveRemoteIP_textBox, PropulsiveRemotePort_textBox, PropulsiveUDPConnectButton);
        }

        private void CheckStartStopUDPClient(UdpClient udpClient, TextBox localPort, TextBox remoteIP, TextBox remotePort, Button button)
        {
            if (udpClient != null)
            {
                localPort.Enabled = false;
                localPort.BackColor = Color.LightGray;
                remoteIP.Enabled = false;
                remoteIP.BackColor = Color.LightGray;
                remotePort.Enabled = false;
                remotePort.BackColor = Color.LightGray;

                button.Text = "Отключить";
            }
            else
            {
                localPort.Enabled = true;
                localPort.BackColor = Color.White;
                remoteIP.Enabled = true;
                remoteIP.BackColor = Color.White;
                remotePort.Enabled = true;
                remotePort.BackColor = Color.White;

                button.Text = "Подключить";
            }
        }

        private void StartPropulsiveUDPClient()
        {
            if (PropulsiveThread != null)
            {
                PropulsiveThread.Abort();
            }
            if (PropulsiveUdpClient != null)
            {
                PropulsiveUdpClient.Close();
            }

            PropulsiveLocalPort = Int32.Parse(PropulsiveLocalPort_textBox.Text);
            PropulsiveRemoteIP = IPAddress.Parse(PropulsiveRemoteIP_textBox.Text);
            PropulsiveRemotePort = Int32.Parse(PropulsiveRemotePort_textBox.Text);

            try
            {
                PropulsiveUdpClient = new UdpClient(PropulsiveLocalPort);
                PropulsiveThread = new Thread(new ThreadStart(PropulsiveReceiveUDPMessage));
                PropulsiveThread.IsBackground = true;
                alivePropulsive = true;
                PropulsiveThread.Start();
                PrintLog("PropulsiveUDPClient started");
                PrintLog("PropulsiveUDPClient started", ControlBoardReportListBox);

                SendCommandToControlBoardUDP("start");
            }
            catch
            {
                PrintLog("PropulsiveUDPClient's start failed");
                PrintLog("PropulsiveUDPClient's start failed", ControlBoardReportListBox);
            }

            CheckStartStopUDPClient(PropulsiveUdpClient, PropulsiveLocalPort_textBox, PropulsiveRemoteIP_textBox, PropulsiveRemotePort_textBox, PropulsiveUDPConnectButton);
        }

        private void PropulsiveReceiveUDPMessage()
        {
            while (alivePropulsive)
            {
                try
                {
                    IPEndPoint remoteIPEndPoint = new IPEndPoint(PropulsiveRemoteIP, PropulsiveRemotePort); 
                    byte[] content = PropulsiveUdpClient.Receive(ref remoteIPEndPoint);
                    if (content.Length > 0)
                    {
                        // Ручная настройка "#Ctrl: 0>  205: 135:" 
                        // Нормальные данные "CVL:auto::0# 150-> 400# 150-> 260#"
                        string message = Encoding.ASCII.GetString(content);
                        BeginInvoke(new Action(() =>
                        {
                            PrintLog(message, ControlBoardReportListBox);
                        }));
                        ControlBoardComPortDataReciveBuffer += message + "\n";
                    }
                }
                catch
                {
                    string errmessage = "RemoteHost lost";
                    BeginInvoke(new Action(() =>
                    {
                        PrintLog(errmessage, ControlBoardReportListBox);
                    }));
                }
            }
        }

        private void PrintLog(string s, ListBox listBox)  // <--------------- временно --------------!!!
        {
            listBox.Items.Add(s);
            while (listBox.Items.Count > 50)
            {
                listBox.Items.RemoveAt(0);
            }
            listBox.SelectedIndex = listBox.Items.Count - 1;
            listBox.SelectedIndex = -1;
        }

    }
}
