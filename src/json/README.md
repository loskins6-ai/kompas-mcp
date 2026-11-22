# Разбор и запись JSON

## Overview

`KompasMcp.JsonLib` преобразует JSON-текст в значения .NET и обратно. Эти операции используются для сообщений JSON-RPC, схем инструментов, потока событий и конфигурации «Давинчи». [Rpc.cs](../Rpc.cs), [ToolRegistry.cs](../ToolRegistry.cs), [StreamJson.cs](../davinci/StreamJson.cs), [DavinciConfig.cs](../davinci/DavinciConfig.cs)

## Interface

- `Json.Parse(string)` читает одно значение. Объект становится `Dictionary<string, object>`, массив становится `List<object>`, числа читаются как `double`. Строки, логические значения и `null` возвращаются как `string`, `bool` и `null`. [Json.cs](./Json.cs)
- `Json.Write(object)` возвращает JSON-строку. Для `Dictionary<string, object>` записывает объект, для остальных значений `IEnumerable` после обработки строк записывает массив. Поддерживает числа `int`, `long` и `double`. Для остальных типов записывает JSON-строку из результата `ToString()`. [Json.cs](./Json.cs)
- `RawJson(string)` хранит готовый фрагмент в поле `Text`. `Json.Write` вставляет его буквально, пропуская экранирование и проверку синтаксиса. `Rpc.ToolsList` использует этот путь для `inputSchema`. [Json.cs](./Json.cs), [Rpc.cs](../Rpc.cs)

## Invariants

- `Json.Parse` после значения допускает только пробельные символы, которые пропускает `SkipWs`. При обнаружении ошибки разбора выбрасывается исключение. Повторный ключ объекта замещает предыдущее значение. [Json.cs](./Json.cs)
- `ParseNum` и числовые ветви `WriteValue` используют `CultureInfo.InvariantCulture`. `WriteValue` преобразует `double.NaN` и обе бесконечности в JSON `null`. [Json.cs](./Json.cs)
- `WriteStr` экранирует кавычки, обратную косую черту и управляющие символы ниже U+0020. Остальные символы Unicode записываются непосредственно. [Json.cs](./Json.cs)

## Side Effects

- `Json.Parse` и `Json.Write` обрабатывают входные данные в памяти. `WriteValue` перечисляет переданные коллекции и может вызвать `ToString()` переданного значения. Чтение и запись файлов выполняют вызывающие модули. [Json.cs](./Json.cs), [DavinciConfig.cs](../davinci/DavinciConfig.cs), [McpConfig.cs](../davinci/McpConfig.cs)

## Verification

Из каталога `kompas-mcp` с собранным `KompasMcp.exe` запустите `powershell -ExecutionPolicy Bypass -File tests\protocol_test.ps1`. Скрипт отправляет JSON-RPC-запросы и разбирает полученные ответы через `ConvertFrom-Json`. КОМПАС-3D при этом не запускается. [Сборка сервера](../../build.ps1), [скрипт проверки](../../tests/protocol_test.ps1)
