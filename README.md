# ReIcon

Небольшая утилита для конвертации изображений в **ICO** или **PNG** — с поддержкой альфа-канала, обрезкой, скруглением углов, обводкой и предпросмотром перед сохранением.

## Возможности

- **Перетаскивание / вставка** — перетащите файл изображения или нажмите Ctrl+V
- **Обрезка** — выбор и настройка области под иконку
- **Скругление углов** — радиус скругления с предпросмотром в реальном времени
- **Обводка** — настраиваемый контур и выбор цвета (пипетка, пресеты, круг HSV, ввод hex)
- **Экспорт ICO** — многоразмерный ICO (256 / 64 / 48 / 32 / 16 px) с поддержкой прозрачности
- **Экспорт PNG** — сохранение обработанного изображения в PNG в оригинальном разрешении
- **Светлый / тёмный фон превью** — переключение фона, чтобы оценить вид иконки на разных поверхностях
- **Локализация** — английский и русский; язык подбирается по системе

Поддерживаемые форматы на входе: PNG, JPEG, WebP, BMP, GIF, TIFF, TGA (через ImageSharp).

## Скриншот

<!-- TODO: добавить скриншот приложения -->

## Скачать

На странице [Releases](../../releases) возьмите последний `ReIcon.exe`.  
Сборка **self-contained** — отдельно ставить .NET не нужно.

## Сборка из исходников

**Нужно:** [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) (или новее).

```bash
# Клонировать репозиторий
git clone https://github.com/dinosmike/ReIcon.git
cd ReIcon

# Опубликовать один исполняемый файл (self-contained)
dotnet publish ReIcon/ReIcon.csproj -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true -p:DebugType=None -o publish

# Или просто запустить скрипт
build-release.bat
```

Готовый `ReIcon.exe` лежит в папке `publish/`.

## Стек

- **WPF** (.NET 6, только Windows)
- **SixLabors.ImageSharp** — загрузка и обработка изображений
- **System.Drawing.Common** — резерв для извлечения иконок

## Лицензия

Проект распространяется «как есть» для личного и учебного использования.

## Автор

**Михаил Крылов**

- Telegram: [@mihail_krylov](https://t.me/mihail_krylov)
- GitHub: [dinosmike](https://github.com/dinosmike)
