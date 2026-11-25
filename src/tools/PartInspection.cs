using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Kompas6API5;
using KompasAPI7;
using Row = System.Collections.Generic.Dictionary<string, object>;

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
        a => RoundOpenings(false));
      const string point = @"{""type"":""array"",""items"":{""type"":""number""},""minItems"":3,""maxItems"":3}";
      ToolRegistry.Add("check_round_part", "Проверить одно тело, габариты XYZ и набор цилиндрических стенок с двумя круглыми торцами. Для каждой стенки задайте имя, диаметр, центры торцов и типы контуров в том же порядке. Размеры в мм. При расхождении возвращает ошибку с измерениями.",
        @"{""type"":""object"",""properties"":{""size_mm"":" + point + @",""tolerance_mm"":{""type"":""number"",""exclusiveMinimum"":0},""walls"":{""type"":""array"",""minItems"":1,""items"":{""type"":""object"",""properties"":{
          ""name"":{""type"":""string""},""diameter_mm"":{""type"":""number""},""from_mm"":" + point + @",""to_mm"":" + point + @",
          ""rims"":{""type"":""string"",""enum"":[""inner-inner"",""outer-outer"",""inner-outer"",""outer-inner""]}},""required"":[""name"",""diameter_mm"",""from_mm"",""to_mm"",""rims""]}}},""required"":[""size_mm"",""tolerance_mm"",""walls""]}", CheckRoundPart);
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

    static Row RoundOpenings(bool includeOuter)
    {
      ksBodyCollection bodies = (ksBodyCollection)Tools3D.GetPart().BodyCollection();
      var walls = new List<Row>();
      for (int b = 0; b < bodies.GetCount(); b++)
      {
        ksBody body = (ksBody)bodies.GetByIndex(b);
        ksFaceCollection faces = (ksFaceCollection)body.FaceCollection();
        var openings = new Dictionary<int, List<Row>>();
        for (int f = 0; f < faces.GetCount(); f++)
        {
          ksFaceDefinition plane = (ksFaceDefinition)faces.GetByIndex(f);
          if (!plane.IsPlanar()) continue;
          ksLoopCollection loops = (ksLoopCollection)plane.LoopCollection();
          for (int l = 0; l < loops.GetCount(); l++)
          {
            ksLoop loop = (ksLoop)loops.GetByIndex(l);
            bool outer = loop.IsOuter();
            if (outer && !includeOuter) continue;
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
            List<Row> rims;
            if (!openings.TryGetValue(wallIndex, out rims)) openings[wallIndex] = rims = new List<Row>();
            var measured = new Row { { "center_mm", new[] { x, y, z } },
              { "diameter_mm", 2 * circle.radius }, { "plane_index", f } };
            if (includeOuter) measured["kind"] = outer ? "outer" : "inner";
            rims.Add(measured);
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

    static double[] Triplet(Row args, string key)
    {
      object value;
      var list = args.TryGetValue(key, out value) ? value as List<object> : null;
      if (list == null || list.Count != 3 || list.Any(v => !(v is double || v is long || v is int)))
        throw new ToolException(key + ": ожидаются три числа");
      var numbers = list.Select(v => Convert.ToDouble(v, CultureInfo.InvariantCulture)).ToArray();
      if (numbers.Any(n => double.IsNaN(n) || double.IsInfinity(n))) throw new ToolException(key + ": нужны конечные значения");
      return numbers;
    }

    static ToolException Difference(string check, object expected, object measured)
    {
      return new ToolException(check + ": expected " + JsonLib.Json.Write(expected) + ", measured " + JsonLib.Json.Write(measured));
    }

    static bool MatchesRim(Row rim, string kind, double[] center, double tolerance)
    {
      var actual = (double[])rim["center_mm"];
      return (string)rim["kind"] == kind &&
        Math.Sqrt(actual.Zip(center, (a,b) => (a-b)*(a-b)).Sum()) <= tolerance;
    }

    static object CheckRoundPart(Row args)
    {
      var size = Triplet(args, "size_mm");
      double tol = ToolRegistry.GetDbl(args, "tolerance_mm");
      if (!(tol > 0) || double.IsInfinity(tol) || size.Any(n => n <= 0)) throw new ToolException("Нужны положительные габариты и конечный допуск");
      object value;
      var expected = args.TryGetValue("walls", out value) ? value as List<object> : null;
      if (expected == null || expected.Count == 0) throw new ToolException("walls: задайте хотя бы одну стенку");

      var measured = RoundOpenings(true);
      if ((int)measured["body_count"] != 1) throw Difference("body_count", 1, measured["body_count"]);
      double x, y, z, x1, y1, z1;
      if (!Tools3D.GetPart().GetGabarit(false, false, out x, out y, out z, out x1, out y1, out z1))
        throw new ToolException("Не удалось измерить габариты детали");
      var actualSize = new[] { x1-x, y1-y, z1-z };
      if (!actualSize.Zip(size, (a,b) => Math.Abs(a-b) <= tol).All(ok => ok))
        throw Difference("size_mm", size, actualSize);

      var remaining = new List<Row>((List<Row>)measured["walls"]);
      foreach (object spec in expected)
      {
        var wall = spec as Row;
        if (wall == null) throw new ToolException("walls: ожидается объект стенки");
        string name = ToolRegistry.GetStr(wall, "name");
        double diameter = ToolRegistry.GetDbl(wall, "diameter_mm");
        var first = Triplet(wall, "from_mm");
        var second = Triplet(wall, "to_mm");
        var kinds = ToolRegistry.GetStr(wall, "rims").Split('-');
        if (kinds.Length != 2 || kinds.Any(k => k != "inner" && k != "outer") || !(diameter > 0) || double.IsInfinity(diameter))
          throw new ToolException(name + ": проверьте diameter_mm и типы контуров rims");
        var located = remaining.Where(candidate => {
          var rims = (List<Row>)candidate["rims"];
          if (rims.Count != 2 || Equals(rims[0]["plane_index"], rims[1]["plane_index"])) return false;
          return Enumerable.Range(0, 2).Any(i => MatchesRim(rims[i], kinds[0], first, tol) &&
            MatchesRim(rims[1-i], kinds[1], second, tol));
        }).ToList();
        if (located.Count == 0) {
          var candidates = remaining.Where(w => ((List<Row>)w["rims"]).Any(r => kinds.Contains((string)r["kind"])));
          throw Difference(name, wall, candidates.ToList());
        }
        var matches = located.Where(w => ((List<Row>)w["rims"]).All(r => Math.Abs((double)r["diameter_mm"] - diameter) <= tol)).ToList();
        if (matches.Count != 1)
          throw Difference(name + ".diameter_mm", diameter, located.SelectMany(w => (List<Row>)w["rims"]).Select(r => r["diameter_mm"]).ToList());
        matches[0]["name"] = name;
        remaining.Remove(matches[0]);
      }
      if (remaining.Count != 0) throw Difference("unexpected_walls", new object[0], remaining);
      measured["size_mm"] = actualSize;
      measured["tolerance_mm"] = tol;
      return measured;
    }
  }
}
