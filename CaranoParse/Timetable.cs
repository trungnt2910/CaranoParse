using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TimetableApp.Core
{
    [Serializable]
    public class Timetable
    {
        #region Private values
        private static readonly int DaysInAWeek = System.Globalization.DateTimeFormatInfo.CurrentInfo.DayNames.Length;
        #endregion

        #region Fields
        //Name: class name, school, id,... for human use.
        public string Name;
        public string UpdateURL;

        //0 is Sunday.
        public List<Lesson>[] Lessons;

        public event EventHandler<EventArgs> OnSucessfulUpdate;
        #endregion

        #region Constructors
        //This class is designed to be used by JSON parsers.
        [JsonConstructor]
        public Timetable() { Lessons = Lessons ?? Enumerable.Range(0, DaysInAWeek).Select(x => new List<Lesson>()).ToArray(); }
        #endregion

        #region Queries
        public Lesson GetCurrentLesson()
        {
            DateTime currentTime = DateTime.Now;
            int day = (int)currentTime.DayOfWeek;
            TimeSpan time = currentTime.TimeOfDay;

            //A typical student cannot have more than 1e5 lessons a day, so yes,
            //brute-forcing is practically acceptable here.

            foreach (var l in Lessons[day])
            {
                if ((l.StartTime <= time) && (time <= l.EndTime))
                {
                    return l;
                }
            }

            //Hooray! No classes for you!
            return null;
        }

        public Lesson GetNextLesson(TimeSpan? MaxDelay)
        {
            DateTime currentTime = DateTime.Now;
            int day = (int)currentTime.DayOfWeek;
            TimeSpan time = currentTime.TimeOfDay;

            for (int i = day; i < day + 7; ++i)
            {
                int dayOfWeek = i % 7;
                foreach (var l in Lessons[dayOfWeek])
                {
                    var startTime = l.StartTime + TimeSpan.FromDays(i - day);
                    if (startTime < time) continue;
                    if (MaxDelay == null)
                    {
                        return l;
                    }
                    else
                    {
                        if (startTime <= time + MaxDelay) return l;
                    }
                }
            }
            

            return null;
        }

        public bool CheckNextLesson(TimeSpan? MaxDelay)
        {
            return GetNextLesson(MaxDelay) != null;
        }
        #endregion

    }
}
