# Ядро kompas-mcp

## Overview

Ядро принимает запросы MCP, находит зарегистрированный обработчик и обслуживает COM-подключение к КОМПАС-3D v22. Режим панели «Давинчи» подключает ту же точку входа к WPF и сессии копилота.

## Interface

- `Program.Main` запускает stdio-сервер или панель (`--panel`). `--version` выводит версию, `--tools-md [файл]` вызывает `ToolsDoc.Markdown` для генерации справочника. [Program.cs](./Program.cs) · [ToolsDoc.cs](./ToolsDoc.cs)
- `Rpc.Handle` обрабатывает строку JSON-RPC 2.0. Методы сервера — `initialize`, `ping`, `tools/list`, `tools/call`. [Rpc.cs](./Rpc.cs)
- `ToolRegistry.Add`, `Find` и `All` задают, ищут и перечисляют `ToolDef`. В каждой записи есть имя, описание, входная схема и обработчик. [ToolRegistry.cs](./ToolRegistry.cs)
- `KompasHost.Kompas` и `App7` предоставляют COM-интерфейсы API-5 и API-7. `Status` возвращает режим, PID, состояние окна и список документов. [KompasHost.cs](./KompasHost.cs)
- [MCP-инструменты](./tools/README.md) реализуют операции, которые публикует `ToolRegistry`. [Расчёты по ГОСТ](./gost/README.md) дают им геометрию, справочные данные и проверки.
- [JSON](./json/README.md) разбирает сообщения и сериализует ответы. [Сессия «Давинчи»](./davinci/README.md) запускает Claude CLI, а [WPF-панель](./Ui/README.md) отображает чат и настройки.

## Invariants

- `Program.StdioLoop` последовательно обрабатывает сообщения UTF-8 по одной строке. `Rpc.Handle` пропускает уведомления без `id`. Ошибки `ToolException` возвращает как `result.isError`. [Program.cs](./Program.cs) · [Rpc.cs](./Rpc.cs)
- `ToolRegistry.Add` отклоняет повторное имя и синтаксически неверную JSON-схему. `Rpc.ToolsList` и `ToolsDoc.Markdown` читают один реестр. [ToolRegistry.cs](./ToolRegistry.cs) · [Rpc.cs](./Rpc.cs) · [ToolsDoc.cs](./ToolsDoc.cs)
- В режиме `--attach` методы `Start` и `Show` отклоняют запрос, а `Stop` освобождает подключение. `KompasHost.Status` без текущего COM-объекта возвращает `running: false` без запуска CAD. [KompasHost.cs](./KompasHost.cs)

## Side Effects

- `KompasHost.Start` создаёт `KOMPAS.Application.5`. `ConfigureFromArgs` в режиме attach ищет существующий экземпляр через COM ROT. `Stop` в собственном режиме завершает процесс по PID, полученному из снимков до и после запуска. При выходе `Shutdown` оставляет видимый собственный экземпляр открытым. [KompasHost.cs](./KompasHost.cs)
- `Paths.Abs` разрешает относительные пути от каталога EXE, `Paths.Out` создаёт каталог `mcp-out` при первом запросе пути. `Log.Write` дописывает диагностику в `kompas-mcp.log`. [Paths.cs](./Paths.cs) · [Log.cs](./Log.cs)

## Verification

Из корня `kompas-mcp` после сборки запустите тест протокола. Он проверяет MCP-ответы без запуска КОМПАС-3D. [protocol_test.ps1](../tests/protocol_test.ps1)

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\protocol_test.ps1
```
