using System;
using System.Collections.Generic;
using System.Globalization;
using Kompas6API5;
using KompasAPI7;

namespace KompasMcp.Tools
{
  internal static class PartInspection
  {
    internal static void Register()
    {
      ToolRegistry.Add("part_variables", "Переменные верхнего уровня текущей 3D-модели.", "{}",
        a => ListVariables());
      ToolRegistry.Add("set_part_variable", "Изменить независимую числовую переменную текущей 3D-модели, проверить прежнее значение и перестроить. Сохранение отдельной командой.",
        @"{""type"":""object"",""properties"":{""name"":{""type"":""string""},""expected"":{""type"":""number""},""value"":{""type"":""number""}},""required"":[""name"",""expected"",""value""]}",
        a => SetVariable(a));
      ToolRegistry.Add("inspect_round_openings", "Измерить круглые внутренние контуры плоских граней, ограниченные одним кольцевым ребром и смежной цилиндрической стенкой. Группировка по стенке, центры в системе модели, размеры в мм.", "{}",
        a => RoundOpenings());
    }

    static IFeature7 RootFeature()
    {
      return (IFeature7)Tools3D.GetDoc7().TopPart;
    }

    static object ListVariables()
    {
      IFeature7 root = RootFeature();
      var variables = new List<object>();
      int count = root.get_VariablesCount(false, false);
      for (int i = 0; i < count; i++)
      {
        IVariable7 v = root.get_Variable(false, false, i);
        variables.Add(new Dictionary<string, object> {
          { "name", v.Name }, { "value", v.Value }, { "expression", v.Expression }, { "note", v.Note }
        });
      }
      return new Dictionary<string, object> { { "variables", variables } };
    }

    static object SetVariable(Dictionary<string, object> args)
    {
      string name = ToolRegistry.GetStr(args, "name");
      double expected = ToolRegistry.GetDbl(args, "expected"), value = ToolRegistry.GetDbl(args, "value");
      if (Double.IsNaN(expected) || Double.IsInfinity(expected) || Double.IsNaN(value) || Double.IsInfinity(value))
        throw new ToolException("Ожидались конечные числовые значения");
      IVariable7 v = RootFeature().get_Variable(false, false, name);
      if (v == null || v.Name != name) throw new ToolException("Переменная не найдена: " + name);
      string expression = v.Expression;
      double literal;
      if (v.ReadOnly || v.Information || !String.IsNullOrEmpty(v.LinkVariableName) ||
          (!String.IsNullOrWhiteSpace(expression) &&
           !Double.TryParse(expression.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out literal)))
        throw new ToolException("Переменная зависимая или доступна только для чтения: " + name);
      double previous = v.Value;
      if (Math.Abs(previous - expected) > 1e-8)
        throw new ToolException("Прежнее значение не совпало: " + name + " = " + previous);
      try
      {
        v.Value = value;
        if (!Tools3D.GetDoc7().RebuildDocument() || Math.Abs(v.Value - value) > 1e-8)
          throw new ToolException("Изменение не подтверждено перестроением: " + name);
      }
      catch
      {
        v.Value = previous;
        if (!Tools3D.GetDoc7().RebuildDocument() || Math.Abs(v.Value - previous) > 1e-8)
          throw new ToolException("Не удалось восстановить " + name + ". Закройте документ без сохранения.");
        throw;
      }
      return new Dictionary<string, object> { { "name", name }, { "previous", previous }, { "value", v.Value } };
    }

    static object RoundOpenings()
    {
      ksBodyCollection bodies = (ksBodyCollection)Tools3D.GetPart().BodyCollection();
      var walls = new List<object>();
      for (int b = 0; b < bodies.GetCount(); b++)
      {
        ksBody body = (ksBody)bodies.GetByIndex(b);
        ksFaceCollection faces = (ksFaceCollection)body.FaceCollection();
        var openings = new Dictionary<int, List<object>>();
        for (int f = 0; f < faces.GetCount(); f++)
        {
          ksFaceDefinition plane = (ksFaceDefinition)faces.GetByIndex(f);
          if (!plane.IsPlanar()) continue;
          ksLoopCollection loops = (ksLoopCollection)plane.LoopCollection();
          for (int l = 0; l < loops.GetCount(); l++)
          {
            ksLoop loop = (ksLoop)loops.GetByIndex(l);
            if (loop.IsOuter()) continue;
            ksEdgeCollection edges = (ksEdgeCollection)loop.EdgeCollection();
            if (edges.GetCount() != 1) continue;
            ksEdgeDefinition rim = (ksEdgeDefinition)edges.GetByIndex(0);
            if (!rim.IsCircle()) continue;
            ksFaceDefinition wall = (ksFaceDefinition)rim.GetAdjacentFace(true);
            if (wall == null || !wall.IsCylinder()) wall = (ksFaceDefinition)rim.GetAdjacentFace(false);
            if (wall == null || !wall.IsCylinder()) continue;
            int wallIndex = faces.FindIt(wall);
            if (wallIndex < 0) throw new ToolException("Цилиндрическая грань не найдена в теле");
            ksCurve3D curve = (ksCurve3D)rim.GetCurve3D();
            ksCircle3dParam circle = (ksCircle3dParam)curve.GetCurveParam();
            double x, y, z;
            if (!((ksPlacement)circle.GetPlacement()).GetOrigin(out x, out y, out z))
              throw new ToolException("Не удалось измерить центр контура");
            List<object> rims;
            if (!openings.TryGetValue(wallIndex, out rims)) openings[wallIndex] = rims = new List<object>();
            rims.Add(new Dictionary<string, object> { { "center_mm", new[] { x, y, z } },
              { "diameter_mm", 2 * circle.radius }, { "plane_index", f } });
          }
        }
        foreach (var pair in openings)
          walls.Add(new Dictionary<string, object> { { "body_index", b }, { "wall_index", pair.Key }, { "rims", pair.Value } });
      }
      int major, minor, release, build;
      if (!KompasHost.App7.GetSystemVersion(out major, out minor, out release, out build))
        throw new ToolException("Не удалось получить версию КОМПАС-3D");
      return new Dictionary<string, object> { { "path", Tools3D.PartPath },
        { "cad_version", new[] { major, minor, release, build } }, { "body_count", bodies.GetCount() }, { "walls", walls } };
    }
  }
}
