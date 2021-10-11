/*
Система управления платой контроля движением Робокара Мосполитеха (в конфигурации "Зимний город" 2018) для KIA ProCeed
by Leonov Vladislav 25.10.2020
Сокращенная версия с деактивированным переключением коробки передач

Система управления с 1 x Arduino Mega, 1 x Mega-multi Expansion Shield, 2x Monster Shield, 2 x Troyka Shield, 1 x Ethernet Shield.

"Механическая" калибровка:
  - мотор сцепления: при прямом включении должен выдвигаться
  - потенциометр сцепления: при выдвижении должны возрастать значения
  - мотор тормоза: при прямом включении должен выдвигаться
  - потенциометр тормоза: при выдвижении должны возрастать значения
  - сервопривод газа: малые значения - газ убран, большие значения - газ нажат

  (отключенная часть)
  - мотор КПП1: при прямом включении должен выдвигаться
  - потенциометр АКПП1: при выдвижении должны возрастать значения  
  - мотор КПП2: при прямом включении должен выдвигаться
  - потенциометр АКПП2: при выдвижении должны возрастать значения
  

!!!!!  ВАЖНО  !!!!
Ведется непрерывный контроль аварийного выключателя и его срабатывании автоматически выжимается тормоз и сцепление.
Устанавливается индикатор норма/авария в 1 (авария)
!!!!!  ВАЖНО  !!!!

Конфигурация платы:
Arduino Mega 2560
Mega-multi Expansion Shield 
Слот 1: Monster Motor Shield
Слот 2: Monster Motor Shield
Слот 3: Troyka Shield + Ethernet Shield
Слот 4: Monster Motor Shield                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           

Управляющие и мониторинговые пины:
Тормоз (Braking)
Газ  (Throttle)
Сцепление (Clutch)
КПП1 (GearBox1)  Вперед - Нейтраль
КПП2 (GearBox2)  Задняя - Первая

Управление через Ethernet (штатный режим):

M  m - режим мониторинга (калибровки) - непосредственное управление сервомоторами
  В данном режиме можно выбрать сервопривод и включить режим "набора" (+) или "убавления" (-) потенциометра. Остановка (s)

L  l - режим освобождения сервоприводов - FreeDrive mode

M      M = {E, N, F, B, A, C, D, R}   
        Символы не связаны между собой, можно вводить по отдельности

ВНИМАНИЕ!   Упрощенная версия!   
Отрабатываются только команды:
                              F, R - движение (сцепление и тормоз отпущены)
                              S, A, B, N, C, D - остановка с выжатым сцеплением и тормозом)
                              E - режим "побега"  (обе педали отпущены)
                              X - сцепление нажато


Схема переходов между состояниями системы управления
  E - стартовое и "свободное" состояние, все педали отжаты, коробка в нейтрали (посередине) - это состояние "побега" (ESCAPE), куда можно "выдернуть" любую передачу
  N - режим "стоячей" нейтрали с нажатым тормозом (сцепление нажато, нажат тормоз, убран газ, коробка в нейтрали (середина)
  // рабочие режимы (после калибровки на "схватывание")
  A - 1-я фаза последовательности "вперед": сцепление нажато, тормоз нажат, убран газ, коробка в нейтрали справа 
  B - 2-я фаза последовательности "вперед": сцепление нажато, тормоз нажат, убран газ, коробка на первой передаче
  F - 3-я фаза последовательности "вперед": сцепление отпущено, тормоз отпущен, газ рабочий, коробка на первой передаче
  C - 1-я фаза последовательности "назад": сцепление нажато, тормоз нажат, убран газ, коробка в нейтрали слева 
  D - 2-я фаза последовательности "назад": сцепление нажато, тормоз нажат, убран газ, коробка на задней передаче   
  R -3-я фаза последовательности "назад": сцепление отпущено, тормоз отпущен, газ рабочий, коробка на задней передаче
  Основная последовательность переключений:  F <-> B <-> A <-> C <-> D <-> R
  Стартовые / финишные последовательности:   A <-> 1 <-> 0 <-> 2 <-> C
  После включения система автоматически переходит в E (ESCAPE)
  Дополнительные команды: X - () выжать сцепление
                          S - остановка
  Тяговые команды:        T - (Throttle) нажать газ
                          I - (Idle) отпусть газ
  

Сервомоторы (последовательность идентификаторов)
  Clutch
  Braking
  Throttle

Управление состояниями (Control SysteLm - CS):  
      char CSGlobalDestState
      char CSLocalDestDtate
      char CSSourceState

*/
// Подключение библиотек управления
#include <Servo.h>
// реле для лампочек-индикаторов
// Стартовые режимы
#define StartingServoControl false   // стартовать доворачивание серв сразу, если нет, то будет режим FreeDrive
#define CustomModePresetAtStart true  // включить режим настройки на старте

// Преднастройки --------------------------------------
#define ServoMotorCount 2  // количество моторов, которыми можно управлять. Константа используется при обработке массивов параметров
                           // сервомотор газа учитывается отдельно (в проекте)

#define RELAY_1 42
#define RELAY_2 43
#define RELAY_3 38



// Настройки внешних устройств 

//#define ledPin 13  // мигаем при исправной работе
 
#define servofreezone 3  // мертвая область серв

#define ActivateThrottleControl false  // выполнять управление тягой мотора, при false - сервопривод не активируется (нет аттача)

#define MotorAlarmKeyPin 2  // Система контроля работы ходового двигателя (ДВС) робокара. Фактически, контроллирует замкнутость контура аварийной кнопки.
                                  // Кнопка должна быть "поддянута вниз", то есть при разомкнутом состоянии передавать 0.
                                  // При аварии должен передаваться 1

//  ------------------------------ сцепление -----
#define ClutchArrayID 0    // идентификатор в массивах
#define ClutchPinA    7  // 24
#define ClutchPinB    8 // 11
#define ClutchPWMPin  5 // 26
#define ClutchPotentiometer A4  // потенциометр сцепления
#define ClutchMax     430 //360       // педаль нажата до упора
#define ClutchPush    400 //315       // педаль нажата (полное размыкание дисков)
#define ClutchMiddle  220       // середина - проскальзывание, но движение
#define ClutchReleasePlus 200   // положение сцепления (педаль почти отпущена)
#define ClutchRelease 150 //140       // педаль отпущена (полное смыкание дисков)  
#define ClutchMin     130 //120       // педаль отпущена полностью
#define ClutchStPWMValue 255    // стандартное значение

//  ------------------------------ тормоз -----
#define BrakingArrayID 1    // идентификатор в массивах
#define BrakingPinA    4 //7  // 27 
#define BrakingPinB    9 //8  // 28
#define BrakingPWMPin  6 //5  // 25
#define BrakingPotentiometer A3  // потенциометр тормоза  //тормоз крутится наоборот
#define BrakingMax     280       // педаль нажата
#define BrakingPush    260       // педаль нажата до движения с сильным сопротивлением
#define BrakingMiddle  230      // середина - машина подтормаживает, но катится
#define BrakingRelease 140       // педаль почти отпущена
#define BrakingMin     100       // педаль отпущена
#define BrakingStPWMValue 255
// стандартное значение

//  ------------------------------ газ -----  (управление стандартным сервоприводом через PIN с поддержкой PWM)
#define ThrottlePin     36       // PWM
#define ThrottlePush    60       // подаль нажата (движение)
#define ThrottleRelease 120       // подаль отпущена (холостой ход)

// Объект для управления
Servo ThrottleServo;  // create servo object to control a servo

// Включение полностью отладочного режима -------------
bool CustomModePreset = CustomModePresetAtStart; // включить режим настройки

// Переменные контроля режимов (управляются тумблерами) -------------- 
bool isFreeDriveMode = !StartingServoControl;   // стартовать доворачивание серв сразу;  true - "свободный" режим
bool isServoCritical = false;  // состояние ошибки сервы (значение потенциометра выходит за допуск (макс-мин + 5)  Автоматический режим самоотключается
bool isUseServoBrake = true; // разрешение торможения серв    
bool isRunMission = false;    // режим миссии
bool isSensorDebugMode = true; // Отладка датчиков - отправляются значения АЦП системы управления

// Параметры подключения контроллеров сервомоторов ---------
//  Руль, сцепление, тормоз, газ, КПП1, КПП2
int pinA[ServoMotorCount] =   {ClutchPinA,   BrakingPinA};  
int pinB[ServoMotorCount] =   {ClutchPinB,   BrakingPinB}; 
int pwmpin[ServoMotorCount] = {ClutchPWMPin, BrakingPWMPin};
int pwmvalues[ServoMotorCount] = {ClutchStPWMValue, BrakingStPWMValue};

int motoralarmkeyvalue = 1;

// датчики
int posvalues[ServoMotorCount] = {0, 0};  // позиции текущие (с потенциометров)
int destvalues[ServoMotorCount] = {0, 0}; // позиции назначения (ожидаемые)
int marginvalues[ServoMotorCount] = {2, 2}; // зазоры контроля достижения нужной позиции
int throttlepos = 0; // положение газа

int GearBoxCurrentState = -1; // состояние коробки передач (вычисляется по реальным данным с потенциометров)

// Ввод-вывод
char GearBoxCMD = '-';         // команда для кробки

// Параметры управления состоянием системы управления -----
char CSDestState = '-';    // Целевая позиция 
char CSSourceState = '-';  // Исходная позиция

int CustomControlServo = 0;

// Параметры отправки мониторинговых данных по Serial ----------------
unsigned long LastSendingMoment = 0; // время предыдущей отправки
unsigned long CurrentMoment = 0;
unsigned long SendingInterval = 500;  // частота (интервал) оправки данных в миллисекундах

// Рабочие переменные разного назначения -----------------------------
char cmd;
char val;
int intvalue = 0;

#include <Ethernet.h>
#include <EthernetUdp.h>
#include <SPI.h>

//#-------- UDP configuration-------#
// Ниже укажите MAC-адрес и IP-адрес вашего контроллера .
// IP-адрес будет зависеть от настроек вашей локальной сети:
// При присваивании IP адрессов следует посмотреть доступные в маршрутизаторе
byte mac[] = {   0x90, 0xA2, 0xDA, 0x10, 0x13, 0xBB };
IPAddress boardIP (192, 168, 0, 205);                       // IP платы
IPAddress hostIP (192, 168, 0, 100);                        // IP server
unsigned int boardPort = 87;                                // Порт платы
unsigned int hostPort = 90;                                 // Порт сервера

EthernetUDP UDP;                                            // Создание экземпляра класса EthernetUDP для отправки и получения UDP-пакетов
char packetBuffer[UDP_TX_PACKET_MAX_SIZE];                  // Храним полученный пакет (приемный буфер Udp-пакета)
bool isStarted = false;                                     // Флаг работы программы
String comGlobal = "";                                      // Переменная для отправки на сервер

void setup() 
{
  pinMode(42, OUTPUT);
  pinMode(43, OUTPUT);
  pinMode(38, OUTPUT);
  // настраиваем выходы управляющих линий
  for (int i=0; i<ServoMotorCount; i++)
  {
      pinMode(pinA[i], OUTPUT);
      pinMode(pinB[i], OUTPUT);
      pinMode(pwmpin[i], OUTPUT);

      digitalWrite(pinA[i], LOW);
      digitalWrite(pinB[i], LOW);
      analogWrite(pwmpin[i], 0);
  }

  if (ActivateThrottleControl)                            // активируем управление газом (через сервомотор)
  {
      ThrottleServo.attach(ThrottlePin);
      delay(10);
      ThrottleServo.write(ThrottleRelease);               // поворачиваем сервопривод на минимум
  }

  Ethernet.begin(mac, boardIP);                           // Инициализируем плату контроллера
  UDP.begin(boardPort);                                   // Включаем прослушивание порта
  
  UDP.beginPacket(hostIP, hostPort);                     
  UDP.write("#Propulsion Module Initializing...");  
  UDP.endPacket();
  delay(1000);
  
  LastSendingMoment = millis(); // Фиксируем момент последней отправки данных

  // инициализация режимов // задаём назначение туда, где мы есть в момент старта
  CSDestState = '-';
  CSDestState = '-';
  CSSourceState = '-';    
  
  destvalues[ClutchArrayID] = analogRead(ClutchPotentiometer);
  delay(10);
  destvalues[BrakingArrayID] = analogRead(BrakingPotentiometer);
  delay(10);
  // Запуск инициализации Control System 
  isStarted = false;                                     // Флаг работы программы
  delay(200);
  UDP.beginPacket(hostIP, hostPort);                     
  UDP.write("#Propulsion Module Ready!");  
  UDP.endPacket();
 
}

void loop() 
{

  //#1 Начало работы кода при включении платы один раз 
  if(UDP.parsePacket() > 0 && !isStarted){
    UDP.read(packetBuffer, UDP_TX_PACKET_MAX_SIZE);         // Cчитываем принятый пакет в буфер packetBufffer
    String comand = packetBuffer;                       
    comand.toLowerCase();
    if(comand == "start"){
      // Отсылаем пакет данных серверу:  
      UDP.beginPacket(hostIP, hostPort);                     
      UDP.write("#CVL Propulsion Module Online");  
      UDP.endPacket();
      LastSendingMoment = millis();                         // Фиксируем момент последней отправки данных
      isStarted = true;
    }
  }
  //#1 Конец работы кода при включении платы один раз 

  if(isStarted){ 
  // Индикация:
  //Serial.println(digitalRead(49));
  //Serial.println(digitalRead(48));
  /*
  int lamp1 = digitalRead(51);
  int lamp2 = digitalRead(50);

  if ((lamp1 = 1) && (lamp2 = 0))
  {
    digitalWrite(RELAY_1, HIGH);
  
  }
  else
  {
    digitalWrite(RELAY_3, LOW);
  }
  if ((lamp1 = 1) && (lamp2 = 1))
  {
    digitalWrite(RELAY_2, HIGH);
  }
  else
  {
    digitalWrite(RELAY_3, LOW);
  }
  if ((lamp1 = 0) && (lamp2 = 0))
  {
    digitalWrite(RELAY_3, HIGH);
  } 
  else 
  {
    digitalWrite(RELAY_3, LOW);
  }
*/
    // читаем все потенциометры
    posvalues[ClutchArrayID] = analogRead(ClutchPotentiometer);
    delay(10);
    posvalues[BrakingArrayID] = analogRead(BrakingPotentiometer);
    delay(10);
    
    motoralarmkeyvalue = digitalRead(MotorAlarmKeyPin); //analogRead(MotorAlarmKeyPin);

    // обработка ввода-вывода по Ethernet  
    UDP.read(packetBuffer, UDP_TX_PACKET_MAX_SIZE);         // считываем принятый пакет в буфер packetBufffer

    cmd = packetBuffer[0];   
    
    //#2 Начало завершения работы кода 
    String comand = packetBuffer;                  
    comand.toLowerCase();
    if(comand == "end"){
    comand = "";
    // Отсылаем пакет данных серверу:  
    UDP.beginPacket(hostIP, hostPort);                        
    UDP.write("#CVL Propulsion Module Offline");  
    UDP.endPacket();
    LastSendingMoment = millis();                           // Фиксируем момент последней отправки данных
    destvalues[ClutchArrayID] = ClutchRelease;              // сцепление отпустить
    destvalues[BrakingArrayID] = BrakingRelease;            // тормоз отпустить
    isStarted = false;
    }
    //#2 Конец завершения работы кода 
    
    if (CustomModePreset)
    {
        if ((cmd == 'M')||(cmd == 'm'))
        {
            CustomModePreset = false;
            SendFeedback("#Calibration mode deactivate (press M for artivate)");      // ОТПРАВЛЯЕМ пакет данных по UDP
        }
        else if (cmd == 'q')  // сбрасываем все целевые параметры до текущих
        {               
            destvalues[ClutchArrayID] = posvalues[ClutchArrayID];
            destvalues[BrakingArrayID] = posvalues[BrakingArrayID];
        }
        else 
        {
            ProcessCalibration(cmd);        
        }    
    }
    else
    {
        switch (cmd)
        {
            case 'M': case 'm':
            {
                CustomModePreset = true;
                SendFeedback("#Calibration mode activate (press M for deactivate");      // ОТПРАВЛЯЕМ пакет данных по UDP
                isFreeDriveMode = true; // сразу сбрасываем режим в свободное руление
                StopAllMotor(); 
            }
            break;
            case 'L': case 'l':
            {
                if (isFreeDriveMode)
                {
                    isFreeDriveMode = false;                    
                    SendFeedback("#FreeDrive mode deactivate (press L for activate)");
                }
                else
                {
                    isFreeDriveMode = true;
                    SendFeedback("#FreeDrive mode activate (press L for deactivate)");
                    StopAllMotor();
                } 
            }
            break;
            case 'S': case 's': case 'X': case 'E': case 'N': case 'F': case 'B': case 'A': case 'C': case 'D': case 'R': case 'T': case 'I':
            {
                GearBoxCMD = cmd;
            }
            break;
                                  
        }
    }
      
    // начинаем обработку
    if (CustomModePreset) // <-- включён режим калибровки
    {
        // очищаем команды
        GearBoxCMD = '-';
    }
    else // <-- включен режим "автоматического управления" 
    {
        // ЗДЕСЬ БЛОК ОСНОВНОЙ ЛОГИКИ УПРАВЛЕНИЯ !!! ------------------------------------------------
        //         
        CSSwitchTo(GearBoxCMD); // коробка
        
        // очищаем команды
        GearBoxCMD = '-';
 
        // КОНЕЦ БЛОКА ОСНОВНОЙ ЛОГИКИ УПРАВЛЕНИЯ !!! ------------------------------------------------                
        // Прочая логика
        // Обработка кручения сервомоторов
        ProcessServoControl(); // крутим сервы до заданных значений (до локального состояния назначения) 
        // Проверка на критические значения всех серв
        CheckServoCritical(); // <-- при ошибке сама переключит скетч в режим мониторинга (отладки) и остановит сервы        
    }
    // Отправка данных мониторинга, если необходимо
    SendDataIfNeed();
  }
  memset(packetBuffer, 0, UDP_TX_PACKET_MAX_SIZE);          //Очищаем буфер приёма данных 
}  // --- end of loop() ----------------

void SendFeedback(String str){
   int strLength = str.length();                            // Длина строки
   char response[strLength+1];                              // Объявляем массив char размером входной строки str
   str.toCharArray(response,sizeof(response));              // Переводим строку в массив char
   UDP.beginPacket(hostIP, hostPort);                       // Начало отправки данных (кому будем отправлять данные)
   UDP.write(response);                                     // Данные,которые отправляем
   UDP.endPacket();                                         // Конец отправки данных
   LastSendingMoment = millis();                            // Фиксируем момент последней отправки данных
}

void SendDataIfNeed() // Отправка данных мониторинга, если пришло время
{
    CurrentMoment = millis();
    comGlobal = "";
    if (CurrentMoment > LastSendingMoment + SendingInterval)
    {
        if (!CustomModePreset)  // если не калибровка
        { 
            // -- Отправляем текущие данные управления --        
            //  CVL:auto:F:B:A:P:500:1024:#500:500#
            comGlobal +="CVL:";
            if (isFreeDriveMode)   // <-- режим управления "свободный" или "автоматический"
            {
                comGlobal += "free:";
            }
            else
            {
                comGlobal += "auto:";
            }
            comGlobal += ":";
            comGlobal += String(motoralarmkeyvalue);// <---------------- сигнал работы ходового двигателя (кнопка прерывателя ключа зажигания) ----------------
            // -- Завершение отправки контрольных данных --
            
        } // if ! CustomModePreset   
        
        comGlobal +="#";                                            // после служебных данных можно отправить любую информацию

        if (CustomModePreset)
        {
          comGlobal += "Ctrl: ";
          comGlobal += String(CustomControlServo);
          comGlobal += "> ";  
        }
        
        // -- Дополнительная информация --
        if (isSensorDebugMode)   // в том числе значения сенсоров для визуального контроля
        { 
            for (int i = 0; i < ServoMotorCount; i++)
            {
                SerialPrintFormattedInt(posvalues[i]);
                if ( !CustomModePreset ) // <-- не включён режим калибровки
                {
                    comGlobal += "->";
                    SerialPrintFormattedInt(destvalues[i]);
                    comGlobal += "#";
                }
                else
                {
                  comGlobal += ":";                                
                }             
            } 
        } 
        SendFeedback(comGlobal);                                  // ОТПРАВЛЯЕМ пакет данных по UDP
        LastSendingMoment = millis();                       // сохраняем текущее время как "старое"
    }
}  // -- end of SendDataIfNeed() -------------------

void SerialPrintFormattedInt(int n) // печатаем форматированное число всегда в 4 символа ----------------------------
{
    if (n < 10)
    {
        comGlobal+="   ";
    }
    else if (n < 100) 
    {
        comGlobal+="  ";
    }
    else if (n<1000)
    {
        comGlobal+=" ";
    }
    comGlobal+=String(n);
} // --- end of SerialPrintFormattedInt() ----------------------------

void ProcessServoControl() // крутим сервы до заданных значений
{
    ClutchContinue(); // продолжаем перемещать педаль сцепления
    BrakingContinue(); // продолжаем нажимать педаль тормоза
    ThrottleContinue(); // продолжаем нажимать педаль газа       
} // --- end of ProcessServoControl()

void CSSwitchTo(char NewGlobalState) // Запуск переключения в нужное состояния
{
    switch (NewGlobalState)
    {
        case 'X':
        {
              CSDestState = 'X';
              destvalues[ClutchArrayID] = ClutchPush;   // сцепление отпустить
              //destvalues[BrakingArrayID] = posvalues[BrakingArrayID];  // тормоз оставить как есть
        }
        break;
        case 'S': case 's': case 'N': case 'A': case 'B': case 'C': case 'D':
        {
              CSDestState = NewGlobalState;
              destvalues[ClutchArrayID] = ClutchPush;   // сцепление отпустить
              destvalues[BrakingArrayID] = BrakingPush;  // тормоз отпустить              
              SendFeedback("# Switch to STOP");
        }
        break;
        case 'E': // <-- нужно реализовать "принудительное вырывание" для режима 0
        {
              CSDestState = 'E';
              destvalues[ClutchArrayID] = ClutchRelease;   // сцепление отпустить
              destvalues[BrakingArrayID] = BrakingRelease;  // тормоз отпустить
              // Выводим отладочное сообщение 
              SendFeedback("# Switch to ESCAPE");  
        }            
        break;
        case 'F': // режим вперед
        {
              destvalues[ClutchArrayID] = ClutchRelease;   // сцепление отпустить
              destvalues[BrakingArrayID] = BrakingRelease;  // тормоз отпустить
              // Выводим отладочное сообщение 
              SendFeedback("# Switch to FORWARD");
        }
        break;
        case 'R':  // режим заднего хода
        {
              destvalues[ClutchArrayID] = ClutchRelease;   // сцепление отпустить
              destvalues[BrakingArrayID] = BrakingRelease;  // тормоз отпустить
              // Выводим отладочное сообщение 
              SendFeedback("# Switch to BACKWARD");
        }
        break;
        case 'T':  // добавка газа
        {
              if (ActivateThrottleControl) // Если включен режим управления двигателем (газом)
              {
                  ThrottleServo.write(ThrottlePush);  // поворачиваем сервопривод на максимум
              }              
              // Выводим отладочное сообщение 
              SendFeedback("# Throttle pushed");
        }
        break;
        case 'I':  // добавка газа
        {
              if (ActivateThrottleControl) // Если включен режим управления двигателем (газом)
              {
                  ThrottleServo.write(ThrottleRelease); // поворачиваем сервопривод на минимум
              }
              // Выводим отладочное сообщение 
              SendFeedback("# Throttle released");
        }
        break;            
        case '-':
        {
        }
        break;
        default:
        {
              SendFeedback("#Unknown CS command");
        }        
        break;
    }
}  // -- end of CSSwitchTo() ---

bool CSCheckState() // Проверка достижения локального состояния
{
    bool checkvalue = false;

    return checkvalue;
}  // -- end of CSCheckState() ---

bool CheckDCMotorPosition(int MotorID)  // если мотор в диапазоне целевой позиции, то значение true
{
    bool ctrl = false;
    if ((MotorID >= 0) && (MotorID < ServoMotorCount))
    {
        ctrl = ((posvalues[MotorID] >= destvalues[MotorID] - marginvalues[MotorID]) && (posvalues[MotorID] <= destvalues[MotorID] + marginvalues[MotorID]));
    }
    return ctrl;
} // -- end of CheckDCMotorPosition() ---

// Проверка критических значений потенциометров серв ===========================================

void StopAllMotor()
{
    for (int i = 0; i < ServoMotorCount; i++) // стопим моторы
    {
        DCMotorControl(i, 0, pwmvalues[i]);
    }  
}

void CheckServoCritical()
{
    // isSensorDebugMode
    // isServoCritical
    bool ctrl = true;
    if (posvalues[ClutchArrayID] < ClutchMin - 5)   // сцепление
    {
        ctrl = false;  String com = "Crit: Clutch:"; com += (String)posvalues[ClutchArrayID]; com +="< mix  "; com += (String)(ClutchMin - 5); SendFeedback(com);
    }
    if (posvalues[ClutchArrayID] > ClutchMax + 5)   // сцепление
    {
        ctrl = false;  String com = "Crit: Clutch:"; com += (String)posvalues[ClutchArrayID];  com +="> max  "; com +=(String)(ClutchMax + 5); SendFeedback(com);
    }    
    if (posvalues[BrakingArrayID] < BrakingMin - 5)  // тормоз
    {
        ctrl = false;  String com = "Crit: Brake:";  com +=(String)posvalues[BrakingArrayID]; com +="< min  "; com +=(String)(BrakingMin - 5); SendFeedback(com);
    }        
    if (posvalues[BrakingArrayID] > BrakingMax + 5)  // тормоз
    {
        ctrl = false;  String com ="Crit: Brake:"; com +=(String)posvalues[BrakingArrayID]; com +="> max  "; com +=(String)(BrakingMax + 5); SendFeedback(com);
    }    
    // реакция на критическое состояние    
    if (ctrl == false)  // если ctrl = true, то хоть где-то был выход за диапазон. Срочно стопорим сервы и включаем режим диагностики
    {
        for (int i = 0; i < ServoMotorCount; i++) // стопим моторы
        {
            DCMotorControl(i, 0, pwmvalues[i]);
        }
        SendFeedback("#ERROR! One or more servos are outborder");
        CustomModePreset = true;
        isFreeDriveMode = true;
        SendFeedback("#Calibration mode activate");      
    }
}

// Калибровочные процедуры =====================================================================

void ProcessCalibration(char localcmd)
{
    switch(localcmd)        
    {
         case '0': 
         {
                CustomControlServo = 0;
         }
         break;
         case '1': 
         {
                CustomControlServo = 1;
         }
         break;   
         case '-':
         {
                DCMotorControl(CustomControlServo, -1, pwmvalues[CustomControlServo]);
                String comD = "Servo ";
                comD += (String)CustomControlServo;
                comD += "Down ";
                SendFeedback(comD);
         }
         break;                            
         case '+': 
         {
                DCMotorControl(CustomControlServo, 1, pwmvalues[CustomControlServo]);
                String comU = "Servo ";
                comU += (String)CustomControlServo;
                comU += "Up ";
                SendFeedback(comU);
         }
         break;
         case 'S': case 's': 
         {
                DCMotorControl(CustomControlServo, 0, pwmvalues[CustomControlServo]);
                String comS = "Servo ";
                comS += (String)CustomControlServo;
                comS += "Stop ";
                SendFeedback(comS);
         }
         break;
    }
}

// Функции воздействия на органы управления ====================================================

void DCMotorControl(int motorID, int motorDirect, int motorPower)  // Управление моторами постоянного тока
{    //   motorDirect == 0  - оба канала "вниз", -1  - мотор "в уменьшение потенциометра", 1 - мотор "в увеличение потенциометра"  !!!! ЭТО ОЧЕНЬ ВАЖНО ДЛЯ ЛОГИКИ КОНТРОЛЯ СЕРВ
     //   
    int currentDirect = motorDirect;
    if ((motorID >= 0) && (motorID < ServoMotorCount))
    {
        // -- непосредственно крутим моторы -----
        if (( !isFreeDriveMode ) || (CustomModePreset))   // если режим "не свободного вращения" или режим "отладки"
        {
            if (currentDirect == 1)
            {
                digitalWrite(pinA[motorID], LOW);
                digitalWrite(pinB[motorID], HIGH);
            }
            else if (currentDirect == 0)
            {
                digitalWrite(pinA[motorID], LOW);
                digitalWrite(pinB[motorID], LOW);
            }
            else if (currentDirect == -1)
            {
                digitalWrite(pinA[motorID], HIGH);
                digitalWrite(pinB[motorID], LOW);
            }
            analogWrite(pwmpin[motorID], motorPower); // тяга мотора (значение PWM)
        }
    }      
}  // -- end of DCMotorControl() ---



// ========================== Сцепление ===============================
bool ClutchContinue() // продолжаем перемещать педаль сцепления
{
    bool ctrl = false;
    int currentvalue = posvalues[ClutchArrayID];
    int destvalue = destvalues[ClutchArrayID];
    if ((currentvalue > destvalue-servofreezone)&&(currentvalue < destvalue+servofreezone)) // сцепление в требуемом положении, стопорим
    {
        if (isUseServoBrake)  // используем тормоз сервопривода
        {
            DCMotorControl(ClutchArrayID, 0, pwmvalues[ClutchArrayID]);
        }
        else // просто отключаем моторы
        {
            DCMotorControl(ClutchArrayID, 0, 0);
        }
        ctrl = true;  // <-- можно переходить к следующей команде
    } 
    else if (currentvalue < destvalue) // значение меньше - нужно крутить в увеличение
    {
        DCMotorControl(ClutchArrayID, 1, pwmvalues[ClutchArrayID]);
    }
    else
    {
        DCMotorControl(ClutchArrayID, -1, pwmvalues[ClutchArrayID]);
    }
    return ctrl;
}

// ========================== Тормоз ===============================
bool BrakingContinue() // продолжаем нажимать педаль тормоза
{
    bool ctrl = false;
    int currentvalue = posvalues[BrakingArrayID];
    int destvalue = destvalues[BrakingArrayID];
    if ((currentvalue > destvalue-servofreezone)&&(currentvalue < destvalue+servofreezone)) // тормоз в требуемом положении, стопорим
    {
        if (isUseServoBrake)  // используем тормоз сервопривода
        {
            DCMotorControl(BrakingArrayID, 0, pwmvalues[BrakingArrayID]);
        }
        else // просто отключаем моторы
        {
            DCMotorControl(BrakingArrayID, 0, 0);
        }
        ctrl = true;  // <-- можно переходить к следующей команде
    } 
    else if (currentvalue < destvalue) // значение меньше - нужно крутить в увеличение
    {
        DCMotorControl(BrakingArrayID, 1, pwmvalues[BrakingArrayID]);
    }
    else
    {
        DCMotorControl(BrakingArrayID, -1, pwmvalues[BrakingArrayID]);
    }
    return ctrl;
}

// ========================== Газ ===============================

bool ThrottleContinue() // продолжаем нажимать педаль газа
{
    bool ctrl = true;  // вообще не заморачиваемся !!!!!
    return ctrl;
}
