using ExcelDataReader;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TimetableApp.Core;
using TimetableApp.Core.Zoom;

namespace CaranoParse
{
    public static class Program
    {
        const string updateUrlTemplate = "{0}timetable/getLocation?id={1}&password={2}";
        const string pushUrlTemplate = "{0}timetable/update?id={1}&updatePassword={2}";

        private static void CheckNullOrEmpty(string name, string arg)
        {
            if (string.IsNullOrEmpty(arg))
            {
                throw new ArgumentNullException(name);
            }
        }

        public static async Task PushToServer(
            string baseApi, 
            string name,
            string displayName, 
            string id, 
            string password,
            string updatePassword,
            string teachersFile,
            string timespanFile,
            string timetableFile)
        {
            CheckNullOrEmpty(nameof(name), name);
            CheckNullOrEmpty(nameof(id), id);
            CheckNullOrEmpty(nameof(password), password);
            CheckNullOrEmpty(nameof(updatePassword), updatePassword);
            CheckNullOrEmpty(nameof(teachersFile), teachersFile);
            CheckNullOrEmpty(nameof(timespanFile), timespanFile);
            CheckNullOrEmpty(nameof(timetableFile), timetableFile);

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var jsonString = new StringWriter();

            Console.OutputEncoding = Encoding.Unicode;

            var teachers = ParseTeachers(teachersFile);

            #region Morning
            var morningTimeSpan = ParseTime(timespanFile, new[] { "sang", "sáng" });
            foreach (var (begin, end) in morningTimeSpan)
            {
                Console.WriteLine($"{begin}-{end}");
            }
            var morningCodes = ParseTimetable(timetableFile, name, "sang", "sáng");

            var morningLessons = morningCodes.Select((codeOfDay) =>
            {
                return codeOfDay.Select((kvp) =>
                {
                    var l = teachers[kvp.Value].Clone();
                    (l.StartTime, l.EndTime) = morningTimeSpan[kvp.Key];
                    return l;
                }).ToList();
            }).ToArray();
            #endregion

            #region Afternoon
            var afternoonTimeSpan = ParseTime(timespanFile, new[] { "chieu", "chiều" });
            foreach (var (begin, end) in morningTimeSpan)
            {
                Console.WriteLine($"{begin}-{end}");
            }
            var afternoonCodes = ParseTimetable(timetableFile, name, "chieu", "chiều");

            var afternoonLessons = afternoonCodes.Select((codeOfDay) =>
            {
                return codeOfDay.Select((kvp) =>
                {
                    var l = teachers[kvp.Value].Clone();
                    (l.StartTime, l.EndTime) = afternoonTimeSpan[kvp.Key];
                    return l;
                }).ToList();
            }).ToArray();
            #endregion

            var settings = new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.Auto };

            var lessons = Enumerable.Zip(morningLessons, afternoonLessons, (m, a) => m.Concat(a).ToList()).ToArray();

            var timetable = new Timetable()
            {
                Name = displayName,
                UpdateURL = string.Format(updateUrlTemplate, baseApi, id, password),
                Lessons = lessons
            };

            Console.WriteLine(JsonConvert.SerializeObject(timetable, settings)
                .Replace(Assembly.GetExecutingAssembly().GetName().Name, "$ASSEMBLY_NAME"));

            var jsonData = JsonConvert.SerializeObject(timetable, settings)
                .Replace(Assembly.GetExecutingAssembly().GetName().Name, "$ASSEMBLY_NAME");

            Console.WriteLine("Pushing to AzureAms server...");

            var push = string.Format(pushUrlTemplate, baseApi, id, updatePassword);

            var request = new HttpRequestMessage(HttpMethod.Put, push);
            request.Content = new StringContent(jsonData, Encoding.UTF8, "application/json");

            using (var client = new HttpClient())
            {
                var response = await client.SendAsync(request);

                Console.WriteLine(await response.Content.ReadAsStringAsync());

                response.EnsureSuccessStatusCode();

                Console.WriteLine("Done! Now reload your TimetableApp!");
            }
        }

        static List<Dictionary<int, string>> ParseTimetable(string timetableFile, string className, params string[] matches)
        {
            // Here, as the school's excel file is mostly
            // computer-generated, we can assume a few stuff:
            // - The first three rows are headers. The forth row is the class name.
            // - Column A is the name of the day, starting from Monday (Thứ 2).
            // - Column B is the order of the class in the day, starting from 1.
            // - There are no merged cells in our interested area (fuck merged cells).

            var excelFile = File.OpenRead(timetableFile);
            var result = Enumerable.Range(0, 7).Select(i => new Dictionary<int, string>()).ToList();


            using (var reader = ExcelReaderFactory.CreateReader(excelFile, new ExcelReaderConfiguration()
                { FallbackEncoding = Encoding.GetEncoding(1252) }))
            {
                do
                {
                    foreach (var match in matches)
                    {
                        if (reader.Name.Contains(match, StringComparison.InvariantCultureIgnoreCase))
                        {
                            goto good;
                        }
                    }

                    continue;

                good:
                    reader.Read();
                    reader.Read();
                    reader.Read();

                    int classIndex = -1;

                    reader.Read();

                    for (int i = 0; i < reader.FieldCount; ++i)
                    {
                        var str = reader.GetString(i);
                        if (string.IsNullOrEmpty(str))
                        {
                            continue;
                        }
                        if (str.Equals(className, StringComparison.InvariantCultureIgnoreCase))
                        {
                            classIndex = i;
                        }
                    }

                    if (classIndex == -1)
                    {
                        throw new InvalidOperationException("Lớp không tồn tại? Hay bảng lỗi?");
                    }

                    int currentDay = -1;

                    while (reader.Read())
                    {
                        // Column B
                        var dayIndex = int.Parse(reader.GetValue(1)?.ToString() ?? "0");

                        if (dayIndex == 0)
                        {
                            // Should be end of timetable
                            break;
                        }

                        var classString = reader.GetValue(classIndex)?.ToString();

                        if (dayIndex == 1)
                        {
                            ++currentDay;
                            // If someone gets mad and forces students to go
                            // to school on Sunday, our program will always be ready.
                            if (currentDay >= 7)
                            {
                                currentDay -= 7;
                            }
                        }

                        int index = (currentDay + 1) % 7;

                        if (classString == null)
                        {
                            continue;
                        }

                        result[index].Add(dayIndex - 1, classString);
                    }
                    break;
                }
                while (reader.NextResult());
            }

            return result;
        }

        static List<(TimeSpan, TimeSpan)> ParseTime(string timespanFile, IEnumerable<string> matches)
        {
            var excelFile = File.OpenRead(timespanFile);
            var result = new List<(TimeSpan begin, TimeSpan end)>();

            var matchList = matches.ToList();

            using (var reader = ExcelReaderFactory.CreateReader(excelFile, new ExcelReaderConfiguration()
                { FallbackEncoding = Encoding.GetEncoding(1252) }))
            {
                var teacherCount = reader.RowCount - 1;

                do
                {
                    foreach (var match in matchList)
                    {
                        if (reader.Name.Contains(match, StringComparison.InvariantCultureIgnoreCase))
                        {
                            goto good;
                        }
                    }

                    continue;

                good:
                    int startIndex = -1;
                    int endIndex = -1;

                    reader.Read();

                    for (int i = 0; i < reader.FieldCount; ++i)
                    {
                        var str = reader.GetString(i);
                        if (string.IsNullOrEmpty(str))
                        {
                            continue;
                        }
                        if (str.Contains("đầu", StringComparison.InvariantCultureIgnoreCase))
                        {
                            startIndex = i;
                        }
                        else if (str.Contains("kết", StringComparison.InvariantCultureIgnoreCase))
                        {
                            endIndex = i;
                        }
                    }

                    if (startIndex == -1 || endIndex == -1)
                    {
                        throw new InvalidOperationException("Bảng kiểu gì đấy? Bao giờ bắt đầu, bao giờ kết thúc?");
                    }

                    while (reader.Read())
                    {
                        var startString = reader.GetValue(startIndex)?.ToString();
                        var endString = reader.GetValue(endIndex)?.ToString();

                        if (startString == null)
                        {
                            break;
                        }

                        const string regex = "^(\\d+)\\D+(\\d*)";

                        var startMatches = Regex.Match(startString, regex);
                        var endMatches = Regex.Match(endString, regex);

                        if ((!startMatches.Success) || (!endMatches.Success))
                        {
                            throw new InvalidDataException("Lỗi trình bày giờ.");
                        }

                        var startSpan =
                            TimeSpan.FromHours(int.Parse(startMatches.Groups[1].Value))
                            .Add(
                                TimeSpan.FromMinutes(
                                    string.IsNullOrEmpty(startMatches.Groups[2].Value) ?
                                        0 : int.Parse(startMatches.Groups[2].Value)
                                ));

                        var endSpan =
                            TimeSpan.FromHours(int.Parse(endMatches.Groups[1].Value))
                            .Add(
                                TimeSpan.FromMinutes(
                                    string.IsNullOrEmpty(endMatches.Groups[2].Value) ?
                                        0 : int.Parse(endMatches.Groups[2].Value)
                                ));

                        result.Add((startSpan, endSpan));
                    }
                }
                while (reader.NextResult());
            }

            return result;
        }

        static Dictionary<string, Lesson> ParseTeachers(string teachersFile)
        {
            var excelFile = File.OpenRead(teachersFile);
            var dict = new Dictionary<string, Lesson>();

            using (var reader = ExcelReaderFactory.CreateReader(excelFile, new ExcelReaderConfiguration()
            { FallbackEncoding = Encoding.GetEncoding(1252) }))
            {
                var teacherCount = reader.RowCount - 1;

                int subjectIndex = -1;
                int nameIndex = -1;
                int idIndex = -1;
                int passIndex = -1;
                int codeIndex = -1;

                reader.Read();

                for (int i = 0; i < reader.FieldCount; ++i)
                {
                    var str = reader.GetString(i);
                    if (string.IsNullOrEmpty(str))
                    {
                        continue;
                    }
                    if (str.Contains("Môn", StringComparison.InvariantCultureIgnoreCase))
                    {
                        subjectIndex = i;
                    }
                    else if (str.Contains("Tên", StringComparison.InvariantCultureIgnoreCase))
                    {
                        nameIndex = i;
                    }
                    else if (str.Contains("id", StringComparison.InvariantCultureIgnoreCase))
                    {
                        idIndex = i;
                    }
                    else if (str.Contains("pass", StringComparison.InvariantCultureIgnoreCase))
                    {
                        passIndex = i;
                    }
                    else if (str.Contains("mã", StringComparison.InvariantCultureIgnoreCase))
                    {
                        codeIndex = i;
                    }
                }

                if (codeIndex == -1)
                {
                    throw new InvalidOperationException("Đm không có mã biết thế quái lớp nào là lớp nào?");
                }

                while (reader.Read())
                {
                    var lesson = new Lesson();
                    if (subjectIndex != -1)
                    {
                        lesson.Subject = reader.GetValue(subjectIndex)?.ToString();
                    }
                    if (nameIndex != -1)
                    {
                        lesson.TeacherName = reader.GetValue(nameIndex)?.ToString();
                    }

                    var credentials = new ZoomCredentials();
                    if (idIndex != -1)
                    {
                        var rawId = reader.GetValue(idIndex)?.ToString();
                        var strippedId = new string(rawId?.Where(ch => char.IsDigit(ch))?.ToArray() ?? new char[] { });
                        credentials.ID = strippedId;
                    }
                    if (passIndex != -1)
                    {
                        credentials.Password = reader.GetValue(passIndex)?.ToString();
                    }

                    lesson.Credentials = credentials;

                    Console.WriteLine(lesson.ToMarkdown());

                    dict.Add(reader.GetValue(codeIndex)?.ToString(), lesson);
                }
            }

            return dict;
        }

        private static bool Contains(this string str, string arg, StringComparison comp)
        {
            return str.IndexOf(arg, comp) >= 0;
        }
    }
}
