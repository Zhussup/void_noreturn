# VOID: NO RETURN — правила для Claude

- Unity 6.6, Built-in Render Pipeline. URP и HDRP не использовать.
- Материалы только Standard. Экраны и вид за окном — Unlit/Color.
- Я (Алауддин) отвечаю за окружение, модели, материалы и свет:
  сцена Assets/VOID/Scenes/Env_Room.unity и префабы окружения.
  Сцену логики и скрипты геймплея (зона Жусупа) не трогать,
  камеру не программировать — только пустые якоря Cam_*.
- Папки: Assets/VOID/Art/{Models,Textures,Materials,Screens}, Assets/VOID/Audio,
  Assets/VOID/Fonts, Assets/VOID/Prefabs, Assets/VOID/Scenes.
- Имена файлов: SM_ модель, MAT_ материал, TX_ текстура, PRB_ префаб
  (MAT_Desk_Wood, PRB_Phone).
- Имена объектов сцены — контракт с кодом, не менять: Desk, Chair,
  Monitor_Terminal, Monitor_Chat, Laptop, Phone, Poster, TrashBin, Door, Wall_*.
  Остальные в том же стиле (Recorder, DeskLamp, WindowFrame…).
- Каждый объект — префаб PRB_* с пустым корнем; меш — дочерний объект Mesh
  (его потом заменяют на модель). Пивот — в основании по центру.
- Коллайдеры только BoxCollider, MeshCollider нельзя.
- Масштаб: 1 юнит = 1 метр, Y вверх.
- Сюжет не выдумывать. Конфликт размеров — остановиться и спросить.
- Существующие папки не переименовывать.
- Перед крупными изменениями сцены напоминать сделать git commit.
