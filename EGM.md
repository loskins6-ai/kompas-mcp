# Архитектура исходников kompas-mcp

## Overview

Сервер связывает MCP-клиент с КОМПАС-3D v22 по stdio и COM. В репозитории есть отдельная библиотека «Давинчи», которая добавляет в меню КОМПАСа запуск панели чата. Установка, примеры и ограничения описаны в [README сервера](./README.md).

## Interface

- [Сервер и WPF-панель](./src/README.md) входят в `KompasMcp.exe`. Точка входа и список инструментов описаны в [Program.cs](./src/Program.cs) и [справочнике MCP](./docs/tools.md).
- [Библиотека «Давинчи»](./davinci/README.md) собирается отдельным скриптом и подключается к КОМПАСу как `DavinciNative.rtw`.
- `build.ps1` собирает сервер. `interop.ps1 [-Target <папка>]` готовит interop-сборки из SDK установленного КОМПАСа. `package.ps1` собирает архив выпуска. [build.ps1](./build.ps1) · [interop.ps1](./interop.ps1) · [package.ps1](./package.ps1)

## Invariants

- `build.ps1` компилирует `src/**/*.cs` в x64 EXE и использует interop-DLL из `lib/` или родительского каталога. Если `Kompas6API5.dll` отсутствует в обоих местах, скрипт вызывает `interop.ps1`. [build.ps1](./build.ps1)
- MCP по stdio использует отдельную строку для каждого сообщения JSON-RPC. Запросы CAD выполняются в собственном экземпляре КОМПАСа или через attach, в зависимости от аргументов запуска. [Program.cs](./src/Program.cs) · [KompasHost.cs](./src/KompasHost.cs)
- `package.ps1` использует уже подготовленный `DavinciNative.rtw` и упаковывает файлы без interop-DLL КОМПАСа. [package.ps1](./package.ps1)

## Side Effects

- `build.ps1` создаёт `KompasMcp.exe` и копирует к нему interop-DLL и изображения панели. `interop.ps1` читает путь к КОМПАСу из реестра Windows и записывает DLL в целевой каталог. [build.ps1](./build.ps1) · [interop.ps1](./interop.ps1)
- `package.ps1` перегенерирует `docs/tools.md` из реестра инструментов и заменяет архив и промежуточный каталог выпуска той же версии в `dist/`. [package.ps1](./package.ps1)
- При выполнении инструментов сервер может менять открытые CAD-документы и записывать модели и экспорт в `mcp-out` или по заданному пути. [MCP-инструменты](./src/tools/README.md) · [Paths.cs](./src/Paths.cs)

## Verification

Из корня `kompas-mcp` соберите сервер и проверьте протокол. Тест ожидает `KompasMcp.exe` рядом с `build.ps1` и выполняется при закрытом КОМПАСе. [build.ps1](./build.ps1) · [protocol_test.ps1](./tests/protocol_test.ps1)

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
powershell -ExecutionPolicy Bypass -File .\tests\protocol_test.ps1
```

Сценарии CAD и проверки копилота перечислены в [README сервера](./README.md#тесты-и-сценарии).
