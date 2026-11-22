# Сессия копилота «Давинчи»

## Обзор (Overview)

Пакет входит в `KompasMcp.exe`. `Program.PanelMain` связывает WPF-панель с сессией, которая запускает Claude Code с MCP-сервером в режиме `--attach`. [Program.cs](../Program.cs), [DavinciPanel.cs](../Ui/DavinciPanel.cs)

## Интерфейс (Interface)

- `DavinciSession.Init` загружает настройки и записывает конфигурацию MCP. `Submit` ставит запуск запроса в очередь пула потоков. `Abort` и `Shutdown` останавливают выполняющийся запрос. `Reset` сбрасывает идентификатор диалога, `Config` возвращает копию настроек, `Apply` сохраняет их для следующих запросов, `AttachmentDir` возвращает каталог вложений, `CheckClaude` отдаёт состояние для индикатора. [DavinciSession.cs](./DavinciSession.cs)
- `ClaudeRunner.Run`, `Stop` и `WaitForExit` управляют процессом по `ClaudeRunnerOptions`. Обработчики `OnEvent`, `OnError` и `OnExit` могут вызываться из фоновых потоков. `StreamEvent.Parse` разбирает одну JSON-строку и извлекает идентификатор сессии, текст, имена инструментов и признаки ошибок. [ClaudeRunner.cs](./ClaudeRunner.cs), [StreamJson.cs](./StreamJson.cs)
- `DavinciConfig.Load`, `Save`, `Clone`, `ResolvedProvider` и `EffectiveEnv` обслуживают `davinci.json` рядом с exe или в подкаталоге `davinci/`. `McpConfig.Write` возвращает путь к служебному JSON для CLI. [DavinciConfig.cs](./DavinciConfig.cs), [McpConfig.cs](./McpConfig.cs)
- `ClaudeProbe.Check` проверяет наличие CLI и HTTP-доступность API. `TestNow` проверяет модель запросом к стороннему API при известном имени модели либо запускает короткий сеанс CLI для стандартного входа Claude Code. `EffectiveModel` выбирает модель, а `Friendly` переводит типичные ошибки в текст для панели. [ClaudeProbe.cs](./ClaudeProbe.cs)

## Инварианты (Invariants)

- `Submit` и `Reset` проверяют `runner.Running`. `Submit` передаёт последний `session_id` в `ClaudeRunner.Run` для `--resume`, а `Reset` очищает его для следующего запроса. `OnEvent` выводит текст успешного `result`, если в ходе ещё не было текста ассистента или ошибки API. [DavinciSession.cs](./DavinciSession.cs), [ClaudeRunner.cs](./ClaudeRunner.cs)
- `ClaudeRunner.Run` требует `McpConfigPath`. Сообщение передаётся через stdin байтами UTF-8. `BuildArgs` передаёт `--strict-mcp-config`, а с настройками по умолчанию также `--allowedTools mcp__kompas-mcp` и `--restricted`. `McpConfig.Write` задаёт `mcpServers.kompas-mcp.args` равным `["--attach"]`. [ClaudeRunner.cs](./ClaudeRunner.cs), [McpConfig.cs](./McpConfig.cs)
- При пустом `Provider` источник определяется по `ANTHROPIC_BASE_URL` из `Env`. `EffectiveEnv` накладывает `Env` поверх параметров источника. `Save` сохраняет неизвестные ключи и при явном `Provider` удаляет из `Env` управляемые источником переменные. [DavinciConfig.cs](./DavinciConfig.cs)

## Побочные эффекты (Side Effects)

- `Run` запускает дочерний процесс через `cmd.exe`, дополняет окружение из реестра Windows и конфига, читает `stream-json` и записывает stderr в журнал сервера. `Stop` вызывает `taskkill /T /F` для дерева дочерних процессов. При положительном `TimeoutSec` это делает также таймер. [ClaudeRunner.cs](./ClaudeRunner.cs)
- `McpConfig.Write` перезаписывает `%TEMP%\kompas-davinci\mcp-config.json`, `DavinciConfig.Save` записывает `davinci.json`, `WorkspaceDir` и `AttachmentDir` создают рабочий каталог и подкаталог `attach`. По умолчанию рабочий каталог называется `mcp-out` и находится рядом с exe. [McpConfig.cs](./McpConfig.cs), [DavinciConfig.cs](./DavinciConfig.cs), [DavinciSession.cs](./DavinciSession.cs)
- `ClaudeProbe.Check` посылает HTTP GET к адресу API. При полной проверке стороннего API и известной модели `TinyRequest` отправляет POST на `/v1/messages` с `max_tokens` равным `1`. `TestNow` при стандартном входе запускает отдельный `claude -p` после успешной проверки доступности. [ClaudeProbe.cs](./ClaudeProbe.cs)

## Проверка (Verification)

Из каталога `kompas-mcp` запустите `powershell -ExecutionPolicy Bypass -File .\build.ps1` для сборки сервера с пакетом. Для отдельных проверок раннера и сессии используются `tests\build_runner_test.ps1` и `tests\build_session_test.ps1`, которые компилируют `tests\runner_test.exe` и `tests\session_test.exe` соответственно. [Сборка сервера](../../build.ps1), [сборка проверки раннера](../../tests/build_runner_test.ps1), [сборка проверки сессии](../../tests/build_session_test.ps1)
