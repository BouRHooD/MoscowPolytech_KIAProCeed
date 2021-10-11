/*
Мониторинг данных робокара 

Прямое считывание датчиков с Arduino Mega системы управления.

28.10.2020 by Leonov Vladislav 181-311

Конфигурация платы:
A0 - энкодер левого переднего колеса
A1 - энкодер правого переднего колеса
A2 - энкодер левого заднего колеса
A3 - энкодер правого заднего колеса
D8 - кнопка принудительного сброса  (кнопки DVRobot инвертированы, нажато == 0)

Опрос: без задержки // или с заержкой 10 ms 

Вывод значения в Serial - 0,5 сек - в формате counter\n

Управление по Serial
c - сброс (очистка)
p - не отправлять данные (пауза отправки, но не счёта импульсов!!!)
r - отправлять данные

(Устарело) ВНИМАНИЕ!  При старте передача данных остановлена (на паузе), чтобы не "шуметь" в СОМ-порт
(Теперь) После старта появляется сообщение #Ready, а затем делается сброс и начинается счёт и вывод данных

 ВНИМАНИЕ!  Переполнение счётчиков энкодеров не проверяется. 
 Это примерно 1500 оборотов колеса. То есть более километра по прямой. 
 Такое событие может наступить через 5-6 минут непрерывной езды без сброса на скорости 10 км/ч

*/

int sensorEncoderLeftPin = A1;    //  - энкодер левого колеса
int sensorEncoderRightPin = A0;   // - энкодер правого колеса

int EncoderLeftThreshold = 350;   // пороговое значение (низкий / высокий уровень)
int EncoderRightThreshold = 350;

int ledPin = 13;      // select the pin for the LED
int buttonPin = 8;

// счёт времени
unsigned long LastSendingMoment = 0; // время предыдущей отправки
unsigned long CurrentMoment = 0;

// частота (интервал) оправки данных
unsigned long SendingInterval = 500; // 500 ms

unsigned long LeftWheelCounter = 0;       // счётчик энкодера левого колеса
unsigned long RightWheelCounter = 0;      // счётчик энкодера правого колеса

// принцип мигания: - если значение меньше 
int sensorValue_EncoderLeft = 0;    // значение сенсора / максимум 1024
int sensorValue_EncoderRight = 0;     //
int sensorValue_Mix = 0;  // значение произвольного сенсора (при мониторинге работы пинов) 

int LastLeftEncoderLevel = 0;        // уровни (1/0) считанного значения с пина
int LastRightEncoderLevel = 0;
int CurrentLeftEncoderLevel = 0;
int CurrentRightEncoderLevel = 0;

// переменная для считывания команды через серийный порт
char cmd;

// переменная для контроля режима отправки данных
byte SendMode = 0;  // данные отправлять, 0 - не отправлять, 1 - отправлять данные
byte DataMode = 0;  // отправлять сумму, 1-левый энкодер, 2-правый энкодер, 3-аналоговые данные

//#-------- UDP configuration-------#
#include <Ethernet.h>
#include <EthernetUdp.h>
#include <SPI.h>
// Ниже укажите MAC-адрес и IP-адрес вашего контроллера .
// IP-адрес будет зависеть от настроек вашей локальной сети:
// При присваивании IP адрессов следует посмотреть доступные в маршрутизаторе
byte mac[] = {   0x90, 0xA2, 0xDA, 0x10, 0x13, 0xBB };
IPAddress boardIP (192, 168, 0, 202);                       // IP платы
IPAddress hostIP (192, 168, 0, 100);                        // IP server
unsigned int boardPort = 8092;                                // Порт платы
unsigned int hostPort = 9092;                                 // Порт сервера

EthernetUDP UDP;                                            // Создание экземпляра класса EthernetUDP для отправки и получения UDP-пакетов
char packetBuffer[UDP_TX_PACKET_MAX_SIZE];                  // Храним полученный пакет (приемный буфер Udp-пакета)
bool isStarted = false;                                     // Флаг работы программы
String com = "";
void setup() {  // стартовая процедура
  // declare the ledPin as an OUTPUT:
  pinMode(ledPin, OUTPUT);  
  pinMode(buttonPin, INPUT);
    
  delay(1000);
  
  Ethernet.begin(mac, boardIP);                           // Инициализируем плату контроллера
    UDP.begin(boardPort);                                   // Включаем прослушивание порта
    UDP.beginPacket(hostIP, hostPort);                     
    UDP.write("#CVL Wheel Encoders Board Ready!");  
    UDP.endPacket();
    
  // сбрасываем счётчики
  LeftWheelCounter = 0;       
  RightWheelCounter = 0;       
}

void loop() {  // бесконечный цикл

  // кнопка сброса счётчиков

//#1 Начало работы кода при включении платы один раз 
  if(UDP.parsePacket() > 0 && !isStarted){
    UDP.read(packetBuffer, UDP_TX_PACKET_MAX_SIZE);         // Cчитываем принятый пакет в буфер packetBufffer
    String comand = packetBuffer;                       
    comand.toLowerCase();
    if(comand == "start"){
      // Отсылаем пакет данных серверу:  
      UDP.beginPacket(hostIP, hostPort);                     
      UDP.write("#CVL Wheel Encoders Board Online");  
      UDP.endPacket();
      LastSendingMoment = millis();                         // Фиксируем момент последней отправки данных
      SendMode = 1;
      isStarted = true;
    }
  }
  //#1 Конец работы кода при включении платы один раз 
  if (isStarted){
    // обработка ввода-вывода по Ethernet  
    UDP.read(packetBuffer, UDP_TX_PACKET_MAX_SIZE);         // считываем принятый пакет в буфер packetBufffer

    //#2 Начало завершения работы кода 
    String comand = packetBuffer;                       
    comand.toLowerCase();
    if(comand == "end"){
    comand = "";
    // Отсылаем пакет данных серверу:  
    UDP.beginPacket(hostIP, hostPort);                        
    UDP.write("#CVL Wheel Encoders Board Offline");  
    UDP.endPacket();
    SendMode = 0;
    isStarted = false;
    }
    //#2 Конец завершения работы кода  
  
    char cmd = packetBuffer[0];                             // Считываем с принятого буфера первый символ
    switch(cmd){
      case 'C': case 'c': {  // сбрасываем счётчики
        LeftWheelCounter = 0;       
        RightWheelCounter = 0;       
      }; break;
      case 'P': case 'p': {  // ставим отправку на паузу
        SendMode = 0;
      }; break;
      case 'R': case 'r': {  // запускаем отправку
        SendMode = 1;
      }; break;
      case '0': {  // выводить сумму счётчиков
        DataMode = 0;
      }; break;
      case '1': {  // выводить удвоенный левый счётчик
        DataMode = 1;
      }; break;
      case '2': {  // выводить удвоенный правый счётчик
        DataMode = 2;
      }; break;      
      case 'A': case 'a': {  // выводить значения аналоговых портов
        DataMode = 3;
      }; break;
    }  
  }
  // считать сенсоры
  sensorValue_EncoderRight = analogRead(sensorEncoderLeftPin);    
  sensorValue_EncoderLeft = analogRead(sensorEncoderRightPin);

  // обработка энкодеров: аналог в уровень
  if (sensorValue_EncoderLeft > EncoderLeftThreshold) 
  {
     CurrentLeftEncoderLevel = 1;
  } else {
     CurrentLeftEncoderLevel = 0;
  }
  if (sensorValue_EncoderRight > EncoderRightThreshold) 
  {
     CurrentRightEncoderLevel = 1;
  } else {
     CurrentRightEncoderLevel = 0;
  }

  // обработка энкодеров: сравнение старый - новый уровень

  if (LastLeftEncoderLevel != CurrentLeftEncoderLevel) // щелчок в левом энкодере
  {
     LeftWheelCounter++;
     LastLeftEncoderLevel = CurrentLeftEncoderLevel;
  }
  if (LastRightEncoderLevel != CurrentRightEncoderLevel) // щелчок в правом энкодере
  {
     RightWheelCounter++;
     LastRightEncoderLevel = CurrentRightEncoderLevel;
  }

  // обработка времени, выполняем только если включён режим "отправлять данные"
  if (SendMode == 1) 
  {
     CurrentMoment = millis();
     if (CurrentMoment > LastSendingMoment + SendingInterval)
     {
        switch (DataMode)
        {
           case 0: {
              com = "ENC:";
              com += (String)(LeftWheelCounter + RightWheelCounter);
              SendFeedback(com);
           }; break;
           case 1: {
              com = "ENCl:";
              com += (String)(LeftWheelCounter * 2);
              SendFeedback(com);
           }; break;
           case 2: {
              com = "ENCr:";
              com += (String)(RightWheelCounter * 2); 
              SendFeedback(com);
           }; break;
           case 3: {
              com = "#";
              sensorValue_Mix = analogRead(A0);
              com += (String)sensorValue_Mix;
              com += ":";
              sensorValue_Mix = analogRead(A1);
              com += (String)sensorValue_Mix;
              com += ":";
              sensorValue_Mix = analogRead(A2);
              com += (String)sensorValue_Mix;
              com += ":";
              sensorValue_Mix = analogRead(A3);
              com += (String)sensorValue_Mix;
              com += ":";
              sensorValue_Mix = analogRead(A4);
              com += (String)sensorValue_Mix;
              com += ":";
              sensorValue_Mix = analogRead(A5);
              com += (String)sensorValue_Mix;
              SendFeedback(com);
           }; break;
        }
        // регистрируем время отправки
        LastSendingMoment = millis(); // CurrentMoment;
     }
  }
  memset(packetBuffer, 0, UDP_TX_PACKET_MAX_SIZE);        //Очищаем буфер приёма данных  
  // Задержка по времени - поставить ТОЛЬКО ЕСЛИ ВСЁ БУДЕТ ГЛЮЧИТЬ(!!!) из-за дребезга контактов энкодеров
  // delay(10);                  
}

void SendFeedback(String str){
   int strLength = str.length();                            // Длина строки
   char response[strLength+1];                              // Объявляем массив char размером входной строки str
   str.toCharArray(response,sizeof(response));              // Переводим строку в массив char
   UDP.beginPacket(hostIP, hostPort);                       // Начало отправки данных (кому будем отправлять данные)
   UDP.write(response);                                     // Данные,которые отправляем
   UDP.endPacket();                                         // Конец отправки данных
   LastSendingMoment = millis();                            // Фиксируем момент последней отправки данных
}
