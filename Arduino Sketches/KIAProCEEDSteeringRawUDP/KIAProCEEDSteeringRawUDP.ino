/*
Система управления платой контроля рулевого колеса Робокара Мосполитеха (в конфигурации "Зимний город 2019") для KIA ProCeed

Версия 23.10.2020 by Leonov Vladislav 181-311

Введена коррекция скорости вращения (снижение мощности при приближении к зоне точного руления)

Система управления с 1 x Arduino Uno, 1 x Monster Shield, 1 x Troyka Shield, 1 х Тумблер, 1 x Ethernet Shield

"Механическая" калибровка: *  
  - мотор руля: при прямом включении должен крутиться вправо
  - потенциометр руля:
     - возрастает при вращении руля вправо
     - 500 в средней позиции (прямо)

!!!!!  ВАЖНО  !!!!
Ведется непрерывный контроль тока через рулевой мотор. В случае превышения номинального значения 
мотор отключается с индикацией авария в 1 (авария)
!!!!!  ВАЖНО  !!!!

К плате подключен тумблер отключения автоматического режима руления.

При включении платы даже в автоматическом режиме (тумблер) вращение не производится до поступления по Serial
любой корректной команды активации вращения (число от 100 до 999).

Конфигурация системы:
Arduino Uno
Monster Motor Shield
Troyka Shield
Ethernet Shield

Управляющие и мониторинговые пины:
Руль (Steering)

Управление через Ethernet Shield (штатный режим):

{100 - 999}  руль   100 - влево до упора, 500 - середина, 999 - вправо до упора
Окончание ввода # или \n. Можно подождать 0,5 секунды и строка обработается сама.

S         остановка вращения руля
F         свободный режим
A         включение автоматического режима (после F)

Тумблер не связан с командами программного управления, а отключает "передачу команд на мотор".
*/

// Настройки внешних устройств 
#define ledPin 13                                           // Мигаем светодиодом при исправной работе
 
//  ------------------------------ РУЛЬ ------------------------------
/*
int inApin[2] = {7, 4};  // INA: Clockwise input
int inBpin[2] = {8, 9}; // INB: Counter-clockwise input
int pwmpin[2] = {5, 6}; // PWM input
int cspin[2] = {2, 3}; // CS: Current sense ANALOG input
int enpin[2] = {0, 1}; // EN: Status of switches output (Analog pin)
 */
#define SteeringPinA    4
#define SteeringPinB    9
#define SteeringPWMPin  6
#define SteeringPotentiometer A5                            // Потенциометр руля
#define SteeringCSPin A2
#define SteeringENPin A0
#define SteeringMax    710                                  // Вправо (ПРЕДЕЛЬНОЕ! значение для ограничителя)
#define SteeringCenter  500                                 // Середина (КАЛИБРОВОЧНОЕ! значение середины)
#define SteeringMin     290                                 // Влево   (ПРЕДЕЛЬНОЕ! значение для ограничителя)
#define SteeringStPWMValueFast 200                          // Стандартное значение
#define SteeringStPWMValueSlow 100                          // Сниженная тяга
#define SteeringAccurateZone 15                             // Зона около destvalue где выполняется снижение мощности мотора
#define SteeringCriticalLoad 1000                           // Предельное значение нагрузки. Измеряется ток (Current) на SteeringCSPin
#define SteetingCriticalLoadTimeOut 1000                    // Время, при превышении которого при повышенной нагрузке тяга сбрасывается

int SteeringStPWMValue = SteeringStPWMValueFast;

#define servofreezone 5  // мертвая область серв

// Переменные контроля режимов (управляются тумблерами) -------------- 
bool isFreeMode = true;                               
// Свободный режим - управление по значению энкодера (потенциометра) не выполняется
bool isUseServoBrake = true;                                // Разрешение торможения серв    

// датчики
int posvalue = 500;                                         // Позиции текущие (с потенциометров)
int destvalue = 500;                                        // Позиции назначения (ожидаемые)
int marginvalue = 2;                                        // Зазоры контроля достижения нужной позиции

// Ввод-вывод
char SteeringCMD = '-';                                     // Буфер символа для команды для руля
String SteeringCMDLine = "";                                // Буфер команды для руления

bool SCtrl = false;                                         // Контроль руления

// Параметры ожидания прихода разделителя данных
unsigned long LastInputMoment = 0;                          // Время прихода последнего символа управляющей команды
unsigned long CurrentInputMoment = 0;                       // Расчет времени прихода данных
unsigned long InputTimeOutInterval = 250;                   // Оидания управшяющих данных в миллисекундах

// Параметры отправки мониторинговых данных по Ethernet --------------
unsigned long LastSendingMoment = 0;                        // Время предыдущей отправки
unsigned long CurrentMoment = 0;                            // Текущей момент отправки
unsigned long SendingInterval = 500;                        // Частота (интервал) оправки данных в миллисекундах

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
IPAddress boardIP (192, 168, 0, 200);                       // IP платы
IPAddress hostIP (192, 168, 0, 100);                        // IP server
unsigned int boardPort = 88;                                // Порт платы
unsigned int hostPort = 90;                                 // Порт сервера

EthernetUDP UDP;                                            // Создание экземпляра класса EthernetUDP для отправки и получения UDP-пакетов
char packetBuffer[UDP_TX_PACKET_MAX_SIZE];                  // Храним полученный пакет (приемный буфер Udp-пакета)
bool isStarted = false;                                     // Флаг работы программы

void setup() 
{
    // Настраиваем выходы управляющих линий
    pinMode(SteeringPinA, OUTPUT);
    pinMode(SteeringPinB, OUTPUT);
    pinMode(SteeringPWMPin, OUTPUT);

    digitalWrite(SteeringPinA, LOW);
    digitalWrite(SteeringPinB, LOW);
    analogWrite(SteeringPWMPin, 0);
   
    // инициализация режимов // задаём назначение туда, где мы есть в момент старта
    destvalue = 500; //analogRead(SteeringPotentiometer);
    
    //SteeringTo('5');  // ставим руль в середину  <---------- не делаем так при штатной работе (должно быть закомменчено)
    Ethernet.begin(mac, boardIP);                           // Инициализируем плату контроллера
    UDP.begin(boardPort);                                   // Включаем прослушивание порта
    UDP.beginPacket(hostIP, hostPort);                     
    UDP.write("#CVL Steering Control Board Ready!");  
    UDP.endPacket();
    LastSendingMoment = millis();                           // Фиксируем момент последней отправки данных
    delay(500);
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
      UDP.write("#CVL Steering Control Board Online");  
      UDP.endPacket();
      LastSendingMoment = millis();                         // Фиксируем момент последней отправки данных
      isStarted = true;
    }
  }
  //#1 Конец работы кода при включении платы один раз 
  
  if(isStarted){ 
    // читаем все потенциометры
    //posvalue = analogRead(SteeringPotentiometer);
    posvalue = 555;

    // время начала цикла обработки
    CurrentInputMoment = millis();
    
    // обработка ввода-вывода по Ethernet  
    UDP.read(packetBuffer, UDP_TX_PACKET_MAX_SIZE);         // считываем принятый пакет в буфер packetBufffer

    //#2 Начало завершения работы кода 
    String comand = packetBuffer;                       
    comand.toLowerCase();
    if(comand == "end"){
    LastInputMoment = CurrentInputMoment;                   // Запоминаем текущий момент обработки
    comand = "";
    // Отсылаем пакет данных серверу:  
    UDP.beginPacket(hostIP, hostPort);                        
    UDP.write("#CVL Steering Control Board Offline");  
    UDP.endPacket();
    LastSendingMoment = millis();                           // Фиксируем момент последней отправки данных
    isStarted = false;
    }
    //#2 Конец завершения работы кода  
  
    char SteeringCMD = packetBuffer[0];                     // Считываем с принятого буфера первый символ
    
    switch (SteeringCMD)
    {
            case 'S': case 's':                             // остановка вращения     
                SendFeedback("#Stop motor");      
                StopMotorSafe();
                destvalue = posvalue;               
                SendFeedback("#Execute");               
                ProcessCMDBuffer();
                LastInputMoment = CurrentInputMoment;       // запоминаем текущий момент обработки
                SteeringCMD = '-';                          // Очищаем буфер символа для команды руля
                SteeringCMDLine = "";                       // Очищаем буфер команд для руления
                break;
            case 'F': case 'f':                             // включение свободного режима     
                SendFeedback("#Stop motor");                // Отправляем сообщение UDP
                StopMotorSafe();                            // Останавливаем мотор
                destvalue = posvalue;                       // Присваеваем значение стремления потенциометра текущее значение
                isFreeMode = true;                          // Переводим в режим свободного управленич
                SendFeedback("#Execute");                   // Отправялем сообщение UDP
                ProcessCMDBuffer();                         // 
                LastInputMoment = CurrentInputMoment;       // запоминаем текущий момент обработки
                SteeringCMD = '-';                          // Очищаем буфер символа для команды руля
                SteeringCMDLine = "";                       // Очищаем буфер команд для руления
                break;                    
            case 'A': case 'a':                             // включаем автоматический режим (из Safe Mode)
                isFreeMode = false;
                destvalue = posvalue;               
                SendFeedback("#Execute");               
                ProcessCMDBuffer();
                LastInputMoment = CurrentInputMoment;       // запоминаем текущий момент обработки
                SteeringCMD = '-';                          // Очищаем буфер символа для команды руля
                SteeringCMDLine = "";                       // Очищаем буфер команд для руления
                break;
            /*
             * case 'E': case 'e':                          // Завершение работы кода                                
            SendFeedback("CVL Steering Control Board Offline");               
            LastInputMoment = CurrentInputMoment;           // запоминаем текущий момент обработки
            isStarted = false;
            SteeringCMD = '-';                              // Очищаем буфер символа для команды руля
            SteeringCMDLine = "";                           // Очищаем буфер команд для руления
            break;
            */
            case '0': case '1': case '2': case '3': case '4': case '5': case '6': case '7': case '8': case '9':
                // рулим                   
                //SteeringCMDLine += SteeringCMD;           // Заполняем буфер команд для руления
                SteeringCMD = '-';                          // Очищаем буфер символа для команды руля
                SendFeedback("#Execute");               
                ProcessCMDBuffer();
                SteeringCMDLine = packetBuffer;
                LastInputMoment = CurrentInputMoment;       // Время прихода последнего символа управляющей команды
                break;  
        }  // switch 

    if (CurrentInputMoment > LastInputMoment + InputTimeOutInterval) // Если прошло много времени от старых данных, то обработаем что есть.
    {
        ProcessCMDBuffer();                                 
        SteeringCMDLine = "";                               // Очищаем буфер команд для руления
        LastInputMoment = CurrentInputMoment;               // Запоминаем текущий момент обработки
    }

    SteeringCMD = '-';                                      // Очищаем буфер символа для команды руля
    
    // автоматическое руление подразумевает коррекцию скорости вращения - выполняется внутри процедуры "продолжения руления"
    SteeringContinue();                                     // продолжаем крутить руль           
    // Проверка на критические значения сервомотора
    CheckServoCritical();                                   // <-- при ошибке сама остановит сервомотор        
    SendDataIfNeed();                                       // Отправка данных мониторинга, если необходимо
    memset(packetBuffer, 0, UDP_TX_PACKET_MAX_SIZE);        //Очищаем буфер приёма данных  
  }  // ---------------- isStarted ----------------
}  // ---------------- end of loop() ----------------

void SendFeedback(String str){
   int strLength = str.length();                            // Длина строки
   char response[strLength+1];                              // Объявляем массив char размером входной строки str
   str.toCharArray(response,sizeof(response));              // Переводим строку в массив char
   UDP.beginPacket(hostIP, hostPort);                       // Начало отправки данных (кому будем отправлять данные)
   UDP.write(response);                                     // Данные,которые отправляем
   UDP.endPacket();                                         // Конец отправки данных
   LastSendingMoment = millis();                            // Фиксируем момент последней отправки данных
}

void SendDataIfNeed()//-------------------------------------// Отправка данных мониторинга, если пришло время
{
    CurrentMoment = millis();           
    if (CurrentMoment > LastSendingMoment + SendingInterval)
    {
        // -- Отправляем текущие данные управления --        
        //  CVLST:500:650#   ключ:режим:текущее положение:требуемое положение
        String com ="CVLST:";
        if (isFreeMode) 
        {
            com += "free:";
        }
        else
        {
           com += "auto:";
        }
        
        // -- Завершение отправки контрольных данных --
        com += String(posvalue);
        com += ":" + String(destvalue)+"#";
        SendFeedback(com);                                  // ОТПРАВЛЯЕМ пакет данных по UDP
        LastSendingMoment = millis();                       // Сохраняем текущее время как "старое"
    }
}  // ------------------------------------------------------//end of SendDataIfNeed()

void ProcessCMDBuffer()//-----------------------------------// обработка буфера входных данных (преобразуем в число и если входит в диапазон - исполняем)
{
    int m = 0;
    m = SteeringCMDLine.toInt();
    if ((m >= SteeringMin) && (m < SteeringMax))
    {
        destvalue = m;
    }
    SteeringCMDLine = "";                                   // Очищаем буфер
}  // ------------------------------------------------------//end of ProcessCMDBuffer() 

void SerialPrintFormattedInt(int n)//-----------------------// Печатаем форматированное число всегда в 4 символа 
{
    if (n < 10)
    {
        SendFeedback("   ");
    }
    else if (n < 100) 
    {
        SendFeedback("  ");
    }
    else if (n<1000)
    {
        SendFeedback(" ");
    }
    
    SendFeedback(String(n));
} // -------------------------------------------------------//end of SerialPrintFormattedInt() 

bool CheckSteeringPosition()//------------------------------// если руль в диапазоне целевой позиции, то значение true
{
    bool ctrl = false;
    ctrl = ((posvalue >= destvalue - marginvalue) && (posvalue <= destvalue + marginvalue));
    return ctrl;
}  // ------------------------------------------------------//end of CheckSteeringPosition()

// Проверка критических значений потенциометров серв ===========================================

void CheckServoCritical()
{  
    if (posvalue < SteeringMin - 5)                         // руль "слишком влево"
    { 
       String com = "CVLST:Crit:";
       com += String(posvalue) + ":";
       com += String(destvalue);
       com += "#Steering critical: " + String(posvalue);
       com += " < " + String(SteeringMin) + "(min)";
       SendFeedback(com);                                   // ОТПРАВЛЯЕМ пакет данных по UDP        
       StopMotorSafe();                                     // торможение или отключение сервомотора
       String Error = "#ERROR! Steering servo is outborder!  Board switch to Safe Mode";
       SendFeedback(Error);                                 // ОТПРАВЛЯЕМ пакет данных по UDP
       isFreeMode = true;         
    }
    else if (posvalue > SteeringMax + 5)                    // руль "слишком вправо"
    {
      String com = "CVLST:Crit:";
      com += String(posvalue) + ":";
      com += String(destvalue);
      com += "#Steering critical: " + String(posvalue);
      com += " > " + String(SteeringMax) + "(max)";
      SendFeedback(com);                                    // ОТПРАВЛЯЕМ пакет данных по UDP
      StopMotorSafe();                                      // Торможение или отключение сервомотора
      String Error = "#ERROR! Steering servo is outborder!  Board switch to Safe Mode";
      SendFeedback(Error);                                  // ОТПРАВЛЯЕМ пакет данных по UDP
      isFreeMode = true; 
    }
}

// Функции воздействия на органы управления ====================================================
void SteeringDCMotorControl(int motorDirect, int motorPower)// Управление мотором постоянного тока (для руля)
{    //   motorDirect == 0  - оба канала "вниз", -1  - мотор "в уменьшение потенциометра", 1 - мотор "в увеличение потенциометра"  !!!! ЭТО ОЧЕНЬ ВАЖНО ДЛЯ ЛОГИКИ КОНТРОЛЯ СЕРВ
     //   
    int currentDirect = motorDirect;
    // -- непосредственно крутим моторы -----
    {
        // задаем направление в зависимости от кода
        if (currentDirect == 1)
        {
            digitalWrite(SteeringPinA, LOW);
            digitalWrite(SteeringPinB, HIGH);
        }
        else if (currentDirect == 0)
        {
            digitalWrite(SteeringPinA, LOW);
            digitalWrite(SteeringPinB, LOW);
        }
        else if (currentDirect == -1)
        {
            digitalWrite(SteeringPinA, HIGH);
            digitalWrite(SteeringPinB, LOW);
        }
        // задаем тягу
        analogWrite(SteeringPWMPin, motorPower);            // тяга мотора (значение PWM)     
        delay(100);
    }      
}  // -- end of DCMotorControl() ---

// ========================== Руль ===============================

void StopMotorSafe()                                        // отключение или торможение мотора в зависимости от разрешения включать моторный тормоз
{
    if (isUseServoBrake)
    {
        StopMotor();
    }
    else
    {
        SwitchOffMotor();
    }
} // --- end of StopMotorSafe() ----

void StopMotor() //  торможение мотора
{
    SteeringDCMotorControl(0, SteeringStPWMValue);          // томозим мотор      
}

void SwitchOffMotor()  // отключение сервомотора
{
    SteeringDCMotorControl(0, 0);                           // отключаем мотор      
} 

void SteeringStopMotor()  // стопорим мотор руля и через треть секунды отпускаем
{
    SteeringDCMotorControl(0, SteeringStPWMValue);          // томозим мотор
    delay(300);                                             // немного ждем 
    posvalue = analogRead(SteeringPotentiometer);
    destvalue = posvalue;                                   // записываем текущее положение как целевое
    SteeringDCMotorControl(0, 0);                           // отключам мотор
    delay(100);
}

bool SteeringMoveToState(int NeedState) // true - если позиция достигнута 
{
    bool ctrl = false; 
    if (destvalue == NeedState)
    {
        ctrl = SteeringContinue();
    }
    return ctrl;
}  // --- end of SteeringMoveToState() -----------

bool SteeringContinue() // продолжаем вращать руль, true - если позиция достигнута
{     
    bool ctrl = false;
    int currentvalue = posvalue;

    if ((currentvalue >= destvalue - SteeringAccurateZone)&&(currentvalue <= destvalue + SteeringAccurateZone)) // настройка скорости вращения
    {
        SteeringStPWMValue = SteeringStPWMValueSlow;        // медленно вблизи
    }
    else
    {
        SteeringStPWMValue = SteeringStPWMValueFast;        // быстро вдали
    }

    if (!isFreeMode)  // если небезопасный режим, то можно крутить руль
    {
    // теперь выбираем направление вращения
        if (CheckSteeringPosition()) // руль в требуемом положении, стопорим
        {
            if (isUseServoBrake)  // используем тормоз сервопривода
            {          
                SteeringDCMotorControl(0, SteeringStPWMValue);
            }
            else // просто отключаем моторы
            {
                SteeringDCMotorControl(0, 0);
            }
            ctrl = true;  // <-- можно переходить к следующей команде
        } 
        else if (currentvalue < destvalue) // значение меньше - нужно крутить в увеличение
        {
            SteeringDCMotorControl(1, SteeringStPWMValue);
        }
        else // currentvalue > destvalue) // значение больше - нужно крутить в уменьшение
        {
            SteeringDCMotorControl(-1, SteeringStPWMValue);
        }
    }
    return ctrl;    
} // --- end of SteeringContinue() ---------

 
