# Overworked-Rework

Проект для реконструкции рабочей сцены третьего этажа из [Overworked](https://github.com/Gekataaaaa/Overworked).

Unity **6000.5.6f1**, URP 2D.

## Что внутри

- **Сцена:** `Assets/_Project/Scenes/Work_P1/Floor_03_Work.unity`. Это единственная сцена в Build Settings.
- **Механики и скрипты**, от которых зависит сцена: игрок (бег, стамина, фонарик), взаимодействие, двери и турникет, шкафчик, Enemy01, кофе и хотбар, лампы и мерцание, чекпоинты, санити и пульс, диалоги, цели, телефон, сохранения.
- **Системы из `_Systems`** (загружает `Bootstrap`) и меню паузы, чтобы сцена в Play Mode работала как в игре.
- **Билдеры сцены** в `Assets/_Project/Editor`: GrayboxTool, WorkCanteenBuilder, WorkKitchenTable, WorkWidenRooms, LockerBuilder, CoffeeBuilder, Enemy01Builder и их зависимости.

GUID ассетов (`.meta`) совпадают с Overworked, поэтому файлы можно переносить между проектами в обе стороны.

## Чего нет

Других этажей, главного меню, белой комнаты и Tiny UFO. В коде остались ссылки на них в виде строковых путей, например в `RoomWiring` и `SaveSystem`. Если эти сцены понадобятся, их нужно перенести из Overworked.
