WheelEncoders

Скетч для Arduino UNO для обработки данных колёсных энкодеров Kia Rio проекта РобокарУМ

Версия: 22.06.2017 г.

Автор: Идиатуллов Т.Т.

Подключение сигнальных проводов:
  A0 - энкодер левого колеса
  A1 - энкодер правого колеса
  GND на кузов автомобиля (общий "ноль"). Ни в коем случае не через выключатель, а именно на корпус. 
       Иначе можно случайно сжечь плату.

По возможности поставить на сигнальные провода защиту от перенапряжения 5В. 
Через стабилитроны, например.
В процессе работы считываются импульсы двух датчиков ABS. Условно левого и правого.
По документации, сигнал 0 - 1,5 В
Датчики ABS ненаправленные, поэтому при переключении направления движения счётчики нужно сбрасывать.
Выводится сумма двух энкодеров или удвоенное значение одного из энкодеров (в зависимости от режима). 

Использование:

Подключить монитор СОМ-порта
Счёт идёт от старта ардуинки

Включить режим отправки данных энкодеров командой:  r  - режим по умолчанию
Включить режим паузы отправки данных:  p
Сбросить счётчики:  c
Использовать усреднение: 0   - режим по умолчанию
Считать по левому энкодеру: 1
Считать по правому энкодеру: 2
Аналоговые данные:  а   - считываются шесть аналоговых портов и записываются в порт в формате #A0:A1:A2:A3:A4:A5\n     , счёт импульсов не выполняется.

ВАЖНО!  Пауза отправки данных не прекращает счёта импульсов, просто данные не передаются на ПК.

Формат передаваемых данных (частота передачи, примерно 2 раза в секунду):  counter\n

Служебные сообщения выводятся в формате #message\n


