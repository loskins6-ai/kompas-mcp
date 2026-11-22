# Подключение и сборка библиотеки «Давинчи»

## Обзор (Overview)

Подключаемая библиотека для КОМПАС-3D v22 добавляет меню «Давинчи» и связывает запущенный экземпляр КОМПАСа с панелью копилота.

## Интерфейс (Interface)

- Библиотека `DavinciNative.rtw` открывает панель и регистрирует COM-интерфейс приложения. [Исходники библиотеки](./src/README.md)
- `build-native.ps1` компилирует C# через `csc`, преобразует сборку в IL через `ildasm` и собирает `.rtw` через `ilasm`. `inject_exports.py` принимает входной и выходной IL-файлы и добавляет восемь директив `.export`. `lib.rc` задаёт пункты «Панель» и «Диагностика». [Сборка](./build-native.ps1), [экспорты](./inject_exports.py), [ресурсы](./lib.rc)
- `AttachDavinci.Main` подключает `DavinciNative.rtw` из каталога своей программы через `ksAttachKompasLibrary` в уже запущенном `KOMPAS.Application.5`. [attach_davinci.cs](./attach_davinci.cs)
- `gen_avatar.ps1` растеризует `avatar.svg` в Edge или Chrome и создаёт `avatar.png` и `avatar.ico` для сборки `KompasMcp.exe`. [Генерация аватара](./gen_avatar.ps1), [сборка сервера](../build.ps1)

## Инварианты (Invariants)

- `build-native.ps1` собирает PE32+ для x64 с `/pe64 /x64`. Список `EXPORTS` ссылается на одноимённые методы `DavinciNative`. [build-native.ps1](./build-native.ps1), [inject_exports.py](./inject_exports.py), [DavinciNative.cs](./src/DavinciNative.cs)
- `lib.rc` читается в UTF-8 по `#pragma code_page(65001)`. Идентификатор ресурса `100` и номера меню `1` и `2` согласованы с `LIBRARYID` и `LIBRARYENTRY`. [lib.rc](./lib.rc), [DavinciNative.cs](./src/DavinciNative.cs)

## Побочные эффекты (Side Effects)

- `build-native.ps1` записывает промежуточные файлы и `DavinciNative.rtw` в каталог скрипта, а также копирует туда доступные interop-сборки. На время пересборки закройте КОМПАС, чтобы освободить загруженный `.rtw`. [build-native.ps1](./build-native.ps1)
- `AttachDavinci.Main` подключает библиотеку к рабочему экземпляру КОМПАСа. `gen_avatar.ps1` записывает изображения рядом с исходным SVG, используя временный каталог браузера. [attach_davinci.cs](./attach_davinci.cs), [gen_avatar.ps1](./gen_avatar.ps1)

## Проверка (Verification)

Из каталога `kompas-mcp` запустите `powershell -ExecutionPolicy Bypass -File .\davinci\build-native.ps1`. Скрипт использует x64 `csc`, `ildasm`, `ilasm`, `rc`, Python и interop-сборки. Пути к инструментам и порядок поиска библиотек заданы в скрипте. [build-native.ps1](./build-native.ps1)

Подключите `DavinciNative.rtw` через менеджер приложений КОМПАС-3D v22. Проверьте, что пункт «Панель» открывает чат, а «Диагностика» записывает в журнал моникер текущего процесса и найденный путь к серверу. [Руководство оператора](../docs/operator-guide.md#310-копилот-давинчи)
