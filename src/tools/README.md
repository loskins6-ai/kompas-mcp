# Инструменты MCP для КОМПАС-3D

## Overview

`ToolRegistry` вызывает методы `Register` и собирает имена инструментов, входные схемы и обработчики. `Rpc` публикует схемы через `tools/list` и передаёт аргументы обработчику через `tools/call`. Аргументы читают сами обработчики, в том числе через `ToolRegistry.GetStr`, `GetDbl` и `GetInt`. [Реестр](../ToolRegistry.cs), [RPC](../Rpc.cs), [справочник аргументов](../../docs/tools.md).

## Interface

- `InfraTools.Register` предоставляет `ping`, `kompas_status`, `list_open_documents`, `kompas_start`, `kompas_show` и `kompas_stop`. Жизненный цикл экземпляра и подключение через COM обслуживает `KompasHost`. [InfraTools.cs](./InfraTools.cs), [KompasHost.cs](../KompasHost.cs).
- `Tools2D.Register` предоставляет создание и закрытие чертежа, виды, примитивы, размеры, штриховку, оформление, сохранение и экспорт. `CreateDrawing`, `GetDoc` и `MakeView` также вызывают генераторы чертежей. `DrawKit.Fit`, `ParamTable`, `TechReq`, `Stamp` и `Finish` обеспечивают их общую компоновку и оформление. [Tools2D.cs](./Tools2D.cs), [DrawKit.cs](./DrawKit.cs).
- `Tools3D.Register` предоставляет создание и открытие детали или сборки, `sketch`, операции с телом, материал, сохранение, рендер, вставку и размещение компонентов. `mass_properties` перестраивает модель и возвращает `mass_kg`. Внутренние `PolySketch`, `Spiral`, `Evolution`, `Loft`, `CircularCopyZ` и `PlacePartAxes` используются генераторами. [Tools3D.cs](./Tools3D.cs).
- `ToolsCalc.Register` предоставляет справочник материалов, посадки, допуски и характеристики сечений. `ToolsCheck.Register` предоставляет `gear_check`, `worm_check` и `spring_check` с численными результатами, полем `ok`, вердиктом и строками `tech` для оформления чертежа. [ToolsCalc.cs](./ToolsCalc.cs), [ToolsCheck.cs](./ToolsCheck.cs).
- `ToolsStd.Register` предоставляет `std_part` для крепежа и подшипников. `ToolsGear.Register` предоставляет `gear_calc`, `gear_wheel` и `gear_pair`. `ToolsGear3D.Register` предоставляет 3D-модели червяка, червячного и конического колёс и сборки обеих передач. [ToolsStd.cs](./ToolsStd.cs), [ToolsGear.cs](./ToolsGear.cs), [ToolsGear3D.cs](./ToolsGear3D.cs).
- `ToolsSpring.Register` предоставляет `spring_calc`, `spring_drawing` и `spring_create`. `ToolsBevelWorm.Register` предоставляет `bevel_gear_drawing`, `worm_drawing` и `worm_wheel_drawing`. [ToolsSpring.cs](./ToolsSpring.cs), [ToolsBevelWorm.cs](./ToolsBevelWorm.cs).
- `ToolsAsmDrawing.Register` предоставляет `assembly_drawing` с позициями компонентов и спецификацией по умолчанию. `ToolsPartDrawing.Register` предоставляет `part_drawing` с ассоциативными видами и `view_project` для проекции точек на вид. `ToolsSpc.Register` предоставляет `spec_drawing` для таблицы спецификации на одном листе А4. [ToolsAsmDrawing.cs](./ToolsAsmDrawing.cs), [ToolsPartDrawing.cs](./ToolsPartDrawing.cs), [ToolsSpc.cs](./ToolsSpc.cs).
- `ToolsImage.Register` предоставляет `image_view` для чтения растрового изображения целиком или по области. `ToolContent.Of` собирает MCP-элементы `image` с PNG в Base64 и `text` с координатами области и масштабом. [ToolsImage.cs](./ToolsImage.cs), [Rpc.cs](../Rpc.cs).

## Invariants

- `Tools2D.GetDoc` требует чертёж, созданный `CreateDrawing`. `MakeView` делает новый вид текущим. `Tools3D.GetPart` требует созданную или открытую модель. `GetSketch` выбирает эскиз по `sketchId` или берёт последний, а `Activate` очищает эскизы при смене 3D-документа. При остановке или отсоединении `KompasHost.ResetDocumentStatics` сбрасывает ссылки на 2D- и 3D-документы. [Tools2D.cs](./Tools2D.cs), [Tools3D.cs](./Tools3D.cs), [KompasHost.cs](../KompasHost.cs).
- `Tools3D.Extrude` и `Revolve` после перестроения проверяют изменение массы, а при вырезании пробуют несколько вариантов направления или стороны. `AddComponentImpl` требует существующий файл, а `PlacePartAxes` сверяет записанные оси компонента перед `UpdatePlacement`. [Tools3D.cs](./Tools3D.cs).
- `ToolsPartDrawing.ViewProject` проверяет, что текущий путь чертежа совпадает с путём последнего `part_drawing`, и открывает выбранный вид для следующих 2D-операций. Возвращаемые точки заданы в координатах вида. `ToolsImage.View` принимает область `[x0, y0, x1, y1]` в долях размеров изображения, с началом в левом верхнем углу. [ToolsPartDrawing.cs](./ToolsPartDrawing.cs), [ToolsImage.cs](./ToolsImage.cs).
- `kompas_start` и `kompas_show` недоступны в `--attach`. `kompas_stop` в этом режиме отсоединяет сервер. `ToolsCalc`, `ToolsCheck`, `gear_calc` и `spring_calc` выполняют расчёты без COM. [InfraTools.cs](./InfraTools.cs), [KompasHost.cs](../KompasHost.cs), [ToolsCalc.cs](./ToolsCalc.cs), [ToolsCheck.cs](./ToolsCheck.cs), [ToolsGear.cs](./ToolsGear.cs), [ToolsSpring.cs](./ToolsSpring.cs).

## Side Effects

- Первое обращение к `KompasHost.Kompas` может запустить скрытый экземпляр КОМПАСа в режиме `own` или подключиться к существующему в `attach`. `kompas_stop` в режиме `own` завершает отслеживаемый процесс, если определён его PID. [KompasHost.cs](../KompasHost.cs), [InfraTools.cs](./InfraTools.cs).
- 2D- и 3D-операции создают и изменяют документы через COM. `save_document`, `SavePart` и генераторы пишут `.cdw`, `.m3d` и `.a3d`. `export_dxf`, `RenderPng` и `Render3D` пишут файлы экспорта. `Paths.Abs` разрешает относительные пути от каталога `KompasMcp.exe`, а `Paths.Out` формирует пути в `mcp-out` рядом с ним. [Tools2D.cs](./Tools2D.cs), [Tools3D.cs](./Tools3D.cs), [DrawKit.cs](./DrawKit.cs), [Paths.cs](../Paths.cs).
- `gear_pair`, `worm_pair` и `bevel_pair` сохраняют детали рядом со сборкой и вставляют их внешними ссылками. `assembly_drawing` при включённой спецификации сохраняет и закрывает сборочный чертёж, затем создаёт отдельный файл спецификации. `spring_create` может переоткрыть сохранённую деталь для получения массы. [ToolsGear.cs](./ToolsGear.cs), [ToolsGear3D.cs](./ToolsGear3D.cs), [ToolsAsmDrawing.cs](./ToolsAsmDrawing.cs), [ToolsSpring.cs](./ToolsSpring.cs), [Tools3D.cs](./Tools3D.cs).

## Verification

Из каталога `kompas-mcp` соберите сервер и проверьте протокол, включая `tools/list`, без запуска КОМПАСа. [Сборка](../../build.ps1), [проверка протокола](../../tests/protocol_test.ps1).

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
powershell -ExecutionPolicy Bypass -File .\tests\protocol_test.ps1
```

При установленном КОМПАС-3D v22 можно запустить сценарии 2D и 3D. Они открывают собственный экземпляр КОМПАСа и записывают результаты в `mcp-out`. [Скрипт запуска](../../tests/run_smoke.ps1), [пример 2D](../../tests/smoke_flange.jsonl), [пример 3D](../../tests/smoke_3d.jsonl).

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\run_smoke.ps1 .\tests\smoke_flange.jsonl
powershell -ExecutionPolicy Bypass -File .\tests\run_smoke.ps1 .\tests\smoke_3d.jsonl
```
