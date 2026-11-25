using System;
using System.Collections.Generic;
using Kompas6API5;
using KompasAPI7;
using Kompas6Constants;
using Kompas6Constants3D;
using Row = System.Collections.Generic.Dictionary<string, object>;

namespace KompasMcp.Tools
{
  internal static class AssemblyMates
  {
    static readonly Dictionary<string, ksObj3dTypeEnum> Datums = new Dictionary<string, ksObj3dTypeEnum> {
      { "XOY", ksObj3dTypeEnum.o3d_planeXOY }, { "XOZ", ksObj3dTypeEnum.o3d_planeXOZ },
      { "YOZ", ksObj3dTypeEnum.o3d_planeYOZ }, { "OX", ksObj3dTypeEnum.o3d_axisOX },
      { "OY", ksObj3dTypeEnum.o3d_axisOY }, { "OZ", ksObj3dTypeEnum.o3d_axisOZ }
    };
    static readonly Dictionary<string, MateConstraintType> Kinds = new Dictionary<string, MateConstraintType> {
      { "coincident", MateConstraintType.mc_Coincidence }, { "concentric", MateConstraintType.mc_Concentric },
      { "parallel", MateConstraintType.mc_Parallel }, { "perpendicular", MateConstraintType.mc_Perpendicular },
      { "distance", MateConstraintType.mc_Distance }
    };
    const string DatumSchema = @"{""type"":""object"",""properties"":{""occurrence"":{""type"":""string""},""datum"":{""type"":""string""}},""required"":[""occurrence"",""datum""]}";

    internal static void Register()
    {
      ToolRegistry.Add("assembly_inspect", "Дерево вхождений сборки, файлы, координаты и оси в общей системе, базы и сопряжения. occurrence — идентификатор КОМПАС в текущей сборке; root — корень.", "{}", a => Inspect());
      ToolRegistry.Add("assembly_add_mate", "Создать сопряжение корневой сборки между именованными базами вхождений. Возвращает идентификатор, имя КОМПАС и состояние. distance задаётся в мм.",
        @"{""type"":""object"",""properties"":{""kind"":{""type"":""string"",""enum"":[""coincident"",""concentric"",""parallel"",""perpendicular"",""distance""]},""first"":" + DatumSchema + @",""second"":" + DatumSchema +
        @",""distance_mm"":{""type"":""number""},""alignment"":{""type"":""string"",""enum"":[""closest"",""same"",""opposite""]}},""required"":[""kind"",""first"",""second""]}", AddMate);
      ToolRegistry.Add("assembly_set_fixed", "Закрепить или освободить конкретное вхождение в корневой сборке.",
        @"{""type"":""object"",""properties"":{""occurrence"":{""type"":""string""},""fixed"":{""type"":""boolean""}},""required"":[""occurrence"",""fixed""]}", SetFixed);
      ToolRegistry.Add("assembly_rotate", "Повернуть вхождение вокруг выбранной оси средствами перемещения компонентов с учётом сопряжений. delta_deg — приращение угла. Возвращает фактическую позу.",
        @"{""type"":""object"",""properties"":{""occurrence"":{""type"":""string""},""axis"":" + DatumSchema + @",""delta_deg"":{""type"":""number""}},""required"":[""occurrence"",""axis"",""delta_deg""]}", Rotate);
      ToolRegistry.Add("assembly_clearance", "Измерить минимальный зазор двух вхождений деталей по телам в сборке, различить зазор, касание и объёмное пересечение.",
        @"{""type"":""object"",""properties"":{""first"":{""type"":""string""},""second"":{""type"":""string""}},""required"":[""first"",""second""]}", Clearance);
    }

    static IKompasDocument3D Document { get { return Tools3D.GetDoc7(); } }
    static Part7 Root
    {
      get {
        if (Document.DocumentType != DocumentTypeEnum.ksDocumentAssembly) throw new ToolException("Откройте сборку .a3d");
        return Document.TopPart;
      }
    }
    static string NativeId(IKompasAPIObject obj)
    {
      string id = ((IKompasDocument1)Document).GetObjectId(obj, null);
      if (String.IsNullOrEmpty(id)) return null;
      return id.TrimStart().StartsWith("{") ? JsonLib.Json.Write(JsonLib.Json.Parse(id)) : id;
    }
    static string Id(IKompasAPIObject obj)
    {
      var id = NativeId(obj);
      if (id == null) throw new ToolException("КОМПАС не выдал идентификатор объекта: " + obj.Type);
      return id;
    }
    internal static Part7 Resolve(string id)
    {
      Part7 root = Root;
      if (id == "root") return root;
      Part7 part = ((IKompasDocument1)Document).FindObjectById(id, null) as Part7;
      if (part == null || Id(part) != id) throw new ToolException("Не найдено вхождение: " + id);
      return part;
    }
    internal static ksPart Api5(Part7 part)
    {
      return (ksPart)KompasHost.Kompas.TransferInterface(part, (int)ksAPITypeEnum.ksAPI5Auto, (int)ksObj3dTypeEnum.o3d_part);
    }
    internal static string OccurrenceOf(ksPart part)
    {
      var inserted = (Part7)KompasHost.Kompas.TransferInterface(part, (int)ksAPITypeEnum.ksAPI7Dual, 0);
      return Id(inserted);
    }
    static string OwnerId(Part7 part) { return part == null || part.Reference == Root.Reference ? "root" : Id(part); }

    static double[] Point(Part7 part, double x, double y, double z)
    {
      if (!Root.TransformPoint(ref x, ref y, ref z, part)) throw new ToolException("Не получена система координат вхождения");
      return new[] { x, y, z };
    }
    static double[] Direction(double[] point, double[] origin)
    { return new[] { point[0]-origin[0], point[1]-origin[1], point[2]-origin[2] }; }
    internal static Row Pose(Part7 part)
    {
      var origin = Point(part, 0, 0, 0);
      return new Row { { "origin_mm", origin }, { "x_axis", Direction(Point(part, 1, 0, 0), origin) },
        { "y_axis", Direction(Point(part, 0, 1, 0), origin) }, { "z_axis", Direction(Point(part, 0, 0, 1), origin) } };
    }
    static IEnumerable<IModelObject> CustomDatums(Part7 part)
    {
      var geom = (IAuxiliaryGeomContainer)part;
      foreach (IModelObject p in geom.Planes3D) yield return p;
      foreach (IModelObject a in geom.Axes3D) yield return a;
    }
    static IModelObject Datum(Row args, string field)
    {
      object input;
      Row sel = args.TryGetValue(field, out input) ? input as Row : null;
      if (sel == null) throw new ToolException("Ожидается база: " + field);
      Part7 part = Resolve(ToolRegistry.GetStr(sel, "occurrence"));
      string name = ToolRegistry.GetStr(sel, "datum");
      ksObj3dTypeEnum type;
      if (Datums.TryGetValue(name, out type)) {
        var standard = part.get_DefaultObject(type);
        if (standard == null) throw new ToolException("База недоступна: " + name);
        return standard;
      }
      IModelObject result = null;
      foreach (var item in CustomDatums(part))
        if (item.Name == name)
        {
          if (result != null) throw new ToolException("Неоднозначное имя базы: " + name);
          result = item;
        }
      if (result == null) throw new ToolException("База не найдена: " + name);
      return result;
    }
    static Row Endpoint(IModelObject obj)
    {
      if (obj == null) return null;
      string name = obj.Name;
      foreach (var d in Datums) if (d.Value == obj.ModelObjectType) name = d.Key;
      return new Row { { "occurrence", OwnerId(obj.Part) }, { "datum", name }, { "object_type", obj.ModelObjectType.ToString() } };
    }
    static bool IsAxis(IModelObject obj)
    {
      return obj.ModelObjectType == ksObj3dTypeEnum.o3d_axisOX || obj.ModelObjectType == ksObj3dTypeEnum.o3d_axisOY ||
             obj.ModelObjectType == ksObj3dTypeEnum.o3d_axisOZ || obj is IAxis3D;
    }
    static Row MateInfo(IMateConstraint3D m)
    {
      string kind = m.ConstraintType.ToString();
      foreach (var k in Kinds) if (k.Value == m.ConstraintType) kind = k.Key;
      var first = m.BaseObject1;
      var second = m.BaseObject2;
      var id = NativeId(m);
      bool unresolved = id == null && first == null && second == null;
      var row = new Row { { "id", id }, { "name", m.Name }, { "kind", kind },
        { "valid", unresolved ? (object)null : m.Valid }, { "state", unresolved ? "unresolved" : m.Valid ? "valid" : "invalid" },
        { "first", Endpoint(first) }, { "second", Endpoint(second) }, { "alignment", m.Alignment.ToString() } };
      var feature = m as IFeature7;
      if (feature != null) {
        row["excluded"] = feature.Excluded;
        row["object_error"] = feature.ObjectError;
        if (feature.Excluded) row["state"] = "excluded";
      }
      if (m.ConstraintType == MateConstraintType.mc_Distance) row["distance_mm"] = m.ParamValue;
      return row;
    }
    static void Visit(Part7 parent, List<string> path, List<Row> parts, List<Row> mates)
    {
      if (parent.Detail) return;
      IMateConstraints3D constraints = parent.MateConstraints;
      for (int i=0; i<constraints.Count; i++) {
        var row = MateInfo(constraints.get_MateConstraint3D(i));
        row["owner"] = OwnerId(parent);
        mates.Add(row);
      }
      foreach (Part7 p in parent.Parts)
      {
        string id = Id(p);
        var childPath = new List<string>(path) { id };
        var bases = new List<string>(Datums.Keys);
        foreach (var datum in CustomDatums(p)) if (!bases.Contains(datum.Name)) bases.Add(datum.Name);
        parts.Add(new Row { { "occurrence", id }, { "tree_path", childPath }, { "name", p.Name },
          { "tree_label", ((IFeature7)p).Label },
          { "source_file", p.FileName }, { "fixed", p.Fixed }, { "pose", Pose(p) }, { "datums", bases } });
        Visit(p, childPath, parts, mates);
      }
    }
    static object Inspect()
    {
      var parts = new List<Row>();
      var mates = new List<Row>();
      Visit(Root, new List<string>(), parts, mates);
      return new Row { { "assembly", Tools3D.PartPath }, { "occurrences", parts }, { "mates", mates } };
    }
    internal static void Rebuild()
    {
      if (!Document.RebuildDocument()) throw new ToolException("КОМПАС не перестроил сборку");
    }
    static object AddMate(Row a)
    {
      string kindName = ToolRegistry.GetStr(a, "kind");
      MateConstraintType kind;
      if (!Kinds.TryGetValue(kindName, out kind)) throw new ToolException("Неподдерживаемый тип сопряжения: " + kindName);
      IMateConstraints3D mates = Root.MateConstraints;
      var first = Datum(a, "first");
      var second = Datum(a, "second");
      if (OwnerId(first.Part) == OwnerId(second.Part)) throw new ToolException("Выберите базы разных вхождений");
      if (kind == MateConstraintType.mc_Concentric && (!IsAxis(first) || !IsAxis(second)))
        throw new ToolException("Для соосности выберите две базовые оси");
      string key1 = Id(first), key2 = Id(second);
      var validBefore = new List<IMateConstraint3D>();
      for (int i=0; i<mates.Count; i++) {
        var old = mates.get_MateConstraint3D(i);
        if (old.Valid) validBefore.Add(old);
        if (old.ConstraintType != kind || old.BaseObject1 == null || old.BaseObject2 == null) continue;
        string u = Id(old.BaseObject1), v = Id(old.BaseObject2);
        if ((u == key1 && v == key2) || (u == key2 && v == key1))
          throw new ToolException("Сопряжение уже существует: " + old.Name);
      }
      double dist = kind == MateConstraintType.mc_Distance ? ToolRegistry.GetDbl(a, "distance_mm") : 0;
      if (Double.IsNaN(dist) || Double.IsInfinity(dist) || (kind != MateConstraintType.mc_Distance && a.ContainsKey("distance_mm")))
        throw new ToolException("distance_mm применим только к сопряжению distance и должен быть конечным");
      string alignment = ToolRegistry.GetStr(a, "alignment", "closest");
      if (alignment != "same" && alignment != "opposite" && alignment != "closest") throw new ToolException("Неизвестная ориентация");
      int previousCount = mates.Count;
      IMateConstraint3D m = mates.Add(kind);
      if (m == null) throw new ToolException("КОМПАС не создал сопряжение");
      try {
        m.BaseObject1 = first; m.BaseObject2 = second;
        m.Alignment = alignment == "closest" ? ksMateConstraintAlignmentEnum.ksMCAlignmentClosest :
          alignment == "same" ? ksMateConstraintAlignmentEnum.ksMCAlignmentCooriented : ksMateConstraintAlignmentEnum.ksMCAlignmentOpposite;
        if (kind == MateConstraintType.mc_Distance) m.ParamValue = dist;
        if (!m.Update()) throw new ToolException("Не удалось применить сопряжение: " + kindName);
        Rebuild();
        if (!m.Valid) throw new ToolException("Недействительное сопряжение: " + kindName);
        foreach (var old in validBefore)
          if (!old.Valid) throw new ToolException("Нарушено ранее действующее сопряжение: " + old.Name);
        return MateInfo(m);
      }
      catch {
        if (mates.Count > previousCount) {
          var feature = m as IFeature7;
          if (feature == null || !feature.Delete() || !Document.RebuildDocument() || mates.Count != previousCount)
            throw new ToolException("Не удалось убрать ошибочное сопряжение. Закройте копию без сохранения.");
        }
        throw;
      }
    }
    static object SetFixed(Row a)
    {
      string id = ToolRegistry.GetStr(a, "occurrence");
      if (id == "root" || !a.ContainsKey("fixed") || !(a["fixed"] is bool)) throw new ToolException("Нужно вхождение и fixed");
      var p = Resolve(id);
      bool requested = (bool)a["fixed"];
      if (p.Fixed != requested) {
        p.Fixed = requested;
        // v22 may return false for an externally linked occurrence even when Fixed was applied.
        p.Update();
        Rebuild();
      }
      if (Resolve(id).Fixed != requested) throw new ToolException("Фиксация не подтверждена: " + id);
      return new Row { { "occurrence", id }, { "fixed", requested } };
    }
    static object Rotate(Row a)
    {
      string id = ToolRegistry.GetStr(a, "occurrence");
      var part = Resolve(id);
      if (id == "root" || part.Fixed) throw new ToolException("Вхождение закреплено");
      var axis = Datum(a, "axis");
      double angle = ToolRegistry.GetDbl(a, "delta_deg");
      if (Double.IsNaN(angle) || Double.IsInfinity(angle)) throw new ToolException("Нужен конечный угол");
      var pos = ((IKompasDocument3D1)Document).ComponentPositioner;
      if (!pos.InitAxis(axis)) throw new ToolException("Не удалось задать ось вращения");
      // Prepare returns a status code (0 means success); RotateComponent and Finish return BOOL.
      if (pos.Prepare(part, Positioner_Type.pnRotate) != 0) throw new ToolException("Не удалось начать вращение");
      try {
        if (!pos.RotateComponent(angle * Math.PI / 180.0)) throw new ToolException("Решатель не выполнил поворот");
      }
      finally { if (!pos.Finish()) throw new ToolException("Не удалось завершить перемещение"); }
      Rebuild();
      return new Row { { "occurrence", id }, { "pose", Pose(part) } };
    }

    static object Clearance(Row a)
    {
      string first = ToolRegistry.GetStr(a, "first"), second = ToolRegistry.GetStr(a, "second");
      var p = Resolve(first); var q = Resolve(second);
      if (first == second || !p.Detail || !q.Detail) throw new ToolException("Нужны два различных вхождения деталей");
      var left = (ksBodyCollection)Api5(p).BodyCollection();
      var right = (ksBodyCollection)Api5(q).BodyCollection();
      var measure = (ksMeasurer)Tools3D.GetPart().GetMeasurer();
      double min = Double.PositiveInfinity;
      bool collision = false, touch = false;
      for (int i=0; i<left.GetCount(); i++) for (int j=0; j<right.GetCount(); j++)
      {
        var x = (ksBody)left.GetByIndex(i); var y = (ksBody)right.GetByIndex(j);
        var overlap = (ksIntersectionResult)x.CheckIntersectionWithBody(y, true);
        if (overlap != null) for (int k=0; k<overlap.GetCount(); k++)
        {
          var type = (Intersection_Type)overlap.GetIntersectionType(k);
          if (type == Intersection_Type.itBody) collision = true;
          else if (type == Intersection_Type.itTangentPoint || type == Intersection_Type.itTangentCurve ||
                   type == Intersection_Type.itTangentSurface) touch = true;
          else throw new ToolException("Неизвестный результат проверки пересечения: " + type);
        }
        if (!collision && !touch) min = Math.Min(min, FaceDistance(x, y, measure));
      }
      if (collision || touch) min = 0;
      if (Double.IsInfinity(min) || Double.IsNaN(min) || min < 0) throw new ToolException("Нет измеряемой пары тел");
      return new Row { { "first", first }, { "second", second }, { "distance_mm", min },
        { "state", collision ? "interference" : touch ? "contact" : "clear" } };
    }
    static double FaceDistance(ksBody a, ksBody b, ksMeasurer measure)
    {
      var xs = (ksFaceCollection)a.FaceCollection(); var ys = (ksFaceCollection)b.FaceCollection();
      double min = Double.PositiveInfinity;
      measure.unit = (uint)ksLengthUnitsEnum.ksLUnMM;
      for (int i=0; i<xs.GetCount(); i++)
      {
        if (!measure.SetObject1(((ksFaceDefinition)xs.GetByIndex(i)).GetEntity())) throw new ToolException("Не измерена первая грань");
        for (int j=0; j<ys.GetCount(); j++)
        {
          if (!measure.SetObject2(((ksFaceDefinition)ys.GetByIndex(j)).GetEntity()) || !measure.Calc())
            throw new ToolException("Не измерено расстояние граней");
          min = Math.Min(min, measure.MinDistance);
        }
      }
      return min;
    }
  }
}
