# Панель «Давинчи»

## Overview

Панель WPF показывает диалог копилота, вызовы инструментов, состояние КОМПАС-3D и Claude. В режиме `--panel` точка входа `Program.PanelMain` связывает окно с `DavinciSession`, запускает цикл MCP в фоне и ждёт закрытия окна. [Точка входа](../Program.cs), [сессия копилота](../davinci/DavinciSession.cs).

## Interface

- `DavinciPanel.Start`, `WaitClosed` и `Close` управляют жизненным циклом окна. `SubmitHandler`, `AbortHandler` и `NewDialogHandler` связывают пользовательские действия с сессией. `ClaudeStatusProvider` возвращает `ServiceState` для индикатора. [DavinciPanel.cs](./DavinciPanel.cs).
- `DavinciPanel.Busy`, `AppendAssistant`, `ToolCall`, `ToolResult`, `TurnDone`, `Notice`, `Error` и `RefreshClaudeStatus` обновляют окно из событий сессии. `PanelWindow.Submit`, `AttachFiles` и `AttachFile` обрабатывают ввод и вложения. [DavinciPanel.cs](./DavinciPanel.cs).
- `SettingsPage.Load` заполняет форму из `DavinciConfig`. После подтверждения `PanelWindow.CloseSettings` передаёт `SettingsPage.Result` в `DavinciSession.Apply` и применяет параметры окна. [SettingsPage.cs](./SettingsPage.cs), [DavinciPanel.cs](./DavinciPanel.cs).
- `Markdown.Build` преобразует ответы в `FlowDocument`. `Styles.Load` загружает WPF-стили, `Styles.Icon` создаёт иконки, `Theme.Apply` и `Theme.Publish` обновляют палитру. [Markdown.cs](./Markdown.cs), [Styles.cs](./Styles.cs), [Theme.cs](./Theme.cs).

## Invariants

- `DavinciPanel.Start` создаёт STA-поток для окна. Именованные мьютекс и событие позволяют другому процессу передать существующей панели сигнал показа. `Post` направляет обновления через `Dispatcher.BeginInvoke`. Обработчики действий пользователя вызываются в потоке окна. [DavinciPanel.cs](./DavinciPanel.cs).
- `PanelWindow.Submit` требует текст или вложение и отклоняет отправку во время текущего запроса. Если провайдер после повторной проверки возвращает `Ok == false` и `Hard == true`, текст и вложения остаются в форме. Без `SubmitHandler` окно показывает эхо-режим. [DavinciPanel.cs](./DavinciPanel.cs).
- `SettingsPage.Build` принимает таймаут от 1 до 600 целых минут. Для Ollama и другого API обязательны адрес и модель. `Theme.Publish` передаёт в ресурсы WPF замороженные копии кистей, поэтому `Theme.Apply` может менять цвета кистей, уже используемых окном. [SettingsPage.cs](./SettingsPage.cs), [Theme.cs](./Theme.cs).

## Side Effects

- `PanelWindow.AttachFile` копирует файл в `<workspace>\attach`, добавляя числовой суффикс при совпадении имени. `Submit` добавляет пути в блок `[Вложения пользователя]`. [DavinciPanel.cs](./DavinciPanel.cs), [DavinciSession.cs](../davinci/DavinciSession.cs).
- `Theme.SaveMode`, `SavePinned` и `SaveBounds` пишут в `HKCU\Software\KompasMcp\Davinci`. `DavinciSession.Apply` сохраняет конфигурацию копилота и обновляет конфигурацию MCP для следующих запросов. [Theme.cs](./Theme.cs), [DavinciSession.cs](../davinci/DavinciSession.cs).
- `PanelWindow.CheckClaude` вызывает провайдер состояния в фоне. `SettingsPage.Test` запускает `ClaudeProbe.TestNow`, а `LoadOllamaModels` запрашивает `/api/tags` у указанного сервера. По щелчку `Markdown.Open` передаёт Windows ссылки HTTP(S) и существующие файлы, распознанные по `PathPattern`. [DavinciPanel.cs](./DavinciPanel.cs), [SettingsPage.cs](./SettingsPage.cs), [Markdown.cs](./Markdown.cs).

## Verification

Команды запускаются из каталога `kompas-mcp` на Windows. Скрипт сборки теста берёт interop DLL из родительского каталога `kompas-mcp`. [Сборка сервера](../../build.ps1), [сборка теста](../../tests/build_panel_test.ps1), [исходник теста](../../tests/panel_test.cs).

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
powershell -ExecutionPolicy Bypass -File .\tests\build_panel_test.ps1
.\tests\panel_test.exe
```
