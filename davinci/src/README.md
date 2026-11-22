# Библиотека «Давинчи» для КОМПАС-3D

## Обзор (Overview)

Исходники `DavinciNative.rtw`. Библиотека загружается в процесс КОМПАС-3D, публикует COM-интерфейс приложения и открывает панель в отдельном процессе.

## Интерфейс (Interface)

- Экспорты `LIBRARYID`, `LIBRARYNAME`, `LIBRARYNAMEW`, `LIBRARYPROTECTNUMBER` и `LibIsOnApplication7` сообщают КОМПАСу идентификатор ресурса, имя, номер защиты и использование API-7. [DavinciNative.cs](./DavinciNative.cs)
- `LibInterfaceNotifyEntry` получает указатель на интерфейс приложения. `LibInterfaceNotifyDisconnect` снимает регистрацию в таблице запущенных COM-объектов (ROT). [DavinciNative.cs](./DavinciNative.cs)
- `LIBRARYENTRY(1)` вызывает `DavinciLauncher.OpenPanel`. `LIBRARYENTRY(2)` сообщает имя ROT-моникера, путь к серверу и журналу. [DavinciNative.cs](./DavinciNative.cs)
- `DavinciLauncher.ResolveExe` ищет существующий `kompasMcpExe` из `davinci.json` рядом с библиотекой, затем `KompasMcp.exe` уровнем выше и в каталоге библиотеки. `OpenPanel` возвращает `null` после запуска процесса или текст ошибки. [DavinciLib.cs](./DavinciLib.cs)

## Инварианты (Invariants)

- `LIBRARYID()` возвращает `100`. Этот идентификатор связывает экспорт с меню и строкой имени в ресурсах. Номера пунктов меню совпадают с аргументами `LIBRARYENTRY`. [DavinciNative.cs](./DavinciNative.cs), [lib.rc](../lib.rc)
- `RegisterInRot` и `OpenPanel` используют одно имя `KOMPAS_DAVINCHI_<PID>`, где `<PID>` относится к процессу КОМПАСа. Панель получает имя в `--rot-name` вместе с `--panel --attach`. [DavinciNative.cs](./DavinciNative.cs), [DavinciLib.cs](./DavinciLib.cs)

## Побочные эффекты (Side Effects)

- `EnsureResolvers` добавляет обработчик `AssemblyResolve` и загружает найденные рядом с библиотекой interop-сборки. `RegisterInRot` удерживает опубликованный COM-объект до отзыва регистрации по сохранённому идентификатору. [DavinciNative.cs](./DavinciNative.cs)
- `OpenPanel` запускает `KompasMcp.exe` без консольного окна. `DavinciLog.Write` дописывает `%TEMP%\kompas-davinci\davinci.log`. `Show` также вызывает `ksMessage`, когда доступен объект API-5. [DavinciLib.cs](./DavinciLib.cs), [DavinciNative.cs](./DavinciNative.cs)

## Проверка (Verification)

Соберите библиотеку и проверьте подключение и оба пункта меню в КОМПАС-3D v22. [Сборка и проверка](../README.md#проверка-verification)
