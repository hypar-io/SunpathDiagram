using Elements;
using Elements.Geometry;
using Newtonsoft.Json;
using SunCalcNet;
using System;
using System.Collections.Generic;
using System.Linq;
using GeoTimeZone;
using TimeZoneConverter;

namespace SunpathDiagram
{
    public static class SunpathDiagram
    {
        /// <summary>
        /// The SunpathDiagram function.
        /// </summary>
        /// <param name="model">The input model.</param>
        /// <param name="input">The arguments to the execution.</param>
        /// <returns>A SunpathDiagramOutputs instance containing computed results and the model with any new elements.</returns>
        public static SunpathDiagramOutputs Execute(Dictionary<string, Model> inputModels, SunpathDiagramInputs input)
        {
            var d = new DateTime(input.Date.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc); //input.Date.LocalDateTime;
            Console.WriteLine(d);
            var origin = inputModels["location"].AllElementsOfType<Origin>().FirstOrDefault();

            var timeZone = TimeZoneLookup.GetTimeZone(origin.Position.Latitude, origin.Position.Longitude).Result;
            var tzInfo = TZConvert.GetTimeZoneInfo(timeZone);
            var offset = tzInfo.BaseUtcOffset;
            var output = new SunpathDiagramOutputs(timeZone);

            var circle = new Circle((0, 0), input.DisplayRadius);
            output.Model.AddElement(circle);
            var LgModelTexts = new List<(Vector3 location, Vector3 facingDirection, Vector3 lineDirection, string text, Color? color)>();
            var SmModelTexts = new List<(Vector3 location, Vector3 facingDirection, Vector3 lineDirection, string text, Color? color)>();

            var hrPts = new Dictionary<int, List<Vector3>>();

            var pts = new Dictionary<string, Vector3> {
                {"N", (0, input.DisplayRadius)},
                {"E", (input.DisplayRadius, 0)},
                {"S", (0, -input.DisplayRadius)},
                {"W", (-input.DisplayRadius, 0)},
            };

            var specialDates = new Dictionary<DateTime, List<Vector3>>()
            {
                {new DateTime(input.Date.Year, 1, 21), new List<Vector3>()},
                {new DateTime(input.Date.Year, 2, 21), new List<Vector3>()},
                {new DateTime(input.Date.Year, 3, 21), new List<Vector3>()},
                {new DateTime(input.Date.Year, 4, 21), new List<Vector3>()},
                {new DateTime(input.Date.Year, 5, 21), new List<Vector3>()},
                {new DateTime(input.Date.Year, 6, 21), new List<Vector3>()},
                {new DateTime(input.Date.Year, 7, 21), new List<Vector3>()},
                {new DateTime(input.Date.Year, 8, 21), new List<Vector3>()},
                {new DateTime(input.Date.Year, 9, 21), new List<Vector3>()},
                {new DateTime(input.Date.Year, 10, 21), new List<Vector3>()},
                {new DateTime(input.Date.Year, 11, 21), new List<Vector3>()},
                {new DateTime(input.Date.Year, 12, 21), new List<Vector3>()}
            };
            foreach (var dir in pts)
            {
                var n = new Line((0, 0), dir.Value);
                output.Model.AddElement(n);
                LgModelTexts.Add((dir.Value * 1.1, Vector3.ZAxis, Vector3.XAxis, dir.Key, Colors.Black));
            }

            var datePts = new List<Vector3>();
            var allPts = new List<Vector3>();

            var phases = SunCalc.GetSunPhases(d, origin.Position.Latitude, origin.Position.Longitude);
            var rise = TimeZoneInfo.ConvertTime(phases.First(p => p.Name.Value == SunCalcNet.Model.SunPhaseName.Sunrise.Value).PhaseTime, tzInfo);
            var set = TimeZoneInfo.ConvertTime(phases.First(p => p.Name.Value == SunCalcNet.Model.SunPhaseName.Sunset.Value).PhaseTime, tzInfo);

            while (d.Year == input.Date.Year)
            {
                var sp = SunCalc.GetSunPosition(d, origin.Position.Latitude, origin.Position.Longitude);
                Vector3 pt = (0, input.DisplayRadius);
                var azimuthTransform = new Transform((0, 0), Units.RadiansToDegrees(sp.Azimuth) + 180);
                var altitudeTransform = new Transform();
                altitudeTransform.Rotate((1, 0), Units.RadiansToDegrees(sp.Altitude)); //(0, 0), (1, 0), Units.RadiansToDegrees(sp.Altitude));
                altitudeTransform.Concatenate(azimuthTransform);
                pt = altitudeTransform.OfPoint(pt);
                var dateInLocalTime = TimeZoneInfo.ConvertTime(d, tzInfo);

                if (d.Hour == 0)
                {
                    phases = SunCalc.GetSunPhases(d, origin.Position.Latitude, origin.Position.Longitude);
                    rise = TimeZoneInfo.ConvertTime(phases.First(p => p.Name.Value == SunCalcNet.Model.SunPhaseName.Sunrise.Value).PhaseTime, tzInfo);
                    set = TimeZoneInfo.ConvertTime(phases.First(p => p.Name.Value == SunCalcNet.Model.SunPhaseName.Sunset.Value).PhaseTime, tzInfo);
                }
                var shouldAdd = dateInLocalTime.TimeOfDay > rise.TimeOfDay && dateInLocalTime.TimeOfDay < set.TimeOfDay;

                if (d.Minute == 0)
                {
                    if (!hrPts.ContainsKey(d.Hour))
                    {
                        hrPts[d.Hour] = new List<Vector3>();
                    }
                    if (shouldAdd)
                    {
                        hrPts[d.Hour].Add(pt);
                    }
                }
                if (shouldAdd)
                {
                    allPts.Add(pt);
                }

                var dateCheck = new DateTime(dateInLocalTime.Year, dateInLocalTime.Month, dateInLocalTime.Day);
                if (specialDates.TryGetValue(dateCheck, out var specialDatePts))
                {
                    if (shouldAdd)
                    {
                        datePts.Add(pt);
                    }
                }
                else if (datePts.Count > 0)
                {
                    if (datePts.Count() > 1)
                    {
                        var arc = new Polyline(datePts);
                        output.Model.AddElements(new ModelCurve(arc, BuiltInMaterials.Concrete));
                        var insetAmt = 14;
                        var labelPt = dateCheck.Month > 6 ? datePts.Skip(insetAmt).First() : datePts.SkipLast(insetAmt).Last();
                        var dirPt = dateCheck.Month > 6 ? datePts.Skip(insetAmt - 1).First() : datePts.SkipLast(insetAmt + 1).Last();
                        SmModelTexts.Add((labelPt * 0.98, Vector3.ZAxis, labelPt - dirPt, $"{dateCheck:MMMM}", Colors.Black));
                    }
                    datePts.Clear();
                }

                d = d.AddMinutes(10);
            }

            foreach (var hr in hrPts)
            {
                try
                {
                    var pgon = new Polyline(hr.Value.Union(new[] { hr.Value.First() }).ToList());
                    if (pgon.Vertices.Count > 1)
                    {
                        var mc = new ModelCurve(pgon);
                        output.Model.AddElement(mc);
                    }
                    var highestPt = hr.Value.OrderBy(p => p.Y).Last();
                    highestPt = (highestPt.X * 0.93, highestPt.Y * 0.93, highestPt.Z);
                    var convertedTime = TimeZoneInfo.ConvertTime(new DateTime(2021, 1, 1, hr.Key, 0, 0, DateTimeKind.Utc), tzInfo);
                    SmModelTexts.Add((highestPt, Vector3.ZAxis, Vector3.XAxis, $"{convertedTime:h:mm tt}", Colors.Black));
                }
                catch
                {

                }
            }
            var mt = new ModelText(LgModelTexts, Elements.FontSize.PT72, 1000);
            var mt2 = new ModelText(SmModelTexts, Elements.FontSize.PT36, 800);
            output.Model.AddElements(mt, mt2);

            // render the selected date 
            var inputDate = input.Date;
            var date = new DateTime(inputDate.Year, inputDate.Month, inputDate.Day, inputDate.Hour, inputDate.Minute, inputDate.Second, DateTimeKind.Utc);
            date -= tzInfo.BaseUtcOffset;

            {
                var sp = SunCalc.GetSunPosition(date, origin.Position.Latitude, origin.Position.Longitude);

                Vector3 pt = (0, input.DisplayRadius);
                var azimuthTransform = new Transform((0, 0), Units.RadiansToDegrees(sp.Azimuth) + 180);
                var altitudeTransform = new Transform();
                altitudeTransform.Rotate((1, 0), Units.RadiansToDegrees(sp.Altitude)); //(0, 0), (1, 0), Units.RadiansToDegrees(sp.Altitude));
                altitudeTransform.Concatenate(azimuthTransform);
                pt = altitudeTransform.OfPoint(pt);
                var sunCircle = new Circle((0, 0), 10);
                var transform = new Transform(pt, Vector3.XAxis, pt.Unitized(), 0);
                var panel = new Panel(sunCircle.ToPolygon(20), new Material("Sun", new Color(1.0, 1.0, 0, 0.69), 0, 0, null, true), transform);
                var directionalLight = new DirectionalLight(Colors.White, new Transform((0, 0), pt), 1);
                var arrow = new ModelArrows(new List<(Vector3 location, Vector3 direction, double magnitude, Color? color)> { (pt, (pt * -1.0).Unitized(), input.DisplayRadius * 0.5, Colors.Yellow) }, false, true);
                output.Model.AddElements(panel, directionalLight, arrow);
            }

            output.Model.Transform = new Transform(0, 0, 4);
            return output;
        }
    }
}